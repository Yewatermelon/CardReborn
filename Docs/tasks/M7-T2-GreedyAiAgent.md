# 任务卡 · M7-T2 基础 AI（GreedyAiAgent）

> 模板见 Docs/templates/TaskCard.template.md。本卡为 M7 第二任务，依赖 M7-T1（已交付 a9a7b81）。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-T2 |
| 所属里程碑 | M7 玩家代理与 AI |
| 上游需求 | FR-6.2（能出牌/能攻击·先解场后打脸/能用技能/会结束回合）、FR-6.5（每命令先经 RuleEngine 校验）、FR-6.1（同一 IPlayerAgent 接口） |
| 规则依据 | Docs/01 §FR-6（L282–290）、Docs/02 M7 任务表 T2；铁律 1/5/6（组件化、命令唯一入口、数值来自配置） |
| 预估 | 1 人日 / 1 会话 |
| 依赖任务 | M7-T1（IPlayerAgent/IAgentContext/AgentMatchRunner 已交付） |

## 2. 目标（一句话）

> 在 `2_Application` 实现纯 BCL 的 `GreedyAiAgent : IPlayerAgent`：激活回调内同步完成一整个回合（英雄技能 → 出牌 → 攻击 → 结束回合），所有命令经 `IAgentContext.Submit` 上行，**预校验合法、零被拒命令、任何局面必终止**。

## 3. 范围（做什么）

- **Application（纯 BCL，kernel 工具链覆盖）**，新文件 `Assets/_Project/2_Application/Match/Agents/GreedyAiAgent.cs`（+ 必要时拆 1 个辅助文件，R5 ≤300 行/文件、≤50 行/方法）：
  - 构造：`(int playerId, CardDatabase database)`；`PlayerId` 暴露座位；数据库只读查询用（费用/ TargetRule/关键词来自配置，铁律 6）。
  - `OnTurnActivated`：同步决策循环，依次四类命令（出牌/攻击/技能/结束回合）——顺序固定：**技能（若可用且有收益）→ 出牌循环 → 攻击循环 → EndTurn**。
  - 决策规则（全部从 `IAgentContext.View` 只读视图 + CardDatabase 读配置判定，不复制规则结算逻辑）：
    - **技能**：`!PowerUsedThisTurn` 且 `Mana.CanSpend(power.Cost)` 时用一次；`TargetRule` 决定目标（Enemy* → 敌方；伤害类默认打敌英雄，Buff/治疗类 → 己方最优随从）；`None` → 无目标。
    - **出牌**：每轮扫描手牌中 `Cost ≤ Mana.Current` 且场面未满（< 7）的卡，**费用从高到低**出（节奏优先）；法术/战吼按 `TargetRule` 选目标（伤害 → 敌方能杀死的最高攻随从，杀不了 → 敌英雄；Friendly → 己方最高攻随从）；每 accepted 一条后重扫视图。
    - **攻击（先解场后打脸）**：可攻击 = `Attack>0`、无 `Frozen`、（无 `SummoningSickness` 或有 `Charge/Rush`）、`Windfury` 可两次；目标选择：敌方有 `Taunt` → 必打嘲讽（多个选血最少）；否则**有利交换**（能杀死且己方不死，或 `Poisonous` 换任意）→ 选敌最高攻随从没有则打脸；无 `Stealth` 目标才可被指。
    - **结束**：无可行动作即 `EndTurnCommand`。
  - **终止保障（T2 内建的轻量版）**：每条命令被拒 → 把该候选移出本回合候选集（不重试同一条）；候选集空 → EndTurn；整体迭代设硬上限（常量，如 500 次提交）防御性兜底。**正式步数/时间/无进展检测归 M7-T3，本卡不做可配置上限。**
  - `OnTurnDeactivated`：空操作（无状态）。
- **测试**（kernel，`7_Tests/EditMode/Match/GreedyAiAgentTests.cs` + 需要时拆夹具文件）：全部真实 `MatchFactory`/`MatchController` 对局 + 少量手工构造状态；复用 M7-T1 的 `AgentMatchRunner` 装配两个 GreedyAiAgent。

## 4. 明确不做（防止范围蔓延）

- 不做步数/时间上 limit 的可配置化、不做"无进展检测"——**M7-T3**。
- 不做批量模拟器/胜率统计——**M7-T4**（本卡只保证多种子整局可复现）。
- 不做难度参数/评估函数权重外置——**M7-T5**（优先级表本卡内常量）。
- 不改 `RuleEngine`/`MatchController`/命令模型；不接 UI、不接网络、不改 `HumanPlayerAgent`；PVE 人机实盘仍属 M7-OBS-1。
- 不做随机性（同种子同序列必须逐位一致，为 T4 模拟器与回归测试打底）。
- 不处理奥秘/扳机类对手反制的预判（规则内核当前无此类效果）。

## 5. 接口约定

```csharp
namespace Card.Application.Match.Agents
{
    /// <summary>贪心基础 AI（FR-6.2）：激活后同步跑完整回合。
    /// 决策只读 IAgentContext.View + CardDatabase；命令一律 context.Submit 上行（FR-6.5）。</summary>
    public sealed class GreedyAiAgent : IPlayerAgent
    {
        public GreedyAiAgent(int playerId, CardDatabase database);
        public int PlayerId { get; }
        public void OnTurnActivated(IAgentContext context); // 技能→出牌→攻击→EndTurn
        public void OnTurnDeactivated();                    // 空操作
    }
}
```

关键语义（测试锁定）：

1. 零非法命令：AI 提交前用视图+配置自判合法（法力/场面容量/召唤失调/嘲讽/潜行/目标规则），整局 `CommandResult.IsInvalid` 次数 = 0；若仍被拒（规则与预判不一致），该候选本回合内不再重试。
2. 确定性：无 `Random`；同种子两次整局的命令序列逐位一致。
3. 终止性：每次激活提交数 ≤ 硬上限；候选耗尽必发 EndTurn。
4. 重入：依赖 M7-T1 runner 的同步重入语义（EndTurn 后对手激活），自身不持有跨回合状态。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 静态门禁 | 新代码在 `2_Application`，纯 BCL；`check.ps1` PASS |
| AC-2 | 整局无非法命令 | 双 GreedyAiAgent 对局（≥3 个种子）打到终局，每个 Submit 均 accepted（IsInvalid 计数 = 0） |
| AC-3 | 终止性 | 每个种子整局在有限提交内结束（IsFinished=true），无死循环 |
| AC-4 | 确定性 | 同种子两局命令序列（类型+座位+目标）逐位一致 |
| AC-5 | 技能使用 | 构造"技能可用且有法力"局面 → 首条命令为 UseHeroPower；`PowerUsedThisTurn` 或法力不足 → 不用 |
| AC-6 | 出牌优先级 | 手牌多张可出时先出费用最高者；场面满 7 不再出随从；TargetRule=EnemyMinion 的卡目标必为敌方随从 |
| AC-7 | 先解场后打脸 | 敌方有嘲讽 → 只打嘲讽；无嘲讽且存在有利交换 → 打随从；否则打脸；潜行随从永不成为目标 |
| AC-8 | 攻击资格 | 召唤失调随从不动（Charge/Rush 除外）、Frozen 不动、Windfury 打两次 |
| AC-9 | 质量门禁 | kernel 全量通过、覆盖率门禁不降；GreedyAiAgent 行覆盖 ≥ 85%；R5 达标 |
| AC-10 | 既有不回归 | M7-T1 的 24 例与既有 761 例全绿（无需用户重跑 PlayMode；EditMode 由用户补跑新增套件） |

## 7. 测试要求

- kernel 测试 **≥ 12 例**：AC-2~AC-8 全覆盖；先红（CS0246）后绿；禁止改测试凑绿。
- 构造局面用手工 `MatchState`（参考既有 `RuleEngineTestHelpers.BuildState`），整局用真实 `MatchFactory` 种子对局。
- "零非法命令"用包装 `ICommandAuthority` 计数器断言，不靠人工读日志。
- 新套件首次提交前在目标环境实跑留证：kernel 由 coverage.ps1 留证（本卡无 Unity 侧新代码，无需用户跑 PlayMode）。

## 8. 涉及文档与配置

- 需更新：本卡结论；`Docs/PROGRESS.md`（M7 1/5→2/5）。HANDOFF 快照随 M7 收官统一刷新。
- 配置表：无改动（卡牌/技能数据沿用现有）。
- 影响面：纯加法；不改 M7-T1 已交付类型公开面。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见 §11）
- [x] 测试通过且覆盖边界（22 例：整局 4 + 决策 9 + 攻击 9）
- [x] 编译 0 error / 0 warning（kernel 工程 0/0；Unity EditMode 1004 实跑通过即编译干净）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见 §11 第 2 步）
- [x] 通过 `Docs/04` 评审，无未关闭 P0/P1（见 §11）
- [x] 涉及文档已同步更新（本卡 + `Docs/PROGRESS.md`）

---

## 10. 待确认问题（开工前）

1. 出牌优先级取"费用从高到低（节奏优先）"。备选：低费优先铺场。默认采用前者，可否？
2. "有利交换"定义：AI 随从能杀死敌方且自身不死（或 Poisonous 任意换）才解场，否则打脸。是否认可该简化（M7-T5 再引入评估权重）？
3. 伤害类技能/法术目标默认"能杀最高攻随从则解场、否则打脸"，与攻击决策共用同一目标函数。可否？

---

## 11. 任务结论（2026-10-08 完成）

**结论：通过。** 三个待确认问题均按默认方案；无 P0/P1 遗留。代码提交 `018632c`，文档随本提交。

### 交付物

- 生产（纯 BCL，`2_Application/Match/Agents/`）：`GreedyAiAgent.cs`（175 行）、`GreedyAiTargeting.cs`（190 行）。
- 测试（`7_Tests/EditMode/Match/`，22 例）：`GreedyAiAgentRealGameTests.cs`（4）、`GreedyAiAgentDecisionTests.cs`（9）、`GreedyAiAgentAttackTests.cs`（9），夹具 `GreedyAiAgentFixtures.cs`（迷你配置库 / 手工局面 / `RecordingAuthority` 零非法计数 / `DirectAgentContext` 无路由直达 / `FailPlaysContext` 拒绝注入）。

### 第 1 步：需求对齐（AC 证据）

| AC | 满足 | 证据 |
| --- | --- | --- |
| AC-1 静态门禁 | 是 | `check.ps1` PASS（312 文件，0 违规）；两文件均在 `2_Application`，仅 using BCL + Core/Domain |
| AC-2 整局零非法 | 是 | `RealGame_Seed1/Seed7/Seed20261008_FinishesWithZeroInvalid`：`InvalidCount=0` |
| AC-3 终止性 | 是 | 三种子均 `IsFinished=true` 且签名数 < 2000；`MaxSubmissionsPerActivation=500` 兜底 + 被拒候选移出集 |
| AC-4 确定性 | 是 | `RealGame_SameSeed_CommandSequenceIsDeterministic`（种子 42 两局签名逐位相等）；实现无 Random |
| AC-5 技能 | 是 | `HeroPower_UsedFirst_WhenAffordable`（首条 HeroPower 打敌英雄）/ `..._WhenManaShort` / `..._WhenAlreadyUsed` |
| AC-6 出牌 | 是 | `Play_HighestCostFirst` / `Play_SkipsMinion_WhenBoardFull` / `Play_EnemyMinionRule_TargetsHighestAttackNonStealth` / `..._WhenOnlyStealthMinions` |
| AC-7 解场/打脸 | 是 | `Attack_Taunt_PicksLowestHealthTaunt` / `Attack_FavorableTrade_TargetsMinion` / `Attack_Unfavorable_GoesFace` / `Attack_Poisonous_TradesUpIntoBiggerMinion` / `Attack_StealthMinion_IsNeverTargeted` |
| AC-8 攻击资格 | 是 | `Attack_SummoningSickness_Skipped` / `Attack_Charge_IgnoresSummoningSickness` / `Attack_Frozen_Skipped` / `Attack_Windfury_AttacksTwice` |
| AC-9 质量门禁 | 是 | kernel **801/801**（基线 779 + 22）；GreedyAiAgent 行覆盖 **91%**、GreedyAiTargeting **96%**；0_Core 96.52%、Domain+App 92.98%（均升/持平）；R5 达标 |
| AC-10 不回归 | 是 | kernel 全量 801 全绿；用户编辑器实跑 **Unity EditMode 1004/1004**（982+22）；本任务无 Unity 侧新代码，PlayMode 3 例不受影响 |

### 第 2 步：铁律扫描（Docs/03 十三条 + 禁止清单）

1 组件优于继承：`sealed` 类 + 静态目标选择器，无玩法继承 ✅；2 依赖向下：仅 Application→Core/Domain ✅；3 纯 BCL：无 `UnityEngine`/`Debug.Log`/`Time`/`Random`，`check.ps1` R1 实证 ✅；4 表现层只读：本任务不涉表现层 ✅；5 命令唯一入口：四类命令全部 `context.Submit`，AI 不触可写状态 ✅；6 数值来自配置：费用/TargetRule/关键词/技能费用全部读 `CardDatabase`，硬编码仅算法常量（500/伤害估值）✅；7 无 Editor 引用 ✅；8 无单例/`Find`/ServiceLocator ✅；9 先写测试：红灯 CS0246 留证后转绿 ✅；10 R5：175/190 行、最大方法 < 50 行、圈复杂度低 ✅；11 内核解耦：环境依赖零引入 ✅；12/13 本任务不涉网络 ✅。禁止清单：无 `try/catch` 吞异常、无索引当业务 ID（全部 InstanceId/座位 Id）、未改测试凑绿。

### 第 3 步：边界推演

| 场景 | 行为 | 测试 |
| --- | --- | --- |
| 空手牌/0 法力/技能不可用 | 直接 EndTurn（单条签名） | `TurnEndsImmediately_WhenNothingToDo` |
| 被拒命令 | 该 InstanceId 入 `rejectedCards`/`exhaustedAttackers`，不重试，其余候选继续 | `RejectedCandidate_IsNotRetriedThisTurn`（FailPlaysContext 注入 1 次拒绝） |
| 场面满 7 | 随从候选过滤，仅法术可出 | `Play_SkipsMinion_WhenBoardFull` |
| 仅潜行合法目标 | 不出牌/不指目标；攻击转打脸 | `Play_EnemyMinionRule_Skipped_WhenOnlyStealthMinions`、`Attack_StealthMinion_IsNeverTargeted` |
| 风怒二次攻击 | 局部 `attacksUsed` 跟踪，打满 2 次 | `Attack_Windfury_AttacksTwice` |
| 终局/非己方回合 | `CanAct` 双保险，激活即返回、不重复 EndTurn | 三种子整局终局自然停活 |

### 第 4 步：测试证据

- `Tools/coverage.ps1`：**801 passed / 0 failed**；覆盖 GreedyAiAgent 91%（111/122 行，未覆盖行为防御性分支：硬上限截断、FindEnemy 兜底）、GreedyAiTargeting 96%（148/154）。
- Unity 编辑器 Test Runner EditMode：**1004 passed / 0 failed**（用户 2026-10-08 实跑回报）；PlayMode 3 例不受影响（无 Unity 侧改动）。

### 第 5 步：影响面

纯加法：新增 2 生产文件 + 4 测试文件，未改 RuleEngine/MatchController/命令模型/M7-T1 公开面，无配置变更。唯一测试侧注意：夹具迷你库需为 `MatchTestCards.Minion` 临时键登记占位定义（TriggerDispatcher 按 CardKey 反查），已在 `PlaceholderMinionKeys` 集中登记并注释。

### 第 6 步：反向审查

1. **"AI 预判与引擎口径漂移会产生被拒命令"**——整局三种子实测零被拒；且即使漂移，被拒即移出候选集 + 500 硬上限保证终止，正式无进展检测归 M7-T3（本卡 §4 明确不做）。
2. **"效果取向判定（伤害/增益）过于简单"**——仅用于 `Any/AnyMinion` 无阵营约束规则；当前卡池此类技能/法术仅伤害类，增益按"己方最高攻"兜底，覆盖现有配置；更完整评估权重属 M7-T5（§4 已排期）。
3. **"局部 attacksUsed 在随从死亡后重登同一 InstanceId 会错记"**——InstanceId 由 `NextInstanceId` 单调分配，整局不复用；且每次激活字典新建，无跨回合状态。
4. **"攻击不看 Rush 不能打脸的限制"**——引擎亦未实现 Rush 打脸限制（Rush 仅豁免失调），AI 与当前引擎口径一致；规则补全时需同步（登记为已知简化，非本卡缺陷）。

无 P0/P1/P2 新增。
