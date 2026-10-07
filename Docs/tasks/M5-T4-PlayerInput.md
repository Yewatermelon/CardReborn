# 任务卡 · M5-T4 `PlayerInputController`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T4 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.1（视图只读）、03 §5.6（输入→`GameCommand`，不判规则）、U-9（输入抽象） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T4：点击/拖拽 → `GameCommand`；完成标准：只发命令；非法命令由 RuleEngine 拒绝并提示） |
| 预估 | 1 会话 |
| 依赖任务 | M5-T3（视图池化）、M4-T2（`MatchController.Submit`/`CommandResult`）、M3-T4（`GameCommand` 家族） |

## 2. 目标（一句话）

> 交付 `PlayerInputController`：把手牌点击/战场点击/英雄点击/技能点击/结束回合按钮等玩家意图翻译成 `GameCommand` 提交给 `MatchController.Submit`；控制器自身**不判断任何规则**，非法命令经 `RuleEngine` 拒绝后把 `CommandError` 通过只读事件透出给提示 UI。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/Input/`）

1. **`ICommandSink`（接口）** — 命令出口抽象：`CommandResult Submit(IGameCommand command)`。生产环境由 `MatchController.Submit` 适配；测试用 stub 断言收到的命令序列。**作用**：让 `PlayerInputController` 不依赖 `Card.Application` 的具体控制器，符合铁律 2/12，也便于 EditMode 单测。

2. **`PlayerInputController`（MonoBehaviour）** — 持有：
   - `[SerializeField] int _localPlayerId`：本地玩家座位 Id；
   - `ICommandSink _sink`（经 `Initialize(ICommandSink)` 显式注入，禁止场景内 `Find`）；
   - 公开方法（供 UI `Button.onClick` / 卡牌视图指针回调订阅）：
     - `NotifyHandCardClicked(int cardInstanceId)` → 构造 `PlayCardCommand(playerId, cardInstanceId, TargetRef.None)` 提交（指向性卡牌的目标选择在 M5-T5，本任务先按无目标提交；RuleEngine 对需目标牌返回 `TargetRequired`，属"非法命令→提示"路径）。
     - `NotifyBoardMinionClicked(int attackerInstanceId, int targetHeroPlayerId)` → 构造 `AttackCommand(playerId, attackerInstanceId, TargetRef.ForHero(targetHeroPlayerId))` 提交。
     - `NotifyHeroPowerClicked()` → 构造 `UseHeroPowerCommand(playerId, TargetRef.None)` 提交。
     - `NotifyEndTurnClicked()` → 构造 `EndTurnCommand(playerId)` 提交。
   - 每次提交把返回的 `CommandResult` 通过 `event Action<CommandError> CommandRejected` 透出（仅 `IsInvalid` 时触发，用于提示 UI 显示原因）与 `event Action CommandAccepted`（用于反馈表现留钩子）。
   - 未 `Initialize` 时调用任何 `NotifyXxx` 抛 `InvalidOperationException`（装配错误要响）。
   - **空发保护**：控制器**不读** `MatchState` 判回合/判费用——那是规则层的事；同一意图重复点击照发，由 RuleEngine 拒绝。这与"View 只读不写"一致，控制器只是命令翻译器。

3. **`MatchControllerCommandSink`（适配器）** — `ICommandSink` 的生产实现：包装 `MatchController`，把 `Submit(IGameCommand)` 透传给 `MatchController.Submit`。放 `4_Presentation`（允许引用 `Card.Application`）。

### 3.2 新增测试（`Assets/_Project/7_Tests/EditMode/Presentation/`，目标 ≥ 12 例）

- `PlayerInputControllerTests`：
  - 未 `Initialize` 调用任一 `NotifyXxx` → 抛 `InvalidOperationException`。
  - `NotifyEndTurnClicked` → sink 收到 `EndTurnCommand` 且 `PlayerId == _localPlayerId`。
  - `NotifyHandCardClicked(42)` → sink 收到 `PlayCardCommand{PlayerId=local, CardInstanceId=42, Target=None}`。
  - `NotifyBoardMinionClicked(7, 1)` → sink 收到 `AttackCommand{PlayerId=local, AttackerInstanceId=7, Target=ForHero(1)}`。
  - `NotifyHeroPowerClicked()` → sink 收到 `UseHeroPowerCommand{PlayerId=local, Target=None}`。
  - sink 返回 `CommandResult.Valid()` → 触发 `CommandAccepted`，不触发 `CommandRejected`。
  - sink 返回 `CommandResult.Invalid(NotYourTurn)` → 触发 `CommandRejected(NotYourTurn)`，不触发 `CommandAccepted`。
  - sink 返回 `CommandResult.Invalid(NotEnoughMana, "…")` → `CommandRejected` 携带原错误码（Detail 不透出，本任务无 UI 文案）。
- `MatchControllerCommandSinkTests`（集成，复用 `MatchTestCards`/`MatchFactory` 夹具）：
  - 用真实 `MatchFactory` 开局 + `MatchController`；sink 提交 `EndTurnCommand(activePlayerId)` → 返回 `IsValid`，`TurnNumber` 前进。
  - sink 提交非行动方 `EndTurnCommand` → 返回 `IsInvalid` 且 `Error == NotYourTurn`（验证控制器把非法拒绝透回表现层）。

## 4. 明确不做（防止范围蔓延）

- **不做指向性卡牌的目标选择交互**（点击手牌后进入指向模式、拖箭头选目标）——属 M5-T5 `TargetingController`。本任务 `PlayCardCommand.Target` 一律 `None`；指向性牌的 `TargetRequired` 拒绝走"非法→提示"路径，由 M5-T5 接管。
- **不做拖拽手势识别**（`IBeginDragHandler`/`IDragHandler`）——属 M5-T5；本任务只用"点击"语义。
- **不做取消（右键/Esc）**——M5-T5。
- **不做悬停放大/详情预览**（FR-8.4）——M5-T6。
- **不做提示 UI 本体**（`ToastView` 等）——本任务只暴露 `CommandRejected` 事件；UI 订阅与文案属 M5-T6/M5-T7。
- **不做输入抽象 `IInputSource`**——U-9 的接口在 M5-T5 与指向/拖拽一同落地（那时才有屏幕坐标、射线命中等输入数据要抽象）；本任务的输入来自 UI `Button.onClick`/`EventTrigger` 回调，参数已是领域 Id（InstanceId/座位 Id），无屏幕坐标需要换算。
- **不做场景装配**：谁在场景里挂 `PlayerInputController`、谁把 `MatchController` 适配进 sink 属 M6-T1。
- **不改规则三层任何代码**；既有 741 内核用例零改动。
- **不做命令队列在表现层的本地缓存/回放**——`MatchController` 内部已有 FIFO。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle.Input
{
    /// <summary>命令出口抽象：表现层把玩家意图命令交给权威侧校验/结算。</summary>
    public interface ICommandSink
    {
        CommandResult Submit(IGameCommand command);
    }

    /// <summary>把玩家 UI 意图翻译成 GameCommand 并提交；自身不判规则。</summary>
    public sealed class PlayerInputController : MonoBehaviour
    {
        [SerializeField] internal int _localPlayerId;

        public event Action<CommandError>? CommandRejected;   // 仅 IsInvalid 时触发
        public event Action? CommandAccepted;                 // 仅 IsValid 时触发

        public void Initialize(ICommandSink sink);
        public void NotifyHandCardClicked(int cardInstanceId);
        public void NotifyBoardMinionClicked(int attackerInstanceId, int targetHeroPlayerId);
        public void NotifyHeroPowerClicked();
        public void NotifyEndTurnClicked();
    }

    /// <summary>生产适配器：包装 MatchController.Submit。</summary>
    public sealed class MatchControllerCommandSink : ICommandSink
    {
        public MatchControllerCommandSink(MatchController controller);
        public CommandResult Submit(IGameCommand command);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 未 `Initialize` 调 `NotifyEndTurnClicked` | 抛 `InvalidOperationException` |
| AC-2 | `NotifyEndTurnClicked` | sink 收到 `EndTurnCommand{PlayerId=_localPlayerId}` |
| AC-3 | `NotifyHandCardClicked(42)` | sink 收到 `PlayCardCommand{PlayerId, CardInstanceId=42, Target=None}` |
| AC-4 | `NotifyBoardMinionClicked(7, 1)` | sink 收到 `AttackCommand{PlayerId, AttackerInstanceId=7, Target=ForHero(1)}` |
| AC-5 | `NotifyHeroPowerClicked()` | sink 收到 `UseHeroPowerCommand{PlayerId, Target=None}` |
| AC-6 | sink 返回 `Valid` | 触发 `CommandAccepted`、不触发 `CommandRejected` |
| AC-7 | sink 返回 `Invalid(NotYourTurn)` | 触发 `CommandRejected(NotYourTurn)`、不触发 `CommandAccepted` |
| AC-8 | sink 返回 `Invalid(NotEnoughMana)` | `CommandRejected` 携带 `NotEnoughMana` |
| AC-9 | 真实 `MatchController` + 适配器：行动方 `EndTurn` | 返回 `IsValid` 且 `TurnNumber+1` |
| AC-10 | 真实 `MatchController` + 适配器：非行动方 `EndTurn` | 返回 `IsInvalid` 且 `Error=NotYourTurn`（拒绝透回） |
| AC-11 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除沿用 T1 口径）。
- 最少用例数：12。
- 必须覆盖的边界：未初始化抛异常、四种意图的命令字段正确性、接受/拒绝两种事件互斥、真实 `MatchController` 拒绝透回。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T4 状态 + 证据）。
- 需更新的配置：无（`Card.Presentation` 已引用 `Card.Application`/`Card.Domain`，asmdef 不动）。
- 是否影响既有模块：纯增量；不改 M5-T1/T2/T3 任何文件。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。`PlayCardCommand.Target=None` 在指向性卡牌上会被 RuleEngine 以 `TargetRequired` 拒绝，本任务把这视为"非法→提示"的合法路径，M5-T5 接管后该路径只用于真正的非法操作。

---

## 11. 结论与证据

### 11.1 自检与评审

- **编译**：0 error / 0 warning（无 Unity 工具链实跑 + Unity 编辑器实跑 815 通过佐证编译干净）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（Presentation 测试沿用 T1 口径排除在 coverage 外）；Unity EditMode 用户实跑 **815 passed / 0 failed**（802 + 13 新例，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（233 文件，+5 新文件：3 实现 + 2 测试）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（不变，规则三层零改动）。
- **铁律扫描**：
  - R2/R3：规则三层零改动；表现层新增代码可引用 `UnityEngine`/`Card.Application`（asmdef 既有引用）✅
  - R4 表现层只读：`PlayerInputController` 只把 UI 意图翻译成 `GameCommand` 经 `ICommandSink` 提交；**不读 `MatchState`、不判回合/费用/目标合法性**，合法性全部由 `RuleEngine` 裁定，拒绝原因经 `CommandRejected(CommandError)` 透出 ✅
  - R5 一切操作走 `GameCommand`：四个 `NotifyXxx` 全部构造 `PlayCardCommand`/`AttackCommand`/`UseHeroPowerCommand`/`EndTurnCommand` 提交，无旁路 ✅
  - R8 无隐式全局：`ICommandSink` 经 `Initialize` 显式注入，未注入抛 `InvalidOperationException`；无 `Find`/`static Instance` ✅
  - R10 小文件：实现 3 文件 19/62/27 行，单方法 ≤ 6 行 ✅
- **命令引用 InstanceId/座位 Id**：四个方法入参均为领域 Id（项目硬约束），无集合索引 ✅

### 11.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P2 | 0 | — |
| P3 | 1 | `MatchControllerCommandSink` 目前仅一行透传；阶段二联网时由 `NetworkMatchClient` 实现同接口（上行序列化、异步回执），`PlayerInputController` 不需改动。当前不过度设计异步语义。 |

### 11.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/Input/ICommandSink.cs` | 命令出口抽象（`Submit(IGameCommand) → CommandResult`） |
| `4_Presentation/Battle/Input/PlayerInputController.cs` | 点击意图 → 四种 `GameCommand`；`CommandAccepted`/`CommandRejected` 互斥事件 |
| `4_Presentation/Battle/Input/MatchControllerCommandSink.cs` | 生产适配器（包装 `MatchController.Submit`） |
| `7_Tests/EditMode/Presentation/PlayerInputControllerTests.cs` | 10 例：未初始化抛异常 ×2、四种命令字段 ×4、接受/拒绝互斥 ×2、错误码携带、null 防护 |
| `7_Tests/EditMode/Presentation/MatchControllerCommandSinkTests.cs` | 3 例：真实 `MatchController` 行动方接受/非行动方 `NotYourTurn` 拒绝透回、null 防护 |

### 11.4 下一步

进入 M5-T5 `TargetingController`（指向模式、箭头、右键/Esc 取消；U-9 `IInputSource` 输入抽象随该任务落地）。
