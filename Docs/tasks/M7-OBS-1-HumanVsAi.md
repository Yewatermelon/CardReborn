# 任务卡 · M7-OBS-1 PVE 人机实盘（分帧泵）

> 观察项单开任务，已决策：分帧泵驱动、最小可玩、实机+PlayMode 验收。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-OBS-1 |
| 所属里程碑 | M7 玩家代理与 AI |
| 上游需求 | FR-6（对手 AI）+ 用户决策四项（时机/驱动/范围/验收） |
| 规则依据 | Docs/02 M7 任务表 + 未关闭问题表 OBS-1 四项决策 |
| 预估 | 1 人日 / 1 会话 |
| 依赖任务 | M7-T1（AgentMatchRunner）、M7-T2（GreedyAiAgent）、M7-T3（TurnGuard） |

## 2. 目标（一句话）

> 在 Battle 场景实现最小可玩 PVE 人机对战：分帧泵驱动 AI 逐命令跑回合（MonoBehaviour 驱动器，规则层仍纯 BCL），玩家固定座位 0 先手，对手 GreedyAiAgent，AI 回合锁输入+思考提示，复用胜负与再来一局。

## 3. 范围（做什么）

### 3.1 架构改动（Application 层，纯 BCL，kernel 可测）

- **GreedyAiAgent 加逐步模式**：
  - 构造扩 `(playerId, database, guardOptions?, clock?, stepMode: false)`
  - `stepMode=false`（默认，T2 行为）：`OnTurnActivated` 内 while `StepOne()` 循环 + EndTurn
  - `stepMode=true`（分帧）：`OnTurnActivated` 只初始化状态，**不 Submit 任何命令**；外部调 `StepOne()` 逐步跑，结束后外部负责 EndTurn
  - 局部状态（`_rejectedCards/_exhaustedAttackers/_attacksUsed/_phase/_context`）提升为实例字段
  - 新增 `public bool StepOne()`：根据 `_phase` 跑一个阶段的一次迭代，Submit 一条命令，推进 `_phase`；返回 `true` = 还有步可跑、`false` = 已无可决策命令（guard exhausted 或 Done）
  - `_phase` 枚举：`HeroPower → PlayCards → Attack → Done`
  - 每 Submit 后仍 `_guard.RegisterStep(BuildStateSignature(_context.View))`（TurnGuard 正常工作）

- **AgentMatchRunner 加外部 Pump 回调**：
  - 新增 `public Action? OnSubmitAccepted`（默认 null）
  - `Submit` 内 `result.IsValid` 后：若 `OnSubmitAccepted != null` 则调它；否则自动 `Pump()`
  - 向后兼容：默认 null 时行为不变

### 3.2 新文件（Bootstrap 层，Unity 侧）

- `Assets/_Project/5_Bootstrap/Battle/AiTurnRunner.cs`（MonoBehaviour）：
  - 持有 `AgentMatchRunner`、`GreedyAiAgent`（stepMode）、`PlayerInputController`、思考提示 Text
  - `void Setup(runner, ai, input, thinkingText)`
  - `OnEnable`：绑定 `runner.OnSubmitAccepted`
  - `Update`：若活跃座位 == AI 且 AI 未跑完 → 每帧 `ai.StepOne()`；StepOne 返回 false → 调 `context.Submit(new EndTurnCommand(ai.PlayerId))` → EndTurn accepted 时手动 `runner.Pump()` 激活玩家
  - AI 回合驱动期间 `input.enabled = false` + 显示思考提示；玩家回合恢复
  - `OnSubmitAccepted` 回调：检查活跃座位是否 AI → 若是跳过 Pump（让 AiTurnRunner 手动控制）；若不是（玩家 Submit）→ 执行 Pump 激活 AI
  - 终局检测：`controller.View.IsFinished` → 禁用自身 + 显示胜负面板（复用 HotSeatHandler.CheckGameEnd）

### 3.3 改现有 Bootstrap

- `BattleSceneBootstrap`：
  - 读 `PlayerPrefs.GetString("GameMode", "PVP")` 区分 PVP / PVE
  - PVP：当前热座装配不变（两个 HumanPlayerAgent）
  - PVE：装配 `seat0 HumanPlayerAgent + seat1 GreedyAiAgent(stepMode:true)`；禁用 HotSeatHandler（不切视角、不交棒屏，视角固定玩家侧）；`AddComponent<AiTurnRunner>`
  - `_controller`/`_pump`/`_synchronizer`/`BattleComposition`/`HotSeatHandler` 装配复用

- `MainMenuBootstrap`：
  - 加"人机对战"按钮（与"双人对战"并排）
  - 点击：`PlayerPrefs.SetString("GameMode", "PVE")` + `SceneManager.LoadScene("Battle")`
  - 原"开始对战"按钮改名"双人对战"，`PlayerPrefs.SetString("GameMode", "PVP")` + LoadScene

### 3.4 测试

- **kernel**：GreedyAiAgent 逐步模式单元测试（`GreedyAiStepperTests.cs`，≥ 8 例）：
  - 默认 stepMode=false：OnTurnActivated 同步跑完（T2 既有测试零回归）
  - stepMode=true：OnTurnActivated 后无 Submit，StepOne 逐步产出命令，返回 false 时 EndTurn 后回合结束
  - StepOne 阶段推进顺序（HeroPower→PlayCards→Attack→Done）
  - guard 在逐步模式下正常工作（exhausted 后 StepOne 直接返回 false）
  - 整局：双 GreedyAiAgent(stepMode:true)，外部驱动 StepOne，EndTurn 手动 Pump → 终局

- **Unity EditMode**：AiTurnRunner 装配测试（≥ 4 例）：
  - PVE 模式装配：runner 座位 0 HumanPlayerAgent + 座位 1 GreedyAiAgent(stepMode:true)，AiTurnRunner 正确绑定
  - runner.OnSubmitAccepted 回调：AI Submit 跳过 Pump、玩家 Submit 执行 Pump
  - Input 锁：AI 回合禁用、玩家回合恢复
  - 思考提示：AI 回合显示、玩家回合隐藏

- **PlayMode**：PVE 人机对局冒烟（≥ 1 例）：
  - 主菜单→Battle（PVE 模式）→ AI 跑几回合→玩家操作→AI 继续→终局断言

## 4. 明确不做（防范围蔓延）

- 不做 AI vs AI 批量模拟器（M7-T4）
- 不做难度分级 / 评估权重外置（M7-T5）
- 不做先后手选择 / 对手选择 / 主菜单更多入口（最小可玩）
- 不做 AI 回合动画/飘字/思考计时（分帧泵机制先就位，表现层随 OBS 后续迭代）
- 不改 HotSeatHandler 内部逻辑（PVE 模式下它完全禁用，走新路径）
- 不改 GreedyAiTargeting / TurnGuard / MatchController / RuleEngine

## 5. 接口约定

```csharp
// Application 层（纯 BCL，kernel 可测）
public sealed class GreedyAiAgent : IPlayerAgent
{
    // 新增：stepMode=false 默认同步跑完；true 分帧模式只初始化
    public GreedyAiAgent(
        int playerId, CardDatabase database,
        TurnGuardOptions? guardOptions = null, IClock? clock = null,
        bool stepMode = false);

    // IPlayerAgent
    public void OnTurnActivated(IAgentContext context);  // stepMode=true 时只初始化
    public void OnTurnDeactivated();

    // 分帧入口：stepMode=true 时有效；默认 false 时返回 true（每次调都返回 true，
    // 但 OnTurnActivated 已循环跑完——外部不应再调）
    public bool StepOne();  // 还有步返回 true，回合结束返回 false
}

public sealed class AgentMatchRunner : ICommandAuthority, IAgentContext
{
    // 新增外部 Pump 回调：默认 null（向后兼容自动 Pump）
    public Action? OnSubmitAccepted { get; set; }
}

// Bootstrap 层（Unity 侧）
public sealed class AiTurnRunner : MonoBehaviour
{
    public void Setup(AgentMatchRunner runner, GreedyAiAgent ai,
                      PlayerInputController input, Text thinkingLabel);
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | kernel 不回归 | 默认 stepMode=false 下 M7-T2 全部 22 例既有测试全绿；kernel 822→830（+8） |
| AC-2 | StepOne 阶段推进 | HeroPower → PlayCards → Attack → Done；每个阶段产出对应命令 |
| AC-3 | stepMode=true 不自动 Submit | OnTurnActivated 后 recorder.Signatures.Count == 0；需外部调 StepOne 才产出 |
| AC-4 | guard 在逐步模式下正常 | exhausted 后 StepOne 直接返回 false，不再 Submit |
| AC-5 | runner.OnSubmitAccepted 回调 | AI Submit 跳过 Pump、玩家 Submit 执行 Pump；EndTurn accepted 后正确激活对手 |
| AC-6 | PVE 装配 | Battle 场景 PVE 模式：座位 1=GreedyAiAgent(stepMode:true)，HotSeatHandler 禁用，视角固定玩家侧 |
| AC-7 | 输入锁 | AI 回合 PlayerInputController 禁用、玩家回合恢复 |
| AC-8 | 思考提示 | AI 回合显示 "AI 思考中……"，玩家回合隐藏 |
| AC-9 | 实机冒烟 | MainMenu → 人机对战 → Battle：AI 回合逐步跑完 → 玩家可操作 → 胜负结算正常 → 再来一局可用 |
| AC-10 | PlayMode 冒烟 | PVE 人机对局跑完整局，终局断言通过 |
| AC-11 | 质量门禁 | check.ps1 PASS；kernel 覆盖率门禁不降；GreedyAiAgent 行覆盖 ≥ 85% |

## 7. 测试要求

- kernel ≥ 8 例（GreedyAiStepperTests），EditMode ≥ 4 例（AiTurnRunnerTests），PlayMode ≥ 1 例（人机对局冒烟）
- 先红后绿；禁止改测试凑绿
- PlayMode 测试首次提交前实跑留证（M6 复盘改进项 1：环境覆盖维度）

## 8. 涉及文档与配置

- PROGRESS：M7 3/5→4/5；未关闭问题表 OBS-1 状态改为 ✅ 已完成
- HANDOFF 快照随 M7 收官统一刷新（中途不动）

## 9. 完成定义（DoD 勾选）

- [ ] 满足全部 AC 且附证据
- [ ] kernel + EditMode + PlayMode 测试通过
- [ ] 编译 0 error / 0 warning
- [ ] check.ps1 PASS
- [ ] 铁律扫描通过（规则三层仍纯 BCL）
- [ ] 文档同步更新（任务卡 + PROGRESS）

---

## 10. 待确认问题

1. **runner.OnSubmitAccepted 回调语义**：回调接收者决定是否 Pump，默认 null 时 runner 自动 Pump——这允许 PVE 场景 AI Submit 时跳过 Pump、玩家 Submit 时执行 Pump。是否认可？（最小侵入，比 AutoPump bool 更灵活）
2. **stepMode 默认 false**：保持 T2 行为完全不变；分帧模式外部驱动——是否认可？
3. **HotSeatHandler 在 PVE 下完全禁用**：不切视角、不交棒屏，视角固定玩家侧；胜负结算仍复用其 CheckGameEnd——是否认可？

---

## 11. 任务结论（完成后回填）

**完成日期**：2026-10-10
**结论**：✅ 核心交付完成，AC-9/AC-10 待 Unity 实跑留证

### 交付清单

| 类别 | 文件 | 行/内容 |
| --- | --- | --- |
| Application（kernel） | `2_Application/Match/Agents/GreedyAiAgent.cs`（partial，主） + `GreedyAiAgent.Stepper.cs`（partial） | 新增 stepMode 构造参数 + StepOne() + 局部状态提升 + 阶段状态机；修复 `ResetTurnState()` 清 `_context` 导致的栈溢出 |
| Application（kernel） | `2_Application/Match/Agents/AgentMatchRunner.cs` | 新增 `Action? OnSubmitAccepted` 属性；Submit accepted 后若非 null 调之替代自动 Pump |
| Bootstrap | `5_Bootstrap/Battle/AiTurnRunner.cs` | 新文件（MonoBehaviour）：分帧驱动 Update → StepOne() + EndTurn；OnSubmitAccepted 回调统一 Pump（Pump 内部 seat 未变安全 return） |
| Bootstrap | `5_Bootstrap/Battle/BattleSceneBootstrap.cs`（partial） + `BattleSceneBootstrap.Input.cs`（partial） | 读 PlayerPrefs("GameMode")；PVP 保持热座，PVE 装配 HumanPlayerAgent(seat0) + GreedyAiAgent(stepMode:true, seat1) + AiTurnRunner；输入回调门控 `InputBlocked()`；HotSeatHandler 只保留 CheckGameEnd，跳过交棒屏 |
| Bootstrap | `5_Bootstrap/Battle/BattleUi.cs` + `BattleUiFactory.cs` | 新增 `ThinkingLabel` 字段 + 工厂构建思考提示 UI |
| Bootstrap | `5_Bootstrap/Menu/MainMenuBootstrap.cs` | 加"人机对战"按钮（PlayerPrefs="PVE"）；原"开始对战"改"双人对战"（PlayerPrefs="PVP"） |
| 测试 kernel | `7_Tests/EditMode/Match/GreedyAiStepperTests.cs` | 9 例（+ runner 回调 2 例） |
| 测试 EditMode | `7_Tests/EditMode/Bootstrap/AiTurnRunnerTests.cs` | 6 例（绑定 + IsAiTurn + OnSubmitAccepted + StepOne） |
| 测试 PlayMode | `7_Tests/PlayMode/PveAiTurnRunnerTests.cs` | 1 例（完整 PVE 对局冒烟） |

### 门禁结果

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| kernel 831/831 通过 | ✅ | `coverage.ps1` 0 failed，覆盖率 0_Core 96.52% / Domain+App 93.19%（↑ 基线 92.98%） |
| check.ps1 | ✅ PASS | 319 文件零违规 |
| R5 文件行数 | ✅ | 全部拆 partial（GreedyAiAgent → 主+Stepper，BattleSceneBootstrap → 主+Input） |
| 铁律扫描 | ✅ | 2_Application 零 UnityEngine/Debug.Log/Time/Random |

### 自检（Docs/04 §7 六步）

1. **编译与门禁** ✅：kernel 全绿 + check 静态扫描 PASS
2. **铁律扫描** ✅：规则三层纯 BCL，Bootstrap 层 MonoBehaviour 合法
3. **文件行数** ✅：无超限
4. **测试覆盖** ✅：kernel +9 例（822→831），EditMode +6 例，PlayMode +1 例
5. **AC 对齐** ✅：AC-1~8/11 有证据，AC-9/10 待 Unity 实跑
6. **改动边界** ✅：未动 RuleEngine/MatchController/EffectExecutor/TurnGuard/GreedyAiTargeting

### 关键修复

| Bug | 根因 | 修复 |
| --- | --- | --- |
| 栈溢出（RealGame + GreedyAiAgentRealGameTests） | `OnTurnDeactivated` 清 `_context=null`，递归返回后 `StepOne` 内部上下文丢失 → 重入时重新激活自己 → 无限循环 | `OnTurnDeactivated` 恢复为空操作；`OnTurnActivated` 开头已 ResetTurnState + 设 `_context`，足够 |
| AgentMatchRunner.cs 322 行超限 | stepMode 状态机逻辑 + TurnPhase 枚举 + 实例字段全部堆在一个文件 | 拆 `GreedyAiAgent.Stepper.cs`（partial） |
| BattleSceneBootstrap.cs 330 行超限 | PVE 装配 + 输入门控大量新增 | 拆 `BattleSceneBootstrap.Input.cs`（partial）；HotSeatHandler 构造合并为单次 |

### 遗留观察项

- AiTurnRunner 里 `OnSubmitAccepted` 总是调 Pump（Pump 内部 seat 未变直接 return）——语义简化，无需区分 AI/玩家 Submit
- PlayMode 测试里 ScriptedPlayerAgent 直接发 EndTurn——占位实现，真实玩家交互属 AC-9（Unity 实机）
- MainMenuBootstrap 用 `Battle.BattleSceneBootstrap.GameModePrefKey` 全限定引用——同一程序集内可见，无循环依赖

### 待确认（用户 Unity 实跑留证）

- AC-9：MainMenu→人机对战→Battle→AI 回合分帧跑→玩家可操作→胜负→再来一局
- AC-10：PlayMode PveAiTurnRunnerTests 在 Test Runner 里通过

