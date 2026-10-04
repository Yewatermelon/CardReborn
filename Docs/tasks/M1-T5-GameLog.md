# 任务卡 · M1-T5 `GameLog` ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T5 ★（★ = 必须通过"无 Unity 依赖"检查） |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-13.3（日志/时间/随机通过抽象注入，内核不直接调用 Unity API）、FR-5.12 / FR-5.13（战斗日志与对局可观测）、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 8 节](../03-开发规范文档.md)（日志规范：`GameLog` 门面 + `LogChannel` 枚举 + 六条规则）、[03 第 5.9.2 节](../03-开发规范文档.md)（`ILogSink` 由外层注入）、[03 第 11.2 节](../03-开发规范文档.md)（边界处降级，不让异常穿透游戏循环）、[05 第 6 节](../05-联网对战_状态同步_设计文档.md)（客户端/权威侧各自注入实现，规则层无改动） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T2（`IEventDispatchFailureSink` 接缝，本任务把它落到 `GameLog`） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供分通道、分等级、可开关的日志门面：规则层只调用它、实现由组合根注入，从而不再触碰 Unity 日志 API，并让 `Card.Server`（Unity 工程外）复用同一份内核源码。

## 3. 范围（做什么）

- `LogLevel` 枚举：`Trace / Info / Warn / Error`。
- `LogChannel` 枚举：`Boot / Config / Match / Rule / Ai / Save / Ui / Perf`（与 03 第 8 节完全一致）。
- `ILogSink` 接口（与 03 第 5.9.2 节一致）：`void Write(LogLevel level, LogChannel channel, string message)`。
- `GameLog` 静态门面（与 03 第 8 节签名一致）
  - `Info` / `Warn` / `Error(channel, message, exception = null)`，另加 `Trace` 与通用 `Write`；
  - `Configure(ILogSink sink, LogLevel minimumLevel)`：组合根一次性注入；
  - `Disable()`：静默（等价于未配置），用于未接线场景与测试隔离；
  - `SetChannelEnabled` / `IsChannelEnabled`：按模块开关（03 第 8 节规则 2）；
  - `IsEnabled(channel, level)`：供热路径先判断再拼字符串（03 第 8 节规则 5）；
  - `MinimumLevel` / `SinkFailureCount` / `LastSinkFailure`：可观测性。
- `EventDispatchLogSink`：把 M1-T2 的 `IEventDispatchFailureSink` 接到 `GameLog.Error`（通道默认 `Match`，可配置），闭合"订阅者异常 → 日志"回路。
- EditMode 测试：等级过滤、通道过滤、开关、未配置时静默、契约校验、sink 抛异常不穿透、热路径判断、桥接端到端。

## 4. 明确不做（防止范围蔓延）

- **不做** Unity 侧 sink（写 Editor 控制台或文件）：按 03 第 5.9.2 节，Unity 版实现放 `Card.Infrastructure`、服务端版放 `Card.Server`，两者在 M11-T3 接线；本任务只交付 Core 的抽象、门面与一个 Core 内可复用的桥接 sink。
- **不做** 日志文件轮转、异步队列、结构化 JSON 输出：属 M10 / M11 的发布与诊断需求。
- **不做** 消息模板/占位符系统：03 第 8 节签名就是字符串 + 通道；热路径由 `IsEnabled` 提前短路。
- **不做** 线程安全：单线程 tick / 主线程约定（与 `EventBus`、`StateMachine`、随机源一致）。
- **不做** 全局可变的服务查找：`GameLog` 只保存一个由组合根注入的 sink，不提供服务解析能力（不是 `ServiceLocator`）。
- **不改** `Tools/check.ps1` 的日志扫描规则：那是 M1-T9；本任务先用独立扫描 + 测试证明 Core 内没有 Unity 日志 API 调用。

## 5. 接口约定

```csharp
namespace Card.Core
{
    public enum LogLevel { Trace, Info, Warn, Error }
    public enum LogChannel { Boot, Config, Match, Rule, Ai, Save, Ui, Perf }

    /// <summary>日志后端；客户端实现写 Editor/控制台，服务端实现写文件或标准输出。</summary>
    public interface ILogSink
    {
        void Write(LogLevel level, LogChannel channel, string message);
    }

    /// <summary>静态日志门面（Docs/03 第 8 节）；状态只能由组合根通过 Configure 注入。</summary>
    public static class GameLog
    {
        public static LogLevel MinimumLevel { get; }
        public static int SinkFailureCount { get; }
        public static System.Exception LastSinkFailure { get; }

        public static void Configure(ILogSink sink, LogLevel minimumLevel);
        public static void Disable();
        public static bool IsDefinedChannel(LogChannel channel);
        public static void SetChannelEnabled(LogChannel channel, bool enabled);
        public static bool IsChannelEnabled(LogChannel channel);
        public static bool IsEnabled(LogChannel channel, LogLevel level);

        public static void Trace(LogChannel channel, string message);
        public static void Info(LogChannel channel, string message);
        public static void Warn(LogChannel channel, string message);
        public static void Error(LogChannel channel, string message, System.Exception exception = null);
        public static void Write(LogLevel level, LogChannel channel, string message, System.Exception exception = null);
    }

    /// <summary>把事件派发失败接到 GameLog（闭合 M1-T2 的接缝）。</summary>
    public sealed class EventDispatchLogSink : IEventDispatchFailureSink
    {
        public EventDispatchLogSink(LogChannel channel = LogChannel.Match);
        public void OnHandlerFailed(EventDispatchFailure failure);
    }
}
```

> **静态门面与铁律 8 的关系**：03 第 8 节明确规定 `GameLog` 为静态门面，05 第 6 节规定"抽象 + 注入实现、规则层无改动"。
> 因此本任务的可变静态状态**只有**一个由组合根注入的 sink 与一份等级/通道配置；`GameLog` 不具备服务解析能力（不是 `ServiceLocator`），
> Domain / Application 只调用、不配置；测试通过 `[SetUp]` 重新 `Disable()` 并恢复通道开关来隔离。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 未配置 sink 时调用任何日志方法 | 不抛异常、不产生任何输出 |
| AC-2 | 配置后记录 `Warn` / `Error` | sink 收到的等级、通道、消息三要素完全一致 |
| AC-3 | 等级过滤 | `MinimumLevel = Warn` 时 `Trace` / `Info` 被过滤，`Warn` / `Error` 通过 |
| AC-4 | `Trace` 等级 | 默认等级下被过滤；`MinimumLevel = Trace` 时通过 |
| AC-5 | 通道开关 | 关闭某通道后只有该通道被过滤；重新打开即恢复 |
| AC-6 | 非法通道 | `SetChannelEnabled` / `EventDispatchLogSink` 构造传未定义枚举值 → 抛 `ArgumentOutOfRangeException` |
| AC-7 | `IsEnabled` | 与"等级 + 通道"两个维度一致（供热路径提前短路） |
| AC-8 | `Disable()` | 停止所有输出；再次 `Configure` 可恢复 |
| AC-9 | `Configure(null, …)` | 抛 `ArgumentNullException`（ParamName=`sink`） |
| AC-10 | 消息为 null / 空白 | 抛 `ArgumentNullException` / `ArgumentException`（不做静默容错） |
| AC-11 | `Error` 带异常 | sink 收到的文本含异常类型与消息（可定位）；不带异常时文本与消息一致 |
| AC-12 | sink 自身抛异常 | 不向上传播；`SinkFailureCount` 递增、`LastSinkFailure` 记录该异常；后续日志仍可用 |
| AC-13 | 事件派发失败桥接 | 订阅者抛异常 → `GameLog.Error(通道, …)`，文本含事件类型与处理器名 |
| AC-14 | 端到端 | `EventBus` + `EventDispatchLogSink` + `GameLog` + 记录型 sink：订阅者抛异常 → 记录一条 Error，其余订阅者照常执行 |
| AC-15 | 端到端（日志关闭） | `Disable()` 后订阅者抛异常：无日志，但异常仍被隔离、`PublishReport.FailureCount` 正确 |
| AC-16 | 内核解耦 | `0_Core` 不出现 Unity 日志 API / `using UnityEngine`（独立扫描 + `check.ps1`），也不使用 `System.Console` |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Core`）
- 最少用例数：20
- 必须覆盖的边界：未配置、等级边界（等于最小等级）、通道开关、未定义通道值、sink 抛异常、消息为 null/空白、重复配置、`Disable` 后恢复、端到端桥接

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无
- 是否影响既有模块：新增一个 `IEventDispatchFailureSink` 实现（M1-T2 的接缝），不改动既有类型

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 156 passed / 0 failed，其中新增 29 例：`GameLogTests` 14 + `GameLogFilterTests` 10 + `EventDispatchLogSinkTests` 5）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 未配置时静默安全 | 已满足 | `Info_WhenNotConfigured_IsSilentAndDoesNotThrow` |
| AC-2 等级/通道/消息三要素透传 | 已满足 | `Warn_WhenConfigured_DeliversLevelChannelAndMessage`、`Error_WhenConfigured_DeliversErrorLevel` |
| AC-3 等级过滤 | 已满足 | `Info_WhenBelowMinimumLevel_IsFiltered` |
| AC-4 `Trace` 开关 | 已满足 | `Trace_WhenMinimumLevelIsInfo_IsFiltered`、`Trace_WhenMinimumLevelIsTrace_IsDelivered` |
| AC-5 通道开关 | 已满足 | `SetChannelEnabled_WhenDisabled_FiltersOnlyThatChannel`、`SetChannelEnabled_WhenReEnabled_DeliversAgain` |
| AC-6 非法通道被拒 | 已满足 | `SetChannelEnabled_WhenChannelIsUndefined_ThrowsArgumentOutOfRangeException`、`Ctor_WhenChannelIsUndefined_ThrowsArgumentOutOfRangeException` |
| AC-7 `IsEnabled` | 已满足 | `IsEnabled_ReflectsLevelAndChannelCombinations` |
| AC-8 `Disable` 与恢复 | 已满足 | `Disable_WhenCalled_StopsAllOutput`、`Configure_AfterDisable_ResumesOutput` |
| AC-9 `Configure(null)` | 已满足 | `Configure_WhenSinkIsNull_ThrowsArgumentNullException` |
| AC-10 消息契约 | 已满足 | `Info_WhenMessageIsNull_ThrowsArgumentNullException`、`Info_WhenMessageIsBlank_ThrowsArgumentException("")/("   ")` |
| AC-11 异常信息可定位 | 已满足 | `Error_WhenExceptionProvided_AppendsExceptionDetails`、`Error_WhenExceptionIsNull_KeepsMessageOnly` |
| AC-12 sink 故障不穿透 | 已满足 | `Write_WhenSinkThrows_DoesNotPropagateAndRecordsFailure`、`Write_WhenSinkThrowsOnce_LaterLogsStillSucceed` |
| AC-13 派发失败桥接 | 已满足 | `OnHandlerFailed_WhenCalled_WritesErrorLogWithEventAndHandler`、`OnHandlerFailed_WhenCustomChannelConfigured_UsesThatChannel` |
| AC-14 端到端日志 | 已满足 | `EndToEnd_WhenSubscriberThrows_FailureIsLoggedAndOtherSubscribersRun` |
| AC-15 端到端（日志关闭） | 已满足 | `EndToEnd_WhenGameLogDisabled_FailureIsStillIsolatedWithoutLog` |
| AC-16 内核解耦 | 已满足 | `check.ps1` PASS + 独立扫描（Unity 日志 API / `using UnityEngine` / `System.Console`）命中 0 |
| 补充 | 已满足 | `IsDefinedChannel_ReflectsEnumValues`、`IsChannelEnabled_WhenChannelIsUndefined_StaysTrue`、`Info_WhenFiltered_DoesNotValidateMessage`、`Configure_WhenCalledTwice_UsesLatestSinkAndLevel` |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`；不使用 `System.Console`（输出完全交给注入的 sink） |
| 4 表现层只读 | 不涉及 | 无表现层改动 |
| 8 禁隐式全局访问 | **需说明** | `GameLog` 是 03 第 8 节明确规定的静态门面，属"抽象 + 注入实现"：可变静态状态只有一个注入的 sink 与等级/通道配置，不具备服务解析能力，Domain / Application 只调用不配置（见第 5 节说明） |
| 9 新功能带测试 | 未命中 | 29 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `GameLog.cs` 162 行；方法均 ≤ 50 行；0 warning（测试文件初版 306 行超限，已拆分为 `GameLogTests` + `GameLogFilterTests`） |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API；日志实现走 `ILogSink` 注入（03 §5.9.2） |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/6/7/12/13） | 不涉及 | 无玩法继承、命令入口、配置数值、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 完全未配置 sink | 不抛、不输出 | 一致 | AC-1 |
| 等级恰好等于最小等级 | 输出（边界含） | 一致 | `Warn_WhenConfigured_...`（`MinimumLevel=Info` 时 `Warn` 通过）、`Info_WhenBelowMinimumLevel_IsFiltered` |
| 通道关闭但等级为 `Error` | 仍然过滤（通道优先） | 一致 | `IsEnabled_ReflectsLevelAndChannelCombinations` |
| 未定义的通道枚举值 | 开关 API 抛 `ArgumentOutOfRangeException`；查询/输出侧视为开启，不丢日志 | 一致 | AC-6、`IsChannelEnabled_WhenChannelIsUndefined_StaysTrue` |
| 被过滤 + 消息为 null | 不校验、不抛（热路径零开销） | 一致 | `Info_WhenFiltered_DoesNotValidateMessage` |
| sink 每次写都抛异常 | 不穿透；计数递增；留档最后异常 | 一致 | AC-12 |
| sink 第一次抛、之后恢复 | 后续日志正常送达 | 一致 | `Write_WhenSinkThrowsOnce_LaterLogsStillSucceed` |
| 日志被 `Disable` 时订阅者抛异常 | 无日志，但隔离与 `PublishReport` 仍正确 | 一致 | AC-15 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'ILogSink'/'LogLevel'/'LogChannel' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=156 passed=156 failed=0`；`warning CS` 0 处 |
| 本次新增 | `GameLogTests` 14 + `GameLogFilterTests` 10 + `EventDispatchLogSinkTests` 5 = 29 |

### 10.5 影响面

- 新增文件：`0_Core/{LogLevel,LogChannel,ILogSink,GameLog,EventDispatchLogSink}.cs` 与 3 个测试文件。
- 改动既有模块：**无**（`EventDispatchLogSink` 是 M1-T2 `IEventDispatchFailureSink` 的新实现，不改动 `EventBus`）。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- 回归风险：低。唯一需要注意的约定是"被过滤的日志不校验参数"与"静态门面需由组合根配置"，两者都已写入类型注释并有测试锁定。M11-T3 接线时将由 `Card.Bootstrap` / `Card.Server` 各自注入 sink。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、156 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

遗留项仍为 M1-R1（启用 `<Nullable>`，P2）、M1-R2（覆盖率统计）、M1-R3（`check.ps1` R1 未剥离注释，并入 M1-T9），按约定 M1 收尾统一处理。
