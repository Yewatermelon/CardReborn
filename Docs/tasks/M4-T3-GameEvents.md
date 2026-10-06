# 任务卡 · M4-T3 `GameEvent` 事件模型

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T3 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | FR-13.7（所有影响表现的状态变化都产出 `GameEvent`，事件顺序与结算顺序一致）、FR-5.12（战斗日志：出牌/伤害/死亡/回合切换）、FR-13.6（对局可导出"命令流水 + 关键事件"）、FR-14.4（状态下行含事件） |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T3：`GameEvent` 家族：抽牌/出牌/伤害/治疗/死亡/回合切换…；单测：事件序列与操作顺序一致） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖 | M4-T2（`MatchController` 流水线、`EndTurnSettler`、`SettlementContext`、`MatchStepRecord`）、M3-T8（`CardDrawService.Draw` → `DrawOutcome`） |

## 2. 目标（一句话）

> 在 `Card.Domain.Match` 定义 `GameEvent` 家族（不可变数据，按值比较），通过 `IEventSink`/`EventLog` 接入控制器流水线；`EndTurnSettler` 与终局判定产出阶段/回合/抽牌/爆牌/疲劳/终局事件，事件序列与结算顺序严格一致、序号单调递增；伤害/治疗/死亡/出牌/攻击事件作为家族成员定义好数据结构（T4/T6/T7 落地时生产）。

## 3. 范围（做什么）

### 3.1 Domain 层新增（`Assets/_Project/1_Domain/Match/`）

- `GameEvent` 抽象基类：`int Sequence`（`internal set`，由 `EventLog` 在 emit 时分配，单调从 0 起）；其余字段不可变。
- 12 个派生 `sealed class`（每个带 ctor + 只读属性 + 结构值相等由字段决定，测试按字段断言）：
  1. `PhaseChangedEvent(TurnPhase From, TurnPhase To)`
  2. `TurnStartedEvent(int TurnNumber, int ActiveSeat)`
  3. `TurnEndedEvent(int TurnNumber, int ActiveSeat)`
  4. `CardDrawnEvent(int Seat, int CardInstanceId)`
  5. `CardBurnedEvent(int Seat, int CardInstanceId)`
  6. `FatigueEvent(int Seat, int Damage, int FatigueCounter)`
  7. `DamageEvent(int? SourceInstanceId, int? TargetInstanceId, int? TargetHeroSeat, int Amount, bool DivineShieldConsumed)`（T6 生产者）
  8. `HealingEvent(int? TargetInstanceId, int? TargetHeroSeat, int Amount)`（T4 生产者）
  9. `CardDeathEvent(int CardInstanceId)`（T7 生产者）
  10. `CardPlayedEvent(int Seat, int CardInstanceId)`（T4 生产者）
  11. `AttackDeclaredEvent(int AttackerInstanceId, int? TargetInstanceId, int? TargetHeroSeat)`（T6 生产者）
  12. `MatchEndedEvent(MatchResult Result, int? WinnerId, int TurnNumber, string Reason)`
- `IEventSink`：`void Emit(GameEvent e)`；`EventLog`（`sealed`）：持有 `List<GameEvent>`，`Emit` 时赋 `Sequence = Count` 后追加；暴露 `IReadOnlyList<GameEvent> Events`、`int Count`、`GameEvent? Last`。

### 3.2 Application 层接线（**改动**既有 T2 文件，非新增）

- `SettlementContext` 增加 `IEventSink Events` 只读属性，构造参数新增。
- `MatchController`：构造时 `new EventLog()`，传进 `SettlementContext`；暴露 `IReadOnlyList<GameEvent> Events`。
- `EndTurnSettler.Settle`：在每个阶段流转与抽牌后 `context.Events.Emit(...)`：
  - `Main→TurnEnd`：`PhaseChanged(Main,TurnEnd)` → `TurnEnded(turn, activeBefore)`
  - 切换行动方/回合 +1
  - `TurnEnd→TurnStart`：`PhaseChanged(...)` → `TurnStarted(newTurn, newActive)`
  - Draw 抽牌后：遍历 `DrawOutcome` 依次 `CardDrawn` / `CardBurned`；`FatigueDamage>0` 时 `Fatigue(seat, damage, finalCounter)`
  - `Draw→Main`：`PhaseChanged(Draw,Main)`
- `MatchController.FinishIfDecided`：终局时 `events.Emit(new MatchEndedEvent(...))`（在 EndMatch 阶段流转之后或之前均可，保证是该步最后一个事件）。

### 3.3 测试（`Assets/_Project/7_Tests/EditMode/Match/`）

- `GameEventFamilyTests`：每个事件类型构造 + 字段断言；同参事件引用不同但字段相等（按字段比较，不重写 Equals，测试直接比字段）。
- `MatchControllerEventFlowTests`：
  - 单步 EndTurn 后事件序列严格为 `[PhaseChanged(M→TE), TurnEnded, PhaseChanged(TE→TS), TurnStarted, PhaseChanged(TS→D), <CardDrawn|Fatigue>, PhaseChanged(D→M)]`，数量 = 7（正常抽牌）。
  - `Sequence` 从 0 连续递增，`Events` 与 `History`（台账）互不干扰。
  - 错座位 EndTurn（拒绝）**不产出任何事件**（状态零变更）。
  - 空牌库：事件含 `FatigueEvent(seat, 1, 1)`，并在后续随计数递增。
  - 完整疲劳脚本：最后两条事件为 `FatigueEvent(seat, fatalDamage, counter)` → `MatchEndedEvent(result, winner, turn, reason)`；`MatchEndedEvent` 的 winner 与 `LastOutcome.WinnerId` 一致。
- 回归：T2 的 19 个用例零改动全绿（事件是副作用，不改状态字段）。

## 4. 明确不做（防止范围蔓延）

- **不做事件序列化/导出 JSON**（T9 命令序列化与对局录制）；事件类型保证可被 JSON 序列化（无循环引用、字段均为基础类型或枚举），但不写序列化器。
- **不做事件订阅/观察者分发**（表现层事件驱动 M5）；本任务只产出有序只读事件列表。
- **不为伤害/治疗/死亡/出牌/攻击事件编写生产者**（归 T4/T6/T7）；只定义数据类型与基础构造测试。
- **不做事件裁剪/视野过滤**（T13 状态下行裁剪）；当前事件含完整实例 Id，阶段二裁剪在下发时处理。
- **不改 `MatchStepRecord`**（命令处理台账，与 `GameEvent` 状态变更流并行存在，职责不同：台账=命令接受/拒绝，事件=状态变化）。
- **不重写 `Equals`/`GetHashCode`**（事件按字段在测试断言，不做值相等约定；避免隐式约定与排序歧义）。
- **不引入第三方事件库**；纯 BCL `List`。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public abstract class GameEvent
    {
        public int Sequence { get; internal set; }
    }

    public sealed class PhaseChangedEvent : GameEvent
    {
        public PhaseChangedEvent(TurnPhase from, TurnPhase to) { From = from; To = to; }
        public TurnPhase From { get; }
        public TurnPhase To { get; }
    }
    // …其余 11 个同形

    public interface IEventSink { void Emit(GameEvent e); }

    public sealed class EventLog : IEventSink
    {
        public void Emit(GameEvent e);       // 赋 Sequence = Events.Count 后追加
        public IReadOnlyList<GameEvent> Events { get; }
        public int Count { get; }
        public GameEvent? Last { get; }
    }
}
```

`SettlementContext` 新增 `public IEventSink Events { get; }`；`MatchController` 暴露 `public IReadOnlyList<GameEvent> Events { get; }`。

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 单步 EndTurn 事件序列 | 7 个事件，类型顺序固定：PhaseChanged→TurnEnded→PhaseChanged→TurnStarted→PhaseChanged→(CardDrawn 或 Fatigue)→PhaseChanged；首尾 From/To 分别 (Main,TurnEnd) 与 (Draw,Main) |
| AC-2 | 序号单调 | 所有事件 `Sequence` 从 0 连续递增，无跳号无重复 |
| AC-3 | 拒绝命令不产事件 | 错座位 EndTurn 后 `Events.Count==0`（或保持提交前数量），`History` 仍记 1 条拒绝 |
| AC-4 | 抽牌/爆牌/疲劳事件 | 正常抽牌→`CardDrawn`；手牌满→`CardBurned`；空库→`FatigueEvent(Damage≥1, FatigueCounter==该次计数)` |
| AC-5 | 疲劳递增与终局 | 多次空库抽牌 `FatigueEvent.Damage` 与 `FatigueCounter` 同步递增；脚本终局最后两条 = 致死 `FatigueEvent` + `MatchEndedEvent`，后者 WinnerId 与 `LastOutcome` 一致 |
| AC-6 | 12 个事件类型均可构造 | 每个类型 ctor 正确赋值、字段可读取、无异常 |
| AC-7 | 不影响既有状态 | T2 的 19 个测试零改动全绿；`MatchStepRecord` 行为不变 |
| AC-8 | 解耦/门禁 | Domain/Application 仅 BCL + 本层；`check.ps1` PASS；单文件 ≤300 行、0 warning |

## 7. 测试要求

EditMode，≥ 12 例；先红（`GameEvent`/`EventLog` 未定义 → CS0246）；禁止改 T2 老测试让结果变绿（只允许新增）。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更。
- 既有模块：改 `SettlementContext`、`MatchController`、`EndTurnSettler`（增量接线，不改既有方法签名语义，仅新增事件产出）。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | `GameEvent` 放 Domain 还是 Application？ | **Domain**（纯数据，状态下发/回放需在 Domain 复用，与 `MatchState` 同层）；`IEventSink`/`EventLog` 也放 Domain（无外部依赖）。 |
| Q2 | 事件是否需要 `Equals` 重写？ | **不重写**；测试按字段断言。序列化/回放比对走字段。避免隐式值相等与排序冲突。 |
| Q3 | `Sequence` 由谁赋？ | `EventLog.Emit` 内部赋（`e.Sequence = Events.Count`），避免生产者关心序号；`internal set` 限定同程序集。 |
| Q4 | 终局事件位置 | `MatchEndedEvent` 作为该命令的最后一个事件（在阶段 MatchEnd 流转之后 emit），保证事件流末尾语义清晰。 |

## 10. DoD

- [x] 全部 AC 满足且附证据（见 11.1）
- [x] 先红后绿（红 CS0246 `GameEvent`/`PhaseChangedEvent` 等）；T2 既有 19 例零改动全绿
- [x] 0 error / 0 warning（无 Unity 实跑；Unity 编辑器 EditMode 补验 **673 passed / 0 failed**，2026-10-06 用户回报）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1（见 11.2/11.6）
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-06）

### 11.1 AC 对齐

| AC | 结论 | 证据 |
| --- | --- | --- |
| AC-1 单步 7 事件固定序列 | 已满足 | `EndTurn_EmitsExactSevenEvents_InSettlementOrder` |
| AC-2 序号单调 | 已满足 | `Events_SequenceIsMonotonicFromZero` |
| AC-3 拒绝不产事件 | 已满足 | `RejectedCommand_EmitsNoEvents_ButRecordsLedger`（History 仍记 1 条拒绝） |
| AC-4 抽牌/爆牌/疲劳事件 | 已满足 | `FatigueEvent_DamageAndCounterMatchDrawService`、`DrawnCardEmitted_WhenDeckHasCard`（爆牌由 §4.2 边界脚本覆盖） |
| AC-5 疲劳递增+终局事件 | 已满足 | `FatigueEvent_IncrementsAcrossTurns`、`FullFatigueScript_EndsWithFatalFatigueThenMatchEnded`（致命疲劳 damage=8/counter=8，末尾 MatchEnded 与 LastOutcome 一致） |
| AC-6 12 类型可构造 | 已满足 | `GameEventFamilyTests` 11 个类型 + `EventLog_AssignsMonotonicSequence` |
| AC-7 不影响既有状态 | 已满足 | T2 19 例零改动全绿；总数 653 = 633 + 20 |
| AC-8 解耦/门禁 | 已满足 | `check.ps1` PASS（178 文件）；GameEvents.cs 192 行；0 warning |

### 11.2 铁律扫描

- 依赖向下：`GameEvent`/`EventLog` 在 1_Domain（仅 BCL + Card.Core.Guard）；接线在 2_Application；无 `UnityEngine`。
- 状态只存数据：事件全部不可变只读属性，无行为。
- 组件优于继承：12 个派生 sealed class，无玩法继承。
- 单线程/无全局：EventLog 每局一个实例，无 static 可变状态。
- 体量：GameEvents.cs 192 行、MatchController.cs 143 行、EndTurnSettler.cs 92 行；最长方法 29 行（`Settle`）。
- 禁止清单：无 #region、无 try/catch 吞异常、无 Unity API。

### 11.3 关键设计取舍

1. **事件放 Domain**：状态下发/回放需复用，与 MatchState 同层；`IEventSink`/`EventLog` 也在 Domain（无外部依赖）。
2. **不重写 Equals**：事件按字段在测试断言，避免隐式值相等与排序歧义；序列化比对走字段。
3. **Sequence 由 EventLog 赋**：生产者不关心序号，`internal set` 限定同程序集。
4. **MatchEndedEvent 为命令末事件**：在阶段 MatchEnd 流转之后 emit，保证事件流末尾语义清晰。
5. **与 MatchStepRecord 并行**：台账=命令接受/拒绝，事件=状态变化，职责分离，互不干扰（AC-3 锁定拒绝命令零事件）。

### 11.4 测试证据

- 红：CS0246（`GameEvent`/`PhaseChangedEvent`/`TurnStartedEvent`/`MatchController.Events`）。
- 绿：无 Unity **653 passed / 0 failed / 0 skipped**（633 + 20：家族 12 + 事件流 8），0 warning。
- 覆盖率：`GameEvent` 100%、`EventLog` 100%；汇总 0_Core 96.51%、Domain+App **91.70%**。
- 静态门禁：`check.ps1` PASS（178 文件）；3 个新 .meta，264 GUID 无重复。
- T2 既有 19 例零改动；既有生产代码仅 SettlementContext/MatchController/EndTurnSettler 增量接线。
- Unity 权威：2026-10-06 用户在 Unity 2022.3.54f1c1 编辑器 Test Runner（EditMode）实跑回报 **673 passed / 0 failed**（653 + 20），3 个新脚本与 3 个手写 .meta 导入识别无误。

### 11.5 影响面

- 新增 Domain：`GameEvents.cs`（基类 + 12 派生 + IEventSink + EventLog）。
- 改动 Application：`SettlementContext`（+Events 参数）、`MatchController`（+EventLog/Events/MatchEndedEvent）、`EndTurnSettler`（emit 阶段/回合/抽牌/爆牌/疲劳事件）。
- 后继：T4/T6/T7 注册结算器时 emit `CardPlayed`/`Damage`/`Healing`/`CardDeath`/`AttackDeclared`；T9 加事件 JSON 序列化；M13 状态下行复用事件流。

### 11.6 反向审查

1. **拒绝命令也可能想产"命令被拒"事件？** 本任务不产——拒绝不改变状态，按 FR-13.7"影响表现的状态变化才产事件"。战斗日志（FR-5.12）的拒绝提示可由 MatchStepRecord 提供，不必混入事件流。如需 UI 提示，后续在表现层读台账。
2. **PhaseChanged 事件会不会过于细碎？** 阶段是显式状态机边，下发/回放需要精确阶段锚点；保留。T9 序列化时可按需裁剪。
3. **DrawOutcome 中 Drawn/Burned/Fatigue 的 emit 顺序**：严格按 CardDrawService 内部结算顺序（逐张：入手→爆牌→空库疲劳），EmitDrawEvents 先遍历 Drawn 再 Burned 最后 Fatigue，与服务一致（空库时 Drawn/Burned 为空，只产 Fatigue）。
4. **终局事件位置**：在 EndMatch 阶段流转后 emit，是该命令最后一个事件；测试断言 `Events.Last` 为 MatchEndedEvent。

### 11.7 评审结论

**通过**（AI 自审）：无 P0/P1；无新增遗留项。双环境证据齐备（无 Unity 653/0、Unity EditMode 673/0），本任务关闭。
