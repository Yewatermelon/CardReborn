# 任务卡 · M3-T8 疲劳与爆牌

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T8 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.9（疲劳机制：牌库空后抽牌受递增伤害）、FR-5.3（抽牌）、FR-13.2（无 Unity 跑通整局） |
| 规则依据 | Docs/01 §3.7 规则表第 111 行（疲劳）、手牌上限第 10 张爆牌；Docs/02 第 148 行验收 |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T7（Zone/卡实例/`FatigueCounter` 字段/MatchEvaluator） |

## 2. 目标（一句话）

> 提供应用层 `CardDrawService`：封装"抽 N 张牌"的系统结算——牌库有牌则入手（手牌满则爆牌销毁）、牌库空则触发递增疲劳伤害，产出纯数据 `DrawOutcome`，供 M4 回合状态机与抽牌效果复用。

## 3. 范围（做什么）

1. 新增 `DrawOutcome`（Domain，不可变数据）：
   - `DrawnInstanceIds`：本次成功入手牌的实例 Id 列表
   - `BurnedInstanceIds`：手牌满被爆掉的实例 Id 列表
   - `FatigueDamage`：本次疲劳伤害总量
   - `FinalFatigueCounter`：结算后的疲劳计数
   - `HeroDied`：结算后英雄生命 ≤0
2. 新增 `CardDrawService`（Application，静态类）：
   - `DrawOutcome Draw(PlayerState player, int count)`
   - 逐张结算（沿用 T5 约定：牌库顶 = `Deck.Cards[Count-1]`）：
     - 牌库有牌且手牌可加入 → `Hand.MoveIn(top, Deck)`
     - 牌库有牌但手牌已满 → 移出 Deck、加入 Graveyard（爆牌，不入手牌）
     - 牌库为空 → `FatigueCounter += 1`，`Hero.Health -= FatigueCounter`
3. 非法参数：`player == null` 抛 ArgumentNullException；`count <= 0` 抛 ArgumentOutOfRangeException（不吞异常）。

## 4. 明确不做（防止范围蔓延）

- 不做回合开始抽牌的触发与阶段流转：M4 回合状态机调用本服务。
- 不做抽牌事件/动画/日志广播：M4 事件系统（本服务只返回数据，调用方据此发事件）。
- 不做战吼/亡语/过牌效果组件接入：M4 效果系统。
- 不做疲劳伤害对护甲的抵扣：M4 伤害/战斗系统统一处理（本任务直接扣生命）。
- 不做疲劳致死中途停止批量抽牌：本任务逐张处理完所有请求（回合抽牌恒为 1 张；批量效果为 M4 场景），假设登记于此。
- 不做同名卡≤2 校验、投降、超时。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public sealed class DrawOutcome
    {
        public IReadOnlyList<int> DrawnInstanceIds { get; }
        public IReadOnlyList<int> BurnedInstanceIds { get; }
        public int FatigueDamage { get; }
        public int FinalFatigueCounter { get; }
        public bool HeroDied { get; }
    }
}

namespace Card.Application.Match
{
    public static class CardDrawService
    {
        public static DrawOutcome Draw(PlayerState player, int count);
    }
}
```

- 只修改传入 `PlayerState` 的 Zone/`FatigueCounter`/`Hero.Health`；无静态可变状态。
- 不依赖 `CardDatabase`（实例已在状态中，无需再查配置）。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 空库抽第 1 张 | 生命 -1，FatigueCounter=1，FatigueDamage=1 |
| AC-2 | 再抽第 2 张 | 生命再 -2，counter=2，damage=2 |
| AC-3 | 再抽第 3 张 | 生命再 -3，counter=3，damage=3（Docs/02 验收：1/2/3） |
| AC-4 | 空库一次抽 3 张 | 总伤害 6，counter=3，无入手/爆牌 |
| AC-5 | 疲劳伤害致英雄归零 | HeroDied=true，生命 0 |
| AC-6 | 牌库有牌抽 1 张 | 顶牌入手牌、Deck -1、无疲劳 |
| AC-7 | 手牌满 10 张时抽牌 | 顶牌不在手牌、Deck -1、Burned 含该实例、Graveyard +1 |
| AC-8 | 爆牌不触发疲劳 | FatigueDamage=0、counter 不变 |
| AC-9 | 爆牌后下次空库抽牌 | 疲劳仍从 1 起（爆牌不影响计数） |
| AC-10 | Draw 参数非法 | null 玩家抛 ArgumentNullException；count=0 抛 ArgumentOutOfRangeException |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/CardDrawServiceTests.cs`），无 Unity 工具链同样执行。
- 最少用例数：10（每条 AC 一条）。
- 数据构造：`new PlayerState` + 手动 `Deck.Add`/`Hand.Add` 真实 `CardInstance`；用自定义 RulesConfig(HandLimit 可配小值加速爆牌用例，或直接填满 10)。
- 边界：空库、空手牌满手牌、致死伤害、一次多张。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡。
- 需更新的配置表：无。
- 生产代码改动：新增 `1_Domain/Match/DrawOutcome.cs`、`2_Application/Match/CardDrawService.cs`。
- 是否影响既有模块：纯增量；不改动 Zone/PlayerState/MatchFactory/RuleEngine 任何既有签名（复用经验 100000219：增量扩展、接口稳定）。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（11 例 EditMode，566/566 通过）
- [x] 测试通过且覆盖边界（疲劳 1/2/3、一次 3 张、致死、正常抽牌、爆牌入坟场、爆牌不疲劳、计数连续、参数非法）
- [x] 编译 0 error / 0 warning（`coverage.ps1` 编译 + 测试 PASS）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（纯 BCL；增量新增不改既有签名；单文件 ≤300 行）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS.md + 本任务卡）

## 10. 自检与评审结论（2026-10-06）

### 10.1 红绿验证

- **红**：`CS0246`（DrawOutcome 不存在）、`CS0234`（CardDrawService 不存在）。
- **绿**：566 passed / 0 failed（555 既有 + 11 新增）。
- **覆盖率**：DrawOutcome 100%（18/18）、CardDrawService 100%（35/35）；汇总 0_Core 96.51% / Domain + App 90.13%。

### 10.2 铁律自查

| 铁律 | 结果 |
| --- | --- |
| 1. 一切皆组件 | ✅ 无继承表达；服务为静态类 |
| 2. 依赖向下 | ✅ Application → Domain |
| 3. 规则三层只依赖 BCL | ✅ 无 UnityEngine/Debug.Log 等 |
| 6. 数值来自配置 | ✅ 手牌上限走 `Hand.CanAdd()`（容量来自 RulesConfig），无硬编码 |
| 10. 单文件 ≤300 行 | ✅ DrawOutcome 39 行、CardDrawService 62 行 |
| 11. 内核与 Unity 解耦 | ✅ 纯 BCL；不依赖 CardDatabase |

### 10.3 未关闭问题

- **P0/P1**：无。
- **P2**：M3-B1（Unity 批处理验证暂缓，沿用既定决策）。

### 10.4 关键决策

1. **爆牌 Deck → Graveyard**：手牌满时抽到的卡移出牌库、入坟场，不入手牌、不触发亡语；调用方可从 `BurnedInstanceIds` 发事件。
2. **疲劳直接扣生命**：本任务不处理护甲抵扣（M4 伤害系统统一做）；顺序为"先递增计数再扣等量生命"。
3. **不中断批量结算**：逐张处理完全部请求（回合抽牌恒为 1；批量过牌为 M4 场景，到时如需可据 `HeroDied` 提前结束）。
4. **经验 100000219 落实**：纯增量新增，Zone/PlayerState/MatchFactory/RuleEngine 零改动。
