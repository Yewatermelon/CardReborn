# 交接说明（AI → AI）

> 这份文档写给**接手的 AI 代理**（以及未来的人类维护者）。
> 目标：不依赖任何聊天记录，也能在 30 分钟内搞清楚"项目是什么、现在到哪、下一步做什么、怎么验证、哪里容易踩坑"。
> 维护约定：每次里程碑结束或交接时更新本文件；正文以仓库内文档与代码为准，冲突时以 `Docs/00`–`Docs/06` + `AGENTS.md` 为准。

## 0. 交接操作清单（项目所有者照这个顺序做）

> 细节都在本文后续章节；这一节只给"按顺序做什么"。带 ✅ 的是**已经完成**的。

- [x] **1. 冻结当前状态**：确认工作区干净、`main` 与远端一致、门禁与测试全绿。命令：`git status -sb`、`Tools/check.ps1`、`Tools/check.ps1 -SelfTest`、`Tools/coverage.ps1`。
- [x] **2. 打检查点标签**：`checkpoint/m2-complete`（已推送远端，指向 `9eb3c13`，见第 10 节）。
- [x] **3. 生成离线备份**：`E:\Unity\Project\CardReborn-backups\CardReborn-2026-10-04-m2-complete.bundle`（3.04 MB，SHA256 见第 10 节）。
- [ ] **4. 把备份复制到异地**（**唯一还没做的关键一步**）：把整个工程目录（**含 `.git`**）和 bundle 一起复制到移动硬盘 / 网盘 / 另一台机器。现在两份备份都在同一块硬盘上，防不了硬件故障。
- [ ] **5. 确认环境三件套**（第 10.6 节）：Unity `2022.3.54f1c1`（许可证已激活）、.NET SDK 9.x、Git + Git LFS（`git lfs install` 至少执行过一次）。
- [ ] **6. 确认新 AI 的访问与授权**（第 8 节）：仓库访问权限（私有仓库需给凭据）；是否允许联网（首次 `dotnet restore`、`git push`、`git lfs fetch`）；是否允许必要时聚焦 Unity 窗口（多数验证已做成无界面方式）。
- [ ] **7. 把"开场指令"粘给新 AI**（可直接复制下面这段）：

  > 这是一个 Unity 2022.3 卡牌游戏项目（`CardReborn`），已完成 M0–M2，下一步是 M3。
  > 请先按顺序读：`AGENTS.md` → `Docs/HANDOFF.md` → `Docs/PROGRESS.md` → `Docs/02` 的 M3 任务表。
  > 然后跑三条命令确认环境：`Tools/check.ps1`、`Tools/check.ps1 -SelfTest`、`Tools/coverage.ps1`。
  > 期望结果：静态门禁 PASS、自检 PASS、443 用例全过、覆盖率 `0_Core 96.51%` / `Domain + App 92.36%`。
  > 把这三条命令的实际输出贴回来；确认无误后再开工。第一个任务是 **M3-T1**，按仓库既有流程：先写任务卡、先写测试再实现。
  > 约束：不要移动或删除 `checkpoint/*` 标签；每个任务结束更新 `Docs/PROGRESS.md` 并在任务卡里写结论；里程碑结束写 `Docs/reviews/` 复盘。

- [ ] **8. 让新 AI 做一次"交接验收"**：跑第 7 步的三条命令并把**实际输出**贴回来；数字对得上才算交接成功（对不上先查环境，不要急着改代码）。
- [ ] **9. 把边界说清楚**（第 8 节第二段）：是否沿用现有流程约定（任务卡 / 先测试 / 复盘）；是否允许修改 `Docs/00`–`Docs/06` 的规范；哪些操作必须先问你（联网、聚焦 GUI、删除、改远端）。
- [ ] **10. 收尾**：让新 AI 更新本文第 3 节快照（一开工数字就会变），并在下一个里程碑结束时用同样方式打新标签。

## 1. 一分钟概览

| 项 | 内容 |
| --- | --- |
| 项目 | `Card`：炉石式回合制卡牌游戏（Unity 2022.3 LTS），仓库 `E:\Unity\Project\CardReborn`（GitHub: `https://github.com/Yewatermelon/CardReborn.git`） |
| 当前阶段 | **阶段一（PVE）已完成 M0、M1、M2；下一步是 M3 领域模型与规则内核** |
| 交付方式 | 每个任务一张任务卡（`Docs/tasks/`）+ 先写测试 + 双环境验证 + 提交里带证据 |
| 已具备的能力 | 规则内核（Core）、配置管线（CSV → 校验 → JSON → 卡池 → 热加载）、测试 443 例、静态门禁、无 Unity 覆盖率工具链 |
| 下一步第一件事 | 读 `Docs/02` 的 M3 任务表 → 按任务卡流程从 **M3-T1** 开始（详见本文第 5 节） |

## 2. 你的第一步（建议按顺序做）

1. 读 `AGENTS.md`（强制入口：铁律、目录、提交流程）→ `Docs/00`（为什么这样分层）→ `Docs/03`（怎么写）→ `Docs/04`（怎么自检/评审）。
2. 读 `Docs/PROGRESS.md`（当前进度与证据）与本文第 3、6 节。
3. 跑一遍验证，确认环境可用（三条命令见第 4 节）：
   - `Tools/check.ps1`（静态门禁）
   - `Tools/check.ps1 -SelfTest`（门禁自身是否有效）
   - `Tools/coverage.ps1`（无 Unity 跑内核测试 + 覆盖率）
4. 读 `Docs/02` 的 **M3 任务表**，写第一张任务卡，再动手。

## 3. 当前状态快照

> 快照日期：2026-10-04。**每次交接前请重新跑第 4 节的命令并更新本节数字。**

| 项 | 值 |
| --- | --- |
| 分支 / 提交 | `main` = `origin/main`，工作区干净（验证时的 HEAD 是 `6246914`；本交接文档提交后 SHA 会前进，以 `git log -1` 为准） |
| 里程碑 | M0 ✅ 8/8、M1 ✅ 9/9、M2 ✅ 7/7；**M3 ⬜ 未开始** |
| 编译 | Unity 2022.3.54f1c1 清缓存重编译：**0 error / 0 warning** |
| 测试 | **443 passed / 0 failed**（EditMode）；其中 215 例是 M2 新增 |
| 覆盖率 | `0_Core 96.51%`、`Domain + Application 92.36%`（门禁 90% / 80%） |
| 静态门禁 | `Tools/check.ps1` PASS（R1 内核解耦 / R2 编辑器 API / R3 隐式查找 / R4 日志 / R5 行数 / R6 asmdef） |
| 配置管线 | 37 行卡表（35 启用 + 2 废弃）→ 校验 → `Assets/_Project/Config/*.json` → `CardDatabase` 可查；热加载可用 |
| 未关闭项 | 均为 P3（见第 7 节） |

## 4. 验证环境与命令（关键：两种跑法）

项目里有**两套互补的验证**，都必须会：

### 4.1 无 Unity 的 .NET 工具链（快、可用在无 GUI 环境）

```powershell
powershell -ExecutionPolicy Bypass -File Tools/check.ps1            # 静态门禁
powershell -ExecutionPolicy Bypass -File Tools/check.ps1 -SelfTest  # 门禁自身有效性
powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1         # 编译内核 + 跑测试 + 覆盖率门禁
```

- `Tools/Coverage/` 用 `Compile Include` **链接** `Assets/_Project/0_Core`、`1_Domain`、`2_Application` 与 `7_Tests/EditMode/**`（不复制源码），因此在 Unity 之外也能编译内核并跑测试。
- 首次运行需要联网还原 NuGet 包（NUnit / NUnit3TestAdapter / coverlet / Microsoft.NET.Test.Sdk）。
- **按设计排除** `7_Tests/EditMode/Infrastructure/**`：`Card.Infrastructure` 允许使用 Unity，不属于"必须无 Unity 可编译"的集合。
- 它能发现 Unity 侧发现不了的问题（新 Roslyn + 完整 NRT 注解），例如 M2 就靠它抓出 6 类可空性缺陷。

### 4.2 Unity 侧（权威门禁：真编译 + 真 Test Runner）

推荐**批处理副本**跑法（不抢用户窗口焦点、不碰用户正在用的工程）：

```powershell
$dst = Join-Path $env:TEMP 'CardReborn_CI'
robocopy 'E:\Unity\Project\CardReborn' $dst /MIR /XD Library Temp Logs UserSettings .git .vs obj
& 'E:\Unity\2022.3.54f1c1\Editor\Unity.exe' -batchmode -nographics -projectPath $dst `
  -runTests -testPlatform EditMode -testResults "$dst-results.xml" -logFile "$dst-unity.log"
# 注意：-runTests 不能与 -quit 同用；Unity.exe 会把自己转成独立进程，父进程立刻返回，需要轮询结果文件
```

要点与陷阱：

1. **改过构建配置（`Assets/csc.rsp`、asmdef、`Packages/manifest.json`）时必须清缓存**：先删 `Library/Bee` 与 `Library/ScriptAssemblies` 再跑，否则可能用旧参数编译并把过期 DLL 当成"通过"（M1 收尾真实踩过）。
2. 若要跑编辑器功能（导入/重载配置），用无界面入口：
   ```powershell
   & $u -batchmode -nographics -quit -projectPath $dst `
     -executeMethod Card.Editor.ConfigPipeline.ConfigImportMenu.ImportForAutomation -logFile "$dst-import.log"
   # 重载：Card.Editor.ConfigPipeline.ConfigImportMenu.ReloadForAutomation
   ```
3. 批处理或工具生成的**新资源**不会自动产生 `.meta`：需要让 Unity 再跑一次纯导入（`-batchmode -quit` 不加 `-runTests`），然后把 `.meta` 拷回仓库；拷完检查 GUID 不重复。
4. 用户通常不在电脑前：**批准弹窗需要用户操作**（GUI 聚焦、联网、写 `.git`）。能用无界面方案就别抢焦点（见第 6 节第 9 条）。

## 5. 开发流程约定（本仓库的"做事方式"）

1. **先读后写**：`AGENTS.md` §3 的固定工作流 —— 读文档 → 写任务卡 → **先测试**（规则类必须）→ 实现 → 自检 → 评审 → 更新文档。
2. **任务卡**：复制 `Docs/templates/TaskCard.template.md` 到 `Docs/tasks/<任务ID>-<名称>.md`，必须填"明确不做"与可测 AC；完成后把 DoD 勾选并把自检/评审结论写回同一张卡（参考 `Docs/tasks/M2-T5-CardDatabase.md`）。
3. **提交**：Conventional Commits + 需求编号（如 `feat(config): ... [M2-T7]`），`type: feat|fix|refactor|test|docs|chore|perf|build`；一次提交只做一件事（实践上：代码+测试一个提交，文档/进度一个提交）。
4. **证据优先**：提交前必须有 `Logs/agent-status.json`（0 编译错误）或批处理日志 + 测试结果；`Logs/` 已 gitignore，所以**证据要重新生成**，不能引用上一轮的。
5. **进度与复盘**：每个任务更新 `Docs/PROGRESS.md`（状态 + 证据）；每个里程碑结束写 `Docs/reviews/<里程碑>-评审与复盘.md`（模板见 `Docs/04` 第 8/9 节）。
6. **文档冲突**：文档与代码冲突时以文档为准；若实现需要偏离文档，必须同时改文档（或写 ADR 并在 `Docs/00` 记录 —— 已有先例：ADR-18/19/20、`Docs/00` §4.2 的 CardDatabase 拆分说明）。

## 6. 已知坑与陷阱（照着做能省几小时）

| # | 现象 | 原因 | 正确做法 |
| --- | --- | --- | --- |
| 1 | Unity 里 `using System.Text.Json;` 报 `CS0234` | Unity 2022.3 的 BCL 不含 `System.Text.Json` | 用本仓库的 `Card.Core.JsonValue` / `JsonParser`（自定义 BCL 实现，见 03 §5.9.4） |
| 2 | C# 9 的 `init` 访问器报 `CS0518 IsExternalInit` | Unity 的 netstandard2.1 缺该类型 | 已在 `0_Core/IsExternalInit.cs` 提供**条件编译**shim（`#if !NET5_0_OR_GREATER`，否则与 .NET 5+ 冲突） |
| 3 | `Assets/csc.rsp` 里写 `#` 注释导致 200+ `CS2001/CS1504` | Unity 把 rsp 内容按空白拆给编译器，不支持注释 | rsp 只放开关（当前：`-nullable:enable`）；说明写文档 |
| 4 | 测试里 `Assert.That(array, Has.Count.EqualTo(n))` 失败："Property Count was not found" | Unity 自带 NUnit 较旧，数组的 `Count` 是显式接口实现 | 断言 `.Count` 属性本身（`Assert.That(x.Count, Is.EqualTo(n))`） |
| 5 | "删掉一个脚本后编译仍报它的错" | Bee 缓存沿用旧编译参数/旧 DLL | 删 `Library/Bee` + `Library/ScriptAssemblies` 再跑（第 4.2 节第 1 条） |
| 6 | 工具生成的资源缺 `.meta` / GUID 重复 | 批处理产出后没再导入 | 跑一次纯导入生成 `.meta`，拷回后检查 GUID 唯一（脚本见 M2 收尾提交） |
| 7 | `Tools/` 下的 `.csproj` 没入库、反而提交了 `bin/obj` | Unity 模板 `.gitignore` 的 `*.csproj` 把手工工具工程也屏蔽了 | 已有窄例外 `!Tools/**/*.csproj`（AGENTS §6 / 03 §12.3）；构建产物已从索引移除 |
| 8 | 效果串里写 `\|` 导致被拆成两个效果 | `\|` 是"多效果"分隔符 | 效果内参数用 `/`：`DamageEffect:5/GainArmorEffect:5`（约定见 `Config/README.md`） |
| 9 | 抢用户窗口焦点/反复弹审批很烦 | 沙箱下写 `.git`、读 `%LOCALAPPDATA%`、联网都要批准 | 用批处理副本 + `-executeMethod` + `Tools/*.ps1`，把需要批准的次数压到最少；确需 GUI 时一次合并多步 |
| 10 | 改内容后一批测试红了 | 早期测试断言了"恰好 N 张卡"这类绝对值 | 内容规模统一由 `ConfigContentTests`（≥ 阈值）把关；单测只断言特定对象的值 |

## 7. 还没做的事（交接时请确认）

### 7.1 立即要做（M3）

`Docs/02` M3（领域模型与规则内核，质量门禁最严）10 项任务：`MatchState` / `PlayerState` / `CardInstance` / `Zone` / `ManaPool` / `HeroState`、`GameCommand` + `CommandResult`、`RuleEngine.Validate`（出牌/攻击/技能的费用·目标·场位校验）、胜负判定、状态序列化往返测试。

> 注意：M3 起状态必须**可序列化、可裁剪、可增量**（阶段二状态同步的前置，见 `Docs/03` §5.9.4 与 `Docs/05`）。`MatchState` 里禁止出现 Unity 类型或 UI 引用。

### 7.2 已登记但未做的 P3（可顺手做，不阻塞）

| 编号 | 描述 | 位置 |
| --- | --- | --- |
| M1-R4 | 补齐未覆盖行（`PublishReport.ToString` 70%、`StateMachine` 82%、`ObjectPool` 96%、`Result` 95%） | 见 M1 复盘 |
| M2-R1 | `CsvTable` 支持 `#` 注释行（表内说明） | 见 M2 复盘 |
| M2-R3 | 生成物 schema 自动迁移（当前版本不匹配直接失败） | `ConfigJsonReader` + M2 复盘 |
| M0-R2 | 编辑器桥接层的"文件触发"入口（当前导入/重载已用 `-executeMethod` 绕开） | 见 M0 复盘 |

### 7.3 后续里程碑

阶段一：M3 → M4（回合状态机与效果系统）→ M5（表现层，依赖 TMP Essentials，已导入）→ M6（垂直切片 Demo Gate）→ M7（AI）→ M8（元游戏，可并行）→ M9（内容与打磨）→ M10（发布验收）。
阶段二：M11–M14（本机房主模式联网验证，权威宿主 + 状态同步，BCL TCP，零新依赖）。

## 8. 接手方需要准备/确认的东西

**环境（一次性）**

1. Unity **2022.3.54f1c1**（当前路径 `E:\Unity\2022.3.54f1c1\Editor\Unity.exe`）且许可证已激活；工程用 TMP Essentials 已导入。
2. .NET SDK（本机为 9.0.308）——`Tools/coverage.ps1` 与 `Tools/Coverage` 需要；首次要联网还原 NuGet 包。
3. Git 与远端访问（`origin` 已配置，LFS 已启用；推送需要凭据/网络）。
4. 权限：写仓库内的 `.git`（提交/推送）、读 `%LOCALAPPDATA%\Unity` 日志、必要时聚焦 Unity 窗口——在受限沙箱里这些都需要用户批准。

**需要用户确认的三件事**

1. 是否允许接手方在验证时**联网**（NuGet 还原、`git push`）；
2. 是否允许**聚焦 Unity 窗口**（仅在必须用编辑器菜单/GUI 时）；
3. 是否需要接手方沿用本文第 5 节的流程约定（任务卡 + 先测试 + 复盘），还是另有要求。

**可选清理**

- `%TEMP%\CardReborn_CI`（批处理验证副本，约 1–2 GB）可保留复用，也可直接删除（下次按第 4.2 节重建）。
- `Logs/`（gitignore）里的证据文件可保留参考，但**不要**当作当前状态的证据。

## 9. 快速文件地图

| 想找什么 | 去哪 |
| --- | --- |
| 规矩与铁律 | `AGENTS.md`、`Docs/03`、`Docs/04` |
| 做什么 / 做到哪 | `Docs/01`（需求）、`Docs/02`（计划与任务表）、`Docs/PROGRESS.md`（进度与证据） |
| 网络方案（阶段二） | `Docs/05` |
| 每个任务的做法与结论 | `Docs/tasks/`（一任务一卡） |
| 里程碑复盘 | `Docs/reviews/` |
| 规则内核（纯 C#） | `Assets/_Project/0_Core`、`1_Domain`、`2_Application` |
| 配置契约 / 校验 / 卡池 | `Assets/_Project/1_Domain/Config` |
| 配置源表（改数据改这里） | `Config/Excel/*.csv` + `Config/README.md` |
| 配置生成物（勿手改） | `Assets/_Project/Config/*.json` |
| 编辑器工具（导入/重载） | `Assets/_Project/6_Editor/ConfigImportMenu.cs` |
| 门禁与工具链 | `Tools/check.ps1`、`Tools/coverage.ps1`、`Tools/Coverage/README.md` |

## 10. 检查点与恢复（已实测）

> 目的：万一后续开发出问题（或需要换回旧 AI/旧版本），能**精确回到 M2 完成时的状态**并从那里继续。
> 关键点：**不要依赖聊天记录**——本文件 + 仓库本身就能把项目恢复到可继续开发的状态。

### 10.1 检查点标签

| 项 | 值 |
| --- | --- |
| 标签 | `checkpoint/m3-complete`（annotated） |
| 指向提交 | 本文档所在提交（2026-10-06，M0–M3 完成 + 本交接/恢复文档更新；提交后立即打标签，代码状态与验证时一致） |
| 标签说明 | 无 Unity 工具链 588 用例全过、编译 0 error/0 warning、覆盖率 `0_Core 96.51%` / `Domain+App 90.65%`、规则内核与状态契约（洗牌/序列化/增量）可用；Unity 权威批处理验证受 M3-B1 阻塞（P2，M4 完成判定前补跑） |

> 历史检查点：`checkpoint/m2-complete`（annotated，**已推送远端**，指向 `9eb3c13`，443 用例、覆盖率 92.36%）保持不动；`checkpoint/*` 标签一律不移动、不删除。

查看方式：`git tag -n99 -l 'checkpoint/*'`；切过去：`git checkout checkpoint/m2-complete`。

### 10.2 三种保底方式（按可靠性排序）

| 方式 | 内容 | 体积 | 恢复时依赖 | 实测结论 |
| --- | --- | --- | --- | --- |
| ① 远端标签 | 全部历史 + 标签 | — | 网络 + Git LFS | ✅ 已推送成功 |
| ② **本地整目录复制（含 `.git/`）** | 源码 + 全部历史 + **LFS 对象**（`.git/lfs` 约 0.44 MB） | 不含 `Library/Temp/Logs` 约 30 MB（全量约 1.4 GB） | 无 | ✅ 本地克隆演练：索引 442 个文件、工作区 0 改动、`LiberationSans.ttf` 为真实 350 KB 内容（不是 LFS 指针） |
| ③ 离线 bundle（本文件提交时已重新生成） | 全部历史 + 标签，**不含 LFS 对象** | 3.04 MB（SHA256 `46DF7DFFF017A577A2A127AE3DA5ACC998FFA63A19E88EB87720EC15EBF2C417`） | 恢复时需另取 LFS 对象 | ⚠️ 实测：直接 `git clone <bundle>` 会在 LFS smudge 处中断，**索引为空**、工作区不干净；必须先联网 `git lfs fetch --all`（或 `GIT_LFS_SKIP_SMUDGE=1` 先出指针再补） |

离线 bundle 位置：`E:\Unity\Project\CardReborn-backups\CardReborn-2026-10-04-m2-complete.bundle`（**m2 时点产物**，M3 未重新生成；如需 M3 检查点 bundle，重跑下方 checkpoint.ps1 后按第 10.2 节方式生成）

**一键完成第 2、3 步**（打标签 + 推标签 + 生成并校验 bundle）；

```powershell
powershell -ExecutionPolicy Bypass -File Tools/checkpoint.ps1 -Name checkpoint/m3-complete `
  -Note "M3 完成：<填当时的编译/测试/覆盖率结论>"
# 先看计划不落盘：加 -DryRun
```

脚本的几条自我保护：工作区有未提交改动或 `HEAD != origin/<branch>` 只**警告**；标签已存在直接失败（检查点本应不可变）；不会删除、不会强推、不会移动已有标签。

> **重要**：②和③目前都在**同一块硬盘**上。真要防硬件故障，请把它们复制到别处（移动硬盘 / 网盘 / 另一台机器）。

### 10.3 恢复步骤

**路线 A（有网络，最省事）**

```bash
git lfs install                                   # 机器上第一次用 LFS 时需要
git clone https://github.com/Yewatermelon/CardReborn.git
cd CardReborn
git checkout checkpoint/m2-complete               # 回到 M2 完成时的精确状态
```

**路线 B（离线，用本地整目录副本）**

1. 复制整个工程目录（**必须包含 `.git`**；`Library/`、`Temp/`、`Logs/` 可不带，Unity 会重建）；
2. 直接打开该副本开发，或 `git clone <副本路径> <新目录>`（已实测 0 改动）；
3. `git checkout checkpoint/m2-complete`。

**路线 C（只有 bundle 时）**

```bash
git clone <bundle 文件> CardReborn
cd CardReborn
git remote add origin https://github.com/Yewatermelon/CardReborn.git
git lfs fetch --all                               # 补齐 LFS 对象（需网络）
git reset --hard checkpoint/m2-complete           # 重建索引与工作区
```

### 10.4 恢复后必做（确认真的可用）

1. 用 **Unity 2022.3.54f1c1** 打开工程（首次会重建 `Library/`）；
2. 跑第 4 节的三条命令：`Tools/check.ps1`、`Tools/check.ps1 -SelfTest`、`Tools/coverage.ps1`；
3. 确认 `git status` 干净、`git log -1` 与预期提交一致；
4. 读 `Docs/PROGRESS.md`（进度与证据）→ 从 **M3-T1** 继续；
5. **更新本文件第 3 节的快照**（因为进度又往前走了，别让下一任接手人读到旧数字）。

### 10.5 恢复时不需要的东西

| 不需要 | 原因 |
| --- | --- |
| 聊天记录 / 上下文 | 本文件 + `Docs/` + 任务卡已覆盖决策与做法 |
| `Logs/`（`agent-status.json` 等） | gitignore 的临时证据，恢复后重新生成即可 |
| `%TEMP%\CardReborn_CI` | 批处理验证副本，按第 4.2 节重建 |
| `Library/`、`Temp/`、`UserSettings/` | Unity 自动重建（`Assets/TextMesh Pro` 等资源已在 git 里） |

### 10.6 环境三件套（恢复前先确认）

1. **Unity 2022.3.54f1c1**（当前路径 `E:\Unity\2022.3.54f1c1\Editor\Unity.exe`），许可证已激活；
2. **.NET SDK 9.x**（`Tools/coverage.ps1` 需要；首次联网还原 NuGet 包）；
3. **Git + Git LFS**（`git lfs install` 至少执行过一次）。
