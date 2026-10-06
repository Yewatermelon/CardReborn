# 任务卡 · M4-T1 `TurnStateMachine`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T1 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | FR-5.1（完整回合状态机：阶段严格按 3.2 流转，非法阶段操作被拒绝且不改变状态）、FR-13.1（规则三层只依赖 BCL）、FR-13.3（规则纯逻辑可测）、NFR-4 |
| 规则依据 | [01 第 3.2 节](../01-开发需求文档.md)（回合阶段表）、[02 M4 任务表](../02-开发计划步骤文档.md)（M4-T1：显式状态类 MatchStart/TurnStart/Draw/Main/TurnEnd/MatchEnd；单测：阶段顺序、非法阶段操作被拒）、[03 第 5.5 节](../03-开发规范文档.md)（状态机只负责阶段流转，阶段内业务交给服务） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T3（`Card.Core.StateMachine<TState>`：显式注册/合法边/非法转移失败不回滚）、M3-T1（`Card.Domain.Match.TurnPhase` 六阶段枚举 + `MatchState.Phase`）、M3-T6（`RuleEngine` 以 `state.Phase == Main` 作为命令阶段校验） |

## 2. 目标（一句话）

> 在 `Card.Application.Match` 提供 `TurnStateMachine`：以 M1-T3 的通用状态机为内核，按 Docs/01 §3.2 声明六个回合阶段与全部合法转移边，驱动阶段严格按序流转；每次成功转移同步写回 `MatchState.Phase`，非法跳转返回失败且**机器状态与对局状态都不改变**。

## 3. 范围（做什么）

- 新增 `Assets/_Project/2_Application/Match/TurnStateMachine.cs`（sealed，命名空间 `Card.Application.Match`，只依赖 BCL + `Card.Core` + `Card.Domain.Match`）：
  - 构造：`TurnStateMachine(MatchState state)`；以 `state.Phase` 为当前阶段，注册全部六个阶段与合法边；
  - 合法边（Docs/01 §3.2）：
    - 正常顺序 `MatchStart → TurnStart → Draw → Main → TurnEnd → TurnStart`（`TurnEnd → TurnStart` 是回合回环，下一回合的开始）；
    - 终局边：**任意非终局阶段 → `MatchEnd`**（疲劳致死等可发生在任意阶段）；`MatchEnd` 为终点，无任何出边（含自身）；
  - `TurnPhase CurrentPhase { get; }`：机器当前阶段；
  - `bool CanMoveTo(TurnPhase target)`：无副作用查询是否存在合法边；
  - `Result MoveTo(TurnPhase target)`：合法 → 经 Core 状态机转移，成功后把 `MatchState.Phase` 写为目标阶段；非法 → 失败（沿用 `StateMachine<TState>.ErrorIllegalTransition`），两状态均不变；
  - `Result Advance()`：沿唯一正常后继推进（成功序见上；`TurnEnd` 的后继是 `TurnStart`）；在 `MatchEnd` 调用返回失败；
  - `Result EndMatch()`：`MoveTo(MatchEnd)` 的语义化入口（终局判定本身仍归 `MatchEvaluator`，本方法只做阶段流转）。
- EditMode 测试 `Assets/_Project/7_Tests/EditMode/Match/TurnStateMachineTests.cs`：阶段顺序、回环、非法跳转被拒且不改状态、终局边与终点、从既有 `Main` 对局构造、与 `RuleEngine` 阶段校验的集成、无业务副作用。

## 4. 明确不做（防止范围蔓延）

- **不做任何阶段内业务**：不加/回法力水晶、不重置攻击次数、不抽牌/疲劳、不切换 `ActivePlayerId`、不自增 `TurnNumber`、不触发 `OnTurnStart/OnTurnEnd`——分别归 M4-T8（法力与技能）、M3-T8 的 `CardDrawService`（由 M4-T2 控制器在 Draw 阶段调用）、M4-T5（触发链）；本任务只交付"阶段流转骨架"。
- **不做终局判定**：不读写 `MatchState.IsFinished`、不判定英雄生命（`MatchEvaluator` 职责，M3-T7 已交付）。
- **不改 `RuleEngine` / `MatchFactory` / `MatchState` / `TurnPhase`**：既有 588 用例零改动；`MatchFactory` 当前直接产出 `Main` 阶段对局，本任务保证从该形态可构造机器，控制器接线在 M4-T2。
- **不做 `GameEvent` / 事件收集**（M4-T3）：本任务不发任何事件。
- **不做换牌（Mulligan）**：Docs/01 §3.2 标注为 P1 可选。
- **不做 `IStateHandler` 注入接缝**：当前无阶段业务，按"需要时再抽象"（03 §5.1）；M4-T2 接业务时再开。
- **不做条件/通配转移、转移历史、线程安全、自动回滚**（与 M1-T3 口径一致）。

## 5. 接口约定

```csharp
namespace Card.Application.Match
{
    using Card.Core;
    using Card.Domain.Match;

    /// <summary>
    /// 回合阶段流转（M4-T1；Docs/01 §3.2）：声明六阶段与合法边，
    /// 成功转移同步写回 MatchState.Phase。只负责阶段合法性，不含阶段业务。
    /// </summary>
    public sealed class TurnStateMachine
    {
        public TurnStateMachine(MatchState state);

        /// <summary>机器当前阶段（成功转移后与 state.Phase 一致）。</summary>
        public TurnPhase CurrentPhase { get; }

        /// <summary>无副作用查询当前是否存在到目标阶段的合法边。</summary>
        public bool CanMoveTo(TurnPhase target);

        /// <summary>按声明边转移；非法返回失败且机器/对局状态均不变。</summary>
        public Result MoveTo(TurnPhase target);

        /// <summary>沿正常顺序推进到唯一后继（TurnEnd 回到 TurnStart）；MatchEnd 无后继，返回失败。</summary>
        public Result Advance();

        /// <summary>进入终局阶段（任意非终局阶段合法；终局判定归 MatchEvaluator）。</summary>
        public Result EndMatch();
    }
}
```

正常后继序（机器内唯一的顺序真相）：

```
MatchStart → TurnStart → Draw → Main → TurnEnd → TurnStart（回环）
任意非终局阶段 → MatchEnd；MatchEnd 无出边
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 以 `MatchStart` 状态构造 | `CurrentPhase == MatchStart`；六阶段均在机内（合法边查询可用） |
| AC-2 | `Advance` 连续推进 | 严格按 `MatchStart→TurnStart→Draw→Main→TurnEnd→TurnStart` 变化；`TurnEnd` 后回到 `TurnStart`（回环） |
| AC-3 | 每次成功转移 | 返回 `Result.Success()`，且 `MatchState.Phase` 与 `CurrentPhase` 同步为目标阶段 |
| AC-4 | 合法终局边 | 从每个非终局阶段 `EndMatch()` / `MoveTo(MatchEnd)` 均成功，两状态为 `MatchEnd` |
| AC-5 | 非法跳转（跳阶段/倒退）被拒 | `MatchStart→Main`、`MatchStart→Draw`、`Draw→TurnEnd`、`Main→TurnStart`、`TurnStart→MatchStart` 均返回失败码 `ERROR_STATE_ILLEGAL_TRANSITION`；`CurrentPhase` 与 `state.Phase` 均保持原值 |
| AC-6 | 未声明的自转移 | `Main→Main`、`Draw→Draw` 被拒（Core 口径：自转移也须显式声明） |
| AC-7 | `MatchEnd` 为终点 | 在 `MatchEnd` 调 `Advance()`、`EndMatch()` 及到任意阶段的 `MoveTo` 全部失败，状态不变 |
| AC-8 | `CanMoveTo` 无副作用 | 对合法/非法/越界枚举值查询只返回 true/false，不改变阶段，不触发任何状态写入 |
| AC-9 | 失败后可恢复 | 非法转移被拒后，紧接着的合法 `MoveTo`/`Advance` 正常成功 |
| AC-10 | 从既有对局形态构造 | `MatchFactory` 产出的 `Main` 阶段对局可直接构造；`CanMoveTo(TurnEnd)`、`CanMoveTo(MatchEnd)` 为 true，其余为 false |
| AC-11 | 与 `RuleEngine` 集成 | 机器把同一 `MatchState` 带到非 `Main` 阶段后，`RuleEngine.Validate` 拒绝玩家命令（阶段不符，状态不变）；带回 `Main` 后通过结构/阶段校验 |
| AC-12 | 无业务副作用 | 任意次转移后 `ActivePlayerId`、`TurnNumber`、双方法力、`IsFinished` 全部不变 |
| AC-13 | 空引用防御 | 构造传 `null` 抛 `ArgumentNullException`（`Guard.NotNull`） |
| AC-14 | 解耦与门禁 | Application 层仅依赖 BCL + Core + Domain；`check.ps1` PASS；无 `UnityEngine`/`Debug.Log` 等 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Match`，复用 `RuleEngineTestHelpers.BuildState`）
- 最少用例数：18
- 必须覆盖的边界：完整顺序+回环、终局边（5 个起点参数化）、五类非法跳转、自转移、`MatchEnd` 终点、`CanMoveTo` 无副作用（含越界枚举值）、失败后恢复、从 `Main` 对局构造、与 `RuleEngine` 的阶段集成、业务字段零副作用、null 防御

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（任务行 + M4 计数）、本任务卡回填自检/评审结论
- 需更新的配置表：无
- 是否影响既有模块：否（新增 1 实现 + 1 测试；不改既有代码）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（先红 CS0246 × 32 → 后绿；新增 26 个用例）
- [x] 编译 0 error / 0 warning（无 Unity 工具链实跑；Unity 编辑器 Test Runner 补验 **634 passed / 0 failed**，2026-10-06 用户回报）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-06）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 MatchStart 构造 | 已满足 | `Ctor_WhenMatchStart_CurrentPhaseIsMatchStart` |
| AC-2 严格顺序 + 回环 | 已满足 | `Advance_FollowsCanonicalOrder_AndLoopsTurnEndBackToTurnStart`（连续 5 次 Advance 断言每一步） |
| AC-3 成功转移同步 state.Phase | 已满足 | `MoveTo_WhenLegal_SucceedsAndSyncsMatchStatePhase`、`EndMatch_FromEveryNonTerminalPhase_*` |
| AC-4 五个非终局阶段均可终局 | 已满足 | `EndMatch_FromEveryNonTerminalPhase_SucceedsAndSyncs`（TestCase × 5） |
| AC-5 非法跳转被拒且两状态不变 | 已满足 | `MoveTo_WhenEdgeNotDeclared_ReturnsIllegalTransitionAndChangesNothing`（6 条非法边参数化，断言错误码 + 机器阶段 + state.Phase 三不变） |
| AC-6 未声明自转移被拒 | 已满足 | `MoveTo_WhenSelfTransitionNotDeclared_IsRejected`（Main/Main、Draw/Draw） |
| AC-7 MatchEnd 终点 | 已满足 | `Advance_AtMatchEnd_ReturnsFailureAndStaysAtMatchEnd`、`EndMatch_AtMatchEnd_ReturnsFailure`、`MoveTo_FromMatchEnd_ToAnyPhase_IsRejected`（枚举全部 6 个目标） |
| AC-8 CanMoveTo 无副作用（含越界值） | 已满足 | `CanMoveTo_ReflectsDeclaredEdgesWithoutSideEffects`（含 `(TurnPhase)999` → false） |
| AC-9 失败后可恢复 | 已满足 | `MoveTo_WhenIllegalThenLegal_RecoversCleanly` |
| AC-10 从 Main 对局形态构造 | 已满足 | `Ctor_WhenStateProducedByFactoryShape_EdgesAreAvailableFromMain` |
| AC-11 与 RuleEngine 阶段校验集成 | 已满足 | `RuleEngine_RejectsCommandsOutsideMain_AndAcceptsWhenPhaseReturnsToMain`（非 Main 拒 EndTurn → `NotYourTurn`；序贯推进回 Main 后 Valid） |
| AC-12 无业务副作用 | 已满足 | `Transitions_HaveNoBusinessSideEffects`（ActivePlayerId/TurnNumber/双方法力/IsFinished 五处快照断言） |
| AC-13 null 防御 | 已满足 | `Ctor_WhenStateNull_ThrowsArgumentNullException`；另有 `Ctor_WhenPhaseUndefined_Throws` 防越界枚举 |
| AC-14 解耦与门禁 | 已满足 | `check.ps1` PASS（167 文件 / 10 asmdef）；仅 using `System`、`System.Collections.Generic`、`Card.Core`、`Card.Domain.Match` |

### 10.2 铁律扫描

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 1 组件优于继承 | 未命中 | sealed 类组合 Core 状态机，无玩法继承 |
| 2 依赖向下 / 3 内核只依赖 BCL | 未命中 | 位于 2_Application，只引用 Core + Domain；无 `UnityEngine`/`Debug.Log`/`Mathf`/`Time`/`UnityEngine.Random` |
| 4 表现层只读 | 不涉及 | 无 View 代码；`CanMoveTo` 供 Application 决策，注释已标注非 View 判规则 |
| 5 操作走命令 | 未命中 | 本任务不结算命令；RuleEngine 既有阶段校验通过 AC-11 保持有效 |
| 6 数值来自配置 | 不涉及 | 无数值（阶段顺序是规则结构非常量数值） |
| 7 禁 UnityEditor / dataPath | 未命中 | 纯 Application 逻辑 |
| 8 禁隐式全局访问 | 未命中 | 无单例/静态实例；静态仅只读后继表 |
| 9 新功能带测试且先写 | 未命中 | 26 个新用例先红（CS0246 × 32）后绿 |
| 10 行数/复杂度/0 warning | 未命中 | `TurnStateMachine.cs` 118 行；最长方法（构造）14 行；`dotnet build` 0 warning；测试文件 256 行 |
| 11 内核解耦/状态只存数据 | 未命中 | 不引 Unity；不改变 MatchState 数据结构，只写既有 `Phase` 字段 |
| 12 权威宿主/13 阶段二范围锁 | 不涉及 | 无网络代码；本类型在共享 Application 层，服务器可直接复用 |
| 禁止清单（#region、单行多语句、省略花括号、拼音、try/catch 吞异常） | 未命中 | 逐项确认；无 try/catch |

### 10.3 边界场景推演

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 跳阶段（MatchStart→Main） | 拒绝，两状态不变 | 一致 | AC-5 |
| 倒退（TurnStart→MatchStart、TurnEnd→Draw） | 拒绝 | 一致 | AC-5 |
| 未声明自转移（Main→Main） | 拒绝（沿用 Core 语义） | 一致 | AC-6 |
| 终局后任何操作 | Advance/EndMatch/MoveTo 全失败且停留 MatchEnd | 一致 | AC-7 |
| 越界枚举目标 `(TurnPhase)999` | CanMoveTo=false；MoveTo 返回 UnknownState 类失败且状态不变 | Core 返回 `ERROR_STATE_UNKNOWN`，本层不写回 | AC-8（CanMoveTo 侧）；MoveTo 侧行为由 Core M1-T3 既有测试保证 |
| 构造传入越界 Phase | 抛 ArgumentException（不允许构造出非法机器） | 一致 | `Ctor_WhenPhaseUndefined_Throws` |
| MatchFactory 既成 Main 对局 | 可构造，仅能去 TurnEnd/MatchEnd | 一致 | AC-10 |
| 连续非法后合法 | 无污染，合法转移成功并同步 | 一致 | AC-9 |

### 10.4 测试证据

- 红：`coverage.ps1` 编译失败，`TurnStateMachineTests.cs` 报 **CS0246 未能找到 TurnStateMachine × 32**（2026-10-06）。
- 绿：`Tools/coverage.ps1` → **614 passed / 0 failed / 0 skipped**（588 + 新增 26：16 个测试方法，含 5 终局参数 + 6 非法边参数 + 2 自转移参数），`dotnet build` **0 warning**。
- 覆盖率：`TurnStateMachine.cs` **57/57 = 100%**；汇总 `0_Core 96.51%`（1160/1202）、`Domain + App 90.86%`（2218/2441，门禁 80%）。
- 静态门禁：`check.ps1` **PASS**（167 文件 / 10 asmdef）；`-SelfTest` 交接时已 PASS 未受影响。
- GUID：新增 2 个手写 .meta，全仓 253 个 meta 无重复。
- Unity 权威：2026-10-06 用户在 Unity 2022.3.54f1c1 编辑器 Test Runner（EditMode）实跑回报 **634 passed / 0 failed**（608 + 26），新增脚本编译导入无误、.meta 被 Unity 正常识别。

### 10.5 影响面

- 新增文件：`2_Application/Match/TurnStateMachine.cs`(+ .meta)、`7_Tests/EditMode/Match/TurnStateMachineTests.cs`(+ .meta)。
- 改动既有模块：**无**。`RuleEngine`/`MatchFactory`/`MatchState`/`TurnPhase` 零改动，既有 588 内核用例零改动全绿。
- 文档/配置：无配置变更；PROGRESS 已更新；M4-T2 控制器将以本类型驱动阶段并接入阶段业务（法力 M4-T8、Draw 阶段调 CardDrawService）。
- 回归风险：低（纯新增）。

### 10.6 反向审查（挑刺 ≥3）

1. **"成功转移才写回 state.Phase，失败不写回"会不会双状态漂移？** —— 不会：写回仅在 `Result.IsSuccess` 分支；Core 保证失败时 `Current` 不变，故两边恒一致；AC-5/AC-7 对两状态同时断言锁定。
2. **为什么终局边从每个非终局阶段都声明，而非法疲劳只发生在 Draw？** —— Docs/01 中英雄归零可发生在 Draw（疲劳）、Main（出牌/攻击/技能）等多个阶段，且 Docs/02 要求"非法阶段操作被拒"的严格阶段流；终局是异常出口，星型边最贴合规则且仍不允许从 MatchEnd 出去。AC-4/AC-7 双向锁定。
3. **为什么不切 ActivePlayerId / 不自增 TurnNumber？是否漏了回合切换？** —— 任务表把"法力与英雄技能（含回合开始重置）"列为 M4-T8，控制器为 M4-T2；本任务卡第 4 节明确不做阶段业务，TurnEnd→TurnStart 只表达"可进入下一回合开始阶段"，行动方切换由控制器在该转移点执行（与 M3 既有服务同风格），AC-12 用测试锁住"现在不做"，防止提前写入未测语义。
4. **`(TurnPhase)999` 未注册状态 MoveTo 的失败码没在本任务单测？** —— 该分支完全由 M1-T3 的 `TransitionTo_WhenTargetNotRegisteredAtAll_ReturnsUnknownStateFailure` 保证（同一份 Core 代码）；本层只额外锁定构造期不允许越界 Phase 与 CanMoveTo=false，避免重复测试。

### 10.7 评审结论

**通过**（AI 自审）：无 P0/P1。无新增遗留项。无 Unity 工具链 614/0、Unity 编辑器 634/0 双环境证据齐备，本任务关闭。
