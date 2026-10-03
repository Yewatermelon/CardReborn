# 任务卡 · M1-T2 `EventBus`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T2 |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-13.3（环境依赖可注入）、FR-13.7（事件流完整，事件顺序与结算顺序一致）、FR-5.14（规则纯逻辑可测）、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.4 节](../03-开发规范文档.md)（事件总线：逻辑与表现的唯一桥梁）、[03 第 5.9 节](../03-开发规范文档.md)（内核解耦）、[00 第 4.1 节](../00-现状解构与架构再设计.md)（Core 含 `EventBus`；`IEventBus`/`GameEvent` 属 Domain） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T1（`Result` / `Guard`，已用其 `Guard.NotNull` 做入参校验） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供一个类型安全、可解绑、异常隔离的事件总线，作为"规则层产出事实 → 表现层消费"的唯一通道，且不依赖任何 Unity API。

## 3. 范围（做什么）

- `EventBus`：`Subscribe<TEvent>(Action<TEvent>)` 返回 `IDisposable`；`Publish<TEvent>(TEvent)` 返回派发统计；`SubscriberCount<TEvent>()` 供诊断与测试。
- 派发语义（这是本任务的核心，必须写进测试）：
  - 类型安全：只有同类型订阅者被调用；
  - 顺序：按订阅先后调用；
  - 派发期间 **新增** 的订阅不参与本次派发，下次生效；
  - 派发期间 **解绑** 的订阅若尚未调用则跳过，下次同样不调用；
  - 重入（handler 内再次 `Publish` 同类型事件）不破坏迭代；
  - 订阅者抛异常 **被隔离**：记录后继续派发其余订阅者，并计入报告。
- 异常上报接缝 `IEventDispatchFailureSink` + 失败记录 `EventDispatchFailure`（供 M1-T5 的 `GameLog` 接入，避免"静默吞异常"）。
- `PublishReport`：本次调用的订阅者数与失败数。
- EditMode 单元测试覆盖上述全部语义与契约边界。

## 4. 明确不做（防止范围蔓延）

- **不做** `IEventBus`（带 `where TEvent : IGameEvent` 约束的接口）与 `GameEvent` 基类型：按 [00 第 4.1 节](../00-现状解构与架构再设计.md) 二者属 **Domain 层**，Core 不能反向依赖；Domain 侧适配在 M3/M4 随事件模型一起落地。本任务只交付 Core 的通用总线（无约束泛型）。
- **不做线程安全**：架构约定单线程（服务器 tick / 主线程），故不加锁、不用并发容器；此约束写进类型注释。
- **不做** 事件排队与批量刷新：03 §5.4 第 2 条要求"先结算完状态，再统一发布"，这是 **Application 层** 的调用纪律（`Apply` 结束后批量 `Publish`），不属于总线职责。
- **不做** 订阅优先级、弱引用订阅、通配符订阅（文档签名未要求；View 必须显式 `Dispose`）。
- **不做** 事件的序列化 / 网络下发（阶段二，见 Docs/05）。
- **不接** `GameLog`（M1-T5）：只用 `IEventDispatchFailureSink` 预留接缝。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>订阅者异常被隔离后的失败记录。</summary>
    public readonly struct EventDispatchFailure
    {
        public EventDispatchFailure(System.Type eventType, string handlerName, System.Exception exception);
        public System.Type EventType { get; }
        public string HandlerName { get; }
        public System.Exception Exception { get; }
    }

    /// <summary>失败上报出口；由外层接到 GameLog（M1-T5）。</summary>
    public interface IEventDispatchFailureSink
    {
        void OnHandlerFailed(EventDispatchFailure failure);
    }

    /// <summary>一次派发的统计结果。</summary>
    public readonly struct PublishReport
    {
        public PublishReport(int handlerCount, int failureCount);
        public int HandlerCount { get; }   // 本次实际调用的订阅者数（含抛异常者）
        public int FailureCount { get; }   // 被隔离的异常数
        public bool HasFailures { get; }
    }

    /// <summary>类型安全的事件总线（非线程安全，单线程使用）。</summary>
    public sealed class EventBus
    {
        public EventBus();
        public EventBus(IEventDispatchFailureSink failureSink);

        public System.IDisposable Subscribe<TEvent>(System.Action<TEvent> handler);
        public PublishReport Publish<TEvent>(TEvent evt);
        public int SubscriberCount<TEvent>();
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 订阅并发布同类型事件 | 订阅者被调用一次，事件内容一致 |
| AC-2 | 发布时存在多个订阅者 | 按订阅顺序全部调用（顺序被断言） |
| AC-3 | 发布其他类型事件 | 该类型的订阅者不被调用（类型安全） |
| AC-4 | 发布时无订阅者 | 不抛异常，`HandlerCount=0`、`HasFailures=false` |
| AC-5 | `Dispose` 后发布 | 不再被调用，`SubscriberCount` 归零 |
| AC-6 | 对同一 token 重复 `Dispose` | 幂等：不抛异常、不影响其他订阅者 |
| AC-7 | 同一 handler 订阅两次 | 两个 token 各自独立，调用两次；Dispose 其一后只调用一次 |
| AC-8 | 订阅者抛异常 | 其余订阅者仍被调用；`FailureCount=1`、`HasFailures=true` |
| AC-9 | 存在失败出口时抛异常 | 出口收到 `EventDispatchFailure`（事件类型、处理器名、异常实例正确） |
| AC-10 | 无失败出口时抛异常 | 不抛到调用方，仅体现在 `FailureCount` |
| AC-11 | 派发中解绑尚未被调用的订阅者 | 本次不再调用它；后续发布也不调用 |
| AC-12 | 派发中新增订阅 | 新订阅不参与本次派发；下一次发布生效 |
| AC-13 | handler 内重入 `Publish` 同类型事件 | 内层完整派发，外层继续；总调用次数正确、无死循环 |
| AC-14 | `handler` 为 null | 抛 `ArgumentNullException`（ParamName=`handler`） |
| AC-15 | 引用类型事件为 null | 抛 `ArgumentNullException` |
| AC-16 | 值类型事件的 `default` 值 | 允许发布，订阅者被调用 |
| AC-17 | 两个 `EventBus` 实例 | 订阅互不可见（无静态共享状态） |
| AC-18 | `SubscriberCount` | 订阅/解绑后数量正确，且不把已解绑的算作活跃 |
| AC-19 | 内核解耦 | 不出现 `UnityEngine` / `Debug.Log` / `Mathf` / `DateTime.Now`（`check.ps1` + 独立扫描） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）
- 最少用例数：20
- 必须覆盖的边界：无订阅者、重复 Dispose、派发中增删订阅、重入发布、null handler、null 事件（引用类型）、值类型默认值事件、多实例隔离、异常隔离顺序

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M1-T2 状态与证据）、本任务卡回填
- 需更新的配置表：无
- 是否影响既有模块：无（新增文件，复用 M1-T1 的 `Guard`）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 83 passed / 0 failed，其中新增 22 例：`EventBusTests` 16 + `EventBusDispatchRuleTests` 6）
- [x] 编译 0 error / 0 warning（批处理日志中 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-03）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 订阅并发布同类型事件 | 已满足 | `Subscribe_WhenPublishMatchingEvent_InvokesHandlerWithSameEvent` |
| AC-2 多订阅者按序调用 | 已满足 | `Publish_WhenMultipleHandlers_InvokesInSubscriptionOrder` |
| AC-3 类型安全 | 已满足 | `Publish_WhenOtherEventType_DoesNotInvokeSubscribers` |
| AC-4 无订阅者发布 | 已满足 | `Publish_WhenNoSubscribers_ReturnsEmptyReport` |
| AC-5 Dispose 后不再投递 | 已满足 | `Dispose_WhenCalledAfterSubscribe_StopsDelivery` |
| AC-6 重复 Dispose 幂等 | 已满足 | `Dispose_WhenCalledTwice_IsIdempotent` |
| AC-7 同一 handler 两次订阅各自独立 | 已满足 | `Subscribe_WhenSameHandlerTwice_TokensAreIndependent` |
| AC-8 异常隔离且继续派发 | 已满足 | `Publish_WhenHandlerThrows_IsolatesFailureAndContinues`、`Publish_WhenTwoHandlersThrow_CountsBothFailures` |
| AC-9 失败上报出口 | 已满足 | `Publish_WhenHandlerThrows_ReportsFailureToSinkAndKeepsSubscription` |
| AC-10 无出口时仅计数 | 已满足 | `Publish_WhenHandlerThrowsWithoutSink_OnlyReportsFailureCount` |
| AC-11 派发中解绑未调用的订阅者 | 已满足 | `Publish_WhenHandlerDisposesLaterSubscription_SkipsItInCurrentDispatch`、`Publish_WhenHandlerDisposesItself_DoesNotAffectOtherSubscribers`、`Publish_WhenHandlersThrowAndDispose_KeepsRemainingOrderIntact` |
| AC-12 派发中新增订阅下次生效 | 已满足 | `Publish_WhenHandlerSubscribesDuringDispatch_NewSubscriberWaitsForNextPublish` |
| AC-13 重入发布 | 已满足 | `Publish_WhenHandlerPublishesSameEventType_ReentrantDispatchCompletes`、`Publish_WhenHandlerPublishesOtherEventType_BothDispatchesComplete` |
| AC-14 null handler | 已满足 | `Subscribe_WhenHandlerIsNull_ThrowsArgumentNullException` |
| AC-15 引用类型事件为 null | 已满足 | `Publish_WhenReferenceTypeEventIsNull_ThrowsArgumentNullException` |
| AC-16 值类型默认值事件 | 已满足 | `Publish_WhenValueTypeEventIsDefault_InvokesHandler` |
| AC-17 多实例隔离 | 已满足 | `Bus_WhenTwoInstances_DoNotShareSubscriptions` |
| AC-18 `SubscriberCount` | 已满足 | `SubscriberCount_WhenSubscribedAndDisposed_CountsActiveOnly`、`Dispose_WhenCalledTwice_IsIdempotent` |
| AC-19 内核零 Unity 依赖 | 已满足 | `check.ps1` R1 PASS；独立扫描 `UnityEngine|Debug.|Mathf|JsonUtility|DateTime.Now|ScriptableObject` 命中 0 |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`、`System.Collections.Generic`；复用 M1-T1 的 `Guard` |
| 4 表现层只读 | 未命中 | 总线只投递事件，不反向读写状态；"处理器内禁止改状态"写入类型注释（03 §5.4 第 5 条） |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态；实例隔离由 `Bus_WhenTwoInstances_DoNotShareSubscriptions` 证明 |
| 9 新功能带测试 | 未命中 | 22 个新用例，先红（CS0246）后绿 |
| 10 行数/复杂度/0 warning | 未命中 | `EventBus.cs` 203 行；最长方法 `Publish` 约 45 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API、无 `DateTime.Now`；日志能力走 `IEventDispatchFailureSink` 接缝 |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/6/7/12/13） | 不涉及 | 不涉及玩法继承、命令入口、配置数值、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 无订阅者时发布 | 不抛、报告为 0 | 一致 | AC-4 |
| 派发中解绑后继订阅者 | 本次跳过、后续也不调用 | 一致 | AC-11 三例 |
| 派发中新增订阅 | 本次不参与、下次生效 | 一致 | AC-12 |
| handler 内重入发布同类型事件 | 内层完整、外层继续、无死循环 | 一致 | AC-13 |
| 订阅者抛异常 | 隔离、其余照常、计数 +1、不自动解绑 | 一致 | AC-8 / AC-9 |
| 引用类型事件为 null | 抛 `ArgumentNullException` | 一致 | AC-15 |
| handler 为 null | 抛 `ArgumentNullException`（ParamName=`handler`） | 一致 | AC-14 |
| 值类型事件取 `default` | 合法，正常投递 | 一致 | AC-16 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试，副本无实现） | `CS0246 'IEventDispatchFailureSink'/'EventDispatchFailure'/'EventBus' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=83 passed=83 failed=0 duration=0.134s`；`warning CS` 0 处 |
| 本次新增 | `EventBusTests` 16 + `EventBusDispatchRuleTests` 6 = 22 |

### 10.5 影响面

- 新增文件：`0_Core/{EventBus,EventDispatchFailure,IEventDispatchFailureSink,PublishReport}.cs` 与 3 个测试文件。
- 改动既有模块：**无**（仅复用 M1-T1 的 `Guard`）。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- 回归风险：低。`EventBus` 目前无调用方；唯一需要注意的约定是"单线程使用"与"派发期间不迭代原集合"，两者都已写入类型注释并有测试锁定。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、83 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

无新增待办。遗留项仍为 M1-R1（启用 `<Nullable>`，P2）、M1-R2（接入覆盖率统计，M1 门禁要求 Core ≥ 90%），按约定在 M1 收尾时统一处理。
