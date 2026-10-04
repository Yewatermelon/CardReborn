# 任务卡 · M1-T6 `ObjectPool<T>`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T6 |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | NFR-2（卡牌视图全程复用对象池，对局中 GC 分配趋近 0）、FR-5.14、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.8 节](../03-开发规范文档.md)（对象池规范：容量上限与超限策略、归还时清理、只池化表现对象）、[03 第 10 节](../03-开发规范文档.md)（性能与内存预算）、[04 §5.F](../04-代码复盘Review规范.md)（高频对象走池）、[00 ADR-09](../00-现状解构与架构再设计.md)（卡牌 UI 用对象池） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T5（`GameLog`：超限策略首次发生时打一次警告） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供一个与 Unity 无关的通用对象池：支持预热、复用、归还清理、闲置上限与扩容上限（超限丢弃并计数、首次告警），并在误用（重复归还、归还外来对象）时立刻报错，避免"池串对象"这类隐蔽缺陷。

## 3. 范围（做什么）

- `IPoolable`：池对象生命周期回调 `OnSpawn()`（借出后）与 `OnDespawn()`（归还时清理状态），对应 03 第 5.8 节第 2 条。
- `ObjectPool<T> where T : class`
  - 构造：`(Func<T> factory, int prewarmCount = 0, int maxSize = 0, int maxCapacity = 0)`；
  - 借出：`Rent()`（超限抛 `InvalidOperationException`）与 `TryRent(out T)`（超限返回 false，供需要降级的调用方）；
  - 归还：`Return(T)` —— 调用 `OnDespawn()` 后纳入闲置列表；重复归还或归还非本池对象立即抛 `InvalidOperationException`；
  - 上限：`maxSize` 为闲置对象上限（超出即丢弃，计入 `DiscardedCount`），`maxCapacity` 为对象总数上限（扩容上限，达到后不再创建）；
  - 观测：`IdleCount` / `ActiveCount` / `CreatedCount` / `DiscardedCount` / `RejectedCount`；
  - `Clear()`：丢弃全部闲置对象，活跃对象不受影响。
- 超限策略留痕（03 第 5.8 节第 3 条）：首次"归还被丢弃"与首次"因上限被拒"各通过 `GameLog.Warn(LogChannel.Perf, …)` 记录一次，之后只累加计数——既有日志，又不违反 03 第 8 节的"禁止疯狂打日志"。
- EditMode 测试：借出 / 归还 / 复用 / 预热 / 两类上限 / 丢弃 / 重复归还 / 外来对象 / 非池化类型 / `Clear` / 工厂契约 / 首次告警。

## 4. 明确不做（防止范围蔓延）

- **不做** Unity 版池（`UnityEngine.Pool`、`Instantiate/Destroy`、prefab 管理）：那是 `Card.Presentation` / `Card.Infrastructure` 的事；本任务交付 Core 的纯逻辑池，可被服务端与测试复用。
- **不做** 线程安全：单线程 tick / 主线程约定（与 `EventBus` / `StateMachine` / `GameLog` 一致）。
- **不做** 自动缩容、定时清理、内存压力感知：当前无需求；需要时由上层在 `Clear()` 之上编排。
- **不做** 池化领域对象：03 第 5.8 节第 4 条明确"领域对象用结构/小对象，靠数量控制而非池化"。
- **不做** `List<T>` 缓冲池包装类型：需要时可让包装类实现 `IPoolable`，或另开任务，避免为假想需求提前抽象。
- **不做** 借出记录的性能特化（如对象头标记）：用引用相等的 `HashSet` 记录在借对象即可，且容量有上限。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>池对象生命周期回调（Docs/03 第 5.8 节）。</summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>通用对象池：预热、复用、归还清理、闲置上限与扩容上限。</summary>
    public sealed class ObjectPool<T> where T : class
    {
        public ObjectPool(Func<T> factory, int prewarmCount = 0, int maxSize = 0, int maxCapacity = 0);

        public int IdleCount { get; }
        public int ActiveCount { get; }
        public int CreatedCount { get; }
        public int DiscardedCount { get; }
        public int RejectedCount { get; }

        public T Rent();
        public bool TryRent(out T item);
        public void Return(T item);
        public void Clear();
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 池空时 `Rent()` | 通过工厂创建新实例；`CreatedCount` 递增 |
| AC-2 | 有闲置对象时 `Rent()` | 复用同一实例，不新建（`CreatedCount` 不变） |
| AC-3 | 借出对象实现 `IPoolable` | 借出后调用一次 `OnSpawn()`；归还时调用一次 `OnDespawn()` |
| AC-4 | 归还后再借出 | 得到同一实例，且已执行过 `OnDespawn()`（状态被清理） |
| AC-5 | 非 `IPoolable` 类型 | 借出/归还正常，不抛异常（池不强制接口） |
| AC-6 | 预热 | 构造时即创建 `prewarmCount` 个闲置对象，`IdleCount` 等于该值 |
| AC-7 | `factory` 为 null | 抛 `ArgumentNullException`（ParamName=`factory`） |
| AC-8 | 工厂返回 null | 抛 `InvalidOperationException`（不把 null 当对象缓存） |
| AC-9 | `prewarmCount < 0` | 抛 `ArgumentOutOfRangeException` |
| AC-10 | `prewarmCount > maxCapacity`（且 `maxCapacity > 0`） | 抛 `ArgumentException`（配置矛盾立刻暴露） |
| AC-11 | 计数 | `IdleCount` / `ActiveCount` / `CreatedCount` 随借出与归还正确变化 |
| AC-12 | 重复归还同一对象 | 抛 `InvalidOperationException`，且池状态不被破坏（后续借出仍正常） |
| AC-13 | 归还非本池对象 | 抛 `InvalidOperationException` |
| AC-14 | `Return(null)` | 抛 `ArgumentNullException`（ParamName=`item`） |
| AC-15 | 闲置数达 `maxSize` 后继续归还 | 丢弃该对象、`DiscardedCount` 递增、`IdleCount` 保持 `maxSize`；后续 `Rent()` 仍可创建新对象 |
| AC-16 | 达到 `maxCapacity` 后 `Rent()` | 抛 `InvalidOperationException`；`TryRent(out)` 返回 false 且不创建对象、`RejectedCount` 递增 |
| AC-17 | `maxCapacity = 0` / `maxSize = 0` | 表示不限制（只受另一方约束） |
| AC-18 | `Clear()` | 闲置对象被丢弃（`IdleCount = 0`），活跃对象不受影响；再次 `Rent()` 会新建 |
| AC-19 | 超限策略留痕 | 首次丢弃 / 首次拒绝各通过 `GameLog.Warn(LogChannel.Perf, …)` 记录一次；重复发生只累加计数 |
| AC-20 | `GameLog` 关闭时 | 计数照常，日志无输出、不抛异常 |
| AC-21 | 内核解耦 | `0_Core` 不出现 Unity API / `System.Console`（`check.ps1` + 独立扫描） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Core`）
- 最少用例数：22
- 必须覆盖的边界：空池、预热、复用、两类上限（闲置上限 / 扩容上限）、丢弃、重复归还、外来对象、工厂返回 null、负数预热、`Clear` 后复用、非池化类型

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无（池容量由上层在 M5 接线时传入）
- 是否影响既有模块：无（复用 `GameLog`）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 185 passed / 0 failed，其中新增 29 例：`ObjectPoolTests` 16 + `ObjectPoolCapacityTests` 13）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 空池借出即创建 | 已满足 | `Rent_WhenPoolIsEmpty_CreatesItemThroughFactory` |
| AC-2 复用闲置对象 | 已满足 | `Rent_WhenIdleItemAvailable_ReusesSameInstance` |
| AC-3 `IPoolable` 回调 | 已满足 | `Rent_WhenItemIsPoolable_CallsOnSpawnOnce`、`Return_WhenItemIsPoolable_CallsOnDespawnAndClearsState` |
| AC-4 归还后状态已清理 | 已满足 | `Return_ThenRent_ReturnsSameInstanceWithCleanState` |
| AC-5 非池化类型可用 | 已满足 | `Rent_WhenItemIsNotPoolable_WorksWithoutCallbacks` |
| AC-6 预热 | 已满足 | `Ctor_WhenPrewarmCountProvided_CreatesIdleItemsUpFront` |
| AC-7 工厂为 null | 已满足 | `Ctor_WhenFactoryIsNull_ThrowsArgumentNullException` |
| AC-8 工厂返回 null | 已满足 | `Factory_WhenReturnsNull_ThrowsInvalidOperationException` |
| AC-9 负数预热 | 已满足 | `Ctor_WhenPrewarmCountIsNegative_ThrowsArgumentOutOfRangeException` |
| AC-10 预热超过扩容上限 | 已满足 | `Ctor_WhenPrewarmExceedsCapacity_ThrowsArgumentException`（且不产生半成品池） |
| AC-11 计数一致 | 已满足 | `Counts_WhenRentingAndReturning_StayConsistent` |
| AC-12 重复归还 | 已满足 | `Return_WhenSameItemReturnedTwice_ThrowsInvalidOperationException`、`Return_WhenDoubleReturnAttempted_PoolStaysUsable` |
| AC-13 归还外来对象 | 已满足 | `Return_WhenItemWasNotRentedFromThisPool_ThrowsInvalidOperationException` |
| AC-14 `Return(null)` | 已满足 | `Return_WhenItemIsNull_ThrowsArgumentNullException` |
| AC-15 闲置上限丢弃 | 已满足 | `Return_WhenIdlePoolIsFull_DiscardsItemAndCounts`、`Return_WhenIdlePoolOverflows_RentStillCreatesNewItems` |
| AC-16 扩容上限 | 已满足 | `Rent_WhenAtMaxCapacity_ThrowsInvalidOperationException`、`TryRent_WhenAtMaxCapacity_ReturnsFalseWithoutCreating` |
| AC-17 上限 0 = 不限 | 已满足 | `MaxCapacity_WhenZero_IsUnlimited`、`MaxSize_WhenZero_KeepsAllReturnedItems` |
| AC-18 `Clear` | 已满足 | `Clear_WhenCalled_DropsIdleItemsOnly`、`Clear_ThenRent_CreatesNewInstance` |
| AC-19 超限策略留痕 | 已满足 | `Discard_WhenFirstOccurs_LogsWarningOnce`、`Rent_WhenFirstRejected_LogsWarningOnce` |
| AC-20 `GameLog` 关闭时仍计数 | 已满足 | `Discard_WhenGameLogDisabled_CountsWithoutLogging` |
| AC-21 内核解耦 | 已满足 | `check.ps1` PASS + 独立扫描命中 0 |
| 补充 | 已满足 | `TryRent_WhenCapacityAvailable_ReturnsItem`、`TryRent_WhenItemReturned_ReusesInsteadOfRejecting` |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`、`System.Collections.Generic`、`System.Runtime.CompilerServices`（`RuntimeHelpers`） |
| 4 表现层只读 | 未命中 | 池只管理对象生命周期，不读写游戏状态 |
| 6 数值来自配置 | 不涉及 | 池容量由上层在 M5 接线时传入，无硬编码玩法数值 |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态；`ReferenceComparer.Instance` 是无状态单例常量（不可变） |
| 9 新功能带测试 | 未命中 | 29 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `ObjectPool.cs` 207 行；最长方法 `TryRent` 约 30 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API；池化对象通过 `IPoolable` 回调，不依赖 MonoBehaviour |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/7/12/13） | 不涉及 | 无玩法继承、命令入口、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 池空时借出 | 新建对象并调用 `OnSpawn` | 一致 | AC-1 / AC-3 |
| 重复归还同一对象 | 抛异常且池不被污染（后续仍可用） | 一致 | AC-12 |
| 归还外来对象 | 抛异常，不进入闲置列表 | 一致 | AC-13 |
| 闲置数达上限后归还 | 丢弃 + 计数 + 首次 Warn；保留的是最早归还者 | 一致 | AC-15、AC-19 |
| 达到扩容上限后借出 | `Rent` 抛异常 / `TryRent` 返回 false 且不创建 | 一致 | AC-16 |
| 上限传 0 | 视为不限制 | 一致 | AC-17 |
| 工厂返回 null | 抛 `InvalidOperationException`（不缓存 null） | 一致 | AC-8 |
| 预热数 > 扩容上限 | 构造即抛 `ArgumentException`，不产生半成品池 | 一致 | AC-10 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'IPoolable' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=185 passed=185 failed=0`；`warning CS` 0 处 |
| 本次新增 | `ObjectPoolTests` 16 + `ObjectPoolCapacityTests` 13 = 29 |

### 10.5 影响面

- 新增文件：`0_Core/IPoolable.cs`、`0_Core/ObjectPool.cs` 与 3 个测试文件。
- 改动既有模块：**无**（复用 M1-T5 的 `GameLog` 记录超限首次告警）。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- 回归风险：低。M5-T3（卡牌视图全部走池）将接入本类型；届时需要提供容量参数（属表现层配置）。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、185 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

遗留项仍为 M1-R1（启用 `<Nullable>`，P2）、M1-R2（覆盖率统计）、M1-R3（`check.ps1` R1 未剥离注释，并入 M1-T9），按约定 M1 收尾统一处理。
