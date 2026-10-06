# 任务卡 · M3-T10 状态增量计算 ★

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T10 |
| 所属里程碑 | M3 领域模型与规则内核（最后一个任务） |
| 上游需求 | FR-13.5（两次状态之间可计算差异，用于下发增量补丁）、FR-14.4（状态下行） |
| 规则依据 | Docs/02 第 150 行（验收：单字段变更只产出对应变化；无变更时产出空列表） |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T9（完整状态模型、序列化） |

## 2. 目标（一句话）

> 提供应用层 `MatchStateDiffer.Diff(before, after)`：逐字段比较两个 `MatchState`，产出确定性顺序的 `StateChange` 列表（标量字段 Modified、分区成员增删 Added/Removed、卡牌运行时字段 Modified），供阶段二状态下发增量复用。

## 3. 范围（做什么）

1. 新增 `StateChange`（Domain，纯数据）：`ChangeKind { Modified, Added, Removed }` + `Path` + `OldValue`/`NewValue`（`JsonValue`，Added 的 old / Removed 的 new 为 `JsonValue.Null()`）。
2. 新增 `MatchStateDiffer`（Application 静态类）：`IReadOnlyList<StateChange> Diff(MatchState before, MatchState after)`。
3. 路径方案（与序列化器键一致）：
   - 根：`phase`（枚举整数值）、`turnNumber`、`activePlayerId`、`isFinished`
   - 玩家：`players[i].fatigueCounter`、`players[i].hero.{health,armor,powerUsedThisTurn}`、`players[i].mana.{max,current}`
   - 分区成员：`players[i].{deck,hand,board,graveyard}` → Added/Removed，值为实例 Id
   - 卡牌字段：`players[i].<zone>[k].{attack,health,keywords,statuses,attacksUsedThisTurn}` → Modified（仅双方都有的实例，按 after 状态下标定位）
4. 顺序确定性：根 → 玩家 0（疲劳→英雄→法力→分区 deck/hand/board/graveyard）→ 玩家 1。

## 4. 明确不做（防止范围蔓延）

- 不做增量补丁的下发/应用（客户端按路径取新值，属阶段二网络层）。
- 不做视野裁剪：diff 输入是全量状态，裁剪在阶段二外层。
- 不做卡牌跨分区移动的合并分析：移动天然产出"旧区 Removed + 新区 Added"。
- 不做 GameEvent 事件模型：M4。
- 不做命令流水录制（FR-13.6）：阶段二。
- 不做牌库顺序变化的检测：分区按实例 Id 集合比较，顺序本身不算变更（牌库顺序不下发，FR-14.5）。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public enum ChangeKind { Modified, Added, Removed }

    public sealed class StateChange
    {
        public ChangeKind Kind { get; }
        public string Path { get; }
        public JsonValue OldValue { get; }
        public JsonValue NewValue { get; }
    }
}

namespace Card.Application.Match
{
    public static class MatchStateDiffer
    {
        public static IReadOnlyList<StateChange> Diff(MatchState before, MatchState after);
    }
}
```

- 纯 BCL；依赖 `Card.Core.JsonValue` 表达值；null 参数抛异常。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 两个完全相同的状态 | 空列表 |
| AC-2 | 仅 turnNumber 变 | 1 条 Modified，path=turnNumber，old/new 为整数 |
| AC-3 | 仅 phase 变 | 1 条 Modified，值为枚举整数（Main=3→TurnEnd=4） |
| AC-4 | 仅 activePlayerId 变 | 1 条 Modified |
| AC-5 | 仅 isFinished 变 | 1 条 Modified，old/new 为布尔 |
| AC-6 | 仅疲劳变 | 1 条 Modified，path=players[0].fatigueCounter |
| AC-7 | 英雄生命/护甲/技能已用三处变 | 恰好 3 条，路径含 hero.* |
| AC-8 | 法力上限与当前变 | 恰好 2 条，路径含 mana.* |
| AC-9 | 分区增删卡 | Added（players[0].hand，new=实例 Id）+ Removed（players[0].deck，old=实例 Id） |
| AC-10 | 双方都在场的卡改生命/已攻击/关键词/状态 | 每字段 1 条 Modified，path 含 board[0].* |
| AC-11 | 多处变更（根+玩家 1） | 全部列出且顺序：根在前、玩家 0 先于玩家 1 |
| AC-12 | before/after 为 null | 抛 ArgumentNullException |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/MatchStateDifferTests.cs`）。
- 最少用例数：12（每条 AC 一条）。
- 数据构造：`BuildBaseState()` 每次生成全新状态（独立对象），分别变更后 diff；不依赖 T9 序列化。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡。
- 生产代码改动：新增 `1_Domain/Match/StateChange.cs`、`2_Application/Match/MatchStateDiffer.cs`（单文件预计 <200 行，无需 partial）。
- 是否影响既有模块：纯增量，零改动既有签名。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（12 例 EditMode，588/588 通过）
- [x] 测试通过且覆盖边界（相同状态空列表、每类单字段、分区增删、卡牌字段、顺序确定性、null 参数）
- [x] 编译 0 error / 0 warning（初版 Zone.Contains 误用 int 参数 + nullable 标注已修）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（纯 BCL；StateChange 纯数据；零改动既有签名）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS.md + 本任务卡）

## 10. 自检与评审结论（2026-10-06）

### 10.1 红绿验证

- **红**：`CS0246`（StateChange 不存在）、`CS0103`（MatchStateDiffer 不存在）。
- **绿**：588 passed / 0 failed / 0 warning（576 既有 + 12 新增）。
- **覆盖率**：StateChange 100%（11/11）、MatchStateDiffer 100%（76/76）；汇总 0_Core 96.51% / Domain + App 90.65%。

### 10.2 铁律自查

| 铁律 | 结果 |
| --- | --- |
| 1. 一切皆组件 | ✅ 无继承表达；Differ 为静态纯函数 |
| 2. 依赖向下 | ✅ Application → Domain → Core（JsonValue） |
| 3. 规则三层只依赖 BCL | ✅ 无 UnityEngine/Debug.Log |
| 10. 单文件 ≤300 行 | ✅ StateChange 36 行、MatchStateDiffer 116 行 |
| 11. 内核与 Unity 解耦 | ✅ 纯 BCL；只读比较、无副作用 |
| 12. 权威宿主解耦 | ✅ diff 结果即阶段二"状态增量下行"（FR-14.4）的数据基础 |

### 10.3 未关闭问题

- **P0/P1**：无。
- **P2**：M3-B1（Unity 批处理验证暂缓，M3 里程碑评审统一处理）。

### 10.4 关键决策

1. **路径键与 MatchStateSerializer 完全一致**：同一套 `players[i].<zone>[k].<field>` 方案，阶段二可直接用 path 从快照 JSON 取新值打补丁。
2. **分区按实例 Id 集合比较**：牌库顺序变化不产出变更（FR-14.5 牌库顺序不下发）；跨区移动 = 旧区 Removed + 新区 Added，不试图合并分析。
3. **值用 JsonValue 表达**：int/bool/null 统一承载，Domain 对 Core 的既有依赖方向不变。
4. **顺序确定性**：根 → 玩家 0 → 玩家 1；玩家内：疲劳 → 英雄 → 法力 → 分区（deck/hand/board/graveyard），使 diff 输出可 diff（git 友好、测试可断言）。
5. **踩坑记录**：`Zone.Contains` 只接受 `CardInstance`（无 int 重载），改用按 Id 建字典比较；`TryGetValue` 的 out 参数需 `CardInstance?`（kernel 工程 Nullable enable）。
