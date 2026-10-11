# 任务卡 · M14-T3 PVP 场景装配（联机流程 UI）

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M14-T3 |
| 所属里程碑 | M14 双人验证与体验打磨 |
| 上游需求 | Docs/02 M14 任务表第 3 行；Docs/05 §3.4 客户端类清单、§9.1/§9.2 协议与生命周期、§15.3 本阶段验证路径 |
| 规则依据 | 铁律 12（`Card.Presentation` 不引用 `Card.Network`；客户端只上行命令、只收状态）；Docs/05 §9.2 生命周期：连接 → 握手 → 准备 → MatchStart → 对战 → 结算 |
| 预估 | 1 会话 |
| 依赖任务 | M14-T1（表现先行）、M14-T2（状态插值）、M12/M13 网络全链路 |
| 移交处置 | M14-T1-L1（Battle 场景 PVP 装配：PvpCommandSink、协调器接线、输入锁定）；M14-T2-L1（pending 视觉与布局插值联动策略） |

## 2. 目标（一句话）

> 主菜单新增联机入口；Battle 场景内置联机大厅（角色选择 / 地址输入 / 准备），开局后双方战斗界面统一走"权威快照+增量下行 → 视图同步 → 命令上行（seq + 在途登记）"网络管线，本机双进程可完整打完一局并正确结算与再来一局，异常有提示，界面标注"房主托管 · 仅用于本机验证"。

## 3. 范围（做什么）

- **A · `Card.Network`：`NetworkMatchClient`**（加入方客户端会话，Docs/05 §3.4 补齐）：
  - 生命周期：`Connect`（外部建传输）→ `SendHello` → HelloAck → `SendReady` → MatchStart → 下行分发 → `Dispose`。
  - 下行事件：`HelloAckReceived` / `MatchStartReceived` / `SnapshotReceived` / `StepReceived` / `MatchEnded(winner, reason)` / `ErrorReceived(code)` / `Disconnected(reason)`。
  - 上行：`SendCommand(envelopeJson)` 封 Command 帧；`Ping` 自动回 `Pong`（回显时间戳）。
  - 防御姿态与 PeerSession 对称：畸形帧 / 载荷非法 JSON → 断链 + Disconnected；未知帧类型忽略记日志；版本门（握手帧版本不匹配 → ErrorReceived + 断链）。
- **B · `Card.Bootstrap.Battle.Net`（装配层，纯 C# 无 UnityEngine 依赖）**：
  - `NetMatchLink`：座位传输适配接口 + 两实现——`HostNetLink`（包装 `MatchHost` + `PeerHost`：`RunNetworkTick` + 本地座位 0 回环端点排空）、`JoinNetLink`（包装 `NetworkMatchClient`）。
  - `NetMatchDriver`：持有 `ClientStateSync`，消费链路下行（ApplySnapshot / ApplyStep），解析 step 事件（`EventSerializer.ReadEvent`）为追加式事件列表；跟踪 `HasSnapshot` / `IsFinished`（step.finished）与 MatchEnd 原因；快照重置时清 pending。
  - `PvpCommandSink`：`ICommandSink` 生产适配——命令序列化（`CommandSerializer.Serialize`）+ 每座位自增 seq 信封 + `PendingActionFeedback.NotifySubmitted` 登记 + 链路上行；本地一律返回 `CommandResult.Valid()`（无本地校验，拒绝走权威回执）。
  - `PvpLobbyFlow`：大厅状态机（房主：创建对局 → 监听 → 准备 → 等对手；加入方：连接 → 准备 → 等开局），成功回调交付 `NetMatchDriver`；失败（端口占用 / 连接失败 / 版本不匹配 / 断链）走错误回调。
  - `PvpLobbyUi` + `PvpLobbyUiFactory`：大厅 overlay 运行时构建——标题、副标签"房主托管 · 仅用于本机验证"、房主/加入按钮、地址输入框（记忆上次地址）、准备按钮、状态文本、返回按钮。
- **C · `BattleSceneBootstrap.Net.cs` partial（联机模式装配）**：
  - `GameMode == PVPNet`：建战斗 UI + 大厅 overlay；`Update` 泵 `NetMatchDriver`，开局（链路 MatchStarted + 快照基线就绪）后装配网络战斗管线——`BattleViewSynchronizer.Push(driver.State)`、事件泵（驱动既有反馈/日志管线）、`PvpCommandSink` 接 `PlayerInputController` + `TargetingController`、`PendingActionFeedback` 接 locator + 文字池（**M14-T1-L1**）。
  - 结算：step `finished` → 胜负面板（胜者 + 原因）；MatchEnd 帧异常原因（HostQuit / Disconnect）→ 显示可读文本；"再来一局" → 重载 Battle 场景回到联机大厅。
- **D · 主菜单**：新增"联机对战（本机验证）"按钮，设 `GameMode = PVPNet`。

## 4. 明确不做（防止范围蔓延）

- 不做重连 UI 与恢复流程编排（客户端 `ReconnectRequest` 的发送 UX 属 M14-T6；M13-T7 已有协议层与快照回退）。
- 不做 `RoomState` 消息与准备状态互见（协议未实现；开局时序沿用 M12 语义——对手 Ready 即 MatchStart，房主"准备"为流程引导，见设计决策 ③）。
- 不做客户端预测与本地规则校验（Docs/05 §6.1 基础版：本地提交一律视为已上行，拒绝由权威 step 回执驱动回退提示）。
- 不做客户端侧心跳超时判定与断线自动重试（表现打磨属 M14-T6；传输层断开经 Disconnected 事件提示）。
- 不修改 `PeerSession` / `PeerHost` / `HostBridge` 协议行为（M12/M13 已交付语义保持不变）。
- 不做 T4 双开启动脚本与构建文档、T5 对局记录、T6 异常体验打磨。
- 不做观战 / 换边 / 卡组选择（阶段三）。

## 5. 接口约定

```csharp
// Card.Network —— 加入方客户端会话（Docs/05 §3.4 NetworkMatchClient）
public sealed class NetworkMatchClient : IDisposable
{
    public bool Connected { get; }              // 传输层是否仍连接
    public bool Handshook { get; }              // 已收 HelloAck
    public bool MatchStarted { get; }           // 已收 MatchStart
    public event Action<JsonValue>? HelloAckReceived;
    public event Action<JsonValue>? MatchStartReceived;
    public event Action<JsonValue>? SnapshotReceived;   // { kind:"snapshot", version, state }
    public event Action<JsonValue>? StepReceived;       // { kind:"step", ... }
    public event Action<int?, string>? MatchEnded;      // winner(null=作废), reason
    public event Action<string>? ErrorReceived;         // 错误码
    public event Action<string>? Disconnected;          // 断链原因
    public NetworkMatchClient(ITransport transport, NetworkDiagnostics? diag = null);
    public void Pump();
    public void SendHello(string playerName);
    public void SendReady();
    public void SendCommand(string envelopeJson);       // 完整信封 {kind,payload,seq}
    public void Dispose();
}

// Card.Bootstrap.Battle.Net
internal interface INetMatchLink : IDisposable
{
    bool Alive { get; }
    bool MatchStarted { get; }                  // 房主=对手 Ready；加入方=收 MatchStart
    void Pump(Action<JsonValue> onDownlink);    // 排空下行（snapshot/step）
    void SendUplink(string envelopeJson);       // 上行命令信封
}

internal sealed class HostNetLink : INetMatchLink   // MatchHost + PeerHost + 座位 0 回环
internal sealed class JoinNetLink : INetMatchLink   // BclTcpTransport + NetworkMatchClient

internal sealed class NetMatchDriver
{
    public NetMatchDriver(INetMatchLink link, int localSeat);
    public ClientStateSync Sync { get; }
    public bool HasSnapshot { get; }
    public bool IsFinished { get; }             // step.finished
    public string? EndReason { get; }           // MatchEnd 帧原因（异常终态）
    public IReadOnlyList<GameEvent> Events { get; }     // 追加式（供事件泵消费）
    public event Action<StepOutcome>? StepApplied;      // 驱动 PendingActionFeedback
    public void Start();                        // 房主：发本地 hello 取开局快照
    public void Pump();                         // 链路泵 + 快照/增量应用
    public void ClearPending();                 // 快照重置后清在途
    public void Dispose();
}

internal sealed class PvpCommandSink : ICommandSink
{
    public PvpCommandSink(NetMatchDriver driver, PendingActionFeedback feedback);
    public CommandResult Submit(IGameCommand command);  // 序列化+seq++（自 1）+登记+上行
}

internal sealed class PvpLobbyFlow
{
    public PvpLobbyFlow(CardDatabase database, int listenPort /*房主*/);
    public bool IsHosting { get; }
    public string? ListenAddress { get; }               // "127.0.0.1:port"
    public NetMatchDriver? Driver { get; }
    public bool MatchReady { get; }                     // MatchStarted && HasSnapshot
    public event Action<string>? StatusChanged;         // 状态/错误文本（错误以 "!" 前缀）
    public void CreateAsHost();                         // 建局+监听；失败 → 错误回调
    public void ConnectAsJoiner(string address);        // "host:port"；失败 → 错误回调
    public void MarkHostReady();                        // SetHostReady（流程引导）
    public void SendJoinerReady();                      // SendReady
    public void Pump();                                 // 驱动链路（大厅阶段）
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 客户端 SendHello → 宿主 Pump | 收 HelloAck；`Handshook=true`、事件携带载荷 |
| AC-2 | 握手后 SendReady → 宿主 Pump | 收 MatchStart；`MatchStarted=true` |
| AC-3 | 宿主下发 MatchSnapshot / StateDelta | 客户端对应事件触发，载荷 JSON 与下行一致 |
| AC-4 | 客户端 SendCommand（信封 JSON） | 宿主收到 Command 帧，信封原文一致（kind/payload/seq） |
| AC-5 | 宿主发 Ping | 客户端自动回 Pong（时间戳原样回显） |
| AC-6 | 畸形帧 / 版本不匹配 / 传输关闭 | Disconnected 触发（版本不匹配先 ErrorReceived）；后续 Pump 不再产出 |
| AC-7 | **端到端（真实 TCP 回环）**：PeerHost 房主 + JoinNetLink 加入方，双 `NetMatchDriver` | 连接→握手→准备→双方各自收到**按座位裁剪**的开局快照；双方交替 Submit 命令→双方本地版本与状态一致推进；一方 finished → `IsFinished` |
| AC-8 | PvpCommandSink.Submit | seq 自 1 自增、信封 JSON 正确、`NotifySubmitted` 登记在途（PlayCard/Attack 提取卡实例 Id）、返回 Valid |
| AC-9 | NetMatchDriver 快照基线 → step | ApplyStep 裁决透传 `StepApplied`；VersionGap 不动基线；MatchEnd reason 记录 |
| AC-10 | 大厅：端口占用 / 地址非法 / 连接拒绝 | `StatusChanged` 收到错误文本；`Driver` 为 null |
| AC-11 | 大厅：房主创建 → 准备；加入方连接 → 准备 | `MatchReady=true` 且双方 Driver 快照基线就绪（经 AC-7 端到端背书） |
| AC-12 | 联机战斗装配 | Driver 驱动 `BattleViewSynchronizer.Push`（己方手牌卡面 / 对方牌背数量）；PendingActionFeedback locator 命中本地手牌/战场视图 |
| AC-13 | 终局 | step finished → 胜负面板显示胜者与原因；MatchEnd HostQuit/Disconnect → 显示可读原因文本 |
| AC-14 | 界面标注 | 大厅面板含"房主托管 · 仅用于本机验证"文案 |
| AC-15 | 主菜单联机入口 | 点击后 `GameMode=PVPNet` 并载入 Battle；既有 PVE / 热座入口零破坏 |

## 7. 测试要求

- 测试类型：EditMode。
  - **kernel 编译集**（`EditMode/Network/`）：`NetworkMatchClientTests` ≥ 10——握手 / 版本不匹配 / Ready-MatchStart / 快照与增量分发 / 命令上行 / Ping-Pong / 畸形帧 / 未知帧忽略 / 断链事件 / Dispose 后不泵。
  - **Unity-only**（不进 kernel，`EditMode/Bootstrap/` + `EditMode/Presentation/`）：
    - `PvpNetFlowTests` ≥ 6：**真实 TCP 回环端到端**（PeerHost + JoinNetLink 双驱动，覆盖 AC-7 / AC-9 主路径）。
    - `PvpCommandSinkTests` ≥ 4：seq 自增 / 信封 JSON / 在途登记 / 卡实例 Id 提取。
    - `PvpLobbyFlowTests` ≥ 6：fake link 驱动状态机（创建失败 / 连接失败 / 成功路径 / 准备引导 / MatchReady 判定）。
    - 装配与 UI：主菜单入口 GameMode 设值、大厅 UI 工厂构建与标注文案（≥ 3）。
- 红绿顺序：先写上述测试（编译红），再实现；kernel 新测试本地 dotnet 可验证红→绿。
- Unity-only 测试遵守 Docs/03 §6：易冲突标识符（`Object` 等）完全限定；`Card.Application` 遮蔽 `UnityEngine.Application`。

## 8. 涉及文档与配置

- 需更新的文档：Docs/PROGRESS.md（任务行）；本任务卡结论。
- 需更新的配置表：无。
- 是否影响既有模块：`MainMenuBootstrap`（+1 按钮）；`BattleSceneBootstrap`（+partial，PVE/热座路径零改动）；`BattleUiFactory` 不改（大厅 UI 独立工厂）。

## 9. 完成定义（DoD 勾选）

- [ ] 满足全部 AC 且附证据
- [ ] 测试通过且覆盖边界
- [ ] 编译 0 error / 0 warning
- [ ] 通过 `Docs/03` 铁律与禁止清单自查（重点：铁律 12——`Card.Presentation` 零 `Card.Network` 引用）
- [ ] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [ ] 涉及文档已同步更新

## 10. 设计决策（装配期裁定，移交项处置）

1. **输入锁定策略（M14-T1-L1）**：pending 期间**不禁点**——M13-T3 序号收件箱天然支持多命令在途，权威按序裁决；在途命令以待确认视觉提示，拒绝经回执回退 + 浮字提示（M14-T1 既有路径）。
2. **pending 视觉 × 布局插值联动（M14-T2-L1）**：**不干预**——pending 视觉不改变布局插值行为；权威接受后状态同步驱动落位（Confirm 复位）；卡被权威移除时视图回收，`CardView.SetData` 复位路径已清 pending 视觉（代码既有，`CardView.cs` SetData→CardPendingView 复位）。登记为设计决策，无需新增代码。
3. **开局时序**：沿用 M12 语义（对手 Ready 即 MatchStart；`PeerSession.HandleReady` 不门控房主 Ready）。房主"准备"按钮为流程引导（`SetHostReady` + 状态文本）。残留竞争窗口（先手为加入方且极快出牌）由 75s 回合超时兜底 + 本机并排验证语境可接受——登记观察项 M14-T3-O1。
4. **监听端口**：房主固定默认 7777（`PvpLobbyFlow` 常量），保证"再来一局"场景重载后地址可复现；占用即错误提示。接入 Docs/05"房主显示本机地址"口径。
5. **终态判定**：本地座位以 step `finished` 字段为主（`SubmitAuthority` 代发的判负/超时也走 step 广播）；MatchEnd 帧作异常原因补充（HostQuit / Disconnect 可读文本）。对局作废（winner=null）显示"对局作废"。
6. **`NetworkMatchClient` 不持视图状态**：帧分类与事件分发归 `Card.Network`，快照/增量应用归 `ClientStateSync`（Application），组合归 Bootstrap——铁律 12 分层落位。

## 11. 结论（2026-10-12 交付）

### 11.1 交付物

- **A · `Card.Network`**：[NetworkMatchClient.cs](Assets/_Project/2_Network/NetworkMatchClient.cs)（加入方客户端会话，215 行）：生命周期 Connected/Handshook/MatchStarted + 事件 HelloAck/MatchStart/Snapshot/Step/MatchEnded/Error/Disconnected；Ping 自动回 Pong；防御对称 PeerSession——畸形帧断链、未知帧忽略、版本门丢弃、JSON 非法断链。
- **B · `Card.Bootstrap.Battle.Net`**（装配层，纯 C# 无 UnityEngine）：
  - [NetMatchLink.cs](Assets/_Project/5_Bootstrap/Battle/Net/NetMatchLink.cs)（138 行）：`INetMatchLink` + `HostNetLink`（TryAcceptPeer 非阻塞接管 + RunNetworkTick + 座位 0 回环排空）+ `JoinNetLink`（NetworkMatchClient 包装）；两实现统一 `SendReady`（房主=SetHostReady / 加入方=SendReady）。
  - [NetMatchDriver.cs](Assets/_Project/5_Bootstrap/Battle/Net/NetMatchDriver.cs)（156 行）：ClientStateSync 快照基线 + 增量应用，step 事件解析为追加式 `Events`（供 MatchEventPump），终态跟踪（step.finished + MatchEnd 帧），VersionGap 自动上行 SnapshotRequest，ApplySnapshot 触发 `PendingCleared`。
  - [PvpCommandSink.cs](Assets/_Project/5_Bootstrap/Battle/Net/PvpCommandSink.cs)（46 行）：命令 → CommandSerializer + 自增 seq 信封 → PendingActionFeedback 登记 → 链路上行；本地一律 Valid（M14-T1-L1：输入不禁点，拒绝走权威回执）。
  - [PvpLobbyFlow.cs](Assets/_Project/5_Bootstrap/Battle/Net/PvpLobbyFlow.cs)（183 行）：大厅状态机——CreateAsHost（端口占用→"!"错误）/ ConnectAsJoiner（地址解析+TCP 连接）/ Ready 引导 / MatchReady=MatchStarted+HasSnapshot / 断链报告；internal 工厂注入供 fake 测试。
  - [PvpLobbyUi.cs](Assets/_Project/5_Bootstrap/Battle/Net/PvpLobbyUi.cs)（138 行）：overlay 工厂——标题、副标签"房主托管 · 仅用于本机验证"、房主/加入/准备/返回按钮、地址输入（PlayerPrefs 记忆）、状态文本（"!"前缀红色）。
  - [UnityTickClock.cs](Assets/_Project/5_Bootstrap/UnityTickClock.cs)（14 行）：Unity 侧 IClock（unscaledTime×10 = 权威 10 Hz tick）。
- **C · 装配**：[BattleSceneBootstrap.Net.cs](Assets/_Project/5_Bootstrap/Battle/BattleSceneBootstrap.Net.cs)（244 行，partial）——GameMode=PVPNet：大厅 overlay → MatchReady 后 AssembleNetBattle（BattleViewSynchronizer 按 localSeat 投影 + 事件泵 + BindNetFeedback（PendingActionFeedback×locator×文字池，M14-T1-L1）+ BindNetInput（PvpCommandSink））→ UpdateNet 泵驱动 → ShowNetEnd（胜者+原因，HostQuit/Disconnect 可读文本，作废提示）；[BattleSceneBootstrap.cs](Assets/_Project/5_Bootstrap/Battle/BattleSceneBootstrap.cs) Start/Update 分支 + BindFeedback 参数化 localSeat（PVE/热座行为不变）；[BattleSceneBootstrap.Input.cs](Assets/_Project/5_Bootstrap/Battle/BattleSceneBootstrap.Input.cs) OnEndTurnClicked 联机分支（无 controller 读 Sync.State.ActivePlayerId）。
- **D · 主菜单**：[MainMenuBootstrap.cs](Assets/_Project/5_Bootstrap/Menu/MainMenuBootstrap.cs) +1 按钮"联机对战（本机验证）"→ GameMode=PVPNet（AC-15）。
- **网络层配套**：[PeerHost.cs](Assets/_Project/2_Network/PeerHost.cs) 新增非阻塞 `TryAcceptPeer`（AcceptPeer 阻塞语义保留，M12/M13 测试零破坏）+ [BclTcpTransport.cs](Assets/_Project/2_Network/BclTcpTransport.cs) `BclTcpHostListener.Pending`。
- **表现层微增量**：[PendingActionFeedback.cs](Assets/_Project/4_Presentation/Battle/Feedback/PendingActionFeedback.cs) `ClearPending()`（快照重置恢复清在途视觉，Confirm 复位落位以快照为准）；PendingCommandTracker 既有 `Clear`（M14-T1 已有，本次接线消费）。

### 11.2 验证证据

- 先红：NetworkMatchClientTests CS0246 → 后绿 kernel **982 passed / 0 failed**（基线 967 + 15）；覆盖率 `0_Core 91.96% / Domain + App 93.23%` 达标，coverage.ps1 PASS。（勘误：本节原记"985"为笔误，967+15=982，2026-10-12 修复轮实测校准）
- 静态门禁：check.ps1 PASS（391 文件，0 违规）；全仓 **503** meta GUID 全唯一、恰 32 位十六进制。
- 本地预编译拦截（新增手段：临时 csproj 引引擎 dll 全量编译 0_Core→5_Bootstrap + 全部 EditMode 测试，0 警告 0 错误）：**首轮拦下 6 个真问题**——tracker.Clear 重复定义（CS0111）、NetMatchLink/PvpLobbyFlow/NetMatchDriver/partial 缺 using、PvpLobbyUi 事件外部不可 Invoke（event→Action 属性）——Presentation/Bootstrap 测试"本地拦不住"缺陷的工程化闭环。
- 测试矩阵：kernel NetworkMatchClientTests 15（握手/版本门/Ready/快照增量分发/命令信封/Ping-Pong/畸形帧/未知帧/断链/Dispose）；Unity-only PvpNetFlowTests 6（**真实 TCP 回环端到端**：双向座位快照+版本一致 / Ready-M12 语义 / 命令交替推进 / 拒绝不推版本 / Forfeit 终局 / sink 在途裁决）+ PvpLobbyFlowTests 8（fake 链路状态机）+ PvpCommandSinkTests 6（seq/信封/登记/卡视觉）+ PvpLobbyUiTests 5（标注文案/阶段/状态色/地址/事件路由）。
- **待用户实跑**：Unity EditMode Test Runner 预期 **1270 passed / 0 failed**（基线 1230 + kernel 15 + Unity-only 25）。
- Unity 侧编译警告修复（2026-10-12）：`BindNetFeedback`/`BindNetInput` 补 `_ui` 非空 guard（CS8602×2，跨方法调用后字段可空收窄失效）+ `HostNetLink.MatchEnded` 占位事件 pragma 压制（CS0067，接口统一性有意不触发——房主终局经 step 路径，见 §11.3 决策 4）；临时 csproj 全量预编译复验 **0 警告 0 错误**。
- **Unity EditMode 复跑 1264 passed / 6 failed 归因与修复**（2026-10-12，三提交链后）：
  - **缺陷 M14-T3-B1**（[BclTcpTransport.cs](Assets/_Project/2_Network/BclTcpTransport.cs)）：对端 RST（"远程主机强迫关闭"）时本端 `_connected` 仍为 true → M12-B2 的 `catch (…) when (!_connected)` 过滤器失效 → IOException 逃逸接收线程顶层 → Unity 记 Exception 日志判 `EndToEnd_Forfeit` 失败（M12-B2 修复不完整，kernel 不暴露同款）。修复：三类 IO 异常在接收循环语境一律静默断链（finally Dispose → Disconnected 事件）；kernel +1 防回归用例 `PeerReset_NoExceptionEscapes_ServerDetectsDisconnect`（LingerOption(true,0) 制造 RST，断言静默断链；"无逃逸日志"断言仅 Unity 口径可判）。
  - **缺陷 M14-T3-B2**（[PvpLobbyFlow.cs](Assets/_Project/5_Bootstrap/Battle/Net/PvpLobbyFlow.cs)）：① fake 工厂路径不写 `ListenAddress`（真实路径才有）→ `CreateAsHost_Success` 断言 null 失败——修复：CreateAsHost 成功后统一 `ListenAddress ??= 127.0.0.1:{port}`（状态文本同样依赖）；② Ready 动作无角色 guard，同一 `_link` 被两个动作重复发送 → `ReadyActions_ForwardToLink` 暴露——修复：MarkHostReady 仅房主角色 / SendJoinerReady 仅加入方角色生效；测试同步拆双 flow 验证互不串扰。
  - **测试修正 ×2**（非实现缺陷）：`Buttons_RouteToEvents` 按钮路径漏 `ChooseGroup` 层级；`PumpUntil` 纯忙循环饿死 `BclTcpTransport.ReceiveLoop` 后台线程 → 真实 TCP 的快照/回执迟到（`EndToEnd_Handshake`/`EndToEnd_CommandSink` 失败）——补 `Thread.Sleep(1)` 让渡（kernel 无此问题：Bootstrap 测试不进 kernel，M14-T3 前仅编译验证过）。
  - 复验：kernel **983 passed / 0 failed**（982 + 1 防回归），临时 csproj 全量预编译 0 警告 0 错误，check.ps1 PASS；Unity 复跑预期 **1271 passed / 0 failed**（1264 + 6 修复 + 1 新用例）。
- **Unity EditMode 二次复跑 1268 passed / 3 failed 归因与修复**（2026-10-12）：fake 组 3 例全过、Forfeit 的 RST 异常消失（B1 生效）——剩余 3 例中 **2 个为测试逻辑错误**（非实现缺陷）：① `EndToEnd_Handshake` 未 Ready 即等快照，而快照随 MatchStart 下发（M12 双方 Ready 才开局）——改走 ReadyUp；② `EndToEnd_CommandSink` 漏接 `activeDriver.StepApplied → feedback.NotifyStep`（BindNetFeedback 接线未在测试复刻），pending 永不裁决——补接线；③ `TurnTimeout_AutoEndsTurn`（M13 既有测试）：AdvanceIdle 每批单帧 Pong + 5ms 让渡，编辑器高负载下 IO 接收线程调度延迟超 5ms → Pong 不入队 → idle 超 60 心跳超时摘端点 → 下批 Send 抛"TCP 传输已断开"——改整批 50 帧缓存 + Sleep(10)（接收线程醒一次即可清零 idle，不再赌固定延迟）。复验：kernel 983/0 + 全量预编译 0/0 + check PASS；Unity 复跑预期 **1271 passed / 0 failed**（总数不变：1268 + 3 修复）。

### 11.3 设计决策（装配期裁定）

1. M14-T1-L1 处置：输入**不禁点**（M13 seq 收件箱支持多在途）；pending 视觉 + 权威回执回退提示（既有 M14-T1 路径）。M14-T2-L1 处置：不干预插值（CardView.SetData 复位路径既有）。
2. 开局时序沿用 M12 语义（对手 Ready 即开局，房主 Ready 为流程引导）；残留竞争窗口由 75s 回合超时兜底——登记观察项 **M14-T3-O1**（P3）。
3. 房主固定端口 7777（"再来一局"地址可复现），占用即"!"提示。
4. 终态判定：step.finished 为主 + MatchEnd 帧补充（HostQuit/Disconnect 可读文本；winner=null 显示"对局作废"）。
5. 大厅 overlay 内置于 Battle 场景（GameMode=PVPNet），避免跨场景对象传输（铁律 8）。
6. `INetMatchLink` 增设 `SendReady()` 统一准备上行（偏离任务卡 §5 原约定一行，两实现各一行转发；fake 测试受益）。

### 11.4 遗留与登记

| 编号 | 级别 | 内容 | 处置 |
| --- | --- | --- | --- |
| M14-T3-O1 | P3（观察项） | 先手为加入方且极快出牌的竞争窗口（M12 Ready 语义），本机并排验证可接受 | 75s 回合超时兜底；体验优化属 M14-T6 |
| M14-T3-T1 | 待验证 | Unity EditMode 实跑（预期 1270/0）待用户回报后留证收官 | 用户实跑 |

