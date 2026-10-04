# 任务卡 · M1-T8 `IClock` ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T8 ★（★ = 必须通过"无 Unity 依赖"检查） |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-13.3（日志/时间/随机通过抽象注入）、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.9.2 节](../03-开发规范文档.md)（`IClock` 接口签名与"环境依赖注入"要求）、[03 第 5.9.1 节](../03-开发规范文档.md)（禁令 4：禁止直接调用 `DateTime.Now` / Unity 时间）、[05 第 4 节 / 第 6 节](../05-联网对战_状态同步_设计文档.md)（权威 tick 10 Hz、时间走 `IClock`） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | 无（独立小任务；M4 的回合超时与 M13 的重连窗口会用到它） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供逻辑时钟抽象与可手动推进的实现，使"时间"成为可注入、可精确控制的外部依赖：测试不再依赖真实时间，服务端 tick 循环与重放也能确定性地推进对局时间。

## 3. 范围（做什么）

- `IClock`：与 [03 第 5.9.2 节](../03-开发规范文档.md) 签名完全一致的 `int CurrentTick { get; }`。
- `ManualClock : IClock`：手动推进的确定性实现
  - 构造 `(int initialTick = 0)`；
  - `Advance(int ticks)`：生产侧按固定 tick 推进（宿主每 tick 调 `Advance(1)`），负数视为调用方错误；
  - `SetTo(int tick)`：仅用于测试 / 重放 / 快照恢复（生产推进一律走 `Advance`）；
  - 非负约束与**溢出保护**（`int` 溢出会静默回绕成负数，必须显式抛错）。
- EditMode 测试：初始值、推进、回拨、负数与溢出边界、实例隔离、接口多态，以及"注入假时钟的消费者"能确定性到期。

## 4. 明确不做（防止范围蔓延）

- **不做** 真实时间实现（`Time.time` / `Stopwatch` / `DateTime`）：Unity 版实现放 `Card.Infrastructure`、服务端版放 `Card.Server`，两者在 **M11-T3** 接线；Core 只交付接口与确定性实现。
- **不做** 墙上时钟（`DateTime UtcNow`）：存档时间戳属 `SaveService`（Infrastructure），不在规则层；把两种时间塞进一个接口会诱导规则层依赖真实时间。
- **不做** tick 频率换算（10 Hz ↔ 秒）：`IClock` 只表达 tick；换算属宿主/表现层。
- **不做** tick 变化事件（订阅通知）：宿主每 tick 推进一步，消费方直接读 `CurrentTick`；需要通知的场景用 `ReactiveValue<int>` 自行包装。
- **不做** 线程安全与跨线程时间：单线程 tick / 主线程约定。
- **不做** `check.ps1` 的 `DateTime` / Unity 时间扫描规则：那是 **M1-T9**；本任务先用独立扫描取证。
  > 依据 [05 第 688 行](../05-联网对战_状态同步_设计文档.md)：帧同步时代的"禁止 `Time`/`DateTime`"约束已**部分撤销**（规则层建议不依赖时间，但不再作为强制扫描项）。因此本任务不把"扫描强制"写死，只保证 Core 内确实不出现系统时间调用。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>逻辑时间抽象：与真实时间解耦，便于测试与服务端推进。</summary>
    public interface IClock
    {
        int CurrentTick { get; }
    }

    /// <summary>手动推进的逻辑时钟：测试与权威宿主都可驱动。</summary>
    public sealed class ManualClock : IClock
    {
        public ManualClock(int initialTick = 0);

        public int CurrentTick { get; }

        public void Advance(int ticks);
        public void SetTo(int tick);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 无参构造 | `CurrentTick = 0` |
| AC-2 | 指定初始 tick | `CurrentTick` 等于传入值 |
| AC-3 | 负初始 tick | 抛 `ArgumentOutOfRangeException`（ParamName=`initialTick`） |
| AC-4 | `Advance(1)` / `Advance(5)` | tick 按增量推进 |
| AC-5 | `Advance(0)` | 不变化（合法空操作） |
| AC-6 | `Advance(负数)` | 抛 `ArgumentOutOfRangeException`（ParamName=`ticks`），提示改用 `SetTo` |
| AC-7 | 连续推进 | 累加正确 |
| AC-8 | 推进导致 `int` 溢出 | 抛 `InvalidOperationException`（不静默回绕成负数） |
| AC-9 | `SetTo` 前进 / 回拨 | `CurrentTick` 设为绝对值（回拨用于测试与重放） |
| AC-10 | `SetTo(负数)` | 抛 `ArgumentOutOfRangeException`（ParamName=`tick`） |
| AC-11 | 通过 `IClock` 接口使用 | 行为与直接使用一致（多态可用） |
| AC-12 | 两个时钟实例 | 互不影响 |
| AC-13 | 注入假时钟的消费者 | 按 tick 精确到期：未推进不到期、推进到阈值到期、可确定性重复 |
| AC-14 | 内核解耦 | `0_Core` 不出现 `using UnityEngine` / Unity 时间 / `DateTime.Now` / `DateTime.UtcNow` / `System.Console`（`check.ps1` + 独立扫描） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Core`）
- 最少用例数：16
- 必须覆盖的边界：负初始值、负推进量、零推进、溢出、回拨、接口多态、实例隔离、假时钟注入后的确定性

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无（tick 频率属宿主配置，M11 引入）
- 是否影响既有模块：无

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 228 passed / 0 failed，其中新增 18 例：`ManualClockTests` 14 + `ClockInjectionTests` 4）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 默认从 0 开始 | 已满足 | `Ctor_WhenInitialTickOmitted_StartsAtZero` |
| AC-2 指定初始 tick | 已满足 | `Ctor_WhenInitialTickProvided_ExposesIt` |
| AC-3 负初始 tick | 已满足 | `Ctor_WhenInitialTickIsNegative_ThrowsArgumentOutOfRangeException` |
| AC-4 推进 tick | 已满足 | `Advance_WhenCalled_IncrementsCurrentTick` |
| AC-5 零推进 | 已满足 | `Advance_WhenZero_KeepsCurrentTick` |
| AC-6 负推进量 | 已满足 | `Advance_WhenDeltaIsNegative_ThrowsArgumentOutOfRangeException`（错误信息指向 `SetTo`） |
| AC-7 连续推进累加 | 已满足 | `Advance_WhenCalledRepeatedly_Accumulates` |
| AC-8 溢出保护 | 已满足 | `Advance_WhenResultWouldOverflow_ThrowsInvalidOperationException`、`Advance_WhenReachingMaxValueExactly_Succeeds` |
| AC-9 `SetTo` 前进 / 回拨 | 已满足 | `SetTo_WhenForward_SetsAbsoluteTick`、`SetTo_WhenBackward_SetsAbsoluteTick` |
| AC-10 `SetTo` 负值 | 已满足 | `SetTo_WhenTickIsNegative_ThrowsArgumentOutOfRangeException` |
| AC-11 接口多态 | 已满足 | `Clock_WhenUsedThroughInterface_ExposesCurrentTick` |
| AC-12 实例隔离 | 已满足 | `Clocks_WhenTwoInstances_AreIndependent` |
| AC-13 假时钟注入 | 已满足 | `Consumer_WhenClockNotAdvanced_DoesNotExpire`、`Consumer_WhenClockReachesLimit_ExpiresExactlyAtDeadline`、`Consumer_WhenSameSequenceRepeated_IsDeterministic`、`Consumer_WhenClockRewound_RecoversRemainingTicks` |
| AC-14 内核解耦 | 已满足 | `check.ps1` PASS + 精确扫描（`using UnityEngine` / `UnityEngine.` / `DateTime.Now|UtcNow|Today` / Unity `Time.*` / `System.Console`）命中 0 |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System` |
| 4 表现层只读 | 不涉及 | 无表现层改动 |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态 |
| 9 新功能带测试 | 未命中 | 18 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `ManualClock.cs` 66 行、`IClock.cs` 13 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API；不调用系统时间（`DateTime`） |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/6/7/12/13） | 不涉及 | 无玩法继承、命令入口、配置数值、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 负初始 tick / 负推进量 / 负 `SetTo` | 一律 `ArgumentOutOfRangeException`，状态不变 | 一致 | AC-3 / AC-6 / AC-10 |
| 推进 0 | 合法空操作 | 一致 | AC-5 |
| 推进到 `int.MaxValue` 恰好 | 允许 | 一致 | `Advance_WhenReachingMaxValueExactly_Succeeds` |
| 推进导致溢出 | 抛 `InvalidOperationException` 且状态不变（不静默回绕） | 一致 | AC-8 |
| 回拨（`SetTo` 更小值） | 允许，用于测试/重放 | 一致 | `SetTo_WhenBackward_SetsAbsoluteTick` |
| 消费者在差 1 tick 时 | 不到期；再推进 1 tick 才到期 | 一致 | `Consumer_WhenClockReachesLimit_ExpiresExactlyAtDeadline` |
| 同一 tick 序列重复执行 | 结果完全一致（确定性） | 一致 | `Consumer_WhenSameSequenceRepeated_IsDeterministic` |
| 两个时钟实例 | 互不影响 | 一致 | AC-12 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'IClock' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=228 passed=228 failed=0`；`warning CS` 0 处 |
| 本次新增 | `ManualClockTests` 14 + `ClockInjectionTests` 4 = 18 |

### 10.5 影响面

- 新增文件：`0_Core/IClock.cs`、`0_Core/ManualClock.cs` 与 2 个测试文件。
- 改动既有模块：**无**。
- 需要同步的文档 / 配置：无配置表（tick 频率属宿主配置，M11 引入）；`Docs/PROGRESS.md` 已更新。
- 回归风险：低，无调用方；M4 的回合超时与 M13 的重连窗口将注入本接口。
- **扫描器观察（并入 M1-R3）**：本轮我先用了一条粗糙的扫描正则（含 `Time\.`），把 `using System.Runtime.CompilerServices` 里的 "Run**time.**CompilerServices" 误判成 Unity 时间调用。这再次说明 `check.ps1` 的规则需要**词边界 + 剥离注释**，否则容易自伤——M1-T9 一并处理。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、228 用例全过、`check.ps1` PASS、内核零 Unity 依赖且不调用系统时间）；文档已同步。

遗留项仍为 M1-R1（启用 `<Nullable>`，P2）、M1-R2（覆盖率统计）、M1-R3（`check.ps1` 扫描精度：注释与词边界，并入 M1-T9），按约定 M1 收尾统一处理。
