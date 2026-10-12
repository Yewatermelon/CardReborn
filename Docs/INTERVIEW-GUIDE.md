# CardReborn 面试技术栈深挖指南

> **定位**：这份文档写给"想把 CardReborn 写进简历、应对面试官深挖"的开发者。
> 它不是项目文档的重复，而是**面试视角**——按面试官最可能追问的维度组织内容，
> 每个维度都给出"简历上怎么写 → 面试官会问什么 → 怎么答 → 证据在哪"。

---

## 0. 简历怎么写（建议模板）

> 以下是经过实盘验证的简历描述，面试官基本会按此追问。选 2–3 条展开，别全堆。

### 方案 A：突出架构分层（适合初中级 → 中高级）

> **CardReborn 回合制卡牌游戏 · 技术负责人**
>
> - 设计并实现**内核与 Unity 解耦的六层架构**（Core / Domain / Application / Network / Presentation / Bootstrap），规则三层纯 BCL 零 UnityEngine 依赖，服务器端直接复用同一份源码
> - 实现**权威宿主 + 状态同步**的本机双进程联网方案：BCL TCP 零第三方依赖、命令 seq 幂等、StateDelta 增量下行 + 快照回退重连、每座位视野裁剪
> - 搭建**双环境验证工具链**：无 Unity 的 .NET kernel 编译 + 覆盖率门禁（0_Core 95.82% / Domain+App 93.33%）+ Unity EditMode 1300 用例全绿
> - 设计**效果组件化 DSL**：37 张卡 → CSV 配置 → 解析器 → 执行器 → 触发器链（OnPlay/OnDeath/OnTurnStart 等），新增卡牌零代码

### 方案 B：突出联网技术（适合网络方向）

> **CardReborn 本地 PVP 验证 · 联网架构**
>
> - **权威宿主模式**：MatchHost（共享库）在房主客户端内跑唯一规则实例，客户端只上行 GameCommand、只接收状态与事件，铁律 12 条防线杜绝客户端作弊
> - **状态同步三件套**：StateDelta 增量下行（路径化 JSON diff）+ 客户端版本裁决 + 快照回退重连（断线 60s 内重连无缝续战）
> - **零依赖传输**：BCL System.Net.Sockets 长度前缀帧 + 心跳 + 重连窗口，不引入任何第三方网络库
> - **命令序号幂等**：信封 seq + 同序号同载荷不执行不推进 + 异载荷/低序号拒绝，防重放防乱序

### 方案 C：突出工程化（适合技术负责人/架构师方向）

> - **铁律 13 条**：从架构分层到命名空间遮蔽到禁止清单，每踩一坑就固化成自动检查（check.ps1 R1–R6 六条静态门禁）
> - **先测试后实现**：规则类 kernel 工程零 UnityEngine 依赖，纯 NUnit，覆盖率门禁 90%/80%
> - **可序列化 MatchState**：全量快照 version 1，枚举名/flags 整数，不冗余存 currentZone，为增量与重连打基础
> - **效果组件化**：拒绝 `class XxxCard : Card` 继承，改用效果组件组合（DamageEffect / HealEffect / SummonEffect / BuffEffect 等）

---

## 1. 架构分层（面试官必问 Top 1）

### 简历上怎么写

> 设计并实现内核与 Unity 解耦的六层架构，规则三层纯 BCL 零 UnityEngine 依赖

### 面试官会问什么

| # | 问题 |
|---|------|
| 1 | 为什么要分六层？每层职责边界是什么？ |
| 2 | 为什么 Core/Domain/Application 不能用 UnityEngine？ |
| 3 | Bootstrap 为什么是组合根？具体做了什么？ |
| 4 | asmdef 的 noEngineReferences 怎么保证不越界？ |
| 5 | 如果服务器端要复用规则，你怎么打包？ |

### 怎么答

**问题 1 · 六层职责**

```
0_Core   → 基础设施：Result/Guard/EventBus/StateMachine/IRandomProvider/IClock/GameLog/ObjectPool/ReactiveValue
            纯 BCL，无游戏领域概念
1_Domain → 领域模型：MatchState/PlayerState/CardInstance/Zone/KeywordSet/StatusSet + 配置契约 + GameEvent + GameCommand
            纯 BCL，描述"是什么"
2_Application → 用例逻辑：MatchController/RuleEngine/MatchFactory/Settlers/EffectExecutor/TriggerDispatcher/MatchStateSerializer/MatchStateDiffer
            纯 BCL，描述"怎么做"，协调 Domain 对象完成一局
2_Network → 联网：MatchHost/PeerHost/BclTcpTransport/LoopbackLink/CommandEnvelope/StateDelta/MatchReconnector
            纯 BCL，"怎么传"
4_Presentation → Unity 视图：CardView/HandView/BoardView/HeroView/BattleViewSynchronizer/PlayerInputController/TargetingController/BattleFeedbackPlayer
            允许 UnityEngine，只读，只渲染状态、只上行意图
5_Bootstrap → 组合根：MainMenuBootstrap/BattleSceneBootstrap/HotSeatHandler/BattleComposition/MatchRecordPersister + 工厂 + 启动参数接线
            允许 UnityEngine，唯一一处 new 具体实现，组装对象图
```

**问题 2 · 为什么规则三层不能用 UnityEngine**

> 两个理由：
> 1. **服务器端复用**：MatchHost 是共享库，将来要在服务器进程（Unity 工程外 .NET 进程）跑，那进程没有 UnityEngine
> 2. **测试纯净度**：kernel 工程 `Tools/Coverage/CardReborn.Kernel.Tests.csproj` 直接链接源码编译，不依赖 Unity，跑 NUnit 只需 `dotnet test`，比等 Unity 编译快 10–50 倍
> 环境依赖走抽象接口：IClock / IRandomProvider / ILogSink，Bootstrap 层注入 Unity 实现

**问题 3 · Bootstrap 做了什么**

> 唯一允许 `new` 具体实现的层。以 BattleSceneBootstrap.Start 为例：
> ```
> 读 StreamingAssets 配置 → CardDatabase 建库
> BattleUiFactory.Create() → 代码构建整个战斗 Canvas + 所有子视图
> BattleViewSynchronizer 绑定视图 → 只读状态刷新
> PlayerInputController + TargetingController 绑输入 → ICommandSink 是命令出口抽象
> HotSeatHandler 绑胜负面板 + 交棒屏
> 首帧 _synchronizer.Push(_controller.View) → 渲染开局状态
> ```
> 关键：没有任何视图/控制器自己 new 依赖，全部从 Bootstrap 注入

**问题 4 · asmdef 怎么保证不越界**

> 两层防线：
> 1. **编译期**：`0_Core/1_Domain/2_Application/2_Network` 的 asmdef 都设了 `noEngineReferences: true`，Unity 编译时直接不让它们引用 UnityEngine
> 2. **静态门禁**：`Tools/check.ps1` R1 扫描这四个目录的 `.cs` 文件，发现 `using UnityEngine` / `Debug.Log` / `Mathf` / `Time` / `UnityEngine.Random` 立即退出码 1
> 而且有 `-SelfTest` 探针机制——故意放违规文件，确认能抓到；移除后确认 PASS

**问题 5 · 服务器怎么复用**

> `Tools/Coverage/CardReborn.Kernel.Tests.csproj` 就是模板——用 `Compile Include` 把 `Assets/_Project/0_Core/1_Domain/2_Application/2_Network` 直接链接进 .NET 控制台工程，不用复制源码。服务器端的 `Card.Server`（规划中）照做即可。

### 证据在哪

| 证据 | 位置 |
|------|------|
| 架构设计文档 | `Docs/00-现状解构与架构再设计.md` |
| 铁律与禁止清单 | `AGENTS.md` §2 + §4 |
| check.ps1 R1–R6 | `Tools/check.ps1` |
| asmdef noEngineReferences | `Assets/_Project/0_Core/Card.Core.asmdef` 等四个文件 |
| kernel 工程 | `Tools/Coverage/CardReborn.Kernel.Tests.csproj` |

---

## 2. 权威宿主 + 状态同步（面试官必问 Top 2）

### 简历上怎么写

> 权威宿主模式：MatchHost 在房主客户端内跑唯一规则实例，客户端只上行 GameCommand；StateDelta 增量下行 + 快照回退重连 + seq 幂等 + 视野裁剪

### 面试官会问什么

| # | 问题 |
|---|------|
| 1 | 为什么选权威宿主而不是帧同步？ |
| 2 | 客户端作弊怎么防？具体哪几条防线？ |
| 3 | StateDelta 怎么算的？增量和快照怎么选？ |
| 4 | 命令 seq 幂等具体怎么实现？重放怎么办？ |
| 5 | 断线重连怎么搞的？窗口多久？ |
| 6 | 视野裁剪具体裁什么？为什么裁？ |
| 7 | 用 BCL TCP 不用 Unity Transport 或 Photon 怎么实现可靠传输？ |

### 怎么答

**问题 1 · 为什么权威宿主不帧同步**

> 三个原因：
> 1. **回合制不追求确定性**：炉石式卡牌游戏每步都是完整状态，权威宿主天然匹配这个模型；帧同步要求跨平台逐位一致（浮点、随机种子、排序），那是动作游戏的刚需，我们不需要
> 2. **客户端预测复杂度过高**：回合制里一张牌可能触发 64 层触发链（亡语套娃），客户端预测要复制整套 RuleEngine，代价巨大且容易和权威不一致；权威宿主只在权威侧跑一次，客户端只看结果
> 3. **架构更简单**：权威宿主模式下"服务器就是跑规则的那台机器"，不需要专门的服务器程序；本机双进程验证时房主进程就是权威，零服务器成本
> 阶段三可能做专用服务器/匹配/NAT 穿透，但本阶段只做本机验证，权威宿主够用

**问题 2 · 客户端作弊防线**

> 铁律 12 + 五道防线：
> 1. **客户端只上行 GameCommand，不下行 MatchState**：客户端发的是"我想出这张牌"，不是"我把这张牌打出去了"
> 2. **RuleEngine 权威校验**：每条命令在权威侧重新 Validate + Settle + Evaluate，不相信客户端上报的任何状态
> 3. **命令序号 + 幂等**：信封带 seq，同 seq 同载荷幂等重放不执行不推进版本，异载荷/低序号返回 StaleSequence
> 4. **目标预检**：PvpCommandSink 对需要目标的无目标命令直接返回 TargetRequired，不上行不耗 seq
> 5. **所有下发走 PlayerViewProjector 裁剪**：快照和增量都经裁剪，客户端看不到对手手牌数量以外的信息

**问题 3 · StateDelta 怎么算**

> `MatchStateDiffer`（1_Domain/Match/StateChange.cs + MatchStateDiffer.cs）：
> - 根字段（Phase/TurnNumber）→ 逐项比较
> - 玩家字段（Health/Armor/Mana/FatigueCounter）→ 逐项比较
> - 分区（Zone.Cards）→ 按实例 Id 集合比较（牌库顺序不算变更），跨区移动 = 旧区 Removed + 新区 Added
> - 卡牌字段（Health/Armor/Keywords/Statuses）→ 逐项 diff
> - 路径键与序列化器一致，顺序确定
> 快照在两种场景下发：客户端首次连接（需要完整基线）+ 重连（增量接不上时的兜底）；平时每 step 只发增量

**问题 4 · seq 幂等实现**

> 三个约束：
> 1. 序号单调：MatchHost 为每个座位维护 `IncomingSeqs`，每接受一条命令 +1
> 2. 同序号同载荷幂等：缓存最近 N 条命令回执，同 seq + 同 hash 的第二次到来直接返回缓存回执，不推进版本
> 3. 异载荷/低序号拒绝：同 seq 不同 hash → StaleSequence；seq 小于基线 → StaleSequence
> 被拒命令不消费 seq，回执不缓存
> 设计权衡：不做客户端预测（回合制代价太高），所以 seq 只用于防乱序防重放，不做乐观更新

**问题 5 · 断线重连**

> 三阶段：
> 1. **心跳超时 60 ticks**：PeerHost 每 10 ticks 发 Pong，60 ticks 没收到 LinkAlive=false，触发断线提示
> 2. **重连窗口 30 秒**：断线后 PeerHost 不立即判负，留 30 秒窗口让客户端重连
> 3. **重连流程**：客户端重建进程 → NetLaunch 读取启动参数 → 连接 PeerHost → MatchHost.Detach(seat) 清理旧会话 + **重置座位 seq 基线**（这是重连后命令序号过期的根因！HostBridge 之前把 KernelEndpoint 误命名导致 Detach 无效果，基线残留）→ Attach 新会话 → 下发当前快照
> 重连后客户端从 seq=1 重新计数，基线已归零，不会 StaleSequence

**问题 6 · 视野裁剪**

> `PlayerViewProjector`（2_Application/Match）：
> - 对手手牌：只给数量，不给卡牌 ID/Key
> - 对手牌库：只给数量，不给内容
> - 自己的四分区：完整信息
> - 英雄：完整信息（血量/护甲/技能可用）
> - 战场随从：双方都给完整信息（场上随从是公开的）
> 裁剪只影响快照和增量下发，权威侧全量保存

**问题 7 · BCL TCP 实现可靠传输**

> 三层封装：
> 1. **Transport**（2_Network/BclTcpTransport.cs）：Socket + 长度前缀帧（4 字节 header + body），处理连接/断开/发送/接收
> 2. **Protocol**（2_Network/LoopbackProtocol.cs 的网络等价版本）：上行 hello/command + 下行 snapshot/step/ack，定义消息类型
> 3. **Host/Join**（PeerHost + JoinClient）：握手 → 交换座位 → 进入大厅 → Ready 后 MatchReady → 启动 MatchHost
> 心跳：PeerSession 每 10 ticks 发 Pong，60 ticks 超时 → LinkAlive=false → 触发断线提示
> 可靠传输靠 TCP 自带保证（可靠有序），不需要自己实现重传；应用层只需要加心跳和超时判定

### 证据在哪

| 证据 | 位置 |
|------|------|
| 联网设计文档 | `Docs/05-联网对战_状态同步_设计文档.md` |
| MatchHost + PeerHost | `Assets/_Project/2_Network/` |
| MatchStateDiffer | `Assets/_Project/1_Domain/Match/MatchStateDiffer.cs` |
| StateDelta | `Assets/_Project/2_Network/StateDelta.cs` |
| 重连流程 | `Assets/_Project/2_Network/MatchReconnector.cs` |
| 双开脚本 | `Tools/dual-launch.ps1` |

---

## 3. 效果组件化 DSL（面试官必问 Top 3）

### 简历上怎么写

> 效果组件化设计：拒绝继承表达卡牌种类/效果，改用 DamageEffect/HealEffect/SummonEffect/BuffEffect 等 5 种基础组件 + TriggerDispatcher 触发链，新增卡牌只需填 CSV 零代码

### 面试官会问什么

| # | 问题 |
|---|------|
| 1 | 为什么拒绝 `class XxxCard : Card` 继承？ |
| 2 | 效果 DSL 格式是什么？解析器怎么实现？ |
| 3 | 触发链怎么防循环？深度限制多少？ |
| 4 | 怎么加新效果类型？ |
| 5 | 配置管线：改 CSV → 导入 → 游戏内生效，中间经过哪些步骤？ |

### 怎么答

**问题 1 · 为什么拒绝继承**

> 三个原因：
> 1. **组合比继承灵活**：炉石一张卡可能有战吼+亡语+光环+条件触发，继承层级会变成 `DamageCard : Card`、`HealCard : Card`、`DamageHealCard : Card`、`DamageHealSummonCard : Card`……指数爆炸；组合只需要 `DamageEffect + HealEffect + SummonEffect` 三个组件
> 2. **数据驱动**：卡牌效果从 CSV 读取，新增卡不改代码；如果用继承，每张新效果都要新类
> 3. **铁律 1 条**：AGENTS.md 明确禁止"继承表达卡牌种类/效果"，用组件组合

**问题 2 · 效果 DSL 格式**

> 格式：`效果名:参数1/参数2/...`，多效果用 `|` 分隔
> 示例：
> ```
> DamageEffect:5           # 对目标造成 5 点伤害
> HealEffect:3             # 治疗 3 点
> DrawCardEffect:2         # 抽 2 张牌
> SummonEffect:MINION_001/3/3  # 召唤 MINION_001，3攻3血
> BuffEffect:Attack:+2/Health:+2  # +2/+2
> OnDeath:DamageEffect:2   # 亡语：对所有角色造成 2 点伤害
> OnPlay:HealEffect:3      # 战吼：治疗 3 点
> ```
> 解析器（EffectParser）：按 `|` 拆效果 → 按 `/` 拆参数 → 按 `:` 拆类型和值 → 构造 TriggeredEffect
> 无前缀默认 OnPlay（向后兼容）

**问题 3 · 触发链防循环**

> TriggerDispatcher（2_Application/Match/TriggerDispatcher.cs）：
> - 深度限制 64 层，超限抛 InvalidOperationException（含"深度"字样）
> - 测试锁定：亡语链式不死循环、OnSummon 自召唤深度超限
> - 设计决策：不用"已触发集合"防循环（那会漏正常的双向触发），用深度硬上限 + 测试覆盖边界

**问题 4 · 加新效果类型**

> 四步：
> 1. Domain：在 `GameEffects.cs` 加新的 `IEffectData` 派生类 + `EffectParser` 加解析分支
> 2. Application：在 `Effects.cs` 加新的 `IEffectExecutor` 派生类 + 类型字典注册
> 3. Domain Config：`Cards.csv` 的 `Effects` 列可用新格式
> 4. 测试：先红后绿，覆盖解析 + 执行 + 触发

**问题 5 · 配置管线**

```
Config/Excel/Cards.csv（手填）
  → ConfigImporter（Card.Editor）校验 + 写 JSON
  → Assets/_Project/Config/cards.json（生成物，勿手改）
  → ConfigFileLoader（Card.Infrastructure）读 JSON
  → CardDatabase（Card.Domain.Config）建库，id/key O(1) 索引
  → 游戏内 BattleComposition.LoadDatabase() 消费
```
中间任何一步校验失败，零写出，既有生成物不动。

### 证据在哪

| 证据 | 位置 |
|------|------|
| EffectParser + GameEffects | `Assets/_Project/1_Domain/Match/GameEffects.cs` |
| EffectExecutor + TriggerDispatcher | `Assets/_Project/2_Application/Match/Effects.cs` + `TriggerDispatcher.cs` |
| 配置管线 | `Assets/_Project/1_Domain/Config/` + `Assets/_Project/6_Editor/ConfigImportMenu.cs` |
| Cards.csv | `Config/Excel/Cards.csv` |
| 铁律 1 | `AGENTS.md` §2 第 1 条 |

---

## 4. 踩坑教训（面试官会问"你做这个项目遇到的最大挑战"）

> 这是简历加分项——能讲具体的坑和解决方案，比"我做了 XXX"更有说服力。

### 坑 1：MatchState 可序列化设计反复改了 3 版

> **现象**：一开始 MatchState 直接 `JsonUtility.ToJson`，然后加了 `System.Text.Json` 发现 Unity 2022.3 没有，又写了 `Card.Core.JsonValue` 自定义序列化器。
> **根因**：没有从一开始就规划"服务器端复用"这个约束，序列化方案选了又换。
> **教训**：涉及跨运行时的能力，先想清楚两端的约束再选技术方案。JsonValue 纯 BCL 实现，虽然比 System.Text.Json 简陋，但服务器端直接能用，零依赖。

### 坑 2：重连后命令序号过期

> **现象**：重连客户端后所有命令被 StaleSequence 拒绝，表现为"技能一直显示已释放"。
> **根因**：HostBridge 把 KernelEndpoint 误命名——它注释写着"权威侧端点"但实际是桥侧端点，PeerHost 接管时用这个错误对象 Detach，MatchHost._seats 以引用为键，Detach 成无操作 → 座位 seq 基线残留 → 新进程 seq 从 1 开始 → 和残留基线比较全过期。
> **教训**：命名误导 = 生产事故。把 HostBridge 的 KernelEndpoint 改成 PeerEndpoint，PeerHost 自持 _peerKernelEndpoint 作为 Detach 依据。

### 坑 3：双开脚本"返回主菜单"又跳 Battle

> **现象**：用 dual-launch.ps1 启动，打完一局点"返回主菜单"，又跳到 Battle 场景。
> **根因**：命令行参数 `-host 7777` 永不消失。NetLaunch.Resolve() 每次读 `Environment.GetCommandLineArgs()` 都返回 Host → MainMenuBootstrap.Start 每次被实例化都自动跳 Battle → 死循环。
> **教训**：进程启动参数是一次性资源，消费后必须标记。在 MainMenuBootstrap 加 `_autoJumpConsumed` 静态守卫，自动跳转只做一次。

### 坑 4：兄弟命名空间不会自动 using

> **现象**：`Card.Bootstrap.Battle` 里引用 `Card.Bootstrap.Menu.MainMenuBootstrap` 报 CS0103。
> **根因**：C# 命名空间是分治的——外层有 `Card.Bootstrap` 不代表自动引入它的子分支。Python 的 `from card import bootstrap` 不会自动让 `bootstrap.menu` 可用，C# 同理。
> **教训**：这是 C# 设计的经典陷阱，别赌"编译器能推断"。每次跨兄弟命名空间引用，显式加 `using`。已在 Docs/03 U-18 固化。

### 坑 5：手工 .meta GUID 31 位导致 Unity 静默排除

> **现象**：新加的 .cs 文件在 Unity 里编译不到，下游 CS0246 类型不存在。
> **根因**：手工写 .meta 时 GUID 只写了 31 位，Unity 静默拒绝导入该资产，程序集缺类型。dotnet kernel 工程通配符编译不暴露这个问题，因为它直接链接源码文件，不经过 Unity 导入。
> **教训**：.meta GUID 必须恰为 32 位十六进制且全仓唯一。生成后用 PowerShell 扫描校验。

### 坑 6：CanvasGroup.alpha 不影响 raycast

> **现象**：池化 CardView 归还时设 CanvasGroup.alpha=0，以为点击不到，实际还能点。
> **根因**：Unity 的 CanvasGroup.alpha 只影响渲染，不影响 raycast——raycast 由 CanvasGroup.blocksRaycasts 控制，不是 alpha。
> **教训**：池化视图复位必须同时复位 alpha、blocksRaycasts、highlight、fadeOut 等所有可视副作用。

---

## 5. 面试高频追问速查表

| 面试官问 | 一句话答案 | 展开 |
|----------|-----------|------|
| 为什么不用帧同步？ | 回合制不需要确定性，权威宿主更简单 | 见 §2 问题 1 |
| 客户端作弊怎么防？ | 铁律 12 + 五道防线 | 见 §2 问题 2 |
| 服务器端规则怎么复用？ | Compile Include 链接源码，零拷贝 | 见 §1 问题 5 |
| 为什么不用继承表达卡牌效果？ | 组合灵活 + 数据驱动 + 防指数爆炸 | 见 §3 问题 1 |
| 怎么加新效果类型？ | 四步：Domain + Application + CSV + 测试 | 见 §3 问题 4 |
| 状态增量怎么算？ | 路径化 JSON diff，顺序确定 | 见 §2 问题 3 |
| 重连怎么搞？ | Detach(seat) 清理旧会话 + Reset 基线 + 下发快照 | 见 §2 问题 5 |
| 视野裁剪裁什么？ | 对手手牌/牌库数量化，其余公开 | 见 §2 问题 6 |
| 为什么不用 Unity Transport / Photon？ | 阶段二范围锁：只做本机双开验证，BCL 够用 | AGENTS §2 第 13 条 |
| 测试覆盖率怎么搞？ | dotnet coverlet + Unity Test Runner 双门禁 | HANDOFF §4 |
| 规则三层怎么保证不用 UnityEngine？ | asmdef noEngineReferences + check.ps1 R1 扫描 | 见 §1 问题 4 |
| 配置改了怎么生效？ | 改 CSV → 导入器校验写 JSON → 游戏内热加载 | 见 §3 问题 5 |
| MatchState 怎么序列化？ | 自定义 JsonValue 纯 BCL，不依赖 System.Text.Json | Docs/03 §5.9.4 |
| 触发链怎么防死循环？ | 深度硬上限 64 + 测试覆盖边界 | 见 §3 问题 3 |
| 命令序号怎么实现幂等？ | 信封 seq + 同 seq 同载荷不执行 + 低序号拒绝 | 见 §2 问题 4 |

---

## 6. 项目规模数字（面试时报这些显得专业）

| 指标 | 数字 |
|------|------|
| 里程碑 | 14 个（M0–M7 阶段一 + M11–M14 阶段二） |
| 任务 | ~80 个 |
| kernel 测试 | 1007 passed / 0 failed |
| Unity EditMode 测试 | 1300 passed / 0 failed |
| 0_Core 覆盖率 | 95.82%（门禁 90%） |
| Domain + App 覆盖率 | 93.33%（门禁 80%） |
| check.ps1 扫描文件 | ~500 |
| 卡牌配置 | 37 行（35 启用 + 2 废弃） |
| 效果组件 | 5 种基础 + 触发链 |
| 铁律 | 13 条 |
| 静态门禁 | 6 条规则 + SelfTest 探针 |
| 踩坑记录 | 6 大类（本指南 §4） |
| Git 提交 | ~100 次（含 feat/fix/docs/test/chore） |
