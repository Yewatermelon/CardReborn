# 任务卡 · M6-T2 端到端 PlayMode 冒烟测试

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M6-T2 |
| 所属里程碑 | M6 垂直切片打通（Demo Gate） |
| 上游需求 | Docs/02 M6-T2（自动化脚本：开局→出牌→攻击→结束回合→分胜负；测试通过且可重复） |
| 规则依据 | Docs/02 第 202 行 |
| 预估 | 1 会话 |
| 依赖任务 | M6-T1（BattleComposition 组合根）；M4-T2（MatchController 命令流水线）；M4-T3（GameEvent） |

## 2. 目标（一句话）

> 用 PlayMode 自动化测试驱动完整对局：经 BattleComposition 真实装配开局 → 驱动器尝试出牌/攻击/结束回合 → 终局判定 → 断言事件流与胜负结果，可重复运行。

## 3. 范围（做什么）

### 3.1 测试驱动器（`7_Tests/PlayMode/BattleSmokeDriver.cs`）

纯 C# 静态类（`internal static`），不依赖 UnityEngine（可被 PlayMode 测试直接调用）：

- `Drive(MatchController controller, int maxSteps)` 主循环：
  1. 读 `controller.View`（只读状态，不持可写引用）
  2. 尝试出牌：遍历行动方手牌，找 `Cost ≤ Mana.Current` 的卡 → `PlayCardCommand(playerId, instanceId, TargetRef.None)`
     - 被拒 `TargetRequired` → 尝试选一个合法目标（敌方英雄/随从）重提
     - 仍被拒 → 跳过该卡
  3. 尝试攻击：遍历行动方战场随从 → `AttackCommand(playerId, minionId, TargetRef.ForHero(enemySeat))`
     - 被拒（嘲讽/召唤失调/已攻击）→ 尝试攻击嘲讽随从 → 仍被拒则跳过
  4. 本轮无任何命令被接受 → 提交 `EndTurnCommand` 切换行动方
  5. `IsFinished` 或达到 `maxSteps` 停止
  6. 返回 `(int stepsExecuted, int playsAccepted, int attacksAccepted)` 统计

### 3.2 PlayMode 测试（`7_Tests/PlayMode/BattleSmokeTests.cs`）

`[UnityTest]` 协程形式（PlayMode 要求），但逻辑同步执行（`yield return null` 每步让出一帧让 Unity 主循环跑）：

1. **开局**：`BattleComposition.LoadDatabase`（真实生成物目录）→ `BuildDeckKeys` → `StartMatch`（固定种子 DemoSeed）
2. **驱动**：`BattleSmokeDriver.Drive(controller, maxSteps: 200)`
3. **断言**：
   - 游戏在 200 步内终局（`IsFinished == true`）
   - `playsAccepted > 0`（至少打出过一张牌）
   - `attacksAccepted > 0`（至少执行过一次攻击）——若牌池无低费冲锋怪导致首局无攻击，此项放宽为"事件日志含 Attack 事件或 attacksAccepted > 0"
   - 事件日志非空且含 `MatchEndEvent`
   - 终局时 `View.IsFinished == true`，至少一方英雄 `Health ≤ 0` 或疲劳致死
   - 重复运行两遍结果一致（固定种子可复现）

### 3.3 不改生产代码

- T2 只写测试与驱动器，不改 Bootstrap/Domain/Application/Presentation。
- 如发现生产代码缺陷，单开 fix 提交并登记，不在 T2 里混提。

## 4. 明确不做（防止范围蔓延）

- **不做 UI 点击模拟**：T1 已验证 UI 人工可操作；T2 只测权威管线（Bootstrap → MatchController → Settlers → Events → State → Outcome），不经过 Button/RectTransform。
- **不做策略 AI**：M7 范围；T2 驱动器是"能出就出、能打就打"的冒烟级，不做最优选牌/选目标。
- **不做合法行动枚举/高亮查询**：T1 明确不做，T2 同样不做；驱动器靠"试错→接受/拒绝"推进。
- **不做 PlayMode 场景加载测试**：场景由 Editor 菜单生成（T1 已验），T2 不加载 .unity 场景。
- **不改规则三层**：如遇缺陷单开 fix。

## 5. 接口约定

```csharp
// 7_Tests/PlayMode/BattleSmokeDriver.cs
namespace Card.Tests.PlayMode
{
    internal static class BattleSmokeDriver
    {
        internal static DriveResult Drive(MatchController controller, int maxSteps);
    }

    internal readonly struct DriveResult
    {
        public int StepsExecuted { get; }
        public int PlaysAccepted { get; }
        public int AttacksAccepted { get; }
        public int TurnsTaken { get; }
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | PlayMode 测试启动 | BattleComposition.LoadDatabase 成功（卡数 > 0）；StartMatch 返回非 null 控制器；首帧 View.TurnNumber == 1 |
| AC-2 | 驱动器出牌 | playsAccepted > 0（至少一张牌被打出并被接受） |
| AC-3 | 驱动器攻击 | attacksAccepted > 0 或事件日志含 Attack 事件 |
| AC-4 | 终局 | IsFinished == true，步数 ≤ 200 |
| AC-5 | 胜负判定 | 事件日志含 MatchEndEvent；终局至少一方 Health ≤ 0 或疲劳致死 |
| AC-6 | 可重复 | 同种子连跑两遍，步数与事件数一致 |
| AC-7 | 编译与门禁 | 0 error / 0 warning；check.ps1 PASS；kernel 750 不变（T2 不新增 kernel 测试） |

## 7. 测试要求

- 测试类型：PlayMode（`[UnityTest]` 协程）。
- 最少用例数：1（完整对局端到端）。
- 必须覆盖的边界：出牌被拒→转目标重试、攻击被拒（嘲讽/召唤失调）→跳过、疲劳终局路径。
- kernel 工程不受影响（PlayMode 目录不在 kernel 编译范围）。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M6-T2 状态 + 证据）。
- 需更新的配置表：无。
- 是否影响既有模块：否（纯测试加法）。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 `Docs/04` 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。固定种子（DemoSeed = 20261007）保证可复现；牌池含低费随从（M2 入库 35 启用卡含 cost 1~2 随从），首回合即可出牌。

---

## 11. 结论与证据（2026-10-08 收官）

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑确认）。
- **测试**：无 Unity 工具链 kernel **750 passed / 0 failed**（T2 不新增 kernel 测试，不变）；覆盖率 `0_Core 96.52%` / `Domain + App 91.52%`；`check.ps1` PASS（290 文件）。Unity 编辑器 Test Runner（EditMode + PlayMode）用户实跑 **933 passed / 0 failed**（2026-10-08），含 PlayMode 新增 2 例（完整对局端到端 + 配置缺失路径）。
- **铁律自查**：纯测试加法，不改规则三层（0_Core/1_Domain/2_Application 零改动）；驱动器只读 `IReadOnlyMatchState`（`controller.View`），不持可写引用、不判规则（R4）；无 `Find`/`static Instance`（R8）；无 `Resources.Load`（U-2）；单文件 ≤ 300 行（驱动器 166 行 / 测试 79 行）。

### 11.2 设计要点

1. **驱动器靠"试错→接受/拒绝"推进**：不复制 RuleEngine 逻辑，每张牌先试 `TargetRef.None` → 被拒 `TargetRequired` 再试敌英雄/敌随从/己随从；攻击先试敌英雄 → 被拒（嘲讽/失调/已攻击）再试敌随从。这同时验证了 RuleEngine 的校验路径。
2. **物化快照**：`active.Hand.Cards.ToList()` 在 Submit 改底层状态前物化，避免边遍历边改。
3. **可重复性**：固定种子 `DemoSeed = 20261007`，两遍运行的步数/出牌数/攻击数/事件数一致（AC-6 断言）。

### 11.3 P3 观察（不阻塞）

- 驱动器不做最优选牌/选目标（M7 范围）；当前"能出就出"策略可能导致次优出牌（如高费牌优先于低费牌），但冒烟测试只验证管线完整性，不验证策略。
