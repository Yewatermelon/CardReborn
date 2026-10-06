# 任务卡 · M3-T9 MatchState 可序列化 ★

> 依据 AGENTS.md 固定工作流与 `rule-task-loop`：先红后绿、双环境验证、自检评审、双提交。
> 注：本任务口头曾称"对局事件与日志"，**以 Docs/02 第 149 行权威定义为准 = 状态可序列化 ★**。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T9 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-13.4（MatchState 完整序列化往返一致；不依赖 JsonUtility）、FR-14.4/14.6（状态下发/重连的数据前提） |
| 规则依据 | Docs/02 第 149 行；Docs/01 第 356 行；Docs/03 §5.9（状态只存数据、可序列化、可裁剪） |
| 预估 | 1 会话 |
| 依赖任务 | M3-T1～T8（完整状态模型、疲劳字段、DrawOutcome） |

## 2. 目标（一句话）

> 提供应用层 `MatchStateSerializer`：把完整 `MatchState`（含双方四分区全部卡牌实例）序列化为 BCL 自定义 JSON 文本，并可反序列化还原为语义一致的新对象，供服务器快照下发、重连与存盘复用。

## 3. 范围（做什么）

1. 新增 `MatchStateSerializer`（静态类 partial 拆分，命名空间 `Card.Application.Match`）：
   - `string Serialize(MatchState state)`
   - `MatchState Deserialize(string json)`
2. JSON 结构（`version: 1`）：
   - 根：version、phase（枚举名字符串）、turnNumber、activePlayerId、isFinished、players[]
   - player：id、fatigueCounter、hero{}、mana{}、zones{deck/hand/board/graveyard}[]
   - hero：heroKey、heroPowerKey、maxHealth、health、armor、powerUsedThisTurn
   - mana：max、current
   - card：instanceId、cardKey、ownerId、attack、maxHealth、health、keywords（flags 整数）、statuses（flags 整数）、attacksUsedThisTurn
   - **不冗余存储 currentZone**：所属分区数组即位置，还原时由 `Zone.Add` 自动设置 `CurrentZone`
3. Domain 层补还原入口：
   - `CardInstance.Restore(...)`：显式反序列化工厂（绕过 FromDefinition，直接还原全部运行时字段）
4. 反序列化容错：
   - JSON 非法/结构不符/枚举名未知/version 不支持 → 抛 `InvalidOperationException` 或 `FormatException`，附字段路径信息；不吞异常、不返回半成品对象。

## 4. 明确不做（防止范围蔓延）

- 不做视野裁剪（对手手牌数量化、牌库顺序隐藏）：阶段二 PlayerViewProjector，本任务产出全量序列化。
- 不做状态增量差异（StateChange）：M3-T10。
- 不做 GameCommand 序列化：M3-T10 之后/阶段二（FR-13.6）。
- 不做事件模型（GameEvent）与事件流水：M4。
- 不做存档落盘/文件 IO：调用方负责；本服务只处理字符串。
- 不做版本迁移（旧版本数据升级）：当前只有 version 1；遇到不支持版本直接抛异常。

## 5. 接口约定

```csharp
namespace Card.Application.Match
{
    public static partial class MatchStateSerializer
    {
        public static string Serialize(MatchState state);
        public static MatchState Deserialize(string json);
    }
}
```

- 序列化确定性：字段顺序固定、JsonValue 2 空格缩进、同一状态两次序列化字符串完全一致。
- 只依赖 `Card.Core`（JsonValue）+ Domain；纯 BCL，无 Unity 引用。
- 反序列化产出全新对象树（与原对象零共享引用）。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 丰富状态（四分区均有卡、多关键词/状态、护甲、疲劳、已攻击次数）往返 | 还原后逐字段语义一致 |
| AC-2 | 分区内卡牌顺序 | 列表顺序完全保留 |
| AC-3 | 同一状态序列化两次 | 字符串完全相同（确定性） |
| AC-4 | 空牌库/空坟场/默认状态 | 正常往返不报错 |
| AC-5 | Card JSON 不冗余存 currentZone | 还原后卡实例 `CurrentZone` 自动指向所属分区 |
| AC-6 | IsFinished=true、英雄 0 血 | 往返保留终局标记 |
| AC-7 | MatchFactory 产出的真实对局（测试 DB） | 序列化→还原后 RuleEngine 对同一命令校验结果一致 |
| AC-8 | 非法 JSON | 抛异常（非静默返回空状态） |
| AC-9 | version 不支持 | 抛异常并说明版本 |
| AC-10 | 还原对象与原对象无共享引用 | 修改还原对象不影响原状态 |

## 7. 测试要求

- 测试类型：EditMode（`7_Tests/EditMode/Match/MatchStateSerializerTests.cs`），无 Unity 工具链同样执行。
- 最少用例数：10（每条 AC 一条）。
- 数据构造：测试内建语义比较器（逐字段比 MatchState/Player/Zone/Card）；AC-7 复用 RuleEngineTestHelpers + MatchFactory + SeededRandomProvider。
- 边界：null 成员、空集合、flags 组合（Taunt|Charge）、0 血英雄。

## 8. 涉及文档与配置

- 需更新的文档：PROGRESS.md、本任务卡。
- 需更新的配置表：无。
- 生产代码改动：
  - 修改 `1_Domain/Match/CardInstance.cs`：+ `Restore(...)` 静态工厂
  - 新增 `2_Application/Match/MatchStateSerializer{,.Write,.Read}.cs`（partial，控制单文件 ≤300 行）
- 是否影响既有模块：`CardInstance` 仅新增方法，不改既有签名（经验 100000219：接口稳定、增量扩展）。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（10 例 EditMode，576/576 通过）
- [x] 测试通过且覆盖边界（四分区/顺序/确定性/null 分区/终局/Factory 真实对局/非法 JSON/版本不符/对象独立性）
- [x] 编译 0 error / 0 warning（初次 NUnit nullable 标注 2 warning 已改 try/catch 清零）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（纯 BCL；状态只存数据；引用全 Id/Key；增量扩展）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS.md + 本任务卡）

## 10. 自检与评审结论（2026-10-06）

### 10.1 红绿验证

- **红**：`CS0234`（MatchStateSerializer 不存在）、`CS0246`（类型缺失）。
- **绿**：576 passed / 0 failed / 0 warning（566 既有 + 10 新增）。
- **覆盖率**：入口文件 100%（8/8）、Write 100%（58/58）、Read 86%（107/125，未覆盖错误类型分支）；汇总 0_Core 96.51% / Domain + App 90.29%。

### 10.2 铁律自查

| 铁律 | 结果 |
| --- | --- |
| 1. 一切皆组件 | ✅ 无继承表达；序列化器为静态类 |
| 2. 依赖向下 | ✅ Application → Domain → Core |
| 3. 规则三层只依赖 BCL | ✅ 无 UnityEngine；不用 JsonUtility（FR-13.4） |
| 6. 数值来自配置 | ✅ 分区容量还原自 RulesConfig 默认值，与真实配置一致 |
| 10. 单文件 ≤300 行 | ✅ 入口 24 行、Write 73 行、Read 180 行 |
| 11. 内核与 Unity 解耦 | ✅ 纯 BCL；无 MonoBehaviour/GameObject/UI 引用，只有 Id/Key/flags |

### 10.3 未关闭问题

- **P0/P1**：无。
- **P2**：M3-B1（Unity 权威验证暂缓，沿用既定决策）。

### 10.4 关键决策

1. **全量快照，不裁剪**：本任务序列化完整状态（含双方手牌明细与牌库顺序）；视野裁剪（PlayerViewProjector）属阶段二，届时在本服务外层包装。
2. **不冗余存 currentZone**：卡牌所属分区数组即位置；还原时 `Zone.Add` 自动设置 `CurrentZone`。
3. **枚举用名字符串、flags 用整数**：phase 等可调试；Keyword/StatusFlags 组合位直接整数往返。
4. **CardInstance.Restore 显式工厂**：只做最小契约校验，允许负数生命等运行期中间状态。
5. **NUnit 坑记录**：`Assert.Throws<T>` 要求精确类型（不匹配派生类）；其可空标注与本工程 nullable 冲突，异常断言改用 try/catch。
6. **经验 100000219 落实**：仅新增方法/文件，CardInstance 与全部既有签名零改动。
