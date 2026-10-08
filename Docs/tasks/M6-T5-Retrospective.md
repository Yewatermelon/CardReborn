# 任务卡 · M6-T5 阶段复盘

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M6-T5 |
| 所属里程碑 | M6 垂直切片打通（Demo Gate） |
| 上游需求 | Docs/02 M6-T5：复盘记录（模板见 Docs/04 第 9 节）；完成标准 = 改进项已转为任务卡 |
| 依赖任务 | M6-T1 ~ T4 全部收官 |

## 2. 目标

> 按 Docs/04 第 9 节模板产出 M6 里程碑复盘；对 P1 缺陷做 5 Why；把改进项转成可执行任务卡（或随本任务立即落地）；刷新 PROGRESS 与 HANDOFF 快照，为 M7 做准备度评估。

## 3. 做什么

- 产出 `Docs/reviews/M6-垂直切片打通-评审与复盘.md`：目标对照、度量数据、做得好、P1 5 Why 根因、改进项、风险更新、M7 准备度。
- 改进项落地：
  1. **验证环境维度缺失**（T2 冒烟测试从未在 PlayMode 实跑，T3 才连环爆 4 个未知效果类型）→ 流程改进，随本复盘立即落地：任务卡验证章节必须区分 EditMode / PlayMode 并分别留证据。
  2. **配置/代码契约漂移**（GainManaEffect 缺实现、DrawEffect/DrawCardEffect 命名不一致、CARD_029 缺 OnDeath 前缀，全部运行时才炸）→ 转任务卡 `BACKLOG-ConfigEffectValidation.md`：CardDatabase 构建期全量校验效果表达式可解析，排期 M9-T1 前置。
  3. **有状态表现组件复用复位契约**（B7 隐形卡：CardFadeOutView 后加，SetData 复位路径未同步）→ 规范改进，随本复盘立即落地：新增可视副作用组件必须同步纳入 SetData 全量复位路径（写入复盘 + 项目记忆）。
- 刷新 `Docs/PROGRESS.md`：M6 5/5 ✅；当前阶段行更新为"下一步 M7-T1"。
- 刷新 `Docs/HANDOFF.md` 第 1/3/7/10 节快照数字（M5 收官时漏更新，本次一并修正到 M6 口径）。

## 4. 明确不做

- 不改任何代码与配置（纯文档任务，无红绿测试要求）。
- 不打 `checkpoint/m6-complete` 标签（需用户确认 Demo Gate 后另行执行）。
- 不修正式本地化/提示系统（B3/B4 维持 M9 排期）。
- 不重写 Docs/02 任务表（改进项通过 BACKLOG 任务卡与新任务并轨，不改里程碑结构）。

## 5. 验收标准

| 编号 | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 复盘文档 | 按 Docs/04 第 9 节模板七节齐全；P1 缺陷有 5 Why 且根因落到机制 |
| AC-2 | 改进项 | 每条根因至少对应一个改进项；改进项全部转为任务卡或随本任务落地留痕 |
| AC-3 | PROGRESS | M6 行 5/5 ✅，Demo Gate 证据回填；当前阶段指向 M7 |
| AC-4 | HANDOFF | 快照数字与 M6 收官口径一致（958/3/761、298 文件、覆盖率 96.52%/91.47%） |
| AC-5 | 可追溯 | 复盘引用 M6-B1~B8 编号与 PROGRESS 一一对应，无失真 |

## 6. 测试要求

- 纯文档任务，无自动化测试要求；以 AC-3/AC-4 数字核对为证据。

## 7. DoD

- [x] 满足全部 AC 且附证据
- [x] 编译 0 error / 0 warning（未触代码，随 T4 口径）
- [x] check.ps1 + coverage.ps1 通过（未触代码，随 T4 口径：298 文件 PASS、761 passed）
- [x] 改进项已转为任务卡（BACKLOG-ConfigEffectValidation）或随本任务落地（验证环境维度、复位契约）

## 8. 结论与证据（M6-T5 收官）

- 复盘文档：`Docs/reviews/M6-垂直切片打通-评审与复盘.md`（2026-10-08）。
- 改进项 1（验证环境维度）已随本任务落地：本仓库后续任务卡的验证章节区分 EditMode/PlayMode 并分别留证据（见复盘 §5）。
- 改进项 2 转任务卡：`Docs/tasks/BACKLOG-ConfigEffectValidation.md`（排期 M9-T1 前置）。
- 改进项 3（复位契约）已随本任务落地：写入复盘 §5 与项目记忆。
- PROGRESS M6 行 5/5 ✅；HANDOFF 快照刷新到 M6 口径。
- 数字核对：check.ps1 298 文件 PASS；kernel 761 passed（96.52% / 91.47%）；Unity EditMode 958 / PlayMode 3。
