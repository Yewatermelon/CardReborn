# 任务卡 · M3-T1 对局状态模型

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T1 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.2（法力水晶：每回合上限 +1、回合开始回满、当前/上限可读）、FR-5.6（英雄 30 血）、FR-5.14（规则纯逻辑可测） |
| 规则依据 | [01 §3.1](../01-开发需求文档.md)（基础参数：英雄生命 30 / 法力上限 10）、[01 §3.2](../01-开发需求文档.md)（TurnPhase 六阶段）、[03 §5.9](../03-开发规范文档.md)（内核解耦：状态只存数据、引用用 Id/Key） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | 无（M3 首个任务） |

## 2. 目标（一句话）

> 建立纯 C# 的对局状态骨架（`MatchState` / `PlayerState` / `HeroState` / `ManaPool` + `TurnPhase`），使测试可在无 Unity 环境下构造一局双人对局，并为 M3-T2…T10 的卡牌实例、命令校验、序列化与增量提供挂载点。

## 3. 范围（做什么）

- 新目录 `Assets/_Project/1_Domain/Match/`（命名空间 `Card.Domain.Match`，属 `Card.Domain` 程序集）：
  - `TurnPhase`：`MatchStart / TurnStart / Draw / Main / TurnEnd / MatchEnd`（01 §3.2，阶段流转的校验在 M3-T6/M4）。
  - `ManaPool`：值语义资源对象，`Current` / `Max`；`CanSpend`、`Spend`（业务失败返回 `Result`，不静默）、`BeginTurn(manaLimit)`（上限 +1 至配置封顶、当前回满）。
  - `HeroState`：以 Key 引用配置（`HeroKey` / `HeroPowerKey`），持有 `MaxHealth` / `Health` / `Armor`、`PowerUsedThisTurn`（FR-5.10 每回合 1 次，重置在 M4/T6）。
  - `PlayerState`：固定座位 `Id`（0/1），聚合 `HeroState Hero` 与 `ManaPool Mana`。
  - `MatchState`：两名玩家（按 Id 字典索引，不用集合下标当业务查询）、`Phase`、`TurnNumber`、`ActivePlayerId`；`GetPlayer(id)` / `ActivePlayer` / `Players`。
- EditMode 测试 `Assets/_Project/7_Tests/EditMode/Match/`：法力资源行为、状态构造与非法构造（重复座位 Id / 行动方不存在 / 未知玩家查询）。

## 4. 明确不做（防止范围蔓延）

- **不做** `CardInstance` 与 Zone（牌库/手牌/战场/坟场）：M3-T2。
- **不做** `KeywordSet` / `StatusSet`：M3-T3。
- **不做** `GameCommand` / `CommandResult`：M3-T4。
- **不做** 开局初始化（配置 → 状态映射、洗牌、抽起手、先后手）：M3-T5；本任务只提供可手工构造的状态骨架。
- **不做** 阶段流转与出牌/攻击合法性校验：M3-T6；`TurnPhase` 只是数据枚举。
- **不做** 伤害/治疗/护甲结算行为：M4/T7；`Health`/`Armor` 此刻为可读写数据字段。
- **不做** 序列化与增量：M3-T9/T10。
- **不做** 临时法力（幸运币）：M3-T5；因此 `ManaPool` 构造只约束非负，不强制 `Current ≤ Max`，为后续"只加不删"演进留口。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public enum TurnPhase { MatchStart, TurnStart, Draw, Main, TurnEnd, MatchEnd }

    public sealed class ManaPool
    {
        public int Max { get; }
        public int Current { get; }
        public ManaPool(int max = 0, int current = 0);
        public bool CanSpend(int amount);
        public Card.Core.Result Spend(int amount);          // ERROR_MANA_INSUFFICIENT / ERROR_AMOUNT_INVALID
        public void BeginTurn(int manaLimit);               // Max=Min(Max+1, manaLimit); Current=Max
    }

    public sealed class HeroState
    {
        public string HeroKey { get; }
        public string HeroPowerKey { get; }
        public int MaxHealth { get; }
        public int Health { get; set; }
        public int Armor { get; set; }
        public bool PowerUsedThisTurn { get; set; }
        public HeroState(string heroKey, string heroPowerKey, int maxHealth);
        public HeroState(string heroKey, string heroPowerKey, int maxHealth, int health);
    }

    public sealed class PlayerState
    {
        public int Id { get; }
        public HeroState Hero { get; }
        public ManaPool Mana { get; }
        public PlayerState(int id, HeroState hero, ManaPool mana);
    }

    public sealed class MatchState
    {
        public TurnPhase Phase { get; set; }                // 默认 MatchStart
        public int TurnNumber { get; set; }                 // 默认 1
        public int ActivePlayerId { get; set; }
        public PlayerState ActivePlayer { get; }
        public IReadOnlyCollection<PlayerState> Players { get; }
        public MatchState(PlayerState first, PlayerState second, int activePlayerId);
        public PlayerState GetPlayer(int playerId);         // 未知 Id 抛异常
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 默认构造法力池 | `Max=0 / Current=0`；负数构造抛 `ArgumentOutOfRangeException` |
| AC-2 | 法力足够时 `Spend(n)` | `Current` 减少 n，返回成功；恰好等于当前值时归 0 |
| AC-3 | 法力不足或费用为负 | 返回 `Result.Failure`（含错误码）且 `Current` 不变 |
| AC-4 | `BeginTurn(manaLimit)` | 上限 +1、当前回满；连续成长在 manaLimit 处封顶；上限未为正抛异常 |
| AC-5 | 构造英雄 | `Health` 默认等于 `MaxHealth`；可显式指定；空 Key / 非法生命抛异常 |
| AC-6 | 构造一局双人状态 | 两玩家按 Id 可取、`Phase=MatchStart`、`TurnNumber=1`、`ActivePlayer` 与 `ActivePlayerId` 一致 |
| AC-7 | 非法对局构造 | 座位 Id 重复、行动方 Id 不存在 → 抛异常；查询未知玩家 Id → 抛异常 |
| AC-8 | 纯 C# 约束 | 新类型位于 `Card.Domain`（`noEngineReferences: true`），无任何 Unity 依赖；无 Unity 工具链可编译并跑通 |

## 7. 测试要求

- 测试类型：EditMode（同时被无 Unity 的 `Tools/coverage.ps1` 工具链编译执行）
- 最少用例数：≥ 20
- 必须覆盖的边界：零水晶开局、费用恰好等于当前、费用超限、负数费用、法力成长封顶（0→…→10 不再增长）、上回合剩余法力不保留（回满）、英雄显式/默认生命、未知玩家查询、重复座位、行动方不存在

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M3-T1 状态与证据）；本任务卡回填结论
- 需更新的配置表：无
- 是否影响既有模块：否（纯新增目录与类型）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 自检与评审结论（2026-10-05，按 Docs/04 §7 六步）

### 10.1 需求对齐

| AC | 满足 | 证据 |
| --- | --- | --- |
| AC-1 零水晶 / 负数构造 | ✅ | `Ctor_Default_IsEmpty`、`Ctor_WhenNegative_Throws` |
| AC-2 法力扣减 / 恰好归零 | ✅ | `Spend_WhenAffordable_DeductsCurrent`、`Spend_WhenExactCost_DrainsToZero` |
| AC-3 不足 / 负数费用失败且不变 | ✅ | `Spend_WhenInsufficient_ReturnsFailureAndKeepsCurrent`（`ERROR_MANA_INSUFFICIENT`）、`Spend_WhenAmountNegative_ReturnsFailure`（`ERROR_AMOUNT_INVALID`） |
| AC-4 成长 / 回满 / 封顶 | ✅ | `BeginTurn_FromEmpty_GrowsMaxAndRefills`、`BeginTurn_DoesNotCarryLeftoverAndRefillsToNewMax`、`BeginTurn_Repeatedly_CapsAtManaLimit`、`BeginTurn_WhenManaLimitNotPositive_Throws` |
| AC-5 英雄构造 | ✅ | `HeroState_Ctor_HealthDefaultsToMax`、`HeroState_Ctor_WithExplicitHealth`、`HeroState_Ctor_WhenArgumentsInvalid_Throws` |
| AC-6 双人对局可构造 | ✅ | `MatchState_Ctor_IsPlayableWithTwoPlayers`、`MatchState_ActivePlayer_TracksActivePlayerId` |
| AC-7 非法构造 / 未知查询 | ✅ | `MatchState_GetPlayer_WhenIdUnknown_Throws`、`MatchState_Ctor_WhenPlayerIdsDuplicate_Throws`、`MatchState_Ctor_WhenActivePlayerMissing_Throws` |
| AC-8 纯 C# / 无 Unity 可跑 | ✅ | `check.ps1` PASS（121 文件、R1/R6）；`coverage.ps1` 工具链 447/447 |

### 10.2 铁律自查

13 条铁律逐条核对：1（无玩法继承，仅 sealed 数据类）、2（Domain 只引用 Core）、3（新文件仅 `using System.*` 与 `Card.Core`，R1 扫描零命中）、6（法力上限经 `BeginTurn(manaLimit)` 参数注入，不硬编码）、9（新增 24 例，先红 CS0234/CS0246 后绿）、10（最大文件 MatchState.cs 59 行；最长方法为构造/测试均 < 50 行；0 warning）、11（`Card.Domain` 保持 `noEngineReferences: true`；状态只存数据，配置引用一律 Key）——均未命中违规。4/5/7/8/12/13 不涉及（本任务无结算行为、无编辑器/单例代码、无网络代码）。

### 10.3 边界推演

| 场景 | 期望 / 实际 | 测试 |
| --- | --- | --- |
| 零水晶开局 | Spend 正数失败、0/0 | `Ctor_Default_IsEmpty` |
| 费用恰好等于当前 | 成功且归 0 | `Spend_WhenExactCost_DrainsToZero` |
| 连续 11 次成长 | 停在 manaLimit 10/10 | `BeginTurn_Repeatedly_CapsAtManaLimit` |
| 上回合剩余法力 | 不保留，回满至新上限 4/4 | `BeginTurn_DoesNotCarryLeftoverAndRefillsToNewMax` |
| 重复座位 / 行动方缺失 / 未知查询 | 一律抛异常 | 三个对应用例 |
| 英雄生命边界 | health=0 与 31（max+1）均拒 | `HeroState_Ctor_WhenArgumentsInvalid_Throws` |

### 10.4 测试证据

- **Unity 权威**（2022.3.54f1c1 批处理副本，无界面）：**467 total / 467 passed / 0 failed**（`%TEMP%\CardReborn_CI-results.xml`），编译段无任何 `warning CS` / `error CS`。
- **无 Unity 工具链**：**447 passed / 0 failed**（原 423 + 新增 24）。
- 覆盖率：`ManaPool` 100%、`MatchState` 100%、`PlayerState` 100%、`HeroState` 88%；汇总 **0_Core 96.51%**（1160/1202）、**Domain + App 92.65%**（1223/1320，门禁 90% / 80%）。
- GUID：新增 9 个 .meta（2 目录 + 7 文件），全仓 206 个 GUID 无重复。

### 10.5 影响面

纯新增：`1_Domain/Match/`（5 文件）、`7_Tests/EditMode/Match/`（2 文件）及对应 9 个 .meta；未改动任何既有文件，零回归风险。

### 10.6 反向审查

1. *"ManaPool 用引用类型会被别名共享"*——每个玩家在构造时注入独立实例；状态根本身要求经同一引用可见修改（权威状态语义），不成立。
2. *"构造不强制 Current ≤ Max"*——为 M3-T5 幸运币临时法力预留的有意设计，已在本卡 §4 登记；Spend 只扣减、不产生非法状态。
3. *"Health/Armor 用 public set 可被写坏"*——规则三层内部访问，任何业务修改仍受铁律 5（GameCommand + RuleEngine）约束；T9 序列化同样需要可写属性；结算行为明确推迟至 M4/T7。

### 10.7 评审结论

**通过**。无 P0/P1；门禁全绿（Unity 467/467、工具链 447/447、覆盖率双达标、静态门禁 PASS）；文档已同步。可提交。

