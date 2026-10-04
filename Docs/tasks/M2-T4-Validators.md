# 任务卡 · M2-T4 实现校验器

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T4 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.2（非法数据导入**必须失败**并给出明确错误行号，不产生半成品数据）、FR-1.5（按 series/rarity/class 筛选，依赖稀有度与职业取值合法） |
| 规则依据 | [01 §7.3](../01-开发需求文档.md)（表头/枚举一致/禁数字/禁行序依赖/范围校验写在工具里）、[03 §9.1](../03-开发规范文档.md)（配置加载失败必须有明确错误：文件、行、字段）、[03 §11.2](../03-开发规范文档.md)（边界处降级而不是崩溃） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M2-T1（契约）、M2-T2（源表）、M2-T3 会话 A（行解析与 JSON 写出） |

## 2. 目标（一句话）

> 把"哪些配置是非法"从散落在代码里的判断收敛成一个可测试的校验器：**一次列全部问题**（表名/行号/列名/原因），只要有一条就不产出生成物。

## 3. 范围（做什么）

- `ConfigSourceSet`：一次导入涉及的 6 张源表（缺表即错误，而不是 null 崩溃）。
- `ConfigValidationIssue` / `ConfigValidationReport`：问题条目（表名/行号/列名/原因）+ 报告（`HasErrors`、`Count`、`ToText()` 供日志与对话框直接展示）。
- `ConfigBundle`：校验通过时给出的**已解析契约集合**（避免导入器二次解析）。
- `ConfigValidator.Validate(ConfigSourceSet) → ConfigValidationReport`，规则集：
  1. **表缺失**；
  2. **行解析错误**（复用 M2-T3 的 `ConfigRowParser`，逐行捕获 `ConfigFormatException`，继续检查其余行）；
  3. **唯一性**：`Id` / `Key` 在同一张表内不得重复；
  4. **外键**：`Heroes.HeroPowerKey` 必须存在于 `HeroPowers`；
  5. **跨表**：`Cards.Cost ≤ Rules.ManaLimit`（规则表可解析时）；
  6. **范围**：卡牌 `Cost ≥ 0`；随从 `Attack ≥ 0` 且 `Health ≥ 1`；非随从不得有攻击/生命；英雄 `Health` 1..100；`Rules` 的 `HandLimit ≤ 10`、`BoardLimit ≤ 7`、`ManaLimit ≤ 10`；`RarityWeights.Weight > 0`；抽卡 `PackSize > 0`、`CoinCost > 0`、`PityCount > 0`；
  7. **稀有度覆盖**：`RarityWeights` 必须覆盖四个稀有度；
  8. **禁用行**：`Enabled = FALSE` 的卡牌只校验可解析性，跳过语义范围检查（允许保留废弃数据）。
- EditMode 测试：每条规则的正例与反例，以及"多问题一次列出"的汇总行为。

## 4. 明确不做（防止范围蔓延）

- **不做** 逐单元格累积（一行内多个非法字段只报**首个**解析错误）：需要逐格报告时要重写解析器为"返回错误列表"；当前按行报告已满足 FR-1.2 的定位要求，登记为 P3。
- **不做** 效果字符串的语法/引用校验（如 `SummonEffect:NEUTRAL_PANGO|2` 里的卡 Key 是否存在）：效果格式属 M4；现在校验等于猜格式。
- **不做** 本地化键（`NameKey`）在本地化表里的存在性校验：本地化表（FR-11）尚未建立。
- **不做** 警告级（warning）与错误级分离：当前只有"错误"，全部阻断导入；等有实际需求再加等级。
- **不做** 生成物落盘与菜单：M2-T3 会话 B 调用本校验器。

## 5. 接口约定

```csharp
namespace Card.Domain.Config
{
    /// <summary>一次导入涉及的源表集合；缺表用 null 表示（缺失本身就是校验错误）。</summary>
    public sealed class ConfigSourceSet
    {
        public ConfigSourceSet(CsvTable? cards, CsvTable? heroes, CsvTable? heroPowers,
                               CsvTable? rarityWeights, CsvTable? gachaConfig, CsvTable? rules);
        public CsvTable? Cards { get; } public CsvTable? Heroes { get; } public CsvTable? HeroPowers { get; }
        public CsvTable? RarityWeights { get; } public CsvTable? GachaConfig { get; } public CsvTable? Rules { get; }
    }

    public sealed class ConfigValidationIssue
    {
        public string TableName { get; } public int LineNumber { get; } public string ColumnName { get; } public string Message { get; }
    }

    public sealed class ConfigValidationReport
    {
        public IReadOnlyList<ConfigValidationIssue> Issues { get; }
        public bool HasErrors { get; }
        public int Count { get; }
        public ConfigBundle? Result { get; }   // 仅无错误时非 null
        public string ToText();
    }

    public sealed class ConfigBundle
    {
        public IReadOnlyList<CardDefinition> Cards { get; }
        public IReadOnlyList<HeroDefinition> Heroes { get; }
        public IReadOnlyList<HeroPowerDefinition> HeroPowers { get; }
        public IReadOnlyList<RarityWeight> RarityWeights { get; }
        public GachaConfig Gacha { get; }
        public RulesConfig Rules { get; }
    }

    public static class ConfigValidator
    {
        public static ConfigValidationReport Validate(ConfigSourceSet sources);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 全部模板合法 | `HasErrors = false`、`Result` 非空且条目数与源表行数一致 |
| AC-2 | 缺某张表 | 报"表缺失"，`Result` 为 null |
| AC-3 | 某行枚举非法 | 报出**表名/行号/列名**，且其他行的检查照常进行（同行后续问题不重复报） |
| AC-4 | 多行/多表都有问题 | 报告**一次列出全部**问题（数量正确） |
| AC-5 | `Id` 重复 | 报"Id 重复"并指出行号 |
| AC-6 | `Key` 重复 | 报"Key 重复"并指出行号 |
| AC-7 | `Heroes.HeroPowerKey` 悬空 | 报"引用了不存在的技能" |
| AC-8 | 随从 `Health = 0` | 报"随从生命必须 ≥ 1" |
| AC-9 | 非随从带攻击/生命 | 报"非随从不应有攻击/生命" |
| AC-10 | `Cost` 为负 / 超过 `ManaLimit` | 分别报错（后者用规则表的 `ManaLimit`） |
| AC-11 | `Rules` 超范围 | `HandLimit > 10`、`BoardLimit > 7`、`ManaLimit > 10`、`HeroHealth` 越界均报错 |
| AC-12 | `RarityWeights` 漏稀有度 | 报"缺少稀有度配置：X" |
| AC-13 | `RarityWeights.Weight ≤ 0` | 报错 |
| AC-14 | 抽卡数值非法 | `PackSize`/`CoinCost`/`PityCount` ≤ 0 报错 |
| AC-15 | 禁用卡（`Enabled = FALSE`）语义越界 | **不报错**（只校验可解析性） |
| AC-16 | `ToText()` | 首行给出问题总数，每条含表名/行号/列名/原因（可直接贴进对话框与日志） |
| AC-17 | 内核解耦与覆盖率 | `check.ps1` PASS；`Tools/coverage.ps1` PASS（`0_Core` ≥ 90%、`Domain + App` ≥ 80%） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Config`）+ 无 Unity 工具链
- 最少用例数：22
- 必须覆盖的边界：缺表、解析失败、重复 Id/Key、悬空外键、负值、上界（等于上限应通过、超 1 应失败）、四档稀有度缺一、禁用行、多问题汇总

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；`Config/README.md` 补"校验规则清单"
- 需更新的配置表：无
- 是否影响既有模块：无（新增类型；M2-T3 会话 B 将调用）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（新增 31 例，累计 359 passed / 0 failed）
- [x] 编译 0 error / 0 warning（Unity 清缓存干净重编译）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS / Config/README.md）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 合法模板零问题 | 已满足 | `Validate_WhenAllTablesValid_HasNoErrorsAndReturnsBundle`、`Validate_WhenRealTemplatesUsed_HasNoErrors` |
| AC-2 缺表 | 已满足 | `Validate_WhenCardsTableMissing_ReportsMissingTableWithoutResult`、`Validate_WhenRulesTableMissing_ReportsMissingTable` |
| AC-3 行解析错误可定位 | 已满足 | `Validate_WhenRowHasBadEnum_ReportsTableLineAndColumn` |
| AC-4 多问题一次列出 | 已满足 | `Validate_WhenMultipleProblemsAcrossTables_ListsAllOfThem`、`Validate_WhenGachaNumbersNotPositive_ReportsEachField`、`Validate_WhenRulesOutOfRange_ReportsEveryLimit` |
| AC-5/AC-6 Id/Key 重复 | 已满足 | `Validate_WhenCardIdDuplicated_ReportsIdDuplicate`、`Validate_WhenCardKeyDuplicated_ReportsKeyDuplicate`、`Validate_WhenRarityDuplicated_ReportsDuplicate` |
| AC-7 悬空外键 | 已满足 | `Validate_WhenHeroPowerKeyDangling_ReportsReference`、`Validate_WhenPowersTableMissing_SkipsForeignKeyCheckButReportsMissingTable` |
| AC-8 随从生命 ≥ 1 | 已满足 | `Validate_WhenMinionHealthIsZero_ReportsHealth` |
| AC-9 非随从无战斗数值 | 已满足 | `Validate_WhenSpellHasStats_ReportsNonMinionStats` |
| AC-10 费用范围 | 已满足 | `Validate_WhenCostIsNegative_ReportsCost`、`Validate_WhenCostExceedsManaLimit_ReportsCost`、`Validate_WhenCostEqualsManaLimit_Passes`（边界含上界） |
| AC-11 规则表范围 | 已满足 | `Validate_WhenRulesOutOfRange_ReportsEveryLimit`、`Validate_WhenRulesHasTwoRows_ReportsSingleRowRule` |
| AC-12 稀有度覆盖 | 已满足 | `Validate_WhenRarityMissing_ReportsMissingRarity` |
| AC-13 权重为正 | 已满足 | `Validate_WhenWeightNotPositive_ReportsWeight`、`Validate_WhenMinPerPackNegative_ReportsMinPerPack` |
| AC-14 抽卡数值 | 已满足 | `Validate_WhenGachaNumbersNotPositive_ReportsEachField`、`Validate_WhenGachaHasTwoRows_ReportsSingleRowRule` |
| AC-15 禁用卡跳过语义检查 | 已满足 | `Validate_WhenCardDisabled_SkipsSemanticChecks`、`Validate_WhenDisabledCardIsUnparsable_StillReportsParseError` |
| AC-16 `ToText()` | 已满足 | `Report_ToText_WhenNoErrors_SaysPassed`、`Report_ToText_WhenErrors_ListsCountAndEveryIssue`、两条 `Issue_ToString_*` |
| AC-17 解耦与覆盖率 | 已满足 | `check.ps1` PASS；`0_Core 97.45%`、`Domain + App 92.29%` |
| 补充 | 已满足 | `Validate_WhenHeroHealthOutOfRange_ReportsHealth`、`Validate_WhenHeroPowerCostNotPositive_ReportsCost` |

### 10.2 铁律扫描

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2/3 依赖向下、内核只依赖 BCL | 未命中 | 只用 `System` / `Collections.Generic` |
| 6 数值来自配置 | 正向命中 | 校验器把"配置数值是否合法"集中到一处，规则层不再各自判断 |
| 9 新功能带测试 | 未命中 | 31 个新用例（每条规则都有正反例） |
| 10 行数 / 复杂度 / 0 warning | 未命中 | **初版 `ConfigValidator.cs` 428 行、3 个方法 52/56/54 行**，超出"文件 ≤ 300 行、方法 ≤ 50 行"；已拆为 `ConfigValidator`（编排与共享原语）+ `CardConfigValidator` / `HeroConfigValidator` / `RuleConfigValidator`（各表规则），并把行处理抽成 `ParseCard/ParsePower/ParseHero`；`JsonValue.cs` 310 行也超限，已把缩进/转义抽到 `JsonText`。修完复测：无文件 >300 行、无方法 >50 行 |
| 11 内核与 Unity 解耦 | 未命中 | 校验器纯 BCL；`grep` 无 Unity 类型 |
| 其余 | 不涉及 | — |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 缺表 | 只报一次"源表缺失"，不级联出"技能不存在" | 一致 | AC-2、`Validate_WhenPowersTableMissing_...` |
| 费用等于 `ManaLimit` | 通过（上界含） | 一致 | `Validate_WhenCostEqualsManaLimit_Passes` |
| 禁用卡数值越界 | 不报错（允许保留废弃数据） | 一致 | AC-15 |
| 禁用卡枚举非法 | **仍然报错**（禁用 ≠ 不合法） | 一致 | `Validate_WhenDisabledCardIsUnparsable_StillReportsParseError` |
| 单行表出现两行 | 报"必须是单行表" | 一致 | `Validate_WhenGachaHasTwoRows_...`、`Validate_WhenRulesHasTwoRows_...` |
| 缺稀有度 | 整表问题（行号 0），文本不带"第 N 行" | 一致 | AC-12 + `Issue_ToString_WhenTableLevel_OmitsLineNumber` |
| 多字段同时非法 | 每字段一条问题（抽卡 3 条、规则 4 条） | 一致 | AC-4 三例 |

### 10.4 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity 清缓存重编译 | `errors: 0`、`distinct warnings: 0`、`result=Passed total=359 passed=359 failed=0` |
| 无 Unity 工具链 | 359/359 通过；`0_Core 97.45%`、`Domain + App 92.29%`（门禁 90% / 80%） |
| 静态门禁 | `Tools/check.ps1` PASS |

### 10.5 影响面

- 新增：`Domain/Config/{ConfigSourceSet,ConfigValidationIssue,ConfigValidationReport,ConfigBundle,ConfigValidator,CardConfigValidator,HeroConfigValidator,RuleConfigValidator}.cs`；`Core/JsonText.cs`（从 `JsonValue` 抽出的缩进/转义）；3 个测试文件。
- 改动既有模块：`JsonValue`（行为不变，仅抽出文本细节）、`CsvTable`（新增真实行号，M2-T3 已用）。
- 需要同步的文档 / 配置：`Config/README.md` 增加"校验规则"清单；`Docs/PROGRESS.md`。
- 回归风险：低。校验器尚无生产调用方；M2-T3 会话 B 的导入菜单将直接消费 `ConfigValidationReport`。

### 10.6 评审结论

**通过**：无 P0/P1；门禁全绿；文档已同步。

**本轮两个有价值的副产品**：

1. **无 Unity 工具链（新 Roslyn + 完整 NRT 注解）能发现 Unity 侧看不见的可空性问题**：本轮它报出 `Equals(object?)` 重写签名、`Dictionary<TState, _>` 的 `notnull` 约束、`IEqualityComparer<T>.Equals(T?, T?)` 签名、`out object?` 等 6 类 CS87xx/CS86xx —— Unity 自带 NUnit/Roslyn 版本较旧，不会报。已按建议修好（`StateMachine<TState>` 增加 `where TState : notnull`）。
2. **门禁抓到自己写的违规**：`ConfigValidator.cs` 428 行 + 3 个方法超 50 行、`JsonValue.cs` 310 行，全部在提交前拆完。
