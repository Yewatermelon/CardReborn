# 任务卡 · M1-T9 内核解耦检查规则 ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T9 ★（★ = 必须通过"无 Unity 依赖"检查；本任务是**检查规则本身**） |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-13.1（内核与 Unity 解耦，可由独立 .NET 进程编译）、NFR-8（Domain/Application 不得引用 UnityEngine，由 asmdef 强制）、NFR-11、铁律 3/11 |
| 规则依据 | [03 第 5.9.1 节](../03-开发规范文档.md)（五条禁令）、[03 第 5.9.5 节](../03-开发规范文档.md)（如何自查）、[03 第 2.3 节](../03-开发规范文档.md)（asmdef 配置要点与依赖方向）、[04 §5.B / §11.1](../04-代码复盘Review规范.md)（架构合规检查项与门禁） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M0-T7（检查脚本本体）、M1-T1…T8（被检查的内核代码） |

## 2. 目标（一句话）

> 把"内核不得依赖 Unity"从口头约定变成**可自动执行、且自身可被验证**的门禁：扫描覆盖 03 §5.9.1 的五条禁令与 asmdef 依赖方向，注释里的 API 名称不再误伤，规则失效时能立刻发现。

## 3. 范围（做什么）

- 重写 `Tools/check.ps1` 的扫描核心，规则集：
  - **R1 内核解耦**（`0_Core` / `1_Domain` / `2_Application` / `2_Network`）：Unity 类型（`using UnityEngine`、`UnityEngine.`、`Vector2/3/4`、`Quaternion`、`Color`、`Rect`）、Unity 日志（`Debug.Log*`）、Unity 数值（`Mathf.`）、Unity 序列化（`JsonUtility`、`ScriptableObject`、`[SerializeField]`）、Unity 随机、Unity 时间（`Time.*`）、系统时间（`DateTime.Now/UtcNow/Today`）。
  - **R2** 运行时代码不得使用 `UnityEditor` / `AssetDatabase`；**R3** 禁隐式查找（含**可变 `static Instance`**，`static readonly` 的不可变常量不报）；**R4** 统一日志（禁 `Debug.Log`）；**R5** 单文件 ≤ 300 行。
  - **R6（新增）asmdef 守门**：内核四层必须 `noEngineReferences: true`、不得引用 Unity 程序集；所有层不得向上依赖（按 Core→Domain→Application/Network→Infrastructure→Presentation→Bootstrap 的层级表）。
- **扫描前剥离注释与字符串字面量**、并使用**词边界**匹配（修掉 M1-R3 记录的两类误报）。
- 新增 `-SelfTest`：在临时目录搭探针树，逐条断言 R1–R6 都能报错、干净样本零误报；自检结束自动清理。
- 修复"违规时退出码仍为 0"的缺陷（`Format-Table` 输出污染函数返回值 → 改用 `Out-Host`）。
- 文档同步：03 §5.9.5（规则清单与两条实现要求）、06 §7.3（提交前跑门禁与自检）。

## 4. 明确不做（防止范围蔓延）

- **不做** 全量 C# 语法解析（不引入 Roslyn 分析器）：本阶段用"剥离注释/字符串 + 词边界正则"即可满足 03 §5.9.5 的要求；真正的语义级检查留给后续需要时评估。
- **不做** `Card.Server` 编译验证：该工程尚未建立（M11）；已在 03 §5.9.5 标注"待补（M11）"。
- **不做** 圈复杂度、覆盖率、重复代码检测：属后续任务（M1-R2 覆盖率、M10 质量报告）。
- **不做** CI 集成（GitHub Actions）：当前门禁是本地脚本，M10/M11 再谈流水线。
- **不做** 强制扫描 `Time` / `DateTime` 的"硬红线"：依据 [05 第 688 行](../05-联网对战_状态同步_设计文档.md)，帧同步时代的该约束已**部分撤销**；本脚本仍会报告，但档位与其它 R1 项一致，且不阻止发布判定以外的场景。

## 5. 接口约定

```powershell
# 扫描真实工程（PASS 退出码 0；有违规退出码 1）
powershell -ExecutionPolicy Bypass -File Tools/check.ps1

# 只报告、不改变退出码（供人工浏览）
powershell -ExecutionPolicy Bypass -File Tools/check.ps1 -ReportOnly

# 探针自检：临时目录搭"故意违规 + 干净样本"探针树，逐条断言规则有效
powershell -ExecutionPolicy Bypass -File Tools/check.ps1 -SelfTest
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 故意在 `1_Domain` 放 `using UnityEngine` + `Debug.Log` + `Time.time` | 脚本报告 R1（≥3 条）与 R4（≥1 条），**退出码 1**，输出含 `文件:行号:源码` |
| AC-2 | 违规文件移除后 | 脚本 PASS、退出码 0 |
| AC-3 | 注释/字符串里提到禁用 API | 不产生任何违规（M1-R3 回归） |
| AC-4 | asmdef 配置错误（内核 `noEngineReferences: false`、内核引用 `UnityEngine.*`、应用层引用表现层） | 各报一条 R6 |
| AC-5 | `-SelfTest` | R1–R6 全部命中探针、干净样本 0 误报，退出码 0；规则失效时退出码 1 |
| AC-6 | 真实工程扫描 | PASS（53 个 C# 文件、10 个 asmdef；无违规） |
| AC-7 | 词边界 | `using System.Runtime.CompilerServices` 不被判为 Unity 时间调用 |

## 7. 测试要求

- 测试类型：`check.ps1 -SelfTest`（PowerShell 探针树）+ 真实工程探针注入
- 最少用例数：6 条规则 × 至少 1 个探针 = 6 个探针文件/配置 + 1 个干净样本
- 必须覆盖的边界：注释/字符串中的禁用词、`static readonly` 常量不误报、向上依赖、内核引擎引用未关闭、超长文件

## 8. 涉及文档与配置

- 需更新的文档：`Docs/03` §5.9.5、`Docs/06` §7.3、`Docs/PROGRESS.md`
- 需更新的配置表：无
- 是否影响既有模块：门禁规则变严，可能暴露既有代码问题（本轮真实工程扫描为 PASS）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10 节）
- [x] 规则可自检（`-SelfTest`，含干净样本零误报）
- [x] 脚本可编译执行、0 error / 0 warning（PowerShell 无编译产物；以实际运行与退出码为准）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（本任务只改工具与文档，未触碰内核代码）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 10. 执行证据与评审结论（2026-10-04）

### 10.1 门禁自检

```
CardReborn - gate self test (throwaway probe tree in temp)
  R1  expected >=  2  found  9  [ok]  kernel Unity / serialization probes
  R2  expected >=  1  found  2  [ok]  UnityEditor probe in Infrastructure
  R3  expected >=  2  found  2  [ok]  GameObject.Find + mutable static Instance probe
  R4  expected >=  1  found  2  [ok]  Debug.Log probe in Bootstrap
  R5  expected >=  1  found  1  [ok]  oversized file probe
  R6  expected >=  3  found  3  [ok]  asmdef probes (engine references + upward dependency)
  clean probe false positives: 0
PASS - every rule fired on its probe and clean code stayed clean.   (exit 0)
```

### 10.2 真实工程探针（AC-1 / AC-2）

| 步骤 | 结果 |
| --- | --- |
| 在 `Assets/_Project/1_Domain/_ProbeViolation.cs` 写入 `using UnityEngine` + `Debug.Log` + `Time.time` | FAIL，4 条违规：R1 ×3（`using UnityEngine` 第 1 行、`Debug.Log` 第 9 行、`Time.time` 第 10 行）、R4 ×1；**退出码 1** |
| 删除该探针（含可能的 `.meta`） | 工程恢复干净：`git status` 无残留 |
| 再次扫描真实工程 | PASS，退出码 0（53 个 C# 文件、10 个 asmdef） |

> 顺带发现并修复：违规路径下退出码曾为 0（`Format-Table` 的输出被当成函数返回值的一部分）。这类缺陷会让 CI/钩子误判为通过，属 P1 级工具缺陷，已在本任务内修复并复验。

### 10.3 评审结论

**通过**（AI 自审）：无未关闭 P0/P1；AC-1…AC-7 全部有可复核证据；`Docs/03 §5.9.5`、`Docs/06 §7.3`、`PROGRESS` 已同步。

**关闭的历史遗留项**：M1-R3（扫描误报）——已通过"剥离注释/字符串 + 词边界"修复，并由自检的干净样本锁定。

**仍待处理**：M1-R1（全仓启用 `<Nullable>`）、M1-R2（覆盖率统计），按约定在 M1 收尾统一处理。
