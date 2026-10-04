# 任务卡 · M2-T3 实现导入器（CSV → JSON）

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T3 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.1（Excel 改完执行导入菜单即可生效）、FR-1.2（非法数据导入必须失败并给出行号/字段/原因）、FR-1.6（生成物带 `schemaVersion`）、NFR-11（内核可被服务端复用） |
| 规则依据 | [03 §9.1](../03-开发规范文档.md)（源表→导入器→生成物；生成物禁手改、带 schemaVersion）、[03 §5.9.4](../03-开发规范文档.md)（序列化实现放 BCL 层：System.Text.Json / MessagePack / **自定义**）、[01 §7.3](../01-开发需求文档.md)（表头/稳定性/禁数字枚举） |
| 实测前提 | **Unity 2022.3 的 BCL 不含 `System.Text.Json`**（本轮探针：`CS0234`）。故采用 03 §5.9.4 允许的"自定义 BCL 实现"，零新增依赖，且 `Card.Server` 可直接复用同一份代码。 |
| 预估 | 1 人日 / 2 会话 |
| 依赖任务 | M2-T1（契约与解析原语）、M2-T2（CSV 模板与 `CsvTable`） |

## 2. 目标（一句话）

> 让"改 CSV → 执行导入 → 生成带 `schemaVersion` 的 JSON"成为一条可被自动化测试覆盖的管线：CSV 行 → 契约对象 → JSON 文本，全程纯 BCL、错误可定位。

## 3. 范围（做什么）

本任务拆成两个会话交付（同一个任务卡）：

**会话 A（本轮）**

- `Core/JsonValue`：极简 JSON 文档模型（对象/数组/字符串/整数/布尔/null），支持构造与**确定性序列化**（固定缩进与成员顺序 → 生成物可 diff）。
- `Domain/Config/ConfigRowParser`：`CsvTable` 行 → 契约对象（`CardDefinition` / `HeroDefinition` / `HeroPowerDefinition` / `GachaConfig` / `RarityWeight` / `RulesConfig`），解析失败抛 `ConfigFormatException`，消息含**表名 + 行号 + 列名 + 原因**。
- `Domain/Config/ConfigJsonWriter`：契约 → `JsonValue`（含 `schemaVersion` 信封），与 `ConfigSchema` 的文件名/版本常量一致。
- EditMode 测试：JSON 文本**逐字符期望值**（golden）、转义、嵌套、行映射的正确与失败路径（枚举非法、整数非法、列缺失、外键悬空留给 M2-T4 的报告式校验）。

**会话 B（下一步）**

- `Card.Editor/ConfigImporter`：菜单 `Tools > Card > 导入配置`，读 `Config/Excel/*.csv` → 写 `Assets/_Project/Config/*.json`；导入前跑校验器（M2-T4）并汇总错误；成功/失败均写 `GameLog`（`LogChannel.Config`）并在对话框给出结论。
- 导入函数与菜单解耦（函数接收输入/输出目录 → 可用临时目录测试）。

## 4. 明确不做（防止范围蔓延）

- **不做** JSON 解析器（读取方向）：M2-T5 的 `CardDatabase` 需要时再实现（同一 `JsonValue` 模型，避免两套实现）。
- **不做** `ScriptableObject` 预览资产：Unity 序列化不支持 `init` 属性，为它做一套镜像模型会引入双份 schema（必然漂移）。
  该决策记入 [00 ADR-20](../00-现状解构与架构再设计.md)：**JSON 是唯一运行时真相；SO 预览等 M5 真正需要 Inspector 预览时再评估（届时优先复用 JSON 而不是镜像模型）**。
- **不做** 完整校验器（范围/唯一性/外键的**报告式**汇总）：M2-T4；本任务的解析器只做"字段本身能否解析"。
- **不做** 热加载：M2-T6。
- **不做** 运行时读盘：M2-T5。
- **不做** 生成物写入的原子性/备份：写入策略在会话 B 里按最小实现处理（先写临时文件再替换），不引入事务概念。

## 5. 接口约定

```csharp
namespace Card.Core
{
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>极简 JSON 文档模型：构造 + 确定性序列化（读取方向在 M2-T5 补）。</summary>
    public sealed class JsonValue
    {
        public JsonKind Kind { get; }
        public static JsonValue Null();
        public static JsonValue From(bool value);
        public static JsonValue From(int value);
        public static JsonValue From(string value);
        public static JsonValue Array(params JsonValue[] items);
        public static JsonValue Object();
        public JsonValue Add(string name, JsonValue value);   // 链式；同名覆盖并按首次出现顺序输出
        public string ToJson();                               // 2 空格缩进、固定顺序、行尾 LF
    }
}

namespace Card.Domain.Config
{
    /// <summary>配置行解析失败（含表名/行号/列名/原因）。</summary>
    public sealed class ConfigFormatException : System.Exception
    {
        public string TableName { get; }
        public int LineNumber { get; }
        public string ColumnName { get; }
    }

    public static class ConfigRowParser
    {
        public static CardDefinition ParseCard(CsvTable table, int rowIndex, string tableName);
        public static HeroDefinition ParseHero(CsvTable table, int rowIndex, string tableName);
        public static HeroPowerDefinition ParseHeroPower(CsvTable table, int rowIndex, string tableName);
        public static RarityWeight ParseRarityWeight(CsvTable table, int rowIndex, string tableName);
        public static GachaConfig ParseGachaConfig(CsvTable table, int rowIndex, string tableName);
        public static RulesConfig ParseRules(CsvTable table, int rowIndex, string tableName);
    }

    public static class ConfigJsonWriter
    {
        public static JsonValue ToJsonDocument<TEntry>(string entriesName, System.Collections.Generic.IReadOnlyList<TEntry> entries);
        // 具体：WriteCards / WriteHeroes / WriteHeroPowers / WriteGacha / WriteRules
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `JsonValue` 标量序列化 | 整数/布尔/字符串/空值输出符合 JSON 语法 |
| AC-2 | 字符串转义 | `"` `\` 换行/制表/控制字符按 `\uXXXX` 转义；中文不转义（保持可读） |
| AC-3 | 嵌套对象与数组 | 缩进 2 空格、层级正确、数组元素逐行 |
| AC-4 | 成员顺序 | 按首次加入顺序输出（生成物稳定、可 diff） |
| AC-5 | 空对象/空数组 | 输出 `{}` / `[]` |
| AC-6 | 卡片行解析 | 全部字段正确映射（含关键词 flags、TargetRule、Effects 列表、Enabled 布尔） |
| AC-7 | 关键词列 | 多关键词按 `|` 分隔并能解析为 flags |
| AC-8 | 效果列 | 多效果按 `|` 分隔成列表；空列 → 空列表 |
| AC-9 | 枚举非法 | 抛 `ConfigFormatException`，含表名/行号/列名 |
| AC-10 | 整数列非法 | 同上（如 `Cost` 写 `abc`） |
| AC-11 | 布尔列非法 | `Enabled` 只接受 `TRUE/FALSE/1/0`；其他值报错 |
| AC-12 | 列缺失 | 表头缺少契约列 → 报错（指出缺哪一列） |
| AC-13 | 英雄/技能/抽卡/规则行解析 | 单值字段正确映射 |
| AC-14 | JSON 信封 | 生成物顶层含 `schemaVersion`（值 = `ConfigSchema.CurrentVersion`）与条目数组 |
| AC-15 | 契约 → JSON 端到端 | 用 `Config/Excel` 的示例模板跑完整管线，输出**逐字符匹配**的期望 JSON（golden，含 2 空格缩进与成员顺序） |
| AC-16 | 内核解耦 | `0_Core`/`1_Domain` 不出现 Unity 类型（`check.ps1` PASS） |
| AC-17 | 覆盖率门禁 | `Tools/coverage.ps1` PASS（`0_Core` ≥ 90%、`Domain + App` ≥ 80%） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）+ 无 Unity 工具链
- 最少用例数：30（`JsonValue` 约 14 + 行解析约 16）
- 必须覆盖的边界：转义字符、空对象/数组、成员重复、枚举非法、整数非法、布尔非法、列缺失、空效果列、多关键词

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、`Docs/00`（ADR-20：JSON 为唯一真相，SO 预览推迟）、`Config/README.md`（补"导入菜单与生成物位置"）
- 需更新的配置表：无（生成物在会话 B 产出）
- 是否影响既有模块：`Core` 新增 `JsonValue`；`Domain.Config` 新增解析器与 JSON 写出器

## 9. 完成定义（DoD 勾选）

- [x] **会话 A**：`JsonValue` + `ConfigRowParser` + `ConfigJsonWriter` 与 41 个新用例全部完成并验证（见第 10 节）
- [x] **会话 B**：`Card.Editor` 导入菜单 + 自动化入口 + 文件产出（见第 11 节）
- [x] 编译 0 error / 0 warning（清缓存干净重编译）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审（会话 A 部分），无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS / 00 ADR-20）

---

## 10. 会话 A 证据与评审结论（2026-10-04）

### 10.1 需求对齐（会话 A 覆盖的 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 标量序列化 | 已满足 | `JsonValueTests.From_WhenScalars_WritesJsonLiterals` |
| AC-2 字符串转义 | 已满足 | `ToJson_WhenStringHasSpecialCharacters_EscapesThem`、`ToJson_WhenStringHasChinese_KeepsItReadable` |
| AC-3 嵌套缩进 | 已满足 | `ToJson_WhenNested_UsesTwoSpaceIndentAndFixedOrder`（逐字符 golden） |
| AC-4 成员顺序 | 已满足 | 同上 + `Add_WhenMemberDuplicated_KeepsFirstPositionAndReplacesValue` |
| AC-5 空对象/空数组 | 已满足 | `ToJson_WhenEmptyCollections_WritesBrackets` |
| AC-6 卡片行映射 | 已满足 | `ConfigRowParserTests.ParseCard_WhenValidRow_MapsAllFields` |
| AC-7 多关键词 | 已满足 | 同上（`Taunt|Charge` → flags） |
| AC-8 效果列表 | 已满足 | 同上 + `ParseCard_WhenEffectsEmpty_YieldsEmptyList` |
| AC-9 枚举非法 | 已满足 | `ParseCard_WhenTypeIsNumeric_Rejects`、`ParseCard_WhenTypeUnknown_Rejects` |
| AC-10 整数非法 | 已满足 | `ParseCard_WhenCostIsNotInteger_ReportsTableLineAndColumn` |
| AC-11 布尔非法 | 已满足 | `ParseCard_WhenEnabledInvalid_ReportsEnabledColumn` |
| AC-12 列缺失 | 已满足 | `ParseCard_WhenRequiredColumnMissing_ReportsHeaderLine` |
| AC-13 其余表映射 | 已满足 | `ParseHero_...`、`ParseHeroPower_...`、`ParseRarityWeight_...`、`ParseGachaConfig_...`、`ParseRules_...` |
| AC-14 JSON 信封 | 已满足 | `WriteCards_WhenNoCards_WritesEmptyArray`、`WriteHeroes_WhenValid_HasSchemaVersionEnvelope`、`EndToEnd_WhenRealSingleRowTemplatesMapped_WritesEnvelopes` |
| AC-15 端到端 golden | 已满足 | `WriteCards_WhenSingleCard_MatchesExpectedJsonExactly`（逐字符）+ 三个真实模板端到端测试 |
| AC-16 内核解耦 | 已满足 | `check.ps1` PASS |
| AC-17 覆盖率门禁 | 已满足 | `0_Core 97.44%`、`Domain + App 98.26%` |

### 10.2 铁律扫描（会话 A）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2/3 依赖向下、内核只依赖 BCL | 未命中 | 只用 `System` / `System.Globalization` / `System.Text` / `Collections.Generic`；**没有**引入 `System.Text.Json`（Unity 无此程序集，实测 CS0234） |
| 6 数值来自配置 | 正向命中 | 生成物把规则/抽卡数值固化在 JSON 里，代码不硬编码 |
| 9 新功能带测试 | 未命中 | 41 个新用例 |
| 10 行数/复杂度/0 warning | 未命中 | 最长 `JsonValue.WriteString` 约 45 行、`ConfigRowParser` 各方法均 < 30 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 解析与序列化纯 BCL；M2-T5 的服务端读盘可复用 |
| 其余 | 不涉及 | — |

### 10.3 边界场景（会话 A）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 字符串含引号/反斜杠/换行/制表/控制字符 | 正确转义（控制字符用 `\uXXXX`） | 一致 | AC-2 |
| 中文文本 | 不转义，保持可读 | 一致 | `ToJson_WhenStringHasChinese_KeepsItReadable` |
| 同名成员重复添加 | 覆盖值但保留首次位置 | 一致 | AC-4 |
| 空行导致行号偏移 | 报真实源文件行号 | 一致 | `ParseCard_WhenBlankLinesBeforeRow_ReportsRealSourceLine` |
| 表头缺列 | 报错并定位到第 1 行 | 一致 | AC-12 |
| 用数字写枚举 | 拒绝并提示"禁止写数字" | 一致 | AC-9 |

### 10.4 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity 清缓存干净重编译 | `errors: 0`、`distinct warnings: 0`、`result=Passed total=328 passed=328 failed=0` |
| 无 Unity 工具链 | 328/328 通过；`0_Core 97.44%`、`Domain + App 98.26%` |
| 静态门禁 | `Tools/check.ps1` PASS |

### 10.5 影响面与待办

- 新增：`Core/{JsonKind,JsonValue}.cs`；`Domain/Config/{ConfigFormatException,ConfigRowParser,ConfigJsonWriter}.cs`；3 个测试文件；`CsvTable` 增加 `GetSourceLineNumber`。
- **会话 B 待办**：`Card.Editor` 导入菜单（读 `Config/Excel/*.csv` → 写 `Assets/_Project/Config/*.json`，导入前调用 M2-T4 的校验器）、`Config/README.md` 补导入流程说明、`Tools/check.ps1` 是否需覆盖生成物（评估）。
- 回归风险：低（新代码尚无生产调用方）。

### 10.6 评审结论

**通过（会话 A）**：无 P0/P1；门禁全绿。会话 B 完成后在 PROGRESS 关闭 M2-T3。

---

## 11. 会话 B 证据与评审结论（2026-10-04）

### 11.1 交付

- `Card.Infrastructure/Config/ConfigFileImporter`：`Import(sourceDirectory, outputDirectory)` —— 读 6 张 CSV → 校验 → 通过才写 6 个 JSON。**校验失败一个文件都不写**（连输出目录都不创建），既有生成物保持原样（有测试锁定）。
- `Card.Infrastructure/Config/ConfigImportResult`：成功与否、校验报告、写出文件列表、`ToText()`。
- `Card.Editor/ConfigImportMenu`：`Tools > Card > 导入配置` 菜单（对话框 + `GameLog`），以及**无界面入口** `ImportForAutomation()`，用法：
  `Unity.exe -batchmode -quit -projectPath <工程> -executeMethod Card.Editor.ConfigPipeline.ConfigImportMenu.ImportForAutomation`
- 生成物写入策略：先写 `<file>.tmp` 再替换，避免中途失败留下截断文件；UTF-8 无 BOM；每个文件首行是"由 Tools/Card/导入配置 生成，请勿手改"（03 §9.1 第 2 条）。
- 实际产出：`Assets/_Project/Config/{cards,heroes,hero_powers,rarity_weights,gacha,rules}.json`（由无界面入口在副本上生成后入库，内容与仓库源表一致）。

### 11.2 需求对齐（会话 B）

| 编号 | 场景 | 是否满足 | 证据 |
| --- | --- | --- | --- |
| 一键导入 | 菜单一次完成 6 张表 | ✅ | 无界面入口实跑：`exit=0`，副本内生成 6 个 JSON（`cards.json` 2896 B 等） |
| 生成物带 `schemaVersion` | 顶层字段 | ✅ | `Import_WhenValid_GeneratedFilesCarryVersionAndNoEditMarker` |
| 非法数据导入必须失败且不产生半成品 | 校验失败 → 零文件写出 | ✅ | `Import_WhenValidationFails_WritesNothing`、`Import_WhenValidationFails_LeavesExistingOutputUntouched` |
| 错误可定位（文件/行/字段/原因） | 报告含表名/行号/列名 | ✅ | 会话 A 的报告 + 会话 B 的 `Import_WhenCsvIsMalformed_ReportsCsvFormatIssue` |
| 幂等 | 重复导入逐字节一致 | ✅ | `Import_WhenCalledTwice_WritesIdenticalContent` |

### 11.3 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity EditMode | 0 error / 0 warning / **370 passed / 0 failed**（新增 11 例：`ConfigFileImporterTests`） |
| 无 Unity 工具链 | PASS（按设计排除 `7_Tests/EditMode/Infrastructure/**`）：`0_Core 97.45%`、`Domain + App 92.33%` |
| 静态门禁 | `Tools/check.ps1` PASS（无文件 > 300 行、无方法 > 50 行） |
| 真实导入 | 无界面入口在工程副本上跑通，6 个生成物入库 |

### 11.4 评审结论

**通过**：M2-T3 两个会话全部完成，无 P0/P1；生成物已入库，M2-T5 的 `CardDatabase` 可以直接读它们。

> 说明：`ConfigFileImporterTests` 刻意写临时目录，不触碰仓库生成物；仓库里的 6 个 JSON 由 `-executeMethod` 入口产出，源表与生成物一一对应。
