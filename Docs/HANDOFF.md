# 交接说明（AI → AI）

> 这份文档写给**接手的 AI 代理**（以及未来的人类维护者）。
> 目标：不依赖任何聊天记录，也能在 30 分钟内搞清楚"项目是什么、现在到哪、下一步做什么、怎么验证、哪里容易踩坑"。
> 维护约定：每次里程碑结束或交接时更新本文件；正文以仓库内文档与代码为准，冲突时以 `Docs/00`–`Docs/06` + `AGENTS.md` 为准。

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
