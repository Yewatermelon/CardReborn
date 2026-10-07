# 任务卡 · M4-T9 命令序列化与对局录制 ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M4-T9 |
| 所属里程碑 | M4 回合状态机与效果系统 |
| 上游需求 | Docs/01 §FR-13、FR-14（联网前置）；Docs/02 M4-T9 |
| 规则依据 | `GameCommand` 紧凑序列化（上行协议基础）；命令流水可导出/导入并重放 |
| 预估 | 1 会话 |
| 依赖 | M3-T4（命令模型）、M3-T9（MatchState 序列化）、M3-T5（MatchFactory）、M4-T2（MatchController） |

## 2. 目标（一句话）

> 实现四种 `GameCommand` 的紧凑 JSON 序列化与反序列化；实现"初始状态+命令流水"对局录制，重放后得到与原局一致的状态与事件序列。

## 3. 范围（做什么）

### 3.1 Domain 层新增

- `CommandSerializer`（`Assets/_Project/1_Domain/Match/`）：
  - `string Serialize(IGameCommand command)` → 紧凑 JSON 文本
  - `IGameCommand Deserialize(string json)` → 还原命令（带运行时类型标签）
  - 格式：`{ "type": "PlayCard", "playerId": 0, "cardInstanceId": 5, "target": { "kind": "Hero", "targetId": 1 } }`
  - `target` 字段：kind=`None|Minion|Hero` + targetId
  - 未知 type 抛 `ArgumentException`

- `MatchRecording`（`Assets/_Project/1_Domain/Match/`）：
  - 不可变值对象：双方 `MatchSetupRequest`（英雄 Key + 卡组 Key 列表）+ `int Seed` + `IReadOnlyList<string> Commands`
  - `ToJson()` / `FromJson(string)`：用 `JsonValue` 纯 BCL 实现

### 3.2 Application 层新增

- `MatchReplayer`（`Assets/_Project/2_Application/Match/`）：
  - `static ReplayResult Replay(MatchRecording recording, CardDatabase db)`
  - 用相同 seed 重建 `IRandomProvider` → `MatchFactory.Create` → 新建 `MatchController`
  - 逐条反序列化命令并 `Submit`
  - 返回终局 `MatchState` + `EventLog` + `History`

### 3.3 测试

- `CommandSerializerTests`：
  - 四种命令各 1 例往返一致
  - TargetRef.None / Minion / Hero 全覆盖
  - 未知 type 抛异常
- `MatchReplayerTests`：
  - 录制→回放：状态终局一致、事件序列一致、History 一致
  - 使用真实 `RuleEngineTestHelpers.BuildDatabase` + 固定 seed

## 4. 明确不做

- **不做网络传输层**（T9 只做序列化/录制数据结构，M12 才做 TCP）
- **不做压缩/加密**（JSON 文本即为录制格式）
- **不做部分重放/快进/回退**（完整重放 only）
- **不做 UI 录制**（只录命令+初始状态）

## 5. 接口约定

```csharp
public static class CommandSerializer
{
    public static string Serialize(IGameCommand command);
    public static IGameCommand Deserialize(string json);
}

public sealed class MatchRecording
{
    public MatchRecording(MatchSetupRequest seat0, MatchSetupRequest seat1, int seed, IReadOnlyList<string> commands);
    public string ToJson();
    public static MatchRecording FromJson(string json);
}

public static class MatchReplayer
{
    public static ReplayResult Replay(MatchRecording recording, CardDatabase db);
}

public sealed class ReplayResult
{
    public MatchState FinalState { get; }
    public IReadOnlyList<GameEvent> Events { get; }
    public IReadOnlyList<MatchStepRecord> History { get; }
}
```

## 6. 验收标准（可测）

| # | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | PlayCardCommand 往返 | Serialize→Deserialize 后字段全等 |
| AC-2 | AttackCommand 往返 | 同上 |
| AC-3 | UseHeroPowerCommand 往返 | 同上（含 TargetRef.None） |
| AC-4 | EndTurnCommand 往返 | 同上 |
| AC-5 | 未知 type 反序列化 | 抛 `ArgumentException` |
| AC-6 | 对局录制→重放一致 | 同 seed 同命令 → 终局状态/事件/History 与原局一致 |
| AC-7 | 门禁 | check.ps1 PASS；0 error / 0 warning |

## 7. 测试要求

EditMode，≥ 8 例；先红。

## 8. 涉及文档与配置

- 更新 `Docs/PROGRESS.md`、本任务卡结论。
- 配置表：无变更。

## 9. 待确认问题与假设

| # | 问题 | 假设 |
| --- | --- | --- |
| Q1 | TargetRef 序列化格式？ | `{ "kind": "None|Minion|Hero", "targetId": N }` 字符串枚举 |
| Q2 | MatchRecording 包含完整卡组 Key 列表？ | 是，重建对局需要双方卡组；体积由调用方控制 |
| Q3 | 重放时 CardDatabase 从哪来？ | 外部传入（Replayer 不持有 DB，与 Factory 口径一致） |

## 10. DoD

- [x] 全部 AC 满足且附证据（AC-1~7 见 §6；11 个测试全绿）
- [x] 先红后绿（CS0103 × 20+ → 3 个 Replay 测试因缺 TheCoinCardKey 失败 → 改用真实配置夹具 → 725/0 全绿）
- [x] 0 error / 0 warning（无 Unity 工具链 725 passed / 0 failed；Unity 编辑器补验待用户回报）
- [x] Docs/03 铁律自查 + Docs/04 评审无 P0/P1
- [x] PROGRESS 更新

---

## 11. 自检与评审结论（2026-10-07）

### 六步自检

1. **编译**：无 Unity 工具链 725 passed / 0 failed / 0 warning；`check.ps1` PASS（200 文件）
2. **测试**：11 例新增（4 命令往返 + 未知类型 + 3 重放一致 + 录制往返 + 缺字段），先红后绿
3. **覆盖率**：CommandSerializer 94%、MatchRecording 100%、MatchReplayer 100%；汇总 0_Core 96.52% / Domain+App 91.92%
4. **铁律**：Domain（CommandSerializer）/ Application（MatchRecording/Replayer）分层；无 UnityEngine 引用；单文件 ≤ 300 行
5. **解耦**：序列化只用 JsonValue（纯 BCL）；Replayer 不持有 DB，外部传入（与 Factory 口径一致）
6. **范围**：不做网络传输/压缩/快进回退（§4 明确不做）

### 评审结论

无 P0/P1。AC-1~7 全部满足，门禁通过。

### 双环境验证

- 无 Unity 工具链：**725 passed / 0 failed**
- Unity 编辑器 Test Runner（EditMode）用户实跑补验：待回报（预期 **745 = 734 + 11 新例**）
