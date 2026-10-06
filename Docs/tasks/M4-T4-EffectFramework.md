# 任务卡 · M4-T4 效果框架

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T4 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | FR-4.2 效果组件清单（Docs/01 §4.2）、FR-5.12 战斗日志、NFR-5 可扩展性（新增效果 ≤ 2 类 + 1 配置） |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T4：`IEffectData`、`EffectExecutor`、`EffectContext`；单测：伤害/治疗/抽牌/召唤/增益各自正确） |
| 预估 | 1 会话 |
| 依赖 | M4-T2（`MatchController`/`ICommandSettler` 接缝、`SettlementContext`）、M4-T3（`GameEvent` 家族、`IEventSink`）、M3（`CardDefinition.Effects` 字符串列表、`PlayCardCommand`、`HeroState`/`CardInstance`/`Zone`） |

## 2. 目标（一句话）

> 在 Domain 定义 `IEffectData` 效果数据契约 + 5 种首版效果数据（伤害/治疗/抽牌/召唤/增益）+ `EffectParser`（配置字符串→效果数据）；在 Application 定义 `IEffectExecutor`/`EffectContext`/`EffectExecutor` 分发器 + 5 个执行器；实现 `PlayCardSettler` 并注册到控制器，完成"出牌→消费法力→进场→执行战吼效果→产事件"的端到端链路；每种效果单测独立可验证。

## 3. 范围（做什么）

### 3.1 Domain 层新增（`Assets/_Project/1_Domain/Match/Effects/`）

- `IEffectData`：标记接口（效果数据契约，可组合）。
- 5 个效果数据 `sealed class`（不可变，构造即校验非负）：
  - `DamageEffectData(int Amount)`
  - `HealEffectData(int Amount)`
  - `DrawCardEffectData(int Count)`
  - `SummonEffectData(string CardKey, int Count)`
  - `BuffEffectData(int Attack, int Health)`（Health 可正可负，攻击同理）
- `EffectParser`：静态 `Parse(IReadOnlyList<string> expressions) → IReadOnlyList<IEffectData>`；格式 `TypeName:param1,param2`，如 `DamageEffect:3`、`BuffEffect:2,3`。未知类型/格式错误抛 `ArgumentException`（配置导入校验 M2 已覆盖，此处为运行时兜底）。

### 3.2 Domain 层改动

- `HeroState` 新增方法（规则内聚，执行器只调用 + 产事件）：
  - `int TakeDamage(int amount)`：护甲先抵，返回实际扣血量（= amount − 消耗护甲，若 amount > 护甲则扣血 = amount − 护甲）；Health 可降至 ≤ 0（T7 判负）。
  - `int Heal(int amount)`：不超过 MaxHealth，返回实际治疗量。

### 3.3 Application 层新增（`Assets/_Project/2_Application/Match/Effects/`）

- `EffectContext`：`MatchState State`、`CardDatabase Database`、`IEventSink Events`、`CardInstance SourceCard`（打出的卡）、`int SelfSeat`、`TargetRef CommandTarget`。
- `IEffectExecutor`：`void Execute(IEffectData effect, EffectContext context)`（失败抛异常——结算期校验已通过，执行失败属装配错误）。
- 5 个执行器 `sealed class`：
  - `DamageExecutor`：解析 `CommandTarget` → 英雄走 `Hero.TakeDamage`（产 `DamageEvent(TargetHeroSeat)`），随从直接 `Health -= amount`（产 `DamageEvent(TargetInstanceId)`）。
  - `HealExecutor`：英雄 `Hero.Heal`（产 `HealingEvent(TargetHeroSeat)`），随从 `Health = Min(Health + amount, MaxHealth)`（产 `HealingEvent(TargetInstanceId)`）。
  - `DrawCardExecutor`：调 `CardDrawService.Draw(selfPlayer, count)`，产 `CardDrawn`/`CardBurned`/`Fatigue` 事件（复用 EndTurnSettler 的 EmitDrawEvents 思路，或抽公共助手）。
  - `SummonExecutor`：从 `Database.RequireCard(CardKey)` 创建实例，加入 `selfPlayer.Board`；产 `CardPlayed` 之外的进场（T4 暂不产 Summon 事件类型——现有 GameEvent 无 SummonEvent，用 CardPlayedEvent 不够精确；**决定**：SummonExecutor 不产事件，进场在 PlayCardSettler 统一产 `CardPlayedEvent`；召唤的随从进场本身不额外产事件，留 T5 触发链补 OnSummon 事件）。
  - `BuffExecutor`：解析 `CommandTarget`（随从），`Attack += buff.Attack`、`Health += buff.Health`（T4 不区分临时/永久，统一改基础值；临时增益留 T5）。
- `EffectExecutor`（分发器）：`Dictionary<Type, IEffectExecutor>` 注册 5 个执行器；`Execute(IEffectData, EffectContext)` 按类型分发，未知类型抛异常。

### 3.4 Application 层新增/改动

- `PlayCardSettler : ICommandSettler`（`Assets/_Project/2_Application/Match/`）：
  1. `CanSettle` = `command is PlayCardCommand`。
  2. `Settle`：按 `cmd.CardInstanceId` 从手牌取出卡 → `Mana.Spend(cost)` → 从手牌移除 → 随从加入己方战场 / 法术移入坟场 → 解析 `CardDefinition.Effects` → `EffectExecutor.Execute` 逐个执行 → 产 `CardPlayedEvent(seat, instanceId)`。
- `MatchController` 构造时注册 `PlayCardSettler`。

### 3.5 测试（`Assets/_Project/7_Tests/EditMode/Match/`）

- `EffectParserTests`：5 种效果各 1 例 + 未知类型抛异常 + 多参数解析。
- `DamageExecutorTests`：英雄受伤（护甲先抵）、随从受伤。
- `HealExecutorTests`：英雄治疗不超上限、随从治疗不超 MaxHealth。
- `DrawCardEffectTests`：抽牌入手牌、空库疲劳。
- `SummonEffectTests`：召唤随从到己方战场（含战场满拒绝？T4 暂不处理战场满——召唤失败抛异常，留 T5）。
- `BuffEffectTests`：随从攻/血增减。
- `PlayCardSettlerTests`：出牌消耗法力、随从进场、法术进坟场、战吼伤害生效、产 `CardPlayedEvent` + 对应效果事件。
- 回归：T2/T3 既有测试零改动全绿。

## 4. 明确不做

- **不做亡语/触发链/临时增益**（T5）：`OnDeath`、`OnTurnStart`、临时 Buff 到期全部留 T5。
- **不做 Destroy/GainMana/GainArmor/Silence/DamageAll/Conditional/Composite 效果**（Docs/01 §4.2 其余 7 种，留后续）。
- **不做效果级 target 参数字段**：战吼目标统一来自 `PlayCardCommand.Target`（`EffectContext.CommandTarget`）；自伤/自愈型卡由卡牌 TargetRule 限定为 None 时执行器取自己英雄/随从（T4 实现：若命令目标 None 且效果需要目标，对自己英雄生效——简化）。
- **不做嘲讽/圣盾/剧毒在效果里的处理**（圣盾在伤害执行器 T6 处理；T4 的 DamageExecutor 不处理圣盾，直接扣血）。
- **不做效果配置的 Excel/JSON 导入扩展**：`CardDefinition.Effects` 已是字符串列表，T4 只加运行时解析器；M2 导入器不扩展字段。
- **不产 SummonEvent**：现有 GameEvent 家族无此类型，召唤随从进场不额外产事件（留 T5 补 `OnSummon`）。

## 5. 接口约定

```csharp
// Domain
namespace Card.Domain.Match.Effects {
    public interface IEffectData { }
    public sealed class DamageEffectData : IEffectData { public int Amount { get; } }
    // ... Heal/Draw/Summon/Buff
    public static class EffectParser {
        public static IReadOnlyList<IEffectData> Parse(IReadOnlyList<string> expressions);
    }
}

// Application
namespace Card.Application.Match.Effects {
    public sealed class EffectContext { /* State, Database, Events, SourceCard, SelfSeat, CommandTarget */ }
    public interface IEffectExecutor { void Execute(IEffectData effect, EffectContext context); }
    public sealed class EffectExecutor { /* 注册 + 分发 */ }
}

// Domain HeroState 新增
public int TakeDamage(int amount);
public int Heal(int amount);
```

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | EffectParser 解析 5 种效果 | 类型与参数正确；未知类型抛异常 |
| AC-2 | Damage 英雄 | 护甲先抵，Health 正确减少，产 DamageEvent(TargetHeroSeat) |
| AC-3 | Damage 随从 | 随从 Health 减少，产 DamageEvent(TargetInstanceId) |
| AC-4 | Heal 英雄 | 不超 MaxHealth，产 HealingEvent |
| AC-5 | Draw 抽牌 | 卡牌入手牌，产 CardDrawnEvent；空库产 FatigueEvent |
| AC-6 | Summon 召唤 | 新随从实例在己方战场，InstanceId 唯一 |
| AC-7 | Buff 随从 | 攻击/生命按参数增减 |
| AC-8 | PlayCard 端到端 | 法力扣除、随从进场/法术进坟场、战吼效果执行、产 CardPlayedEvent + 效果事件 |
| AC-9 | 无副作用方法 | 拒绝命令不改变状态、不产事件（T3 回归） |
| AC-10 | 门禁/解耦 | Domain 仅 BCL；Application 仅 BCL+Core+Domain；check.ps1 PASS；单文件 ≤300 行 |

## 7. 测试要求

EditMode，≥ 15 例；先红（`IEffectData`/`EffectExecutor`/`PlayCardSettler` 未定义 → CS0246）；禁止改 T2/T3 老测试。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更（`CardDefinition.Effects` 字符串列表已存在）。
- 既有模块：改 `HeroState`（+2 方法）、`MatchController`（注册 PlayCardSettler）。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | 效果数据/解析器放 Domain？ | **是**（纯数据+纯函数，状态下发/回放复用）。执行器放 Application（改状态）。 |
| Q2 | 伤害规则放 HeroState 还是执行器？ | **HeroState**（护甲先抵是核心规则，内聚在 Domain；执行器只调用+产事件）。随从伤害直接改 Health（无护甲）。 |
| Q3 | 战吼目标来源？ | **PlayCardCommand.Target**（EffectContext.CommandTarget）；不引入效果级 target 参数。目标 None 时效果对自己英雄生效（简化）。 |
| Q4 | PlayCardSettler 在 T4 实现？ | **是**（T2 留接缝，T4 注册）。亡语/触发链留 T5。 |
| Q5 | 临时增益？ | **不做**（T4 Buff 改基础值；临时增益/到期留 T5）。 |
| Q6 | 圣盾在 DamageExecutor 处理？ | **否**（圣盾属战斗结算，留 T6 CombatResolver；T4 伤害直接扣血）。 |

## 10. DoD

- [x] 全部 AC 满足且附证据（见 11.1）
- [x] 先红后绿（红 CS0246/CS0234 `IEffectData`/`EffectContext`/`Effects` 命名空间）；T2/T3 既有测试零改动（仅 T2 接缝测试 `Submit_ValidPlayCardWithoutSettler` 前提失效，改为验证出牌成功）
- [x] 0 error / 0 warning（无 Unity 实跑；Unity 编辑器 EditMode 补验 **699 passed / 0 failed**，2026-10-06 用户回报）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1（见 11.2/11.6）
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-06）

### 11.1 AC 对齐

| AC | 结论 | 证据 |
| --- | --- | --- |
| AC-1 EffectParser 解析 5 种 + 未知抛异常 | 已满足 | `EffectParserTests`（8 例） |
| AC-2 Damage 英雄（护甲先抵） | 已满足 | `HeroStateCombatTests` 3 例 + `EffectExecutorTests.Damage_Hero_ArmorFirst_ThenHealth` |
| AC-3 Damage 随从 | 已满足 | `EffectExecutorTests.Damage_Minion_ReducesHealth` |
| AC-4 Heal 英雄不超上限 | 已满足 | `HeroStateCombatTests` + `EffectExecutorTests.Heal_Hero_CappedAtMax` |
| AC-5 Draw 抽牌/疲劳 | 已满足 | `EffectExecutorTests.DrawCard_AddsToHand` / `DrawCard_EmptyDeck_Fatigue` |
| AC-6 Summon 召唤 | 已满足 | `EffectExecutorTests.Summon_AddsMinionToOwnBoard`（InstanceId 唯一，由 MatchState.AllocateInstanceId 保证） |
| AC-7 Buff 随从 | 已满足 | `EffectExecutorTests.Buff_IncreasesAttackAndHealth` |
| AC-8 PlayCard 端到端 | 已满足 | `PlayCardSettlerTests` 3 例（随从进场/法术进坟场/战吼伤害）+ `MatchControllerRejectTests.Submit_ValidPlayCard_SucceedsAndMutatesState` |
| AC-9 拒绝命令零副作用 | 已满足 | T3 既有 `RejectedCommand_EmitsNoEvents_ButRecordsLedger` 未改动；T2 拒绝路径测试全绿 |
| AC-10 门禁/解耦 | 已满足 | Domain 仅 BCL+Core；Application 仅 BCL+Core+Domain；`check.ps1` PASS（185 文件）；最大文件 Effects.cs 232 行 |

### 11.2 铁律扫描

- 依赖向下：效果数据/解析器在 1_Domain（仅 BCL + Card.Core）；执行器/上下文/出牌结算器在 2_Application；无 `UnityEngine`。
- 组件优于继承：5 个效果数据类 + 5 个执行器类各自独立，无玩法继承；`EffectExecutor` 用类型字典分发，符合"新增效果 ≤ 2 类"扩展要求（NFR-5）。
- 状态只存数据：`IEffectData` 纯数据；`EffectContext` 持状态引用但自身不可变。
- 权威解耦：效果结算只在控制器流水线内执行；`EffectExecutor` 无 static 全局，每局由 `PlayCardSettler` 持有实例。
- 体量：GameEffects.cs 132 行、Effects.cs 232 行、PlayCardSettler.cs 74 行；最长方法 Settle 约 30 行。

### 11.3 关键设计取舍

1. **效果数据/解析器放 Domain**：纯数据+纯函数，状态下发/回放可复用；执行器放 Application（改状态、产事件）。
2. **伤害规则内聚 HeroState**：护甲先抵是核心规则，放 Domain；随从无护甲直接扣血。
3. **战吼目标来自 PlayCardCommand.Target**：不引入效果级 target 参数；目标 None 时对自己英雄生效（简化自伤型法术）。
4. **PlayCardSettler 在 T4 实现**：T2 留接缝，T4 注册并完成端到端。
5. **InstanceId 分配器**：给 MatchState 加 `AllocateInstanceId()`，MatchFactory 设初值 `2*DeckSize+1`，召唤随从用全场唯一 Id。
6. **临时增益不做**：T4 Buff 改基础值；临时增益/到期留 T5。
7. **圣盾不处理**：圣盾属战斗结算，留 T6；T4 伤害直接扣血。

### 11.4 测试证据

- 红：CS0246/CS0234（`IEffectData`/`EffectContext`/`Card.Application.Match.Effects` 命名空间不存在）。
- 绿：无 Unity **679 passed / 0 failed / 0 skipped**（653 + 26：解析器 8 + 英雄战斗 6 + 执行器 9 + 出牌 3），0 warning。
- 覆盖率：`GameEffects.cs` 81%（EffectParser 错误分支未全覆）、`HeroState.cs` 94%、`Effects.cs` 执行器全覆盖；汇总 0_Core 96.51%、Domain+App **91.25%**。
- 静态门禁：`check.ps1` PASS（185 文件）；7 个新 .meta，277 GUID 无重复。
- 既有测试：T2 接缝测试 `Submit_ValidPlayCardWithoutSettler_ThrowsAndStateStaysIntact` 前提失效，改为 `Submit_ValidPlayCard_SucceedsAndMutatesState` 验证出牌成功；其余 T2/T3 测试零改动。
- Unity 权威：2026-10-06 用户在 Unity 2022.3.54f1c1 编辑器 Test Runner（EditMode）实跑回报 **699 passed / 0 failed**（673 + 26），新增脚本与 .meta 导入识别无误。

### 11.5 影响面

- 新增 Domain：`GameEffects.cs`（`IEffectData` + 5 效果数据 + `EffectParser`）。
- 新增 Application：`Effects.cs`（`EffectContext` + `IEffectExecutor` + `EffectExecutor` 分发 + 5 执行器）、`PlayCardSettler.cs`。
- 改动 Domain：`HeroState`（+`TakeDamage`/`Heal`）、`MatchState`（+`NextInstanceId`/`AllocateInstanceId`）。
- 改动 Application：`MatchFactory`（设 NextInstanceId 初值）、`MatchController`（注册 PlayCardSettler）、`ICommandSettler`（SettlementContext.Events 改 EventLog 类型）。
- 后继：T5 触发链（亡语/回合开始结束/临时增益）、T6 CombatResolver（圣盾/嘲讽/剧毒/同时伤害）、T7 死亡管线。

### 11.6 反向审查

1. **EffectParser 错误分支覆盖率 81%**：Summon/Buff 参数数量错误分支未测，但运行时配置由 M2 导入器校验，错误配置不会进入运行时；不影响门禁。可后续补测。
2. **Buff 目标必须是随从**：若命令目标为英雄，BuffExecutor 抛异常。这是合理的——增益随从效果不应指向英雄。
3. **Summon 战场满抛异常**：T4 不处理战场满的优雅失败，直接抛 InvalidOperationException（结算期不应发生，因为 RuleEngine 校验出牌时不校验召唤数量）。留 T5/T6 处理召唤上限。
4. **PlayCardSettler 的 EffectParser 每次出牌都解析**：性能可接受（每局出牌次数有限）；如需缓存可在 T5 引入 CardDefinition 级别的预解析缓存。
5. **T2 接缝测试改造**：`Submit_ValidPlayCardWithoutSettler` → `Submit_ValidPlayCard_SucceedsAndMutatesState`，这是接缝被填上的正常演进，测试语义从"无结算器抛异常"改为"有结算器成功出牌"。

### 11.7 评审结论

**通过**（AI 自审）：无 P0/P1；P2：EffectParser 错误分支覆盖率可后续补测。双环境证据齐备（无 Unity 679/0、Unity EditMode 699/0），本任务关闭。
