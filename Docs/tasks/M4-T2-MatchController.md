# 任务卡 · M4-T2 `MatchController`（★ 关键任务）

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T2 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | FR-5.1（命令驱动的回合流；非法阶段操作被拒绝）、FR-5.9（疲劳 §3.7，复用 M3-T8 `CardDrawService`）、FR-13.1/FR-13.2（规则三层只依赖 BCL、纯逻辑可测、无 Unity 可跑通对局）、NFR-4、Docs/01 §3.2（TurnStart/Draw 阶段职责） |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T2：命令队列 + 校验 + 结算 + 事件收集，**输入与结算解耦**；完成标准：单测完整回放一段对局脚本，状态与期望一致）、[01 §2.2/§3.2/§3.7](../01-开发需求文档.md) |
| 预估 | 1 人日 / 1 会话 |
| 依赖 | M4-T1（`TurnStateMachine`）、M3-T6（`RuleEngine.Validate`）、M3-T7（`MatchEvaluator`）、M3-T8（`CardDrawService`）、M3-T5（`MatchFactory`）；后继 M4-T3（富事件）、T4/T6（出牌/技能/攻击结算器） |

## 2. 目标（一句话）

> 在 `Card.Application.Match` 提供 `MatchController`：以 **FIFO 命令队列**接收意图，按"**入队 → 校验（RuleEngine）→ 结算（可注册 Settler）→ 终局判定（MatchEvaluator）→ 步骤台账**"流水线同步推进；当前只注册 `EndTurnSettler`（阶段流 + 行动方切换 + 回合开始簿记 + Draw 阶段抽 1），其余三类命令通过接缝留给 M4-T4/T6；用一段同种子可复现的疲劳致死脚本完整回放，状态与步骤序列两次完全一致。

## 3. 范围（做什么）

### 3.1 新增实现（均在 `Assets/_Project/2_Application/Match/`，纯 BCL + Core + Domain）

1. **`MatchStepRecord`**（不可变值对象）：单条命令处理台账——序号、命令类型名、发起座位、是否接受、`CommandError`、处理后阶段、是否终局。**控制器级流水台账，不是 M4-T3 的 `GameEvent` 富事件家族**（伤害/治疗/死亡等领域事件由 T3 建模后接入）。
2. **`ICommandSettler`**（结算接缝）+ **`SettlementContext`**（向结算器暴露 `MatchState` / `CardDatabase` / `TurnStateMachine`；`CardDrawService`/`MatchEvaluator` 为既有静态服务）。
3. **`EndTurnSettler`**：通过校验的 `EndTurnCommand` 的完整结算（仅在 Main 调用合法）：
   - `Main→TurnEnd`；
   - 切换行动方（0↔1，固定双人座）、`TurnNumber += 1`；
   - `→TurnStart`，对新行动方做回合开始簿记：`Mana.BeginTurn(rules.ManaLimit)`（M3-T1 已实现并测试的规则）、`Hero.PowerUsedThisTurn=false`、己方场上随从 `AttacksUsedThisTurn=0` 且移除 `SummoningSickness`（Docs/01 §3.2 TurnStart 职责；下回合可攻击）；
   - `→Draw`，`CardDrawService.Draw(player, 1)`（先手第 1 回合不抽由 `MatchFactory` 直接产出 Main/Turn1 保证，本任务不回改工厂）；
   - `→Main`。任一步阶段流转失败即快速失败（`InvalidOperationException`，理论不可达，锁一致性）。
4. **`MatchController`**：持有 `MatchState`、`CardDatabase`、内部 `TurnStateMachine`、命令队列、Settler 列表、台账；API：
   - `Enqueue(IGameCommand)` / `Submit(cmd)`（=入队+处理一条）/ `ProcessPending()`（FIFO 循环至队列空或对局终局，返回处理条数）/ `ProcessNext()`；
   - `State` / `Phase` / `IsFinished` / `PendingCount` / `LastOutcome` / `History`；
   - `RegisterSettler(...)`：构造时默认注册 `EndTurnSettler`，供 M4-T4/T6 追加（先注册者优先）；
   - 每条命令：先 `RuleEngine.Validate`，失败→记台账并返回（**状态零变更**）；通过→查 Settler，**缺失则抛 `InvalidOperationException`**（已知命令类型却无结算器=装配错误，不许"校验通过却无效果"地静默吞掉）；结算后统一 `MatchEvaluator`：非 Ongoing → `IsFinished=true`、`EndMatch()`、存 `LastOutcome`。
   - 终局后 `ProcessPending` 立即停止，剩余命令留队列；终局后 `Submit` 由 `RuleEngine` 拒（InvalidTarget）并记台账。

### 3.2 新增测试（`Assets/_Project/7_Tests/EditMode/Match/`，目标 ≥ 20 例，3 文件）

- 单步回合流：接受/阶段/行动方切换/TurnNumber/抽 1/法力增长回满/回合开始重置（技能、攻击次数、召唤失调）；
- 拒绝路径：错座位 EndTurn（NotYourTurn，状态不变）、未知命令（UnknownCommand，用 IGameCommand 测试伪类型）、终局后命令（InvalidTarget，不复活）、空队列 ProcessNext 抛异常、null 入队/构造 ANE；
- 接缝：校验通过但无 Settler 的已知命令（PlayCard）→ `InvalidOperationException`，状态不变；
- 队列：FIFO 顺序与序号、`ProcessPending` 终局截断（剩余留队）；
- **完整脚本回放**：真实源表构造同种子两局，连续提交交替座位的 EndTurn 脚本直至疲劳分胜负；两局 `MatchStateComparer.AssertEqual`、台账序列逐项一致、终局步数/获胜座位一致；负方疲劳计数 8、英雄 ≤0、非负方状态符合推演；脚本同时覆盖手牌 10 张爆牌（只结束回合手牌自然涨到 10+，第 10 张后抽牌爆牌入坟，Docs/02 §4.2 边界 1/2）。

## 4. 明确不做（防止范围蔓延）

- **不做 PlayCard / Attack / HeroPower 的任何结算**（扣法力+入场、战吼、攻击互殴、技能伤害）：归 M4-T4（效果框架）、T6（CombatResolver）、T7（死亡管线）；本任务只交付接缝与流水线，未装结算器时快速失败。
- **不做 `GameEvent` 富事件家族**（T3）：`MatchStepRecord` 只记命令处理结果，不含伤害/治疗/死亡明细；T3 落地后控制器同时产出领域事件。
- **不做 OnTurnStart/OnTurnEnd 效果触发**（T5 触发链）：TurnStart 簿记只操作既有字段/API。
- **不做换牌（Mulligan，P1 可选）、命令超时、异步/多线程队列**：同步 FIFO，单线程权威宿主模型（Docs/05 权威宿主）。
- **不改 M3 既有任何生产代码**（`RuleEngine`/`MatchFactory`/`MatchState`/`CardDrawService`/`MatchEvaluator` 零改动），既有 614 内核用例零改动。
- **不做 View/网络/序列化变更**；不发网络消息。

## 5. 接口约定

```csharp
namespace Card.Application.Match
{
    // 控制器步骤台账（不可变；非 T3 GameEvent）
    public readonly struct MatchStepRecord
    {
        public int Sequence { get; }            // 从 0 起，每处理一条 +1（含被拒）
        public string CommandType { get; }      // command.GetType().Name
        public int PlayerId { get; }
        public bool Accepted { get; }
        public CommandError Error { get; }      // None=接受
        public TurnPhase PhaseAfter { get; }
        public bool MatchFinished { get; }
    }

    // 输入与结算解耦的结算接缝（M4-T4/T6 实现并注册）
    public interface ICommandSettler
    {
        bool CanSettle(IGameCommand command);
        void Settle(SettlementContext context, IGameCommand command);
    }

    public sealed class SettlementContext
    {
        public SettlementContext(MatchState state, CardDatabase database, TurnStateMachine phases);
        public MatchState State { get; }
        public CardDatabase Database { get; }
        public TurnStateMachine Phases { get; }
    }

    public sealed class MatchController
    {
        public MatchController(MatchState state, CardDatabase database);

        public MatchState State { get; }
        public TurnPhase Phase { get; }
        public bool IsFinished { get; }
        public int PendingCount { get; }
        public MatchOutcome? LastOutcome { get; }
        public IReadOnlyList<MatchStepRecord> History { get; }

        public void RegisterSettler(ICommandSettler settler);
        public void Enqueue(IGameCommand command);
        public CommandResult Submit(IGameCommand command);   // = Enqueue + ProcessNext
        public CommandResult ProcessNext();                  // 空队列抛 InvalidOperationException
        public int ProcessPending();                         // 返回处理条数；终局即停
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 构造与初始态 | `Phase==Main`、`IsFinished==false`、`History`/`PendingCount` 为 0、`LastOutcome==null`；state/database 传 null 抛 ANE |
| AC-2 | 首条合法 EndTurn | 台账 1 条 Accepted/None/序号 0；行动方 0→1（构造为座 0 先手时）、TurnNumber 2、阶段回 Main；新手牌玩家手牌 +1、牌库 −1；其法力 1/1，先手方法力不变 |
| AC-3 | TurnStart 簿记 | 新行动方英雄 `PowerUsedThisTurn==false`；其场上随从攻击次数清零且 `SummoningSickness` 被移除（非行动方场上随从不受影响） |
| AC-4 | 法力增长接线 | 同一玩家隔回合再开始时 Max +1/Current 回满（如第 3 回合 2/2）；上限规则本身沿用 ManaPool 既有测试 |
| AC-5 | 错座位 EndTurn | Invalid/NotYourTurn；台账 Accepted=false 且带错误码；行动方/回合/阶段/手牌数均不变，序列序号仍递增 |
| AC-6 | 未知命令（测试伪 IGameCommand） | Invalid/UnknownCommand，记台账，状态不变 |
| AC-7 | 合法 PlayCard 但无结算器 | 抛 `InvalidOperationException`（消息含命令类型名）；抛出前后关键状态不变（阶段/手牌/法力） |
| AC-8 | 队列 FIFO | 按入队顺序处理，台账序号连续、CommandType 顺序一致 |
| AC-9 | 终局截断 | 脚本在队列未空时打出终局：处理停止，剩余命令留在 `PendingCount`；`IsFinished`、`Phase==MatchEnd`、`LastOutcome` 有值 |
| AC-10 | 终局后命令 | Submit EndTurn → Invalid/InvalidTarget 并记台账；阶段仍 MatchEnd、`LastOutcome` 不变 |
| AC-11 | 完整脚本可复现 | 同种子真实配置两局 + 同一交替 EndTurn 脚本：`MatchStateComparer.AssertEqual` 通过；台账投影（Accepted/Error/Type/Phase/Finished）逐项一致；终局发生在同一步 |
| AC-12 | 疲劳致死脚本断言 | 负方 `FatigueCounter==8`、英雄生命 ≤0（−6）；胜方与推演一致（后手牌少先耗尽，死亡发生在其第 8 次疲劳）；胜方疲劳计数 ==7；`LastOutcome.WinnerId` 等于开局先手座位、Result 对应 Player0/1Wins |
| AC-13 | 爆牌路径 | 脚本中手牌满 10 后继续抽牌：多出的牌进坟场不入手（手牌恒 ≤10），不崩溃、不中断脚本（§4.2 边界 2） |
| AC-14 | 空队列/空参 | `ProcessNext()` 空队列抛 InvalidOperationException；`Enqueue(null)` 抛 ANE |
| AC-15 | 解耦与门禁 | Application 仅依赖 BCL + Core + Domain；无 `UnityEngine`/`Debug.Log`；`check.ps1` PASS；单文件 ≤300 行、方法 ≤50 行、0 warning |

## 7. 测试要求

- EditMode（`Card.Tests.EditMode.Match`），≥ 20 个用例，分 3 文件：`MatchControllerTurnFlowTests` / `MatchControllerRejectTests` / `MatchControllerScriptTests`。
- 完整脚本走真实源表（复用 `ConfigTemplates.Load` + `ConfigValidator` + `CardDatabase` + `MatchFactory.Create(seed)`，与 `MatchFactoryTests` 同夹具口径）。
- 先红后绿：红为新类型 CS0246；禁止改老测试。

## 8. 涉及文档与配置

- 更新：`Docs/PROGRESS.md`、本任务卡结论；在 [02 M4 任务表](../02-开发计划步骤文档.md)下补一条**实施说明**（T2 已把 §3.2 TurnStart 簿记接线进控制器：法力增长/回满规则沿用 M3-T1 `ManaPool`，攻击/技能/失调清零为既有字段；M4-T8 落地时聚焦"技能命令结算与每回合 1 次在控制器链路上的测试"，规则本体不新增）。
- 配置表：无变更。
- 既有模块：零改动。

## 9. 待确认问题与假设

| # | 问题 | 本任务假设（最小影响） |
| --- | --- | --- |
| Q1 | T2 的"事件收集"是否直接建 T3 的 GameEvent？ | **否**。只建控制器步骤台账 `MatchStepRecord`（命令接受/拒绝+阶段+终局）。富领域事件（伤害/治疗/死亡）由 T3 建模，避免提前发明事件模型再返工；台账命名与注释已显式区分。 |
| Q2 | 校验通过但 Settler 未注册时返回失败还是抛异常？ | **抛 `InvalidOperationException`**。命令类型是封闭集合（RuleEngine 未知类型已在更早处拒），已知名却无结算器只可能是宿主装配缺陷；若返回业务失败会伪装成"合法命令无效果"，属正确性 P0 风险。T4/T6 注册后该路径自然消失。 |
| Q3 | TurnStart 重置（攻击/失调/技能）算不算抢 M4-T8？ | §3.2 明确这些是 TurnStart 阶段职责，且控制器是唯一正确接线点；底层规则/字段 M3 已存在并有测试。T2 只做"接线调用"，不新增规则；Docs/02 同步加实施说明，T8 范围按第 8 节收窄。 |
| Q4 | 终局判定时机 | 每条命令**完整结算后**统一判一次（T2 中死亡只可能发生在 Draw 疲劳）；效果/攻击链引入后仍保持"结算器内不写终局、控制器单入口判定"，避免多处提前封局（终局入口唯一化）。 |

## 10. 完成定义（DoD）

- [x] 满足全部 AC 且附证据（见 11.1）
- [x] 先红后绿（红 CS0246 `MatchController`/`MatchStepRecord`；新增 19 例全绿）
- [x] 编译 0 error / 0 warning（无 Unity 实跑；Unity 编辑器 EditMode 补验 **653 passed / 0 failed**，2026-10-06 用户回报）
- [x] 通过 Docs/03 铁律与禁止清单自查（见 11.2）
- [x] 通过 Docs/04 评审，无未关闭 P0/P1（见 11.6）
- [x] 涉及文档同步（PROGRESS / 02 实施说明）

---

## 11. 自检与评审结论（2026-10-06）

### 11.1 需求对齐（逐条 AC）

| AC | 结论 | 证据（测试方法） |
| --- | --- | --- |
| AC-1 初始态 + null 防御 | 已满足 | `Ctor_InitialState_IsMainTurnOneEmptyLedger`、`Ctor_NullArguments_Throw` |
| AC-2 首条 EndTurn 全字段 | 已满足 | `EndTurn_SwitchesActive_DrawsOne_RefillsMana_RecordsAccepted`（切换/回合/手牌6/牌库−1/双方法力/台账 7 字段） |
| AC-3 TurnStart 簿记 | 已满足 | `TurnStart_ResetsPowerAndAttacksAndSickness_OnlyForNewActive`（技能、攻击清零、失调解除、圣盾保留、非行动方不动；空库疲劳 1 锁 Draw 执行） |
| AC-4 法力增长接线 | 已满足 | `Mana_GrowsAndRefills_OnEachPlayerTurn`（回合 3/4 双方 2/2） |
| AC-5 错座位拒绝 | 已满足 | `Submit_WrongSeatEndTurn_RejectedAndStateUnchanged` |
| AC-6 未知命令 | 已满足 | `Submit_UnknownCommand_RejectedWithUnknownCommand`（伪 `IGameCommand`） |
| AC-7 无结算器快速失败 | 已满足 | `Submit_ValidPlayCardWithoutSettler_ThrowsAndStateStaysIntact`（抛异常含类型名；手牌/法力/场面/台账不变） |
| AC-8 FIFO | 已满足 | `Queue_ProcessesInFifoOrder_RejectedThenAccepted` |
| AC-9 终局截断 | 已满足 | `FullScript_ProcessPending_StopsAtFinishAndLeavesRestQueued`（处理 67、留队 13）、`FullScript_FatigueKillsSecondPlayer_*` |
| AC-10 终局后拒绝 | 已满足 | `Submit_AfterMatchFinished_RejectedAndDoesNotReopenMatch`（构造态）+ `Submit_AfterScriptedFinish_RejectedAndStaysFinished`（真实脚本后） |
| AC-11 同种子可复现 | 已满足 | `FullScript_SameSeed_ReplayProducesIdenticalStateAndLedger`（`MatchStateComparer` 全等 + 台账投影全等） |
| AC-12 疲劳致死推演 | 已满足 | `FullScript_FatigueKillsSecondPlayer_FirstPlayerWins`：67 步、回合 68、后手 −6（疲劳 8）、先手 9（疲劳 6）、胜者=开局先手座 |
| AC-13 爆牌路径 | 已满足 | `FullScript_BurnsCardsWhenHandFull_*`（回合 12 手牌 10、坟场 1；全程 ≤10） |
| AC-14 空队列/空参 | 已满足 | `ProcessNext_WhenQueueEmpty_Throws`、`Enqueue_Null_Throws`、`RegisterSettler_Null_Throws` |
| AC-15 解耦/门禁 | 已满足 | `check.ps1` PASS（175 文件/10 asmdef）；using 仅 `System*`、`Card.Core`、`Card.Domain.*`；最大文件 160 行、最长方法 17 行、0 warning |

### 11.2 铁律扫描

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 1 组件优于继承 | 未命中 | Settler 为接口 + 组合注册；控制器 sealed，无玩法继承 |
| 2 依赖向下 / 3 BCL 纯净 | 未命中 | 4 个新文件全在 2_Application，仅引用 Core/Domain；无 `UnityEngine`/`Debug.Log`/`Mathf`/`JsonUtility`/`Time`/随机 |
| 4 表现层只读 | 不涉及 | 无 View；台账只读暴露 |
| 5 全部操作走命令校验 | 未命中且强化 | 唯一状态变更入口 = 校验后的 Settler；校验失败零变更并有测试 |
| 6 数值来自配置 | 未命中 | 抽牌数/法力上限均取自 `RulesConfig`（`ManaLimit`）与常量 `CardsDrawnPerTurn=1`（§3.2 固定规则） |
| 7/8 全局访问 | 未命中 | 无单例/Find/ServiceLocator；每局一个控制器实例 |
| 9 测试先行 | 未命中 | 19 例先红（CS0246）后绿 |
| 10 体量/复杂度/0 warning | 未命中 | 160/74/51/45 行；方法最长 17 行；`dotnet build` 无 warning |
| 11 状态只存数据 | 未命中 | 不引 Unity；写的都是既有字段（Phase/ActivePlayerId/TurnNumber/IsFinished/Mana/Statuses） |
| 12 权威宿主解耦 | 正向 | 控制器即 M13 `MatchHost` 的权威推进内核雏形：只接受命令意图，全量校验；无网络引用 |
| 禁止清单 | 未命中 | 无 #region、无单行多语句、花括号完整、无拼音命名、无 try/catch 吞异常 |

### 11.3 边界推演

| 场景 | 行为 | 证据 |
| --- | --- | --- |
| 空库抽牌（疲劳递增） | Draw 阶段疲劳 1,2,…,8 致后手死亡，脚本精确终止于第 67 步 | AC-12（对应 §4.2 边界 1） |
| 手牌 10 张抽牌 | 爆牌入坟、不入手、不中断 | AC-13（§4.2 边界 2） |
| 英雄归零后输入 | 队列截断 + 后续 Submit 拒绝（InvalidTarget），不复活、LastOutcome 不变 | AC-9/10（§4.2 边界 10） |
| 被拒命令污染 | 拒绝在任何状态写入之前返回；错座位后合法命令照常执行 | AC-5/8 |
| 结算器缺失 | 抛装配异常，杜绝"校验通过无效果" | AC-7（Q2 假设） |
| 终局入口多处化 | 结算器不判终局；仅 `FinishIfDecided` 单入口写 IsFinished/EndMatch/Outcome | 代码结构 + AC-9/12 |
| 不可达防御分支 | `MatchController.cs` L124-126（EndMatch 失败抛错）3 行不可达：Settler 成功后阶段必为非终局，终局边恒存在；同 M1-T3 防御口径；本文件行覆盖 97%（87/90），分支 95% | 覆盖率 XML 实查 |

### 11.4 测试证据

- 红：`coverage.ps1` 编译失败 CS0246（`MatchController`、`MatchStepRecord` 未找到）。
- 绿：无 Unity 工具链 **633 passed / 0 failed / 0 skipped**（614 + 19：回合流 4 + 拒绝 9 + 脚本 6），0 warning。
- 覆盖率（cobertura 实查）：`MatchController` 87/90 **97%**、`EndTurnSettler`/`ICommandSettler`/`MatchStepRecord` 计入 Domain+App；汇总 **0_Core 96.51%**（1160/1202）、**Domain + App 91.20%**（2363/2591，门禁 80%）。
- 静态门禁：`check.ps1` **PASS**（175 文件 / 10 asmdef）；新增 8 个手写 .meta，全仓 261 GUID 无重复。
- 既有代码零改动；既有 614 用例零改动。
- Unity 权威：2026-10-06 用户在 Unity 2022.3.54f1c1 编辑器 Test Runner（EditMode）实跑回报 **653 passed / 0 failed**（634 + 19），8 个新脚本编译导入与手写 .meta 识别无误。

### 11.5 影响面

- 新增：`2_Application/Match/` 4 文件（控制器/接缝/结束回合结算器/台账）+ `.meta`；`7_Tests/EditMode/Match/` 4 文件（夹具 + 3 测试）+ `.meta`。
- 既有生产代码：**零改动**。文档：PROGRESS、02 M4 表实施说明、本任务卡。
- 对后继任务的接缝：M4-T3 在控制器流水线中挂富事件产出；M4-T4/T6 `RegisterSettler` 注册出牌/技能/攻击结算器（注册后 AC-7 的抛异常路径自然消失）；M4-T5 接 OnTurnStart/End 触发点（EndTurnSettler 已留注释位置语义）；M13-T1 MatchHost 直接复用本类做权威推进。

### 11.6 反向审查（挑刺）

1. **"校验通过但无结算器抛异常"会不会在对局中崩掉权威宿主？** 命令类型封闭（4 种 struct），未知类型在 RuleEngine 第一步即拒；只有"已知名但本版本未实现结算"才到抛异常路径——这是构建期装配缺口而非玩家输入，快速失败优于静默吞命令（后者会让客户端以为生效）。T4/T6 落地后该路径消失；已用测试锁定当前行为。
2. **被拒命令也写台账，会不会把序号/流水搞乱？** 序号只在 `Record` 内按 `_history.Count` 分配，拒绝与接受同路径记录；FIFO 测试显式断言"先拒后受"两条记录序号 0/1、发起座位与结果正确。
3. **疲劳致死为什么发生在 Draw 而控制器却在 Main 才判终局？** 结算器只推进状态（Draw 阶段英雄可能已 ≤0），阶段走完到 Main 后由控制器**唯一入口** `FinishIfDecided` 判定并 EndMatch；避免在抽牌服务/结算器多处写终局面（终局入口唯一化，防止后续 T5/T7 触发链提前封局）。
4. **TurnNumber 在切行动方时 +1，语义是"单方回合数"还是"整轮数"？** Docs/01 §3.1 回合定义为单个玩家的回合（先手回合 1、后手回合 2…），与 MatchFactory 起 TurnNumber=1、Evaluator 输出一致；脚本终局回合 68 与疲劳推演吻合（AC-12）。
5. **为什么重置召唤失调放在新行动方回合开始，而不是随从进场后定时？** §3.2 TurnStart 职责且是唯一自然接线点；非行动方随从的失调在其下回合开始前保留，AC-3 双向断言锁定。

### 11.7 评审结论

**通过**（AI 自审）：无 P0/P1；无新增遗留项。双环境证据齐备（无 Unity 633/0、Unity EditMode 653/0），本任务关闭。
