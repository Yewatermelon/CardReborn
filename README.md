# CardReborn

炉石传说式回合制卡牌对战游戏（重做版）。以 PvE 为基础，预留并逐步实现联网对战。

## 基本信息

| 项 | 值 |
| --- | --- |
| 远程仓库 | https://github.com/Yewatermelon/CardReborn.git |
| Unity 版本 | **2022.3.54f1c1（LTS）** |
| 模板 / 渲染管线 | 2D / Built-In（无 URP、无 HDRP） |
| 旧工程（参考） | `E:\Unity\Project\Card` —— 只读参考，代码不并入本仓库 |

## 快速开始

1. 用 **Unity 2022.3.54f1c1** 打开本目录（首次打开会生成 `Library/` 与资源的 `.meta`）。
2. 首次打开后执行一次 **Window → TextMeshPro → Import TMP Essential Resources**（必须，否则 TMP 文本不显示）。
3. 运行本地门禁：`powershell -ExecutionPolicy Bypass -File Tools/check.ps1`
4. 测试：`Window → General → Test Runner`，运行 EditMode / PlayMode。
5. 无 Unity 环境下跑内核测试与覆盖率：`powershell -ExecutionPolicy Bypass -File Tools/coverage.ps1`（首次需联网还原 NuGet 包）。
6. 导入 / 重载配置：菜单 `Tools > Card > 导入配置` / `Tools > Card > 重载配置`（无界面入口见 [Docs/HANDOFF.md](Docs/HANDOFF.md) 第 4 节）。

## 文档（唯一权威依据）

所有开发工作必须以 [`Docs/`](Docs/README.md) 为准；AI Agent 的强制入口是 [`AGENTS.md`](AGENTS.md)。

| 文档 | 作用 |
| --- | --- |
| [Docs/00 现状解构与架构再设计](Docs/00-现状解构与架构再设计.md) | 为什么重做、目标架构、旧→新映射 |
| [Docs/01 开发需求文档](Docs/01-开发需求文档.md) | 规则规格、功能需求（FR）、验收标准 |
| [Docs/02 开发计划步骤文档](Docs/02-开发计划步骤文档.md) | 里程碑 M0–M14、门禁、风险 |
| [Docs/03 开发规范文档](Docs/03-开发规范文档.md) | 铁律、目录与程序集、代码规范 |
| [Docs/04 代码复盘 Review 规范](Docs/04-代码复盘Review规范.md) | 评审分级、Checklist、模板 |
| [Docs/05 联网对战设计文档](Docs/05-联网对战_状态同步_设计文档.md) | 权威状态同步（当前阶段：本机房主模式验证） |
| [Docs/06 新项目搭建清单](Docs/06-新项目搭建与仓库初始化清单.md) | 环境搭建、Git 初始化 |
| [Docs/PROGRESS.md](Docs/PROGRESS.md) | 进度看板 |
| [Docs/HANDOFF.md](Docs/HANDOFF.md) | **换人 / AI 交接说明**（现状快照、验证命令、已知坑、待办） |
| [Docs/tasks/](Docs/tasks/) ・ [Docs/reviews/](Docs/reviews/) | 任务卡 ・ 里程碑复盘 |

## 目录结构

```
Assets/_Project/
├─ 0_Core/           Card.Core            纯 BCL，基础工具
├─ 1_Domain/         Card.Domain          规则与状态模型（唯一真相）
├─ 2_Application/    Card.Application     用例编排、命令、权威运行时 MatchHost
├─ 2_Network/        Card.Network         联网实现（阶段二）
├─ 3_Infrastructure/ Card.Infrastructure  配置、存档、Unity 适配
├─ 4_Presentation/   Card.Presentation    表现层（只读订阅）
├─ 5_Bootstrap/      Card.Bootstrap       组装与启动
├─ 6_Editor/         Card.Editor          仅编辑器工具（配置导入/重载、校验）
├─ 7_Tests/          Card.Tests.*         EditMode / PlayMode 测试
└─ Art/ Audio/ Config/ Prefabs/ Scenes/

Config/Excel/        CSV 配置源表（Assets 之外，Unity 不导入；Excel 可直接另存）
Tools/               check.ps1、coverage.ps1 与无 Unity 覆盖率工具链
Docs/                开发文档
```

## 依赖方向（编译期强制）

```
Core ← Domain ← Application ← { Network, Infrastructure } ← Presentation ← Bootstrap
```

- `0_Core` / `1_Domain` / `2_Application` **禁止引用 `UnityEngine`**（asmdef 已设 `noEngineReferences`，且 `check.ps1` 会扫描）。
- 表现层只读：不判断规则、不修改游戏状态。

## 当前状态

见 [Docs/PROGRESS.md](Docs/PROGRESS.md)（每个任务的证据）与 [Docs/HANDOFF.md](Docs/HANDOFF.md)（交接快照）。

**当前阶段：M3 领域模型与规则内核**；已完成 M0（工程基建）、M1（Core 基础层）、M2（配置与数据管线）。
