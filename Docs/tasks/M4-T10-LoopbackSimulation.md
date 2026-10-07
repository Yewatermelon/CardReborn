# 任务卡 · M4-T10 进程内"客户端 ↔ 服务器"模拟 ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T10 |
| 所属里程碑 | M4 回合状态机与效果系统（**M4 最终门禁**） |
| 上游需求 | Docs/01 §FR-13.4/13.5/13.6、FR-14.2/14.3/14.4（雏形）、NFR-13；Docs/02 M4-T10（第 169 行）+ M4 门禁（第 171 行） |
| 规则依据 | 权威 MatchController 作为服务器 + 只读视图作为客户端 + 内存消息通道；不写任何真实网络代码 |
| 预估 | 1 会话 |
| 依赖 | M4-T2（MatchController）、M4-T9（CommandSerializer）、M3-T9（MatchStateSerializer）、M3-T10（MatchStateDiffer/StateChange） |

## 2. 目标（一句话）

> 在同一进程内用内存消息通道连接"权威服务器（MatchController）"与"只读客户端视图"：客户端上行序列化命令，服务器校验/结算后下发状态增量，客户端应用增量后视图与权威状态一致，且整局可跑完——为阶段二 PVP 做可行性预演。

## 3. 范围（做什么）

### 3.1 Domain 层修改

- `ManaPool`（`Assets/_Project/1_Domain/Match/ManaPool.cs`）新增：
  - `void Restore(int max, int current)`：快照/增量恢复入口（先例：`CardInstance.Restore`）；负值抛契约异常

### 3.2 Application 层修改

- `MatchStateDiffer`（`MatchStateDiffer.cs`）**协议升级**：
  - `Added` 的 `NewValue` 由"仅 instanceId"升级为"完整卡 JSON 对象"（与 `MatchStateSerializer.WriteCard` 同构）
  - 理由：增量作为下发协议时，客户端可能从未见过该卡（召唤/对手抽牌），仅有 Id 无法重建；FR-13.5 本意即"用于下发增量补丁"
  - 既有测试 `MatchStateDifferTests.Diff_ZoneMembershipChanged_ReturnsAddedAndRemoved` 的 Added 断言同步升级（规格变更，非改绿）
- `MatchStateSerializer`（`.cs` / `.Write.cs` / `.Read.cs`）：`WriteState` / `ReadState` 由 private 改 internal，供协议层直接读写 `JsonValue`（避免字符串往返）

### 3.3 Application 层新增（全部位于 `Assets/_Project/2_Application/Match/`）

- `LoopbackLink.cs`（双类）：
  - `static class LoopbackLink`：`CreatePair(out LoopbackEndpoint a, out LoopbackEndpoint b)` 创建互通双端点
  - `sealed class LoopbackEndpoint`：`Send(string)` / `bool TryReceive(out string)` / `int Pending`；单向 FIFO 队列成对交叉
- `StateChangeSerializer.cs`：`StateChange` 列表 ↔ `JsonValue`（kind 字符串 + path + old/new 内嵌 JsonValue）
- `StateChangeApplier.cs`：`static void Apply(MatchState state, IReadOnlyList<StateChange> changes)`
  - 应用顺序：**Removed → Modified → Added**（Added 语义 = 追加到分区末尾，与 Zone.Add/结算语义一致）
  - 路径解析：`phase|turnNumber|activePlayerId|isFinished`、`players[N].fatigueCounter|hero.*|mana.*`、`players[N].<zone>`（增删）、`players[N].<zone>[i].<field>`（改）
  - `mana.max/current` 经 `ManaPool.Restore`；`keywords/statuses` 经 `Clear()+Add(flags)`
  - `Added` 用携带的完整卡数据经 `CardInstance.Restore` 重建
  - 未知路径/结构非法 → 抛 `FormatException`（明确失败，不吞）
- `LoopbackProtocol.cs`（internal static）：消息编解码
  - 上行：`{"kind":"hello"}`、`{"kind":"command","payload":<CommandSerializer 对象>}`
  - 下行：`{"kind":"snapshot","version":N,"state":<WriteState 对象>}`、`{"kind":"step","version":N,"accepted":bool,"error":"None|...","finished":bool,"changes":[...]}`
- `LoopbackServer.cs`：权威侧
  - `ctor(MatchController)`；`Attach(LoopbackEndpoint)`；`int Pump()`（处理全部待收消息，返回处理条数）
  - hello → 下发快照（含当前版本）
  - command → before 深副本（`MatchStateSerializer` 往返）→ `Submit` → 接受时 `MatchStateDiffer.Diff` 产增量、版本 +1；拒绝时增量为空、版本不变 → 下发 step
  - 状态版本 `Version`：每接受一条命令 +1（为阶段二 FR-14.4 版本机制铺路）
- `LoopbackClient.cs`：只读客户端视图
  - `Attach(LoopbackEndpoint)`；`RequestSnapshot()`（发 hello）；`SubmitCommand(IGameCommand)`（序列化上行）；`int Pump()`（处理下行：快照→重建视图；step→应用增量并记录 `LastAccepted/LastError/Version`）
  - `MatchState? View`（未收到快照为 null；**只读语义**，调用方不得修改）、`bool IsConnected`、`int Version`
  - 未 Attach/未连接时 `SubmitCommand` 抛 `InvalidOperationException`

### 3.4 测试（`Assets/_Project/7_Tests/EditMode/Match/`）

- `ManaPoolTests.cs`：+2 例（Restore 精确覆盖 / 负值抛）
- `StateChangeApplierTests.cs`（新增，7 例）：标量修改 / mana 恢复 / Added 完整卡 / Removed / 跨区移动 / 根字段 / 未知路径抛
- `LoopbackMatchTests.cs`（新增，7 例）：通道 FIFO 双向 / hello 快照一致 / 单命令往返 / 非法命令拒绝且视图不变 / 未连接上行抛 / 双客户端整局疲劳脚本跑完且三方状态一致 / 版本数 == 已接受命令数

## 4. 明确不做（防止范围蔓延）

- **不做视野裁剪**（PlayerViewProjector 属阶段二 FR-14.5；本任务双客户端都收全量）
- **不做事件流下发**（`GameEvent` 序列化与下发属阶段二 FR-14.4 事件部分）
- **不做真实网络**（无 Socket/线程/异步；同步 Pump 驱动，单线程）
- **不做断线重连/快照请求以外的恢复**（版本冲突处理属阶段二）
- **不补 `MatchStateSerializer` 的 `NextInstanceId` 字段**（既有缺口；纯视图客户端不分配 Id 故本任务不受影响——登记为观察项 OBS，阶段二重连快照前必须补）
- **不改 `MatchController`/`RuleEngine`/任何结算器**（权威逻辑零改动）

## 5. 接口约定

```csharp
// Domain 修改
public sealed class ManaPool { public void Restore(int max, int current); }

// Application 新增
public static class LoopbackLink { public static void CreatePair(out LoopbackEndpoint a, out LoopbackEndpoint b); }
public sealed class LoopbackEndpoint { public void Send(string msg); public bool TryReceive(out string msg); public int Pending { get; } }

public static class StateChangeSerializer
{
    public static JsonValue WriteList(IReadOnlyList<StateChange> changes);
    public static List<StateChange> ReadList(JsonValue json);
}

public static class StateChangeApplier { public static void Apply(MatchState state, IReadOnlyList<StateChange> changes); }

public sealed class LoopbackServer
{
    public LoopbackServer(MatchController controller);
    public int Version { get; }
    public void Attach(LoopbackEndpoint endpoint);
    public int Pump();
}

public sealed class LoopbackClient
{
    public MatchState? View { get; }
    public bool IsConnected { get; }
    public int Version { get; }
    public bool LastAccepted { get; }
    public CommandError LastError { get; }
    public void Attach(LoopbackEndpoint endpoint);
    public void RequestSnapshot();
    public void SubmitCommand(IGameCommand command);
    public int Pump();
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 客户端 hello → 服务器 Pump → 客户端 Pump | 客户端收到快照，`Serialize(View)` == `Serialize(权威State)`，版本 0 |
| AC-2 | 客户端上行 EndTurn（合法） | 服务器接受、版本 +1、下发增量；客户端应用后 `Serialize(View)` == `Serialize(权威State)` |
| AC-3 | 客户端上行非行动方命令 | step.accepted=false、error=NotYourTurn、changes 为空、视图不变且仍与权威一致、版本不变 |
| AC-4 | 双客户端 + 疲劳脚本整局（≤80 步，全部经通道上行） | 跑完 IsFinished=true、Outcome 非 Ongoing；两客户端视图与权威三方序列化一致；版本 == 接受命令数 |
| AC-5 | Added 增量含完整卡数据 | 客户端仅凭增量即可在目标分区重建该卡全部字段（含从未见过的 Id） |
| AC-6 | StateChangeApplier 遇未知路径/非法结构 | 抛 `FormatException`，不吞不留半成品语义（明确失败） |
| AC-7 | 门禁 | 无 Unity 工具链全绿；check.ps1 PASS；0 error / 0 warning；GUID 无重复 |

## 7. 测试要求

- 测试类型：EditMode（先红后绿）
- 最少用例数：+16（ManaPool 2 + Applier 7 + Loopback 7）
- 必须覆盖的边界：拒绝命令零变更、Added 完整卡重建、跨区移动顺序、未连接上行、整局终局一致

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M4 10/10、门禁行）、本任务卡结论
- 需更新的配置表：无
- 是否影响既有模块：`MatchStateDiffer` Added 载荷升级（1 处既有测试断言同步）；`ManaPool` 加方法；Serializer 两方法 private→internal——均为窄变更，不改行为语义

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | Added 卡插入位置 | 当前所有结算语义（抽牌/召唤/移坟）均为"分区末尾追加"（Zone.Add），增量协议假设此不变；整局三方序列化比对兜底 |
| Q2 | 服务器 before 副本成本 | 每命令一次 Serialize+Deserialize 深拷贝，测试规模可忽略；进程内模拟不追性能 |
| Q3 | 快照缺 `NextInstanceId`（M3-T9 既有缺口） | 纯视图客户端不分配 Id，不影响本任务；登记 OBS，阶段二重连前补 |
| Q4 | 回环类放 Application 层而非 Card.Network | M4-T10 是"无网络代码"的进程内预演（Docs/02 §171）；Card.Network 程序集阶段二才建，届时传输替换、Loopback 编排逻辑可复用（Docs/05 §15.3 称本任务为其雏形） |

## 10. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界（先红后绿）
- [x] 编译 0 error / 0 warning
- [x] 通过 Docs/03 铁律与禁止清单自查
- [x] 通过 Docs/04 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 11. 结论（完成后回填）

### 交付物

- **Domain 修改**：`ManaPool.Restore(int max, int current)`（`Assets/_Project/1_Domain/Match/ManaPool.cs`）——快照/增量恢复入口，负值抛 `ArgumentOutOfRangeException`。
- **Application 修改**：
  - `MatchStateDiffer.cs`：`Added` 的 `NewValue` 升级为完整卡 JSON（复用 `MatchStateSerializer.WriteCard`），客户端仅凭增量即可重建未见过的卡。
  - `MatchStateSerializer`：`.cs` 加 `internal WriteStateTree/ReadStateTree`；`.Write.cs` 的 `WriteCard` 改 internal；`.Read.cs` 的 `ReadCard` 改 internal（避免协议层字符串往返）。
- **Application 新增**（`Assets/_Project/2_Application/Match/`，6 文件）：
  - `LoopbackLink.cs`：双类（`LoopbackLink.CreatePair` + `LoopbackEndpoint`），单向 FIFO 队列成对交叉，无网络/线程。
  - `StateChangeSerializer.cs`：`StateChange` 列表 ↔ `JsonValue`（kind 枚举名 + path + old/new 内嵌）。
  - `StateChangeApplier.cs` + `StateChangeApplier.Paths.cs`（partial 拆分，行数门禁）：按 **Removed→Modified→Added** 顺序应用；根字段/hero/mana/fatigueCounter/分区增删/卡字段修改全路径覆盖；未知路径/非法结构抛 `FormatException`。
  - `LoopbackProtocol.cs`：上行 hello/command、下行 snapshot/step 的编解码（internal static）。
  - `LoopbackServer.cs`：权威侧，hello 回快照（仅请求者）、command 广播 step（全端点）；版本每接受命令 +1，拒绝不变。
  - `LoopbackClient.cs`：只读客户端视图，hello 请求快照、SubmitCommand 上行、Pump 应用增量；未连接时上行抛 `InvalidOperationException`。
- **测试新增**（`Assets/_Project/7_Tests/EditMode/Match/`，2 文件 16 例）：
  - `StateChangeApplierTests.cs`（7 例）：根标量/hero+mana 修改/Added 完整卡重建/Removed/跨区移动顺序/卡字段修改/未知路径抛。
  - `LoopbackMatchTests.cs`（7 例）：链路 FIFO 双向/快照视图一致/EndTurn 广播两端一致/非行动方拒绝零变更/未连接上行抛/双客户端疲劳整局三方一致/版本数==接受命令数。
  - `ManaPoolTests.cs`：+2 例（Restore 精确覆盖/负值抛）。

### 验证数字

| 验证项 | 结果 |
| --- | --- |
| 无 Unity 工具链（`Tools/coverage.ps1`） | **741 passed / 0 failed**（基线 725 + 16 新增） |
| 覆盖率 | `0_Core 96.52%` / `Domain + App 91.39%`（门禁 90%/80%） |
| 静态门禁（`Tools/check.ps1`） | **PASS**（209 文件，0 违规） |
| 编译 | 0 error / 0 warning |
| .meta | 9 个新文件手写 .meta，全 Assets 295 GUID 无重复（_Project 内 249） |
| Unity EditMode 补验 | 用户实跑 **761 passed / 0 failed**（745 + 16 新增，2026-10-07） |

### 关键决策与边界

1. **Differ 协议升级**：Added 载荷从"仅 instanceId"改为完整卡 JSON，这是增量作为下发协议的必要条件（客户端可能从未见过该卡）。
2. **ManaPool.Restore**：`Max`/`Current` 为 private set，Restore 是唯一绕过口；负值抛契约异常，不做 CanSpend 校验（恢复语义）。
3. **增量应用顺序**：Removed→Modified→Added 保证跨区移动正确（先移除旧区，再追加新区）。
4. **before 副本**：服务器在 `Submit` 前做 `Serialize+Deserialize` 深拷贝，测试规模可忽略；进程内模拟不追性能（Q2）。
5. **已知缺口（OBS）**：`MatchStateSerializer` 不含 `NextInstanceId`（纯视图客户端不分配 Id，不影响本任务；阶段二重连前必须补）。
6. **范围锁**：不做视野裁剪、不做事件流下发、不做真实网络、不做断线重连；权威逻辑（MatchController/RuleEngine/结算器）零改动。

### 评审结论

- **P0/P1**：无未关闭项。
- **铁律自查**：规则三层只依赖 BCL；无 `UnityEngine`/`Debug.Log`/`Mathf`/`JsonUtility`/`Time`/`UnityEngine.Random`；状态只存数据；单文件 ≤300 行、方法 ≤50 行；0 warning。
- **遗留**：M4-T10-OBS-1（`NextInstanceId` 缺口，阶段二前补）。
