# 任务卡 · M1-T7 `ReactiveValue<T>`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T7 |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-5.14、FR-8.x（表现层订阅数据变化驱动渲染）、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.4 节](../03-开发规范文档.md)（订阅可解绑、订阅者异常隔离）、[03 第 5.6 节](../03-开发规范文档.md)（表现层只读订阅）、[00 第 4.1 节](../00-现状解构与架构再设计.md)（Core 含 `ReactiveValue<T>`） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T1（`Guard`）、M1-T2（`IDisposable` 订阅语义与 `IEventDispatchFailureSink` 失败上报口径） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供一个可观察值：值真正变化时按订阅顺序通知订阅者（供 View 只读订阅），相同值不打扰任何人，且订阅生命周期与异常隔离规则与 `EventBus` 完全一致。

## 3. 范围（做什么）

- `ReactiveValue<T>`
  - 构造：`(T initialValue)` 或 `(T initialValue, IEventDispatchFailureSink failureSink)`；
  - 读：只读属性 `Value`（写入口是 `Set`，这样调用方能拿到"是否真的变了"的返回值）；
  - 写：`bool Set(T value)` —— 值相同返回 false 且**不通知**；不同则更新并按订阅顺序通知，返回 true；
  - 订阅：`IDisposable Subscribe(Action<T> handler, bool notifyWithCurrentValue = false)`；解绑幂等；
  - 观测：`SubscriberCount`、`NotificationFailureCount`。
- 通知语义（与 `EventBus` 对齐，逐条有测试）
  - 派发期间新增的订阅不参与本次通知；派发期间解绑且尚未通知的订阅者被跳过；
  - **通知过程中值再次变化时，旧派发中止剩余订阅者**，由新派发接管——避免 UI 收到过期值；
  - 单个订阅者抛异常被隔离（记录后继续），并计入 `NotificationFailureCount`；有失败出口时上报。
- EditMode 测试：变更触发、相同值不触发、取消订阅、立即通知、异常隔离、重入写入、实例隔离。

## 4. 明确不做（防止范围蔓延）

- **不做** 值组合/派生（`Computed`、`Linq` 式的 `Where/Select`）：需要时在上层编排，避免在 Core 里长出一个响应式框架。
- **不做** 节流/防抖/批合并：UI 刷新节奏由表现层决定，M5 需要时另开任务。
- **不做** 线程安全与跨线程通知：单线程 tick / 主线程约定。
- **不做** 自动解绑（弱引用订阅）：订阅者必须显式 `Dispose`（与 03 第 5.4 节第 3 条一致）。
- **不做** `IObservable<T>` / `INotifyPropertyChanged` 适配：若 M5 需要，属表现层适配层的事。
- **不做** 可变集合的深度观察（`ReactiveList<T>` 之类）：当前只需要标量值。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>可观察的标量值：变化时通知订阅者（供 View 只读订阅）。</summary>
    public sealed class ReactiveValue<T>
    {
        public ReactiveValue(T initialValue);
        public ReactiveValue(T initialValue, IEventDispatchFailureSink failureSink);

        public T Value { get; }
        public int SubscriberCount { get; }
        public int NotificationFailureCount { get; }

        /// <summary>值变化时更新并通知；未变化返回 false 且不通知。</summary>
        public bool Set(T value);

        /// <summary>订阅变更；返回的 IDisposable 用于解绑（幂等）。</summary>
        public System.IDisposable Subscribe(System.Action<T> handler, bool notifyWithCurrentValue = false);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 构造后读取 | `Value` 等于初始值，`SubscriberCount = 0` |
| AC-2 | 值变化时 `Set` | 订阅者收到新值，`Set` 返回 true |
| AC-3 | 值相同（`int` / `string` / 结构体） | 不通知任何订阅者，`Set` 返回 false |
| AC-4 | 引用类型值设为 null | 视为一次变化（null 是合法值） |
| AC-5 | 多个订阅者 | 按订阅顺序全部收到通知 |
| AC-6 | `Subscribe`（默认） | 订阅瞬间**不**通知；只有后续变化才通知 |
| AC-7 | `Subscribe(..., notifyWithCurrentValue: true)` | 订阅瞬间以当前值通知一次 |
| AC-8 | `Dispose` 之后 | 不再收到通知；`SubscriberCount` 减少；重复 `Dispose` 幂等 |
| AC-9 | 同一 handler 订阅两次 | 两个 token 独立，通知两次；解绑其一后只通知一次 |
| AC-10 | 无订阅者时 `Set` | 不抛异常，值仍更新 |
| AC-11 | 订阅者抛异常 | 其余订阅者仍被通知；`NotificationFailureCount` 递增；值已更新 |
| AC-12 | 有失败出口时抛异常 | 出口收到 `EventDispatchFailure`（类型、处理器名、异常实例正确） |
| AC-13 | 立即通知时 handler 抛异常 | 异常被隔离，`Subscribe` 仍返回可用的订阅句柄 |
| AC-14 | 派发期间新增订阅 | 新订阅不参与本次通知，后续变化才通知 |
| AC-15 | 派发期间解绑尚未通知的订阅者 | 该订阅者本次被跳过 |
| AC-16 | 派发期间某个 handler 再次 `Set` 新值 | 旧派发中止剩余订阅者（不收到过期值），新派发完成，最终持有最新值 |
| AC-17 | 两个 `ReactiveValue` 实例 | 互不影响 |
| AC-18 | 内核解耦 | `0_Core` 不出现 Unity API / `System.Console`（`check.ps1` + 独立扫描） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Core`）
- 最少用例数：20
- 必须覆盖的边界：相同值不触发、引用类型 null、结构体默认值、重复 Dispose、派发期间增删订阅、重入写入、立即通知时抛异常、无订阅者

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无
- 是否影响既有模块：无（复用 `Guard` / `IEventDispatchFailureSink`）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 210 passed / 0 failed，其中新增 25 例：`ReactiveValueTests` 16 + `ReactiveValueDispatchTests` 9）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 初始值 / 无订阅者 | 已满足 | `Ctor_WhenCreated_ExposesInitialValueAndNoSubscribers` |
| AC-2 变化触发通知 | 已满足 | `Set_WhenValueChanges_NotifiesSubscriberWithNewValue` |
| AC-3 相同值不触发 | 已满足 | `Set_WhenValueIsEqual_DoesNotNotifyAndReturnsFalse`、`Set_WhenStringValueIsEqual_DoesNotNotify`、`Set_WhenStructValueIsEqual_DoesNotNotify` |
| AC-4 引用类型 null 视为变化 | 已满足 | `Set_WhenReferenceValueBecomesNull_IsTreatedAsChange` |
| AC-5 多订阅者按序通知 | 已满足 | `Set_WhenMultipleSubscribers_NotifiesInSubscriptionOrder` |
| AC-6 默认不立即通知 | 已满足 | `Subscribe_WhenCalled_DoesNotNotifyImmediately` |
| AC-7 可选的立即通知 | 已满足 | `Subscribe_WhenNotifyWithCurrentValue_InvokesHandlerImmediately` |
| AC-8 解绑与幂等 | 已满足 | `Dispose_WhenCalled_StopsNotifications`、`Dispose_WhenCalledTwice_IsIdempotent`、`SubscriberCount_ReflectsActiveSubscriptions` |
| AC-9 同一 handler 两次订阅 | 已满足 | `Subscribe_WhenSameHandlerTwice_TokensAreIndependent` |
| AC-10 无订阅者时 `Set` | 已满足 | `Set_WhenNoSubscribers_UpdatesValueWithoutThrowing` |
| AC-11 异常隔离 | 已满足 | `Set_WhenHandlerThrows_IsolatesFailureAndContinues`、`Set_WhenNoSink_StillCountsFailures` |
| AC-12 失败上报出口 | 已满足 | `Set_WhenHandlerThrows_ReportsFailureToSink` |
| AC-13 立即通知时异常 | 已满足 | `Subscribe_WhenImmediateNotifyThrows_IsolatesAndKeepsSubscription` |
| AC-14 派发期间新增订阅 | 已满足 | `Set_WhenHandlerSubscribesDuringDispatch_NewSubscriberWaitsForNextChange` |
| AC-15 派发期间解绑 | 已满足 | `Set_WhenHandlerDisposesLaterSubscription_SkipsIt` |
| AC-16 派发期间再次 `Set` | 已满足 | `Set_WhenHandlerSetsNewValue_RemainingHandlersSkipStaleValue`、`Set_WhenHandlerSetsEqualValue_DoesNotRecurse` |
| AC-17 实例隔离 | 已满足 | `Values_WhenTwoInstances_AreIndependent` |
| AC-18 内核解耦 | 已满足 | `check.ps1` PASS + 独立扫描命中 0 |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`、`System.Collections.Generic` |
| 4 表现层只读 | 未命中 | 只做"通知"，不反向读写任何状态；View 只能读 `Value` |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态，实例隔离有测试 |
| 9 新功能带测试 | 未命中 | 25 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `ReactiveValue.cs` 204 行；最长方法 `Notify` 约 30 行；0 warning（初稿有一处 XML 注释笔误已修复） |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API；异常上报走 M1-T2 的 `IEventDispatchFailureSink` |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/6/7/12/13） | 不涉及 | 无玩法继承、命令入口、配置数值、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 赋相同值 | 不通知、返回 false | 一致 | AC-3（含 string 与结构体） |
| 引用类型赋 null | 视为变化并通知 | 一致 | AC-4 |
| 结构体默认值 | 按值比较，相同不通知 | 一致 | `Set_WhenStructValueIsEqual_DoesNotNotify` |
| 派发中新增订阅 | 本次不通知，下次生效 | 一致 | AC-14 |
| 派发中解绑后继订阅者 | 本次跳过 | 一致 | AC-15 |
| 派发中再次 `Set` 新值 | 旧派发中止剩余订阅者，最终值为最新 | 一致 | AC-16 |
| 派发中 `Set` 相同值 | 不递归 | 一致 | `Set_WhenHandlerSetsEqualValue_DoesNotRecurse` |
| 订阅者抛异常 | 隔离、计数、值仍更新；立即通知路径同样隔离 | 一致 | AC-11 / AC-13 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'ReactiveValue<>' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=210 passed=210 failed=0`；`warning CS` 0 处 |
| 本次新增 | `ReactiveValueTests` 16 + `ReactiveValueDispatchTests` 9 = 25 |

### 10.5 影响面

- 新增文件：`0_Core/ReactiveValue.cs` 与 2 个测试文件。
- 改动既有模块：**无**（复用 `Guard` 与 M1-T2 的失败上报口径）。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- 回归风险：低，无调用方；M5 的 `ManaView` 等将用 `Subscribe(..., notifyWithCurrentValue: true)` 做首次渲染。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、210 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

遗留项仍为 M1-R1（启用 `<Nullable>`，P2）、M1-R2（覆盖率统计）、M1-R3（`check.ps1` R1 未剥离注释，并入 M1-T9），按约定 M1 收尾统一处理。
