# 任务卡 · M3-T4 命令与结果模型

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T4 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.1（结束回合）、FR-5.3（出牌流程）、FR-5.5（攻击流程）、FR-5.10（英雄技能）；FR-13.6（命令可序列化）、FR-14.3（命令上行/Accepted/Rejected） |
| 规则依据 | [01 §3.3](../01-开发需求文档.md)（出牌规则）、[01 §3.4](../01-开发需求文档.md)（攻击规则）、[01 §3.6](../01-开发需求文档.md)（英雄技能）；[03 §5.3](../03-开发规范文档.md)（命令模式：不可变值对象、Validate→Apply）；铁律 5、12 |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M3-T2（CardInstance/Zone）、M3-T3（关键词/状态） |

## 2. 目标（一句话）

> 建立"玩家意图 → 不可变命令"的 `GameCommand` 家族（出牌/攻击/英雄技能/结束回合）与携带失败原因的 `CommandResult`，使所有玩家与 AI 操作有唯一、可录制、可校验的数据载体（本任务只建模型，不含校验/结算逻辑）。

## 3. 范围（做什么）

- `1_Domain/Match/IGameCommand.cs`：命令标记接口，仅 `int PlayerId { get; }`（命令来源座位）。
- `1_Domain/Match/TargetRef.cs`：目标引用值对象 + `TargetKind` 枚举（None/Minion/Hero）；工厂 `TargetRef.None`、`ForMinion(int instanceId)`、`ForHero(int playerId)`；只读 `Kind`、`TargetId`、`IsNone`。
- `1_Domain/Match/GameCommands.cs`：四个 `readonly struct` 命令（均实现 `IGameCommand`），构造函数赋值、不可变：
  - `PlayCardCommand(playerId, cardInstanceId, target)`：出一张手牌；`target` 可为 `None`。
  - `AttackCommand(playerId, attackerInstanceId, target)`：我方场上随从攻击；`target` 为随从或英雄。
  - `UseHeroPowerCommand(playerId, target)`：使用英雄技能；`target` 可为 `None` 或角色。
  - `EndTurnCommand(playerId)`：主动结束回合。
- `1_Domain/Match/CommandResult.cs`：校验结果值对象 + `CommandError` 枚举。
  - `CommandResult`：`IsValid`/`IsInvalid`、`CommandError Error`、`string Detail`；工厂 `Valid()`、`Invalid(error, detail?)`。
  - `CommandError`：`None=0` + 与四类命令对应的失败原因（见 §5）。
- EditMode 测试：命令不可变数据正确、`TargetRef` 三种构造、`CommandResult` 有效/失败携带错误码与说明。

## 4. 明确不做（防止范围蔓延）

- **不做** `RuleEngine.Validate/Apply`：M3-T6；本任务只提供命令与结果数据结构，不写任何校验/结算规则。
- **不做** 命令的网络信封（会话令牌、命令序号、目标回合号）：属 `Card.Network`/M11–M13；`IGameCommand` 不含序号。
- **不做** 命令序列化实现：M4-T9 / M3-T9；本任务只保证命令是"可序列化形态"（纯数据、无引用、无方法依赖）。
- **不做** 命令队列/录制器/回放器：M4-T2/T9。
- **不做** 目标合法性的业务判定（嘲讽强制、目标存活等）：M3-T6；`TargetRef` 只是引用。
- **不做** 新增配置表或关键词枚举。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public interface IGameCommand
    {
        int PlayerId { get; }
    }

    public enum TargetKind { None = 0, Minion = 1, Hero = 2 }

    public readonly struct TargetRef
    {
        public TargetKind Kind { get; }
        public int TargetId { get; }          // Minion=CardInstance.InstanceId; Hero=玩家座位Id; None=0
        public bool IsNone { get; }
        public static TargetRef None { get; }
        public static TargetRef ForMinion(int instanceId);
        public static TargetRef ForHero(int playerId);
    }

    public readonly struct PlayCardCommand : IGameCommand
    {
        public PlayCardCommand(int playerId, int cardInstanceId, TargetRef target);
        public int PlayerId { get; }
        public int CardInstanceId { get; }
        public TargetRef Target { get; }
    }

    public readonly struct AttackCommand : IGameCommand
    {
        public AttackCommand(int playerId, int attackerInstanceId, TargetRef target);
        public int PlayerId { get; }
        public int AttackerInstanceId { get; }
        public TargetRef Target { get; }
    }

    public readonly struct UseHeroPowerCommand : IGameCommand
    {
        public UseHeroPowerCommand(int playerId, TargetRef target);
        public int PlayerId { get; }
        public TargetRef Target { get; }
    }

    public readonly struct EndTurnCommand : IGameCommand
    {
        public EndTurnCommand(int playerId);
        public int PlayerId { get; }
    }

    public enum CommandError
    {
        None = 0,
        UnknownCommand,
        NotYourTurn,
        NotEnoughMana,
        BoardFull,
        TargetRequired,
        InvalidTarget,
        CardNotInHand,
        AttackerNotOnBoard,
        SummoningSickness,
        AlreadyAttacked,
        MustTargetTaunt,
        HeroPowerAlreadyUsed
    }

    public readonly struct CommandResult
    {
        public bool IsValid { get; }
        public bool IsInvalid { get; }
        public CommandError Error { get; }
        public string Detail { get; }
        public static CommandResult Valid();
        public static CommandResult Invalid(CommandError error, string? detail = null);
    }
}
```

### 5.1 关键设计决策（偏离 Docs/03 示例，需登记）

**命令引用卡牌/角色一律使用 `InstanceId` / 座位 Id，不使用集合索引。**

- Docs/03 §5.3、§5.6 与 §14.4 的示例使用 `HandIndex`、`AttackerBoardIndex`、`TargetRef.ForMinion(slotIndex)`（集合下标）。
- 本实现改用稳定 Id：出牌用手牌实例的 `InstanceId`、攻击用场上随从 `InstanceId`、目标随从用其 `InstanceId`、目标英雄用玩家座位 Id。
- 依据：① AGENTS 禁止清单"`list[index]` 作为业务 ID → id/key 字典查询"为强制入口规则；② 命令要可序列化/录制/重放（FR-13.6、M4-T9），出牌/移区后集合索引会错位而 `InstanceId` 不变；③ 权威侧按 Id 在分区/字典中 O(1) 查询更稳健。
- 结论：Docs/03 的索引命名视为示意性伪代码，以 AGENTS 强制规则与可重放要求为准；此决策在此登记，供 T6/序列化任务沿用。

### 5.2 其它约定

- 命令为**纯不可变数据容器**：构造函数只赋值、不做业务校验、不抛异常——任何（含越权/伪造/字段非法的）命令都能被构造，语义合法性全部由权威侧 `RuleEngine` 校验后拒绝（铁律 12：全字段校验、非法即拒绝）。这使"伪造越权命令被拒"可在测试中直接构造。
- `CommandResult` 与 Core 的 `Result` 区别：`Result` 是通用成功/失败（字符串错误码），`CommandResult` 是命令校验专用（强类型 `CommandError` 枚举 + 可选 Detail），对应网络层 `CommandAccepted/Rejected`。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 构造四种命令 | `PlayerId` 与各字段（含 Target）按构造值返回；均实现 `IGameCommand` |
| AC-2 | 命令不可变 | 属性无 set/公开可写字段；struct + readonly，只读字段语义 |
| AC-3 | `TargetRef.None` | `Kind=None`、`IsNone=true`、`TargetId=0` |
| AC-4 | `ForMinion(id)` | `Kind=Minion`、`IsNone=false`、`TargetId=id` |
| AC-5 | `ForHero(id)` | `Kind=Hero`、`IsNone=false`、`TargetId=id` |
| AC-6 | 命令携带目标 | PlayCard/Attack/HeroPower 的 `Target` 正确保存（含 None 与角色两种） |
| AC-7 | `CommandResult.Valid()` | `IsValid=true`、`IsInvalid=false`、`Error=None`、`Detail` 为空 |
| AC-8 | `Invalid(error)` | `IsValid=false`、`Error` 等于传入错误码；默认 `Detail` 为空 |
| AC-9 | `Invalid(error, detail)` | `Detail` 正确携带补充说明（失败原因可读） |
| AC-10 | 纯 C# | 全部类型仅依赖 BCL；无 Unity 工具链编译跑通 |

## 7. 测试要求

- 测试类型：EditMode（同时被 `Tools/coverage.ps1` 执行）
- 最少用例数：≥ 20
- 必须覆盖的边界：四种命令各自构造、命令以 `IGameCommand` 引用访问 `PlayerId`、三种 `TargetRef`、`EndTurnCommand` 无目标、Attack 目标可为随从/英雄、CommandResult 有效/多错误码失败/Detail 文本、两个不同 `CommandResult` 的 Error 可区分

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；本任务卡回填结论
- 需更新的配置表：无
- 是否影响既有模块：纯新增 4 文件；不改既有类型（无破坏性）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（AC-1～AC-10；AC-10 由无 Unity 工具链满足）
- [x] 测试通过且覆盖边界（新增 20 例，511/511）
- [x] 编译 0 error / 0 warning（无 Unity 工具链 dotnet build；Unity 批处理验证受 M3-B1 暂缓，见 §10）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（遗留 M3-B1 为 P2 环境问题）
- [x] 涉及文档已同步更新（PROGRESS + 本任务卡）

---

## 10. 六步自检与评审结论（2026-10-05）

### 10.1 先红后绿
- 红：测试引用 `IGameCommand`/`TargetRef`/四个命令/`CommandResult`/`CommandError`/`TargetKind` 时报 CS0246/CS0103（类型不存在）。
- 绿：实现 4 文件后全量通过。

### 10.2 验证证据
| 项 | 结果 |
| --- | --- |
| 无 Unity 工具链 `Tools/coverage.ps1` | **511 passed / 0 failed**（491 + 20） |
| 行覆盖率 | TargetRef **100%**、GameCommands **100%**、CommandResult **100%** |
| 汇总覆盖率 | `0_Core` 96.51%（门禁 90%）、`Domain + App` 93.71%（门禁 80%） |
| 静态门禁 `check.ps1` | PASS（138 文件，+6） |
| .meta / GUID | 新增 6 .meta；Assets 223 个 GUID 无重复 |

### 10.3 铁律与禁止清单自查
- 四新文件仅依赖 BCL（`System`），命名空间 `Card.Domain.Match`；无 `UnityEngine`/`Debug.Log`/`Mathf` 等。
- 命令与结果均为 `readonly struct`（纯数据、无对象引用、属性只读），符合可序列化方向（铁律 11）。
- 单文件 ≤ 100 行、方法短、圈复杂度低；不修改任何既有类型（纯新增）。
- 命令引用一律用 `InstanceId`/座位 Id，落实 AGENTS 禁止清单"list[index] → id/key 查询"。

### 10.4 Unity 批处理验证：暂缓（M3-B1，P2）
- 与 T3 相同：TRAE 沙箱拦截 `bee`/`upm` 等致 `isUpdating` 恒真、`EditorApplication.update` 不执行，`-runTests` 被静默跳过。
- 本轮 6 个新文件的 `.meta` 按 Unity 标准 `MonoImporter` 格式手写、GUID 全仓唯一，Unity 打开工程时可直接识别（无需重新生成 GUID）。
- 经用户既定决策（T3 起跳过 Unity 权威验证以保证进度），T4 以无 Unity 工具链 511/511 与 100% 覆盖率为正确性证据，待 M3-B1 环境修复后统一补跑。

### 10.5 评审结论
- P0：无。P1：无。
- 唯一遗留 M3-B1 为工具链/环境问题（P2），不影响命令模型正确性，不阻塞 T5/T6。
- **结论：M3-T4 通过（Unity 验证暂缓前提下），允许提交并继续 M3-T5。**
