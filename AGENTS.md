# AGENTS.md — AI 开发强制入口

本文件面向所有在此仓库工作的 AI Agent（以及人类开发者）。**开始任何任务前必须先读本文件。**

---

## 0. 项目一句话

`Card` 是一个"炉石传说式"回合制卡牌对战游戏。当前仓库处于**从旧原型重做为规范工程**的阶段（详见 `Docs/00`）。

开发分两个阶段：**阶段一 M0–M10 = PVE 基础**（当前工作）；**阶段二 M11–M14 = 本机房主模式联网验证**——在一台 PC 上用两个进程跑通双人联网。网络结构为**权威宿主 + 状态同步**：房主客户端进程内跑唯一的规则实例，客户端只上行意图、下行状态。

**阶段二的范围（明确）**：只做 `LocalHost`（PVE）与 `PeerHost`（房主模式）；传输用 BCL `System.Net.Sockets`，**不引入任何第三方网络库**；**不做**专用服务器、匹配服务、账号、NAT 穿透、中继、反作弊、客户端预测（这些全部列入阶段三）。

## 1. 必读顺序（缺一不可）

1. [`Docs/00-现状解构与架构再设计.md`](Docs/00-现状解构与架构再设计.md) —— 为什么重做、目标架构、旧→新映射
2. [`Docs/01-开发需求文档.md`](Docs/01-开发需求文档.md) —— 做什么（规则规格 / FR / AC / 配置表）
3. [`Docs/02-开发计划步骤文档.md`](Docs/02-开发计划步骤文档.md) —— 按什么顺序做（里程碑 / 门禁 / DoD）
4. [`Docs/03-开发规范文档.md`](Docs/03-开发规范文档.md) —— 怎么写（铁律 / 禁止清单 / 模板）
5. [`Docs/04-代码复盘Review规范.md`](Docs/04-代码复盘Review规范.md) —— 怎么自检与验收
6. [`Docs/05-联网对战_状态同步_设计文档.md`](Docs/05-联网对战_状态同步_设计文档.md) —— 联网方案（**3.0 版：本机房主模式验证**）、权威宿主、裁剪、状态增量与重连；第 15 节是当前范围（**涉及规则层或联网时必须读**）
7. [`Docs/06-新项目搭建与仓库初始化清单.md`](Docs/06-新项目搭建与仓库初始化清单.md) —— 新工程搭建、目录骨架、Git/GitHub 初始化（**首次搭建或换工程时读**）
8. [`Docs/HANDOFF.md`](Docs/HANDOFF.md) —— **换人/AI 交接说明**：现状快照、验证命令（含无界面批处理跑法）、已知坑清单、待办与接手准备（**接手项目时必读**）

> 文档与代码冲突时，**以文档为准**，并同时提交代码修复或文档变更提案。

## 2. 十三条铁律（违反即视为未完成，不予合并）

1. 一切皆组件，组件优于继承；禁止 `class XxxCard : Card` 这类玩法继承。
2. 依赖只能向下：Core ← Domain ← Application ← Infrastructure ← Presentation ← Bootstrap。
3. `0_Core` / `1_Domain` / `2_Application` **只依赖 BCL**：禁止 `using UnityEngine`、禁止 `Debug.Log`/`Mathf`/`JsonUtility`/`Time`/`UnityEngine.Random`。
4. 表现层只读：View 不判断规则、不修改游戏状态。
5. 所有操作走 `GameCommand`，由 `RuleEngine` 校验后生效。
6. 数值、效果、卡池、关卡必须来自配置，禁止硬编码。
7. 运行时程序集禁止 `UnityEditor`、`AssetDatabase`、写入 `Application.dataPath`。
8. 禁止 `MonoSingleton` / `static Instance` / `GameObject.Find` / `FindAnyObjectByType` / `ServiceLocator` 隐式全局访问。
9. 新功能必须带自动化测试；规则类先写测试。
10. 单文件 ≤ 300 行、单方法 ≤ 50 行、圈复杂度 ≤ 15、编译 0 warning。
11. **内核与 Unity 解耦**：规则三层只依赖 BCL（服务器要复用同一份源码）；环境依赖走 `GameLog` / `IClock` / `IRandomProvider`；`MatchState` 必须可序列化、可裁剪、可增量。（见 `Docs/03` 第 5.9 节）
12. **权威宿主与网络解耦**：对局关键计算只在权威宿主（`MatchHost`，共享一份实现；本阶段 = 房主客户端）；客户端只上行 `GameCommand`、只接收状态与事件，不得直接改权威状态；网络代码只能在 `Card.Network`；`Card.Presentation` 不得引用 `Card.Network`。（见 `Docs/05`）

13. **阶段二范围锁**：只做 `LocalHost` + `PeerHost`（本机双进程验证）；传输用 BCL TCP；不引入第三方网络库，不做匹配/账号/NAT/中继/反作弊/预测。

> 注：本项目**不再要求**逻辑层零浮点或跨平台逐位一致——那是已废弃的帧同步方案的要求，详见 `Docs/05` 第 14 节。

## 3. 每次任务的固定工作流

```
① 读文档：Docs/00 与 Docs/01 的相关章节 + Docs/03 铁律与禁止清单
② 写任务卡：复制 Docs/templates/TaskCard.template.md，填全（含"明确不做"与可测 AC）
③ 先测试：补/写失败测试（规则类必须）
④ 再实现：小步推进，保持可编译
⑤ 自检：按 Docs/04 第 7 节六步自检 + 第 7.2 节提示词，输出证据
⑥ 评审：按 Docs/04 第 8 节模板输出结论，关闭 P0/P1
⑦ 更新文档：触及需求/计划/规范时同步修改，并在提交信息中引用编号
```

## 4. 绝对禁止（速查）

| 禁止 | 替代 |
| --- | --- |
| 继承表达卡牌种类/效果 | 效果组件组合 |
| View 里 `if (gamePhase == ...)` | 命令 + 事件 |
| `using UnityEditor` 在运行时程序集 | 移入 `Card.Editor` |
| `Application.dataPath` 存档 | `Application.persistentDataPath` |
| `list[index]` 作为业务 ID | id/key 字典查询 |
| `Debug.Log` | `GameLog` 带通道 |
| 改测试让结果变绿 | 修实现或明确登记缺陷 |
| `try/catch` 吞异常 | 明确失败路径与日志 |
| 顺手实现范围外功能 | 先改 `Docs/01` 并排优先级 |
| 规则层出现 `using UnityEngine` / `Debug.Log` / `Mathf` / `JsonUtility` / `Time` / `UnityEngine.Random` | 只用 BCL；`GameLog` / `IClock` / `IRandomProvider` |
| `MatchState` 里放 `MonoBehaviour` / `GameObject` / UI 引用 | 只存数据，引用用 Id / Key |
| 客户端直接修改对局状态（PVP） | 上行 `GameCommand`，由服务器校验与结算 |
| 绕过 `PlayerViewProjector` 下发状态（含快照与重连） | 所有下发路径统一走裁剪 |
| 服务器信任客户端上报的状态 | 只接受意图，全字段校验，非法即拒绝并记录 |
| `Card.Presentation` 引用 `Card.Network` | 依赖 `Card.Application` 中的接口 |

## 5. 目录速查

| 路径 | 内容 |
| --- | --- |
| `Assets/_Project/0_Core` … `5_Bootstrap` | 新代码（按分层） |
| `Assets/_Project/2_Network` | 联网代码（`Card.Network`，可整体移除） |
| `Server/Card.Server` | 服务器程序（Unity 工程外，共享 `0_Core`/`1_Domain`/`2_Application` 源码） |
| `Assets/_Project/6_Editor` | 仅编辑器工具（Excel 导入、校验） |
| `Assets/_Project/7_Tests` | EditMode / PlayMode 测试 |
| `Assets/_Project/Config` | 由 Excel 导出的 JSON / SO（生成物，勿手改） |
| `Config/Excel` | Excel 配置源表（**在 `Assets/` 之外**，Unity 不会导入） |
| `Tools` | `check.ps1` 等本地校验脚本 |
| 旧工程 `E:\Unity\Project\Card` | 旧原型，**只读参考**；旧代码**不要拷进本仓库的 `Assets/`**（会导致编译错误） |
| `Docs/` | 权威文档 |

## 6. 提交规范

```
<type>(<scope>): <描述> [<需求编号>]
type: feat | fix | refactor | test | docs | chore | perf | build
```

一次提交只做一件事；提交必须可编译；禁止提交 `Library/`、`Temp/`、`Logs/`、`*.csproj`、`*.sln`。

> **例外**：`Tools/` 下**手工维护**的 .NET 工具工程（如 `Tools/Coverage/**/*.csproj`）应当入库——
> 上面的规则针对的是 Unity 自动生成、每台机器各不相同的工程文件；工具工程是构建脚本的一部分，不入库就无法复现门禁。
> `.gitignore` 里对应有 `!Tools/**/*.csproj` 的窄例外。

## 7. 遇到不确定时

1. 先查 `Docs/01`（需求）与 `Docs/03`（规范）。
2. 仍不确定 → 不要自行发明规则；把它作为**待确认问题**列在任务卡中，按最小影响方案推进，并明确标注假设。
3. 若需求本身需要变更 → 改文档 + 在任务卡中说明理由，不要沉默地扩大范围。
