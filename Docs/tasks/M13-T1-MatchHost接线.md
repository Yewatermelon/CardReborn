# 任务卡 · M13-T1 MatchHost 接线

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M13-T1 |
| 所属里程碑 | M13 权威状态同步与裁剪 |
| 上游需求 | FR-13（联网对战）、[05 §3.4/§4](../05-联网对战_状态同步_设计文档.md) |
| 规则依据 | Docs/05 §4.3（服务器 tick）、§9.1（消息列表）、§9.2（生命周期） |
| 预估 | 1 会话 |
| 依赖任务 | M12 全部（PeerHost/PeerSession/MessageCodec/ITransport 已交付） |

## 2. 目标（一句话）

> 打通"网络帧 ↔ MatchHost 上行"桥接：PeerSession 收到 `Command` 帧后转发给权威 MatchHost，MatchHost 产出的 snapshot/step 回传网络对端；双方 Ready 后进入对局并各自收到按座位投影的开局快照，房主本地走 Loopback。

## 3. 范围（做什么）

- `MatchHost`（Card.Application）：
  - `Attach` 携带座位 Id（`Attach(endpoint, seat)`），按端点记忆座位；
  - 初始快照按端点座位投影（`observerPlayerId = seat`），不再硬编码 0；
  - `step` 广播保持现有形态（T4 再加 basedOn 版本）。
- `HostBridge`（Card.Network，新文件）：把一个 `ITransport` 与一个 `LoopbackEndpoint` 对接——
  - 网络 → 内核：`Command` 帧 payload 转成 MatchHost 上行 JSON 入 endpoint；
  - 内核 → 网络：endpoint 出站 `snapshot`/`step` JSON 帧化为 `Snapshot`/`StateDelta` 帧发送；
  - 未知 kind / 畸形 JSON 记 NetworkLogger 警告并丢弃，不抛出（T9 再做完整异常注入矩阵）。
- `PeerSession`：`Pump` 中将 `NetworkMessageType.Command` 帧交给 HostBridge 上行；握手完成（Ready→MatchStart）后建立桥接。
- `PeerHost`：提供 `RunNetworkTick()`——依次驱动 PeerSession.Pump（握手/心跳）、HostBridge 网络侧泵、MatchHost.Pump（权威推进）。

## 4. 明确不做（防止范围蔓延）

- 不做命令信封（seq/turn/token）与幂等——T2/T3；
- 不做 StateDelta{basedOn} 正式结构与客户端应用——T4；
- 不做 SnapshotRequest/重连——T5/T7；
- 不做真实视野裁剪（仍 identity 投影）——T6；
- 不做异常注入矩阵与终态策略——T8/T9；
- 不做回合操作超时自动结束回合——T8；
- 客户端侧（NetworkMatchClient）仅保证能收到帧，客户端应用逻辑不在本卡。

## 5. 接口约定

```csharp
// Card.Application/Match/MatchHost.cs
public void Attach(LoopbackEndpoint endpoint, int seatPlayerId); // 保留旧单参重载 = seat 0
public IReadOnlyList<int> AttachedSeats { get; }                  // 供测试断言

// Card.Network/HostBridge.cs（新）
public sealed class HostBridge
{
    public HostBridge(MatchHost host, LoopbackEndpoint kernelEndpoint, ITransport transport, int seat);
    public void PumpNetworkToKernel();  // 网络帧 → kernel 上行
    public int PumpKernelToNetwork();   // kernel 下行 → 网络帧；返回条数
}

// Card.Network/PeerSession.cs
public bool MatchStarted { get; }     // Ready→MatchStart 后为 true

// Card.Network/PeerHost.cs
public void RunNetworkTick();         // 组合泵：session.Pump + bridge 双向 + matchHost.Pump
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | Attach(endpoint, seat=1) 后 hello | 端点收到的初始快照按座位 1 投影（identity 下内容不变，但投影调用参数正确可观测：AttachedSeats 含 1） |
| AC-2 | 网络侧发送 Command 帧（EndTurn） | HostBridge 转入 MatchHost → 权威接受 → 网络侧收到 StateDelta 帧（kind=step，accepted=true） |
| AC-3 | 非行动方座位发命令 | 权威拒绝，回 step accepted=false，StateVersion 不变 |
| AC-4 | 畸形 Command 帧（非法 JSON / 未知 kind） | HostBridge 丢弃并记警告，MatchHost 状态不变、不抛异常 |
| AC-5 | PeerHost.RunNetworkTick 编排 | session.Pump → bridge 双向 → host.Pump 顺序执行；无消息时 tick 空转无副作用 |

## 7. 测试要求

- 测试类型：EditMode（放 `Assets/_Project/7_Tests/EditMode/Network/`）
- 最少用例数：8（MatchHost 座位投影 2 + HostBridge 4 + PeerHost 编排 2）
- 必须覆盖的边界：座位记忆、命令转发闭环、拒绝路径、畸形帧、空转 tick

## 8. 涉及文档与配置

- 需更新的文档：Docs/PROGRESS.md（任务行）
- 需更新的配置表：无
- 是否影响既有模块：MatchHost.Attach 签名扩展（保留旧重载兼容既有 M11 测试）；PeerSession.Command 分支从忽略改为转发

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 04 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 结论（自检与评审，2026-10-11）

### 六步自检证据

1. **红绿**：先写 9 例确认编译红（`HostBridge`/`AttachedSeats`/`MatchStarted`/`RunNetworkTick`/`NotifyMatchStarted` 未定义）→ 实现 → 全绿。
2. **门禁**：`coverage.ps1` PASS——kernel **911 passed / 0 failed**（基线 902 + 9），`0_Core 91.96%`（门禁 90）、`Domain + App 93.02%`（门禁 80）；`check.ps1` PASS 0 违规。
3. **铁律**：规则三层仅 BCL（MatchHost 只加 Dictionary/List）；依赖方向正确（Card.Network → Card.Application，反向未引入）；Presentation 未触碰；无 UnityEngine / Debug.Log / static Instance / GameObject.Find。
4. **行数**：MatchHost.cs 218 / HostBridge.cs 145 / PeerHost.cs 298（贴近 300 上限，登记观察项）；新增方法均 ≤ 50 行。
5. **0 warning**：CS8602（NUnit 断言不收窄可空）已按惯例以 `AcceptPeer()!` 显式收窄修复。
6. **.meta**：4 个新文件 .meta 已生成，全仓 GUID 无重复。

### AC 对照

| AC | 证据 |
| --- | --- |
| AC-1 | `MatchHostSeatTests.Attach_WithSeat_RemembersSeatAndSendsSnapshot` + `Attach_DefaultOverload_SeatIsZero` |
| AC-2 | `HostBridgeTests.CommandFrame_Accepted_StateDeltaFrameOut`（StateDelta 帧 accepted=true，版本 0→1） |
| AC-3 | `HostBridgeTests.CommandFrame_WrongPlayer_Rejected_VersionUnchanged` |
| AC-4 | `HostBridgeTests.CommandFrame_MalformedJson_DroppedNoThrow` + `CommandFrame_UnknownKind_DroppedNoThrow` |
| AC-5 | `PeerHostTickTests.RunNetworkTick_FullPipeline_SnapshotThenStateDelta`（真实 TCP 回环全链路）+ `RunNetworkTick_Idle_NoSideEffects` |

### 遗留与登记

- 🔵 **待 Unity EditMode 实跑留证**（新测试套件首提环境覆盖门禁）——等用户 Test Runner 回报数字后关闭。
- 🔵 **M13-OBS-1（P3）**：PeerHost.cs 298 行贴近 300 上限——T2 改造时把 PeerSession 拆分为独立文件。
- 开局前 Command 帧当前仅丢弃 + 记日志；正式的拒绝语义（错误码回传客户端）归 T2/T9 范围。
- ✅ **M12-B1（本卡验证期间发现并关闭）**：Unity 报 `PeerHostTickTests.cs(33,57) CS0246 PeerHost not found`。排查链：EditMode 测试 asmdef 引用正常 → Card.Network.asmdef 引用正常 → 源码 dotnet 编译 0 错 → 二进制检索 `Library/ScriptAssemblies/Card.Network.dll` 发现缺 `PeerHost`/`BclTcpTransport` 类型 → 定位 M12 批次手工生成 5 个 `.meta` 的 GUID 为 33/34 位（合法 32 位），Unity 拒绝导入并静默排除 `PeerHost.cs`/`BclTcpTransport.cs`/3 个网络测试文件。已重写 5 个 meta 为合法随机 GUID（全仓 462 个 meta 复扫 0 非 32 位、0 重复），登记 PROGRESS 未关闭问题表 M12-B1（已关闭）+ HANDOFF 已知坑 #12。

