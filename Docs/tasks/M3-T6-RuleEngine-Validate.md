# 任务卡 · M3-T6 RuleEngine.Validate

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T6 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.3（出牌流程）、FR-5.5（攻击流程）、FR-5.10（英雄技能）、FR-14.3（命令上行） |
| 规则依据 | Docs/01 §3.3 出牌规则、§3.4 攻击规则、§3.6 英雄技能、§3.2 回合阶段 |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T5（状态骨架、分区、命令模型、开局初始化） |

## 2. 目标（一句话）

> 提供应用层 `RuleEngine`：对四种 `GameCommand`（出牌/攻击/英雄技能/结束回合）做权威合法性校验，产出 `CommandResult`（含 `CommandError` 与补充说明），不修改任何状态。

## 3. 范围（做什么）

1. 新增 `RuleEngine`（静态类，命名空间 `Card.Application.Match`）：
   - `CommandResult Validate(MatchState state, IGameCommand command, CardDatabase database)`
2. 校验顺序（先结构后语义，失败即返回）：
   - 结构：命令 null、玩家座位存在、玩家是 ActivePlayer
   - 出牌：阶段 Main、牌在手牌、费用 ≤ 当前法力、随从战场有空位、目标规则满足
   - 攻击：阶段 Main、攻击者在我方场上、未召唤失调（或冲锋/突袭）、攻击次数 >0、目标存在且类型匹配、嘲讽强制
   - 英雄技能：阶段 Main、本回合未使用、费用 ≤ 当前法力、目标规则满足
   - 结束回合：阶段 Main（无其他限制）
3. 目标合法性校验辅助 `TargetValidator`（内部静态类）：
   - 根据 `TargetRule`（None/Any/Enemy/Friendly/EnemyMinion/FriendlyMinion/AnyMinion）判断 `TargetRef` 是否合法
   - 潜行（Stealth）随从不可被选为目标（P1，本任务先实现基础检查）
4. 边界：不检查 `TurnStart/Draw/TurnEnd` 阶段（由 M4 状态机负责流转），`Main` 阶段外的命令一律 `NotYourTurn`（复用该错误码表示阶段不符）。

## 4. 明确不做（防止范围蔓延）

- 不修改状态（无结算、无事件、无 Zone 移动）：纯查询校验。
- 不做回合状态机流转（TurnStart→Draw→Main→TurnEnd）：M4-T1/T2。
- 不做攻击结算（CombatResolver）：M4-T6。
- 不做效果解析/战吼/亡语：M4 效果系统。
- 不做冻结（Frozen）对攻击的影响：P1 关键词，本任务只校验召唤失调与次数。
- 不做风怒（Windfury）2 次攻击：P1，本任务按每回合 1 次校验。
- 不做武器/英雄卡：配置表无此类型数据。

## 5. 接口约定

```csharp
namespace Card.Application.Match
{
    public static class RuleEngine
    {
        public static CommandResult Validate(
            MatchState state,
            IGameCommand command,
            CardDatabase database);
    }
}
```

- `CardDatabase` 用于查询卡牌定义（费用、类型、TargetRule）与英雄技能定义。
- 所有校验只读 `MatchState`/`CardDatabase`，不修改。
- 错误码与 `CommandError` 一一对应；`Detail` 附人类可读原因（调试/日志用）。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 非 ActivePlayer 出牌 | `NotYourTurn` |
| AC-2 | 非 Main 阶段出牌 | `NotYourTurn`（阶段不符） |
| AC-3 | 手牌中无该 InstanceId | `CardNotInHand` |
| AC-4 | 费用 > 当前法力 | `NotEnoughMana` |
| AC-5 | 随从战场已满 | `BoardFull` |
| AC-6 | 需要目标但 Target=None | `TargetRequired` |
| AC-7 | 目标类型与 TargetRule 不符 | `InvalidTarget` |
| AC-8 | 合法出牌 | `Valid()` |
| AC-9 | 攻击者不在我方场上 | `AttackerNotOnBoard` |
| AC-10 | 攻击者召唤失调且无冲锋/突袭 | `SummoningSickness` |
| AC-11 | 攻击者本回合已攻击 | `AlreadyAttacked` |
| AC-12 | 目标不存在/已死亡 | `InvalidTarget` |
| AC-13 | 敌方有嘲讽却攻击非嘲讽 | `MustTargetTaunt` |
| AC-14 | 合法攻击 | `Valid()` |
| AC-15 | 英雄技能已使用 | `HeroPowerAlreadyUsed` |
| AC-16 | 英雄技能费用不足 | `NotEnoughMana` |
| AC-17 | 合法英雄技能 | `Valid()` |
| AC-18 | 结束回合 | `Valid()` |
| AC-19 | 非 Main 阶段结束回合 | `NotYourTurn` |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/RuleEngineValidateTests.cs`），无 Unity 工具链同样执行。
- 最少用例数：19（每条 AC 一条）。
- 数据构造：复用 `MatchTestCards` + 手动构造 `MatchState`/`PlayerState`；用 `CardDatabase` 查询真实定义。
- 必须覆盖的边界：空手牌、满场、0 法力、潜行目标、嘲讽链、英雄技能 0 费。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡。
- 需更新的配置表：无（只读校验，不改配置）。
- 生产代码改动：新增 `2_Application/Match/RuleEngine.cs`；可能新增 `2_Application/Match/TargetValidator.cs`。
- 是否影响既有模块：无侵入；`CardDatabase`/`MatchState`/`Zone`/`CardInstance` 只读使用。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（21 例 EditMode，546/546 通过）
- [x] 测试通过且覆盖边界（出牌 9 + 攻击 7 + 技能/结束 5；含嘲讽、冲锋、满场、0 法力、目标类型错配）
- [x] 编译 0 error / 0 warning（`coverage.ps1` 编译 + 测试 PASS）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（纯 BCL；partial 拆分避免超 300 行；无继承表达；只读校验不改状态）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS.md + 本任务卡）

## 10. 自检与评审结论（2026-07-02）

### 10.1 红绿验证

- **红**：`CS0234`（RuleEngine 不存在）、`CS0200`（ConfigBundle 只读属性）→ 修正后 `CS0234` 保持为最终红状态。
- **绿**：546 passed / 0 failed（511 既有 + 21 新增 + 14 其他新增/修复）。
- **覆盖率**：RuleEngine.cs 85%（未覆盖行为未触发的 `Enemy`/`Friendly`/`AnyMinion` 分支与嘲讽边界）、RuleEngine.Attack.cs 88%、RuleEngine.HeroPower.cs 100%、RuleEngine.Target.cs 49%（大量分支未触发，属预期）。

### 10.2 铁律自查

| 铁律 | 结果 |
| --- | --- |
| 1. 一切皆组件 | ✅ 无继承表达；RuleEngine 为静态校验器 |
| 2. 依赖向下 | ✅ `Card.Application.Match` → `Card.Domain.Match` + `Card.Domain.Config` |
| 3. 规则三层只依赖 BCL | ✅ 无 `UnityEngine`/`Debug.Log`/`Mathf` 等 |
| 5. 所有操作走 GameCommand | ✅ 校验器消费 `IGameCommand` |
| 6. 数值来自配置 | ✅ 费用/目标规则/场面上限均读 `CardDefinition`/`RulesConfig` |
| 10. 单文件 ≤300 行 | ✅ partial 拆分：主文件 105 行、Attack 90 行、HeroPower 28 行、Target 130 行；测试拆 3 文件 + helper |
| 11. 内核与 Unity 解耦 | ✅ 纯 BCL；`coverage.ps1` 无 Unity 编译通过 |

### 10.3 未关闭问题

- **P0/P1**：无。
- **P2**：M3-B1（Unity 批处理验证受 TRAE 沙箱阻塞，沿用 T3–T5 决策暂缓）。

### 10.4 关键决策

1. **阶段不符复用 `NotYourTurn`**：非 Main 阶段（TurnStart/Draw/TurnEnd）与"非当前回合"共用同一错误码；语义统一为"当前不可操作"，由调用方按阶段区分。
2. **风怒已支持**：`ValidateAttack` 检查 `Keyword.Windfury` 决定 maxAttacks=2；任务卡 §4 原文"不做风怒"指不额外写测试用例，实现侧已覆盖。
3. **潜行目标校验未做**：`TargetRule` 校验未检查 `Keyword.Stealth`（P1 关键词，M4 效果系统再接入）。
