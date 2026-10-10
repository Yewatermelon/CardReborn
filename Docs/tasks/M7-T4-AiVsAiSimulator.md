# 任务卡 · M7-T4 AI vs AI 模拟器

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-T4 |
| 所属里程碑 | M7 玩家代理与 AI |
| 上游需求 | FR-6（对手 AI）+ M7 门禁（500 局无崩溃/无死循环/无非法操作，胜率 45–55% 或调整记录） |
| 规则依据 | Docs/02 M7 任务表：批量跑对局并输出统计（胜率/平均回合数/异常日志） |
| 预估 | 1 人日 / 1 会话 |
| 依赖任务 | M7-T1（AgentMatchRunner）、M7-T2（GreedyAiAgent）、M7-T3（TurnGuard） |

## 2. 目标（一句话）

> 在 Application 层（纯 BCL，kernel 可测）提供 `AiVsAiSimulator`：固定种子批量装配双 GreedyAiAgent 整局同步跑，输出胜率/平均回合数/异常日志；EditMode 测试实跑 ≥ 500 局满足 M7 门禁并输出统计摘要。

## 3. 范围（做什么）

### 3.1 模拟器（Application 层，纯 BCL）

- `Assets/_Project/2_Application/Match/Agents/SimulationModels.cs`：
  - `AiVsAiSimulatorOptions`：`MatchCount`（默认 500）、`BaseSeed`（默认 1）、`MaxSubmissionsPerMatch`（默认 2000，防死循环硬保险）、`GuardOptions`（默认 null = TurnGuard 内置默认）
  - `AiVsAiMatchRecord`：`Seed`、`Result`（MatchResult）、`WinnerId`、`TurnNumber`、`SubmissionCount`、`InvalidCount`、`Failure`（null = 正常完成；否则记异常/超限原因）
  - `AiVsAiSimulationReport`：`Total/P0Wins/P1Wins/Draws/Failures`、`AverageTurns`（成功完成局口径）、`Player0WinRate`（P0Wins / 完成局）、`Matches` 列表、`Summary()` 生成人可读统计摘要（含异常日志逐条列出）
- `Assets/_Project/2_Application/Match/Agents/AiVsAiSimulator.cs`（静态类）：
  - `Run(CardDatabase, MatchSetupRequest setup0, MatchSetupRequest setup1, AiVsAiSimulatorOptions)` → `AiVsAiSimulationReport`
  - 单局装配：`MatchFactory.Create(database, setup0, setup1, new SeededRandomProvider(seed))` + `MatchController` + 计数装饰器（`ICommandAuthority` 包装，统计提交数与被拒数）+ 双 `GreedyAiAgent` + `AgentMatchRunner.Start()`（一次调用栈同步跑完整局，与 `GreedyAiAgentFixtures.RunRealGame` 同模式）
  - 种子派生：`seed_i = BaseSeed + i`（确定性、可复现）
  - 防死循环硬保险：计数装饰器在提交数超过 `MaxSubmissionsPerMatch` 时抛专用 `SimulationAbortException`，该局记 `Failure="提交数超限"` 后继续下一局
  - 单局异常兜底：任何单局异常捕获后记 `Failure`（异常类型 + 消息）继续批量，**不中断整体、不吞日志**——异常清单进报告（批量工具的明确失败路径）
  - 统计守恒：`Total = P0Wins + P1Wins + Draws + Failures`

### 3.2 测试（EditMode）

- `Assets/_Project/7_Tests/EditMode/Match/AiVsAiSimulatorTests.cs`：
  - 小批量（10 局）全部正常完成、零 Invalid、统计守恒、Summary 含胜率与平均回合字段
  - 同参数两次 Run 报告完全一致（确定性）
  - `MaxSubmissionsPerMatch` 设极小值：全部记 Failure、不抛出、不中断
  - **M7 门禁测试：500 局**——零 Failure、零 Invalid、全部终局、平均回合数合理上限内；`TestContext.Out` 输出统计摘要（胜率/平均回合/异常日志）；P0 胜率 45–55% 区间若不落入则按文档要求登记调整记录（不硬编码断言区间，以报告数字为准）

## 4. 明确不做（防止范围蔓延）

- 不做并行/多线程批量（单线程同步够用，性能不够再开观察项）
- 不做编辑器菜单入口 / 报告落盘文件（EditMode 测试输出即验收面；后续需要再开 OBS）
- 不做难度分级与权重外置（M7-T5）
- 不做胜率调参（除非 500 局跑出超区间——那是"调整记录"流程，不在本任务实现权重改动）
- 不修改 GreedyAiAgent / TurnGuard / AgentMatchRunner / RuleEngine / MatchController

## 5. 接口约定

```csharp
namespace Card.Application.Match.Agents;

public sealed class AiVsAiSimulatorOptions
{
    public int MatchCount { get; init; } = 500;
    public int BaseSeed { get; init; } = 1;
    public int MaxSubmissionsPerMatch { get; init; } = 2000;
    public TurnGuardOptions? GuardOptions { get; init; }
}

public sealed class AiVsAiMatchRecord
{
    public required int Seed { get; init; }
    public MatchResult Result { get; init; }        // 正常完成时为终局结果
    public int? WinnerId { get; init; }
    public int TurnNumber { get; init; }
    public int SubmissionCount { get; init; }
    public int InvalidCount { get; init; }
    public string? Failure { get; init; }           // null = 正常完成
}

public sealed class AiVsAiSimulationReport
{
    public int Total { get; }
    public int P0Wins { get; }
    public int P1Wins { get; }
    public int Draws { get; }
    public int Failures { get; }
    public double AverageTurns { get; }             // 成功完成局口径
    public double Player0WinRate { get; }           // P0Wins / 完成局数
    public IReadOnlyList<AiVsAiMatchRecord> Matches { get; }
    public string Summary();                        // 人可读统计摘要（含异常日志）
}

public static class AiVsAiSimulator
{
    public static AiVsAiSimulationReport Run(
        CardDatabase database,
        MatchSetupRequest setup0, MatchSetupRequest setup1,
        AiVsAiSimulatorOptions options);
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 批量驱动 | 500 局全部跑完，进程不崩溃、单测试内完成 |
| AC-2 | 零死循环 | 每局提交数 ≤ 2000，全部终局；`Failures == 0` |
| AC-3 | 零非法命令 | 全部 500 局 `InvalidCount == 0` |
| AC-4 | 统计输出 | Summary() 含：局数、P0/P1 胜率、平均回合数、异常日志；EditMode 测试 `TestContext.Out` 打印 |
| AC-5 | 确定性 | 同 options 两次 Run：报告逐字段一致 |
| AC-6 | 容错 | 单局失败（超限注入）不中断批量：记录 Failure、守恒、继续跑完 |
| AC-7 | 胜率分布 | P0 胜率落入 [45%, 55%]，否则在 PROGRESS/任务卡登记调整记录 |
| AC-8 | 质量门禁 | coverage.ps1 kernel 全绿 + 覆盖率门禁不降；check.ps1 PASS；铁律扫描（Application 层纯 BCL） |

## 7. 测试要求

- 测试类型：EditMode（kernel 口径，覆盖 2_Application）
- 最少用例数：≥ 6（小批量正确性 / 统计守恒 / 确定性 / 超限容错 / Summary 字段 / 500 局门禁）
- 必须覆盖的边界：超限防死循环、异常单局不中断、守恒关系、种子确定性
- 先红后绿；禁止改测试凑绿

## 8. 涉及文档与配置

- 需更新的文档：Docs/PROGRESS.md（M7 4/5→5/5 视 T5 是否同批完成；本任务先 4/5→"T4 ✅"）
- 需更新的配置表：无
- 是否影响既有模块：只新增，不改既有类型

## 9. 完成定义（DoD 勾选）

- [ ] 满足全部 AC 且附证据
- [ ] 测试通过且覆盖边界
- [ ] 编译 0 error / 0 warning
- [ ] 通过 `Docs/03` 铁律与禁止清单自查
- [ ] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [ ] 涉及文档已同步更新

---

## 10. 待确认问题

1. **胜率口径**：`Player0WinRate = P0Wins / 完成局数`（平局计入分母不计入 P0 胜）——是否认可？（平局若大量出现会稀释胜率，报告同时输出 Draws 供解读）
2. **500 局测试耗时**：EditMode 单测试同步跑 500 局（预估数十秒）——若过慢再拆 `MaxSubmissionsPerMatch` 或降低默认局数，先实测。
3. **胜率超区间**：若跑出 [45%,55%] 之外，本任务只登记调整记录（Docs/02 门禁口径），不改 AI 权重——是否认可？

---

## 11. 任务结论（完成后回填）

**完成日期**：2026-10-10
**结论**：✅ 全部完成。AC-1~8 全部有证据；发现并修复 M4-T4 遗留 TODO（SummonExecutor 满场抛异常违反 NFR-7）；胜率超区间按 Docs/02 口径登记调整记录 M7-B1。

### 交付清单

| 类别 | 文件 | 内容 |
| --- | --- | --- |
| Application（kernel） | `2_Application/Match/Agents/AiVsAiSimulator.cs` | `SimulationAbortException` + `CountingAuthority`（提交计数/被拒统计/超限中止）+ 静态 `Run`（种子 `BaseSeed+i` 逐局装配、单局异常记 Failure 后继续批量） |
| Application（kernel） | `2_Application/Match/Agents/SimulationModels.cs` | `AiVsAiSimulatorOptions` / `AiVsAiMatchRecord` / `AiVsAiSimulationReport`（守恒统计 + `Summary()` 摘要） |
| 规则修复 | `2_Application/Match/EffectExecutors.cs` | `SummonExecutor` 满场召唤由抛异常改为软失败跳过（M4-T4 注释遗留 TODO，NFR-7 "满场满手不得崩溃" + Docs/01 规则表 "满场时不能召唤"） |
| 测试 kernel | `7_Tests/EditMode/Match/AiVsAiSimulatorTests.cs` | 5 例（小批量+守恒/确定性/超限容错/Summary 字段/500 局门禁） |
| 测试 kernel | `7_Tests/EditMode/Match/EffectExecutorTests.cs` | +1 例 `Summon_FullBoard_SkipsWithoutThrowing`（满场软失败防回归） |

### 门禁结果

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| kernel 837/837 通过 | ✅ | `coverage.ps1`：0 failed（基线 831 + 6），覆盖率 `0_Core 96.52%` / `Domain + App 92.96%`（≥ 门禁 90%/80%） |
| check.ps1 | ✅ PASS | 324 文件零违规 |
| 铁律扫描 | ✅ | 新增文件均在 2_Application，纯 BCL，无 UnityEngine/Debug.Log/Time/Random |
| Unity EditMode 实跑留证 | ✅ | EditMode **1047** passed / 0 failed（2026-10-10 用户 Test Runner 回报；基线 1041 + 5 模拟器 + 1 满场防回归） |

### 500 局门禁报告（M7 门禁证据）

```text
AI vs AI 模拟报告：共 500 局
P0 胜率：67.8%（339/500）
P1 胜率：32.2%（161/500）
平局：0；失败：0
平均回合数：29.6（完成局口径）
异常日志：
  （无）
```

- AC-1/2/3 ✅：500 局零失败、零被拒命令、全部终局、平均回合 29.6（< 60 上限）
- AC-4 ✅：`Summary()` 输出胜率/平均回合/异常日志，测试 `TestContext.Out` 打印
- AC-5 ✅：同参数两次 Run 报告逐字段一致（确定性）
- AC-6 ✅：`MaxSubmissionsPerMatch=2` 注入 5 局全部超限记 Failure、批量继续、守恒成立
- AC-7 ⚠️ 调整记录：P0 胜率 67.8% 超 [45%,55%] → 登记 **M7-B1**（根因英雄技能不对齐，非 AI 缺陷；Docs/02 口径 "或给出调整记录"）
- AC-8 ✅：coverage.ps1 PASS + check.ps1 PASS + 铁律扫描通过

### 关键修复（批量模拟发现的既有缺陷）

| 缺陷 | 根因 | 修复 |
| --- | --- | --- |
| seed 309 抛 `InvalidOperationException: 召唤失败（战场已满）` | `SummonExecutor`（M4-T4）注释遗留 TODO："T4 不处理战场满，失败抛异常；T5 补"——战吼/亡语召唤效果在战场满 7 时执行 `Board.Add` 失败即抛异常，违反 NFR-7 | 满场软失败：`CanAdd()` 为 false 时跳过召唤不抛异常（Docs/01 "满场时不能召唤"）；补满场防回归测试 |

### 自检（Docs/04 §7 六步）

1. **编译与门禁** ✅：kernel 837 全绿 + check.ps1 PASS
2. **铁律扫描** ✅：Application 层纯 BCL；批量 try/catch 为明确失败路径（Failure 进报告异常清单，非吞异常）
3. **文件行数** ✅：AiVsAiSimulator.cs ~160 行 / SimulationModels.cs ~155 行，方法 ≤ 50 行
4. **测试覆盖** ✅：kernel +6 例（5 例模拟器 + 1 例满场防回归，831→837），AiVsAiSimulator 行覆盖 78%/80%（容错分支靠注入用例覆盖）
5. **AC 对齐** ✅：AC-1~8 全部有证据（AC-7 以 M7-B1 调整记录口径达成）
6. **改动边界** ✅：未动 GreedyAiAgent / TurnGuard / AgentMatchRunner / RuleEngine / MatchController；SummonExecutor 修复属 Docs/01 已有口径的缺陷修复（先红后绿）

### 待确认问题处置

1. 胜率口径 `P0Wins / 完成局数` 按最小影响方案采用；报告同时输出 Draws（本批 0 平局）
2. 500 局测试实测 ~1s（含在 kernel 全量 1s 内），无性能问题
3. 胜率超区间 → M7-B1 调整记录，不改 AI 权重（待 M7-T5 / 内容平衡任务）
