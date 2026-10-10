# 任务卡 · M7-T3 回合终止保障（TurnGuard）

> 模板见 Docs/templates/TaskCard.template.md。本卡为 M7 第三任务，依赖 M7-T2（已交付 018632c）。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-T3 |
| 所属里程碑 | M7 玩家代理与 AI |
| 上游需求 | FR-6.4（不死循环：单回合行动有步数/时间上限，必然终止）、FR-6.5（命令经 RuleEngine 校验） |
| 规则依据 | Docs/01 §FR-6（L289）、Docs/02 M7 任务表 T3（完成标准"任何局面下 AI 回合必然结束"）；铁律 11（环境时间走 `IClock`，禁止 `UnityEngine.Time`）、铁律 1（组件优于继承） |
| 预估 | 0.5–1 人日 / 1 会话 |
| 依赖任务 | M7-T1（runner/上下文）、M7-T2（GreedyAiAgent，内含轻量终止保障：被拒候选不重试 + `MaxSubmissionsPerActivation=500` 硬上限） |

## 2. 目标（一句话）

> 把 M7-T2 的轻量终止保障升级为正式、可配置的 `TurnGuard` 组件（步数上限 + `IClock` 时间上限 + 无进展检测三层独立兜底），接入 `GreedyAiAgent` 决策循环，使**任何局面下 AI 回合必然以 `EndTurn` 收尾**，为 M7-T4 批量模拟器与 M7-OBS-1 分帧泵打地基。

## 3. 范围（做什么）

- **Application（纯 BCL，kernel 工具链覆盖）**，新文件 `Assets/_Project/2_Application/Match/Agents/TurnGuard.cs`：
  - `TurnGuardOptions`（sealed class + init 属性 + 静态 `Default`）：
    - `MaxSteps`（默认 500，接管 T2 常量语义）：单次激活内提交数上限；
    - `MaxTicks`（默认 0 = 不启用）：激活内允许跨越的 `IClock` tick 预算；
    - `NoProgressLimit`（默认 8）：连续多少步提交后局面签名无变化即判定"无进展"；
    - 触发原因常量 `ReasonStepLimit` / `ReasonTimeLimit` / `ReasonNoProgress`（诊断用）。
  - `TurnGuard`（sealed，策略对象）：
    - 构造 `(TurnGuardOptions? options = null, IClock? clock = null)`；**防呆**：`MaxTicks > 0` 而 `clock == null` → `ArgumentException`（要时间上限就必须给时钟，Fail fast）；
    - `OnActivationStarted()`：重置全部计数并记录起始 tick（跨回合可复用、幂等）；
    - `RegisterStep(string signature)`：每条**已提交**命令（含被拒）后调用——计步、对比签名、按"步数 → 时间 → 无进展"顺序判定，任一触发即 `IsExhausted`（此后调用幂等，不再累计）；
    - 只读状态：`Steps` / `NoProgressSteps` / `IsExhausted` / `ExhaustReason`。
    - 职责单一：guard 只计数与判定，**局面签名由调用方生成并传入**（guard 不依赖 MatchState 类型）。
  - 校验：`MaxSteps < 1` / `MaxTicks < 0` / `NoProgressLimit < 1` → `ArgumentException`。
- **GreedyAiAgent 接入**（`GreedyAiAgent.cs` 小改）：
  - 构造扩展为 `(int playerId, CardDatabase database, TurnGuardOptions? guardOptions = null, IClock? clock = null)`（可选参数，T2 调用零改动）；
  - `OnTurnActivated` 开头 `_guard.OnActivationStarted()`；技能/出牌/攻击三处循环条件由 `submissions < MaxSubmissionsPerActivation` 换为 `!_guard.IsExhausted`（步数上限含义并入 guard）；每条 Submit 后 `_guard.RegisterStep(BuildStateSignature(context.View))`；
  - `BuildStateSignature(IReadOnlyMatchState)` 私有静态：决策相关局面面——双方 `TurnNumber/ActivePlayerId`、英雄 `Health/Armor/PowerUsedThisTurn`、法力 `Current/Max`、`Deck/Hand/Graveyard` 张数、`FatigueCounter`、场面每张卡 `InstanceId/Attack/Health/StatusFlags/KeywordFlags`（确定性顺序遍历，无 Random）；
  - **`EndTurn` 不受 guard 约束**：循环退出后 `CanAct` 成立即发 `EndTurn`（否则违反"必然结束"）；
  - 删除 `internal const int MaxSubmissionsPerActivation`（全仓仅 agent 内部 3 处引用，测试未引用）。
- **测试**（kernel，`7_Tests/EditMode/Match/`）：
  - 新文件 `TurnGuardTests.cs`（纯单元，≥ 12 例）：默认/自定义步数、时间上限三种形态、无进展计数与重置、防呆构造、`OnActivationStarted` 重置、exhausted 后幂等；
  - 新文件 `GreedyAiAgentGuardTests.cs`（agent 集成，≥ 6 例）：小步数截断后 EndTurn 收尾、时钟推进触发时间上限、默认配置整局不回归、guard 诊断状态可断言。

## 4. 明确不做（防止范围蔓延）

- 不做分帧泵 / 逐步泵重构（AI 仍在激活回调内同步跑完回合）——**M7-OBS-1**（已决策 T3 后单开；本卡只交付 guard 机制，Unity 侧真实时钟实现与接线也在 OBS-1）。
- 不做 AI vs AI 批量模拟器——**M7-T4**（guard 是其前置可靠性依赖）。
- 不做难度分级 / 评估权重外置——**M7-T5**（guard 默认值本卡为常量，不做 Excel 配置表；"可配置"指构造参数级，与 T2 算法常量先例一致，铁律 6 不适用）。
- 不改 `RuleEngine` / `MatchController` / `AgentMatchRunner` / `IPlayerAgent` / `IAgentContext` 公开面与语义；不改 `HumanPlayerAgent`（人类走 UI，不经同步决策循环）。
- 不做"回滚到检查点"或"撤销已提交命令"——guard 触发只停止**后续**决策，已 accepted 命令不回滚（命令已按 FR-6.5 合法生效）。
- 不处理 agent 抛异常（runner 既有语义：向上传播，不吞）。

## 5. 接口约定

```csharp
namespace Card.Application.Match.Agents
{
    /// <summary>回合终止保障选项（M7-T3，FR-6.4）：三层上限互相独立，任一触发即终止本回合决策。</summary>
    public sealed class TurnGuardOptions
    {
        /// <summary>步数上限：单次激活内提交数（含被拒）。默认 500（接管 M7-T2 硬上限）。</summary>
        public int MaxSteps { get; init; } = 500;

        /// <summary>时间上限：激活内允许跨越的 IClock tick 预算；0 = 不启用（默认）。</summary>
        public int MaxTicks { get; init; } = 0;

        /// <summary>无进展阈值：连续 N 步提交后局面签名无变化即触发。默认 8。</summary>
        public int NoProgressLimit { get; init; } = 8;

        public static TurnGuardOptions Default { get; } = new TurnGuardOptions();

        public const string ReasonStepLimit = "step-limit";
        public const string ReasonTimeLimit = "time-limit";
        public const string ReasonNoProgress = "no-progress";
    }

    /// <summary>回合守卫（M7-T3）：agent 决策循环每提交一条命令后 RegisterStep 一次，
    /// 三层上限（步数/时间/无进展）任一耗尽即 IsExhausted；agent 收到后停止决策并必发 EndTurn。</summary>
    public sealed class TurnGuard
    {
        /// <summary>MaxTicks > 0 时必须提供 clock（否则构造抛 ArgumentException）。</summary>
        public TurnGuard(TurnGuardOptions? options = null, IClock? clock = null);

        /// <summary>每次激活开始调用：重置计数并记录起始 tick（幂等）。</summary>
        public void OnActivationStarted();

        /// <summary>每条已提交命令（含被拒）后调用：signature 为提交后的局面签名（由调用方生成）。
        /// 已 IsExhausted 后调用幂等。</summary>
        public void RegisterStep(string signature);

        public int Steps { get; }              // 本激活已注册步数
        public int NoProgressSteps { get; }    // 当前连续无进展步数
        public bool IsExhausted { get; }
        public string? ExhaustReason { get; }  // Reason* 常量之一；未触发为 null
    }
}
```

关键语义（实现必须遵守，测试锁定）：

1. **三层独立**：任一触发即 `IsExhausted`；触发顺序判定固定"步数 → 时间 → 无进展"，`ExhaustReason` 记首个触发者。
2. **无进展判定**：`RegisterStep` 的新签名与上一步相同 → `_noProgress++`；不同 → 归零。被拒命令也计入（状态零变更必然签名不变，属"安全方向"提前终止：连续多步全被拒说明决策与引擎口径漂移，继续尝试无意义）。
3. **时间判定**：`clock.CurrentTick - 激活起始tick >= MaxTicks` 即触发；`MaxTicks = 0` 时完全跳过（时钟可为 null）。
4. **幂等**：`IsExhausted` 后 `RegisterStep` 不再累计；`OnActivationStarted` 可重复调用。
5. **EndTurn 豁免**：guard 只约束决策循环；agent 在循环退出后仍须发 `EndTurn`（`CanAct` 成立时），保证"任何局面下 AI 回合必然结束"。
6. 确定性：签名生成无 Random、遍历顺序稳定；同种子同局面签名逐位一致。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 静态门禁 | 新代码在 `2_Application`，纯 BCL（`IClock` 来自 `Card.Core`）；`check.ps1` PASS |
| AC-2 | 构造校验 | `MaxSteps<1` / `MaxTicks<0` / `NoProgressLimit<1` / `MaxTicks>0 且 clock=null` 均 `ArgumentException` |
| AC-3 | 步数上限 | 默认 500：注册 499 步未触发、第 500 步触发（Reason=step-limit）；自定义 `MaxSteps` 生效；exhausted 后 `RegisterStep` 幂等 |
| AC-4 | 时间上限 | `MaxTicks>0` + `ManualClock`：未超预算不触发；`Advance` 超预算后触发（Reason=time-limit）；`MaxTicks=0` 时时钟任意推进不触发 |
| AC-5 | 无进展 | 连续 `NoProgressLimit` 步相同签名触发（Reason=no-progress）；签名变化归零；被拒步计入（由 agent 集成用例验证：拒绝注入场景 guard 正常计数） |
| AC-6 | 重置 | `OnActivationStarted` 后 Steps/NoProgressSteps/ExhaustReason 全清零，起始 tick 重记（跨"回合"可复用） |
| AC-7 | agent 集成（必然结束） | `MaxSteps=小值` 的整局/单回合：激活内提交数（决策部分）≤ MaxSteps 且**以 EndTurn 收尾**；时钟推进触发时间上限后同样 EndTurn 收尾 |
| AC-8 | 默认不回归 | 默认构造（不带 options/clock）下 M7-T2 全部既有测试绿：三种子整局零非法、终止、同种子确定性逐位一致 |
| AC-9 | 质量门禁 | kernel 全量通过；覆盖率门禁不降；`TurnGuard.cs` 行覆盖 ≥ 90%；R5（≤300 行/文件、≤50 行/方法） |
| AC-10 | 既有不回归 | 既有 801 例全绿；Unity EditMode 由用户实跑（预期 1004 + 本卡新增） |

## 7. 测试要求

- kernel 测试 **≥ 18 例**（TurnGuard 单元 ≥ 12 + agent 集成 ≥ 6），先红（CS0246 / 断言失败）后绿；禁止改测试凑绿。
- 单元测试直接构造 `TurnGuardOptions` + `ManualClock`（`Card.Core` 既有测试夹具），不依赖 MatchState。
- agent 集成复用 `GreedyAiAgentFixtures`（迷你配置库 / `BuildState` 手工局面 / `RecordingAuthority` / `DirectAgentContext` / `FailPlaysContext`）。
- "必然结束"断言口径：`RecordingAuthority.Signatures` 最后一条 = `EndTurn|<座位>`，且决策命令数 ≤ `MaxSteps`。
- 新套件首次提交前在目标环境实跑留证：kernel 由 coverage.ps1 留证；Unity EditMode 由用户在编辑器 Test Runner 实跑回报（M6 复盘改进项 1：环境覆盖维度；本卡无 Unity 侧新代码，PlayMode 不受影响）。

## 8. 涉及文档与配置

- 需更新：本卡结论；`Docs/PROGRESS.md`（M7 2/5→3/5 + 任务级状态行）。HANDOFF §3 快照随 M7 收官统一刷新（中途不动，按会话约定）。
- 配置表：无改动（guard 参数为构造级选项，非 Excel 配置）。
- 影响面：纯加法 + `GreedyAiAgent` 内部小改（构造可选参数向后兼容；公开类面新增 `TurnGuardOptions`/`TurnGuard`）；删除仅内部引用的 T2 常量。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见 §11）
- [x] 测试通过且覆盖边界（21 例：TurnGuard 单元 16 + agent 集成 5）
- [x] 编译 0 error / 0 warning（kernel 工程 0/0；Unity EditMode 1025 实跑通过即编译干净）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见 §11 第 2 步）
- [x] 通过 `Docs/04` 评审，无未关闭 P0/P1（见 §11）
- [x] 涉及文档已同步更新（本卡 + `Docs/PROGRESS.md`）

---

## 10. 待确认问题（开工前）

1. **形态取舍**：guard 做成 agent 内嵌策略对象（`GreedyAiAgent` 持有），而非 `IPlayerAgent` 装饰器。理由：当前是"激活回调内同步跑完一回合"模型，外部装饰器无法中断内层同步循环（不能抛异常中断——禁止吞异常约定）；agent 主动查 guard 是最小侵入。分帧泵（OBS-1）后如需外置可再演进。默认采用，可否？
2. **时间上限语义**：以 `IClock` tick 差为口径，`clock` 可选注入；未注入且未启用（`MaxTicks=0`）为默认形态——同步整回合模型下时钟本不推进，时间上限真正生效要等 OBS-1 分帧泵 / T4 模拟器注入时钟。是否接受"机制先行、接线后置"？
3. **无进展默认阈值 8**：无实测数据支撑，取"连续 8 步局面无变化几乎必为循环"的保守值；误判代价仅是本回合提前 EndTurn（安全方向）。是否认可？
4. **被拒步计入无进展**：连续 `NoProgressLimit` 步全被拒同样触发提前终止（而非等步数上限）。语义上"决策与引擎口径漂移"属异常态，早停优于硬撑。是否认可？

---

## 11. 任务结论（2026-10-10 完成）

**结论：通过。** 四个待确认问题均按默认方案；无 P0/P1 遗留。

### 交付物

- 生产（纯 BCL，`2_Application/Match/Agents/`）：`TurnGuard.cs`（含 `TurnGuardOptions`，120 行）。
- 生产（改，`2_Application/Match/Agents/`）：`GreedyAiAgent.cs`——构造加可选 `(TurnGuardOptions?, IClock?)`；决策循环接入 guard；`BuildStateSignature` 局面签名；删除 T2 的 500 常量；**EndTurn 不受 guard 约束**（保证必然结束）。
- 测试（`7_Tests/EditMode/Match/`，21 例）：`TurnGuardTests.cs`（16）+ `GreedyAiAgentGuardTests.cs`（5），夹具 `GreedyAiAgentFixtures.cs` 加可选 guardOptions/clock 透传。
- 文档：本卡 + `Docs/PROGRESS.md`（M7 2/5→3/5）。

### 第 1 步：需求对齐（AC 证据）

| AC | 满足 | 证据 |
| --- | --- | --- |
| AC-1 静态门禁 | 是 | `check.ps1` PASS（315 文件，0 违规）；`TurnGuard.cs` 在 `2_Application`，仅 using BCL + Core/Domain；R1 实证无 `UnityEngine` |
| AC-2 构造校验 | 是 | `Constructor_InvalidOptions_Throws`（3 用例）+ `Constructor_TimeLimitWithoutClock_Throws`：MaxSteps<1 / MaxTicks<0 / NoProgressLimit<1 / MaxTicks>0 且 clock=null 分别抛（M3 复盘教训：try/catch 断言基类 ArgumentException，精确匹配 ArgumentOutOfRangeException 失败） |
| AC-3 步数上限 | 是 | `Steps_DefaultLimit500_TriggersAt500thRegistration`（499 不触发、第 500 触发，Reason=step-limit）/ `Steps_CustomLimit_Triggers` / `Exhausted_RegisterStep_IsIdempotent` |
| AC-4 时间上限 | 是 | `TimeLimit_Zero_NeverTriggers` / `TimeLimit_NotTriggered_WithinBudget`（tick 差 9 < 10）/ `TimeLimit_Triggers_WhenBudgetExhausted`（tick 差 10 >= 10，Reason=time-limit） |
| AC-5 无进展 | 是 | `NoProgress_Triggers_AtLimit`（首步无上一步可比永不判无进展）/ `NoProgress_FirstStep_NeverCompares` / `NoProgress_ResetOnChange`（签名变化归零） |
| AC-6 重置 | 是 | `ActivationStart_ResetsAllState`（Steps/NoProgressSteps/ExhaustReason 全清零）/ `ActivationStart_RerecordsStartTick`（起始 tick 重记后预算重新计算） |
| AC-7 agent 必然结束 | 是 | `MaxSteps_TruncatesDecision_AndEndsTurn`（MaxSteps=2：技能+1出牌，EndTurn 收尾）/ `SpellArmorLoop_StoppedByStepLimit`（10 张叠甲被步数 5 兜底，无进展不触发因护甲每步 +1 局面变化）/ `OneStepLimit_StillEndsTurn`（MaxSteps=1 极限）/ `TimeLimit_TruncatesDecision_AndEndsTurn`（`ManualClock`+时钟推进装饰器，分帧泵预演，时间上限触发后 EndTurn 收尾） |
| AC-8 默认不回归 | 是 | `DefaultGuardOptions_T2BehaviorUnchanged`（显式 null 走新构造，HeroPower 首 + EndTurn 末，InvalidCount=0）；M7-T2 全部 22 例既有测试全绿（kernel 801→822，+21 全为新例） |
| AC-9 质量门禁 | 是 | kernel **822/822**（基线 801 + 21）；`TurnGuard.cs` 行覆盖 **97%**（57/59）、`GreedyAiAgent.cs` **93%**（140/151）；0_Core 96.52%（持平）、Domain+App 93.08%（基线 92.98% ↑）；R5：120 行/文件、最大方法 < 50 行、圈复杂度低 |
| AC-10 既有不回归 | 是 | kernel 全量 822 全绿；用户编辑器实跑 **Unity EditMode 1025/1025**（1004+21）；PlayMode 3 例全绿（无 Unity 侧新代码，回归确认） |

### 第 2 步：铁律扫描（Docs/03 十三条 + 禁止清单）

1 组件优于继承：`sealed` 类，无玩法继承 ✅；2 依赖向下：仅 Application→Core/Domain ✅；3 纯 BCL：`TurnGuard` 无 `UnityEngine`/`Debug.Log`/`Time`/`Random`，`IClock` 来自 `Card.Core`，`check.ps1` R1 实证 ✅；4 表现层只读：本任务不涉表现层 ✅；5 命令唯一入口：四类命令仍 `context.Submit`，guard 不上行命令 ✅；6 数值来自配置：guard 默认值为算法常量（500/0/8），非玩法数值，与 T2 算法常量先例一致 ✅；7 无 Editor 引用 ✅；8 无单例/`Find`/ServiceLocator ✅；9 先写测试：红灯 CS0246 留证后转绿 ✅；10 R5：120 行（TurnGuard）/改后 GreedyAiAgent 在限内、最大方法 < 50 行、圈复杂度低 ✅；11 **内核解耦（铁律 11，重点）**：时间上限走 `IClock` 抽象，禁止 `UnityEngine.Time`——`TurnGuard` 构造接受 `IClock?`，生产接线由 OBS-1 注入真实时钟，规则层零 `Time` 引用 ✅；12/13 本任务不涉网络 ✅。禁止清单：无 `try/catch` 吞异常（构造校验抛 `ArgumentOutOfRangeException`/`ArgumentException` 向上传播）、无索引当业务 ID（全部 InstanceId/座位 Id）、未改测试凑绿。

### 第 3 步：边界推演

| 场景 | 行为 | 测试 |
| --- | --- | --- |
| EndTurn 豁免 | guard 只约束决策循环；循环退出后 `CanAct` 成立即发 EndTurn | `MaxSteps_TruncatesDecision_AndEndsTurn`（最后一条=EndTurn） |
| 被拒步计入无进展 | 被拒命令状态零变更 → 签名不变 → `_noProgress++`；连续 `NoProgressLimit` 步全被拒触发早停 | T2 既有 `RejectedCandidate_IsNotRetriedThisTurn`（被拒候选移出集，guard 正常计数不回归） |
| 护甲每步变化 | 10 张叠甲法术：护甲 +1 → 签名变化 → 无进展归零，只能靠步数上限兜底（三层独立性真实局面验证） | `SpellArmorLoop_StoppedByStepLimit` |
| 时钟推进预演 | 装饰器每次 Submit 前 `Advance(1)`，预算 3 → 第 3 步注册后触发 time-limit（OBS-1 分帧泵形态预演） | `TimeLimit_TruncatesDecision_AndEndsTurn` |
| 默认配置无时钟 | (null, null) 走新构造，`MaxTicks=0` 跳过时间判定，行为与 T2 完全一致 | `DefaultGuardOptions_T2BehaviorUnchanged` |
| exhausted 后幂等 | `RegisterStep` 不再累计，`ExhaustReason` 保持首个触发者 | `Exhausted_RegisterStep_IsIdempotent` |

### 第 4 步：测试证据

- `Tools/coverage.ps1`：**822 passed / 0 failed**（基线 801 + 21）；覆盖 TurnGuard 97%（57/59 行，未覆盖：MaxTicks=0 时 `_clock!.CurrentTick` 短路分支、signature==null 防御性 throw）、GreedyAiAgent 93%（140/151）。
- Unity 编辑器 Test Runner EditMode：**1025 passed / 0 failed**（用户 2026-10-10 实跑回报）；PlayMode 3 例全绿（无 Unity 侧改动）。

### 第 5 步：影响面

- 纯加法 + `GreedyAiAgent` 内部小改（构造可选参数 `(TurnGuardOptions?, IClock?)` 向后兼容，T2 调用零改动；公开类面新增 `TurnGuardOptions`/`TurnGuard`）。
- 删除仅 T2 内部引用的 `MaxSubmissionsPerActivation` 常量（全仓仅 agent 内 3 处引用，测试未引用，安全删除）。
- 不改 `RuleEngine`/`MatchController`/`AgentMatchRunner`/`IPlayerAgent`/`IAgentContext`/`HumanPlayerAgent` 公开面与语义；不改配置表。

### 第 6 步：反向审查

1. **"guard 内嵌 agent 而非外置装饰器，未来扩展性如何？"**——同步整回合模型下装饰器无法中断内层循环（不能抛异常中断——禁止吞异常约定）；当前形态最小侵入。分帧泵（OBS-1）改为协程/分帧后，guard 可平滑外置为装饰器（`RegisterStep` API 不变），无需破坏性演进。
2. **"时间上限默认不启用（MaxTicks=0），是否形同虚设？"**——机制先行、接线后置（任务卡 §10 Q2 明确）。同步整回合模型下时钟本不推进，时间上限真正生效场景：① OBS-1 分帧泵注入真实时钟；② T4 模拟器注入时钟。当前已用 `ClockTickingContext` 装饰器预演分帧泵形态，机制验证完成。
3. **"无进展阈值 8 是拍脑袋值"**——无实测数据支撑，取保守值；误判代价仅是本回合提前 EndTurn（安全方向）。T4 模拟器批量跑后可用实测数据校准（属 T4/T5 范围）。
4. **"被拒步计入无进展可能误杀"**——被拒说明决策与引擎口径漂移，连续多步全被拒几乎必为异常态；早停优于硬撑步数上限；且 agent 还有"被拒候选移出集"独立机制（T2 既有，guard 不替代）。

无 P0/P1/P2 新增。
