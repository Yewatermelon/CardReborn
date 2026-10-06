# 任务卡 · M3-T5 开局初始化

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T5 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.1（起手/先后手）、FR-4.6（种子可复现）、FR-13.2/13.3（无 Unity 可运行） |
| 规则依据 | Docs/01 §3.1 对局主流程（行 66–67）、§7.2 规则表（卡组 30 张；先手 3 / 后手 4 + 幸运币；先手第 1 回合少抽 1 张） |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T4（状态骨架、分区、CardInstance、命令模型） |

## 2. 目标（一句话）

> 提供应用层 `MatchFactory`：从 `CardDatabase` 与双方卡组请求出发，完成牌库构建、确定性洗牌、掷先后手、抽起手与幸运币发放，产出先手玩家可立即行动（法力 1、首回合不抽牌）的 `MatchState`；同一种子全程可复现。

## 3. 范围（做什么）

1. 新增 `MatchSetupRequest`：座位的英雄 key + 30 张卡 key 列表。
2. 新增 `MatchFactory.Create(CardDatabase, seat0, seat1, IRandomProvider)`：
   - 校验：请求非空；卡组张数 = `Rules.DeckSize`；英雄 key 存在；每张卡 key 存在。
   - 为座位 0/1 建 `PlayerState`（HeroState/HeroPowerKey/ManaPool/四分区），由卡组创建 30 个 `CardInstance`，`IRandomProvider.Shuffle` 后入牌库。
   - `NextInt(0,2)` 掷先手；先手抽 `StartingHandFirst` 张、后手抽 `StartingHandSecond` 张（从牌库顶=洗后列表末位抽，走 `Zone.MoveIn`）。
   - 后手手牌直接加入 1 张幸运币实例（`Rules.TheCoinCardKey`；非牌库移动）。
   - 先手 `Mana.BeginTurn(ManaLimit)`（0→1）；状态 `Phase=Main`、`TurnNumber=1`、`ActivePlayerId=先手`。
3. Rules 表扩展（4 字段，见 §8）：`DeckSize` / `StartingHandFirst` / `StartingHandSecond` / `TheCoinCardKey`。
4. Cards 表新增幸运币定义（Id=38，0 费法术，`GainManaEffect:1`，`Enabled=FALSE`：不进卡池/抽卡/收藏，但 `CardDatabase.RequireCard` 可按 key 取）。
5. 生成物由无 Unity 工具链重新导入（ConfigFileImporter 为纯 BCL；见 §5.2）。

## 4. 明确不做（防止范围蔓延）

- 不做换牌/调度（Mulligan）：Docs/01 标注 P1 阶段，M3 不做。
- 不做回合状态机（TurnStart/TurnEnd 流转、后续回合抽牌）：M3-T6 / M4；本任务只把先手推进到首回合 Main。
- 不做幸运币效果结算（GainMana 的实际生效）：M4 效果系统；本任务只产出配置与实例。
- 不做疲劳、胜负判定：T7/T8。
- 不做"同名卡 ≤ 2 张"构筑校验：属卡组编辑器（后续阶段）；本任务只校验张数与 key 存在性。
- 不做关卡/AI 卡组装配、不做 UI、不写网络代码。

## 5. 接口约定

### 5.1 类型签名

```csharp
namespace Card.Application.Match
{
    public sealed class MatchSetupRequest
    {
        public MatchSetupRequest(string heroKey, IReadOnlyList<string> deckCardKeys);
        public string HeroKey { get; }
        public IReadOnlyList<string> DeckCardKeys { get; }
    }

    public static class MatchFactory
    {
        public static MatchState Create(
            CardDatabase database,
            MatchSetupRequest seat0,
            MatchSetupRequest seat1,
            IRandomProvider random);
    }
}
```

- 座位 Id 固定 0/1（与 PlayerState 约定一致）；InstanceId 由工厂内递增计数器从 0 分配，全场 61 张唯一。
- 随机调用顺序固定：**先双方洗牌（座0→座1），再掷先手**——保证牌序不受先后手结果影响，"同种子牌序一致"成立。
- 契约/配置非法（null、张数不符、未知 key）直接抛 `ArgumentException`/`ConfigLookupException`：属调用契约错误；用户侧"不崩溃"由上层入口先校验（NFR-7），本任务不吞异常。

### 5.2 Rules 新字段解析策略（向后兼容）

- CSV：ConfigRowParser 新增"缺列/空值回落默认"的可选读取；旧 fixture（无新列）不破坏。仓库 Rules.csv 写实际值。
- JSON：ConfigJsonReader 新增 OptionalInt/OptionalString（TryGetMember + 默认）；生成物同步更新；`schemaVersion` 保持 1（纯追加字段，任务卡登记，不做迁移 M2-R3 不变）。

### 5.3 幸运币 CSV 行

```
38,NEUTRAL_THE_COIN,CARD_038_NAME,CARD_038_DESC,0,Spell,Common,Neutral,0,0,,None,GainManaEffect:1,Core,art_spell_coin,sfx_cast_coin,FALSE
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 开局后先手手牌 | 恰好 3 张，牌库剩 27 |
| AC-2 | 开局后后手手牌 | 5 张（4 普通 + 1 幸运币），牌库剩 26 |
| AC-3 | 先手首回合不抽牌 | ActivePlayer.Hand.Count = 3；无多抽 |
| AC-4 | 幸运币 | 后手手牌恰 1 张 CardKey=NEUTRAL_THE_COIN，CurrentZone=Hand |
| AC-5 | 同种子可复现 | 同种子两次 Create：双方牌库 CardKey 序列逐项一致、ActivePlayerId 一致 |
| AC-6 | 洗牌真实生效 | 种子 1..20 中至少一个使牌序不同于卡组原序 |
| AC-7 | 先后手随机 | 100 个种子下两个座位都曾成为先手 |
| AC-8 | 首回合状态 | Phase=Main、TurnNumber=1；先手法力 1/1，后手 0/0 |
| AC-9 | 身份与归属 | 所有实例 InstanceId 全场唯一；OwnerId 与所在分区正确；英雄 HeroPowerKey 已接入 |
| AC-10 | 非法输入 | 卡组 29 张、未知卡 key、未知英雄 key、null 参数 → 抛异常 |
| AC-11 | 无 Unity 可运行 | coverage.ps1 通过；check.ps1 PASS |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/MatchFactoryTests.cs`），无 Unity 工具链同样执行。
- 最少用例数：14。
- 数据构造：复用 internal `ConfigTemplates`（从仓库 CSV 加载 → ConfigValidator.Validate → CardDatabase）；卡组用真实 key ×30；英雄用 HERO_MAGE / HERO_WARRIOR。
- 必须覆盖的边界：见 AC-1～AC-10（含异常路径与随机分布）。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡；Docs/01 §7.2 Rules 表字段说明（追加 4 列描述）。
- 需更新的配置表：`Config/Excel/Rules.csv`（+4 列）、`Config/Excel/Cards.csv`（+幸运币行）；生成物 `Assets/_Project/Config/*.json`（rules.json、cards.json）。
- 生产代码改动：RulesConfig、ConfigRowParser、ConfigJsonWriter、ConfigJsonReader、RuleConfigValidator、ConfigValidator（幸运币外键存在性）；新增 Application/Match 两文件。
- 是否影响既有模块：配置层均为追加式扩展（可选读取+默认值），既有行为不变。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见 §11.1）
- [x] 测试通过且覆盖边界（14 用例，见 §11.4）
- [x] 编译 0 error / 0 warning（kernel 工程 Nullable enable；check.ps1 PASS）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见 §11.2）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见 §12）
- [x] 涉及文档已同步更新（PROGRESS、Docs/01 §7.2、本任务卡）

## 10. 待确认问题与假设

1. 幸运币以 `Enabled=FALSE` 存在于 Cards 表：假设该标记只表示"不进启用卡池/抽卡/收藏"，系统发放不受其限（RequireCard 全量可查）——已核实 CardDatabase 实现支持。
2. MatchFactory 产出即首回合 Main（含先手法力 1）：依据 Docs/01 主流程"开局→回合开始（法力+1、抽1，先手除外）"，把首回合开始序列视为开局的一部分，使 AC-3 可在本任务验证。
3. Rules 追加字段不提升 schemaVersion：纯可选追加，读旧生成物回落默认；与 M2-R3（自动迁移）不冲突。
4. TheCoinCardKey 语义在实现中修正为"列缺失 → null → 跳过幸运币外键检查"（而非回落默认字符串）：隔离 fixture 的 4 列 Rules 天然兼容；仓库真实 Rules.csv 显式配置 NEUTRAL_THE_COIN，真实链路仍强制非空与外键；MatchFactory 在 key 为 null 时以 ArgumentException 显式失败，不静默读错。

## 11. 自检证据（Docs/04 §7.1 六步）

### 11.1 需求对齐（AC → 测试，均为 MatchFactoryTests）

| AC | 满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 | 是 | Create_FirstPlayerKeepsThreeCards_DeckHas27 |
| AC-2/AC-4 | 是 | Create_SecondPlayerHasFourCardsAndCoin（断言幸运币 CardKey/CurrentZone=Hand） |
| AC-3 | 是 | Create_FirstTurnSkipsDraw_ForManySeeds（种子 0..49 先手恒 3 张） |
| AC-4 唯一性 | 是 | Create_GivesExactlyOneCoinToSecondPlayerOnly |
| AC-5 | 是 | Create_SameSeedProducesIdenticalDeckOrderAndFirstSeat（双方逐项序列一致） |
| AC-6 | 是 | Create_ShuffleActuallyReordersDeck（种子 1..20 存在重排） |
| AC-7 | 是 | Create_FirstSeatIsRandomizedAcrossSeeds（100 种子两座位均先手过） |
| AC-8 | 是 | Create_FirstTurnState_MainTurnOne_ActiveHasOneMana（先手 1/1、后手 0/0） |
| AC-9 | 是 | Create_InstanceIdsUnique_61Cards_WithOwnershipAndZone + Create_HeroPowerKeyWiredFromHeroDefinition |
| AC-10 | 是 | Create_DeckWithWrongSize_Throws / Create_UnknownCardKey_Throws / Create_UnknownHeroKey_Throws / Create_NullArguments_Throw |
| AC-11 | 是 | coverage.ps1 PASS、check.ps1 PASS（见 §11.4） |

### 11.2 铁律扫描（13 条）

- 未命中：无卡牌玩法继承（仅 DTO/工厂）；依赖方向不变（Application→Domain→Core，均为 BCL）；无 `UnityEngine`/`Debug.Log`/`Time`/`UnityEngine.Random`（随机走 IRandomProvider）；数值全部来自配置（30/3/4/coinKey 读 RulesConfig）；无单例/Find/ServiceLocator；状态只存数据（InstanceId/OwnerId/ZoneKey）；所有非法输入显式抛 ArgumentException/ConfigLookupException，无 try/catch 吞异常；单文件 ≤300（ConfigJsonReader 超行后已 partial 拆分，主文件约 238 行）。
- 铁律 5（一切走 GameCommand）：本任务为开局工厂（创建初始状态，不发生对局操作），不产生 GameCommand，不适用；后续对局操作在 T6 起遵守。
- 铁律 9（新功能带测试）：14 个新测试先行（先红后绿见 §11.4）。

### 11.3 边界推演

| 场景 | 期望 | 实际 | 测试 |
| --- | --- | --- | --- |
| 卡组 29/31 张 | 抛 ArgumentException | 抛出 | Create_DeckWithWrongSize_Throws |
| 未知卡/英雄 key | 抛 ConfigLookupException/ArgumentException | 抛出 | Create_Unknown*_Throws |
| null 请求/列表 | 抛 ArgumentNullException/ArgumentException | 抛出 | Create_NullArguments_Throw |
| 起手抽牌不越界（规则配置 0 张时） | 抽 0 次，Deck 不被移动 | 循环自然为 0 次 | 配置校验 StartingHand 范围 0..HandLimit（RuleConfigValidator 测试覆盖） |
| 重复随机调用/跨种子 | 牌序只由种子决定、座间不串号 | 固定调用序 + 分段 InstanceId | AC-5/AC-9 测试 |
| 幸运币 key 未配置 | 工厂显式失败、校验跳过外键 | Guard.Require 抛异常 / 校验跳过 | AddCoin 路径；ConfigValidator 隔离测试 |

### 11.4 测试证据（先红后绿）

- 红：MatchFactory 与 MatchSetupRequest 未创建时，coverage 编译失败 CS0234/CS0246（namespace/类型不存在），符合"先写失败测试"。
- 绿：`Tools/coverage.ps1` → **525 用例全通过**（T4 基线 511 + 本任务 14），0 failed。
- 覆盖率：0_Core **96.51%**（1160/1202，门禁 90%）；Domain+App **92.84%**（1622/1747，门禁 80%，NFR-4）；MatchFactory/MatchSetupRequest 均 100%。
- 静态门禁：`Tools/check.ps1` → **PASS**，扫描 142 个 C# 文件、10 个 asmdef，0 违规。
- 警告：kernel 工程（Nullable enable，真实门禁）0 warning；0 warning 以它为准（Unity Editor 权威验证受 M3-B1 沙箱阻塞，暂缓）。

### 11.5 影响面

- 配置链路（Rules/Cards 解析、读写、校验）均为追加式：旧 CSV/JSON 缺列时回落，既有行为不变；生成物 rules.json/cards.json 已用无 Unity 导入器重新生成。
- 受影响既有测试：ConfigTemplateTests（表头 8 列断言）、ConfigValidatorTableTests（幸运币外键）；均随本任务一并更新并通过，无回归。
- 文档同步：Docs/01 §7.2 Rules 字段、PROGRESS、本任务卡。

### 11.6 反向审查（挑刺 3 条）

1. "Enabled=FALSE 的幸运币会不会被校验器放过非法效果串？"——禁用卡只解析不做语义校验是既有设计；幸运币效果结算在 M4，届时以真实施放路径再校验，本任务不产生玩法风险。
2. "洗牌是否真随机而非恒等？"——Create_ShuffleActuallyReordersDeck 与 100 种子先后手分布两个测试共同保证，且随机序经 IRandomProvider 注入、种子可复现。
3. "末位抽牌约定是否脆弱？"——洗后列表末位=牌库顶为工厂与 Zone 的内部一致约定，抽牌走 `MoveIn` 保持归属/分区不变量；上层只通过 Zone API 访问，不依赖列表位置。

## 12. 评审结论（Docs/04 §8）

- 结论：**通过**。门禁全绿（测试 525/525；覆盖率双达标；规范扫描 PASS；asmdef 依赖方向 check.ps1 校验）；文档已同步。
- 问题清单：无 P0、无 P1。
- 遗留（非本任务质量问题）：M3-B1（P2）Unity 批处理受 TRAE 沙箱（bee/upm 拦截）无法权威运行，继续以无 Unity 工具链 + 手写 .meta（5 个新 meta、GUID 经全仓 228 个 meta 去重）为证，待环境恢复后由 Unity 补跑确认。
