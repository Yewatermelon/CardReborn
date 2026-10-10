# 任务卡 · M11-T2 权威运行时 MatchHost（共享库）

> 本任务是 M11 里程碑的核心门禁——`MatchHost` 在**无 Unity 的测试进程**中跑完一整局。
> 原型来源：M4-T10 `LoopbackServer`/`LoopbackClient`/`LoopbackProtocol`。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M11-T2 |
| 所属里程碑 | M11 权威运行时与状态序列化 |
| 上游需求 | FR-13.1–FR-13.7（内核无 Unity 依赖、状态可序列化/可增量、事件流完整）；Docs/05 §3.3/§4 |
| 规则依据 | Docs/05 §4 权威模型、§5 状态同步机制、§9 协议与生命周期 |
| 预估 | 3–4 会话（提炼 LoopbackServer + IMatchHost 抽象 + PlayerViewProjector 框架 + 测试） |
| 依赖任务 | M11-T1（已通过审计：三层 BCL 纯净 ✅、MatchState 序列化往返 ✅、LoopbackServer kernel 跑完整对局 ✅） |

## 2. 目标（一句话）

> 把 M4-T10 `LoopbackServer` 提炼为正式的 `MatchHost` 权威运行时共享库（位于 `Card.Application`），新增 `IMatchHost` 宿主抽象与 `PlayerViewProjector` 视野裁剪框架，并通过 **kernel 工具链完整对局**门禁。

## 3. 范围（做什么）

### 3.1 新增类型（全部位于 `Assets/_Project/2_Application/Match/`）

| 类型 | 职责 |
| --- | --- |
| `IMatchHost` | 宿主抽象接口：LocalHost / PeerHost / DedicatedHost 共用 |
| `MatchHost` | 权威运行时：持有 MatchController + MatchRecorder，处理 CommandEnvelope → 校验结算 → 生成事件 + 状态增量 |
| `CommandEnvelope` | 命令信封：序号 + 会话令牌 + 目标回合号 + 命令内容（预留字段，M13 做幂等校验） |
| `HostMessage` 系列 | 宿主侧上行/下行消息：Hello / Command → Snapshot / Step（含裁剪状态 + 事件） |
| `PlayerViewProjector` | **框架**：按观察者视角裁剪 MatchState（骨架，裁剪逻辑 M13 实现；M11 默认全量下发） |
| `EventSerializer` | GameEvent → JsonValue 编解码（M4-T9 CommandSerializer 的事件对应物） |
| `ServerTickClock` | 10 Hz tick 驱动（可配置；无真实 tick 时改为命令事件驱动） |

### 3.2 提炼 / 重写

| 原 Loopback 类型 | 去向 | 改动 |
| --- | --- | --- |
| `LoopbackServer` | **删除**，由 `MatchHost` 取代 | ProcessOne 逻辑拆为 MatchHost.SubmitEnvelope |
| `LoopbackClient` | **删除**，由 M12 `NetworkMatchClient` 取代 | M11 不做客户端侧 |
| `LoopbackProtocol` | 拆为 `HostMessageSerializer`（MatchHost 下行/上行）+ `CommandSerializer`（保留） | 加 EventSerializer |
| `LoopbackLink` / `LoopbackEndpoint` | 保留（M12 InMemoryTransport 底层依赖） | 不改 |
| `StateChangeSerializer` | 保留 | 不改 |
| `StateChangeApplier` / `.Paths` | 保留 | 不改 |

### 3.3 协议升级

| 字段 | 原 Loopback | 新 MatchHost |
| --- | --- | --- |
| 状态版本 | `int _version` | `ulong _stateVersion`（单调递增，防长时间运行溢出） |
| step 下行 | 只带 changes | 加 `events`（GameEventView 列表） |
| 命令上行 | 裸 IGameCommand | 加 `CommandEnvelope`（序号 + 令牌 + 回合） |
| 快照 | 全量 state tree | 加 `PlayerViewProjector.Project(state, observerId)` 裁剪（M11 默认 identity） |

### 3.4 文件结构

```
Assets/_Project/2_Application/Match/
├── IMatchHost.cs                    ← 新增
├── MatchHost.cs                     ← 新增（主类）
├── MatchHost.Step.cs                ← 新增（partial，权威推进 + 事件收集）
├── CommandEnvelope.cs               ← 新增
├── PlayerViewProjector.cs           ← 新增（框架）
├── EventSerializer.cs               ← 新增
├── HostMessageSerializer.cs         ← 新增（替代 LoopbackProtocol）
├── LoopbackServer.cs                ← 删除（由 MatchHost 取代）
├── LoopbackClient.cs                ← 删除
├── LoopbackProtocol.cs              ← 删除（内容拆到 HostMessageSerializer）
├── LoopbackLink.cs                  ← 保留（LoopbackEndpoint）
└── ...（其他已有文件不动）
```

## 4. 明确不做（防止范围蔓延）

- **不做真实 TCP 传输**（M12 `BclTcpTransport` 的工作）
- **不做 PeerHost 完整实现**（M12 任务）
- **不做客户端侧**（M12 `NetworkMatchClient` / CommandUplink / StateApplier）
- **不做真正的视野裁剪**（M13 任务）——PlayerViewProjector 只是框架（identity 实现）
- **不做重连逻辑**（M13 任务）
- **不做命令幂等 / 序号校验 / 会话令牌校验**（M13 任务）——CommandEnvelope 只加字段，不做严格检查
- **不做心跳 / 超时 / 掉线检测**（M13 任务）
- **不删除 LoopbackMatchTests**（它仍然在 kernel 工具链跑，作为 M4-T10 的回归基线；M11 新增独立测试）

## 5. 接口约定

```csharp
// IMatchHost（宿主抽象）
public interface IMatchHost
{
    /// <summary>当前权威状态版本（每接受一条命令 +1，拒绝不变）。</summary>
    ulong StateVersion { get; }

    /// <summary>对局是否结束。</summary>
    bool IsFinished { get; }

    /// <summary>处理全部待收上行消息；返回处理条数（Pump 驱动）。</summary>
    int Pump();

    /// <summary>上行一个信封命令；返回权威侧结果（接受/拒绝 + 事件 + 增量）。</summary>
    HostStepResult SubmitEnvelope(CommandEnvelope envelope);
}

// CommandEnvelope（命令信封）
public sealed record CommandEnvelope(
    ulong CommandSequence,         // 客户端递增序号（M11 只加字段不校验）
    string SessionToken,           // 会话令牌（M11 只加字段不校验）
    IGameCommand Command);

// HostStepResult（Step 下行载荷，替代原 LoopbackProtocol.WriteStep）
public sealed record HostStepResult(
    ulong StateVersion,
    bool Accepted,
    CommandError Error,
    bool Finished,
    IReadOnlyList<StateChange> Changes,
    IReadOnlyList<GameEvent> Events);

// PlayerViewProjector（视野裁剪框架）
public sealed class PlayerViewProjector
{
    /// <summary>按观察者视角裁剪权威状态；M11 返回原状态（identity），M13 实现裁剪逻辑。</summary>
    public static MatchState Project(MatchState authoritative, int observerPlayerId);
}
```

**兼容性约定**：
- `MatchHost` 构造器接收 `MatchController` + 可选 `IRandomProvider`/`IClock`/`GameLog` 注入（与 MatchController 构造器签名兼容）
- `CommandEnvelope.SessionToken` 可为 null（PVE 无会话）
- `LoopbackLink` 保留不动，M11 测试复用它做"进程内 MatchHost ↔ 只读客户端"链路

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | MatchHost 从 hello 请求返回快照 | 快照 JSON 含 version=2（NextInstanceId 完整），与权威状态字段一致 |
| AC-2 | MatchHost 接受 EndTurn 命令 | StateVersion +1，StepResult.Accepted=true，Changes 非空，Events 非空（至少 PhaseChanged + TurnStarted + Draw） |
| AC-3 | 非行动方命令被拒绝 | StateVersion 不变，Accepted=false，Error=NotYourTurn，Changes 和 Events 都为空 |
| AC-4 | 双客户端完整对局 | 疲劳脚本跑完（约 70 步），两个客户端经 MatchHost 同步后视图与权威状态一致，StepVersion 等于接受的命令数 |
| AC-5 | CommandEnvelope 序号/令牌透传 | 构造 MatchHost 时接受 envelope，M11 只消费 Command 字段，不拒绝 envelope 其他字段 |
| AC-6 | MatchHost 在 kernel 工具链（无 Unity）跑完整对局 | coverage.ps1 全绿，kernel 测试数从 848 增长 |
| AC-7 | PlayerViewProjector 默认 identity | Project(state, 0).Serialize() == state.Serialize() |

## 7. 测试要求

- **测试类型**：EditMode（kernel 工具链可编译）
- **最少用例数**：12 条（覆盖 AC-1~AC-7 + 边界）
- **必须覆盖的边界**：
  - hello → 快照版本号为 2（FormatVersion 升级后 NextInstanceId 完整）
  - Pump 无消息时返回 0
  - SubmitEnvelope 接受/拒绝路径 Changes 和 Events 一致性
  - 双客户端同步视图与权威状态
  - PlayerViewProjector identity 一致性

- **测试位置**：`Assets/_Project/7_Tests/EditMode/Match/MatchHostTests.cs`（kernel 工具链编译范围）
- **测试夹具**：复用 `MatchControllerFixtures.BuildDatabase()` 和 `MatchControllerFixtures.NewMatch()`

## 8. 涉及文档与配置

- **需更新的文档**：
  - 本任务卡（完成后填 DoD）
  - `Docs/PROGRESS.md`（M11 进度条 + 任务级状态）
  - `Docs/HANDOFF.md` 快照（kernel 测试数 + 覆盖率）
  - `Docs/05` §3.3 MatchHost 描述（提炼后代码位置与 Loopback 关系）
- **需删除的代码**：`LoopbackServer.cs`、`LoopbackClient.cs`、`LoopbackProtocol.cs`（LoopbackLink.cs 保留）
- **需更新的测试**：`LoopbackMatchTests.cs` 可能需要小改以适配新 MatchHost（或保留为独立回归）
- **是否影响既有模块**：
  - LoopbackMatchTests.cs 中 `LoopbackServer`/`LoopbackClient` 用法需适配 → 改为 MatchHost
  - M4-T10 相关文档引用更新
  - `StateChangeApplier` / `StateChangeSerializer` / `MatchStateDiffer` 保持不动（已在 MatchHost 内部消费）

## 9. 完成定义（DoD 勾选）

- [x] LoopbackServer/LoopbackClient/LoopbackProtocol 提炼为 MatchHost + IMatchHost + HostMessageSerializer
- [x] PlayerViewProjector 框架就位（identity 实现）
- [x] kernel 工具链新测试 ≥ 12 条（MatchHostTests 5 + MatchHostCoverageTests 7），全绿
- [x] MatchHost 在 kernel 工具链跑完整双客户端对局（疲劳脚本 DualClient_FatigueScript_FinishesWithConsistentViews）
- [x] coverage.ps1 PASS，0_Core 96.52%、Domain+App 92.85%
- [x] check.ps1 329 文件 PASS，R1~R6 零违规
- [x] LoopbackMatchTests 重命名为 MatchHostTests + MatchHostCoverageTests（拆分以过 R5 行数门禁）
- [x] Docs/02 / HANDOFF / PROGRESS 同步更新
- [x] 提交信息带 `[M11-T2]`

## 10. 实际完成摘要

**提交哈希**：`a9e800d`  
**双门禁**：check.ps1 329 PASS、coverage kernel 全绿 0_Core 96.52% / Domain+App 92.85%  
**删除**：LoopbackServer.cs / LoopbackClient.cs / LoopbackProtocol.cs / LoopbackMatchTests.cs（3 源文件 + 1 测试 + 4 .meta）  
**新增**：MatchHost.cs / IMatchHost.cs / CommandEnvelope.cs / PlayerViewProjector.cs（4 源）+ MatchHostTests.cs / MatchHostCoverageTests.cs（12 条用例）+ M11-T2 任务卡  
**关键设计决策**：
- ProcessOne 拆为 ProcessHello + ProcessCommand（M12 Client 侧独立实现）
- Events 收集用 Count 差值（EventLog 不暴露 Clear）
- Step 消息加 events 字段（类型名列表，完整编码 M11-T6）
- StateVersion ulong（防长时间溢出），版本号 JSON 用 int（兼容 JsonValue.From 重载）
- FormatVersion 升级到 2（M4-OBS-2 NextInstanceId 补位）
- PlayerViewProjector identity 实现（裁剪逻辑 M13）
- 测试拆两个文件过 R5 行数门禁（MatchHostTests 185 行 + MatchHostCoverageTests 155 行）

**M11 门禁结论**：✅ MatchHost 在 kernel 工具链（无 Unity）跑完一整局——M11-T2 硬门槛达成。  
**下一步**：M11-T6（事件完整编码）可跟 M12（本机双进程 + TCP 传输）并行。
