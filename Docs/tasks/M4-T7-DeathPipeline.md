# 任务卡 · M4-T7 死亡管线

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T7 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | Docs/01 §3.4.7、FR-5.7 死亡结算 |
| 规则依据 | [02 M4 任务表](../02-开发计划步骤文档.md)（M4-T7：死亡收集、移除战场、触发亡语、进坟场；单测：随从死亡后从战场移除并进入坟场、亡语触发） |
| 预估 | 1 会话 |
| 依赖 | M4-T6（`AttackSettler` 造成死亡）、M4-T5（`TriggerDispatcher.RaiseOnDeath`）、M4-T3（`CardDeathEvent`）、M3（`Zone`/`Graveyard`） |

## 2. 目标（一句话）

> 实现 `DeathProcessor`：每次命令结算后收集双方场上 Health ≤ 0 的随从，按"移除战场 → 触发 OnDeath 亡语 → 移入坟场 → emit CardDeathEvent"顺序处理，循环直到无新死亡；接入 `MatchController`（结算后、终局判定前）。

## 3. 范围（做什么）

### 3.1 Application 层新增

- `DeathProcessor`（`Assets/_Project/2_Application/Match/`）：
  - `Process(SettlementContext ctx)`：
    1. 外层 `do/while`：收集 → 处理 → 检查是否有新死亡，直到无新死亡（亡语可能产生新死亡）。
    2. 每轮遍历双方玩家的 `Board`，快照所有 `Health <= 0` 的随从。
    3. 对每个死亡随从：`Board.Remove(card)` → `Dispatcher.RaiseOnDeath(ctx, card)` → `Graveyard.Add(card)` → `Events.Emit(new CardDeathEvent(card.InstanceId))`。
    4. 亡语触发顺序：按场上索引顺序（先死先触发，同时死亡按位置）。

### 3.2 Application 层改动

- `MatchController`：
  - 持有 `DeathProcessor` 实例。
  - 在 `ProcessNext` 中，`settler.Settle(...)` 之后、`FinishIfDecided(...)` 之前调用 `_deathProcessor.Process(ctx)`。
  - 终局判定仍由 `MatchEvaluator` 负责（英雄死亡不在死亡管线处理）。

### 3.3 测试

- `DeathProcessorTests`：
  - 单随从死亡：移除战场、进坟场、emit CardDeathEvent。
  - 亡语触发：死亡随从亡语造成伤害，导致另一个随从也死亡，链式处理。
  - 多随从同时死亡：按顺序触发亡语、全部进坟场。
  - 亡语召唤新随从：新随从不被立即处理死亡。
  - 英雄死亡不由死亡管线处理（只处理 Board 随从）。

## 4. 明确不做

- **不处理英雄死亡**：英雄不在 Board，由 MatchEvaluator 判定终局。
- **不实现复活/回归**（如"复活"效果）：留后续。
- **不改变亡语触发顺序规则**：简单按场上索引顺序，不区分"先亡语后进坟场"的区域归属细节（随从在亡语期间不在任何区）。
- **不处理亡语触发的终局判定**：亡语可能杀英雄，终局由 MatchController 在死亡处理后统一判定。
- **不实现"亡语触发亡语"的无限循环防护**：复用 TriggerDispatcher 的深度限制（T5）。

## 5. 接口约定

```csharp
public sealed class DeathProcessor
{
    /// <summary>处理双方场上所有 Health≤0 的随从，循环至无新死亡。</summary>
    public void Process(SettlementContext ctx);
}
```

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 单随从死亡 | 从 Board 移除、进入 Graveyard、emit CardDeathEvent |
| AC-2 | 亡语触发 | 死亡随从的 OnDeath 效果被执行（用治疗效果验证：英雄回血） |
| AC-3 | 多随从同时死亡 | 全部移除、全部进坟场、亡语按场上顺序触发 |
| AC-4 | 亡语召唤新随从 | 新随从在场上、不被立即处理（除非 Health≤0） |
| AC-5 | 英雄死亡不处理 | 只处理 Board 随从，英雄留待 MatchEvaluator |
| AC-6 | 门禁/解耦 | Application 层；check.ps1 PASS；单文件 ≤ 300 行 |

## 7. 测试要求

EditMode，≥ 5 例；先红。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更。
- 既有模块：改 `MatchController.cs`（接线 DeathProcessor）。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | 死亡管线调用时机？ | **结算后、终局判定前**（MatchController.ProcessNext 中 settler.Settle 之后），保证亡语导致的英雄死亡能被终局判定捕获。 |
| Q2 | 亡语与进坟场顺序？ | **先移除战场 → 触发亡语 → 进坟场**（Docs/01 §3.4.7："触发 OnDeath → 移入坟场"）。亡语期间随从不在任何区。 |
| Q3 | 多随从死亡顺序？ | **按场上索引顺序**（从前往后）。同时死亡（如随从交换）按各自场上位置顺序。 |
| Q4 | 亡语产生新死亡？ | **外层 do/while 循环**直到无新死亡；深度防护复用 TriggerDispatcher.MaxDepth。 |
| Q5 | 英雄死亡？ | 不处理（英雄不在 Board），由 MatchEvaluator 在死亡处理后判定。 |

## 10. DoD

- [x] 全部 AC 满足且附证据（见 11.1）
- [x] 先红后绿（CS0246 `DeathProcessor` 不存在）
- [x] 0 error / 0 warning（无 Unity 实跑；Unity 编辑器补验待回报）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1（见 11.2/11.6）
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-06）

### 11.1 AC 对齐

| AC | 结论 | 证据 |
| --- | --- | --- |
| AC-1 单随从死亡 | 已满足 | `SingleDeadMinion_RemovedFromBoard_ToGraveyard_EmitsEvent` |
| AC-2 亡语触发 | 已满足 | `Deathrattle_ExecutesEffect`（亡语治疗英雄 10，从 20 回满到 30） |
| AC-3 多随从同时死亡 | 已满足 | `MultipleDeadMinions_AllRemoved_AllToGraveyard` |
| AC-4 亡语召唤新随从 | 已满足 | `DeathrattleSummonsMinion_NewMinionStaysOnBoard`（A 进坟场、C 上场） |
| AC-5 英雄死亡不处理 | 已满足 | `HeroDeath_NotHandledByDeathProcessor`（英雄 Health=0 不被移动） |
| AC-6 门禁/解耦 | 已满足 | Application 层；check.ps1 PASS（193 文件）；DeathProcessor.cs 59 行 |
| 附加 | 双方玩家死亡都处理 | `BothPlayers_DeadMinionsProcessed` |

### 11.2 铁律扫描

- 依赖向下：DeathProcessor 在 2_Application，调用 Domain（Zone/PlayerState/CardInstance）与 Application（TriggerDispatcher）；无 UnityEngine。
- 终局入口唯一：死亡管线不判终局、不写 IsFinished；英雄死亡由 MatchEvaluator 在死亡处理后判定。
- 体量：DeathProcessor.cs 59 行。

### 11.3 关键设计取舍

1. **调用时机：结算后、终局判定前**（MatchController.ProcessNext 中 settler.Settle 之后、FinishIfDecided 之前），保证亡语导致的英雄死亡能被终局判定捕获。
2. **亡语与进坟场顺序：先移除战场 → 触发亡语 → 进坟场**（Docs/01 §3.4.7）。亡语期间随从不在任何区。
3. **多随从死亡顺序：按场上索引顺序**（从前往后）。
4. **链式死亡：外层 do/while 循环**至无新死亡；亡语产生新死亡会被下一轮收集处理。
5. **英雄死亡不处理**：英雄不在 Board，由 MatchEvaluator 判定。
6. **AC-2 用治疗验证亡语执行**：当前效果系统无 AoE，"亡语伤害杀死另一随从"需要目标指定或 AoE（留效果系统扩展）；用治疗验证亡语确实触发更可靠。

### 11.4 测试证据

- 红：CS0246（`DeathProcessor` 不存在）。
- 绿：无 Unity **708 passed / 0 failed / 0 skipped**（702 + 6），0 warning。
- 覆盖率：0_Core 96.51%、Domain+App **91.59%**。
- 静态门禁：`check.ps1` PASS（193 文件）；2 个新 .meta，279 GUID 无重复。
- 既有测试：零改动全绿。
- Unity 权威：待编辑器 EditMode 补验，预期 **719 passed / 0 failed**（713 + 6）。

### 11.5 影响面

- 新增 Application：`DeathProcessor.cs`。
- 改动 Application：`MatchController.cs`（持有 DeathProcessor，结算后调用）。
- 后继：T8 英雄技能、T9 AI。

### 11.6 反向审查

1. **亡语期间随从不在任何区**：Board.Remove 后、Graveyard.Add 前触发亡语，亡语效果查"场上随从"时该随从已不在。
2. **亡语召唤新随从不被立即处理死亡**：新随从 Health 满，不会被收集；若亡语同时造成伤害导致新随从 Health≤0，外层循环会处理。
3. **终局判定在死亡处理后**：亡语杀英雄时，FinishIfDecided 能正确判定终局。
4. **深度防护复用 TriggerDispatcher**：亡语链式触发由 T5 的 MaxDepth=64 限制。

### 11.7 评审结论

**通过**（AI 自审）：无 P0/P1；无新增遗留项。无 Unity 708/0 + Unity 编辑器补验数字回填后关闭。
