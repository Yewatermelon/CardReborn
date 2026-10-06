# 进度看板

> 维护方式：每个里程碑结束后更新；状态变更需注明日期与证据（测试报告 / 评审记录）。
> 状态图例：⬜ 未开始 ｜ 🟡 进行中 ｜ 🔵 待评审 ｜ ✅ 完成 ｜ 🔴 阻塞

**当前阶段：M4（回合状态机与效果系统）🟡 进行中（4/10）｜M0–M3 已完成（M3 评审通过，2026-10-06）**

> **交接**：项目已移交后续 AI/开发者，请先读 [Docs/HANDOFF.md](./HANDOFF.md)（现状快照、验证命令、已知坑、接手准备）。
> 本文件的状态与证据在每次交接前需重新跑验证并更新。

| 里程碑 | 状态 | 任务完成/总数 | 门禁 | 备注 |
| --- | --- | --- | --- | --- |
| M0 工程基建与规范落地 | ✅ 完成 | 8/8 | 通过 | 2026-10-03 验证：编译 0 error/0 warning、EditMode/PlayMode 冒烟各 1 通过、空场景出包成功（76.5 MB / 0 error）、`check.ps1` PASS、TMP Essentials 已导入；详见 [评审与复盘](./reviews/M0-工程基建-评审与复盘.md) |
| M1 核心基础层（Core） | ✅ 完成 | 9/9 | 通过 | 2026-10-04 验证：Unity 干净重编译 0 error/0 warning、228 用例全过、`Card.Core` 行覆盖率 **97.71%**（门禁 ≥ 90%）、`check.ps1 -SelfTest` 通过、内核在无 Unity 的 .NET 进程编译并跑通全部测试；详见 [评审与复盘](./reviews/M1-核心基础层-评审与复盘.md) |
| M2 配置与数据管线 | ✅ 完成 | 7/7 | 通过 | 2026-10-04 验证：37 行卡表（35 启用 + 2 废弃）→ 校验 → 生成物 → 加载 → 建库查询全链路打通；443 用例全过、编译 0 error/0 warning、`check.ps1` PASS、覆盖率 `0_Core 96.51%` / `Domain + App 92.36%`；详见 [M2 评审与复盘](./reviews/M2-配置与数据管线-评审与复盘.md) |
| M3 领域模型与规则内核 | ✅ 完成 | 10/10 | 通过 | 2026-10-06 评审：十项任务全部完成；无 Unity 工具链 588 用例全过、编译 0 error/0 warning、`check.ps1` PASS、覆盖率 `0_Core 96.51%` / `Domain + App 90.65%`；同日 Unity 2022.3.54f1c1 编辑器 Test Runner（EditMode）补验 **608 passed / 0 failed**，M3-B1 解除；三项 ★（种子洗牌/序列化/增量）为联网前置能力；详见 [M3 评审与复盘](./reviews/M3-领域模型与规则内核-评审与复盘.md) |
| M4 回合状态机与效果系统 | 🟡 进行中 | 4/10 | 通过（T1/T2/T3/T4） | 2026-10-06 T4 完成：效果框架 IEffectData/EffectExecutor/EffectContext + PlayCardSettler 端到端，出牌→进场→战吼全链路；无 Unity 679 例、Unity EditMode 待补验；质量门禁最严 |
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
| M1-T5 `GameLog` ★ | ✅ | 任务卡 [tasks/M1-T5-GameLog.md](./tasks/M1-T5-GameLog.md)；代码 `Assets/_Project/0_Core/{LogLevel,LogChannel,ILogSink,GameLog,EventDispatchLogSink}.cs`（纯 BCL，无 Unity 日志 API，也不用 `System.Console`）；`GameLog` 为 03 第 8 节规定的静态门面：分等级（Trace/Info/Warn/Error）、分通道（Boot…Perf，可开关）、`IsEnabled` 供热路径短路、sink 故障不穿透并计数；`EventDispatchLogSink` 闭合 M1-T2 的订阅者异常接缝；EditMode 156 passed / 0 failed（新增 29 例），`warning CS` 0 处；`check.ps1` PASS |
| M1-T6 `ObjectPool<T>` | ✅ | 任务卡 [tasks/M1-T6-ObjectPool.md](./tasks/M1-T6-ObjectPool.md)；代码 `Assets/_Project/0_Core/{IPoolable,ObjectPool}.cs`（纯 BCL）；预热、复用、`IPoolable` 归还清理、闲置上限（超限丢弃）与扩容上限（`Rent` 抛错 / `TryRent` 降级）、重复归还与外来对象立即报错、引用相等比较器保证不受 `Equals` 重写影响、超限首次经 `GameLog.Warn(Perf)` 留痕后只计数；EditMode 185 passed / 0 failed（新增 29 例），`warning CS` 0 处；`check.ps1` PASS |
| M1-T7 `ReactiveValue<T>` | ✅ | 任务卡 [tasks/M1-T7-ReactiveValue.md](./tasks/M1-T7-ReactiveValue.md)；代码 `Assets/_Project/0_Core/ReactiveValue.cs`（纯 BCL）；值真正变化才通知（`EqualityComparer<T>.Default` 判定）、`Set` 返回"是否变化"、订阅返回 `IDisposable` 且解绑幂等、`notifyWithCurrentValue` 供 View 首帧渲染、派发期增删订阅与 `EventBus` 口径一致、派发期再次 `Set` 会中止旧派发（避免 View 收到过期值）、订阅者异常隔离并计数/上报；EditMode 210 passed / 0 failed（新增 25 例），`warning CS` 0 处；`check.ps1` PASS |
| M1-T8 `IClock` ★ | ✅ | 任务卡 [tasks/M1-T8-IClock.md](./tasks/M1-T8-IClock.md)；代码 `Assets/_Project/0_Core/{IClock,ManualClock}.cs`（纯 BCL，签名与 03 §5.9.2 一致）；`Advance` 只允许前进且做溢出保护（不静默回绕）、`SetTo` 仅供测试/重放/恢复（可回拨）、tick 非负；EditMode 228 passed / 0 failed（新增 18 例，含"注入假时钟的消费者确定性到期"），`warning CS` 0 处；`check.ps1` PASS；并按 05 第 688 行如实记录"禁止 Time/DateTime"已部分撤销，不把扫描强制写死 |
| M1-T9 内核解耦检查规则 ★ | ✅ | 任务卡 [tasks/M1-T9-GateRules.md](./tasks/M1-T9-GateRules.md)；`Tools/check.ps1` 重写加固：R1 扩展为完整五条禁令（Unity 类型/日志/序列化/随机/时间 + 系统时间）、新增 **R6 asmdef 守门**（内核必须 `noEngineReferences: true`、不得引用 Unity 程序集、不得向上依赖）、**扫描前剥离注释与字符串 + 词边界匹配**（修掉 M1-R3 的两类误报）；新增 `-SelfTest` 探针自检（R1–R6 逐条断言 + 干净样本零误报）；真实探针验证：故意在 `1_Domain` 放 `using UnityEngine` → 报 4 条违规且退出码 1，移除后恢复 PASS。文档同步：03 §5.9.5（规则清单与实现要求）、06 §7.3（提交前自检流程） |

> M1 门禁：Core 覆盖率 ≥ 90%；★ 项通过"无 Unity 依赖"检查（R1/R6 已由 `check.ps1` 静态保证）。
> 收尾结果：**M1-R1 已关闭**（`Assets/csc.rsp` = `-nullable:enable`，64 条 CS86xx 全部修复，仍 0 warning）；
> **M1-R2 已关闭**（`Tools/coverage.ps1` + `Tools/Coverage` 独立 .NET 工具链，行覆盖率 97.71%）；
> **M1-R3 已关闭**（`check.ps1` 剥离注释/字符串 + 词边界 + `-SelfTest`）。
> 新增遗留：**M1-R4（P3）** 未覆盖行补齐（`PublishReport.ToString` 70%、`StateMachine` 82%、`ObjectPool` 96%、`Result` 95%），M2 期间顺手补。

---

## M2 任务级状态（进行中）

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| M2-T1 定义配置 Schema | ✅ | 任务卡 [tasks/M2-T1-ConfigSchema.md](./tasks/M2-T1-ConfigSchema.md)；代码 `Assets/_Project/1_Domain/Config/*.cs`（5 枚举 + `ConfigTokens` / `KeywordTokens` + 6 个 Definition + `ConfigDocument<T>` / `ConfigSchema`，共 14 文件）；EditMode 262 passed / 0 failed（新增 34 例：`KeywordTokensTests` 15 + `ConfigContractTests` 19），清缓存干净重编译 0 error/0 warning；`check.ps1` PASS；无 Unity 工具链 262/262 通过（`0_Core 97.71%`、`Domain + App 93.94%`） |
| M2-T2 编写配置源表模板（CSV） | ✅ | 任务卡 [tasks/M2-T2-TableTemplates.md](./tasks/M2-T2-TableTemplates.md)；新增 [00 ADR-19](./00-现状解构与架构再设计.md)（源表用 CSV，导入器零新依赖）；`Config/Excel/{Cards,Heroes,HeroPowers,RarityWeights,GachaConfig,Rules}.csv` + `Config/README.md`（填写说明与常见错误）；`Core/CsvTable.cs`（纯 BCL 极简 CSV：BOM/CRLF/空行/行列不齐/列名严格匹配）；EditMode 287 passed / 0 failed（新增 25 例：`CsvTableTests` 14 + `ConfigTemplateTests` 11），0 error/0 warning；无 Unity 工具链 287/287 通过（`0_Core 97.69%`、`Domain + App 93.94%`）；`check.ps1` PASS。模板校验覆盖表头一致性、枚举/关键词/整数/布尔、外键与唯一性 |
| M2-T3 实现导入器 | ✅ | 任务卡 [tasks/M2-T3-ConfigImporter.md](./tasks/M2-T3-ConfigImporter.md)（会话 A + 会话 B）。**会话 A**：`Core/JsonValue`（确定性 JSON 序列化）+ `Domain/Config/{ConfigFormatException,ConfigRowParser,ConfigJsonWriter}`（行→契约、契约→带 `schemaVersion` 的 JSON）。**会话 B**：`Card.Infrastructure/Config/{ConfigFileImporter,ConfigImportResult}`（读 CSV → 校验 → 通过才写 JSON；失败零写出、既有生成物不动；先写 `.tmp` 再替换；生成物首行"勿手改"标记）+ `Card.Editor/ConfigImportMenu`（菜单 `Tools > Card > 导入配置` + 无界面入口 `ImportForAutomation` 供 `-executeMethod` 使用）。**实际产出已入库**：`Assets/_Project/Config/{cards,heroes,hero_powers,rarity_weights,gacha,rules}.json`。新增 41 + 11 = 52 例（累计 370 passed / 0 failed），清缓存 0 error/0 warning，`check.ps1` PASS |
| M2-T4 实现校验器 | ✅ | 任务卡 [tasks/M2-T4-Validators.md](./tasks/M2-T4-Validators.md)；代码 `Assets/_Project/1_Domain/Config/{ConfigSourceSet,ConfigValidationIssue,ConfigValidationReport,ConfigBundle,ConfigValidator,CardConfigValidator,HeroConfigValidator,RuleConfigValidator}.cs`（纯 BCL）；**一次列出全部问题**（表名/行号/列名/原因，`ToText()` 可直接贴日志与对话框）、有错则 `Result` 为 null（保证"不产生半成品数据"）；规则覆盖缺表、行解析、Id/Key 唯一、技能外键、费用与数值范围（含 `Cost ≤ ManaLimit` 跨表）、稀有度四档齐全、单行表约束、禁用卡只校验可解析性；新增 31 例（累计 359 passed / 0 failed），`check.ps1` PASS，`0_Core 97.45%` / `Domain + App 92.29%`；`Config/README.md` 补"导入前校验什么"清单 |
| M2-T5 `CardDatabase` | ✅ | 任务卡 [tasks/M2-T5-CardDatabase.md](./tasks/M2-T5-CardDatabase.md)；`Core/JsonParser` + `JsonValue.Parse`（严格解析：位置化错误、容 BOM、拒小数与重复键）；`Domain/Config/{ConfigReadException,ConfigJsonReader,ConfigLookupException,CardDatabase}`（JSON→契约，失败含文件名+JSON 路径；id/key O(1) 索引、职业/稀有度/系列筛选、缺失给明确错误、重复 id/key 建库即抛）与 `Infrastructure/Config/{ConfigFileLoader,ConfigLoadResult}`（读 6 份生成物，损坏/缺字段汇成错误列表）；新增 57 例（累计 427 passed / 0 failed），含"导入 → 读回 → 查询"闭环与直接加载仓库生成物；`check.ps1` PASS；`0_Core 96.51%` / `Domain + App 93.05%`。覆盖率门禁在此任务中拦下过一次 Domain 76.32%（读取器单测被放到被排除的 Infrastructure），补测后恢复 |
| M2-T6 热加载 | ✅ | 任务卡 [tasks/M2-T6-HotReload.md](./tasks/M2-T6-HotReload.md)；`Domain/Config/ConfigService`（`Current` 永不为 null；`TryReload()` **成功才换库**、失败保留旧库并记录报告；`Version` 供 UI 判断刷新；加载委托注入，故留在 Domain 可被工具链与服务端复用）；`ConfigLoadResult` 从 Infrastructure 移到 Domain（纯数据结果）；`Card.Editor` 新增菜单 `Tools > Card > 重载配置` + 无界面入口 `ReloadForAutomation`（加载失败弹窗列全部问题，成功给出卡牌/英雄/每包摘要）；新增 8 例（累计 435 passed / 0 failed）；`check.ps1` PASS；`0_Core 96.51%` / `Domain + App 92.36%` |
| M2-T7 首版配置数据 | ✅ | 任务卡 [tasks/M2-T7-FirstContent.md](./tasks/M2-T7-FirstContent.md)；`Config/Excel/Cards.csv` 扩到 37 行（35 启用：随从 23 + 法术 12；稀有度四档齐全；覆盖 5 个必备关键词与 8 种效果组件；含 2 张 `Enabled=FALSE` 的废弃卡）；`Config/README.md` 补效果串约定（`效果名[:参数/参数]`，多效果用 `\|`，复合用 `CompositeEffect:子效果+子效果`）；新增 `ConfigContentTests` 8 例把"内容达标"变成门禁（卡牌/启用数 ≥30、随从 ≥20 与法术 ≥10、四档稀有度、5 关键词、8 效果、英雄技能可查、职业池 ≥5 张、废弃卡不进启用池）；生成物重新导入入库（`cards.json` 17.4 KB）；累计 443 passed / 0 failed |

> M2 门禁：`改 Excel → 导入 → 新卡出现在卡池 → 无需改代码` 全链路演示成功。
> 覆盖率门禁（M1-R2 扩展）：`0_Core` 行覆盖率 ≥ 90%、`Domain + Application` ≥ 80%（NFR-4），由 `Tools/coverage.ps1` 强制。
> **收尾结果**：全链路已打通并入库（37 行卡表 → 校验 → 生成物 → 加载 → 卡池查询；热加载成功才换库、失败保旧）；内容目标由 `ConfigContentTests` 锁定。
> **遗留（P3）**：M2-R1（`CsvTable` 支持 `#` 注释行）、M2-R3（生成物 schema 自动迁移）、M1-R4（补齐未覆盖行）。

---

## M3 任务级状态（进行中 5/10）

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| M3-T1 对局状态模型 | ✅ | 任务卡 [tasks/M3-T1-MatchState.md](./tasks/M3-T1-MatchState.md)（含 AC 对齐、铁律扫描、边界推演、反向审查与评审结论）；代码 `Assets/_Project/1_Domain/Match/{TurnPhase,ManaPool,HeroState,PlayerState,MatchState}.cs`（纯 C#，命名空间 `Card.Domain.Match`）；先红（CS0234/CS0246）后绿，新增 24 例（ManaPoolTests 12 + MatchStateModelTests 12）。Unity 2022.3.54f1c1 批处理 **467 passed / 0 failed**、编译 0 error/0 warning；无 Unity 工具链 **447 passed / 0 failed**；ManaPool/MatchState/PlayerState 覆盖率 100%、HeroState 88%；汇总 0_Core 96.51% / Domain + App 92.65%；新增 9 个 .meta，全仓 206 GUID 无重复；`check.ps1` PASS |
| M3-T2 卡牌实例与分区 | ✅ | 任务卡 [tasks/M3-T2-CardInstance-Zone.md](./tasks/M3-T2-CardInstance-Zone.md)；代码 `1_Domain/Match/{ZoneType,CardInstance,Zone}.cs`（新增）+ `PlayerState.cs` 接入四分区（容量取自 RulesConfig；牌库/坟场不限）；先红（CS0246/CS1729）后绿，新增 20 例（CardInstanceTests 5 + ZoneMovementTests 15）。Unity 批处理 **487 passed / 0 failed**、最终编译 0 error/0 warning（首次导入前一过性 CS0246 已在任务卡 §10.6 核实时序）；无 Unity 工具链 **467 passed / 0 failed**；CardInstance/PlayerState 100%、Zone 97%；汇总 0_Core 96.51% / Domain + App 93.04%；新增 6 .meta，全仓 212 GUID 无重复；`check.ps1` PASS |
| M3-T3 关键词与状态集合 | ✅ | 任务卡 [tasks/M3-T3-Keyword-Status.md](./tasks/M3-T3-Keyword-Status.md)；代码 `1_Domain/Match/{StatusFlags,KeywordSet,StatusSet}.cs`（新增）+ `CardInstance` 接入 Keywords/Statuses（工厂按定义初始化关键词，含圣盾关键词预置可消耗状态）；先红后绿，新增 24 例（KeywordSetTests + StatusSetTests）。**Unity 批处理验证暂缓**（M3-B1：TRAE 沙箱拦截 bee/upm 致 `isUpdating` 恒真、`EditorApplication.update` 不执行，多轮未解决，经用户同意跳过该步）；无 Unity 工具链 **491 passed / 0 failed**；KeywordSet/StatusSet/CardInstance 覆盖率 **100%**；汇总 0_Core 96.51% / Domain + App 93.41%；新增 5 .meta；`check.ps1` PASS（132 文件） |
| M3-T4 命令与结果模型 | ✅ | 任务卡 [tasks/M3-T4-Command-Result.md](./tasks/M3-T4-Command-Result.md)；代码 `1_Domain/Match/{IGameCommand,TargetRef,GameCommands,CommandResult}.cs`（新增 4 文件：接口 + 目标引用 + 命令类型 + 结果与 CommandError）；先红（CS0246/CS0103）后绿，新增 20 例。Unity 批处理验证同样受 **M3-B1** 阻塞暂缓（.meta 按 Unity 标准格式手写、GUID 唯一，非 Unity 生成）；无 Unity 工具链 **511 passed / 0 failed**；TargetRef/GameCommands/CommandResult 覆盖率 **100%**；汇总 0_Core 96.51% / Domain + App 93.71%；新增 6 .meta，Assets 223 GUID 无重复；`check.ps1` PASS（138 文件）。关键决策：命令引用卡牌/角色用 InstanceId/座位 Id 而非集合索引（任务卡 §5.1） |
| M3-T5 开局初始化 ★ | ✅ | 任务卡 [tasks/M3-T5-Match-Setup.md](./tasks/M3-T5-Match-Setup.md)；代码 `2_Application/Match/{MatchSetupRequest,MatchFactory}.cs`（新增）+ 配置链路追加 4 Rules 字段与幸运币卡（RulesConfig/Parser/Writer/Reader/校验器 + Rules.csv/Cards.csv + 生成物）；先红（CS0234/CS0246）后绿，新增 14 例。Unity 批处理验证受 **M3-B1** 阻塞暂缓（5 个新 .meta 手写，含 Application/Match 新文件夹，GUID 经 228 个 meta 去重）；无 Unity 工具链 **525 passed / 0 failed**；MatchFactory/MatchSetupRequest 覆盖率 **100%**；汇总 0_Core 96.51% / Domain + App 92.84%；`check.ps1` PASS（142 文件）。关键约定：随机序固定（双方洗牌→掷先手）、InstanceId 分段、末位=牌库顶、TheCoinCardKey 缺列=null 跳过外键但真实 CSV 强制（任务卡 §10-4） |
| M3-T6 `RuleEngine.Validate` | ✅ | 任务卡 [tasks/M3-T6-RuleEngine-Validate.md](./tasks/M3-T6-RuleEngine-Validate.md)；代码 `2_Application/Match/{RuleEngine,RuleEngine.Attack,RuleEngine.HeroPower,RuleEngine.Target}.cs`（partial 拆分，主文件 105 行）+ `1_Domain/Match/CardInstance.cs` 补 `AttacksUsedThisTurn`；测试拆三文件（PlayCard/Attack/HeroPower+EndTurn）+ 共享 helper。先红（CS0234）后绿，新增 21 例（出牌 9 + 攻击 7 + 技能/结束回合 5）。Unity 批处理验证受 **M3-B1** 阻塞暂缓（7 个新 .meta 手写，全仓 GUID 无重复）；无 Unity 工具链 **546 passed / 0 failed**；RuleEngine 各 partial 覆盖率 85%–100%；汇总 0_Core 96.51% / Domain + App 89.77%；`check.ps1` PASS。关键决策：阶段不符复用 `NotYourTurn`；风怒已支持（关键词检查 2 次）；潜行目标校验未做（P1，任务卡 §4） |
| M3-T7 `MatchEvaluator` 胜负判定 | ✅ | 任务卡 [tasks/M3-T7-Match-Evaluator.md](./tasks/M3-T7-Match-Evaluator.md)；代码 `1_Domain/Match/{MatchResult,MatchOutcome}.cs` + `2_Application/Match/MatchEvaluator.cs` + `PlayerState.FatigueCounter` + `MatchState.IsFinished` + `RuleEngine` 终局检查；`HeroState` 构造器下限从 1 放宽到 0（运行期允许归零）。先红（CS0246/CS0234）后绿，新增 9 例。无 Unity 工具链 **555 passed / 0 failed**；MatchEvaluator 100% 覆盖；汇总 0_Core 96.51% / Domain + App 89.88%；`check.ps1` PASS |
| M3-T8 疲劳与爆牌 | ✅ | 任务卡 [tasks/M3-T8-Fatigue-Overdraw.md](./tasks/M3-T8-Fatigue-Overdraw.md)；代码 `1_Domain/Match/DrawOutcome.cs`（纯数据：入手/爆牌实例、疲劳伤害、计数、致死）+ `2_Application/Match/CardDrawService.cs`（逐张结算：顶牌入手/手牌满爆牌入坟场/空库 `FatigueCounter+1` 并扣等量生命）。先红（CS0246/CS0234）后绿，新增 11 例（疲劳 5 + 抽牌 1 + 爆牌 3 + 参数 2）。无 Unity 工具链 **566 passed / 0 failed**；DrawOutcome/CardDrawService **100%** 覆盖；汇总 0_Core 96.51% / Domain + App 90.13%；`check.ps1` PASS；3 个新 .meta，243 GUID 无重复。关键决策：爆牌 Deck→Graveyard（不入手、不触发亡语）；疲劳直接扣生命（护甲 M4 伤害系统处理）；Docs/02 验收 1/2/3 已锁 |
| M3-T9 MatchState 可序列化 ★ | ✅ | 任务卡 [tasks/M3-T9-State-Serialization.md](./tasks/M3-T9-State-Serialization.md)；代码 `2_Application/Match/MatchStateSerializer{,.Write,.Read}.cs`（partial，version 1，枚举名/flags 整数，不冗余存 currentZone）+ `1_Domain/Match/CardInstance.Restore(...)` 反序列化工厂。先红（CS0234/CS0246）后绿，新增 10 例（丰富往返/顺序/确定性/空分区/分区还原/终局/Factory 对局+RuleEngine 一致/非法 JSON/版本不符/对象树独立）。无 Unity 工具链 **576 passed / 0 failed / 0 warning**；Serializer 入口/写入 100%、读取 86%；汇总 0_Core 96.51% / Domain + App 90.29%；`check.ps1` PASS；5 个新 .meta，248 GUID 无重复。关键决策：全量快照（裁剪留阶段二）；还原对象树零共享；NUnit Throws 精确类型匹配改 try/catch 断言 |
| M3-T10 状态增量计算 ★ | ✅ | 任务卡 [tasks/M3-T10-State-Diff.md](./tasks/M3-T10-State-Diff.md)；代码 `1_Domain/Match/StateChange.cs`（ChangeKind + Path + JsonValue 新旧值）+ `2_Application/Match/MatchStateDiffer.cs`（根字段/玩家字段/分区成员增删/卡牌字段逐项 diff，路径键与序列化器一致，顺序确定）。先红（CS0246）后绿，新增 12 例（相同空列表/单字段各 1 条/英雄三路径/法力两路径/分区增删/卡牌四字段/顺序确定/参数 null）。无 Unity 工具链 **588 passed / 0 failed / 0 warning**；StateChange/MatchStateDiffer **100%** 覆盖；汇总 0_Core 96.51% / Domain + App 90.65%；`check.ps1` PASS；3 个新 .meta，251 GUID 无重复。关键决策：分区按实例 Id 集合比较（牌库顺序不算变更，FR-14.5）；跨区移动 = 旧区 Removed + 新区 Added；M3 全部 10 任务完成，待整体评审 |

> M3 门禁：`RuleEngine` + 状态模型单测覆盖 ≥ 85%，含全部边界场景；不写任何 UI 代码；★ 项在无 Unity 环境下通过。

---

## M4 任务级状态（进行中 4/10）

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| M4-T1 `TurnStateMachine` | ✅ | 任务卡 [tasks/M4-T1-TurnStateMachine.md](./tasks/M4-T1-TurnStateMachine.md)（含 AC 对齐、铁律扫描、边界推演、反向审查与评审结论）；代码 `2_Application/Match/TurnStateMachine.cs`（封装 M1-T3 `StateMachine<TurnPhase>`，声明 Docs/01 §3.2 六阶段边：顺序 `MatchStart→TurnStart→Draw→Main→TurnEnd→TurnStart` 回环 + 任意非终局阶段→`MatchEnd`、MatchEnd 无出边；成功转移才写回 `MatchState.Phase`，不含阶段业务）；先红（CS0246 × 32）后绿，新增 26 例。无 Unity 工具链 **614 passed / 0 failed / 0 warning**，`TurnStateMachine.cs` 覆盖率 **100%**（57/57），汇总 0_Core 96.51% / Domain + App 90.86%；Unity 编辑器 Test Runner（EditMode）用户实跑补验 **634 passed / 0 failed**；`check.ps1` PASS（167 文件）；2 个新 .meta，全仓 253 GUID 无重复。关键边界：阶段业务（水晶/抽牌/行动方切换）明确留到 M4-T2/T8，AC-12 测试锁定零副作用 |
| M4-T2 `MatchController` ★ | ✅ | 任务卡 [tasks/M4-T2-MatchController.md](./tasks/M4-T2-MatchController.md)（含 AC 对齐、铁律扫描、边界推演、反向审查、Q1–Q4 范围决策）；代码 `2_Application/Match/`：`MatchController`（FIFO 命令队列：RuleEngine 校验→ICommandSettler 结算→MatchEvaluator 终局判定单入口→MatchStepRecord 台账）、`EndTurnSettler`（Main→TurnEnd→切行动方/回合+1→TurnStart 簿记：ManaPool.BeginTurn+技能/攻击次数/召唤失调清零→Draw 抽 1→Main）、`ICommandSettler`+`SettlementContext` 接缝（出牌/攻击/技能留 M4-T4/T6，缺失即抛装配异常）、`MatchStepRecord` 步骤台账（非 T3 GameEvent）；先红 CS0246 后绿，新增 19 例。无 Unity **633 passed / 0 failed / 0 warning**，`MatchController` 覆盖 97%（3 行不可达防御），汇总 0_Core 96.51% / Domain+App 91.20%；Unity EditMode 补验 **653 passed / 0 failed**；`check.ps1` PASS（175 文件）；8 个新 .meta，261 GUID 无重复；M3 生产代码零改动。完整疲劳脚本（真实配置同种子）：67 步终局于回合 68，后手第 8 次疲劳 −6 死亡，两局状态+台账全等；覆盖 §4.2 边界 1/2/10 |
| M4-T3 `GameEvent` 事件模型 | ✅ | 任务卡 [tasks/M4-T3-GameEvents.md](./tasks/M4-T3-GameEvents.md)；Domain 新增 `GameEvents.cs`（`GameEvent` 基类 + 12 派生：PhaseChanged/TurnStarted/TurnEnded/CardDrawn/CardBurned/Fatigue/Damage/Healing/CardDeath/CardPlayed/AttackDeclared/MatchEnded + `IEventSink` + `EventLog` 单调序号）；Application 增量接线：`SettlementContext` 加 `Events`、`MatchController` 持有 `EventLog` 并暴露 `Events`、终局 emit `MatchEndedEvent`、`EndTurnSettler` 在各阶段/抽牌/爆牌/疲劳产出事件；事件与 `MatchStepRecord` 台账职责分离，拒绝命令零事件。先红 CS0246 后绿，新增 20 例（家族 12 + 事件流 8：单步 7 事件固定序列、序号单调、拒绝零事件、疲劳递增、完整脚本末尾 MatchEnded）。无 Unity **653 passed / 0 failed / 0 warning**，GameEvent/EventLog 覆盖 100%、Domain+App 91.70%；Unity EditMode 补验 **673 passed / 0 failed**；`check.ps1` PASS（178 文件）；3 个新 .meta，264 GUID 无重复；T2 既有 19 例零改动
| M4-T4 效果框架 | ✅ | 任务卡 [tasks/M4-T4-EffectFramework.md](./tasks/M4-T4-EffectFramework.md)；Domain 新增 `GameEffects.cs`（`IEffectData` + 5 效果数据：Damage/Heal/DrawCard/Summon/Buff + `EffectParser`）；Application 新增 `Effects.cs`（`EffectContext` + `IEffectExecutor` + `EffectExecutor` 类型字典分发 + 5 执行器）、`PlayCardSettler.cs`（消费法力→进场/入坟场→战吼效果→CardPlayedEvent）；Domain 改 `HeroState`（+TakeDamage 护甲先抵/+Heal 不超上限）、`MatchState`（+AllocateInstanceId）；Application 改 `MatchFactory`/`MatchController`。先红后绿，新增 26 例；无 Unity **679 passed / 0 failed**，Domain+App 91.25%；`check.ps1` PASS（185 文件）；7 个新 .meta，277 GUID 无重复。T2 接缝测试改为验证出牌成功 |

> M4 门禁：可用脚本驱动一整局（无 UI）并输出事件日志；覆盖率 ≥ 85%；进程内"客户端 ↔ 服务器"模拟可完整跑完一局（M4-T10 ★）。

---

## 未关闭问题（P0 / P1）

| 编号 | 级别 | 来源 | 描述 | 责任人 | 状态 |
| --- | --- | --- | --- | --- | --- |
| M3-B1 | P2（已解除 2026-10-06） | M3-T3～T10 | Unity 批处理 EditMode 验证在 TRAE 环境失效：沙箱拦截 `bee\trash`/`upm.log` 等，`EditorApplication.isUpdating` 恒真、主循环 `update` 不执行，`-runTests` 被静默跳过（exit 0、无结果文件）。T3 起各任务以无 Unity 工具链（588/588）为验收口径；**2026-10-06 用户在编辑器 Test Runner（EditMode）手动补验 608 passed / 0 failed，.meta 导入与程序集编译实跑无误，阻塞解除** | AI | ✅ 已解除 |

## 里程碑复盘记录索引

| 里程碑 | 复盘日期 | 记录位置 | 主要改进项 |
| --- | --- | --- | --- |
| M0 工程基建与规范落地 | 2026-10-03 | [reviews/M0-工程基建-评审与复盘.md](./reviews/M0-工程基建-评审与复盘.md) | 桥接层补"文件触发"入口（M0-R2）；M1 起接入覆盖率与复杂度统计；提交前例行 `git diff ProjectSettings/` |
| M1 核心基础层（Core） | 2026-10-04 | [reviews/M1-核心基础层-评审与复盘.md](./reviews/M1-核心基础层-评审与复盘.md) | 覆盖率工具纳入常规验证；构建配置改动必须"清缓存干净验证"；补齐未覆盖行（M1-R4） |
| M2 配置与数据管线 | 2026-10-04 | [reviews/M2-配置与数据管线-评审与复盘.md](./reviews/M2-配置与数据管线-评审与复盘.md) | 测试按"被测代码所在层"组织；内容规模断言集中到 `ConfigContentTests`；`.gitignore` 区分手工与生成工程 |
| M3 领域模型与规则内核 | 2026-10-06 | [reviews/M3-领域模型与规则内核-评审与复盘.md](./reviews/M3-领域模型与规则内核-评审与复盘.md) | 无 Unity 工具链确立为权威验收口径（M3-B1 挂 P2）；缺列回落默认值不得触发外键校验；NUnit `Assert.Throws<T>` 精确类型匹配改 try/catch；写调用前先读目标签名 |

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
| 2026-10-04 | M1-T5 完成 | `Card.Core` 新增日志抽象与门面：`LogLevel` / `LogChannel`（Boot…Perf）/ `ILogSink` / 静态门面 `GameLog`（分等级过滤、按通道开关、`IsEnabled` 热路径短路、未配置时静默、sink 故障不穿透并计数、消息契约校验）/ `EventDispatchLogSink`（把 M1-T2 的订阅者异常接到 `GameLog.Error`）；新增 29 个 EditMode 用例（先红后绿，累计 156 passed / 0 failed）；测试文件初版 306 行超出 300 行上限，已拆分为 `GameLogTests` + `GameLogFilterTests`（R5 门禁拦截后修复） | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-04 | M1-T6 完成 | `Card.Core` 新增 `IPoolable` + `ObjectPool<T>`（纯 BCL）：预热、复用、归还清理、闲置上限（超限丢弃并计数）与扩容上限（`Rent` 抛异常 / `TryRent` 返回 false，`RejectedCount` 计数）、重复归还与归还外来对象立即抛错、`ReferenceComparer` 保证引用相等判定不受 `Equals` 重写影响、超限首次经 `GameLog.Warn(LogChannel.Perf, …)` 留痕后仅计数（避免热路径刷屏）；新增 29 个 EditMode 用例（先红后绿，累计 185 passed / 0 failed） | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-04 | M1-T7 完成 | `Card.Core` 新增 `ReactiveValue<T>`（纯 BCL 可观察标量值）：相同值不通知、`Set` 返回是否变化、订阅 `IDisposable` 幂等解绑、可选 `notifyWithCurrentValue` 立即推送、派发期新增/解绑订阅与 `EventBus` 一致、**派发期再次 `Set` 会中止旧派发避免过期值**、订阅者异常隔离（计数 + 可选上报）；新增 25 个 EditMode 用例（先红后绿，累计 210 passed / 0 failed） | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-04 | M1-T8 完成 | `Card.Core` 新增 `IClock`（签名与 03 §5.9.2 一致）+ `ManualClock`（纯 BCL）：tick 非负、`Advance` 只前进并做 int 溢出保护、`SetTo` 仅供测试/重放/恢复（允许回拨）；新增 18 个 EditMode 用例（先红后绿，累计 228 passed / 0 failed），含"注入假时钟的消费者按 tick 精确到期且可重复"；同时记录扫描器精度问题（粗糙正则把 `Runtime.CompilerServices` 误判为 Unity 时间）并入 M1-R3 | PROGRESS / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/7_Tests |
| 2026-10-04 | M1-T9 完成 | `Tools/check.ps1` 加固为内核解耦门禁：R1 覆盖 03 §5.9.1 五条禁令（Unity 类型/日志/序列化/随机/时间 + 系统时间）、新增 R6 asmdef 守门（内核 `noEngineReferences`、禁 Unity 程序集引用、禁向上依赖方向）、扫描前**剥离注释与字符串**并改用**词边界**（M1-R3 关闭）；新增 `-SelfTest` 探针自检（含"注释提到禁用 API 不得误报"的回归样本）；修复"违规时退出码为 0"的 bug（`Format-Table` 输出污染函数返回值 → 改 `Out-Host`）；真实探针：`1_Domain` 注入 `using UnityEngine` → 4 条违规 + exit 1，移除后 PASS | PROGRESS / Tools / Docs/03 §5.9.5 / Docs/06 §7.3 |
| 2026-10-04 | M1 收尾（R1/R2） | **M1-R1**：`Assets/csc.rsp` 开启 `-nullable:enable`，修复 64 条 CS86xx（Core 的可空字段/参数、编辑器桥 DTO、测试中"故意传 null"改用 `null!`），仍保持 0 warning；记录 Unity `csc.rsp` **不支持 `#` 注释**的限制（曾导致 200+ 编译错误）。**M1-R2**：新增 `Tools/Coverage`（内核库 + 测试工程，链接同一份源码）+ `Tools/coverage.ps1`，在 Unity 工程外用 .NET 9 + NUnit + coverlet 跑通 228 个测试并测出 `Card.Core` 行覆盖率 **97.71%**（分支 88.13%），顺带得到 FR-13.2/13.3 的直接证据；新增 [00 ADR-18](./00-现状解构与架构再设计.md)。**M1-R3 关闭**。新增 **M1-R4（P3）** | PROGRESS / 00(ADR-18) / 03(§4/§10/§5.9.5) / Tools / Assets/csc.rsp / Assets/_Project |
| 2026-10-04 | M1 完成 | M1 九项任务全部关闭并通过门禁：Unity 干净重编译 0 error/0 warning、228 用例全过、`Card.Core` 行覆盖率 97.71%（门禁 90%）、`check.ps1 -SelfTest` 通过、内核在无 Unity 进程编译并跑通测试；新增 [M1 评审与复盘](./reviews/M1-核心基础层-评审与复盘.md)（含 3 个工具侧问题的 5 Why 与改进项） | PROGRESS / reviews(新增) |
| 2026-10-04 | M2-T1 完成 | `Card.Domain.Config` 新增配置契约（14 文件）：`CardType/CardRarity/CardClass/TargetRule/Keyword`（12 关键词 flags）、`ConfigTokens`（严格枚举解析：拒数字/拒未定义/大小写不敏感）、`KeywordTokens`（`Taunt\|Charge` ⇄ flags，含稳定格式化与失败 token 上报）、`CardDefinition/HeroDefinition/HeroPowerDefinition/GachaConfig/RarityWeight/RulesConfig`、`ConfigDocument<T>` + `ConfigSchema`（`schemaVersion` 与文件名常量）；新增 34 个 EditMode 用例（累计 262 passed / 0 failed）；`Tools/Coverage` 内核库纳入 `1_Domain`，覆盖率门禁扩展为 **Core ≥ 90% + Domain/App ≥ 80%（NFR-4）**，当前 `0_Core 97.71%` / `Domain + App 93.94%`。记录两个坑：Unity 缺 `IsExternalInit`（用条件编译 shim 解决）、Unity 自带旧 NUnit 对数组不支持 `Has.Count` | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/0_Core / Assets/_Project/7_Tests / Tools |
| 2026-10-04 | M2-T2 完成 | 配置源表改为一等公民的 **CSV**（[ADR-19](./00-现状解构与架构再设计.md)，用户选定方案 A：导入器零新依赖）：`Config/Excel/` 下 6 张表（Cards / Heroes / HeroPowers / RarityWeights / GachaConfig / Rules）+ `Config/README.md`（列说明、枚举取值、铁律、常见错误示例）；`Card.Core` 新增 `CsvTable`（BOM/CRLF/空行/行列不齐/列名大小写严格匹配，错误带行号）；新增 25 个 EditMode 用例（累计 287 passed / 0 failed），模板校验覆盖表头一致性、枚举与关键词解析、整数/布尔、外键（HeroPowerKey）与 Id/Key 唯一性；测试定位仓库根目录不使用 `Application.dataPath`，因此无 Unity 工具链同样 287/287 通过 | PROGRESS / 00(ADR-19) / Config(新增 CSV+README) / Assets/_Project/0_Core / Assets/_Project/7_Tests / tasks(新增) |
| 2026-10-04 | M2-T3 会话 A | 实测确认 **Unity 2022.3 不带 `System.Text.Json`**（探针 CS0234），故选 03 §5.9.4 允许的"自定义 BCL 实现"：`Core/JsonValue`（构造 + 确定性序列化：2 空格缩进、LF、成员顺序固定、中文不转义、控制字符 `\uXXXX`）+ `Domain/Config/ConfigRowParser`（CSV 行 → 契约，失败抛 `ConfigFormatException` 且含表名/行号/列名；`CsvTable` 新增真实行号）+ `Domain/Config/ConfigJsonWriter`（契约 → 带 `schemaVersion` 的 JSON，camelCase 字段、枚举写名字、关键词沿用 `Taunt|Charge` 文本）；新增 41 个用例（累计 328 passed / 0 failed），含 JSON 逐字符 golden 与真实模板端到端；新增 [00 ADR-20](./00-现状解构与架构再设计.md)（生成物只做 JSON，SO 预览推迟，避免双 schema 漂移）；会话 B（编辑器菜单 + 落盘）待做 | PROGRESS / 00(ADR-20) / tasks / Assets/_Project/0_Core / Assets/_Project/1_Domain / Assets/_Project/7_Tests |
| 2026-10-04 | M2-T4 完成 | 配置校验器：`ConfigSourceSet`（缺表即错误）、`ConfigValidationIssue` / `ConfigValidationReport`（一次列出全部问题、有错则不给结果）、`ConfigBundle`（成功时的已解析契约集合）、`ConfigValidator` + `CardConfigValidator` / `HeroConfigValidator` / `RuleConfigValidator`（规则拆表）；覆盖缺表/行解析/Id·Key 唯一/技能外键/费用与数值范围（含跨表 `Cost ≤ ManaLimit`）/稀有度四档/单行表/禁用卡只校验可解析性；新增 31 例（累计 359 passed / 0 failed）；`Config/README.md` 增加"导入前校验什么"。**副产品**：无 Unity 工具链（新 Roslyn + 完整 NRT 注解）报出 6 类 Unity 侧看不见的可空性问题（`Equals(object?)`、`Dictionary` 键 `notnull`、`IEqualityComparer<T>.Equals(T?,T?)`、`out object?`），已全部修复；门禁同时抓出 `ConfigValidator.cs` 428 行 + 3 个方法超 50 行、`JsonValue.cs` 310 行，已拆分为 4 个校验器文件 + `JsonText` | PROGRESS / Config/README / Assets/_Project/0_Core / Assets/_Project/1_Domain / Assets/_Project/7_Tests / tasks(新增) |
| 2026-10-04 | M2-T3 会话 B | 导入器落盘完成：`Card.Infrastructure/Config/ConfigFileImporter`（读 `Config/Excel/*.csv` → 校验 → 通过才写 `Assets/_Project/Config/*.json`；**校验失败零写出**且不创建输出目录、既有生成物保持原样；每个文件先写 `.tmp` 再替换；UTF-8 无 BOM；首行 `_generated` 勿手改标记）+ `ConfigImportResult`（成功与否、报告、写出列表、`ToText()`）+ `Card.Editor/ConfigImportMenu`（菜单 + **无界面入口** `ImportForAutomation`，可用 `-executeMethod` 在 CI 调用）；新增 11 例（累计 370 passed / 0 failed）；用无界面入口在工程副本上实跑并入库 6 个生成物；`7_Tests/EditMode/Infrastructure/**` 按设计排除出无 Unity 工具链（Infrastructure 允许用 Unity），并在 harness README 说明 | PROGRESS / Config/README / Tools/Coverage / Assets/_Project/3_Infrastructure / Assets/_Project/6_Editor / Assets/_Project/7_Tests / Assets/_Project/Config(生成物入库) |
| 2026-10-04 | M2-T5 完成 | 运行时链路打通：`Core/JsonParser` + `JsonValue.Parse`（严格解析 + 位置化错误；拒小数/重复键/尾随逗号；容 BOM）、`Domain/Config/ConfigJsonReader`（JSON→契约，失败抛 `ConfigReadException` 含文件名与 `cards[2].cost` 这类 JSON 路径；schema 版本不匹配直接失败，不做自动迁移）、`Domain/Config/CardDatabase`（id/key O(1)、职业/稀有度/系列筛选、`Require*` 缺失给明确错误、重复 id/key 建库即抛）、`Infrastructure/Config/ConfigFileLoader` + `ConfigLoadResult`（读盘并把缺文件/损坏/缺字段汇成错误列表，不穿透异常）；新增 57 例（累计 427 passed / 0 failed），含"写出 → 读回 → 建库查询"闭环与直接加载仓库已入库生成物；[00 §4.2](./00-现状解构与架构再设计.md) 补注 CardDatabase 的拆分（纯查询 Domain / 读盘 Infrastructure）；覆盖率门禁拦下过一次 Domain 76.32%（读取器单测放错层），补 `ConfigJsonReaderTests` 后恢复 93.05% | PROGRESS / 00 / tasks(新增) / Assets/_Project/0_Core / Assets/_Project/1_Domain / Assets/_Project/3_Infrastructure / Assets/_Project/7_Tests |
| 2026-10-04 | M2-T6 完成 | 热加载：`Domain/Config/ConfigService`（构造即持有配置、`Current` 永不为 null；`TryReload()` 成功才换库并 `Version++`，失败保留旧库、记录 `LastReload`，绝不因坏配置清空卡池；加载委托由外部注入）；`ConfigLoadResult` 移入 Domain；`Card.Editor` 菜单 `Tools > Card > 重载配置`（+ `ReloadForAutomation` 无界面入口，成功摘要为"卡牌 N（启用 M）/英雄 H/每包 P"，失败弹窗列全部问题）；新增 8 例（累计 435 passed / 0 failed）；`Config/README.md` 补"导入 / 重载"两步说明 | PROGRESS / Config/README / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/3_Infrastructure / Assets/_Project/6_Editor / Assets/_Project/7_Tests |
| 2026-10-04 | M2-T7 完成 | 首版内容入库：`Cards.csv` 37 行（35 启用 = 随从 23 + 法术 12；稀有度四档；5 必备关键词；8 种效果组件；2 张废弃卡），`Config/README.md` 补效果串约定；新增 `ConfigContentTests`（8 例）把内容目标变成自动化门禁；重新导入生成物（`cards.json` 17.4 KB）；两条绑定旧条数的加载测试改为"≥ 内容目标"式断言，内容规模统一由 `ConfigContentTests` 把关；累计 443 passed / 0 failed，清缓存 0 error/0 warning | PROGRESS / Config/Excel/Cards.csv / Config/README / Assets/_Project/Config(生成物) / Assets/_Project/7_Tests / tasks(新增) |
| 2026-10-04 | M2 完成 | M2 七项任务全部关闭并通过门禁：`改 CSV → 导入（含校验）→ 生成物 → 加载 → 卡池查询` 全链路打通，热加载支持"成功才换库、失败保旧"，内容目标由测试锁定；累计 443 用例（M1 228 → M2 +215）；覆盖率 `0_Core 96.51%` / `Domain + App 92.36%`；新增 [M2 评审与复盘](./reviews/M2-配置与数据管线-评审与复盘.md) | PROGRESS / reviews(新增) |
| 2026-10-04 | 交接准备 | 新增 [Docs/HANDOFF.md](./HANDOFF.md)（面向接手 AI 的交接说明：现状快照、两种验证跑法与命令、开发流程约定、10 条已知坑、M3 与 P3 待办、接手准备与需用户确认的三件事、快速文件地图）；入口接入 `AGENTS.md` 必读顺序与 `Docs/README.md` 索引；`Docs/02` M2 段加"实施说明"批注（CSV/JSON 取代 xlsx/SO，以 ADR-19/20 为准）；`PROGRESS` 顶部加交接指引并把当前阶段切到 M3；交接前重跑验证：Unity 清缓存 0 error/0 warning + 443 passed、工具链 `0_Core 96.51%` / `Domain + App 92.36%`、`check.ps1` 与 `-SelfTest` PASS、工作区干净且与 `origin/main` 一致 | AGENTS / Docs/README / Docs/02 / Docs/HANDOFF(新增) / PROGRESS |
| 2026-10-04 | 检查点与备份 | 建立可精确回退的检查点：**标签 `checkpoint/m2-complete`**（annotated，指向 `3adfef5`）已推送远端；HANDOFF 新增第 10 节"检查点与恢复（已实测）"，含三种保底方式对比与恢复步骤。实测结论：**本地整目录复制（含 `.git/`，内有 LFS 对象）可离线恢复到 0 改动的干净工作区**（本地克隆演练：索引 442 文件、工作区 0 改动、LFS 文件为真内容）；**离线 bundle（3.04 MB，SHA256 `68DA…4433`）不含 LFS 对象**，直接克隆会在 smudge 处中断（索引为空），需先 `git lfs fetch --all` 或跳过 smudge | Docs/HANDOFF / PROGRESS |
| 2026-07-02 | M3-T6 完成 | `RuleEngine.Validate` 四种命令合法性校验落地：出牌（费用/满场/目标规则）/攻击（失调/次数/嘲讽）/英雄技能（已用/费用/目标）/结束回合；代码 `2_Application/Match/RuleEngine{,Attack,HeroPower,Target}.cs`（partial 拆分，主文件 105 行）+ `CardInstance.AttacksUsedThisTurn`；测试 21 例（出牌 9 + 攻击 7 + 技能/结束 5）；无 Unity 工具链 **546 passed / 0 failed**、0_Core 96.51% / Domain+App 89.77%；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/2_Application / Assets/_Project/1_Domain / Assets/_Project/7_Tests |
| 2026-07-02 | M3-T7 完成 | `MatchEvaluator` 胜负判定落地：`1_Domain/Match/{MatchResult,MatchOutcome}.cs` + `2_Application/Match/MatchEvaluator.cs`；`PlayerState.FatigueCounter` + `MatchState.IsFinished`；`RuleEngine` 终局后拒绝所有命令；`HeroState` 构造器下限放宽到 0；测试 9 例；无 Unity 工具链 **555 passed / 0 failed**、0_Core 96.51% / Domain+App 89.88%；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M3-T8 完成 | `CardDrawService` 疲劳与爆牌落地：`1_Domain/Match/DrawOutcome.cs`（纯数据）+ `2_Application/Match/CardDrawService.cs`（顶牌入手/手牌满爆牌入坟场/空库递增疲劳 1/2/3…，疲劳可致死）；测试 11 例；无 Unity 工具链 **566 passed / 0 failed**、0_Core 96.51% / Domain+App 90.13%；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M3-T9 完成 | `MatchStateSerializer` 状态可序列化落地：`2_Application/Match/MatchStateSerializer{,.Write,.Read}.cs`（JSON 快照往返，version 1，纯 BCL）+ `CardInstance.Restore`；测试 10 例；无 Unity 工具链 **576 passed / 0 failed / 0 warning**、0_Core 96.51% / Domain+App 90.29%；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M3-T10 完成（M3 任务全部完成） | `MatchStateDiffer` 状态增量落地：`1_Domain/Match/StateChange.cs` + `2_Application/Match/MatchStateDiffer.cs`（路径键与序列化器一致，根→玩家 0→玩家 1 确定性顺序）；测试 12 例；无 Unity 工具链 **588 passed / 0 failed / 0 warning**、0_Core 96.51% / Domain+App 90.65%；`check.ps1` PASS；M3 进入待评审状态 | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M3 完成 | M3 十项任务全部关闭并通过门禁：无 Unity 工具链 588 用例全过（M2 443 → +165）、编译 0 error/0 warning、覆盖率 `0_Core 96.51%` / `Domain + App 90.65%`、`check.ps1` PASS；三项 ★（种子洗牌/序列化/增量）就绪；新增 [M3 评审与复盘](./reviews/M3-领域模型与规则内核-评审与复盘.md)（4 类问题 5 Why、M3-B1 挂 P2 为通过条件）；打标签 `checkpoint/m3-complete` | PROGRESS / reviews(新增) / HANDOFF(第 10 节) |
| 2026-10-06 | M3-B1 解除 | Unity 权威验证补跑完成：编辑器 Test Runner（EditMode）**608 passed / 0 failed**（kernel 588 + Infrastructure 20），.meta 导入与程序集编译在真实 Unity 2022.3.54f1c1 中实跑无误；评审文档、PROGRESS、HANDOFF 同步回填；经验：批处理被沙箱阻塞时"编辑器手动跑 + 结果回填"为有效补验路径 | PROGRESS / reviews / HANDOFF |
| 2026-10-06 | M4-T1 完成 | `TurnStateMachine` 回合阶段流转落地：`2_Application/Match/TurnStateMachine.cs`（组合 Core `StateMachine<TurnPhase>`，六阶段显式边：严格顺序 + TurnEnd→TurnStart 回环 + 任意非终局→MatchEnd、MatchEnd 无出边；成功转移同步写回 `MatchState.Phase`，非法转移两状态不变；不含阶段业务）；测试 26 例（含 6 非法边/5 终局起点参数化、自转移、终局终点、与 RuleEngine 阶段集成、业务零副作用）；先红 CS0246 × 32 后绿；无 Unity 工具链 **614 passed / 0 failed**、新文件 100% 覆盖、Domain+App 90.86%；Unity 编辑器补验 **634 passed / 0 failed**；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M4-T2 ★ 完成 | `MatchController` 命令驱动权威入口落地：FIFO 队列 + RuleEngine 校验 + ICommandSettler 结算（输入与结算解耦）+ 终局判定单入口 + MatchStepRecord 步骤台账；`EndTurnSettler` 完成回合流全接线（行动方切换、法力增长/回满、技能/攻击/失调回合开始清零、Draw 抽 1）；出牌/攻击/技能只留接缝（M4-T4/T6 注册）；测试 19 例（回合流/拒绝路径/FIFO/终局截断），完整疲劳脚本同种子两局状态与台账逐项全等（67 步终局、后手疲劳 8 死亡、含爆牌路径）；先红后绿；无 Unity **633 passed / 0 failed**、控制器覆盖 97%、Domain+App 91.20%；Unity 编辑器补验 **653 passed / 0 failed**；Docs/02 加 M4 实施说明（T8 范围收窄为技能命令端到端） | PROGRESS / tasks(新增) / 02-开发计划步骤文档 / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M4-T3 完成 | `GameEvent` 事件模型落地：Domain 新增 GameEvents.cs（基类 + 12 派生事件 + IEventSink + EventLog 单调序号）；Application 增量接线 SettlementContext/MatchController/EndTurnSettler 产出阶段/回合/抽牌/爆牌/疲劳/终局事件，事件序列与结算顺序严格一致，拒绝命令零事件；新增 20 例；先红后绿；无 Unity **653 passed / 0 failed**、GameEvent/EventLog 覆盖 100%、Domain+App 91.70%；Unity 编辑器补验 **673 passed / 0 failed**；`check.ps1` PASS | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
| 2026-10-06 | M4-T4 完成 | 效果框架落地：Domain `GameEffects.cs`（IEffectData + 5 效果数据 + EffectParser）；Application `Effects.cs`（EffectContext/IEffectExecutor/EffectExecutor 分发 + 5 执行器）+ `PlayCardSettler`（出牌端到端：法力→进场/坟场→战吼效果→事件）；Domain 改 HeroState（TakeDamage 护甲先抵/Heal 不超上限）、MatchState（AllocateInstanceId）；新增 26 例；先红后绿；无 Unity **679 passed / 0 failed**、Domain+App 91.25%；`check.ps1` PASS（185 文件）；T2 接缝测试改为验证出牌成功 | PROGRESS / tasks(新增) / Assets/_Project/1_Domain / Assets/_Project/2_Application / Assets/_Project/7_Tests |
