# 任务卡 · M5-T3 对象池接入

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T3 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.1（视图只读渲染）；性能约束：对局中无 `Instantiate/Destroy` 卡牌（[02 M5 任务表](../02-开发计划步骤文档.md)） |
| 规则依据 | [02 M5-T3](../02-开发计划步骤文档.md)：产出物=卡牌视图全部走池；完成标准=对局中无 `Instantiate/Destroy` 卡牌，GC 分配接近 0；[03 §5.8](../03-开发规范文档.md)：对象池只用于表现对象 |
| 预估 | 1 会话 |
| 依赖任务 | M5-T2（`HandView`/`BoardView`/`CardListSync`）、M1-T6（`ObjectPool<T>`） |

## 2. 目标（一句话）

> 把 `HandView`/`BoardView` 的卡牌子件增删从 `Instantiate/Destroy` 改造为走 `ObjectPool<CardView>` 的租还（借出激活、归还禁用），峰值之后增删不再触发新建，对局中 GC 分配接近 0。

## 3. 范围（做什么）

### 3.1 新增实现

1. **`CardViewPool`**（`4_Presentation/Battle/CardViewPool.cs`，`internal sealed`）— `ObjectPool<CardView>` 的 Unity 侧封装：
   - 构造入参：`CardView prefab`、`Transform poolRoot`（禁用容器）、`prewarmCount`；
   - 工厂：`Instantiate(prefab, poolRoot)` 后立即 `SetActive(false)`；
   - `Rent(Transform parent)`：池借出 → `SetParent(parent, false)` → `SetActive(true)`；
   - `Return(CardView)`：`SetActive(false)` → `SetParent(poolRoot, false)` → 池归还；
   - 暴露 `CreatedCount` / `IdleCount` / `ActiveCount`（观察新建数，供测试断言"对局中无新建"）。
2. **改造 `HandView` / `BoardView`**：
   - 新增 `[SerializeField] internal int _prewarmCount`（手牌默认 4、战场默认 7，表现配置非规则数值）；
   - 首次 `SetCards` 时惰性建池（`EnsurePool`）：在自身 `transform` 下建禁用子物体 `CardViewPool` 作 `poolRoot`，并按 `_prewarmCount` 预热；
   - 增删逻辑从 `CardListSync.Sync` 改为池租还（多退少租），布局仍走 `CardListLayout`；
   - 暴露 `internal IReadOnlyList<CardView> Children` 与 `internal CardViewPool Pool`（测试观察点，`InternalsVisibleTo` 既有）。
3. **删除 `CardListSync`**（`CardListLayout.cs` 中移除该类）：池化后无引用；`CardListLayout` 保留。

### 3.2 测试（EditMode，目标 ≥ 10 新例 + 更新既有 9 例）

- 新增 `CardViewPoolTests`：租出激活/挂载、归还禁用/回容器、租还复用 `SameAs`、预热计数、归还后 `IdleCount`。
- 更新 `HandViewTests` / `BoardViewTests`：
  - 取子件从 `transform.GetChild(i)` 改为 `_view.Children[i]`（poolRoot 占用层级，语义断言与层级解耦）；
  - "Shrink 销毁"改为"Shrink 归还入池"（`IdleCount` 增加、被退件 `SetActive(false)`）；
  - 新增"峰值后增删无新建"：N 张 → 0 张 → N 张，`CreatedCount` 恒为 N 且复用原对象；
  - 新增"预热覆盖"：`_prewarmCount = 5` 首次 `SetCards` 后 `CreatedCount == 5`。

## 4. 明确不做（防止范围蔓延）

- **不改规则三层任何代码**；`ObjectPool<T>`（M1-T6）本体不改。
- **不做 GC 分配的 Profiler 自动化断言**：靠"复用 + 峰值后零新建"间接保证，运行期 GC 观察留待 M6 垂直切片性能核查。
- **不做动画/飘字/音效**（M5-T6）、不做输入（M5-T4/T5）、不做对局视图模型（M5-T8）。
- **不做池上限拒绝策略**：手牌/战场规模由规则层封顶（10/7），池 `maxSize`/`maxCapacity` 不设限（默认 0）。
- **不做 `CardView` 归还时的数据清空**：`SetData` 全字段覆盖（含攻血面板显隐），残留不可见；预热对象从未 `SetData` 也只是禁用态。
- **不做场景装配**：谁持有视图/喂数据属 M6-T1。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle
{
    internal sealed class CardViewPool
    {
        public CardViewPool(CardView prefab, Transform poolRoot, int prewarmCount = 0);
        public int CreatedCount { get; }   // 工厂累计 Instantiate 数（含预热）
        public int IdleCount { get; }
        public int ActiveCount { get; }
        public CardView Rent(Transform parent);   // SetParent(parent)+SetActive(true)
        public void Return(CardView view);        // SetActive(false)+SetParent(poolRoot)
    }

    public sealed class HandView : MonoBehaviour
    {
        // 追加：[SerializeField] internal int _prewarmCount = 4;
        internal IReadOnlyList<CardView> Children { get; }
        internal CardViewPool Pool { get; }       // 触发惰性建池
        // SetCards / ChildCount 签名不变
    }

    public sealed class BoardView : MonoBehaviour
    {
        // 同 HandView，_prewarmCount 默认 7
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 池 `Rent(parent)` | 返回实例 `activeSelf==true`，父级为 `parent`，`ActiveCount+1` |
| AC-2 | 池 `Return(view)` | 实例 `activeSelf==false`，父级为 `poolRoot`，`IdleCount+1`；再 `Rent` 返回 `SameAs` 原实例 |
| AC-3 | 池预热 3 | 构造后 `CreatedCount==3`、`IdleCount==3`、全部禁用且挂载 `poolRoot` 下 |
| AC-4 | `HandView.SetCards` 3 张（无预热） | `Children` 3 个渲染正确、`CreatedCount==3`、居中布局不变 |
| AC-5 | 3 张 → 1 张 | 末尾 2 件归还（禁用、入池），`IdleCount==2`，剩 1 件数据更新 |
| AC-6 | 峰值后增删 | 3 张 → 0 张 → 3 张：`CreatedCount` 恒为 3，第三次取回的 3 件与首次 `SameAs`（零新建 = 对局中无 Instantiate） |
| AC-7 | `_prewarmCount=5` 首次 `SetCards(1)` | `CreatedCount==5`（预热一次建成）、之后增删在 5 以内零新建 |
| AC-8 | 同数量再 `SetCards` | 不租不还不新建，仅刷新数据（同 T2 语义） |
| AC-9 | `BoardView` 全量同形状验证 | 增删/复用/布局行为与 `HandView` 一致 |
| AC-10 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过（Presentation 不在 coverage 扫描范围） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除口径同 T1/T2）。
- 最少用例数：新增 ≥ 10。
- 必须覆盖的边界：空列表、增/减/同数三向、峰值复用零新建、预热、归还对象禁用态。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T3 状态 + 证据）。
- 需更新的配置：无。
- 是否影响既有模块：`HandView`/`BoardView` 内部实现变化（对外 `SetCards`/`ChildCount` 签名不变）；`CardListSync` 删除（无其他引用方，已全仓 grep 确认）；既有 9 例视图测试随层级语义更新。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 10. 结论与证据

### 10.1 自检与评审

- **先红后绿**：`CardViewPoolTests`（6 例）先于实现编写（编译错误即红）；`HandViewTests`/`BoardViewTests` 按池化语义重写（`transform.GetChild` → `_view.Children`，"销毁"改"归还入池"）并新增峰值零新建、预热用例。
- **编译**：无 Unity 工具链 PASS；Unity 侧 0 error / 0 warning（编辑器实编确认，2026-10-07）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（基线不变，Presentation 测试排除在 kernel 口径外）；Unity EditMode 用户实跑 **802 passed / 0 failed**（793 + 6 池用例 + 2 手牌新例 + 1 战场新例，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（228 文件）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（不变）。
- **铁律扫描**：
  - R1：池为表现层基础设施，无玩法继承 ✅
  - R2/R3/R11：规则三层零改动；`CardViewPool` 位于 Presentation，依赖 Core + UnityEngine ✅
  - R4：视图仍只读渲染，无规则判断、无状态回写 ✅
  - R6：`_prewarmCount` 为表现层序列化配置（手牌 4 / 战场 7），非规则数值 ✅
  - R8：无 `Find`/`static Instance`；池容器为实例持有的禁用子物体 ✅
  - R10：最大文件 65 行，单方法 ≤ 20 行 ✅
- **完成标准对照**："对局中无 `Instantiate/Destroy` 卡牌"由 AC-6 证明——峰值 N 后任意增删 `CreatedCount` 恒为 N（零新建零销毁）；预热使开局阶段的新建集中发生在首次同步。

### 10.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P2 | 0 | — |
| P3 | 1 | `HandView`/`BoardView` 池化代码完全重复（含 `EnsurePool`），沿用 T2 已登记观察项的决定：未来手牌扇形/战场合位分化时再抽象，当前不为两处重复引入基类。 |

### 10.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/CardViewPool.cs` | 新增：`ObjectPool<CardView>` 的 Unity 侧封装（租还/预热/观察计数） |
| `4_Presentation/Battle/HandView.cs` / `BoardView.cs` | 增删改走池租还；新增 `_prewarmCount`、`Children`、`Pool` |
| `4_Presentation/Battle/CardListLayout.cs` | 删除已无引用的 `CardListSync`（Instantiate/Destroy 路径退役） |
| `7_Tests/EditMode/Presentation/CardViewPoolTests.cs` | 新增 6 例 |
| `7_Tests/EditMode/Presentation/HandViewTests.cs` / `BoardViewTests.cs` | 按池化语义重写 9 例 + 新增 3 例 |
