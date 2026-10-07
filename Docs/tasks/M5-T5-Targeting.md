# 任务卡 · M5-T5 `TargetingController`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T5 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.3（目标箭头跟随鼠标，分辨率变化后仍准确）、03 §5.6（表现层不判规则）、U-8（屏幕坐标统一走 `RectTransformUtility`）、U-9（输入抽象 `IInputSource`） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T5：指向模式、箭头、右键/Esc 取消；完成标准：分辨率变化后箭头仍准确） |
| 预估 | 1 会话 |
| 依赖任务 | M5-T4（`ICommandSink`/`PlayerInputController`）、M3-T4（`TargetRef`/`GameCommand`）、M4-T3（`RuleEngine.Target` 校验） |

## 2. 目标（一句话）

> 交付指向子系统：进入指向模式后箭头从源位置跟随指针，左键点选实体产出带 `TargetRef` 的完整命令提交权威侧，右键/Esc 取消；箭头两端坐标每帧经 `RectTransformUtility` 从屏幕坐标现算，分辨率变化后仍准确。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/Targeting/`）

1. **`IInputSource`（接口，U-9 落地）** — 输入抽象：
   - `Vector2 PointerScreenPosition { get; }`（指针屏幕坐标）
   - `bool IsConfirmPressed { get; }`（本帧确认=左键按下，边沿语义由实现方保证）
   - `bool IsCancelPressed { get; }`（本帧取消=右键或 Esc 按下）

2. **`ITargetPicker`（接口）** — 命中测试抽象：`bool TryPickTarget(Vector2 screenPosition, out TargetRef target)`。把"指针下是什么实体"翻译成领域 `TargetRef`（随从=InstanceId、英雄=座位 Id）；未命中返回 false。生产实现（EventSystem/Physics 射线）属 M6 场景装配；本任务测试用 stub。**picker 只做命中识别，不做合法性过滤**（合法性由 RuleEngine 裁定）。

3. **`TargetingArrowView`（MonoBehaviour）** — 箭头表现：
   - `[SerializeField] internal RectTransform _area`（换算参照，通常 Canvas RectTransform）、`_line`（细条）、`float _lineWidth`；
   - `Show()`/`Hide()` 切显隐；
   - `SetEndpoints(Vector2 startScreen, Vector2 endScreen)`：两端各自经 `RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, _uiCamera)` 现算本地坐标，再设 `_line` 的中点位置/角度/长度。**每次调用都现算，不缓存屏幕↔本地映射**——这是"分辨率变化后仍准确"的实现方式（U-8）。
   - `_uiCamera` 经 `Initialize(Camera?)` 注入；Screen Space - Overlay 画布传 null。

4. **`TargetingController`（MonoBehaviour）** — 指向状态机（Idle ↔ Targeting）：
   - 依赖注入：`Initialize(IInputSource input, ITargetPicker picker, ICommandSink sink)`；`[SerializeField] internal int _localPlayerId`、`_arrow`。
   - `BeginPlayCardTargeting(int cardInstanceId, Func<Vector2> originScreenProvider)` / `BeginAttackTargeting(int attackerInstanceId, ...)` / `BeginHeroPowerTargeting(...)`：进入指向模式并显示箭头。**起点不是快照坐标而是 `Func<Vector2>` 现算提供者**（由调用方闭包读取源 RectTransform 的实时屏幕位置），分辨率变化/布局移动后箭头起点仍准确。
   - `Update()` 透传到 `internal void Tick()`（EditMode 可测）：箭头跟随 `_origin()` → 指针；取消按下 → `CancelTargeting()`（隐箭头 + 触发 `TargetingCancelled`，不发命令）；确认按下且 picker 命中 → 按模式构造 `PlayCardCommand`/`AttackCommand`/`UseHeroPowerCommand`（`PlayerId=_localPlayerId`）经 sink 提交，按结果触发 `CommandAccepted`/`CommandRejected(CommandError)`，无论接受与否都退出指向（拒绝原因已透出，由玩家重新发起）；确认但未命中 → 保持指向、不发命令。
   - `CancelTargeting()`：非指向态为空操作。
   - 指向中再次 `Begin*`：先隐式取消旧的（触发 `TargetingCancelled`）再开新的。
   - 未 `Initialize` 调 `Begin*` 抛 `InvalidOperationException`；origin provider 为 null 抛 `ArgumentNullException`。
   - **控制器不读 `MatchState`、不持 `TargetRule`、不过滤目标种类**——需要目标的卡牌由 RuleEngine 以 `TargetRequired`/`InvalidTarget` 兜底，指向模式只负责"选一个实体"。

5. **`UnityInputSource`** — `IInputSource` 生产实现：`Input.mousePosition` / `GetMouseButtonDown(0)` / `GetMouseButtonDown(1) || GetKeyDown(Escape)`。薄适配，无单测（`Input.*` 静态不可在 EditMode 驱动；Presentation 本就排除在 coverage 外）。

### 3.2 新增测试（`7_Tests/EditMode/Presentation/`，目标 ≥ 15 例）

- `TargetingArrowViewTests`（6 例）：屏幕中心→本地零点、中点/角度/长度计算、**画布尺寸从 800×600 改为 1920×1080 后同一屏幕点映射到新本地坐标（分辨率准确核心用例）**、Show/Hide 显隐。
- `TargetingControllerTests`（≥ 10 例）：未初始化 Begin 抛异常；Begin 后 IsTargeting 且箭头显示；Tick 非指向态空转；取消→隐藏+事件+无提交；非指向 CancelTargeting 空操作；确认命中分别产出 PlayCard/Attack/HeroPower 三种带目标命令；确认未命中保持指向；sink 拒绝→`CommandRejected` 携带错误码并退出指向；sink 接受→`CommandAccepted`；指向中再 Begin→旧模式取消事件+新模式生效；箭头起点跟随 origin provider 移动；null provider 抛 `ArgumentNullException`。

## 4. 明确不做（防止范围蔓延）

- **不做生产级命中测试**（EventSystem 射线/`Collider2D`）：`ITargetPicker` 的生产实现与场景装配属 M6-T1。
- **不做目标合法性高亮**（FR-8.6 可攻击目标/嘲讽高亮）——属 M5-T6 反馈表现；本任务 picker 不过滤、控制器不判规则。
- **不做拖拽出牌手势**（手牌拖出即进入指向的联动）：`Begin*` 由谁调用属 M6 装配；M5-T4 的 `PlayerInputController.NotifyHandCardClicked` 保持不变（无目标直出路径仍存在，供 `TargetRule.None` 卡牌使用）。
- **不做箭头美术**（贝塞尔曲线/箭头头部贴图/动画）：本任务用一条细矩形 `_line`；打磨属 M9。
- **不做触屏适配**：`IInputSource` 已为此留缝，触屏实现属后续。
- **不改规则三层任何代码**；既有 741 内核用例零改动。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle.Targeting
{
    public interface IInputSource
    {
        Vector2 PointerScreenPosition { get; }
        bool IsConfirmPressed { get; }   // 本帧左键按下（边沿）
        bool IsCancelPressed { get; }    // 本帧右键或 Esc 按下（边沿）
    }

    public interface ITargetPicker
    {
        bool TryPickTarget(Vector2 screenPosition, out TargetRef target);
    }

    public sealed class TargetingArrowView : MonoBehaviour
    {
        // [SerializeField] internal RectTransform _area, _line; float _lineWidth;
        public void Initialize(Camera? uiCamera);        // Overlay 画布传 null
        public void Show();
        public void Hide();
        public void SetEndpoints(Vector2 startScreen, Vector2 endScreen);
    }

    public sealed class TargetingController : MonoBehaviour
    {
        // [SerializeField] internal int _localPlayerId; TargetingArrowView _arrow;
        public bool IsTargeting { get; }
        public event Action? CommandAccepted;
        public event Action<CommandError>? CommandRejected;
        public event Action? TargetingCancelled;

        public void Initialize(IInputSource input, ITargetPicker picker, ICommandSink sink);
        public void BeginPlayCardTargeting(int cardInstanceId, Func<Vector2> originScreenProvider);
        public void BeginAttackTargeting(int attackerInstanceId, Func<Vector2> originScreenProvider);
        public void BeginHeroPowerTargeting(Func<Vector2> originScreenProvider);
        public void CancelTargeting();
        internal void Tick();                            // Update() 仅透传
    }

    public sealed class UnityInputSource : IInputSource { /* Input.* 薄适配 */ }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 未 `Initialize` 调 `BeginPlayCardTargeting` | 抛 `InvalidOperationException` |
| AC-2 | `Begin*` 后 | `IsTargeting==true`，箭头 `Show` |
| AC-3 | Tick 中取消按下 | 退出指向、箭头 `Hide`、`TargetingCancelled` 触发、sink 零提交 |
| AC-4 | Tick 确认且 picker 命中（三种模式） | sink 分别收到 `PlayCardCommand`/`AttackCommand`/`UseHeroPowerCommand`，`PlayerId=_localPlayerId`、`Target`=命中 `TargetRef` |
| AC-5 | Tick 确认但 picker 未命中 | 保持指向、sink 零提交 |
| AC-6 | sink 返回 `Invalid(MustTargetTaunt)` | `CommandRejected(MustTargetTaunt)` 触发、退出指向 |
| AC-7 | sink 返回 Valid | `CommandAccepted` 触发、不触发 `CommandRejected` |
| AC-8 | 指向中再次 `Begin*` | 先触发一次 `TargetingCancelled`，新模式生效 |
| AC-9 | origin provider 返回值移动后 Tick | 箭头起点同步移动（非快照） |
| AC-10 | 画布 800×600→1920×1080，`SetEndpoints` 同一屏幕点 | 换算出的本地坐标不同且与新画布几何一致（分辨率准确） |
| AC-11 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除沿用 T1 口径）。
- 最少用例数：15。
- 必须覆盖的边界：未初始化、非指向态空转、未命中保持指向、取消与确认的互斥、分辨率变化映射、origin 现算。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T5 状态 + 证据）。
- 需更新的配置：无（asmdef 引用不动）。
- 是否影响既有模块：纯增量；不改 M5-T1~T4 任何文件。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。指向中点击空白（未命中）保持指向、右键/Esc 取消、拒绝后退出由玩家重发——与炉石交互一致，作为本任务假设记录。

---

## 11. 结论与证据

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑 836 通过佐证）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（Presentation 排除在 coverage 外）；Unity EditMode 用户实跑 **836 passed / 0 failed**（815 + 21 新例，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（242 文件，+9 新文件：5 实现 + 4 测试/夹具）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（不变，规则三层零改动）。
- **修复记录**：① `UnityEngine.Input` 被同级命名空间 `Card.Presentation.Battle.Input` 遮蔽（CS0234）→ `UnityInputSource` 全限定引用并注释说明；② R5 行数超限（334>300）→ stub/夹具抽为 `TargetingTestStubs.cs`/`TargetingTestFixture.cs`；③ 可访问性不一致（CS0052/CS0060）→ fixture 与测试类统一 `internal`。
- **铁律扫描**：
  - R3/R4/R5：控制器不读 `MatchState`、不持 `TargetRule`、不过滤目标；四种 `Begin*` 最终只构造 `GameCommand` 经 `ICommandSink` 提交 ✅
  - R8：无 `Find`/`static Instance`；依赖全部 `Initialize` 显式注入，未装配抛 `InvalidOperationException` ✅
  - R10：实现 5 文件 ≤ 130 行，单方法 ≤ 15 行 ✅
  - U-8：箭头两端每次 `SetEndpoints` 经 `RectTransformUtility` 现算，分辨率变化用例（AC-10）实证 ✅
  - U-9：`IInputSource` 抽象落地，控制器不直接读 `Input.*` ✅

### 11.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P2 | 0 | — |
| P3 | 1 | 指向中每帧调用一次 `Func<Vector2>` origin provider 与一次箭头几何更新（Trig/Vector2），仅在指向态发生，量极小；不违反 U-11 的意图（热路径指常态每帧），记录备查。 |

### 11.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/Targeting/IInputSource.cs` | 输入抽象（U-9） |
| `4_Presentation/Battle/Targeting/ITargetPicker.cs` | 命中测试抽象（只识别不过滤） |
| `4_Presentation/Battle/Targeting/TargetingArrowView.cs` | 箭头视图（U-8 现算换算） |
| `4_Presentation/Battle/Targeting/TargetingController.cs` | 指向状态机（Idle↔Targeting） |
| `4_Presentation/Battle/Targeting/UnityInputSource.cs` | 生产输入薄适配 |
| `7_Tests/EditMode/Presentation/TargetingArrowViewTests.cs` | 6 例（几何/显隐/分辨率映射） |
| `7_Tests/EditMode/Presentation/TargetingControllerTests.cs` | 15 例（状态机/命令产出/事件互斥/origin 跟随） |
| `7_Tests/EditMode/Presentation/TargetingTestStubs.cs` + `TargetingTestFixture.cs` | 共享 stub 与夹具 |

### 11.4 下一步

进入 M5-T6 反馈表现（伤害数字、死亡淡出、回合横幅、音效钩子；目标高亮 FR-8.6 也归该任务）。
