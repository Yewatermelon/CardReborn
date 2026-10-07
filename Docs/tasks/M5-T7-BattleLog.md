# 任务卡 · M5-T7 `BattleLogView`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T7 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-5.12（战斗日志：记录并展示关键事件——出牌、伤害、死亡、回合切换，可滚动）、03 §5.6（表现层只读）、§5.8（高频创建条目走池）、U-6（TMP） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T7：事件流滚动显示；完成标准：与 `GameEvent` 一一对应） |
| 预估 | 1 会话 |
| 依赖任务 | M4-T3（12 类 `GameEvent` + `EventLog`）、M5-T2（`MatchEventPump`） |

## 2. 目标（一句话）

> 交付战斗日志：12 类 `GameEvent` 一一格式化为中文行（`BattleLogFormatter`），`BattleLogView` 追加条目、滚动到底、容量超限回收最旧条目且条目对象走池。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/Log/`）

1. **`BattleLogFormatter`（static，纯 C#）**：`string Format(GameEvent e)`——12 类事件一一映射，每类恰一行；未知派生类型抛 `ArgumentOutOfRangeException`（新增事件类型必须显式补映射，不容忍静默缺失）。文案含座位/实例 Id（如"随从 #7 受到 3 点伤害"）；卡牌名称本地化属 M8，本任务用 Id 占位。`DamageEvent.DivineShieldConsumed` 追加"（圣盾抵消）"。
2. **`BattleLogView`（MonoBehaviour）**：
   - `[SerializeField] internal TMP_Text _entryPrefab` / `Transform _content` / `ScrollRect? _scrollRect`（可空，空则不滚动）/ `int _maxEntries = 100`；
   - `Append(GameEvent e)`：格式化 → 追加条目 → 滚动到底（`verticalNormalizedPosition = 0`）；
   - 条目池化：内部 `ObjectPool<TMP_Text>`（M1-T6）惰性建池；超过 `_maxEntries` 时回收**最旧**条目（归还池）再租新条目——容量有上限的滚动窗口；
   - `EntryCount` / `internal IReadOnlyList<TMP_Text> Entries` / `internal int CreatedCount`（测试观察池复用）/ `Clear()` 全部归还。

### 3.2 新增测试（`7_Tests/EditMode/Presentation/`，目标 ≥ 18 例）

- `BattleLogFormatterTests`（13 例）：12 类事件各一例（断言关键子串：座位/Id/数值/阶段名）+ 未知派生事件抛异常。
- `BattleLogViewTests`（6 例）：Append 条目落到 `_content` 下且文本正确；按序多条；滚动到底置 0；超容量回收最旧（max=3 追加 5 条 → 剩 3 条且文本为第 3/4/5 条）；池复用（churn 后 `CreatedCount` 不涨）；`Clear` 归还全部且复用。

## 4. 明确不做（防止范围蔓延）

- **不做日志过滤/分类染色/点击跳转**：UI 打磨属 M9。
- **不做卡牌名称本地化**：文案用 Id 占位，本地化在 M8 决定。
- **不做日志持久化/导出**：FR-13.6 命令流水导出已由 M4-T9 覆盖；本视图纯内存。
- **不接 `MatchEventPump` 的装配**：由 M6 场景装配把泵事件接到 `Append`；视图只暴露 `Append`。
- **不改规则三层**；既有 741 内核用例零改动。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle.Log
{
    public static class BattleLogFormatter
    {
        public static string Format(GameEvent e);   // 12 类一一映射；未知类型抛 ArgumentOutOfRangeException
    }

    public sealed class BattleLogView : MonoBehaviour
    {
        // [SerializeField] internal TMP_Text _entryPrefab; Transform _content; ScrollRect? _scrollRect; int _maxEntries = 100;
        public int EntryCount { get; }
        public void Append(GameEvent e);
        public void Clear();
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 12 类事件逐一 `Format` | 每类产出非空且互不相同的行，关键字段（座位/Id/数值）入文案 |
| AC-2 | 未知 `GameEvent` 派生 | 抛 `ArgumentOutOfRangeException` |
| AC-3 | `Append(e)` | `_content` 下新增一条激活条目，文本==`Format(e)` |
| AC-4 | 多条追加 | 条目顺序与事件顺序一致（后追加的在末尾） |
| AC-5 | 追加后 | `_scrollRect.verticalNormalizedPosition == 0`（滚到底） |
| AC-6 | max=3 追加 5 条 | `EntryCount==3`，文本为第 3/4/5 条（最旧被回收） |
| AC-7 | 超容量反复追加后 | 池 `CreatedCount` 稳定（复用，零新建） |
| AC-8 | `Clear()` | 全部归还禁用；再追加复用旧对象 |
| AC-9 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除沿用 T1 口径）。
- 最少用例数：18。
- 必须覆盖的边界：未知事件类型、容量边界（恰好满/超一条）、池复用、Clear 后再追加。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T7 状态 + 证据）。
- 需更新的配置：无（asmdef 引用不动——`UnityEngine.UI` 已在 Card.Presentation 引用内）。
- 是否影响既有模块：纯增量。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。文案用 Id 占位为显式假设（本地化 M8）。

---

## 11. 结论与证据

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑 910 通过佐证）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（规则三层零改动）；Unity EditMode 用户实跑 **910 passed / 0 failed**（886 + 24 新例，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（268 文件，+4）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（不变）。
- **修复记录**：`TurnPhase.Combat` 不存在（枚举实为 MatchStart/TurnStart/Draw/Main/TurnEnd/MatchEnd；攻击是主阶段内动作），两处测试用例改用真实阶段——实现未动，属测试笔误。
- **铁律扫描**：
  - R4/§5.6：视图只消费事件、只写自身 UI；格式化器为纯函数 ✅
  - R8：无 `Find`/单例；依赖经序列化字段/`InitializeForTests` 显式注入 ✅
  - R10：实现 2 文件（73 + 97 行），单方法 ≤ 15 行，0 warning ✅
  - §5.8：条目 `ObjectPool<TMP_Text>` 池化，超容量回收最旧（churn 20 条 `CreatedCount` 恒 3 已验证）✅
  - U-6：文本全 TMP；`Append` 事件驱动无每帧分配（`string.Format` 属事件路径）✅
  - 数字入文案走 `CultureInfo.InvariantCulture`（与系统区域无关）✅

### 11.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P3 | 0 | — |

### 11.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/Log/BattleLogFormatter.cs` | 12 类事件一一格式化；未知类型抛异常 |
| `4_Presentation/Battle/Log/BattleLogView.cs` | 追加/滚动/容量回收/池化/Clear |
| `7_Tests/.../BattleLogFormatterTests.cs` | 17 例 |
| `7_Tests/.../BattleLogViewTests.cs` | 6 例 |

### 11.4 下一步

进入 M5-T8 只读视图模型 ★（M5 收官项：表现层依赖只读对局视图，评审确认 View 拿不到可写状态；PVP 下发状态可直接复用同一套表现层）。
