# 任务卡 · M14-T7 20 局回归与 Soak

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M14-T7 |
| 所属里程碑 | M14 双人验证与体验打磨（最后一个任务，7/7） |
| 上游需求 | FR-9.3 阶段二验收清单（[01](../01-开发需求文档.md) §9.3）；Docs/02 M14 门禁 |
| 规则依据 | Docs/05 §15（本阶段范围锁：LocalHost + PeerHost，BCL TCP） |
| 预估 | 2 会话（前置准备 1 + 实现验证 1） |
| 依赖任务 | M14-T1～T6 全部完成；M13 权威同步收官 |

## 2. 目标（一句话）

> 用自动化手段证明"连续 30 局 + 长时间挂机无内存/连接/句柄泄漏"，并逐项核对 [01](../01-开发需求文档.md) §9.3 验收清单，收官 M14。

## 3. 范围（做什么）

**前置准备（本会话）**

- [x] 调研现有基础设施（结论：`AiVsAiSimulator` 提供 kernel 批量对打先例；`PeerHost`/`NetworkMatchClient`/`BclTcpTransport` 提供 TCP 全流程 API；`PeerHostTickTests` 提供真实 TCP 回环全流程测试模板；`ClientStateSync` 提供客户端状态应用；`PvpNetFlowTests.CommandEnvelope` 提供 seq 信封格式）
- [x] 任务卡（本文件）
- [x] `Tools/soak-watch.ps1`：真机双开挂机时的进程资源采样脚本（WorkingSet / PrivateMemory / HandleCount / Threads → CSV），PowerShell 5.1 兼容、纯 ASCII 注释

**实现（下一会话）**

- [x] `NetworkSoakRunner`（`2_Network`，纯 BCL）：真实 TCP 回环（PeerHost 监听 127.0.0.1:0）连续跑 N 局 AI 对打
  - 每局全链路：MatchFactory 建局 → MatchController → MatchHost → PeerHost → 客户端 Connect → Hello/HelloAck → Ready/MatchStart + 快照 → 双座位 AI 驱动至终局 → 收 MatchEnd → 全资源 Dispose → 下一局重建（30 次建/拆监听，专抓 socket/句柄泄漏）
  - 双座位对称驱动（M13-T1 房主镜像语义）：座位 0 走 `PeerHost.LocalClientEndpoint` 上行 + LoopbackEndpoint 收下行；座位 1 走 `NetworkMatchClient.SendCommand` 上行 + Pump 收下行；两侧各挂一个 `ClientStateSync` 应用快照/增量，从应用后的只读状态决策（**数据路径与真实客户端一致**）
  - 决策策略 `SoakPolicy`（脚本化，简单可复现）：可出随从（费用≤法力）则出第一张 → 有可攻击随从则攻击（嘲讽优先、Rush 不能打英雄）→ 否则 EndTurn；命令一律 seq 信封（座位 0/1 各自独立递增，复用 `PvpNetFlowTests.CommandEnvelope` 格式）
  - 报告 `NetworkSoakReport`：局数 / 终局数 / 胜者 / 回合数 / 提交数 / 被拒数 / 首个拒绝原因 / 断连数 / 心跳循环数，一局失败即记失败原因继续
- [x] 挂机模拟（kernel 测试）：`ManualClock` 快进 ≥ 60 000 tick（≈100 分钟 @10 Hz）对局中挂机，断言心跳维持、连接不断、StateVersion 无无故增长、会话对象无堆积（`NetworkSoakIdleTests`）
- [x] §9.3 清单勾选表回填本任务卡（自动化已覆盖 vs 待真机验证逐项标注）

## 4. 明确不做（防止范围蔓延）

- 不做客户端预测 / 反作弊 / 命令频率限制 / 并发对局压测 / 专用服务器（阶段三，Docs/02 已移出）
- 不做真机内存 profiling 自动化——真机双开 30 局与挂机由用户按 HANDOFF §4.3 操作，`soak-watch.ps1` 只提供采样工具，判定由人做
- 不改规则层任何行为；不引入第三方网络库
- 不追求 AI 对局质量（SoakPolicy 只需合法且能终局；AI 质量属 M7 已验收范围）

## 5. 接口约定

```csharp
// 2_Network/NetworkSoakRunner.cs（Card.Network，仅依赖 BCL + Card.Application 既有类型）
public static class NetworkSoakRunner
{
    public static NetworkSoakReport Run(CardDatabase database, NetworkSoakOptions options);
}

public sealed class NetworkSoakOptions
{
    public int MatchCount;              // 局数（门禁快测 3，完整 30）
    public int BaseSeed;                // 每局 seed = BaseSeed + i
    public int MaxStepsPerMatch;        // 单局步数上限（防死循环，超限记失败）
    public int MaxPumpsPerWait;         // 单次等待 tick 泵上限（防卡死）
}

public sealed class NetworkSoakReport
{
    public IReadOnlyList<NetworkSoakMatchRecord> Matches { get; }
    public int FinishedCount { get; }   // 收到 MatchEnd 且 Outcome 非 Ongoing
    public bool AllFinished { get; }
}

// Tools/soak-watch.ps1（纯 ASCII 注释）
# param: -Name CardReborn | -ProcIds 1234,5678 -Interval 5 -Duration 0(=until Ctrl+C) -Out soak.csv
# CSV columns: timestamp,pid,name,working_set_mb,private_mb,handles,threads
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `NetworkSoakRunner.Run` 连续 30 局真实 TCP 回环 | 30 局全部终局（每局收到 MatchEnd、Outcome 非 Ongoing），`AllFinished == true`，0 失败 |
| AC-2 | 每局结束全资源 Dispose 后重建下一局 | 30 次建/拆监听无端口耗尽、无连接残留；下一局握手正常 |
| AC-3 | 每局对局数据 | 提交数 > 0；被拒数如实统计（SoakPolicy 只发合法命令，理想为 0）；断连数 = 0 |
| AC-4 | 挂机模拟：ManualClock 快进 ≥ 60 000 tick | 心跳 Ping/Pong 维持、连接不断、StateVersion 无无故增长、无异常抛出 |
| AC-5 | 静态门禁与覆盖率 | `check.ps1` PASS；coverage 门禁 `0_Core ≥ 90%` / `Domain+App ≥ 80%`；kernel 全绿 |
| AC-6 | `soak-watch.ps1` | 可按进程名/pid 采样并输出 CSV；PS 5.1 下无乱码（纯 ASCII 注释） |
| AC-7 | 真机双开（用户执行） | 双开 30 局 + ≥30 分钟挂机，soak-watch CSV 无持续增长趋势；§9.3 待真机项逐项勾选 |

## 7. 测试要求

- 测试类型：EditMode（kernel 同源，`Tools/Coverage` 可跑）
- 最少用例数：8（SoakRunner 快测 3 局 1 + 单局结构断言 2 + 挂机模拟 2 + 报告聚合 2 + 防死循环上限 1）
- 必须覆盖的边界：单局步数超限记失败不中断批量；对局中挂机；大厅阶段挂机（不开局）；重复 Dispose 幂等
- 决策点：完整 30 局测试耗时若 > 60s，则门禁只跑 3 局快测，30 局改为独立入口（[Explicit] 或命令行工具），实现时实测耗时后定并回填本卡
  - **实测结论：30 局约 24s（kernel 同步驱动 + 回环 TCP），远低于 60s → 门禁直接跑 30 局（`Run_ThirtyMatches_AllFinished`），无需 [Explicit] 独立入口**

## 8. 涉及文档与配置

- 需更新的文档：本任务卡（结论回填 + §9.3 勾选表）；`Docs/PROGRESS.md`（M14 7/7 收官）；`Docs/HANDOFF.md`（快照 + §4.3 附 soak-watch 用法）
- 需更新的配置表：无
- 是否影响既有模块：只加不改；`2_Network` 新文件需 .meta（GUID 32 位 hex 全仓唯一，M12-B1 教训）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（AC-1～AC-5 见 §11 验证数字；AC-6 `Tools/soak-watch.ps1` 已入库提交 `47f4065`；AC-7 真机项移交用户，见 §11 遗留）
- [x] 测试通过且覆盖边界（kernel `NetworkSoakRunnerTests` + `NetworkSoakIdleTests` 共 9 例全绿：30 局全部终局 / 单局结构 / 步数超限记失败 / 对局中与大厅阶段挂机 / 重复 Dispose 幂等 / 报告聚合）
- [x] 编译 0 error / 0 warning（kernel dotnet 构建；`check.ps1` PASS 同门禁）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（`2_Network` 纯 BCL、无第三方网络库；无单例/Find；单文件 ≤300 行；新 .cs 均带 .meta 且 GUID 32 位全仓唯一）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（两处必要变更已按 AGENTS §7 登记，见 §11 根因链 3/4）
- [x] 涉及文档已同步更新（本卡 + `Docs/PROGRESS.md` + `Docs/HANDOFF.md` §3/§4.3）

## 10. §9.3 阶段二验收清单核对表

> 状态图例：✅ 自动化已覆盖（引用测试）｜🖥 真机待验证（用户执行）｜⬜ 待回填

**环境与流程**

- [ ] 同一台 PC 上两个进程可连接 → 🖥（M13 已 20 局；本任务 30 局复验）
- [ ] 房主界面显示 `IP:端口`，玩家 2 填入后可进入准备状态 → 🖥（M14-T3/T6 已验证过，复验）
- [ ] 双方各看到自己的 3 张起手牌；玩家 2 界面上看不到房主手牌内容 → 🖥 + ✅（裁剪测试 M13-T4/T5；真机复验）
- [ ] 完整打完 ≥ 20 局，双方画面一致、回合归属正确、无错乱、无假活 → ✅ AC-1（kernel 30 局）+ 🖥 真机 30 局

**权威与数据**

- [ ] 所有状态变更都能追溯到一条经房主校验的命令 → ✅（MatchStepRecord 台账；Soak 报告提交数断言）
- [ ] 玩家 2 侧修改本地数据（血量/法力/手牌）不影响对局结果 → ✅（客户端只读状态 + 权威校验，M13 已证；Soak 双 ClientStateSync 路径复证）
- [ ] 玩家 2 侧伪造非法/重复/越权/超频命令全部被拒且状态不变 → ✅（M13-T9 异常注入防御测试）
- [ ] 版本冲突或消息异常后可通过快照恢复，不需要重启 → ✅（M13-T7 重连快照回退测试）
- [ ] 房主手牌与牌库顺序不出现在任何下发给玩家 2 的数据中（含快照与重连数据）→ ✅（M13-T4/T5 裁剪测试）

**异常与边界**

- [ ] 玩家 2 掉线：房主提示"对手掉线"，重连窗口内可恢复 → ✅（M13-T7 / M14-T3-B5 / T6 HostPeerWatch）+ 🖥 真机复验
- [ ] 房主退出：对局结束并给出明确提示，不卡在"等待中" → ✅（M13-T8 HostQuit 编排 / M14-T6）+ 🖥 真机复验
- [ ] 双方回环 RTT ≤ 5 ms，操作到权威确认 ≤ 100 ms → ✅（本机回环 TCP；Soak 步进延迟隐式覆盖）+ 🖥 体感复验
- [ ] 双实例同时运行在本机 ≥ 60 FPS（低配机 ≥ 30 FPS 可接受）→ 🖥（用户观察回报）

**工程约束**

- [ ] 传输使用 BCL，未引入任何第三方网络库 → ✅（check.ps1 R6 + Packages/manifest.json 无新依赖）
- [ ] 未引入匹配服务、账号体系、NAT 穿透或中继 → ✅（范围锁，代码内无此概念）
- [ ] 移除 `Card.Network` 程序集后项目仍可编译出包，PVE 功能不受影响 → ✅ 结构保证（`Card.Presentation`/`5_Bootstrap` 仅依赖 `Card.Application` 接口，铁律 12；`check.ps1` 依赖方向校验 PASS）+ 🖥 Unity 编译复验（用户执行：移除 asmdef 引用后编译）
- [ ] 文档与 UI 均明确标注"本阶段不提供反作弊，仅用于本机验证" → ✅（联机大厅标注 M14-T3；Docs/05 §15）

## 11. 结论（收尾回填）

**完成日期**：2026-10-13（前置准备与实现验证同会话完成，收官 M14 7/7）

### 11.1 交付物

- `2_Network`（纯 BCL）：[NetworkSoakRunner.cs](Assets/_Project/2_Network/NetworkSoakRunner.cs)（30 局真实 TCP 回环全链路：MatchFactory 建局 → MatchHost → PeerHost → 客户端 Hello/Ready/快照 → 双座位对称 AI 驱动至终局 → MatchEnd → 全资源 Dispose 重建）+ `SoakPolicy` / `SoakSeatClient` / `NetworkSoakReport` / `NetworkSoakOptions` / `SoakDiagnostics`
- kernel 测试：`NetworkSoakRunnerTests`（门禁直接跑 30 局）+ `NetworkSoakIdleTests`（ManualClock 快进 ≥60 000 tick 挂机）+ `SoakTestHarness` 夹具
- `Tools/soak-watch.ps1`（真机采样，前置会话提交 `47f4065`）

### 11.2 根因链（Soak 调试收口，四项修复）

1. Nagle 滞留 → `NoDelay`（前置会话）。
2. 传输层 O(n²) 拆帧 → `BclTcpTransport` 定长装配缓冲 + `Buffer.BlockCopy` O(n) 重写（前置会话）。
3. **seq 回执广播污染**（本会话协议修正，**必要变更登记**）：`MatchHostStepFactory.BuildSeatSteps` 原把同一 seq 写给**所有座位**的 step → 对手 `LastReceiptSeq` 被交叉污染（实证 seat0Seq==seat1Seq==759）→ 基于过期视图连发命令大量被拒（实证单局 Rejected 2442）。修正为 **seq 回执只写命令归属座位**：`BuildSeatSteps` 新增 `actorSeat` 参数，`MatchHost` 的 `SubmitAndBroadcast`/`Reject`/`ProcessCommand`/`HandleSequenced` 四处同步；依据 `PendingCommandTracker` 设计注释（"对手 step → None"）与 M13-T3"信封 seq 按座位独立编号"协议语义。连带更新 `MatchHostCommandSequenceTests.SequencesIndependentPerSeat`（显式排空对手侧收到的无 seq step 帧）。
4. **SoakPolicy 误用 `StatusSet.Has` 全位语义**（本会话）：`Has(Sickness | Frozen)` 是"包含全部位"而非"任一"（`StatusSet.cs` §20 实锤），召唤失调随从被误判可攻击、遭权威拒绝（实证 17 869 次，firstReject=seq=3 SummoningSickness）。修正为分别检查 `SummoningSickness` 与 `Frozen`。

### 11.3 验证数字（2026-10-13 实测）

| 项 | 结果 |
| --- | --- |
| Soak 测试 | **9/9 全绿**（`dotnet test --filter FullyQualifiedName~Soak`，26s；30 局全部终局、断连 0、心跳维持） |
| coverage 门禁 | **kernel 1016 passed / 0 failed**（基线 1007 + 9）；`0_Core 95.82%`（门禁 90%）/ `Domain + App 92.66%`（门禁 80%） |
| 静态门禁 | `check.ps1` PASS（415 文件）；编译 0 warning |
| Unity EditMode | **1309 passed / 0 failed**（2026-10-13 实跑，1300 + 9 与预期一致） |

### 11.4 决策点实测

30 局约 24s（kernel 同步驱动 + 回环 TCP），远低于 60s 阈值 → **门禁直接跑 30 局**（`Run_ThirtyMatches_AllFinished`），无需 [Explicit] 独立入口。

### 11.5 提交

- feat：`2e93fcc`（26 文件，+1406/−71）+ 兼容修复 `64635fe`（Stopwatch.GetElapsedTime → GetTimestamp+Frequency）
- docs：本提交（任务卡 + PROGRESS + HANDOFF）

### 11.6 遗留项（移交）

- **AC-7 真机**（用户执行）：双开 30 局 + ≥30 分钟挂机，`soak-watch.ps1` 采样 CSV 无持续增长趋势（用法见 HANDOFF §4.3）；§10 各 🖥 项随真机复验逐项勾选。
- **"移除 `Card.Network` 程序集仍可编译出包"复验**（用户执行）：移除 asmdef 引用后编译（§10 结构保证项）。
