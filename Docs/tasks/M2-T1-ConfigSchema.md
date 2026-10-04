# 任务卡 · M2-T1 定义配置 Schema

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T1 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.1（卡牌数据由配置表驱动）、FR-1.4（id/key 双索引查询）、FR-13.1/13.3（内核只依赖 BCL）、NFR-5（新增一张卡 0 行代码）、NFR-11 |
| 规则依据 | [01 §4.1 / §7.2 / §7.3](../01-开发需求文档.md)（卡牌字段、配置表清单与铁律）、[03 §9.1](../03-开发规范文档.md)（生成物含 `schemaVersion`、禁止手改）、[03 §3.2](../03-开发规范文档.md)（`XxxDefinition` 命名）、[00 §4.1](../00-现状解构与架构再设计.md)（Domain 纯 C#） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1 全部（`Result` / `Guard` / `GameLog` 等基础设施） |

## 2. 目标（一句话）

> 在 `Card.Domain` 用**纯 C# 数据契约**描述配置表结构（卡牌 / 英雄 / 英雄技能 / 抽卡 / 规则），使导入器与校验器有唯一权威的数据模型，且这些契约不含任何 Unity 类型。

## 3. 范围（做什么）

- 枚举与取值契约（与 [01 §3.5 / §4.1](../01-开发需求文档.md) 一致）：`CardType`、`CardRarity`、`CardClass`、`TargetRule`、`Keyword`（flags，12 个关键词）。
- 文本取值解析/格式化助手：`KeywordTokens`（`"Taunt|Charge"` ⇄ `Keyword`）、`ConfigTokens.TryParseEnum<TEnum>`（严格模式：拒绝纯数字、拒绝未定义值、大小写不敏感）。
- 数据契约：`CardDefinition`、`HeroDefinition`、`HeroPowerDefinition`、`GachaConfig`、`RarityWeight`、`RulesConfig`。
- 文档契约：`ConfigDocument<TEntry>`（`schemaVersion` + 条目列表）与 `ConfigSchema`（当前版本号、各表文件名常量）。
- EditMode 测试：关键词解析/格式化往返、非法 token 报错、枚举严格解析、契约默认值、文档信封默认值。

## 4. 明确不做（防止范围蔓延）

- **不做** JSON/SO 读写与 Excel 导入：分别是 M2-T3（导入器）、M2-T4（校验器）；本任务只定义"数据长什么样"。
- **不做** `Economy` / `Levels` / `Decks` 三张表的契约：它们是 P1 功能（M8 元游戏 / M9 关卡），等到对应里程碑再定义，避免现在猜测字段。
- **不做** 效果组件的类型化模型（`DamageEffect` 等）：属 M4；本任务的 `Effects` 先保留**原始描述串**（导入器只做格式切分与语法校验）。
- **不做** 运行时校验逻辑（范围/引用完整性）：M2-T4；本任务只提供解析原语。
- **不做** `ScriptableObject` 包装层：属 M2-T3 的编辑器产物。
- **不做** 运行时数据库 `CardDatabase`：M2-T5。

## 5. 接口约定

```csharp
namespace Card.Domain.Config
{
    public enum CardType { Minion, Spell, Weapon, Hero }
    public enum CardRarity { Common, Rare, Epic, Legendary }
    public enum CardClass { Neutral, Mage, Warrior }
    public enum TargetRule { None, Any, Enemy, Friendly, EnemyMinion, FriendlyMinion, AnyMinion }

    [System.Flags]
    public enum Keyword { None = 0, Taunt = 1, Charge = 2, /* … 共 12 个 */ }

    public static class KeywordTokens
    {
        public const char Separator = '|';
        public static string Format(Keyword keywords);
        public static bool TryParse(string? text, out Keyword keywords, out string? failedToken);
    }

    public static class ConfigTokens
    {
        public static bool TryParseEnum<TEnum>(string? text, out TEnum value) where TEnum : struct, System.Enum;
    }

    public sealed class CardDefinition { /* Id/Key/NameKey/Cost/Type/Rarity/Class/Attack/Health/Keywords/TargetRule/Effects/SetKey/ArtKey/AudioKey/Enabled */ }
    public sealed class HeroDefinition { /* Id/Key/NameKey/Health/HeroPowerKey/Class */ }
    public sealed class HeroPowerDefinition { /* Id/Key/Cost/TargetRule/Effects */ }
    public sealed class GachaConfig { /* PackSize/CoinCost/PityCount/PityRarity */ }
    public sealed class RarityWeight { /* Rarity/Weight/MinPerPack */ }
    public sealed class RulesConfig { /* HeroHealth/HandLimit/BoardLimit/ManaLimit */ }

    public sealed class ConfigDocument<TEntry> { public int SchemaVersion { get; init; } public IReadOnlyList<TEntry> Entries { get; init; } }
    public static class ConfigSchema { public const int CurrentVersion = 1; /* 文件名常量 */ }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `"Taunt"` / `"taunt"` 解析 | 得 `Keyword.Taunt`（大小写不敏感） |
| AC-2 | `"Taunt|Charge"` 解析 | 两个标志位都置上；顺序无关 |
| AC-3 | `" Taunt | Charge "`（含空白） | 容忍空白，解析成功 |
| AC-4 | 空串 / 全空白 / `null` | 解析为 `Keyword.None` 且不报错（空列 = 无关键词） |
| AC-5 | 未知 token（`"Fly"`） | 返回 false，并给出失败的 token 名 |
| AC-6 | `"Taunt|Fly|Charge"` | 返回 false 且指出 `Fly` |
| AC-7 | 重复 token（`"Taunt|Taunt"`） | 幂等，结果等于 `Taunt` |
| AC-8 | `Format` → `TryParse` 往返 | 得到同一组标志位（空集格式化为空串） |
| AC-9 | `ConfigTokens.TryParseEnum` 合法值 | 成功且值正确（大小写不敏感） |
| AC-10 | 纯数字（`"1"`） | **拒绝**（配置表禁止用数字代替枚举） |
| AC-11 | 未定义名字（`"Dragon"`） | 拒绝 |
| AC-12 | 契约默认值 | 字符串字段默认为空串、`Effects` 默认为空集合（不返回 null） |
| AC-13 | `ConfigDocument<T>` 默认值 | `Entries` 为空集合，`SchemaVersion` 与 `ConfigSchema.CurrentVersion` 一致时可被接受 |
| AC-14 | 内核解耦 | `1_Domain` 不出现 Unity 类型（`check.ps1` R1 PASS） |
| AC-15 | 无 Unity 进程可编译 | `Tools/coverage.ps1` 的内核库里加入 `1_Domain` 后仍能编译并通过测试 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Config`）+ 独立 .NET 工具链
- 最少用例数：20
- 必须覆盖的边界：null/空/空白、大小写、重复 token、未知 token、纯数字、未定义枚举名、往返一致性、默认值

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；`Tools/coverage.ps1` 与内核库（纳入 `1_Domain`）
- 需更新的配置表：无（本任务只定义契约）
- 是否影响既有模块：`Tools/Coverage` 内核库新增 `1_Domain` 源码链接

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 262 passed / 0 failed，其中新增 34 例）
- [x] 编译 0 error / 0 warning（清缓存干净重编译验证）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 单关键词 / 大小写不敏感 | 已满足 | `TryParse_WhenSingleKeyword_SetsFlag`、`TryParse_WhenLowerCase_IsCaseInsensitive` |
| AC-2 多关键词 | 已满足 | `TryParse_WhenMultipleKeywords_SetsAllFlags`、`TryParse_WhenOrderReversed_ProducesSameFlags` |
| AC-3 容忍空白 | 已满足 | `TryParse_WhenWhitespaceAroundTokens_IsTolerated` |
| AC-4 空列 = 无关键词 | 已满足 | `TryParse_WhenEmpty_YieldsNone`（null / `""` / `"   "`） |
| AC-5 未知 token 报名字 | 已满足 | `TryParse_WhenUnknownToken_FailsAndReportsToken` |
| AC-6 混合时指出冒犯者 | 已满足 | `TryParse_WhenMixedWithUnknown_FailsAndReportsOffender` |
| AC-7 重复 token 幂等 | 已满足 | `TryParse_WhenDuplicateTokens_IsIdempotent`、`TryParse_WhenTrailingSeparator_IsTolerated` |
| AC-8 格式化往返 | 已满足 | `Format_ThenTryParse_IsLossless`（含全 12 关键词样本）、`Format_WhenMultiple_UsesStableDeclarationOrder`、`Format_WhenNone_IsEmpty` |
| AC-9 枚举严格解析 | 已满足 | `TryParseEnum_WhenValidName_Parses`、`TryParseEnum_WhenWhitespacePadded_Parses` |
| AC-10 拒绝纯数字 | 已满足 | `TryParseEnum_WhenNumeric_Fails`、`TryParse_WhenNumericToken_Fails` |
| AC-11 拒绝未定义名 | 已满足 | `TryParseEnum_WhenUndefinedName_Fails` |
| AC-12 契约默认值 | 已满足 | `CardDefinition_WhenDefaulted_HasSafeDefaults`、`HeroDefinition_...`、`HeroPowerDefinition_...`、`RulesConfig_...`、`GachaConfig_...`、`RarityWeight_...` |
| AC-13 文档信封 | 已满足 | `ConfigDocument_WhenDefaulted_HasCurrentVersionAndEmptyEntries`、`ConfigDocument_WhenInitialized_KeepsEntries`、`ConfigSchema_ExposesFileNamesAndVersion` |
| AC-14 内核解耦 | 已满足 | `check.ps1` PASS（R1 覆盖 `1_Domain`），R6 asmdef 守门通过 |
| AC-15 无 Unity 进程可编译 | 已满足 | `Tools/Coverage` 内核库纳入 `1_Domain` 后：262 个测试全过，`Domain + App` 覆盖率 93.94% |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅 `System`、`System.Collections.Generic`、`System.Text` |
| 4 表现层只读 | 不涉及 | 纯数据契约，无表现逻辑 |
| 6 数值来自配置 | **正向命中** | 本任务正是把英雄生命/手牌上限/场面上限/法力上限/抽卡参数变成配置契约 |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态（`ConfigSchema` 只含常量） |
| 9 新功能带测试 | 未命中 | 34 个新用例 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | 最大文件 `KeywordTokens.cs` 122 行；最长方法 `TryParse` 约 45 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity 类型；`init` 访问器所需的 `IsExternalInit` 用条件编译 shim 提供（Unity 缺该类型） |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/7/12/13） | 不涉及 | 无玩法继承、命令入口、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 关键词列为 null / 空 / 纯空白 | 视为"无关键词"，合法 | 一致 | AC-4 |
| 关键词列含多余空白与尾随 `|` | 容忍 | 一致 | AC-3、`TryParse_WhenTrailingSeparator_IsTolerated` |
| 未知关键词夹在合法关键词之间 | 失败并指出冒犯者，结果不部分生效 | 一致 | AC-6（失败时 `keywords = None`） |
| 用数字代替枚举（`"1"`） | 拒绝 | 一致 | AC-10 |
| 枚举名未定义（`"Dragon"`） | 拒绝 | 一致 | AC-11 |
| 契约未初始化 | 字符串为空串、集合为空集合（不是 null） | 一致 | AC-12 |
| 全 12 个关键词组合 | 格式化 → 解析往返无损 | 一致 | `Format_ThenTryParse_IsLossless` |

### 10.4 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity 清缓存干净重编译 + 测试 | `errors: 0`、`distinct warnings: 0`、`result=Passed total=262 passed=262 failed=0` |
| 无 Unity 工具链 | `dotnet test` 262/262 通过；`0_Core 97.71%`、`Domain + App 93.94%` |
| 静态门禁 | `Tools/check.ps1` PASS（含 R6 asmdef 守门） |

### 10.5 影响面

- 新增文件：`0_Core/IsExternalInit.cs`（Unity 的 netstandard2.1 缺 C# 9 `init` 所需类型，条件编译提供）、`1_Domain/Config/*.cs`（14 个）、`7_Tests/EditMode/Config/*.cs`（2 个）。
- 改动既有模块：`Tools/Coverage` 内核库纳入 `1_Domain`（覆盖率统计与门禁随之扩展为 Core ≥ 90% + Domain/App ≥ 80%）。
- 需要同步的文档 / 配置：`Docs/PROGRESS.md`；无配置表变更。
- 回归风险：低。契约目前无调用方；其中 `Effects` 暂以字符串保存（M4 解析为效果组件），M3/M4 需要时再类型化。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿；文档已同步。

**本轮踩到并记录的两个坑**（供后续任务复用）：

1. **Unity 缺 `System.Runtime.CompilerServices.IsExternalInit`**：C# 9 的 `init` 在 netstandard2.1 下无法编译，必须提供条件编译 shim（`#if !NET5_0_OR_GREATER`），否则与 .NET 5+ 的 BCL 冲突（CS0433）。
2. **Unity 自带 NUnit 版本较老**：`Assert.That(array, Has.Count.EqualTo(n))` 会因数组的 `Count` 是显式接口实现而失败（"Property Count was not found"）；改用 `Is.EqualTo(n)` 断言 `.Count` 属性。
