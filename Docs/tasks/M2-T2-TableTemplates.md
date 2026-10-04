# 任务卡 · M2-T2 编写配置源表模板（CSV）

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T2 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.1（卡牌数据由配置表驱动）、FR-1.2（导入时给出明确错误）、NFR-5（新版仅用已有效果的卡 = 0 行代码） |
| 规则依据 | [01 §7.2 / §7.3](../01-开发需求文档.md)（配置表清单与铁律：首行表头、Id/Key 不可改、禁用数字代替枚举、禁止行序依赖、范围校验在工具里）、[03 §9.1](../03-开发规范文档.md)（源表 → 导入器 → 生成物）、[00 AGENTS 目录表](../../AGENTS.md)（`Config/Excel/` 在 `Assets/` 之外，Unity 不导入） |
| 决策 | **源表用 CSV 而不是 .xlsx**（用户 2026-10-04 选择方案 A）：Excel 可直接打开/另存 CSV，导入器零新依赖（纯 BCL），省掉第三方读写库与版本维护；代价是没有下拉校验与多 Sheet 说明，改用 `Config/README.md` + 该任务列的取值说明代替。新增 [00 ADR-19](../00-现状解构与架构再设计.md)。 |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M2-T1（配置契约与解析原语） |

## 2. 目标（一句话）

> 交付 6 张可被工具直接消费的 CSV 源表模板 + 填写说明，并让"模板本身合法"成为可自动验证的门禁——表头拼错、枚举写错、外键指向不存在的行，都会在测试里立刻失败。

## 3. 范围（做什么）

- `Core` 新增极简 `CsvTable`（纯 BCL）：表头索引、按列名取值、BOM/CRLF/空行处理、行列数不一致时**明确报错**（配置表铁律：错误必须带位置）。
- 6 张源表模板（`Config/Excel/`，首行表头）：
  `Cards.csv`、`Heroes.csv`、`HeroPowers.csv`、`RarityWeights.csv`、`GachaConfig.csv`、`Rules.csv`。
- `Config/README.md`：每张表的列含义、枚举合法取值、分隔符、填写铁律与常见错误示例。
- EditMode 测试（不依赖 UnityEngine）：
  - `CsvTable` 解析边界（BOM、CRLF、空行、缺列、行列不齐）；
  - **模板校验测试**：读取真实模板 → 表头必须与契约列名一致 → 每行都能解析成 M2-T1 的契约（枚举/关键词/整数/布尔）→ 外键（如 `HeroPowerKey`）必须存在。

## 4. 明确不做（防止范围蔓延）

- **不做** 导入器与生成物（JSON/SO）：M2-T3。
- **不做** 完整字段级校验器与错误报告格式：M2-T4（本任务的测试只覆盖"模板自身合法"，不含范围/唯一性等业务规则）。
- **不做** `Economy` / `Levels` / `Decks` 三张表：对应契约尚未定义（M8/M9），现在写模板等于猜字段。
- **不做** `.xlsx` 解析与下拉校验：ADR-19 已决定用 CSV。
- **不做** 首版 30 张卡的实际内容：M2-T7（本任务每表只放少量示例行，够驱动导入器与校验器开发即可）。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>极简 CSV 表（配置源表用）：首行表头、逗号分隔、不带引号转义。</summary>
    public sealed class CsvTable
    {
        public static CsvTable Parse(string text);
        public IReadOnlyList<string> Header { get; }
        public int ColumnCount { get; }
        public int RowCount { get; }
        public IReadOnlyList<string> GetRow(int rowIndex);
        public bool HasColumn(string columnName);
        public int IndexOfColumn(string columnName);          // 不存在返回 -1
        public string GetCell(int rowIndex, string columnName); // 列不存在 → ArgumentException
    }
}

# Config/Excel（首行表头；列名与契约字段一一对应）
Cards.csv          : Id,Key,NameKey,DescKey,Cost,Type,Rarity,Class,Attack,Health,Keywords,TargetRule,Effects,SetKey,ArtKey,AudioKey,Enabled
Heroes.csv         : Id,Key,NameKey,Health,HeroPowerKey,Class
HeroPowers.csv     : Id,Key,Cost,TargetRule,Effects
RarityWeights.csv  : Rarity,Weight,MinPerPack
GachaConfig.csv    : PackSize,CoinCost,PityCount,PityRarity
Rules.csv          : HeroHealth,HandLimit,BoardLimit,ManaLimit
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `CsvTable.Parse` 解析常规表 | 表头与行内容正确，`RowCount` 正确 |
| AC-2 | 文件带 UTF-8 BOM | BOM 不被当成第一个列名的一部分 |
| AC-3 | CRLF 与 LF 混用 | 均能解析 |
| AC-4 | 空行 / 全空白行 | 跳过，不计入行数 |
| AC-5 | 列名不存在 | `HasColumn` 为 false、`IndexOfColumn` 返回 -1、`GetCell` 抛 `ArgumentException` |
| AC-6 | 某行列数多于表头 | 抛 `FormatException` 并指出行号（配置表错误必须可定位） |
| AC-7 | 某行列数少于表头 | 缺的列按空字符串读取（Excel 另存 CSV 的常见行为） |
| AC-8 | 6 张模板文件存在且表头与契约列名完全一致 | 通过 |
| AC-9 | 模板每一行的枚举/关键词列 | 都能被 M2-T1 的严格解析器接受（拒绝数字与未定义值） |
| AC-10 | 模板外键 | `Heroes.HeroPowerKey` 必须能在 `HeroPowers.Key` 里找到；`Cards.Id`/`Key` 不重复 |
| AC-11 | 模板数值列 | 全部为合法整数（`Cost`/`Attack`/`Health`/`Weight` 等）与布尔（`Enabled` 接受 TRUE/FALSE/1/0） |
| AC-12 | 模板行数 | 每张表至少 1 行示例数据，且 `RarityWeights` 覆盖全部 4 个稀有度 |
| AC-13 | 内核解耦 | 新增 `Core`/`Domain` 代码不含 Unity 类型（`check.ps1` PASS） |
| AC-14 | 覆盖率门禁 | `Tools/coverage.ps1` PASS（`0_Core` ≥ 90%、`Domain + App` ≥ 80%） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）+ 无 Unity 工具链
- 最少用例数：24（`CsvTable` 约 12 + 模板校验约 12）
- 必须覆盖的边界：BOM、CRLF、空行、列缺失、行列数不齐、枚举非法、外键悬空、重复 Id/Key、布尔写法
- 测试必须在**无 Unity 进程**下也能跑：定位仓库根目录要靠向上查找 `Config/Excel`，不得使用 `Application.dataPath`

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、`Docs/00`（ADR-19）、`Config/README.md`（新增）
- 需更新的配置表：**本任务即产出源表模板**
- 是否影响既有模块：`Core` 新增 `CsvTable`（被 M2-T3 导入器复用）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 287 passed / 0 failed，其中新增 25 例：`CsvTableTests` 14 + `ConfigTemplateTests` 11）
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS / 00 ADR-19 / Config/README.md）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 常规解析 | 已满足 | `CsvTableTests.Parse_WhenSimpleTable_ReadsHeaderAndRows`、`Parse_WhenHeaderOnly_HasNoRows` |
| AC-2 BOM | 已满足 | `Parse_WhenBomPresent_StripsItFromFirstHeader` |
| AC-3 CRLF/LF | 已满足 | `Parse_WhenCrlf_HandlesLineEndings`（解析器同时支持 `\r\n`、`\r`、`\n`） |
| AC-4 空行跳过 | 已满足 | `Parse_WhenBlankLinesPresent_SkipsThem` |
| AC-5 列名不存在 | 已满足 | `IndexOfColumn_WhenColumnMissing_ReturnsMinusOne`、`GetCell_WhenColumnMissing_ThrowsArgumentException`、`IndexOfColumn_WhenCaseDiffers_IsCaseSensitive` |
| AC-6 行列数过多 | 已满足 | `Parse_WhenRowHasMoreCellsThanHeader_ThrowsWithLineNumber`（消息含"第 2 行"） |
| AC-7 行列数过少 | 已满足 | `Parse_WhenRowHasFewerCellsThanHeader_PadsWithEmpty` |
| AC-8 6 张模板表头一致 | 已满足 | `Templates_WhenLoaded_AllSixExistAndParse`、`Cards_HeaderMatchesContractColumns`、`Heroes_HeaderMatchesContractColumns`、`HeroPowers_...`、`RarityWeights_...`、`GachaConfig_...`、`Rules_...` |
| AC-9 枚举/关键词列合法 | 已满足 | `Cards_EveryRowParsesIntoValidValues`、`HeroPowers_EveryRowParsesIntoValidValues`、`GachaConfig_HasSingleValidRow` |
| AC-10 外键与唯一性 | 已满足 | `Cards_KeysAndIdsAreUnique`、`Heroes_HeroPowerKeysExistInHeroPowers` |
| AC-11 类型与布尔 | 已满足 | 上述解析测试中对 `int` / `TRUE|FALSE|1|0` 的断言 |
| AC-12 每表至少 1 行、稀有度覆盖 | 已满足 | `Templates_WhenLoaded_AllSixExistAndParse`、`RarityWeights_CoversAllRaritiesWithPositiveWeights` |
| AC-13 内核解耦 | 已满足 | `check.ps1` PASS（`0_Core` 新增 `CsvTable` 无 Unity 类型） |
| AC-14 覆盖率门禁 | 已满足 | `Tools/coverage.ps1` PASS：`0_Core 97.69%`、`Domain + App 93.94%` |
| 补充 | 已满足 | `Cards_MinionsHavePositiveStatsAndSpellsAreZero`、`Rules_HasSingleRowWithinPrdRanges`、`Parse_WhenCellsHaveWhitespace_TrimsThem`、`GetRow_WhenIndexOutOfRange_ThrowsArgumentOutOfRangeException` |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | `CsvTable` 只用 `System` / `System.Collections.Generic` |
| 4 表现层只读 | 不涉及 | 无表现逻辑 |
| 6 数值来自配置 | **正向命中** | 模板覆盖英雄生命/手牌上限/场面上限/法力上限/抽卡与稀有度权重 |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态 |
| 9 新功能带测试 | 未命中 | 25 个新用例 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `CsvTable.cs` 159 行；最长方法 `Parse` 36 行（初版 61 行超限，已拆出 `FillHeader` / `AddRow` 修正）；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity 类型；测试定位仓库根目录刻意不使用 `Application.dataPath` |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/7/12/13） | 不涉及 | 无玩法继承、命令入口、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 文件带 BOM | 第一个列名不带 `\uFEFF` | 一致 | AC-2 |
| 混用 CRLF/LF | 均能解析 | 一致 | AC-3 |
| 中间夹空行 | 跳过，行号仍准确 | 一致 | AC-4、AC-6 |
| 行列数多于表头 | 抛 `FormatException` 且消息含行号 | 一致 | AC-6 |
| 行列数少于表头（Excel 另存常见） | 缺列读作空串 | 一致 | AC-7 |
| 表头大小写不一致 | 视为列不存在（严格） | 一致 | `IndexOfColumn_WhenCaseDiffers_IsCaseSensitive` |
| 模板里写数字枚举（`1`） | 被模板测试拒绝 | 一致 | `Cards_EveryRowParsesIntoValidValues`（经 `ConfigTokens`） |
| 悬空外键（`HeroPowerKey` 不存在） | 被模板测试拒绝 | 一致 | AC-10 |

### 10.4 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity EditMode | `errors: 0`、`distinct warnings: 0`、`result=Passed total=287 passed=287 failed=0` |
| 无 Unity 工具链 | `已通过! - 失败: 0，通过: 287，总计: 287`；`0_Core 97.69%`、`Domain + App 93.94%` |
| 静态门禁 | `Tools/check.ps1` PASS |

### 10.5 影响面

- 新增：`Core/CsvTable.cs`；`Config/Excel/{Cards,Heroes,HeroPowers,RarityWeights,GachaConfig,Rules}.csv`；`Config/README.md`；`7_Tests/EditMode/{Core/CsvTableTests,Config/ConfigTemplateTests,Config/ConfigTemplateTestSupport}.cs`。
- 改动既有模块：无（`CsvTable` 尚无生产调用方，M2-T3 导入器将复用）。
- 需要同步的文档 / 配置：`Docs/00`（ADR-19）、`Docs/PROGRESS.md`。
- 回归风险：低。模板数据量为示例级别（每表数行），M2-T7 会扩到 ≥30 张卡。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿；文档已同步。

**登记待办**：

| 编号 | 级别 | 描述 | 处理 |
| --- | --- | --- | --- |
| M2-R1 | P3 | `CsvTable` 暂不支持 `#` 注释行（表内说明只能用 README） | 若策划确需表内注释，M2-T3 接入时再补 |
| M2-R2 | P3 | 模板校验目前只在测试里做，尚未在编辑器侧提供"导入前校验"入口 | M2-T3/T4 落地（校验器与导入器共用同一套解析原语） |
