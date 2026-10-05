# 任务卡 · M3-T2 卡牌实例与分区

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T2 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.3（出牌：场位校验通过才结算）、FR-5.4（战场随从上限 7）、FR-5.8（手牌上限 10）、FR-5.7（死亡移入坟场） |
| 规则依据 | [01 §3.1](../01-开发需求文档.md)（手牌上限 10 / 战场上限 7）、[00 §4.2](../00-现状解构与架构再设计.md)（`CardDefinition` 不可变 + `CardInstance` 可变）、[03 §5.9](../03-开发规范文档.md)（状态只存数据、禁 `list[index]` 业务查询） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M3-T1（PlayerState/MatchState 已存在） |

## 2. 目标（一句话）

> 实现可变卡牌实例 `CardInstance` 与有序分区 `Zone`（牌库/手牌/战场/坟场），使卡牌在分区间的移动满足"来源区移除、目标区加入、容量不足则整体失败且状态不变"，并将四个分区接入 `PlayerState`。

## 3. 范围（做什么）

- `1_Domain/Match/ZoneType.cs`：枚举 `Deck / Hand / Board / Graveyard`。
- `1_Domain/Match/CardInstance.cs`（sealed，可变）：
  - 身份：`InstanceId`（局内唯一实例 Id，由 T5 分配器保证）、`CardKey`（指向 `CardDefinition`）、`OwnerId`。
  - 随从运行时数值：`Attack` / `MaxHealth` / `Health`（法术为 0）；`CurrentZone`（`ZoneType?`，未入区为 null）。
  - 工厂 `FromDefinition(CardDefinition, instanceId, ownerId)`：从配置契约复制初始值，配置与运行时状态分离。
- `1_Domain/Match/Zone.cs`（sealed）：
  - 有序卡牌列表（追加保序）；`Capacity`（null = 不限）、`Count`、`Cards`、`Contains`、`CanAdd`。
  - `Add`（满 → `ERROR_ZONE_FULL`；重复 → `ERROR_CARD_ALREADY_PRESENT`）、`Remove`（外来卡返回 false）。
  - `MoveIn(card, from)`：先校验（卡在来源区、目标非同源、容量），再"来源区移除 → 目标区加入"；任何校验失败状态不变。
- 接入 `PlayerState`：新增 `Deck / Hand / Board / Graveyard`（手牌/战场容量取自 `RulesConfig.HandLimit/BoardLimit`；牌库/坟场不限），构造增加可选 `RulesConfig` 参数（缺省用配置层默认值，T5 注入实际配置）。
- EditMode 测试：实例工厂、加入/移除、四个区的移区、满手/满场拦截与原子性、来源区不存在卡、同区移动、容量配置来源。

## 4. 明确不做（防止范围蔓延）

- **不做** `KeywordSet` / `StatusSet`（召唤失调/冻结/圣盾）：M3-T3；随从是否能攻击不在本任务。
- **不做** 开局初始化（牌库加载/洗牌/抽起手）：M3-T5；本任务只手工造卡与移区。
- **不做** 出牌/攻击合法性校验：M3-T6。
- **不做** 伤害/治疗结算与死亡检测：M4/T7；`Health` 仅为可读写数据。
- **不做** 爆牌销毁目标区（burn 不入 Hand）：M3-T8；本任务只保证"满则 Add/Move 失败"。
- **不做** 跨玩家移动（精神控制类）：无对应 P0 需求。
- **不做** 区内插入排序/换位：M4 需要时再补（当前只追加）。
- **不做** "除外/SetAside"区：首版无此需求。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public enum ZoneType { Deck = 0, Hand = 1, Board = 2, Graveyard = 3 }

    public sealed class CardInstance
    {
        public int InstanceId { get; }
        public string CardKey { get; }
        public int OwnerId { get; }
        public int Attack { get; set; }
        public int MaxHealth { get; }
        public int Health { get; set; }
        public ZoneType? CurrentZone { get; internal set; }
        public static CardInstance FromDefinition(Card.Domain.Config.CardDefinition definition,
                                                  int instanceId, int ownerId);
    }

    public sealed class Zone
    {
        public ZoneType Type { get; }
        public int? Capacity { get; }
        public int Count { get; }
        public IReadOnlyList<CardInstance> Cards { get; }
        public Zone(ZoneType type, int? capacity);
        public bool Contains(CardInstance card);
        public bool CanAdd();
        public Card.Core.Result Add(CardInstance card);
        public bool Remove(CardInstance card);
        public Card.Core.Result MoveIn(CardInstance card, Zone from);   // 原子
    }
}
```

PlayerState 新增：`Zone Deck / Hand / Board / Graveyard`（构造追加 `RulesConfig? rules = null`）。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 从随从定义造实例 | CardKey/Attack/MaxHealth/Health/OwnerId 与定义一致；`CurrentZone` 为 null |
| AC-2 | 从法术定义造实例 | 可构造，Attack/Health 为 0 |
| AC-3 | Add 入区 | Count+1、Contains 为真、卡的 CurrentZone 变为该区；顺序为追加 |
| AC-4 | Add 重复卡 / 满区 | 返回失败（`ERROR_CARD_ALREADY_PRESENT` / `ERROR_ZONE_FULL`）且 Count 不变 |
| AC-5 | Remove | 卡移除、CurrentZone 归 null；移外来卡返回 false 且不变 |
| AC-6 | Deck → Hand 移动 | 来源区 Count-1 且不含卡、目标区含卡、CurrentZone=Hand |
| AC-7 | 目标区已满时移动 | 返回 `ERROR_ZONE_FULL`，卡仍在来源区（原子性） |
| AC-8 | 卡不在来源区 / 同区移动 | 返回失败且两区均不变 |
| AC-9 | PlayerState 四分区 | 类型与容量正确：Hand=HandLimit、Board=BoardLimit（来自 RulesConfig），Deck/Graveyard 不限 |
| AC-10 | 纯 C# | 无 Unity 依赖；无 Unity 工具链编译并跑通 |

## 7. 测试要求

- 测试类型：EditMode（同时被 `Tools/coverage.ps1` 执行）
- 最少用例数：≥ 20
- 必须覆盖的边界：空区、恰好满员、超容量（第 8 个随从 / 第 11 张手牌）、空来源移区、同区移动、重复加入、外来卡 Remove、不限容量区可超 10/7、移动原子性

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；本任务卡回填结论
- 需更新的配置表：无
- 是否影响既有模块：`PlayerState` 新增分区属性（向后兼容，3 参构造保留）

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
| AC-1 随从实例复制 | ✅ | `FromDefinition_Minion_CopiesIdentityAndStats` |
| AC-2 法术实例零战斗值 | ✅ | `FromDefinition_Spell_HasZeroCombatStats` |
| AC-3 Add 追加 + CurrentZone | ✅ | `Add_AppendsCardAndMarksCurrentZone` |
| AC-4 重复 / 满区失败 | ✅ | `Add_WhenSameCardRepeated_ReturnsFailure`、`Add_WhenAtCapacity_ReturnsZoneFull` |
| AC-5 Remove / 外来卡 | ✅ | `Remove_TakesCardOutAndClearsCurrentZone`、`Remove_WhenCardForeign_ReturnsFalse` |
| AC-6 跨区移动 | ✅ | `MoveIn_BetweenZones_RemovesFromSourceAndAddsToTarget` |
| AC-7 满目标原子失败 | ✅ | `MoveIn_WhenTargetFull_FailsAndKeepsCardInSource` |
| AC-8 不在来源 / 同区 | ✅ | `MoveIn_WhenCardNotInSource_Fails`、`MoveIn_WhenSourceAndTargetSame_Fails` |
| AC-9 四分区与配置容量 | ✅ | `PlayerState_Zones_CapacitiesComeFromRulesConfig`、`PlayerState_WithoutRules_UsesConfigDefaults` |
| AC-10 纯 C# | ✅ | `check.ps1` PASS（127 文件）；工具链 467/467 |

### 10.2 铁律自查

1（仅 sealed 类、无玩法继承）、2/3（新文件仅 `using System.*`/`Card.Core`/`Card.Domain.Config`，R1 零命中）、6（Hand/Board 容量一律取自 `RulesConfig`，Zone 不内置 10/7）、9（新增 20 例，先红 CS0246/CS1729 后绿）、10（最大文件 Zone.cs 115 行；方法均 < 50 行；0 warning）、11（`Card.Domain` 保持 `noEngineReferences`；卡牌只以 Key 引配置、分区只存 Id 引用）——均未命中。4/5/7/8/12/13 不涉及。

### 10.3 边界推演

| 场景 | 期望 / 实际 | 测试 |
| --- | --- | --- |
| 空区 | Count 0、CanAdd true | `Ctor_IsEmptyAndExposesTypeAndCapacity` |
| 恰好满员 + 第 3 张 | 前 2 成功、第 3 张 `ERROR_ZONE_FULL` | `Add_WhenAtCapacity_ReturnsZoneFull` |
| 满目标移动 | 失败且卡留来源区 | `MoveIn_WhenTargetFull_FailsAndKeepsCardInSource` |
| 空来源移区 | `ERROR_CARD_NOT_IN_SOURCE` | `MoveIn_WhenCardNotInSource_Fails` |
| 同区移动 | `ERROR_SAME_ZONE` | `MoveIn_WhenSourceAndTargetSame_Fails` |
| 负容量 / 空参数 | 抛 BCL 异常 | `Ctor_WhenCapacityNegative_Throws`、`MoveIn_WhenArgumentsNull_Throws` |
| 不限容量区超 12 | 全部成功 | `UnlimitedZone_AcceptsMoreThanStandardLimits` |

### 10.4 测试证据

- **Unity 权威**（批处理副本）：**487 total / 487 passed / 0 failed**（上轮 467 + 新增 20）；最终编译 0 error / 0 warning。
  - 日志在首次导入前曾有一过性 CS0246（新脚本缺 .meta 时的首遍编译，见 §10.6），导入后重编译干净；新测试全部被执行即最终 DLL 含新代码的直接证据。
- **无 Unity 工具链**：**467 passed / 0 failed**（447 + 20）。
- 覆盖率：CardInstance **100%**、PlayerState **100%**、Zone **97%**（60 行中 58）；汇总 **0_Core 96.51%**（1160/1202）、**Domain + App 93.04%**（1324/1423）。
- 新增 6 个 .meta，全仓 212 GUID 无重复。

### 10.5 影响面

- 新增：`ZoneType.cs`、`CardInstance.cs`、`Zone.cs` + 3 测试文件（含助手）与 6 个 .meta。
- 修改：`PlayerState.cs`（新增 4 分区属性与可选 RulesConfig 参数；3 参构造签名保留，T1 测试与既有调用零改动）。无回归。

### 10.6 反向审查

1. *"日志里有 CS0246，编译其实失败了"*——时序核实：错误发生在新脚本导入前的第一遍编译（log 280→611）；随后 ReloadAssembly、ZoneType/Zone 导入并重编译（log 710/714/726），此后零错误；487 个测试（含 20 个新测试）被执行，失败的编译不可能产出含新测试的结果。结论：最终编译干净。.meta 已入库，后续同步不会再出现该瞬态。
2. *"Add 不阻止同一张卡进两个不同 Zone"*——`MoveIn` 先校验目标是否已含卡；直接 Add 跨区是显式的构造期操作（T5 唯一入口），且卡的 CurrentZone 始终反映最后加入的区，状态可观测；不额外加全局注册表（避免过度设计），如 T5/T6 发现滥用再补。
3. *"CardInstance.Health 可被任意写坏"*——同 T1 结论：业务修改受铁律 5 约束；伤害/死亡结算在 M4/T7；序列化（T9）需要可写属性。

### 10.7 评审结论

**通过**。无 P0/P1；Unity 487/487、工具链 467/467、覆盖率双达标且新类型近全覆盖、静态门禁 PASS。可提交。
