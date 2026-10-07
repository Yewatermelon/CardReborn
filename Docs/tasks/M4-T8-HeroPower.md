# 任务卡 · M4-T8 英雄技能

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T8 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | Docs/01 §3.6、FR-5.10 英雄技能 |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T8：`HeroPowerCommand` 结算——消耗法力、执行技能效果、设置本回合已用；单测：技能消耗法力、执行效果、本回合只能用一次） |
| 预估 | 1 会话 |
| 依赖 | M4-T2（MatchController/RuleEngine.HeroPower 校验）、M4-T4（效果框架）、M4-T5（TriggerDispatcher/EffectParser）、M3（`UseHeroPowerCommand`、`HeroState.PowerUsedThisTurn`、`ManaPool.Spend`） |

## 2. 目标（一句话）

> 实现 `HeroPowerSettler`：处理 `UseHeroPowerCommand`——消耗法力 → 解析技能配置效果并执行（直接调 `EffectExecutor`，走 T4 效果框架）→ 标记 `Hero.PowerUsedThisTurn = true` → emit 事件；注册到 `MatchController`。

## 3. 范围（做什么）

### 3.1 Application 层新增

- `HeroPowerSettler`（`Assets/_Project/2_Application/Match/`）：
  - `CanSettle`：`command is UseHeroPowerCommand`
  - `Settle`：
    1. 取 `PlayerState player = ctx.State.GetPlayer(cmd.PlayerId)`
    2. 取 `HeroPowerDefinition power = ctx.Database.RequireHeroPower(player.Hero.HeroPowerKey)`
    3. `player.Mana.Spend(power.Cost)`（失败抛 InvalidOperationException，校验期已保证不会失败）
    4. `player.Hero.PowerUsedThisTurn = true`
    5. 解析 `power.Effects` 为 `IReadOnlyList<TriggeredEffect>`（`EffectParser.Parse`）
    6. 对每个效果：直接构造 `EffectContext` 并调 `EffectExecutor.Execute`（不走 Dispatcher——技能效果是即时效果，不是 Trigger 时机）
    7. emit `HeroPowerUsedEvent`（若不存在则新增；先查 GameEvents.cs）

### 3.2 Application 层改动

- `MatchController`：构造函数注册 `new HeroPowerSettler()`

### 3.3 测试

- `HeroPowerSettlerTests`：
  - 技能消耗法力（2 费技能，法力从 3 → 1）
  - 技能执行效果（火冲 DamageEffect:1 对敌方英雄造成伤害）
  - 本回合只能用一次（第二次被 RuleEngine 拒绝 `HeroPowerAlreadyUsed`）
  - 目标随从伤害（火冲对随从造成 1 伤害）
  - 无目标技能（默认对自己英雄生效）

## 4. 明确不做

- **不实现技能替换**（如"换英雄技能"效果）
- **不实现技能次数异常**（如"本回合可用 2 次"）
- **不处理技能触发链**（技能效果是即时效果，不走 OnPlay/OnDeath 等 Trigger）
- **不改变技能配置格式**（沿用 T4 的 `EffectParser.Parse`）

## 5. 接口约定

```csharp
public sealed class HeroPowerSettler : ICommandSettler
{
    public bool CanSettle(IGameCommand command) => command is UseHeroPowerCommand;
    public void Settle(SettlementContext context, IGameCommand command);
}
```

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 技能消耗法力 | 2 费技能，法力从 3 → 1 |
| AC-2 | 技能执行效果 | 火冲对敌方英雄造成 1 伤害，Health 减 1 |
| AC-3 | 本回合只能用一次 | 第二次被 RuleEngine 拒绝 `HeroPowerAlreadyUsed` |
| AC-4 | 目标随从伤害 | 火冲对随从造成 1 伤害，随从 Health 减 1 |
| AC-5 | 无目标技能 | 默认对自己英雄生效（简化） |
| AC-6 | 门禁/解耦 | Application 层；check.ps1 PASS；单文件 ≤ 300 行 |

## 7. 测试要求

EditMode，≥ 5 例；先红。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更（`HeroPowerDefinition.Effects` 已存在）。
- 既有模块：改 `MatchController.cs`（注册 HeroPowerSettler）；可能加 `HeroPowerUsedEvent`。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | 技能效果走 TriggerDispatcher 还是直接 EffectExecutor？ | **直接 EffectExecutor**——技能效果是即时效果，不是 Trigger 时机（OnPlay/OnDeath 等）。Dispatcher 用于卡牌触发，技能效果直接执行。 |
| Q2 | 需要新事件吗？ | 先查 GameEvents.cs 是否有 `HeroPowerUsedEvent`；若无则新增（记录 playerId、powerKey）。 |
| Q3 | 技能目标为 None 时效果作用于谁？ | 沿用 T4 简化：**对自己英雄生效**（自伤型技能）。 |
| Q4 | 技能费用/效果校验失败？ | 结算期失败属装配错误，直接抛 `InvalidOperationException`（与 T4/T6 一致）。 |

## 10. DoD

- [x] 全部 AC 满足且附证据（AC-1~6 见 §6；6 个测试全绿）
- [x] 先红后绿（CS0246 → 5 过 1 败 → 重写 TargetRule.None 测试 → 714/0 全绿）
- [x] 0 error / 0 warning（无 Unity 工具链 714 passed / 0 failed；Unity 编辑器补验 **734 passed / 0 failed**）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-07）

### 六步自检

1. **编译**：无 Unity 工具链 714 passed / 0 failed / 0 warning；`check.ps1` PASS（195 文件）
2. **测试**：6 例新增（耗法力/对英雄伤害/每回合一次/对随从伤害/TargetRule.None 无目标/emit 事件），先红后绿
3. **覆盖率**：HeroPowerSettler 94%（31/33），汇总 0_Core 96.52% / Domain+App 91.65%
4. **铁律**：Application 层；无 UnityEngine 引用；单文件 ≤ 300 行（HeroPowerSettler 33 行，测试 129 行）
5. **解耦**：技能效果直接走 EffectExecutor（Q1），不走 TriggerDispatcher；事件沿用 DamageEvent 未新增
6. **范围**：未实现技能替换/次数异常/触发链（§4 明确不做）

### 评审结论

无 P0/P1。AC-1~6 全部满足，门禁通过。

### 双环境验证

- 无 Unity 工具链：**714 passed / 0 failed**
- Unity 编辑器 Test Runner（EditMode）用户实跑补验：**734 passed / 0 failed**（= 728 + 6 新例，与预期一致）
