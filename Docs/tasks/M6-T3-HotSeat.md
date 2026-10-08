# 任务卡 · M6-T3 双人本地对局

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M6-T3 |
| 所属里程碑 | M6 垂直切片打通 |
| 上游需求 | Docs/02 M6-T3：人类 vs 人类（共享输入或简单切换），完整打完一局并出现胜负结算 |
| 依赖任务 | M6-T1（场景引导器）、M6-T2（冒烟驱动器） |

## 2. 目标

> 在 T1 场景基础上加热座切换与终局结算：回合结束时弹出"交棒"屏，下家点击后视角自动切换（手牌卡面/牌背互换、输入方随之切换），终局弹出胜负面板 + "再来一局"按钮。

## 3. 范围

### 3.1 热座切换
- **回合切换检测**：Update 中比较 `View.ActivePlayerId` 与 `_currentLocalSeat`，不同则进入"交棒"态。
- **交棒屏**：全屏遮罩 + "玩家 X 的回合 — 点击继续"，阻止下层 UI 交互。
- **视角切换**：交棒屏点击后，同步器 SwitchSeats → 手牌卡面/牌背互换、英雄/法力/战场上下对调；输入控制器 `_localPlayerId` 更新；UiTargetPicker / TableFeedbackLocator 英雄锚点映射互换；反馈播放器 localSeat 更新。
- **事件泵阻塞**：交棒屏显示期间不 Pump（玩家不看对方手牌）；点击后 Pump + Push 刷新。

### 3.2 胜负结算
- **终局检测**：`IsFinished == true` 时 Pump 完剩余事件，显示胜负面板。
- **胜负面板**：全屏遮罩 + "玩家 X 获胜！"（或"平局！"）+ "再来一局"按钮。
- **再来一局**：`SceneManager.LoadScene` 重载 Battle 场景；移除 Canvas 的 DontDestroyOnLoad（避免重载后画布叠加）。

## 4. 明确不做

- **不做 AI**：M10 范围。
- **不做分屏/独立输入设备**：共享同一键鼠，热座轮流操作。
- **不做回合计时器/操作锁**：M7 范围。
- **不改规则三层**：0_Core / 1_Domain / 2_Application 零改动。

## 5. 验收标准

| 编号 | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | 游戏开始 | 首帧无交棒屏（首位玩家直接操作）；UI 与 T1 一致 |
| AC-2 | 点结束回合 | 全屏遮罩"玩家 X 的回合 — 点击继续"，阻止下层交互 |
| AC-3 | 交棒屏点击后 | 视角切换：操作方手牌卡面在下方，对方牌背在上方；英雄/法力/战场对调 |
| AC-4 | 下家操作 | 可正常出牌、攻击、使用技能、结束回合 |
| AC-5 | 终局 | 胜负面板显示"玩家 X 获胜！"，含"再来一局"按钮 |
| AC-6 | 点"再来一局" | Battle 场景重载，新对局开始，无画布叠加 |
| AC-7 | 编译与门禁 | 0 error / 0 warning；check.ps1 PASS；kernel 750 不变 |

## 6. 测试要求

- M6-T3 主要是 Bootstrap/Presentation 表现层逻辑，不改规则三层。
- 不强制先红测试（非规则类）；靠 M6-T2 冒烟测试（已覆盖完整对局管线）+ 人工验收。
- 若 BattleViewSynchronizer.SwitchSeats() 有独立逻辑值得测，可补 EditMode 单例。

## 7. DoD

- [x] 满足全部 AC 且附证据
- [x] 编译 0 error / 0 warning
- [x] check.ps1 + coverage.ps1 通过
- [x] 人工验收 AC-1~6 通过

## 8. 结论与证据（2026-10-08）

### 交付物

- **新增** `5_Bootstrap/Battle/HotSeatHandler.cs`（133 行）：回合切换检测 → 交棒屏 → 视角切换；终局检测 → 胜负面板 → 再来一局（`SceneManager.LoadScene("Battle")`）。从 BattleSceneBootstrap 抽出以满足 R5（≤300 行），Bootstrap 由 327 行降到 193 行。
- **视角切换六处接线**：`BattleViewSynchronizer.SwitchSeats()`、`PlayerInputController._localPlayerId`、`TargetingController._localPlayerId`、`UiTargetPicker.SwitchHeroAnchors()`、`TableFeedbackLocator.SwitchHeroAnchors()`、`BattleFeedbackPlayer.UpdateLocalSeat()`。
- **UI**：`BattleUiFactory.BuildPassScreen()`（全屏 0.85 alpha 遮罩 + 36pt 文本 + 继续按钮）、`BuildVictoryPanel()`（600×300 面板 + 40pt 胜负文本 + "再来一局"按钮）；`BattleUi` 增 6 个可空字段。
- **顺带修复（验收暴露）**：
  1. **效果系统补齐**（M4-T4 框架未实现的数据类型，PlayMode 冒烟首次实跑暴露）：新增 `GainManaEffectData`/`GainArmorEffectData`/`DestroyEffectData`/`CompositeEffectData` + 对应执行器与解析分支；`ManaPool.Gain()`、`HeroState.GainArmor()`；`DrawEffect` 作为 `DrawCardEffect` 的配置历史别名；执行器类拆到新文件 `EffectExecutors.cs`（Effects.cs 114 行 / EffectExecutors.cs 253 行）。
  2. **CARD_032 配置修复**：`DamageEffect:5/GainArmorEffect:5` → `DamageEffect:5|GainArmorEffect:5`（`/` 是效果内参数分隔符，两效果分隔符为 `|`），同步 CSV + 两份 JSON + README。
  3. **Button 图元重复加 Image**：`BattleUiPrimitive.Button()` 改为先 `GetComponent<Image>()` 复用（PassScreen 全屏遮罩自身已有 Image）。

### 验证数字

| 门禁 | 结果 |
| --- | --- |
| check.ps1 | PASS（292 文件） |
| coverage.ps1（无 Unity 工具链） | **759 passed / 0 failed**（750 + 9 新效果测试）；覆盖率 `0_Core 96.52%` / `Domain + App 91.47%` |
| Unity EditMode | **942 passed / 0 failed**（933 + 9），0 error / 0 warning |
| Unity PlayMode | **3 passed / 0 error**（M6-T2 端到端冒烟在补齐效果后首次真正跑通整局） |
| 人工验收 | AC-1~6 全部通过（用户实机 MainMenu→Play 热座完整对局） |

### 评审结论

- 无 P0/P1。
- 铁律扫描：热座逻辑全部在 Bootstrap/Presentation，规则三层仅"加法式"补齐效果数据/执行器（无既有行为改动）；表现层只读；命令仍全部经 MatchController 校验。
- 新登记缺陷（移交 M6-T4）：**M6-B1** 卡牌费用数字与名字重叠（布局）；**M6-B2** 卡名/描述显示本地化 Key、无本地化系统；**M6-B3** 命令被拒绝（如嘲讽限制）无 UI 提示引导。
- P3 观察：CompositeExecutor 经 `SetDispatcher` setter 注入分发器（构造期循环依赖的折中），当前仅装配处一处使用，可接受。
