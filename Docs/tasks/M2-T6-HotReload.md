# 任务卡 · M2-T6 热加载

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T6 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.3（编辑器下点击"重载配置"，对局外的卡牌数据即时刷新，无需重启） |
| 规则依据 | [01 §7.3](../01-开发需求文档.md)（查询一律走 id/key）、[03 §5.7](../03-开发规范文档.md)（依赖注入与组合根）、[03 §9.1](../03-开发规范文档.md)（生成物与版本） |
| 结构说明 | 换库语义放 **Domain**（`ConfigService`：纯逻辑 + 注入的加载委托），读盘仍在 Infrastructure（`ConfigFileLoader`）；`ConfigLoadResult` 随之从 Infrastructure 移到 Domain（纯数据结果）。 |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M2-T5（`CardDatabase` 与加载器） |

## 2. 目标（一句话）

> 让"重新加载配置"成为一次**可回退的原子换库**：成功就换上新卡池（新卡立刻可查），失败就保留旧卡池并把问题报出来——绝不让一次坏配置把游戏卡池清空。

## 3. 范围（做什么）

- `Domain/Config/ConfigService`：
  - 构造：`(CardDatabase initial, Func<ConfigLoadResult> reloadSource)` —— 启动必须有配置，`Current` 永不为 null；
  - `TryReload()`：加载成功 → 建新库并替换 `Current`、`Version++`；失败 → 保留旧库、记录 `LastReload`、返回 false；
  - `Version`：供 UI/View 判断"配置变了，需要刷新"。
- `Card.Editor` 菜单 `Tools > Card > 重载配置`（含 `ReloadForAutomation()` 无界面入口）：重新加载 `Assets/_Project/Config` 的生成物并做冒烟自检（卡牌/英雄/每包张数），失败弹窗 + `GameLog`。
- EditMode 测试：换库、保旧、版本递增、失败后恢复、旧库对象仍可用、注入为空时的契约校验。

## 4. 明确不做（防止范围蔓延）

- **不做** 运行时自动轮询文件变化（文件监听）：热加载由人/CI 显式触发（菜单或 `-executeMethod`），避免后台 IO 与不可预期的对局中途换配置。
- **不做** 对局中途换配置的流程编排：何时允许重载（如"仅在对局外"）属 M5/M6 的对局流程；本任务只提供可回退的换库原语。
- **不做** 把服务实例挂到全局静态（铁律 8）：实例由组合根创建，通过构造函数注入给使用者；编辑器菜单做的是"加载 + 自检"，运行时的"同一个服务实例换库"由 M5 接线（届时 View 读的就是 `Current`）。
- **不做** 配置热更新的网络下载：阶段二范围外。

## 5. 接口约定

```csharp
namespace Card.Domain.Config
{
    /// <summary>运行时的"当前配置"句柄：成功才换库，失败保留旧库。</summary>
    public sealed class ConfigService
    {
        public ConfigService(CardDatabase initial, System.Func<ConfigLoadResult> reloadSource);

        public CardDatabase Current { get; }      // 永不为 null
        public int Version { get; }               // 成功重载次数
        public ConfigLoadResult? LastReload { get; }

        public bool TryReload();
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 构造参数为空 | 抛 `ArgumentNullException` |
| AC-2 | 构造后 | `Current` 就是传入的库；`Version = 0`；`LastReload` 为 null |
| AC-3 | 重载成功 | 返回 true；`Current` 换成新库；**新卡立刻可查**；`Version` 递增 |
| AC-4 | 重载失败 | 返回 false；`Current` 仍是旧库（同一实例）；旧卡仍可查；`Version` 不变；`LastReload` 记录问题 |
| AC-5 | 连续成功重载 | `Version` 每次 +1，`Current` 始终是最新的库 |
| AC-6 | 先失败后成功 | 能恢复到新配置 |
| AC-7 | 加载委托返回 null | 抛 `ArgumentNullException`（而不是静默当失败） |
| AC-8 | 已持有旧库对象 | 旧库对象本身仍可用（换库不影响已抓取的引用） |
| AC-9 | 编辑器菜单 | 加载失败时弹窗列出全部问题并返回 false；成功时给出"卡牌/英雄/每包张数"摘要并返回 true |
| AC-10 | 解耦与覆盖率 | `check.ps1` PASS；`Tools/coverage.ps1` PASS（`0_Core` ≥ 90%、`Domain + App` ≥ 80%） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Config`，可被无 Unity 工具链覆盖）
- 最少用例数：8
- 必须覆盖的边界：失败保旧、失败后恢复、版本递增、委托返回 null、旧库引用仍可用、空参数

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；`Config/README.md` 补"导入 / 重载"两步说明
- 需更新的配置表：无
- 是否影响既有模块：`ConfigLoadResult` 从 Infrastructure 移到 Domain（命名空间变化，行为不变）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10 节）
- [x] 测试通过且覆盖边界（新增 8 例，累计 435 passed / 0 failed）
- [x] 编译 0 error / 0 warning（Unity 清缓存干净重编译）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS / Config/README.md）

---

## 10. 证据与评审结论（2026-10-04）

### 10.1 AC 证据

| AC | 证据（测试名） |
| --- | --- |
| AC-1 | `Ctor_WhenArgumentsMissing_ThrowsArgumentNullException` |
| AC-2 | `Ctor_WhenCreated_ExposesInitialDatabaseAndZeroVersion` |
| AC-3 | `TryReload_WhenSourceSucceeds_NewCardAppearsInCurrent` |
| AC-4 | `TryReload_WhenSourceFails_KeepsOldDatabase` |
| AC-5 | `TryReload_WhenCalledTwice_IncrementsVersionEachSuccess` |
| AC-6 | `TryReload_WhenFailureThenSuccess_Recovers` |
| AC-7 | `TryReload_WhenSourceReturnsNull_ThrowsArgumentNullException` |
| AC-8 | `TryReload_SwapsToADifferentDatabaseInstance` |
| AC-9 | 菜单实现（`Tools > Card > 重载配置` + `ReloadForAutomation`），失败走 `ConfigLoadResult.ToText()` 全量问题列表 |
| AC-10 | `check.ps1` PASS；工具链 `0_Core 96.51%`、`Domain + App 92.36%` |

### 10.2 测试证据

| 阶段 | 结果 |
| --- | --- |
| Unity 清缓存重编译 | `errors: 0`、`warnings: 0`、`result=Passed total=435 passed=435 failed=0` |
| 无 Unity 工具链 | PASS：`0_Core 96.51%`、`Domain + App 92.36%` |
| 静态门禁 | `Tools/check.ps1` PASS |

### 10.3 评审结论

**通过**：无 P0/P1；门禁全绿；文档已同步。

**到 M5 的接缝（已在菜单注释与本卡写明）**：编辑器里还没有常驻游戏实例，所以菜单做的是"重新加载 + 冒烟自检"；
`ConfigService` 才是运行时的热替换原语——M5 的 View/服务通过构造注入持有同一个 `ConfigService`，
重载成功后读到的 `Current` 立即是新卡池（这条语义由 AC-3/AC-5 的测试锁定）。
