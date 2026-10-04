# 任务卡 · M2-T5 `CardDatabase`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T5 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.4（运行时 id/key 双索引查找，查不到给明确错误而非 null 崩溃）、FR-1.5（按系列/稀有度/职业筛选）、FR-1.6（生成物带 schemaVersion） |
| 规则依据 | [01 §7.3](../01-开发需求文档.md)（禁 `list[index]` 作为业务查询）、[03 §9.1](../03-开发规范文档.md)（生成物读取与版本）、[03 §5.9.4](../03-开发规范文档.md)（序列化实现放 BCL 层） |
| 结构说明 | 按 [00 §4.1](../00-现状解构与架构再设计.md) `CardDatabase` 属 L1；本任务把它拆成**纯查询（Domain：`CardDatabase`）**与**读盘（Infrastructure：`ConfigFileLoader`）**两部分。理由：查询逻辑无 I/O、无 Unity，放 Domain 后可被内核测试与 M11 的服务端直接复用；读盘留在 Infrastructure。已在 00 文档补注。 |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M2-T1…T4、M2-T3 会话 B（生成物已入库） |

## 2. 目标（一句话）

> 打通"生成物 → 契约 → 卡池查询"的运行时链路：id/key O(1) 查询、按职业/稀有度/系列筛选，查不到时给明确错误；并用"写出 → 读回 → 再写出逐字符一致"的闭环把生成物正确性钉死。

## 3. 范围（做什么）

- `Core/JsonParser` + `JsonValue.Parse`：极简 JSON **解析**（M2-T3 只做了写出）。严格：多余内容、未闭合、未知字面量、小数、重复键、尾随逗号都报错并给位置；容 BOM；中文不转义。
- `Domain/Config/ConfigJsonReader`：生成物 JSON → 契约（camelCase 字段、枚举写名字、关键词 `Taunt|Charge`）；失败抛 `ConfigReadException`（含**文件名 + JSON 路径**如 `cards[2].cost`）。
  - schema 版本不匹配时**直接失败**（不做自动迁移，登记 M2-R3），避免把不同版本字段静默读错。
- `Domain/Config/CardDatabase`：`TryGetCard(id/key)`、`RequireCard(id/key)`（缺失抛 `ConfigLookupException`，消息含标识）、`FilterCards(cardClass, rarity, setKey, includeDisabled)`、`RequireHero` / `RequireHeroPower` / `GetRarityWeight`、`Gacha` / `Rules`、计数属性；构造时发现重复 id/key 立即抛错。
- `Infrastructure/Config/ConfigFileLoader` + `ConfigLoadResult`：读 6 份生成物 → 契约 → `ConfigBundle`；缺文件/JSON 损坏/字段缺失汇成错误列表返回（不抛异常穿透）。
- ELK 测试：解析器边界、读取器逐字段与失败路径、卡池查询与筛选、**导入 → 读回 → 查询**的端到端闭环、以及直接读仓库里已入库的生成物。

## 4. 明确不做（防止范围蔓延）

- **不做** 热加载与重载菜单：M2-T6。
- **不做** 首版内容（≥30 卡）：M2-T7。
- **不做** 生成物的自动版本迁移：目前版本不匹配即失败（M2-R3）。
- **不做** 卡池之外的数据服务（如本地化表、关卡表）：对应表尚未定义。
- **不做** JSON 浮点支持：配置数值目前都是整数，遇到小数直接报错（避免精度/显示歧义）。

## 5. 接口约定

```csharp
namespace Card.Core
{
    public sealed class JsonValue
    {
        public static JsonValue Parse(string text);   // 新增：解析方向
    }
}

namespace Card.Domain.Config
{
    public sealed class ConfigReadException : System.Exception   // 含 SourceName / JsonPath / Reason
    public sealed class ConfigLookupException : System.Exception

    public static class ConfigJsonReader
    {
        public static IReadOnlyList<CardDefinition> ReadCards(JsonValue document, string source);
        public static IReadOnlyList<HeroDefinition> ReadHeroes(JsonValue document, string source);
        public static IReadOnlyList<HeroPowerDefinition> ReadHeroPowers(JsonValue document, string source);
        public static IReadOnlyList<RarityWeight> ReadRarityWeights(JsonValue document, string source);
        public static GachaConfig ReadGacha(JsonValue document, string source);
        public static RulesConfig ReadRules(JsonValue document, string source);
        public static ConfigBundle ReadBundle(JsonValue cards, JsonValue heroes, JsonValue heroPowers,
                                              JsonValue rarityWeights, JsonValue gacha, JsonValue rules);
    }

    public sealed class CardDatabase
    {
        public CardDatabase(ConfigBundle bundle);
        public bool TryGetCard(int id, out CardDefinition? card);
        public bool TryGetCard(string key, out CardDefinition? card);
        public CardDefinition RequireCard(int id);
        public CardDefinition RequireCard(string key);
        public IReadOnlyList<CardDefinition> FilterCards(CardClass? cardClass = null, CardRarity? rarity = null,
                                                         string? setKey = null, bool includeDisabled = false);
        public HeroDefinition RequireHero(string key);
        public HeroPowerDefinition RequireHeroPower(string key);
        public RarityWeight GetRarityWeight(CardRarity rarity);
    }
}

namespace Card.Infrastructure.Config
{
    public sealed class ConfigLoadResult { bool Succeeded; ConfigBundle? Bundle; IReadOnlyList<string> Errors; string ToText(); }
    public static class ConfigFileLoader { public static ConfigLoadResult Load(string directory); }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 解析标量/对象/数组 | 结构正确；空白与 BOM 容忍 |
| AC-2 | 解析转义与中文 | `\"`/`\\`/`\n`/`\t`/`\uXXXX` 正确还原；中文原样 |
| AC-3 | 解析异常输入 | 多余内容 / 未闭合 / 未知字面量 / 小数 / 重复键 / 尾随逗号 → 抛 `FormatException`（含位置或原因） |
| AC-4 | 写出 → 解析 → 再写出 | 逐字符一致 |
| AC-5 | 读取卡片文档 | 每个字段正确映射（含关键词 flags、效果列表、布尔） |
| AC-6 | 读取失败 | 版本不匹配、字段缺失、类型不符、枚举非法、关键词非法 → `ConfigReadException` 且含**文件名 + JSON 路径** |
| AC-7 | `TryGetCard` | 命中返回同一实例；未命中返回 false 且 out 为 null |
| AC-8 | `RequireCard` 未命中 | 抛 `ConfigLookupException`，消息含 id/key |
| AC-9 | 筛选 | 按职业、稀有度、系列及组合筛选正确；默认排除 `Enabled = FALSE`，`includeDisabled` 可包含 |
| AC-10 | 重复 id/key | 建库时抛 `ConfigLookupException`（手改生成物立刻暴露） |
| AC-11 | 读取生成物目录 | 6 份文件齐备 → `ConfigBundle`；缺文件/损坏/字段缺失 → 错误列表（不抛） |
| AC-12 | 端到端闭环 | 导入器写出 → 加载器读回 → 建库查询，字段与源表一致 |
| AC-13 | 已入库生成物 | 直接加载仓库 `Assets/_Project/Config` 成功 |
| AC-14 | 解耦与覆盖率 | `check.ps1` PASS；`Tools/coverage.ps1` PASS（`0_Core` ≥ 90%、`Domain + App` ≥ 80%） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）+ 无 Unity 工具链（Domain/Core 部分）
- 最少用例数：40（实际：解析器 18 + 读取器 16 + 卡池 14 + 加载器 9 = 57）
- 必须覆盖的边界：BOM、空集合、重复键、小数、未闭合、版本不匹配、字段缺失、类型不符、未命中查询、禁用卡、重复 id/key

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；`Docs/00`（CardDatabase 拆分说明）
- 需更新的配置表：无
- 是否影响既有模块：`JsonValue` 增加 `Parse`（写出行为不变）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10 节）
- [x] 测试通过且覆盖边界（新增 57 例，累计 427 passed / 0 failed）
- [x] 编译 0 error / 0 warning（Unity 清缓存干净重编译）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS / Docs/00 结构说明）

---

## 10. 证据与评审结论（2026-10-04）

### 10.1 AC 证据

| AC | 证据（测试名） |
| --- | --- |
- AC-1…AC-4 | `JsonParserTests` 18 例（标量 / 空白 / 嵌套 / 转义 / 中文 / 空集合 / BOM / 异常输入 5 类 / 往返稳定） |
| AC-5 | `ConfigJsonReaderTests.ReadCards_WhenValid_MapsEveryField`、`ReadCards_WhenKeywordsPresent_ParsesFlags` |
| AC-6 | `ReadCards_WhenSchemaVersionDiffers_Throws`、`WhenFieldMissing_ReportsJsonPath`、`WhenFieldHasWrongType_ReportsType`、`WhenEnumIsUnknown_Throws`、`WhenKeywordIsUnknown_Throws` |
| AC-7/AC-8 | `CardDatabaseTests.TryGetCard_*`、`RequireCard_WhenMissing_ThrowsWithIdentifier` |
| AC-9 | `FilterCards_WhenNoCriteria_ReturnsAllEnabled`、`ByClassRarityAndSetKey_CombinesCriteria`、`WhenIncludeDisabled_ReturnsParkedCardsToo` |
| AC-10 | `Ctor_WhenDuplicateCardIdOrKey_Throws` |
| AC-11 | `ConfigFileLoaderTests.Load_WhenFilesAreMissing_ReportsEachOne`、`WhenJsonIsCorrupt_ReportsParseError`、`WhenSchemaVersionDiffers_ReportsMismatch`、`WhenFieldIsMissing_ReportsJsonPath` |
| AC-12 | `Load_WhenReadingImporterOutput_ReturnsSameContent`、`Load_ThenBuildDatabase_QueriesWork` |
| AC-13 | `Load_WhenCommittedArtifactsUsed_Succeeds` |
| AC-14 | `check.ps1` PASS；工具链 `0_Core 96.51%`、`Domain + App 93.05%` |

### 10.2 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity 清缓存重编译 | `errors: 0`、`warnings: 0`、`result=Passed total=427 passed=427 failed=0` |
| 无 Unity 工具链 | PASS：`0_Core 96.51%`、`Domain + App 93.05%`（门禁 90% / 80%） |
| 静态门禁 | `Tools/check.ps1` PASS |

> 覆盖率插曲：补读取器时我把它的单测并进了 Infrastructure（被工具链排除），导致 Domain 覆盖率一度掉到 **76.32%**（门禁 80%）被拦下；补上 `ConfigJsonReaderTests`（Domain/Core 侧）后回到 93.05%。这条正说明"覆盖率门禁在防什么"。

### 10.3 评审结论

**通过**：无 P0/P1；门禁全绿；文档已同步。

**登记**：M2-R3（生成物 schema 自动迁移）——目前版本不匹配直接失败，等真有跨版本升级需求再做。
