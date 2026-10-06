---
name: rule-task-loop
description: CardReborn 规则任务标准闭环，从任务卡到红绿测试、双环境验证、自检评审与双提交。用于用户要求继续或开始某个里程碑任务（如继续 M3-T4、开发下一个任务、按流程做 Mx-Ty）。不用于纯答疑或临时缺陷排查。
---

# CardReborn 规则任务闭环

当用户要求开始/继续某个里程碑任务（`Mx-Ty`）时，严格按本流程执行。文档与代码冲突时以文档为准。沟通用中文。

## 1. 开工前必读

- 先读 `AGENTS.md` 的十三条铁律与禁止清单。
- 读 `Docs/02-开发计划步骤文档.md` 中该任务的确切范围（不要凭任务编号猜测内容）。
- 按需读 `Docs/00`（架构/旧新映射）、`Docs/01`（规则规格/FR/AC）、`Docs/03`（规范）。

## 2. 写任务卡

- 复制 `Docs/templates/TaskCard.template.md`，保存为 `Docs/tasks/Mx-Ty-<短名>.md`。
- 必填：基本信息、一句话目标、做什么、**明确不做（防范围蔓延）**、接口约定、**可测 AC**、测试要求、DoD。
- 遇不确定：列为“待确认问题”，按最小影响方案推进并标注假设，不自行发明规则。

## 3. 先红后绿

- 规则类必须先写失败测试（EditMode，放 `Assets/_Project/7_Tests/EditMode/...`）。
- 先确认红（编译错误或断言失败），再实现，小步保持可编译。
- 测试助手（如 `MatchTestCards`）放测试目录，用 `internal static`。
- 禁止改测试让结果变绿；失败要么修实现，要么登记缺陷。

## 4. 验证（双环境）

- 无 Unity 工具链：`powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1`
  - 含全量测试与覆盖率门禁：`0_Core ≥ 90%`、`Domain + App ≥ 80%`。
- Unity 权威验证：工程副本 + 批处理 EditMode（不占用用户编辑器）。
  - 注意已知环境问题 **M3-B1**：TRAE 沙箱可能拦截 `bee`/`upm` 等致 `isUpdating` 恒真、`EditorApplication.update` 不执行、`-runTests` 被静默跳过（exit 0 无结果）。
  - 若复现该问题，不要无限重试；向用户说明并按其决定是否暂缓，禁止伪造结果。
- 静态门禁：`powershell -ExecutionPolicy Bypass -File Tools/check.ps1`。
- 新文件的 `.meta` 需由 Unity 生成并拷回仓库；校验全仓 GUID 无重复。

## 5. 自检与评审

- 按 `Docs/04` 第 7 节六步自检，把证据回填任务卡（新增结论章节）。
- 检查铁律要点：规则三层只依赖 BCL；无 `UnityEngine`/`Debug.Log`/`Mathf`/`JsonUtility`/`Time`/`UnityEngine.Random`；状态只存数据、引用用 Id/Key；单文件 ≤ 300 行、方法 ≤ 50 行、圈复杂度 ≤ 15、0 warning。
- 输出 P0/P1 评审结论；遗留问题登记编号（如 `Mx-Bn`、级别、来源）。

## 6. 更新文档与提交

- 更新 `Docs/PROGRESS.md`：顶部任务计数、里程碑行、任务级状态行；阻塞/暂缓如实登记到未关闭问题表。
- 分两个提交（Conventional Commits，带任务编号）：
  - 代码 + 测试：`feat(domain): <描述> [Mx-Ty]`
  - 文档：`docs(mx): <描述> [Mx-Ty]`
- 精确 `git add` 相关路径，不用 `git add -A`；提交必须可编译。
- 未经用户明确要求不 push；不要移动/删除 `checkpoint/*` 标签。
- 提交后向用户汇报：交付物、验证数字、提交哈希、遗留项，并询问是否继续下一任务。
