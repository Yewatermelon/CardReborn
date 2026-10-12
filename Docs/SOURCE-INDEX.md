# CardReborn 核心模块源码索引

> **用途**：面试准备 / 新 AI 快速定位 / 学习者按图索骥。
> 每个模块标注"面试必考度"和"建议阅读时长"，并给出"讲什么"的要点提示。

---

## 🔴 面试必考（面试官大概率会追问）

### 1. MatchController — 唯一权威驱动入口

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Application/Match/MatchController.cs` |
| 必考度 | ⭐⭐⭐⭐⭐ |
| 建议阅读 | 15 分钟 |

**讲什么**：
- FIFO 命令队列：RuleEngine 校验 → ICommandSettler 结算 → MatchEvaluator 终局判定，单入口
- MatchStepRecord 台账 vs GameEvent 事件（职责分离：台账是审计追踪，事件是表现订阅）
- MatchOutcome 扩展（M14-T5）：MatchId / StartTick / EndTick / DurationTicks，MatchEvaluator 传 dummy id/0 tick，终局时重新构造完整 MatchOutcome
- MatchRecord + 持久化：MatchRecordSerializer（Card.Core.JsonValue）+ MatchRecordPersister（persistentDataPath/MatchRecords/）

**关键依赖**：RuleEngine, MatchFactory, MatchEvaluator, IClock, ICommandSettler

---

### 2. RuleEngine — 规则校验与结算

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Application/Match/RuleEngine.cs` + `RuleEngine.Validate.cs` + `RuleEngine.Attack.cs` + `RuleEngine.HeroPower.cs` + `RuleEngine.Target.cs` |
| 必考度 | ⭐⭐⭐⭐⭐ |
| 建议阅读 | 20 分钟 |

**讲什么**：
- partial 拆分（主文件 105 行，合规铁律 10）
- 四类 Validate：PlayCard / Attack / HeroPower / EndTurn
- TargetRule 四类判定（None / Any / Enemy / Friendly）
- 风怒（2 次攻击）/ 嘲讽（必须先打嘲讽）/ 召唤失调（登场回合不能攻击）/ 圣盾（抵消一次）/ 剧毒（必杀）

---

### 3. EffectExecutor + TriggerDispatcher — 效果系统核心

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Application/Match/Effects.cs` + `TriggerDispatcher.cs` |
| 必考度 | ⭐⭐⭐⭐⭐ |
| 建议阅读 | 20 分钟 |

**讲什么**：
- 类型字典分发：`_executors = new Dictionary<string, IEffectExecutor>`，按效果名查执行器
- 递归深度 64 防循环，超限抛 InvalidOperationException（含"深度"）
- TriggerDispatcher 三种触发：RaiseOnPlay / RaiseOnDeath / RaiseOnTurnStart / RaiseOnTurnEnd / RaiseOnSummon
- EffectContext 注入 Settlement（效果可以访问结算上下文）

---

### 4. MatchHost — 权威宿主

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Network/MatchHost.cs` |
| 必考度 | ⭐⭐⭐⭐⭐ |
| 建议阅读 | 25 分钟 |

**讲什么**：
- 共享库：纯 BCL，不引用 UnityEngine，服务器端可直接复用
- 座位化：`_seats = new Dictionary<int, PeerSession>`，每个座位独立 seq 基线
- 命令信封：`CommandEnvelope { seq, command }`，seq 幂等（同 seq 同载荷不执行、异载荷拒绝）
- StateDelta 增量 + 快照回退：`_version` 单调递增，客户端版本落后过多时下发快照
- 视野裁剪：PlayerViewProjector 裁剪对手手牌/牌库数量

---

### 5. MatchStateDiffer + StateDelta — 状态增量

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/1_Domain/Match/MatchStateDiffer.cs` + `2_Network/StateDelta.cs` |
| 必考度 | ⭐⭐⭐⭐ |
| 建议阅读 | 20 分钟 |

**讲什么**：
- 路径化 diff：根字段 / 玩家字段 / 分区成员增删 / 卡牌字段逐项比较
- 分区按实例 Id 集合比较（牌库顺序不算变更）
- 跨区移动 = 旧区 Removed + 新区 Added
- 序列化器：MatchStateSerializer（JsonValue 纯 BCL，version 1）

---

### 6. PvpCommandSink — 客户端命令出口

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/5_Bootstrap/Battle/PvpCommandSink.cs` |
| 必考度 | ⭐⭐⭐⭐ |
| 建议阅读 | 15 分钟 |

**讲什么**：
- 目标预检（M14-T3-B4）：与 RuleEngine 同源 TargetRule 判据，无目标且需目标 → Invalid(TargetRequired) 不上行不耗 seq
- seq 递增：每次 Submit → _nextSeq++，封装 CommandEnvelope
- 在途登记：PendingActionFeedback 消费 PendingCleared 事件

---

### 7. BattleSceneBootstrap — 联机装配（partial 拆分）

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/5_Bootstrap/Battle/BattleSceneBootstrap.cs` + `.Input.cs` + `.Net.cs` + `.NetEnd.cs` + `.NetLaunch.cs` |
| 必考度 | ⭐⭐⭐⭐ |
| 建议阅读 | 20 分钟 |

**讲什么**：
- 五份 partial：主文件 + 输入 + 联机 + 联机终局 + 联机启动参数
- 联机 Update 流程：大厅阶段泵大厅 → MatchReady → AssembleNetBattle → 退出大厅分支 → 战斗阶段泵驱动
- 断线检测：LinkAlive=false → 60s 重连窗口 → 终态编排
- 启动参数一次性消费（M14-T6 修复）：_autoJumpConsumed 守卫

---

## 🟡 面试常问（大概率会问）

### 8. CardDatabase — 配置运行时查询

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/1_Domain/Config/CardDatabase.cs` |
| 必考度 | ⭐⭐⭐ |
| 建议阅读 | 10 分钟 |

**讲什么**：
- id/key O(1) 索引：`_byId = new Dictionary<string, CardDefinition>`
- 职业/稀有度/系列筛选：Linq Where
- 缺失给明确错误：throw ConfigLookupException（含 id/key）
- 配置管线闭环：CSV → 校验 → JSON → 加载 → 建库

---

### 9. NetLaunch — 启动参数解析

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Application/Launch/NetLaunch.cs` |
| 必考度 | ⭐⭐⭐ |
| 建议阅读 | 5 分钟 |

**讲什么**：
- 双开脚本接线：`-host [port]` → Host 自动创建对局，`-join host:port` → 自动连接
- 端口默认值：未指定时落到 PvpLobbyFlow.DefaultListenPort
- 显式 args vs 读命令行：测试注入 vs 生产读 Environment.GetCommandLineArgs
- M14-T6 消费问题：在 MainMenuBootstrap 加守卫而非改 NetLaunch（消费守卫放调用方）

---

### 10. BattleViewSynchronizer — 视图同步

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/5_Bootstrap/Battle/BattleViewSynchronizer.cs` |
| 必考度 | ⭐⭐⭐ |
| 建议阅读 | 15 分钟 |

**讲什么**：
- 状态驱动渲染：只 Push 状态，View 不读 MatchState
- 手牌/战场/英雄/法力分区刷新
- 对手牌背：EnemyHand 只给数量
- SwitchSeats()：热座/PVE 切换视角

---

### 11. BattleFeedbackPlayer — 事件驱动反馈

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/4_Presentation/Battle/Feedback/BattleFeedbackPlayer.cs` |
| 必考度 | ⭐⭐⭐ |
| 建议阅读 | 15 分钟 |

**讲什么**：
- MatchEventPump 订阅 → 12 类 GameEvent → 对应反馈
- DamageEvent → 浮动数字 + 伤害动画
- CardDeathEvent → 淡出效果
- MatchEndedEvent → 胜负横幅
- 拒绝命令零事件（设计决定）

---

### 12. GreedyAiAgent — AI 实现

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/2_Application/Match/Agents/GreedyAiAgent.cs` |
| 必考度 | ⭐⭐⭐ |
| 建议阅读 | 10 分钟 |

**讲什么**：
- 贪心排序：按效果量排序出牌（伤害牌优先打英雄，治疗牌优先奶低血随从）
- 三档难度：Easy（劣化排序）/ Normal（贪心）/ Hard（评估函数 + 斩杀意识）
- 分帧驱动：AiTurnRunner 在 Update 里一步一步跑，不阻塞主线程
- AiVsAiSimulator：批量 500 局门禁 + 胜率统计 + 难度单调性验证

---

## 🟢 面试加分（被深挖时展开）

### 13. GameLog — 日志系统

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/0_Core/GameLog.cs` + `LogChannel.cs` |
| 必考度 | ⭐⭐ |
| 建议阅读 | 5 分钟 |

**讲什么**：
- 静态门面 + 分等级（Trace/Info/Warn/Error）+ 分通道（Boot/Config/Match/Rule/Ai/Save/Ui/Perf）
- IsEnabled 供热路径短路
- sink 故障不穿透
- Unity 侧注入：BootLogging.ConfigureOnce() → UnityFileLogSink（persistentDataPath/Logs/）+ 控制台桥接

---

### 14. StateMachine<T> — 状态机

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/0_Core/StateMachine.cs` |
| 必考度 | ⭐⭐ |
| 建议阅读 | 5 分钟 |

**讲什么**：
- 显式注册状态与边，非法转移返回 Result.Failure
- 回调顺序固定 Exit(旧) → Enter(新)
- 未声明的自转移也被拒绝
- TurnStateMachine 封装：六阶段边 MatchStart → TurnStart → Draw → Main → TurnEnd → 回环 + 任意非终局阶段 → MatchEnd

---

### 15. JsonValue + JsonParser — 自定义 JSON

| 项 | 值 |
|----|----|
| 路径 | `Assets/_Project/0_Core/JsonValue.cs` + `JsonParser.cs` |
| 必考度 | ⭐⭐ |
| 建议阅读 | 10 分钟 |

**讲什么**：
- 确定性 JSON 序列化（字典按 key 排序）
- 严格解析：位置化错误、容 BOM、拒小数与重复键
- 为什么需要：Unity 2022.3 缺 System.Text.Json，服务器端不能依赖 Newtonsoft.Json

---

## 测试文件索引（面试展示测试质量时用）

| 模块 | 测试路径 | 说明 |
|------|---------|------|
| MatchController | `7_Tests/EditMode/Match/MatchControllerTests.cs` | 疲劳脚本 + 完整对局 |
| RuleEngine | `7_Tests/EditMode/Match/RuleEngine*.cs`（三文件） | 出牌/攻击/技能/结束回合 |
| EffectExecutor | `7_Tests/EditMode/Match/EffectExecutorTests.cs` + `TriggerDispatcherTests.cs` | 5 种效果 + 触发链 |
| MatchHost | `7_Tests/EditMode/Network/MatchHost*.cs` | 权威宿主/命令/状态/重连 |
| StateDelta | `7_Tests/EditMode/Match/StateChangeApplierTests.cs` + `MatchStateDifferTests.cs` | 增量编解码与应用 |
| NetLaunch | `7_Tests/EditMode/Application/NetLaunchTests.cs` | 启动参数解析 |
| 效果 DSL | `7_Tests/EditMode/Config/EffectParserTests.cs` | 解析器 15+ 例 |
| 配置管线 | `7_Tests/EditMode/Config/ConfigContentTests.cs` | 内容达标门禁 |
| BattleSceneBootstrap.NetEnd | `7_Tests/EditMode/Network/DescribeNetEndTests.cs` | 原因翻译 4 例 |

---

## 文档入口索引

| 想看什么 | 去哪 |
|----------|------|
| 架构设计 | `Docs/00-现状解构与架构再设计.md` |
| 联网方案（必读） | `Docs/05-联网对战_状态同步_设计文档.md` |
| 铁律与禁止清单 | `AGENTS.md` §2 + §4 |
| 开发规范 | `Docs/03-开发规范文档.md` |
| 任务进度 | `Docs/PROGRESS.md` |
| 交接材料 | `Docs/HANDOFF.md` |
| 面试深挖 | **本文件 + `Docs/INTERVIEW-GUIDE.md`** |
| 每个任务的做法与结论 | `Docs/tasks/`（一任务一卡） |
| 里程碑复盘 | `Docs/reviews/` |
| 双开验证指南 | `Docs/HANDOFF.md` §4.3 |
| 踩坑教训合集 | `Docs/INTERVIEW-GUIDE.md` §4 |
