# 任务卡 · M5-T6 反馈表现

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T6 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.2（出牌/攻击/死亡动画，可被设置项加速/关闭）、FR-8.6（可出牌/可攻击目标/嘲讽高亮）、FR-8.5（音效钩子，音频资源本体属 P1）、03 §5.6（表现层只读）、§5.8（伤害数字/特效必须走池）、U-3（Update 只做表现插值）、U-11（热路径零分配） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T6：伤害数字、死亡淡出、回合横幅、音效钩子；完成标准：每类事件都有反馈；可加速/关闭） |
| 预估 | 1 会话 |
| 依赖任务 | M5-T2（区视图 + `MatchEventPump`）、M5-T3（`CardViewPool`）、M4-T3（12 类 `GameEvent`） |

## 2. 目标（一句话)

> 交付反馈子系统：订阅权威事件流，把 12 类 `GameEvent` 映射为浮动伤害/治疗数字、随从死亡淡出、回合/终局横幅与音效钩子，每类反馈可独立开关、整体可加速；另交付 FR-8.6 高亮机制（可出牌/可攻击/嘲讽三态视图区分），高亮数据由外部以 id 集合喂入，表现层不判规则。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/Feedback/`）

1. **`FeedbackSettings`**（纯 C# 可变设置，引用共享、即时生效）：
   - `Speed`（默认 1，setter 钳制 ≥ 0.01f）：所有时长实际值 = 基准时长 / Speed；
   - `DamageNumbersEnabled` / `DeathFadeEnabled` / `TurnBannerEnabled` / `AudioEnabled` 四个独立开关（默认全开）；
   - `internal float ScaleDuration(float baseSeconds)`。

2. **`AudioCue`（枚举）**：`CardPlayed / AttackDeclared / DamageDealt / HealingReceived / MinionDeath / TurnStarted / CardDrawn / CardBurned / FatigueDamage / Victory / Defeat / MatchDraw`。

3. **`IAudioCuePlayer`（接口）**：`void Play(AudioCue cue)`。音效钩子本体；生产实现（AudioSource/混音）属 M6/M9，测试用录音 stub。

4. **`CardHighlight`（[Flags] 枚举，命名空间 `Card.Presentation.Battle`）**：`None=0 / Playable=1 / Attackable=2 / Taunt=4`。

5. **`FloatingTextView`（MonoBehaviour）**：单个浮动文字（伤害/治疗/疲劳数字）。
   - `[SerializeField] internal TMP_Text _text`、`_risePerSecond = 60f`；
   - `Show(string content, Color color, float duration)`：记录当前 `anchoredPosition` 为起点，置文本/颜色/alpha=1/激活；
   - `internal void Tick(float dt)`（`Update` 透传 `Time.deltaTime`，U-3）：随时间上浮并按剩余比例衰减 alpha；到时 `SetActive(false)`；
   - `bool IsPlaying`、`internal float Duration`（供测试断言加速语义）。

6. **`FloatingTextPool`（internal，§5.8 落地）**：复用 M1-T6 `ObjectPool<T>`；`Rent(Transform parent)` / `Return(view)` / `ReclaimFinished()`（把 `!IsPlaying` 的在借对象批量归还）/ `CreatedCount` / `ActiveCount`。预热 4。

7. **`TurnBannerView`（MonoBehaviour）**：`Show(string content, float duration)` / `Hide()` / `internal Tick(dt)` 自动隐藏 / `IsPlaying` / `internal float Duration`。用 SetActive 切显隐，不做渐变（美术打磨属 M9）。

8. **`CardFadeOutView`（MonoBehaviour，死亡淡出）**：挂到将死随从视图所在 GameObject；`Play(float duration)` 起播，Tick 中把 `CanvasGroup.alpha` 从 1 渐到 0；`Stop()` 中止；`IsPlaying`。`CanvasGroup` 缺失时 `Play` 内现取/现加。

9. **`IFeedbackTargetLocator`（接口）**：事件载荷（InstanceId/座位）→ 视图定位。
   - `bool TryGetMinionAnchor(int instanceId, out Vector3 worldPosition)`；
   - `bool TryGetHeroAnchor(int seat, out Vector3 worldPosition)`；
   - `bool TryGetMinionView(int instanceId, out CardView view)`；
   - 生产实现（遍历双方 `BoardView.Children` 按 `CardView.InstanceId` 匹配）属 M6 装配；未命中返回 false，播放器跳过该条视觉反馈但不影响音效。

10. **`BattleFeedbackPlayer`（纯 C# 编排器）**：
    - 构造注入：`(IFeedbackTargetLocator, FloatingTextPool, Transform floatingTextParent, TurnBannerView banner, IAudioCuePlayer audio, FeedbackSettings settings, int localSeat)`；
    - `Bind(MatchEventPump)` / `Unbind()`（订阅/退订 `EventAppended`）；`Handle(GameEvent e)` 直接喂事件（测试主路径）；
    - 事件映射（每类事件都有反馈；`PhaseChangedEvent`/`TurnEndedEvent` 显式无反馈——前者由 `TurnStarted` 承载、后者无信息增量，在此登记为设计决定）：

      | 事件 | 视觉 | 音效 |
      | --- | --- | --- |
      | `DamageEvent` | 目标处浮动红色 `-N`（随从按 InstanceId、英雄按座位定位；未命中跳过） | `DamageDealt` |
      | `HealingEvent` | 目标处浮动绿色 `+N` | `HealingReceived` |
      | `FatigueEvent` | 该座位英雄处浮动红色 `-N` | `FatigueDamage` |
      | `CardDeathEvent` | 命中随从视图 `CardFadeOutView.Play` | `MinionDeath` |
      | `TurnStartedEvent` | 横幅：`ActiveSeat==localSeat` → "你的回合"，否则 "对手回合" | `TurnStarted` |
      | `MatchEndedEvent` | 横幅：Draw→"平局"；`WinnerId==localSeat`→"胜利！"；否则 "败北" | `Victory/Defeat/MatchDraw` |
      | `CardPlayedEvent` | —（手牌/战场刷新由状态驱动，M5-T2 已覆盖） | `CardPlayed` |
      | `AttackDeclaredEvent` | —（攻击位移表现属 M9 打磨） | `AttackDeclared` |
      | `CardDrawnEvent` | — | `CardDrawn` |
      | `CardBurnedEvent` | — | `CardBurned` |

    - 开关语义：对应开关关闭 → 跳过该类视觉/全部音效；Speed 只缩放时长，不改反馈有无；
    - 基准时长常量：`FloatingText 1.2s` / `DeathFade 0.8s` / `Banner 1.6s`（internal，测试断言加速后 = 基准/2）。

11. **`CardHighlightDriver`（纯 C#）**：FR-8.6 机制。
    - `void Apply(IReadOnlyList<CardView> views, IReadOnlyCollection<int> playableIds, IReadOnlyCollection<int> attackableIds, IReadOnlyCollection<int> tauntIds)`；
    - 按 `CardView.InstanceId` 查三个 id 集合（调用方传 `HashSet<int>`，O(1) 成员判断），组合出 `CardHighlight` 写入视图；`InstanceId==null` 的视图恒 `None`；
    - **id 集合的计算（合法行动枚举）不属本任务**——表现层不判规则，集合由 M5-T8 只读视图模型/权威侧下发，本任务交付机制与视觉区分。

### 3.2 改造既有文件（4 个，均为加法式改动）

1. **`ICardViewData`**：新增 `int? InstanceId { get; }`（配置态 null，局内实例态为实例 Id；PVP JSON 可序列化）。
2. **`CardViewData`**：ctor 尾部加可选参数 `int? instanceId = null`（既有调用零改动）；`FromInstance` 传 `instance.InstanceId`。
3. **`CardView`**：新增 `[SerializeField] internal GameObject _playableHighlight/_attackableHighlight/_tauntHighlight` + `SetHighlight(CardHighlight)`（按位切三个面板显隐）+ `int? InstanceId { get; private set; }`（`SetData` 时从数据快照同步）。
4. **`CardViewPool.Return`**：归还时复位——`SetHighlight(None)`、停止 `CardFadeOutView`、`CanvasGroup.alpha` 归 1（§5.8 第 2 条"归还时清理状态"）。

### 3.3 新增测试（`7_Tests/EditMode/Presentation/`，目标 ≥ 30 例）

- `FeedbackTestStubs.cs`：`StubAudioCuePlayer`（录音 cue 列表）、`StubFeedbackTargetLocator`（可配命中/坐标/视图）。
- `FeedbackTestPrefabs.cs`：代码组装 `FloatingTextView` / `TurnBannerView` 测试预制。
- 改造 `PresentationTestPrefabs.CreateCardViewPrefab`：补三个高亮面板 + `CanvasGroup`（池复位的依赖）。
- `FeedbackSettingsTests`（3 例）：Speed 钳制、ScaleDuration 加速语义、默认全开。
- `FloatingTextViewTests`（5 例）：Show 置文本/颜色/alpha/激活；Tick 半程 alpha≈0.5 且位置上浮；到时自动隐藏且 IsPlaying=false；闲置 Tick 空转；再 Show 重新起播（位置重置）。
- `FloatingTextPoolTests`（4 例）：Rent 激活/Return 禁用归位；ReclaimFinished 只收播完的；峰值后复用（CreatedCount 不涨）。
- `TurnBannerViewTests`（4 例）：Show 显文本；Tick 到时自隐；Hide 立隐；播中再 Show 重计时。
- `CardFadeOutViewTests`（3 例）：Play 渐隐至 0；Stop 中止保持当前 alpha；缺 CanvasGroup 自动补。
- `CardViewHighlightTests`（5 例）：None 全隐；三旗标各自显示对应面板；组合旗标并存；SetData 同步 InstanceId（含 null）。
- `CardHighlightDriverTests`（4 例）：playable/attackable/taunt 各命中写旗标；未命中与 null Id 归 None。
- `BattleFeedbackPlayerTests`（≥ 12 例）：伤害→随从锚点红字 + cue；伤害→英雄；治疗绿字；疲劳→英雄；死亡→目标视图起播淡出；回合横幅我方/敌方文案；终局胜/负/平文案 + cue；出牌/攻击/抽牌/爆牌 cue；四类开关各自屏蔽；Speed=2 时长减半；locator 未命中跳过视觉仍播音效；Bind 泵集成（EventLog 追加 → Pump → 收到反馈）。

## 4. 明确不做（防止范围蔓延）

- **不接真实音频资源**：`IAudioCuePlayer` 生产实现、`AudioClip` 资源、音量调节（FR-8.5 P1 部分）属 M6/M9。
- **不做合法行动枚举**：可出牌/可攻击/嘲讽 id 集合的计算属规则层查询/M5-T8 只读视图模型；本任务只消费外部给定集合。
- **不做生产级 `IFeedbackTargetLocator`**：双 BoardView 遍历装配属 M6-T1；测试用 stub。
- **不做攻击位移/碰撞帧、卡牌入场动画、横幅渐显渐隐美术**：M9 打磨。
- **不做伤害数字对象池的容量上限策略细化**：沿用 `ObjectPool<T>` 默认（无上限），与 `CardViewPool` 口径一致。
- **不改规则三层任何代码**；既有 741 内核用例零改动。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle
{
    [Flags] public enum CardHighlight { None = 0, Playable = 1, Attackable = 2, Taunt = 4 }

    public interface ICardViewData { /* 既有成员 */ int? InstanceId { get; } }

    public sealed class CardView : MonoBehaviour
    {
        public int? InstanceId { get; }
        public void SetHighlight(CardHighlight highlight);
    }
}

namespace Card.Presentation.Battle.Feedback
{
    public sealed class FeedbackSettings
    {
        public float Speed { get; set; }                  // ≥ 0.01f
        public bool DamageNumbersEnabled { get; set; }    // 默认 true
        public bool DeathFadeEnabled { get; set; }        // 默认 true
        public bool TurnBannerEnabled { get; set; }       // 默认 true
        public bool AudioEnabled { get; set; }            // 默认 true
    }

    public enum AudioCue { CardPlayed, AttackDeclared, DamageDealt, HealingReceived,
        MinionDeath, TurnStarted, CardDrawn, CardBurned, FatigueDamage, Victory, Defeat, MatchDraw }

    public interface IAudioCuePlayer { void Play(AudioCue cue); }

    public sealed class FloatingTextView : MonoBehaviour
    {
        public bool IsPlaying { get; }
        public void Show(string content, Color color, float duration);
        internal void Tick(float dt);
    }

    public sealed class TurnBannerView : MonoBehaviour
    {
        public bool IsPlaying { get; }
        public void Show(string content, float duration);
        public void Hide();
        internal void Tick(float dt);
    }

    public sealed class CardFadeOutView : MonoBehaviour
    {
        public bool IsPlaying { get; }
        public void Play(float duration);
        public void Stop();
        internal void Tick(float dt);
    }

    public interface IFeedbackTargetLocator
    {
        bool TryGetMinionAnchor(int instanceId, out Vector3 worldPosition);
        bool TryGetHeroAnchor(int seat, out Vector3 worldPosition);
        bool TryGetMinionView(int instanceId, out CardView view);
    }

    public sealed class BattleFeedbackPlayer
    {
        public BattleFeedbackPlayer(IFeedbackTargetLocator locator, FloatingTextPool textPool,
            Transform floatingTextParent, TurnBannerView banner, IAudioCuePlayer audio,
            FeedbackSettings settings, int localSeat);
        public void Bind(MatchEventPump pump);
        public void Unbind();
        public void Handle(GameEvent e);
    }

    public sealed class CardHighlightDriver
    {
        public void Apply(IReadOnlyList<CardView> views, IReadOnlyCollection<int> playableIds,
            IReadOnlyCollection<int> attackableIds, IReadOnlyCollection<int> tauntIds);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `DamageEvent(目标随从)` | 该随从锚点处浮出 `-N` 红字，cue=`DamageDealt` |
| AC-2 | `DamageEvent(目标英雄)` / `FatigueEvent` | 对应座位英雄锚点浮字 |
| AC-3 | `HealingEvent` | 目标处浮出 `+N` 绿字，cue=`HealingReceived` |
| AC-4 | `CardDeathEvent` | 命中随从视图 `CardFadeOutView.IsPlaying==true`，cue=`MinionDeath` |
| AC-5 | `TurnStartedEvent(ActiveSeat==localSeat / 否则)` | 横幅 "你的回合" / "对手回合"，cue=`TurnStarted` |
| AC-6 | `MatchEndedEvent` 胜/负/平 | 横幅 "胜利！"/"败北"/"平局"，cue=`Victory`/`Defeat`/`MatchDraw` |
| AC-7 | `CardPlayed/AttackDeclared/CardDrawn/CardBurned` | 各发对应 cue，无视觉副作用 |
| AC-8 | `DamageNumbersEnabled=false` | 伤害/治疗/疲劳不出浮字，cue 仍发 |
| AC-9 | `DeathFadeEnabled=false` / `TurnBannerEnabled=false` / `AudioEnabled=false` | 对应反馈全部跳过 |
| AC-10 | `Speed=2` 时伤害事件 | 浮字 `Duration == 1.2f/2`（横幅/淡出同理减半） |
| AC-11 | locator 全部未命中 | 不抛异常、无浮字无淡出，cue 正常发 |
| AC-12 | 高亮：给定 playable/attackable/taunt id 集合 | 命中视图显示对应高亮面板，未命中与 `InstanceId==null` 视图全隐 |
| AC-13 | `CardViewPool.Return` | 归还后高亮清零、alpha=1、淡出停止 |
| AC-14 | 泵集成：EventLog 追加事件后 `Pump()` | 播放器按序产出反馈 |
| AC-15 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`，Presentation 目录，coverage 排除沿用 T1 口径）。
- 最少用例数：30。
- 必须覆盖的边界：开关全关组合、Speed 边界钳制、locator 未命中、播中重播、池峰值复用、泵集成顺序。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T6 状态 + 证据）。
- 需更新的配置：无（asmdef 引用不动——`BattleFeedbackPlayer` 只引 `Card.Domain.Match` 事件类型与 `UnityEngine`，均在既有引用内）。
- 是否影响既有模块：`CardView`/`CardViewData`/`ICardViewData`/`CardViewPool` 加法式改动，`PresentationTestPrefabs` 夹具补字段；M5-T1~T5 测试语义不变。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- `PhaseChangedEvent`/`TurnEndedEvent` 无专属反馈（阶段流转噪声由 `TurnStarted` 横幅承载）——按最小影响方案推进，登记为设计决定；若后续需要阶段指示器，属 M9 UI 打磨。

---

## 11. 结论与证据

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑 886 通过佐证）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（规则三层零改动，Presentation 排除在 coverage 外）；Unity EditMode 用户实跑 **886 passed / 0 failed**（836 + 50 新例，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（264 文件，+22：10 实现 + 12 测试/夹具改动含新增）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（不变）。
- **修复记录**：① `IReadOnlyCollection<T>.Contains` 缺 `using System.Linq`（CS1061，补 using；LINQ `Contains` 对 `HashSet` 走 `ICollection<T>` 优化仍 O(1)，事件驱动路径不违 U-11）；② `FloatAtTarget` out 参数在双 null 分支编译器无法证明赋值（CS0165，`= default` 初始化）；③ EditMode 下 `SetActive(true)` 不触发 `Awake`——池克隆自已禁用预制体导致 `_rect` 为 null（13 例 NRE 同根），改惰性缓存 `Rect` 属性并注释说明。
- **铁律扫描**：
  - R4/§5.6：`BattleFeedbackPlayer` 只消费事件、只驱动视图；`CardHighlightDriver` 消费外部 id 集合，表现层零规则判断 ✅
  - R6：无硬编码数值进规则；时长/颜色为表现层常量，与规则无关 ✅
  - R8：无 `Find`/`static Instance`；全部依赖构造/`Initialize` 显式注入 ✅
  - R10：实现 10 文件均 ≤ 210 行（最大 `BattleFeedbackPlayer` ~210），单方法 ≤ 20 行，0 warning ✅
  - §5.8：浮动数字走 `FloatingTextPool`（预热 4 + 播完回收），`CardViewPool` 归还复位 ✅
  - U-3：`Update` 仅透传 `Tick(Time.deltaTime)` 表现插值，不读游戏状态 ✅
  - U-6/U-11：文本全 TMP；事件驱动路径无每帧分配（LINQ `Contains` 命中 `ICollection<T>` 快速路径无枚举器分配） ✅

### 11.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P2 | 0 | — |
| P3 | 1 | `PhaseChangedEvent`/`TurnEndedEvent` 无专属反馈（设计决定，已登记任务卡第 10 节）；若需阶段指示器属 M9。 |

### 11.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `4_Presentation/Battle/CardHighlight.cs` | [Flags] 高亮三态（FR-8.6） |
| `4_Presentation/Battle/Feedback/FeedbackSettings.cs` | 加速 + 四开关（FR-8.2） |
| `4_Presentation/Battle/Feedback/AudioCue.cs` + `IAudioCuePlayer.cs` | 音效钩子（FR-8.5 钩子） |
| `4_Presentation/Battle/Feedback/FloatingTextView.cs` + `FloatingTextPool.cs` | 浮动数字（走池） |
| `4_Presentation/Battle/Feedback/TurnBannerView.cs` | 回合/终局横幅 |
| `4_Presentation/Battle/Feedback/CardFadeOutView.cs` | 死亡淡出 |
| `4_Presentation/Battle/Feedback/IFeedbackTargetLocator.cs` | 定位抽象（生产实现属 M6） |
| `4_Presentation/Battle/Feedback/BattleFeedbackPlayer.cs` | 事件→反馈映射编排器 |
| `4_Presentation/Battle/Feedback/CardHighlightDriver.cs` | 高亮驱动（不判规则） |
| 改造：`ICardViewData`/`CardViewData`/`CardView`/`CardViewPool` | `InstanceId`、高亮面板、归还复位 |
| `7_Tests/EditMode/Presentation/`：9 测试文件 + 2 夹具（`FeedbackTestStubs`/`FeedbackTestPrefabs`）+ `PresentationTestPrefabs` 补字段 | 50 例 |

### 11.4 下一步

进入 M5-T7 `BattleLogView`（事件流滚动显示，与 `GameEvent` 一一对应）。
