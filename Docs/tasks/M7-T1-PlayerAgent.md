# 任务卡 · M7-T1 IPlayerAgent 统一决策接口

> 使用方式：复制本模板，填写完整后再开始编码。字段缺一不可，尤其是"明确不做"与"验收标准"。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-T1 |
| 所属里程碑 | M7 玩家代理与 AI |
| 上游需求 | FR-6.1（AI 与人类使用同一 `IPlayerAgent` 接口，不允许直接改状态）、FR-6.5（每个命令先经 `RuleEngine` 校验） |
| 规则依据 | Docs/01 §FR-6（L282–290）、Docs/02 M7 任务表 T1；铁律 5（一切操作走 GameCommand）、铁律 12（客户端只上行命令） |
| 预估 | 0.5–1 人日 / 1 会话 |
| 依赖任务 | M4-T2（MatchController/Submit）、M5-T4（ICommandSink 人类输入管线）、M5-T8（IReadOnlyMatchState） |

## 2. 目标（一句话）

> 在规则三层建立"决策者"统一抽象 `IPlayerAgent` + 决策上下文 `IAgentContext` + 回合激活路由 `AgentMatchRunner`，使人类（经 UI 输入）与算法（AI/T2）实现同一接口、产出同类 `IGameCommand` 并都只经 `MatchController.Submit` 权威通道生效。

## 3. 范围（做什么）

- **Application（纯 BCL，kernel 工具链覆盖）**，目录 `Assets/_Project/2_Application/Match/Agents/`：
  - `IPlayerAgent`：座位归属 + 两个生命周期回调（激活/停活），不含任何规则判断。
  - `IAgentContext`：决策时可见/可用的最小表面——`ActivePlayerId`、只读视图 `IReadOnlyMatchState View`、`CommandResult Submit(IGameCommand)`。
  - `AgentMatchRunner`：装配两个座位的 agent；装饰权威入口（实现 `ICommandAuthority`），每次 accepted 提交后按 `View.ActivePlayerId`/`IsFinished` 路由：旧座位 `OnTurnDeactivated` → 新座位 `OnTurnActivated(context)`；终局时停活当前 agent；构造与激活均幂等守卫。
- **Bootstrap（Unity 侧人类实现）**，`Assets/_Project/5_Bootstrap/Battle/HumanPlayerAgent.cs`：
  - `HumanPlayerAgent : IPlayerAgent`（internal）：激活时把现有 `PlayerInputController._localPlayerId` 指向自己的座位（与 `HotSeatHandler.SwitchPerspective` 赋同值，幂等）；不新增任何输入门控（热座交棒屏仍为唯一门控，行为零变化）。
  - `BattleSceneBootstrap.BindInput` 装配改为：`MatchController` → `AgentMatchRunner`（注册座位 0/1 两个 `HumanPlayerAgent`）→ `Start()`；现有 `MatchControllerCommandSink(runner)` 不变（runner 即 `ICommandAuthority`）。
- **测试**：
  - kernel：`7_Tests/EditMode/Match/AgentMatchRunnerTests.cs` + 同目录测试桩 `ScriptedPlayerAgent`（internal，可按脚本队列产命令/可在激活回调内同步跑完整回合，模拟未来 AI 形态）。
  - Unity 侧：`7_Tests/EditMode/Bootstrap/HumanPlayerAgentTests.cs`（MonoBehaviour 输入接线，仅编辑器跑）。

## 4. 明确不做（防止范围蔓延）

- 不写任何 AI 决策算法（出牌优先级、先解场后打脸、技能使用）——**M7-T2**。
- 不做步数/时间上限、无进展检测——**M7-T3**；本任务不防御"恶意/愚蠢 agent 无限提交"，非法命令照常被拒绝，激活路由不含熔断。
- 不做 AI vs AI 批量模拟器、胜率/回合数统计、异常日志汇总——**M7-T4**。
- 不做难度分级与评估函数参数——**M7-T5**。
- 不做 PVE 人机对战场景模式（主菜单 AI 入口、AI 回合输入禁用、跨帧/异步驱动 AI）：M7 门禁（500 局）走无界面模拟器；人机实盘接入待 T2 后按需单开任务（登记为观察项 M7-OBS-1）。
- 不改 `RuleEngine`/`MatchController`/命令模型/事件模型；不引入 `async`/协程/线程（AI 整回合在激活回调内同步完成；跨帧时间切片属 T3 之后）。
- 不接网络层、不改 `HotSeatHandler` 交棒流程与视角切换。

## 5. 接口约定

```csharp
namespace Card.Application.Match.Agents
{
    /// <summary>对局决策者：人类（UI 输入）与 AI（算法）的统一接口（FR-6.1）。
    /// 实现只表达意图，不直接改状态；意图一律为 IGameCommand，经 IAgentContext.Submit 上行。</summary>
    public interface IPlayerAgent
    {
        int PlayerId { get; }

        /// <summary>成为行动方时调用一次；人类实现启用输入，AI 实现可在回调内同步跑完回合。</summary>
        void OnTurnActivated(IAgentContext context);

        /// <summary>行动权转移或对局终局时调用；人类实现停活输入。与激活配对、幂等。</summary>
        void OnTurnDeactivated();
    }

    /// <summary>决策上下文：agent 读取对局与上行命令的唯一通道。View 为零拷贝只读活视图。</summary>
    public interface IAgentContext
    {
        int ActivePlayerId { get; }
        IReadOnlyMatchState View { get; }

        /// <summary>等价于权威 MatchController.Submit：先 RuleEngine 校验再结算；
        /// accepted 后路由回合激活；非法命令原样返回 CommandResult 且状态/激活均不变。</summary>
        CommandResult Submit(IGameCommand command);
    }

    /// <summary>装配并驱动两个座位的 agent。本身是 ICommandAuthority 装饰器：
    /// UI（经 ICommandSink）与 AI（经 IAgentContext）走同一个 Submit。</summary>
    public sealed class AgentMatchRunner : ICommandAuthority, IAgentContext
    {
        public AgentMatchRunner(
            ICommandAuthority authority,
            IReadOnlyMatchState view,
            IReadOnlyList<IPlayerAgent> agents);

        /// <summary>装配校验后激活当前 ActivePlayerId；重复调用不产生重复激活。</summary>
        public void Start();

        /// <summary>权威提交 + 激活路由（accepted 才路由）；返回权威侧原始结果。</summary>
        public CommandResult Submit(IGameCommand command);

        /// <summary>显式重同步激活态（幂等）：供未走本 runner 的外部提交后补路由用。</summary>
        public void Pump();

        public int ActivePlayerId { get; }
        public IReadOnlyMatchState View { get; }
    }
}
```

关键语义（实现必须遵守，测试锁定）：

1. 构造守卫：`authority`/`view`/`agents` null 或 agent null → `ArgumentNullException`；agent 数 ≠ 2、`PlayerId` 重复/不是 {0,1} → `ArgumentException`。
2. 激活序列：以权威结算后的 `ActivePlayerId` 为准；切换顺序固定旧 `OnTurnDeactivated` → 新 `OnTurnActivated`；终局 → 当前 `OnTurnDeactivated`，此后不再激活。
3. Submit 被拒（`CommandResult.IsInvalid`）→ 不 Pump、不改激活、状态零变更（由权威侧保证）。
4. 重入：AI 在 `OnTurnActivated` 内连续 Submit（含 EndTurn → 立即激活对手）必须可用；同一座位不重复回调；agent 抛异常不吞，向上传播。
5. `IAgentContext.Submit` 与直接 `MatchController.Submit` 的权威结果逐位一致（runner 只加路由，不加校验、不改命令）。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 静态门禁 | 新代码位于 `2_Application`/`5_Bootstrap`，规则三层纯 BCL；`check.ps1` PASS（R1 无 UnityEngine、R6 依赖方向） |
| AC-2 | 构造守卫 | null、agent 数 ≠2、座位重复/越界分别抛 ArgumentNull/ArgumentException，且不产生任何激活回调 |
| AC-3 | 开局激活 | `Start()` 后初始行动方恰好收到 1 次 OnTurnActivated；context.ActivePlayerId 与 controller.View 一致、View 同源实时 |
| AC-4 | 四类意图同源 | scripted agent 经 context.Submit 在真实 MatchFactory 对局中打出出牌/攻击/技能/结束回合各 ≥1 条 accepted 命令，状态按既有规则推进 |
| AC-5 | 非法命令 | 非行动方座位、费用不足等命令返回 Invalid（错误码透传），状态零变更、激活不切换、台账记录被拒 |
| AC-6 | 回合路由 | accepted EndTurn 后回调序列 = 旧 deactivate → 新 activate，新座位 View.ActivePlayerId 正确；`Pump()`/二次 `Start()` 幂等无重复回调 |
| AC-7 | 终局停活 | 终局命令后当前 agent 收到 deactivate、无新激活；终局后 Submit 被权威拒绝（NotYourTurn/终局语义） |
| AC-8 | 双 agent 整局 | 两个 scripted agent（固定种子脚本/试错，不复制规则、只认 CommandResult）从开局驱动到终局；命令全部为四种 IGameCommand；同种子两遍激活/终局回合一致 |
| AC-9 | 人类实现走 UI（Unity） | HumanPlayerAgent 激活后 PlayerInputController 座位指向对应 PlayerId，UI 点击经 runner（ICommandAuthority）提交，接受/拒绝透传不变；两 agent 随回合交替激活 |
| AC-10 | 实机不回归（用户手动） | MainMenu→Battle 开局→交棒屏→视角切换→胜负面板→再来一局行为与 M6 一致；EditMode 全绿、PlayMode 3 例全绿 |
| AC-11 | 质量门禁 | 编译 0 error/0 warning；kernel 全量测试通过、覆盖率 90%/80% 门禁不降，新增 runner 代码行覆盖 ≥ 85%；R5（文件 ≤300 行/方法 ≤50 行） |

## 7. 测试要求

- 测试类型：
  - kernel EditMode（无 Unity 工具链口径，进 761+ 总数）：`Match/AgentMatchRunnerTests.cs`，**≥ 14 例**，覆盖 AC-2~AC-8 全部边界 + 重入激活 + Submit 等价性。
  - Unity EditMode（编辑器口径）：`Bootstrap/HumanPlayerAgentTests.cs`，**≥ 4 例**（AC-9）；复用现有 `MatchControllerFixtures`/`MatchTestCards` 建真实对局。
  - PlayMode 不新增套件；既有 3 例端到端冒烟不回归（用户实跑留证）。
- 先红后绿：先写测试确认 CS0246/断言失败，再实现；禁止改测试凑绿。
- 必须覆盖的边界：双 null/空列表/重复座位；被拒命令不路由；终局去激活；激活回调内重入整回合；幂等。
- 新测试套件首次提交前在目标环境实跑留证：kernel 套件由 coverage.ps1 留证；Bootstrap 套件由用户在编辑器 Test Runner 实跑回报（M6 复盘改进项 1：环境覆盖维度）。

## 8. 涉及文档与配置

- 需更新的文档：本卡结论（DoD + 自检证据）；`Docs/PROGRESS.md`（M7 行 0/5→1/5、任务级状态、未关闭问题登记 M7-OBS-1）；HANDOFF §3 快照数字随 M7 收官统一刷新（中途不动）。
- 需更新的配置表：无。
- 是否影响既有模块：`BattleSceneBootstrap.BindInput` 装配点小改（行为等价）；不改 HotSeatHandler/输入组件/规则三层既有类型公开面（runner 为纯加法）。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 11. 完成结论（2026-10-08）

### 交付物

- 生产：`2_Application/Match/Agents/{IPlayerAgent, IAgentContext, AgentMatchRunner}.cs`（纯 BCL，进 kernel）
- Bootstrap：`5_Bootstrap/Battle/HumanPlayerAgent.cs`（internal；激活时路由输入座位，停活空操作）；`BattleSceneBootstrap.BindInput` 装配改为 `AgentMatchRunner` + `runner.Start()`
- 测试：`7_Tests/EditMode/Match/{AgentMatchRunnerTests, AgentMatchRunnerFixtures, ScriptedPlayerAgent}.cs`（16 例）+ `7_Tests/EditMode/Bootstrap/HumanPlayerAgentTests.cs`（6 例）
- 文档：`Docs/03` 新增 U-17（易冲突标识符必须完全限定）

### 验证证据

| 门禁 | 结果 |
| --- | --- |
| 无 Unity 工具链测试 | **779 passed / 0 failed / 0 skipped**（基线 761 + 18 新例） |
| 覆盖率 0_Core | 1166/1208 = **96.52%**（门禁 90%，持平） |
| 覆盖率 Domain+App | 3835/4127 = **92.92%**（门禁 80%，较基线 91.48% ↑） |
| AgentMatchRunner.cs | 65/65 = **100%**（≥85% 达标） |
| `check.ps1` | **PASS**（306 文件 / 10 asmdef，0 违规） |
| 用户编辑器 EditMode | **982 passed / 0 failed**（2026-10-08 实跑回报） |
| 用户编辑器 PlayMode | **3 passed / 0 failed**（2026-10-08 实跑回报） |
| MainMenu→Battle 热座实机 | 行为与 M6 一致（2026-10-08 人工验收） |

### 提交

- 代码：`a9a7b81` feat(application+bootstrap): M7-T1 ...（19 文件，+1123/−4）
- 文档：（本卡 + PROGRESS + Docs/03 U-17）随后一个 `docs(m7)` 提交

### 已知观察项

- **M7-OBS-1**（P3）：PVE 人机实盘场景不在本任务；M7 门禁 500 局走无界面模拟器，人机实盘接入待 T2 后按需单开任务。

---

## 10. 待确认问题（开工前）

1. 接口命名：现拟 `OnTurnActivated/OnTurnDeactivated`（避免与 `TurnStartedEvent` 混淆，语义是"决策权"而非阶段事件）。可否？
2. `AgentMatchRunner` 同时实现 `ICommandAuthority`（装饰器）与 `IAgentContext`，以零新增 sink 类型接入现有 UI 管线。是否认可该取舍？
3. PVE 人机实盘场景明确不在 M7-T1（列入 M7-OBS-1），M7 验收以模拟器为准——与 Docs/02 M7 门禁一致，请确认。
