# 任务卡 · M3-T7 胜负判定

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T7 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.6（英雄 30 血归零判负）、FR-5.9（疲劳机制）、FR-13.2（无 Unity 跑通整局） |
| 规则依据 | Docs/01 §3.7 疲劳与胜负条件 |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T6（状态骨架、命令模型、规则校验） |

## 2. 目标（一句话）

> 提供应用层 `MatchEvaluator`：检查 `MatchState` 中双方英雄生命是否归零，判定胜负/平局/继续，并补疲劳计数与爆牌的状态字段，为 M4 抽牌结算做准备。

## 3. 范围（做什么）

1. 新增 `MatchEvaluator`（静态类，命名空间 `Card.Application.Match`）：
   - `MatchResult Evaluate(MatchState state)`：返回 `Ongoing / Player0Wins / Player1Wins / Draw`
   - 判定规则：双方英雄均 ≤0 → `Draw`；单方 ≤0 → 对方胜；否则 `Ongoing`
2. 新增 `MatchResult` 枚举（Domain）：`Ongoing / Player0Wins / Player1Wins / Draw`
3. 新增 `MatchOutcome` 记录（Domain）：封装结果 + 胜者 ID + 回合数 + 结束原因
4. 补状态字段：
   - `PlayerState.FatigueCounter`（int，初始 0）：疲劳计数，M4 抽牌结算时递增
   - `MatchState.IsFinished`（bool）：终局标记，防止重复结算
5. `RuleEngine.Validate` 追加：当 `IsFinished` 时任何命令返回 `InvalidTarget`（复用）+ Detail 说明

## 4. 明确不做（防止范围蔓延）

- 不做抽牌结算（疲劳伤害递增、爆牌不入手）：M4-T2 效果系统。
- 不做投降命令（SurrenderCommand）：P1，FR-5.11 标记为 P1。
- 不做超时判负：Docs/01 标记为"可选"。
- 不做结算界面/奖励发放：M6 UI 层。
- 不做回合状态机流转：M4-T1/T2。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public enum MatchResult { Ongoing, Player0Wins, Player1Wins, Draw }

    public sealed class MatchOutcome
    {
        public MatchResult Result { get; }
        public int? WinnerId { get; }
        public int TurnNumber { get; }
        public string Reason { get; }
    }
}

namespace Card.Application.Match
{
    public static class MatchEvaluator
    {
        public static MatchOutcome Evaluate(MatchState state);
    }
}
```

- `Evaluate` 只读查询，不修改状态；调用方（M4 状态机）负责根据结果设 `IsFinished`。
- `MatchOutcome` 为不可变数据，供事件/日志/UI 使用。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 双方英雄均 >0 | `Ongoing` |
| AC-2 | 座 0 英雄 ≤0，座 1 >0 | `Player1Wins` |
| AC-3 | 座 0 >0，座 1 ≤0 | `Player0Wins` |
| AC-4 | 双方均 ≤0 | `Draw` |
| AC-5 | 座 0 恰好 0（边界） | `Player1Wins`（≤0 判负） |
| AC-6 | 终局后 RuleEngine 拒绝出牌 | `InvalidTarget` + "对局已结束" |
| AC-7 | 终局后 RuleEngine 拒绝结束回合 | `InvalidTarget` + "对局已结束" |
| AC-8 | `FatigueCounter` 初始为 0 | 0 |
| AC-9 | `IsFinished` 初始为 false | false |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/MatchEvaluatorTests.cs`）。
- 最少用例数：9（每条 AC 一条）。
- 数据构造：手动构造 `MatchState` + 直接设 `Hero.Health`；用 `RuleEngine` 验证终局后命令被拒。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡。
- 需更新的配置表：无。
- 生产代码改动：
  - 新增 `1_Domain/Match/MatchResult.cs`、`1_Domain/Match/MatchOutcome.cs`
  - 修改 `1_Domain/Match/PlayerState.cs`（+`FatigueCounter`）
  - 修改 `1_Domain/Match/MatchState.cs`（+`IsFinished`）
  - 新增 `2_Application/Match/MatchEvaluator.cs`
  - 修改 `2_Application/Match/RuleEngine.cs`（+`IsFinished` 检查）
- 是否影响既有模块：`MatchState`/`PlayerState` 加字段为增量兼容；`RuleEngine` 加检查不改变合法命令行为。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（9 例 EditMode，555/555 通过）
- [x] 测试通过且覆盖边界（Ongoing/Player0Wins/Player1Wins/Draw/恰好 0 血/终局后命令拒绝/字段默认值）
- [x] 编译 0 error / 0 warning（`coverage.ps1` 编译 + 测试 PASS）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（纯 BCL；无继承表达；`MatchEvaluator` 只读校验）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS.md + 本任务卡）

## 10. 自检与评审结论（2026-07-02）

### 10.1 红绿验证

- **红**：`CS0246`（MatchOutcome 不存在）、`CS0234`（MatchEvaluator 不存在）。
- **绿**：555 passed / 0 failed（546 既有 + 9 新增）。
- **覆盖率**：MatchEvaluator 100%；汇总 0_Core 96.51% / Domain + App 89.88%。

### 10.2 铁律自查

| 铁律 | 结果 |
| --- | --- |
| 1. 一切皆组件 | ✅ 无继承表达；MatchEvaluator 为静态校验器 |
| 2. 依赖向下 | ✅ `Card.Application.Match` → `Card.Domain.Match` |
| 3. 规则三层只依赖 BCL | ✅ 无 `UnityEngine`/`Debug.Log` 等 |
| 5. 所有操作走 GameCommand | ✅ `RuleEngine` 终局检查消费 `MatchState.IsFinished` |
| 6. 数值来自配置 | ✅ 无硬编码 |
| 10. 单文件 ≤300 行 | ✅ MatchResult 16 行、MatchOutcome 32 行、MatchEvaluator 36 行、RuleEngine +5 行 |
| 11. 内核与 Unity 解耦 | ✅ 纯 BCL |

### 10.3 未关闭问题

- **P0/P1**：无。
- **P2**：M3-B1（Unity 批处理验证受 TRAE 沙箱阻塞，沿用 T3–T6 决策暂缓）。

### 10.4 关键决策

1. **终局后统一 `InvalidTarget`**：`RuleEngine.Validate` 在 `IsFinished` 时拒绝所有命令，错误码复用 `InvalidTarget`（语义"目标无效"扩展为"状态无效"）。
2. **HeroState 构造器下限从 1 放宽到 0**：允许构造后归零（运行期允许），但构造时仍不允许负数；Draw 测试用 `Hero.Health = -3` 设置。
3. **Draw 判定优先于单方判负**：双方同时归零时返回 `Draw`，不偏向任何一方。
