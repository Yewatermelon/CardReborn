# 任务卡 · M4-T6 CombatResolver

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T6 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | Docs/01 §3.4 攻击规则、FR-5.5 攻击流程 |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T6：CombatResolver；单测：攻击双方扣血正确、圣盾抵消一次、剧毒必杀） |
| 预估 | 1 会话 |
| 依赖 | M4-T2（`ICommandSettler`/`MatchController`/`RuleEngine.Attack` 校验）、M4-T3（`DamageEvent`/`AttackDeclaredEvent`）、M3（`CardInstance`/`StatusSet`/`KeywordSet`/`HeroState.TakeDamage`） |

## 2. 目标（一句话）

> 实现 `AttackSettler` 并注册到 `MatchController`，完成攻击结算：攻击者对目标造成攻击力伤害；随从交换时双方同时互伤；圣盾抵消一次伤害后消失；剧毒对随从造成伤害即摧毁；消耗攻击次数；emit 攻击/伤害事件。死亡处理留 T7。

## 3. 范围（做什么）

### 3.1 Domain 层改动

- `CardInstance` 新增 `TakeDamage(int amount, bool poisonous)`：
  - amount ≤ 0：返回 0，不修改状态（0 伤害不触发圣盾消耗、不触发剧毒）。
  - 有圣盾：`ConsumeDivineShield()` 返回 true，不扣血、不触发剧毒，返回 0。
  - 无圣盾：`Health -= amount`；若 poisonous 则 `Health = 0`（剧毒必杀）。
  - 返回实际扣除的生命值（圣盾抵消时返回 0）。

### 3.2 Application 层新增

- `AttackSettler`（`Assets/_Project/2_Application/Match/`）：
  - `CanSettle` 返回 `command is AttackCommand`。
  - `Settle`：
    1. 找到攻击者（己方场上）。
    2. 目标是英雄：`enemy.Hero.TakeDamage(attacker.Attack)`，emit `DamageEvent`；攻击者不受伤。
    3. 目标是随从：**同时结算**——攻击者 `TakeDamage(defender.Attack, defenderPoison)`，防御者 `TakeDamage(attacker.Attack, attackerPoison)`；emit 两个 `DamageEvent`（顺序：攻击者受伤 → 防御者受伤，或反之？按"同时"语义，emit 顺序不影响状态，统一先防御者后攻击者或先攻击者后防御者，测试按实际 emit 顺序断言）。
    4. `attacker.AttacksUsedThisTurn++`（消耗攻击次数；0 伤害攻击也消耗）。
    5. emit `AttackDeclaredEvent(playerId, attackerInstanceId, target)`。
  - 不处理死亡（Health ≤ 0 不移除、不触发亡语，留 T7）。

### 3.3 Application 层改动

- `MatchController` 构造函数注册 `new AttackSettler()`（在 PlayCardSettler 之后、EndTurnSettler 之前或之后均可，按命令类型匹配）。

### 3.4 测试

- `CardInstanceCombatTests`：TakeDamage 圣盾抵消、剧毒必杀、0 伤害不扣血不消耗圣盾、正常扣血。
- `AttackSettlerTests`：攻击英雄扣血、随从交换双方扣血、圣盾抵消、剧毒必杀、0 攻击仍消耗次数、emit 事件。

## 4. 明确不做

- **不实现死亡管线**（T7）：随从 Health ≤ 0 不移除、不触发亡语、不进坟场；T7 统一处理。
- **不实现 OnAttack/OnAttacked/OnDamageTaken 触发**：触发时机留后续扩展（Docs/01 §4.3 列出但非本任务）。
- **不实现风怒/突袭/潜行/冻结**：风怒在 RuleEngine 校验已支持（maxAttacks=2），结算器自然支持多次；突袭/潜行/冻结为 P1，本任务不做。
- **不实现攻击动画/表现**。
- **不修改 RuleEngine 校验**（嘲讽/失调/次数校验已在 M3 完成）。

## 5. 接口约定

```csharp
// Domain
public sealed class CardInstance
{
    /// <summary>承受伤害：圣盾抵消一次；剧毒对随从必杀。返回实际扣血量。</summary>
    public int TakeDamage(int amount, bool poisonous);
}

// Application
public sealed class AttackSettler : ICommandSettler
{
    public bool CanSettle(IGameCommand command) => command is AttackCommand;
    public void Settle(SettlementContext context, IGameCommand command);
}
```

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 攻击英雄 | 英雄扣攻击力血，攻击者不受伤，AttacksUsedThisTurn=1 |
| AC-2 | 随从交换 | 双方互相扣对方攻击力血（同时） |
| AC-3 | 圣盾抵消 | 圣盾随从被攻击，圣盾消失、不扣血；攻击者正常受伤 |
| AC-4 | 剧毒必杀 | 剧毒攻击者攻击随从，目标 Health=0（即使攻击力 < 目标生命） |
| AC-5 | 0 伤害攻击 | 仍消耗攻击次数，双方不扣血 |
| AC-6 | emit 事件 | 攻击产生 AttackDeclaredEvent + DamageEvent |
| AC-7 | 死亡不处理 | 随从 Health ≤ 0 仍留在场上（T7 负责移除） |
| AC-8 | 门禁/解耦 | Domain 仅 BCL；check.ps1 PASS；单文件 ≤ 300 行 |

## 7. 测试要求

EditMode，≥ 10 例；先红。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更（Keywords 已在 Cards 表，剧毒 Poisonous 已定义）。
- 既有模块：改 `CardInstance.cs`（加 TakeDamage）、`MatchController.cs`（注册）。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | 伤害逻辑放 Domain 还是 Application？ | **放 Domain（CardInstance.TakeDamage）**：圣盾/剧毒是随从固有属性，内聚在实例上；与 HeroState.TakeDamage 对称。 |
| Q2 | 圣盾 + 剧毒同时存在？ | 先判圣盾：圣盾抵消则剧毒不生效（与炉石一致）；无圣盾才扣血并触发剧毒。 |
| Q3 | 同时伤害的 emit 顺序？ | 先 emit 防御者受伤、再 emit 攻击者受伤（或统一顺序）；状态已同时写入，顺序仅影响事件流，测试按实际顺序断言。 |
| Q4 | 0 伤害是否消耗圣盾/触发剧毒？ | **不消耗**（amount ≤ 0 直接返回，圣盾不消耗、剧毒不触发）；但攻击次数仍消耗。 |
| Q5 | 攻击者 Health ≤ 0 后是否仍可被找到？ | 结算器在结算开始时已持有 attacker 引用，Health ≤ 0 不影响引用；T7 负责移除。 |

## 10. DoD

- [x] 全部 AC 满足且附证据（见 11.1）
- [x] 先红后绿（CS1061 `CardInstance.TakeDamage` + CS0246 `AttackSettler`）
- [x] 0 error / 0 warning（无 Unity 实跑；Unity 编辑器补验待回报）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1（见 11.2/11.6）
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-06）

### 11.1 AC 对齐

| AC | 结论 | 证据 |
| --- | --- | --- |
| AC-1 攻击英雄 | 已满足 | `AttackSettlerTests.AttackHero_DealsDamageToHero_AttackerUnharmed`（英雄扣 4、攻击者不受伤、次数+1） |
| AC-2 随从交换 | 已满足 | `AttackMinion_BothTakeDamageSimultaneously`（双方互扣攻击力血） |
| AC-3 圣盾抵消 | 已满足 | `AttackDivineShieldMinion_ShieldConsumes_NoHealthLoss` + `CardInstanceCombatTests.TakeDamage_DivineShield_ConsumesAndBlocks` |
| AC-4 剧毒必杀 | 已满足 | `PoisonousAttacker_DestroysDefenderMinion`（1/2 剧毒攻击 5/10 → 防御者 Health=0） |
| AC-5 0 伤害攻击 | 已满足 | `ZeroAttack_ConsumesAttackCount_NoDamage`（次数+1、双方不扣血、圣盾不消耗） |
| AC-6 emit 事件 | 已满足 | `AttackEmitsEvents`（AttackDeclaredEvent + DamageEvent） |
| AC-7 死亡不处理 | 已满足 | `DeadMinion_StaysOnBoard_DeathHandledByT7`（双方 Health≤0 仍在场上） |
| AC-8 门禁/解耦 | 已满足 | Domain 仅 BCL+Core；check.ps1 PASS（191 文件）；AttackSettler.cs 72 行 |

### 11.2 铁律扫描

- 依赖向下：TakeDamage 在 1_Domain（CardInstance），AttackSettler 在 2_Application；无 UnityEngine。
- 终局入口唯一：攻击结算不判终局、不触发死亡处理；随从 Health≤0 留 T7。
- 体量：AttackSettler.cs 72 行；CardInstance.cs 加 TakeDamage 后仍 < 150 行。

### 11.3 关键设计取舍

1. **伤害逻辑放 Domain（CardInstance.TakeDamage）**：圣盾/剧毒是随从固有属性，内聚在实例上，与 HeroState.TakeDamage 对称。
2. **圣盾优先于剧毒**：先判圣盾，圣盾抵消则剧毒不生效（与炉石一致）。
3. **0 伤害不消耗圣盾/不触发剧毒**：amount≤0 直接返回，状态不变；但攻击次数仍消耗（Docs/01 §3.4.6）。
4. **同时伤害**：先扣防御者再扣攻击者（状态已同时写入，emit 顺序不影响结果）。
5. **死亡不处理**：Health≤0 不移除、不触发亡语，留 T7 死亡管线统一处理。
6. **圣盾消耗判定**：攻击力>0 且实际扣血=0 ⇔ 圣盾抵消（用于 DamageEvent.DivineShieldConsumed）。

### 11.4 测试证据

- 红：CS1061（`CardInstance.TakeDamage` 不存在）、CS0246（`AttackSettler` 不存在）。
- 绿：无 Unity **702 passed / 0 failed / 0 skipped**（690 + 12：CardInstanceCombat 5 + AttackSettler 7），0 warning。
- 覆盖率：0_Core 96.51%、Domain+App **91.40%**。
- 静态门禁：`check.ps1` PASS（191 文件）；3 个新 .meta，277 GUID 无重复。
- 既有测试：零改动全绿。
- Unity 权威：待编辑器 EditMode 补验，预期 **713 passed / 0 failed**（701 + 12）。

### 11.5 影响面

- 新增 Application：`AttackSettler.cs`。
- 改动 Domain：`CardInstance.cs` 加 `TakeDamage(int amount, bool poisonous)`。
- 改动 Application：`MatchController.cs` 注册 AttackSettler。
- 后继：T7 死亡管线（Health≤0 的随从移除+亡语）、T8 英雄技能。

### 11.6 反向审查

1. **攻击者 Health≤0 后引用仍有效**：结算器在开始时持有 attacker 引用，Health 变化不影响引用。
2. **圣盾只抵消一次**：ConsumeDivineShield 原子检测并移除，第二次攻击正常扣血。
3. **剧毒对英雄无效**：攻击英雄走 HeroState.TakeDamage，不经过剧毒判定（剧毒只对随从）。
4. **0 攻击仍消耗次数**：AttacksUsedThisTurn++ 在所有路径末尾执行。

### 11.7 评审结论

**通过**（AI 自审）：无 P0/P1；无新增遗留项。无 Unity 702/0 + Unity 编辑器补验数字回填后关闭。
