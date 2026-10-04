# 进度看板

> 维护方式：每个里程碑结束后更新；状态变更需注明日期与证据（测试报告 / 评审记录）。
> 状态图例：⬜ 未开始 ｜ 🟡 进行中 ｜ 🔵 待评审 ｜ ✅ 完成 ｜ 🔴 阻塞

**当前阶段：M1（核心基础层 Core）｜M0 已于 2026-10-03 完成**

| 里程碑 | 状态 | 任务完成/总数 | 门禁 | 备注 |
| --- | --- | --- | --- | --- |
| M0 工程基建与规范落地 | ✅ 完成 | 8/8 | 通过 | 2026-10-03 验证：编译 0 error/0 warning、EditMode/PlayMode 冒烟各 1 通过、空场景出包成功（76.5 MB / 0 error）、`check.ps1` PASS、TMP Essentials 已导入；详见 [评审与复盘](./reviews/M0-工程基建-评审与复盘.md) |
| M1 核心基础层（Core） | 🟡 进行中 | 4/9 | — | M1-T1（Result/Guard）、M1-T2（EventBus）、M1-T3（StateMachine）、M1-T4（RandomProvider ★）完成，累计新增 126 用例全过；M1-R1（Nullable）、M1-R2（覆盖率）按约定在 M1 收尾统一处理 |
| M2 配置与数据管线 | ⬜ 未开始 | 0/7 | — | — |
| M3 领域模型与规则内核 | ⬜ 未开始 | 0/10 | — | 质量门禁最严 |
| M4 回合状态机与效果系统 | ⬜ 未开始 | 0/10 | — | 质量门禁最严 |
| M5 表现层与交互 | ⬜ 未开始 | 0/8 | — | 依赖 TMP Essentials |
| M6 垂直切片打通 | ⬜ 未开始 | 0/5 | — | Demo Gate |
| M7 玩家代理与 AI | ⬜ 未开始 | 0/5 | — | — |
| M8 元游戏 | ⬜ 未开始 | 0/8 | — | 可与 M5–M7 并行 |
| M9 内容扩充与打磨 | ⬜ 未开始 | 0/8 | — | — |
| M10 发布与验收 | ⬜ 未开始 | 0/6 | — | — |

**阶段二：联网 PVP 拓展**

| 里程碑 | 状态 | 任务完成/总数 | 门禁 | 备注 |
| --- | --- | --- | --- | --- |
| M11 权威运行时与状态序列化 | ⬜ 未开始 | 0/8 | — | 阶段二硬门槛：无 Unity 可跑完整对局 |
| M12 本机双进程与直连传输 | ⬜ 未开始 | 0/9 | — | BCL TCP，零新依赖 |
| M13 权威状态同步与裁剪 | ⬜ 未开始 | 0/9 | — | 状态同步核心 |
| M14 双人验证与体验打磨 | ⬜ 未开始 | 0/7 | — | — |

> **阶段三（未来，暂不做）**：专用服务器、公网对战、匹配服务、NAT 穿透与中继、反作弊加固、客户端预测与和解、主机迁移。

---

## M0 任务级状态（已完成 8/8）

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| M0-T1 新建工程并校准基线 | ✅ | `ProjectSettings/ProjectVersion.txt` = `2022.3.54f1c1`；`Packages/manifest.json` 无 URP/HDRP，含 feature.2d / textmeshpro / test-framework |
| M0-T2 拷入文档与 AGENTS.md | ✅ | `Docs/`（含 00–06、模板、PROGRESS）与根 `AGENTS.md` 已入库 |
| M0-T3 建立目录骨架 | ✅ | `Assets/_Project/{0_Core…7_Tests, Art, Audio, Prefabs, Scenes, Config}`、`Config/Excel/`、`Tools/`、`Assets/ThirdParty/`、`Docs/reference/` |
| M0-T4 创建 8 个 asmdef | ✅ | 8 个运行时 asmdef；`0_Core/1_Domain/2_Application/2_Network` 均设 `noEngineReferences: true`（编译期禁止引用 UnityEngine）；Unity 已编译出 `Card.Core/Domain/Application/Network/Infrastructure/Presentation/Bootstrap/Editor.dll` |
| M0-T5 建立测试工程 | ✅ | 2026-10-03 于 Unity 2022.3.54f1c1 实跑：`TestRunner_IsWired_Up` 1 passed/0 failed（`Logs/agent-tests-editmode.json`，09:10:37Z）、`PlayModeRunner_IsWired_Up` 1 passed/0 failed（`Logs/agent-tests.json`，09:10:53Z）；早期 `CS0103` 已修复（`325df66`） |
| M0-T6 初始化 Git 仓库与 .gitignore | ✅ | 首次提交 `cc56f56`（64 文件）；已推送 `origin/main`；Git LFS 已启用（.gitattributes） |
| M0-T7 本地校验脚本 | ✅ | `Tools/check.ps1` 跑通（PASS）；用故意违规探针验证可正确报出 R1/R4 并以退出码 1 失败 |
| M0-T8 开工自检 | ✅ | [06 第 5.5 节](./06-新项目搭建与仓库初始化清单.md)五项全过：① 编译 0 error/0 warning，空场景出包成功（76.5 MB / 0 error，`Logs/agent-build.txt`）② `check.ps1` PASS ③ 运行时程序集 `using UnityEditor` 0 处 ④ 旧类型（`BattleManager`/`CardStore`/`MonoSingleton`）0 处 ⑤ `Docs` + `AGENTS.md` + `PROGRESS.md` 就位；`.meta` 已由 Unity 生成，TMP Essentials 已导入（72 文件） |

### M0 收尾记录（2026-10-03 完成）

1. ✅ Unity 已打开并刷新编译，`.meta` 全部生成，编译 0 error / 0 warning。
2. ✅ `TMP Essential Resources` 已导入（`Assets/TextMesh Pro/`，72 文件）——M5 前置项提前关闭。
3. ✅ `Tools > Card > Agent > Run EditMode Tests` / `Run PlayMode Tests` 均已跑通，各 1 passed / 0 failed。
4. ✅ 已读 `Logs/agent-status.json`（0 编译错误、0 警告）与 `Logs/agent-tests.json`（0 失败），剩余 `.meta` 已提交。

> 备注：Unity 在窗口非前台时不会可靠地重新导入脚本，本轮验证由**临时**的 Editor-only 触发脚本（读 `Logs/agent-run-tests.flag`）驱动，验证后已删除；`Tools > Card > Agent` 菜单入口保持不变。改进项登记为 M0-R2。

### AI 协同开发（Agent Bridge，已内置）

- `Tools/check.ps1`：静态门禁（内核解耦 / 编辑器 API / 禁用查找 / 日志规范 / 文件行数），已验证可拦截故意违规。
- `Assets/_Project/6_Editor/AgentConsoleBridge.cs`：把编译诊断与 Console 报错写入 `Logs/agent-status.json`。
- `Assets/_Project/6_Editor/AgentTestBridge.cs`：跑测试并把结果写入 `Logs/agent-tests.json`。
- 详见 [06 第 7 节](./06-新项目搭建与仓库初始化清单.md)。

---

## M1 任务级状态（进行中）

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| M1-T1 `Result` / `Guard` | ✅ | 任务卡 [tasks/M1-T1-Result-Guard.md](./tasks/M1-T1-Result-Guard.md)（含 AC 对齐、铁律扫描、边界推演、反向审查）；代码 `Assets/_Project/0_Core/{Result,Guard}.cs`；EditMode 61 passed / 0 failed（新增 60 例，含 28 Guard + 16 `Result` + 16 `Result<T>`），`warning CS` 0 处；血状态为 `CS0246`（先测后写）；`check.ps1` PASS；内核独立扫描 Unity 依赖 0 命中 |
| M1-T2 `EventBus` | ✅ | 任务卡 [tasks/M1-T2-EventBus.md](./tasks/M1-T2-EventBus.md)；代码 `Assets/_Project/0_Core/{EventBus,EventDispatchFailure,IEventDispatchFailureSink,PublishReport}.cs`；EditMode 83 passed / 0 failed（新增 22 例），`warning CS` 0 处；红状态为 `CS0246`；`check.ps1` PASS；内核扫描 0 命中。语义已锁：类型安全、订阅顺序、Dispose 幂等、派发中增删订阅、重入发布、异常隔离（含 `IEventDispatchFailureSink` 接缝供 M1-T5 接入） |
| M1-T3 `StateMachine<TState>` | ✅ | 任务卡 [tasks/M1-T3-StateMachine.md](./tasks/M1-T3-StateMachine.md)；代码 `Assets/_Project/0_Core/StateMachine.cs`（含 `IStateHandler<TState>`）；EditMode 104 passed / 0 failed（新增 21 例），`warning CS` 0 处；红状态为 `CS0246`；`check.ps1` PASS。语义已锁：显式注册状态与边、非法转移返回 `Result.Failure` 且状态/回调均不变、回调顺序固定 `Exit(旧)→Enter(新)`、未声明的自转移也被拒绝、回调抛异常向上传播不回滚 |
| M1-T4 `IRandomProvider` ★ | ✅ | 任务卡 [tasks/M1-T4-RandomProvider.md](./tasks/M1-T4-RandomProvider.md)；代码 `Assets/_Project/0_Core/{IRandomProvider,SeededRandomProvider}.cs`（xorshift32 + 拒绝采样 + Fisher–Yates，纯 BCL，不用 `System.Random` 以保证跨运行时复现）；EditMode 127 passed / 0 failed（新增 23 例），`warning CS` 0 处；红状态为 `CS0246`；`check.ps1` PASS。含 [04 案例 3](./04-代码复盘Review规范.md) 回归（同种子两次"洗牌→按索引抽取"顺序一致） |
| M1-T5 `GameLog` ★ | ⬜ | — |
| M1-T6 `ObjectPool<T>` | ⬜ | — |
| M1-T7 `ReactiveValue<T>` | ⬜ | — |
| M1-T8 `IClock` ★ | ⬜ | — |
| M1-T9 内核解耦检查规则 ★ | ⬜ | — |

> M1 门禁：Core 覆盖率 ≥ 90%；★ 项通过"无 Unity 依赖"检查。覆盖率工具尚未接入，登记为 M1-R2（待办）。

---

## 未关闭问题（P0 / P1）

| 编号 | 级别 | 来源 | 描述 | 责任人 | 状态 |
| --- | --- | --- | --- | --- | --- |
| — | — | — | 暂无 | — | — |

## 里程碑复盘记录索引

| 里程碑 | 复盘日期 | 记录位置 | 主要改进项 |
| --- | --- | --- | --- |
| M0 工程基建与规范落地 | 2026-10-03 | [reviews/M0-工程基建-评审与复盘.md](./reviews/M0-工程基建-评审与复盘.md) | 桥接层补"文件触发"入口（M0-R2）；M1 起接入覆盖率与复杂度统计；提交前例行 `git diff ProjectSettings/` |

## 变更日志（文档/架构）

| 日期 | 类型 | 说明 | 影响文档 |
| --- | --- | --- | --- |
| 2026-10-03 | 基线 | 建立 00–04 文档集、模板与 AGENTS.md | 全部 |
| 2026-10-03 | 需求变更 | 联网 PVP 由"明确不做"改为**阶段二拓展功能**，网络结构定为确定性锁步帧同步；据此新增 FR-13（确定性内核）与 FR-14（联网对战）、NFR-11～14、ADR-11～14、铁律 11～12，新增 05 文档与 M11–M14 里程碑 | 00 / 01 / 02 / 03 / 04 / 05 / AGENTS / README |
| 2026-10-03 | 架构变更 | 联网网络结构由**帧同步**改为**服务端权威 + 状态同步**：撤销"逻辑层零浮点/零系统时间/固定遍历顺序/跨平台状态哈希一致"约束，改立"规则三层只依赖 BCL + 状态可序列化/可裁剪/可增量 + 视野裁剪"；新增 ADR-15（服务端为独立 .NET 程序）与 `Card.Server` 目录；M11–M14 任务重写 | 00 / 01 / 02 / 03 / 04 / 05(重写) / AGENTS / README / PROGRESS |
| 2026-10-03 | 架构澄清 | 修正"帧同步不需要服务器"的不准确表述（帧同步省的是权威逻辑服务器，信令/中继/房间仍需要）；明确状态同步**可用房主模式**（一个客户端充当权威宿主），并新增部署拓扑章节与 ADR-15/16（`MatchHost` 为共享库，三种宿主共用），新增 FR-14.11～13、N-15/N-16、M12-T9/T10 | 00 / 01 / 02 / 03 / 05 / PROGRESS |
| 2026-10-03 | 范围收敛 | 阶段二收敛为**单台 PC 上以监听服务器（房主模式）验证双人联网**：只做 `LocalHost` + `PeerHost`；传输改用 BCL `System.Net.Sockets`（零新增依赖）；移除匹配/房间服务、账号、NAT 穿透、中继、反作弊、客户端预测（列入阶段三）；专用服务器仅保留接口。新增 ADR-17、铁律 13、FR-14.12～14、N-17/N-18，重写 05 文档为 3.0 版（新增第 15 节：范围收敛与本机双进程验证方法） | 00 / 01 / 02 / 03 / 04 / 05 / AGENTS / README / PROGRESS |
| 2026-10-03 | 落地准备 | 明确"新建独立 Unity 工程 + 拷文档"的开工路径：新增 06（新项目搭建与仓库初始化清单，含该拷/不该拷清单、工程基线、目录骨架、Git/GitHub 流程与仓库地址该写在哪）；M0 由 7 项调整为 8 项并改为在新工程中执行；取消 `Assets/_Legacy/` 方案，改为旧工程独立保留；Excel 源表移到 `Assets/` 之外的 `Config/Excel/` | 00 / 02 / 03 / 06(新增) / AGENTS / README / PROGRESS |
| 2026-10-03 | 工程落地 | 新工程 `E:\Unity\Project\CardReborn` 建立并完成 M0 主体：目录骨架 + 8 个运行时 asmdef（其中 4 个设 `noEngineReferences: true`）+ 2 个测试程序集与冒烟测试 + `Tools/check.ps1`（已用违规探针验证有效）+ Git 初始化/LFS/首次提交 `cc56f56` 并推送 `origin/main`；旧工程 Docs 副本冻结，**此后以本仓库 `Docs/` 为唯一权威** | 全部（本仓库内） |
| 2026-10-03 | 修复 + 工具 | 修复 PlayMode 冒烟测试 `CS0103`（漏 `using UnityEngine`）；新增 Editor 侧 AI 协同桥接（`AgentConsoleBridge` 输出 `Logs/agent-status.json`，`AgentTestBridge` 输出 `Logs/agent-tests.json`，零第三方依赖），使 AI 可自行验证"编译是否通过、测试是否全绿"；`Card.Editor` 增加 `UnityEditor.TestRunner` 引用；文档 06 增加第 7 节（含 Unity MCP 升级路径与风险说明） | 06 / PROGRESS / Assets |
| 2026-10-03 | M0 完成 | M0 八项任务全部关闭（8/8）：Unity 侧实跑验证通过——编译 0 error/0 warning、EditMode/PlayMode 冒烟各 1 通过、空场景出包成功（`StandaloneWindows64`，76.5 MB / 0 error）、`check.ps1` PASS、运行时程序集零 `using UnityEditor`、零旧类型；导入 TMP Essential Resources（`Assets/TextMesh Pro/`，72 文件，M5 前置）；补齐 Editor 桥接 `.meta` 与 `ProjectSettings/SceneTemplateSettings.json`；新增 [M0 评审与复盘](./reviews/M0-工程基建-评审与复盘.md)，登记改进项 M0-R2（桥接层补文件触发） | PROGRESS / reviews(新增) / Assets/TextMesh Pro(新增) / Assets/_Project/6_Editor / ProjectSettings |
| 2026-10-03 | M1-T1 完成 | `Card.Core` 新增 `Result` / `Result<T>` / `Guard`（纯 BCL，无 Unity 依赖）；新增 60 个 EditMode 用例（先红后绿）；新增任务卡 [tasks/M1-T1-Result-Guard.md](./tasks/M1-T1-Result-Guard.md)；登记 M1-R1（启用 `<Nullable>`，P2）与 M1-R2（接入覆盖率统计）；验证方式改为**工程副本 + Unity 批处理模式**（不占用用户编辑器会话，避免 M0-R2 的"必须人工点菜单"瓶颈） | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-03 | M1-T2 完成 | `Card.Core` 新增 `EventBus` + `EventDispatchFailure` + `IEventDispatchFailureSink` + `PublishReport`（纯 BCL）；锁定派发语义（类型安全、订阅顺序、Dispose 幂等、派发中增删订阅、重入发布、异常隔离不等于解绑）；新增 22 个 EditMode 用例（先红后绿，累计 83 passed / 0 failed）；任务卡 [tasks/M1-T2-EventBus.md](./tasks/M1-T2-EventBus.md) 明确记录"`IEventBus`/`GameEvent` 属 Domain，留待 M3/M4"；用户决策：M1-R1（Nullable）与 M1-R2（覆盖率）推迟到 M1 收尾统一处理 | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-03 | M1-T3 完成 | `Card.Core` 新增 `StateMachine<TState>` + `IStateHandler<TState>`（纯 BCL）：状态与合法转移显式注册、非法转移返回 `Result.Failure`（`ERROR_STATE_UNKNOWN` / `ERROR_STATE_ILLEGAL_TRANSITION`）且不变更状态与回调、回调顺序固定 `Exit(旧)→Enter(新)`、未声明自转移被拒、回调异常向上传播不回滚；新增 21 个 EditMode 用例（先红后绿，累计 104 passed / 0 failed）；任务卡 [tasks/M1-T3-StateMachine.md](./tasks/M1-T3-StateMachine.md)。M4 的 `TurnStateMachine` 将复用本类 | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-04 | M1-T4 完成 | `Card.Core` 新增 `IRandomProvider` + `SeededRandomProvider`（纯 BCL：xorshift32 序列 + 拒绝采样 `NextInt` + Fisher–Yates 洗牌；不用 `System.Random`，保证同种子跨运行时复现；`seed=0` 有兜底；空/单元素洗牌不消耗随机数）；新增 23 个 EditMode 用例（先红后绿，累计 127 passed / 0 failed）；登记 M1-R3（`check.ps1` R1 未剥离注释导致文档注释误报，并入 M1-T9） | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
