# 任务卡 · M5-T2 `HandView` / `BoardView` / `HeroView` / `ManaView`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T2 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.1（视图只读渲染）、FR-5.2（法力显示当前/上限）、FR-5.7（死亡更新场面） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T2：布局、增删卡、事件驱动刷新；完成标准：数据变化 → UI 自动刷新；UI 不反向改数据） |
| 预估 | 1 会话 |
| 依赖任务 | M5-T1（`CardView`/`ICardViewData`）、M4-T3（`EventLog`/`GameEvent`） |

## 2. 目标（一句话)

> 交付四个只读区域视图：`HandView`/`BoardView`（卡牌列表的增删与布局）、`HeroView`（英雄血/甲）、`ManaView`（法力当前/上限），以及事件驱动机制 `MatchEventPump`（观察 `EventLog` 追加并派发）；数据变化经事件触发刷新，视图不回写任何状态。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/`）

1. **`ManaView`** — `SetData(int current, int max)` 渲染两个 TMP 字段（参照 03 §14.3 示例）。
2. **`HeroView`** — `SetData(int health, int maxHealth, int armor)`；护甲 > 0 时显示护甲面板，为 0 隐藏（表现逻辑，同 T1 法术隐藏攻血）。
3. **`HandView` / `BoardView`** — `SetCards(IReadOnlyList<ICardViewData>)`：
   - 子件数量对齐：少了 `Instantiate` 注入的 `CardView` prefab，多了从末尾 `Destroy`（对象池属 T3，本任务明确用实例化）；
   - 每个子件 `SetData` + 水平居中布局（共享 `CardListLayout` 静态定位，间距由各自 `_spacing` 配置）。
4. **`CardListLayout`**（internal static）— 居中水平排布：`x = (i - (n-1)/2) * spacing`。
5. **`MatchEventPump`** — 持有 `EventLog`，记录已派发序号；`Pump()` 把新追加事件按序派发给 `EventAppended` 订阅者并返回条数；构造时跳过历史事件（视图首帧直接 `SetData`，泵只管增量）。
6. **`CardViewData.FromInstance(CardDefinition, CardInstance)`** — 运行时攻/血取自实例（受伤/buff 后正确显示），名称/描述/费用/美术/类型取自配置。

> 范围调整说明：M5-T1 任务卡曾把 `FromInstance` 留给 M5-T8；T2 的手牌/战场必须显示运行时攻血（死亡/交换后立即刷新是完成标准），故提前到本任务。T8 仍负责完整只读对局视图模型。

### 3.2 新增测试（`Assets/_Project/7_Tests/EditMode/Presentation/`，目标 ≥ 20 例）

- `CardViewDataTests` 追加：`FromInstance` 映射/受伤血量/null 防护。
- `ManaViewTests` / `HeroViewTests`：渲染与护甲显隐。
- `HandViewTests` / `BoardViewTests`：增卡实例化、减卡销毁、同数量复用更新、居中布局坐标、清空。
- `MatchEventPumpTests`：无新事件返回 0、按序派发、跳过历史；**集成**：真实 `MatchFactory`+`MatchController` 提交 `EndTurn` → 泵派发 → 回调里用手牌数据刷新 `HandView` → 断言新抽的牌出现（证明"事件驱动刷新"端到端成立）。

## 4. 明确不做（防止范围蔓延）

- **不做对象池**（M5-T3）：本任务增删卡用 `Instantiate`/`Destroy`。
- **不做点击/拖拽/指向交互**（M5-T4/T5）。
- **不做动画/飘字/音效**（M5-T6）、不做战斗日志（M5-T7）。
- **不做完整只读对局视图模型**（M5-T8 ★）：本任务视图数据由调用方按 03 §5.6"读 MatchState 快照"组装；场景装配（谁持有泵、谁喂数据）属 M6-T1。
- **不做 LayoutGroup/CurvedFan 扇形手牌等视觉打磨**：手动直线布局，M9 再美化。
- **不改规则三层任何代码**；既有 741 内核用例零改动。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle
{
    public sealed class ManaView : MonoBehaviour
    {
        public void SetData(int current, int max);
    }

    public sealed class HeroView : MonoBehaviour
    {
        public void SetData(int health, int maxHealth, int armor);
    }

    public sealed class HandView : MonoBehaviour
    {
        // [SerializeField] internal CardView _cardPrefab; float _spacing
        public void SetCards(IReadOnlyList<ICardViewData> cards);
        public int ChildCount { get; }
    }

    public sealed class BoardView : MonoBehaviour { /* 同 HandView 形状 */ }

    internal static class CardListLayout
    {
        public static void Layout(IReadOnlyList<CardView> children, float spacing);
    }

    public sealed class MatchEventPump
    {
        // 消费 IReadOnlyList<GameEvent>（MatchController.Events 即此类型，EventLog 不直接外泄）
        public MatchEventPump(IReadOnlyList<GameEvent> source);   // 跳过既有历史，只派发新增
        public event Action<GameEvent>? EventAppended;
        public int PendingCount { get; }
        public int Pump();                             // 返回本次派发条数
    }

    // CardViewData 追加：
    public static CardViewData FromInstance(CardDefinition definition, CardInstance instance);
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `ManaView.SetData(3,5)` | 当前/上限文本渲染为 "3"/"5" |
| AC-2 | `HeroView.SetData(25,30,4)` | 生命 "25"，护甲面板显示且文本 "4"；`armor=0` 时护甲面板隐藏 |
| AC-3 | `HandView.SetCards` 3 张 | 子件 3 个，各自 `SetData` 内容正确，位置按 `CardListLayout` 居中分布 |
| AC-4 | 3 张 → 1 张 | 末尾 2 个子件被销毁，剩 1 个数据更新 |
| AC-5 | 同数量再 `SetCards` | 不新建/销毁子件，仅刷新数据（对象复用） |
| AC-6 | `FromInstance` 受伤随从 | 视图数据 Health 为实例当前值而非配置满血 |
| AC-7 | 泵跳过历史 | 构造后 `PendingCount==0`，`Pump()==0`，无派发 |
| AC-8 | 泵按序派发 | `EventLog` 追加 3 条后 `Pump()==3`，订阅者收到顺序一致 |
| AC-9 | 端到端事件驱动 | 真实对局 `EndTurn` → 泵派发 → 回调刷新 `HandView` → 新抽牌出现在子件中 |
| AC-10 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除同 T1）。
- 最少用例数：20。
- 必须覆盖的边界：空列表、增/减/同数三向对齐、护甲 0、泵空转、事件顺序。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T2 状态 + 证据）。
- 需更新的配置：无（asmdef 与 coverage 排除 T1 已就位）。
- 是否影响既有模块：`CardViewData` 追加 `FromInstance`（纯增量）。

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

- **编译**：0 error / 0 warning（无 Unity 工具链 + Unity 编辑器双侧实跑确认）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（Presentation 测试同 T1 口径排除在 coverage 外）；Unity EditMode 补验 **793 passed / 0 failed**（770 + 23 新例，用户实跑 2026-10-07）。

> 修复记录：Unity 侧首编 2 错——`Application.isPlaying` 撞 `Card.Application` 命名空间（改全限定 `UnityEngine.Application`）；测试误用不存在的 `TurnPhase.End`（改 `TurnEnd`）。均已修复并复验通过。
- **静态门禁**：`check.ps1` PASS（226 文件，+12 新文件）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（Presentation 不在扫描范围，不变）。
- **铁律扫描**：
  - R1：规则三层零改动；`MatchEventPump` 纯 C# 无 UnityEngine 依赖 ✅
  - R3：无 `Find`/`static Instance`；子件经 `_cardPrefab` 注入 ✅
  - R4：无 `Debug.Log`；`Guard.NotNull` 守契约 ✅
  - R5：单文件 ≤ 78 行，单方法 ≤ 12 行 ✅
  - R6：`Card.Presentation` 引用未变（Core/Domain/App/Infra/TMP 既有） ✅
- **UI 不反向改数据**：四个视图仅暴露 `SetData/SetCards` 入参渲染，无任何回写 `MatchState` 的路径（评审确认）。

### 10.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P2 | 0 | — |
| P3 | 1 | `HandView`/`BoardView` 形状相同但各自独立成类（未来手牌扇形/战场合位会分化）；共享逻辑收敛在 `CardListLayout`/`CardListSync` 两个 internal static，未做提前抽象基类。 |

### 10.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/ManaView.cs` / `HeroView.cs` | 法力 / 英雄（血+护甲显隐）只读视图 |
| `4_Presentation/Battle/HandView.cs` / `BoardView.cs` | 卡牌列表视图（增删对齐 + 复用） |
| `4_Presentation/Battle/CardListLayout.cs` | `CardListLayout`（居中布局）+ `CardListSync`（子件增减） |
| `4_Presentation/Battle/MatchEventPump.cs` | 事件泵：观察 `IReadOnlyList<GameEvent>` 增量按序派发 |
| `4_Presentation/Battle/CardViewData.cs` | 追加 `FromInstance`（运行时攻/血） |
| `7_Tests/EditMode/Presentation/` | 新增 5 个测试文件 23 例；`PresentationTestPrefabs` 共享夹具 |

### 10.4 下一步

Unity EditMode 补验 **793 passed / 0 failed** 已完成（2026-10-07）。进入 M5-T3 对象池接入。
