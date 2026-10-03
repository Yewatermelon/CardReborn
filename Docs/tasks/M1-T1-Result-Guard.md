# 任务卡 · M1-T1 `Result` / `Guard`

> 使用方式：复制本模板，填写完整后再开始编码。字段缺一不可，尤其是"明确不做"与"验收标准"。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T1 |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-13.1 / FR-13.3（内核只依赖 BCL）、FR-5.14（规则纯逻辑可测）、NFR-4（可测试性）、NFR-6（0 warning）、NFR-11（内核可移植） |
| 规则依据 | [03 第 4 节](../03-开发规范文档.md)（代码风格：`Guard` 判空）、[03 第 5.9 节](../03-开发规范文档.md)（内核解耦五条禁令）、[03 第 11 节](../03-开发规范文档.md)（可预期失败用返回值表达）、[00 第 4.1 节](../00-现状解构与架构再设计.md)（Core 内容清单） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | 无（M0 已完成） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供"操作结果"与"参数校验"两块零依赖基础设施，让 Domain / Application 层能统一表达成功与失败原因、统一拒绝非法入参，同时保证 Core 只依赖 BCL（可被无 Unity 的服务端进程直接编译）。

## 3. 范围（做什么）

- `Result`（无返回值）与 `Result<T>`（带值）：成功 / 失败状态、错误码、错误说明、取值与 `TryGetValue`、值相等性、`ToString`。
- `Guard`：`NotNull`、`NotNullOrWhiteSpace`、`NotNullOrEmpty`（集合）、`Positive`、`NotNegative`、`InRange`、`Require`。
- 语义边界：**可预期的业务失败 → 返回 `Result`；程序员错误（传 null、越界）→ 抛 BCL 异常**（与 03 第 11 节一致）。
- EditMode 单元测试覆盖成功、失败、null、空/空白、0、负数、区间上下界、失败态取值、相等性。

## 4. 明确不做（防止范围蔓延）

- **不做**自定义异常体系（`GameException` 之类）；失败一律走返回值，只有契约被违反才抛 BCL 异常。
- **不做** `Result<TError, TValue>` 强类型错误码；错误码本任务用 `string`，等 M3 规则内核确定 `CommandError` 枚举后再评估是否升级（届时另开任务）。
- **不做** `Option<T>`（[00 第 4.1 节](../00-现状解构与架构再设计.md) 列在 Core，但 [02 M1](../02-开发计划步骤文档.md) 任务表未列入）——保持 M1 任务表逐条对应，需要时单开任务。
- **不开启** `<Nullable>enable</Nullable>`：会波及既有代码并可能引入 warning，与"M1 门禁 0 warning"冲突，单独立项（记为 M1-R1）。
- **不引入**任何第三方库（FluentResults / CSharpFunctionalExtensions 等）。
- **不做**日志输出（`GameLog` 属 M1-T5）；**不做**异常→Result 的自动转换。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>不带返回值的结果：成功或失败（含错误码与说明）。</summary>
    public readonly struct Result : IEquatable<Result>
    {
        public bool IsSuccess { get; }
        public bool IsFailure { get; }
        public string ErrorCode { get; }     // 成功时为空字符串，永不为 null
        public string ErrorMessage { get; }  // 未提供时为空字符串，永不为 null

        public static Result Success();
        public static Result Failure(string errorCode, string errorMessage = "");
    }

    /// <summary>带返回值的结果；失败时不允许取值。</summary>
    public readonly struct Result<T> : IEquatable<Result<T>>
    {
        public bool IsSuccess { get; }
        public bool IsFailure { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public T Value { get; }              // 失败态访问 → InvalidOperationException

        public static Result<T> Success(T value);
        public static Result<T> Failure(string errorCode, string errorMessage = "");
        public bool TryGetValue(out T value);
    }

    /// <summary>公共 API 入参契约校验；违反即抛 BCL 异常（程序员错误）。</summary>
    public static class Guard
    {
        public static T NotNull<T>(T value, string paramName) where T : class;
        public static string NotNullOrWhiteSpace(string value, string paramName);
        public static IReadOnlyCollection<T> NotNullOrEmpty<T>(IReadOnlyCollection<T> items, string paramName);
        public static int Positive(int value, string paramName);
        public static int NotNegative(int value, string paramName);
        public static int InRange(int value, int minInclusive, int maxExclusive, string paramName);
        public static void Require(bool condition, string paramName, string message);
    }
}
```

> 文件组织说明：`Result` 与 `Result<T>` 同属一个类型族、互相配合，放在同一个 `Result.cs` 中；若强行分文件，泛型版只能叫 `ResultOfT.cs` 或 `Result{T}.cs`，两者都劣于现状。此处为对 03 第 3.2 节"一文件一主类型"的**显式说明**，不是疏漏。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `Result.Success()` | `IsSuccess=true`、`IsFailure=false`、`ErrorCode` 与 `ErrorMessage` 均为空串 |
| AC-2 | `Result.Failure("E_X", "说明")` | `IsFailure=true`，错误码与说明原样保留，`ToString()` 同时含两者 |
| AC-3 | `Result.Failure(null)` / `Result.Failure("")` / `Result.Failure("   ")` | 抛 `ArgumentException`（参数名 `errorCode`）；`Failure("E")` 的说明为空串而非 null |
| AC-4 | `Result<T>.Success(v)` | `Value == v`，`TryGetValue` 返回 true 并输出 `v` |
| AC-5 | `Result<T>.Failure("E")` | `Value` 抛 `InvalidOperationException`；`TryGetValue` 返回 false 且 out 值为 `default` |
| AC-6 | `Result<string>.Success(null)` | 仍为成功（成功与否只由错误码决定），`TryGetValue` 返回 true、值为 null |
| AC-7 | `Guard.NotNull` | null → `ArgumentNullException` 且 `ParamName` 正确；非 null 时**原样返回同一实例** |
| AC-8 | `Guard.NotNullOrWhiteSpace` | null → `ArgumentNullException`；空串 / 空白 → `ArgumentException`；正常值原样返回 |
| AC-9 | `Guard.NotNullOrEmpty` | null → `ArgumentNullException`；空集合 → `ArgumentException`；非空集合原样返回 |
| AC-10 | `Guard.Positive` / `Guard.NotNegative` | `Positive(0)`、`Positive(-1)` 抛 `ArgumentOutOfRangeException`，`Positive(1)` 通过；`NotNegative(-1)` 抛，`NotNegative(0)` 通过 |
| AC-11 | `Guard.InRange` | 下界含、上界不含；越界抛 `ArgumentOutOfRangeException`；`min > max` 视为调用方错误抛 `ArgumentException` |
| AC-12 | `Guard.Require(false, ...)` | 抛 `ArgumentException` 且消息为传入的 message；`true` 时不抛 |
| AC-13 | 相等性 | 同错误码同说明的两个 `Result`/`Result<T>` 相等、`GetHashCode` 相同；`==`/`!=` 行为与 `Equals` 一致；成功态两个 `Result` 相等 |
| AC-14 | 内核解耦 | `Card.Core` 不出现 `UnityEngine` / `Debug.Log` / `Mathf` / `DateTime.Now`（`Tools/check.ps1` R1 扫描 + 独立 grep 双证据） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）
- 最少用例数：24
- 必须覆盖的边界：null、空串、纯空白、0、负数、区间下界、区间上界（不含）、区间反向、失败态访问值、`TryGetValue` 失败态、引用类型为 null 的成功态、相等性与哈希一致性、`ToString` 内容

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M1-T1 状态与证据）、本任务卡回填结论
- 需更新的配置表：无
- 是否影响既有模块：无（纯新增文件；`Card.Core` asmdef 无引用，不需改动）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 61 passed / 0 failed，其中新增 60 个用例）
- [x] 编译 0 error / 0 warning（Unity 2022.3.54f1c1 批处理日志中 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.7 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-03）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 成功结果语义 | 已满足 | `ResultTests.Success_WhenCreated_ReportsSuccessWithEmptyError`、`Default_WhenNotInitialized_IsTreatedAsSuccess` |
| AC-2 失败保留错误码与说明 | 已满足 | `ResultTests.Failure_WhenCreated_KeepsErrorCodeAndMessage`、`ToString_WhenFailureWithMessage_ContainsCodeAndMessage` |
| AC-3 空/空白错误码被拒 | 已满足 | `Failure_WhenErrorCodeIsNull_ThrowsArgumentNullException`、`Failure_WhenErrorCodeIsBlank_ThrowsArgumentException("")/("   ")` |
| AC-4 带值成功 | 已满足 | `ResultOfValueTests.Success_WhenCreated_ExposesValue`、`Success_WhenTryGetValue_ReturnsTrueAndValue` |
| AC-5 失败态不可取值 | 已满足 | `Failure_WhenAccessingValue_ThrowsInvalidOperationException`、`Failure_WhenTryGetValue_ReturnsFalseAndDefaultValue` |
| AC-6 引用类型 null 值仍为成功 | 已满足 | `ResultOfValueTests.Success_WhenReferenceValueIsNull_IsStillSuccess` |
| AC-7 `Guard.NotNull` | 已满足 | `NotNull_WhenNull_ThrowsArgumentNullExceptionWithParamName`、`NotNull_WhenValue_ReturnsSameInstance` |
| AC-8 `Guard.NotNullOrWhiteSpace` | 已满足 | `NotNullOrWhiteSpace_WhenNull_...`、`..._WhenBlank_ThrowsArgumentException("")/(" ")/("\t")`、`..._WhenValid_ReturnsSameString` |
| AC-9 `Guard.NotNullOrEmpty` | 已满足 | `NotNullOrEmpty_WhenNull_...`、`..._WhenEmpty_ThrowsArgumentException`、`..._WhenHasItems_ReturnsSameInstance` |
| AC-10 数值正负校验 | 已满足 | `Positive_WhenValueIsNotPositive_...(0)/(-1)/(int.MinValue)`、`NotNegative_..._(0)/(int.MaxValue)` |
| AC-11 区间校验（下含上不含） | 已满足 | `InRange_WhenValueEqualsMinInclusive_...`、`WhenValueInsideRange`、`WhenValueAtOrAboveMaxExclusive(10)/(11)`、`WhenValueBelowMin`、`WhenRangeIsEmpty`、`WhenMinGreaterThanMax` |
| AC-12 `Guard.Require` | 已满足 | `Require_WhenConditionIsFalse_ThrowsArgumentExceptionWithMessage`、`Require_WhenConditionIsTrue_DoesNotThrow` |
| AC-13 相等性与哈希一致性 | 已满足 | `ResultTests.Equals_*`（4 例）、`EqualityOperators_WhenResultsDiffer_MatchEquals`、`ResultOfValueTests.Equals_*`（4 例） |
| AC-14 内核零 Unity 依赖 | 已满足 | `Tools/check.ps1` R1 扫描 PASS；另独立扫描 `UnityEngine|Debug.|Mathf|JsonUtility|DateTime.Now|UnityEditor` 命中 0 |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 1 组件优于继承 | 未命中 | 纯工具类型，无玩法继承 |
| 2 依赖只能向下 | 未命中 | `Card.Core` asmdef 无任何引用；新代码只依赖 BCL |
| 3 内核只依赖 BCL | 未命中 | 独立扫描 0 命中（AC-14） |
| 4 表现层只读 | 不涉及 | 无表现层改动 |
| 5 一律走 `GameCommand` | 不涉及 | 不涉及对局操作 |
| 6 数值来自配置 | 不涉及 | 无硬编码数值，仅提供校验工具 |
| 7 运行时禁 `UnityEditor` | 未命中 | 无 `UnityEditor` / `AssetDatabase` |
| 8 禁隐式全局访问 | 未命中 | `Guard` 为无状态静态工具，无 `Instance` / `Find` |
| 9 新功能带测试 | 未命中 | 新增 60 个用例，先红后绿 |
| 10 行数/复杂度/0 warning | 未命中 | `Result.cs` 235 行、`Guard.cs` 110 行；方法均 ≤ 50 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API、无 `DateTime.Now`；不涉及状态序列化 |
| 12 权威宿主与网络解耦 | 不涉及 | 无网络代码 |
| 13 阶段二范围锁 | 不涉及 | 未引入任何第三方库 |
| 禁止清单（拼音名/匈牙利前缀/`#region`/单行多语句/省略花括号） | 未命中 | 命名英文、无 `#region`、一行一句、花括号齐全 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| `default(Result)` 未初始化 | 视为成功（错误码为 null） | 一致 | `Default_WhenNotInitialized_IsTreatedAsSuccess` |
| 错误码为 null / `""` / `"   "` | 分别抛 `ArgumentNullException` / `ArgumentException`，ParamName 为 `errorCode` | 一致 | AC-3 三例 |
| 失败态访问 `Value` | 抛 `InvalidOperationException` 且消息含错误码 | 一致 | `Failure_WhenAccessingValue_ThrowsInvalidOperationException` |
| 引用类型成功值为 null | 仍为成功，`TryGetValue` 返回 true | 一致 | `Success_WhenReferenceValueIsNull_IsStillSuccess` |
| 区间上界（maxExclusive） | 视为越界 | 一致 | `InRange_WhenValueAtOrAboveMaxExclusive_ThrowsArgumentOutOfRangeException(10)` |
| 区间反向 / 空区间（min ≥ max） | 视为调用方错误，抛 `ArgumentException` | 一致 | `InRange_WhenRangeIsEmpty_...`、`InRange_WhenMinGreaterThanMax_...` |
| 数值极值 | `int.MinValue` / `int.MaxValue` 不溢出、不误判 | 一致 | `Positive_*(int.MinValue)`、`NotNegative_*(int.MaxValue)` |
| 空集合 / null 集合 | 分别抛 `ArgumentException` / `ArgumentNullException` | 一致 | AC-9 三例 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode -testResults results.xml`

结果：`result=Passed total=61 passed=61 failed=0 duration=0.098s`

| 测试类 | 用例数 |
| --- | --- |
| `Card.Tests.EditMode.Core.GuardTests` | 28 |
| `Card.Tests.EditMode.Core.ResultOfValueTests` | 16 |
| `Card.Tests.EditMode.Core.ResultTests` | 16 |
| `Card.Tests.EditMode.SmokeTests` | 1 |

编译警告：`warning CS` 命中 0 处。血状态（实现前）：`CS0246 'Result'/'Guard' could not be found` → exit 1。

> 验证方式说明：本轮在**工程副本**上用 Unity 批处理模式跑测试（结果 XML 与日志留在临时目录），不占用你正在使用的编辑器会话；`Tools/check.ps1` 仍在主仓库直接运行。

### 10.5 影响面

- 新增文件：`Assets/_Project/0_Core/Result.cs`、`Assets/_Project/0_Core/Guard.cs`，以及 3 个 EditMode 测试文件。
- 改动既有模块：**无**（未触碰 asmdef、未改既有测试）。
- 需要同步的文档 / 配置：无配置表变更；`Docs/PROGRESS.md` 已更新。
- 回归风险：低。唯一实际风险是命名冲突——测试侧曾因 NUnit 内部同名 `Guard` 报 `CS0122`，已在测试文件显式 `using Card.Core;` 解决，并作为后续新增测试的注意事项。

### 10.6 反向审查（挑刺三条）

1. **"规范第 4 节第 10 条要求开启 `Nullable`，为什么没开？"**
   确实未开启，已作为**明确不做**写入第 4 节并登记 M1-R1。理由：开启后既有代码与测试（如向 `Assert.Throws` 传 null 字面量）会产生 CS86xx 警告，与"M1 门禁 0 warning"直接冲突，需一次性全仓改造，不能夹带进本任务。
2. **"`Result` 与 `Result<T>` 同文件，违反'一文件一主类型'。"**
   属**显式说明的偏差**（见第 5 节文件组织说明）：两者是同一类型族、语义互锁；拆分的唯一命名选择是 `ResultOfT.cs` 或 `Result{T}.cs`，均不优于现状。若评审坚持拆分，成本极低，可立即执行。
3. **"`Guard.Require` 用 `ArgumentException` 表达前置条件是否语义过窄？"**
   本任务的原则是"契约违反 = 程序员错误"，`ArgumentException` 携带 ParamName 能让日志直接定位调用点。"对象/规则状态非法"属规则层（M3），届时用领域错误码 + `Result` 表达，而非异常。

### 10.7 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、61 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

| 编号 | 级别 | 描述 | 处理 |
| --- | --- | --- | --- |
| M1-R1 | P2 | 全仓库启用 `<Nullable>enable</Nullable>`（03 第 4 节第 10 条） | 单独立项，M1 内评估；需一次性清理 CS86xx 警告 |
