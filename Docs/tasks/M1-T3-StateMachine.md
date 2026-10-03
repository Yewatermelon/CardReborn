# 任务卡 · M1-T3 `StateMachine<TState>`

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T3 |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-5.1（完整回合状态机：严格流转、非法阶段操作被拒绝且不改变状态）、FR-5.14（规则纯逻辑可测）、FR-13.1/13.3、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.5 节](../03-开发规范文档.md)（状态机规范：显式注册、非法转移不得静默忽略、Enter/Exit 成对）、[03 第 11 节](../03-开发规范文档.md)（失败用返回值表达）、[01 第 3.2 节](../01-开发需求文档.md)（回合阶段表）、[00 第 4.1 节](../00-现状解构与架构再设计.md)（Core 含 `StateMachine<TState>`） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T1（`Result` / `Guard`：非法转移用 `Result.Failure` 表达） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供一个通用状态机：状态与合法转移显式注册，合法转移按"先 Exit 旧、再 Enter 新"的确定顺序执行回调，非法转移返回失败且**不改变状态、不触发任何回调**。

## 3. 范围（做什么）

- `IStateHandler<TState>`：单个状态的 `Enter()` / `Exit()`（阶段内业务由处理器依赖的服务完成，依赖通过构造函数注入——03 §5.7）。
- `StateMachine<TState>`：
  - 构造：`(initialState, initialHandler = null)`；
  - `Register(state, handler = null)`：显式注册状态；重复注册视为配置错误抛 `ArgumentException`；
  - `AllowTransition(from, to)`：显式声明合法转移边；端点未注册抛 `ArgumentException`；重复登记幂等；
  - `TransitionTo(target)`：非法（未注册 / 未声明边）→ 返回 `Result.Failure`，状态与回调均不变；合法 → 先 `Exit(旧)` 再 `Enter(新)`，返回 `Result.Success()`；
  - `CanTransitionTo(target)`：无副作用的可用性查询（供 Application 层，不供 View 判规则）；
  - `Current` / `IsRegistered`。
- 错误码常量：`ErrorUnknownState`、`ErrorIllegalTransition`。
- EditMode 测试覆盖：合法转移、非法转移被拒、回调顺序、自转移、无处理器状态、回调抛异常、失败后状态可恢复、多实例隔离。

## 4. 明确不做（防止范围蔓延）

- **不做** 具体回合流程（`TurnPhase` 枚举、各阶段业务、加水晶/抽牌）：那是 **M4** 的 `TurnStateMachine` + 阶段处理器；本任务只交付 Core 的通用机制，不预设任何玩法枚举。
- **不做** 通配/条件转移（守卫谓词、优先级）：文档未要求；需要时由 Application 层在调用前判断，再用 `CanTransitionTo` 决策。
- **不做** 转移历史记录与回放：事件/日志由 M1-T2（`EventBus`）与 M1-T5（`GameLog`）承担。
- **不做** 线程安全（同上：单线程 tick / 主线程）。
- **不做** 自动回滚：回调抛异常时异常向上传播（不吞异常），不回滚已完成的转移；该行为写入类型注释并被测试锁定。
- **不做** `IStateMachine` 接口抽象：当前只有一个实现，按"需要时再抽象"处理（03 §5.1 组件优于继承，不预设继承层级）。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>单个状态的进入/退出回调。</summary>
    public interface IStateHandler<TState>
    {
        void Enter();
        void Exit();
    }

    /// <summary>通用状态机：状态与合法转移显式注册；非法转移返回失败。</summary>
    public sealed class StateMachine<TState>
    {
        public const string ErrorUnknownState = "ERROR_STATE_UNKNOWN";
        public const string ErrorIllegalTransition = "ERROR_STATE_ILLEGAL_TRANSITION";

        public StateMachine(TState initialState, IStateHandler<TState> initialHandler = null);

        public TState Current { get; }

        public void Register(TState state, IStateHandler<TState> handler = null);
        public void AllowTransition(TState from, TState to);
        public bool IsRegistered(TState state);
        public bool CanTransitionTo(TState target);
        public Result TransitionTo(TState target);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 构造状态机 | `Current == initialState`；传入 `initialHandler` 时该状态已注册 |
| AC-2 | `Register` 注册状态 | `IsRegistered` 为 true；可在其上声明转移边 |
| AC-3 | 重复 `Register` 同一状态 | 抛 `ArgumentException`（配置错误），ParamName 正确 |
| AC-4 | `AllowTransition` 端点未注册 | 抛 `ArgumentException` |
| AC-5 | 合法转移 | 返回 `Result.Success()`，`Current` 更新为目标状态 |
| AC-6 | 合法转移的回调顺序 | **先** `Exit(旧)` **后** `Enter(新)`（顺序被断言） |
| AC-7 | 转移到未注册状态 | `Result.Failure(ErrorUnknownState)`；`Current` 不变；无任何回调 |
| AC-8 | 未声明边的转移 | `Result.Failure(ErrorIllegalTransition)`；`Current` 不变；无任何回调 |
| AC-9 | 未声明边的自转移（A→A） | 同 AC-8 被拒绝；显式声明后允许，且 Exit→Enter 各一次 |
| AC-10 | 目标状态无处理器 | 转移仍成功（不出现空引用） |
| AC-11 | `Exit` 抛异常 | 异常向上传播（不吞）；`Current` 已是目标状态（行为被测试锁定） |
| AC-12 | `Enter` 抛异常 | 同上 |
| AC-13 | `CanTransitionTo` 合法 / 未声明 / 未注册 | 分别 true / false / false，且**不改变** `Current`、不触发回调 |
| AC-14 | 重复 `AllowTransition` 同一条边 | 幂等（不抛），转移仍只执行一次回调 |
| AC-15 | 多步链式流转（含回环） | 顺序正确，`Current` 最终正确 |
| AC-16 | 失败的转移之后 | 状态机仍可正常执行合法转移（无残留污染） |
| AC-17 | 两个状态机实例 | 注册与状态互不影响 |
| AC-18 | 内核解耦 | 不出现 `UnityEngine` / `Debug.Log` / `Mathf` / `DateTime.Now`（`check.ps1` + 独立扫描） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）
- 最少用例数：20
- 必须覆盖的边界：非法转移（未注册 / 未声明边 / 自转移）、失败时状态与回调均不变、回调顺序、回调抛异常、无处理器状态、重复注册/重复声明、链式回环、多实例隔离

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无
- 是否影响既有模块：无（新增文件，复用 `Result`）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 104 passed / 0 failed，其中新增 21 例：`StateMachineTests` 14 + `StateMachineCallbackTests` 7）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-03）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 构造 / 初始状态 | 已满足 | `Ctor_WhenCreated_CurrentIsInitialState`、`Ctor_WhenInitialHandlerOmitted_DoesNotRegisterInitialState` |
| AC-2 注册状态 | 已满足 | `Ctor_WhenInitialHandlerOmitted_DoesNotRegisterInitialState`（注册后 `IsRegistered` 为 true） |
| AC-3 重复注册被拒 | 已满足 | `Register_WhenSameStateTwice_ThrowsArgumentException` |
| AC-4 声明边时端点未注册 | 已满足 | `AllowTransition_WhenFromStateUnregistered_ThrowsArgumentException`、`AllowTransition_WhenToStateUnregistered_ThrowsArgumentException` |
| AC-5 合法转移 | 已满足 | `TransitionTo_WhenAllowed_ReturnsSuccessAndUpdatesCurrent` |
| AC-6 回调顺序（Exit→Enter） | 已满足 | `TransitionTo_WhenAllowed_ExitsPreviousThenEntersNext`、`TransitionTo_WhenChainedWithLoopBack_ReachesFinalState` |
| AC-7 目标状态未注册 | 已满足 | `TransitionTo_WhenTargetNotRegisteredAtAll_ReturnsUnknownStateFailure` |
| AC-8 未声明边 | 已满足 | `TransitionTo_WhenEdgeNotDeclared_ReturnsIllegalTransitionFailure` |
| AC-9 自转移 | 已满足 | `TransitionTo_WhenSelfTransitionNotDeclared_IsRejected`、`TransitionTo_WhenSelfTransitionDeclared_ExitsThenEntersSameState` |
| AC-10 目标无处理器 | 已满足 | `TransitionTo_WhenTargetHasNoHandler_StillSucceeds` |
| AC-11 `Exit` 抛异常 | 已满足 | `TransitionTo_WhenExitThrows_PropagatesAndTargetEnterIsNotCalled` |
| AC-12 `Enter` 抛异常 | 已满足 | `TransitionTo_WhenEnterThrows_PropagatesAndKeepsTargetState` |
| AC-13 `CanTransitionTo` 无副作用 | 已满足 | `CanTransitionTo_ReflectsDeclaredEdgesWithoutSideEffects` |
| AC-14 重复声明边幂等 | 已满足 | `AllowTransition_WhenDeclaredTwice_IsIdempotent` |
| AC-15 链式流转（含回环） | 已满足 | `TransitionTo_WhenChainedWithLoopBack_ReachesFinalState` |
| AC-16 失败后可恢复 | 已满足 | `TransitionTo_WhenFailedThenLegal_RecoversCleanly`、`TransitionTo_WhenHandlerThrowsOnce_SubsequentTransitionsStillWork` |
| AC-17 多实例隔离 | 已满足 | `Machines_WhenTwoInstances_AreIndependent` |
| AC-18 内核零 Unity 依赖 | 已满足 | `check.ps1` R1 PASS + 独立扫描命中 0 |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`、`System.Collections.Generic`；非法转移复用 M1-T1 的 `Result` |
| 4 表现层只读 | 未命中 | 状态机不判规则、不碰表现；`CanTransitionTo` 明确标注供 Application 层使用 |
| 8 禁隐式全局访问 | 未命中 | 无静态状态；实例隔离有测试 |
| 9 新功能带测试 | 未命中 | 21 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `StateMachine.cs` 186 行；最长方法 `TransitionTo` 约 25 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API、无 `DateTime.Now` |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/6/7/12/13） | 不涉及 | 无玩法继承、命令入口、配置数值、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 未注册状态的转移 | 返回 `ErrorUnknownState`，状态与回调均不变 | 一致 | AC-7 |
| 未声明边的转移 | 返回 `ErrorIllegalTransition`，状态与回调均不变 | 一致 | AC-8 / `TransitionTo_WhenIllegal_DoesNotInvokeAnyHandler` |
| 未声明的自转移 | 同样被拒绝（不隐式放行） | 一致 | AC-9 前半 |
| 显式声明的自转移 | Exit→Enter 各一次 | 一致 | AC-9 后半 |
| 目标状态没有处理器 | 仍能转移，不空引用 | 一致 | AC-10 |
| `Exit` 抛异常 | 异常传播、不再执行 `Enter`、`Current` 已指向目标 | 一致 | AC-11 |
| `Enter` 抛异常 | 异常传播、`Current` 已指向目标、后续仍可用 | 一致 | AC-12、AC-16 |
| 失败后再执行合法转移 | 不受污染，回调只执行一次 | 一致 | AC-16 |
| 重复声明同一条边 | 幂等，不产生重复回调 | 一致 | AC-14 |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'IStateHandler<>'/'StateMachine<>' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=104 passed=104 failed=0`；`warning CS` 0 处 |
| 本次新增 | `StateMachineTests` 14 + `StateMachineCallbackTests` 7 = 21 |

### 10.5 影响面

- 新增文件：`0_Core/{StateMachine}.cs`（含 `IStateHandler<TState>`）与 3 个测试文件。
- 改动既有模块：**无**（复用 `Result`、`Guard` 风格约定）。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- 回归风险：低。当前无调用方；M4 的 `TurnStateMachine` 将复用本类，届时需按 [01 第 3.2 节](../01-开发需求文档.md) 的回合阶段表声明边，并补"非法阶段操作不改状态"的规则层测试。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、104 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

无新增待办。遗留项仍为 M1-R1（启用 `<Nullable>`，P2）与 M1-R2（覆盖率统计，M1 门禁要求 Core ≥ 90%），按约定 M1 收尾统一处理。
