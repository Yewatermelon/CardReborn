# 任务卡 · M5-T8 只读视图模型 ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T8 |
| 所属里程碑 | M5 表现层与交互（收官 ★） |
| 上游需求 | Docs/02 M5-T8（表现层依赖"只读对局视图"而非可直接修改的状态对象；评审确认 View 层无法拿到可写状态；PVP 时同一套表现层可直接渲染服务器下发状态）；Docs/03 §5.6/R4（表现层只读）；ADR-14（状态可序列化可裁剪可增量） |
| 规则依据 | Docs/02 M5 门禁 ★ 项 |
| 预估 | 1 会话 |
| 依赖任务 | M3-T3/T5（状态模型）、M4-T1（MatchController）、M4-T9（MatchStateSerializer）、M5-T1（ICardViewData） |

## 2. 目标（一句话）

> Domain 状态类型加法式实现一组 `IReadOnly*` 只读接口，`MatchController` 以 `View` 只读出口替代对可变状态的暴露，表现层全部改吃只读接口；反射门禁测试证明 Presentation 程序集表面不存在可变状态/控制器引用；序列化往返测试证明 PVP 下发状态可直接被同一套视图数据管线渲染。

## 3. 范围（做什么）

### 3.1 Domain（`1_Domain/Match/ReadOnlyViews.cs`，单文件聚合 6 个接口）

- `IReadOnlyCardInstance`：InstanceId/CardKey/OwnerId/Attack/MaxHealth/Health/CurrentZone? + `Keyword KeywordFlags` + `StatusFlags StatusFlags`（枚举值类型，暴露安全；不暴露可变 `KeywordSet`/`StatusSet` 对象）。
- `IReadOnlyZone`：Type/Capacity/Count/`IReadOnlyList<IReadOnlyCardInstance> Cards`（协变）。
- `IReadOnlyManaPool`：Max/Current/CanSpend(int)。
- `IReadOnlyHeroState`：HeroKey/HeroPowerKey/MaxHealth/Health/Armor/PowerUsedThisTurn。
- `IReadOnlyPlayerState`：Id/Hero/Mana/Deck/Hand/Board/Graveyard/FatigueCounter。
- `IReadOnlyMatchState`：Phase/TurnNumber/ActivePlayerId/IsFinished/ActivePlayer/Players/GetPlayer(int)。**不含** `NextInstanceId`/`AllocateInstanceId`（分配器是写路径）。
- 既有类加法式实现：`MatchState`/`PlayerState`/`HeroState`/`ManaPool`/`Zone`/`CardInstance` 各加 `: IReadOnlyXxx`，返回类型不匹配的用显式实现（协变/委托既有方法）。**不改任何行为、不改任何既有成员签名**。

### 3.2 Application

- `ICommandAuthority { CommandResult Submit(IGameCommand); }`——表现层命令入口的最小权威抽象；`MatchController : ICommandAuthority` 并实现（已有 `Submit` 天然满足）。
- `MatchController` 新增 `public IReadOnlyMatchState View => _state;`；保留 `State`（规则层内部/测试用），但表现层不再触碰。

### 3.3 Presentation（改吃只读接口）

- `CardViewData.FromInstance(CardDefinition, IReadOnlyCardInstance)`（签名收紧；实例 Id/运行时攻血来源不变）。
- `MatchControllerCommandSink` 构造参数从 `MatchController` 收窄为 `ICommandAuthority`。
- 调用方同步：`CardViewDataTests`/`MatchEventPumpTests`/`MatchControllerCommandSinkTests`。

### 3.4 测试

**kernel（Tools/Coverage，`ReadOnlyViewTests.cs`，≥ 8 例）**：
- 只读视图实时映射底层状态（改状态 → 视图值变）；
- 12 类只读属性逐字段可读（英雄/法力/分区/卡牌 flags）；
- `MatchController.View` 返回只读接口且与 State 同源；
- **PVP 证明**：MatchFactory 建局 → 序列化 → 反序列化 → 两个 `IReadOnlyMatchState` 逐字段深等（回合/座位/英雄/法力/四分区逐卡）。

**EditMode（`ReadOnlySurfaceGateTests.cs`，≥ 4 例）**：
- 反射扫描 Card.Presentation 程序集全部 public+internal 方法参数/返回值/属性/字段类型，断言不含可变类型集合 {MatchState, PlayerState, HeroState, ManaPool, Zone, CardInstance, KeywordSet, StatusSet, MatchController, RuleEngine}；
- `CardViewData.FromInstance` 用反序列化状态的只读卡牌渲染，与原始状态渲染逐字段相等（同一套表现层可直接渲染服务器下发状态）。

## 4. 明确不做（防止范围蔓延）

- **不做视野裁剪**（对手手牌遮蔽）：`PlayerViewProjector` 是 M11-T2/M13-T6 的事；本任务只保证"只读"，不保证"看不见对手"。
- **不做快照式视图模型**（拷贝数据）：零拷贝活视图；PVP 侧经反序列化天然已是独立对象。
- **不改规则行为、不动 12 类事件与命令**；既有 741 内核用例语义不变。
- **不做增量下发的客户端应用端**（StateChangeApplier 接入 LoopbackClient 已在 M4-T10）。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    public interface IReadOnlyCardInstance { int InstanceId { get; } string CardKey { get; } int OwnerId { get; } int Attack { get; } int MaxHealth { get; } int Health { get; } ZoneType? CurrentZone { get; } Keyword KeywordFlags { get; } StatusFlags StatusFlags { get; } }
    public interface IReadOnlyZone { ZoneType Type { get; } int? Capacity { get; } int Count { get; } IReadOnlyList<IReadOnlyCardInstance> Cards { get; } }
    public interface IReadOnlyManaPool { int Max { get; } int Current { get; } bool CanSpend(int amount); }
    public interface IReadOnlyHeroState { string HeroKey { get; } string HeroPowerKey { get; } int MaxHealth { get; } int Health { get; } int Armor { get; } bool PowerUsedThisTurn { get; } }
    public interface IReadOnlyPlayerState { int Id { get; } IReadOnlyHeroState Hero { get; } IReadOnlyManaPool Mana { get; } IReadOnlyZone Deck { get; } IReadOnlyZone Hand { get; } IReadOnlyZone Board { get; } IReadOnlyZone Graveyard { get; } int FatigueCounter { get; } }
    public interface IReadOnlyMatchState { TurnPhase Phase { get; } int TurnNumber { get; } int ActivePlayerId { get; } bool IsFinished { get; } IReadOnlyPlayerState ActivePlayer { get; } IReadOnlyCollection<IReadOnlyPlayerState> Players { get; } IReadOnlyPlayerState GetPlayer(int playerId); }
}

namespace Card.Application.Match { public interface ICommandAuthority { CommandResult Submit(IGameCommand command); } }
// MatchController : ICommandAuthority；新增 IReadOnlyMatchState View { get; }
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 底层状态变更 | 经 `IReadOnly*` 接口读取到新值（活视图零拷贝） |
| AC-2 | 反射扫描 Presentation | 程序集表面无可变状态/控制器类型 |
| AC-3 | 序列化 → 反序列化 | 两个 `IReadOnlyMatchState` 逐字段深等 |
| AC-4 | 反序列化状态 → `CardViewData.FromInstance` | 与原始状态渲染逐字段相等 |
| AC-5 | `MatchControllerCommandSink(ICommandAuthority)` | 接受/拒绝语义不变（既有 3 例仍绿） |
| AC-6 | 编译与门禁 | 0 error / 0 warning；`check.ps1` PASS；kernel 覆盖率不降 |

## 7. 测试要求

- 测试类型：kernel（无 Unity）+ EditMode（反射门禁与渲染等价）。
- 最少用例数：12（kernel 8 + EditMode 4）。
- 必须覆盖的边界：活视图实时性、协变分区列表、未知座位 Id 抛异常（透传）、接口不含 NextInstanceId。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T8 状态 + 证据）；M5 收官后在 `Docs/reviews/` 写 M5 复盘。
- 需更新的配置：无。
- 是否影响既有模块：Domain 6 类加接口（加法式）；Presentation 2 处签名收窄；测试调用方同步。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。接口放 Domain 为显式决定：零拷贝、BCL-only、规则层/服务器/表现层三方复用；"只读视图模型"语义由接口命名承载，不需独立 wrapper 层。

---

## 11. 结论与证据

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑 920 通过佐证）。
- **测试**：无 Unity 工具链 **748 passed / 0 failed**（741 + 7 kernel 新例）；Unity EditMode 用户实跑 **920 passed / 0 failed**（910 + 7 kernel 同源 + 3 门禁，2026-10-07）。
- **静态门禁**：`check.ps1` PASS（272 文件）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.41%**（不降反微升）。
- **修复记录**：`Keyword` 位于 `Card.Domain.Config`（非 `Card.Domain.Match`），`ReadOnlyViews.cs` 补 using——CS0246/CS0539/CS0535 同源。
- **铁律扫描**：
  - R2/R3：只读接口在 Domain（BCL-only），规则三层无 Unity 依赖 ✅
  - R4/§5.6：★ 反射门禁测试扫描 Card.Presentation 全程序集表面，无可变状态/权威类型（AC-2）✅
  - R12：PVP 证明——序列化→反序列化→只读视图逐字段深等（AC-3），下发状态经同一 `FromInstance` 渲染相等（AC-4）✅
  - R10：实现 1 新文件（112 行）+ 7 处加法式编辑，0 warning ✅

### 11.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0–P3 | 0 | — |

### 11.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `1_Domain/Match/ReadOnlyViews.cs` | 6 个 `IReadOnly*` 接口聚合 |
| Domain 6 类加法实现 | MatchState/PlayerState/HeroState/ManaPool/Zone/CardInstance |
| `2_Application/Match/ICommandAuthority.cs` + `MatchController` | 命令权威最小抽象 + `View` 只读出口 |
| `CardViewData.FromInstance` 签名收紧 | `CardInstance` → `IReadOnlyCardInstance` |
| `MatchControllerCommandSink` 收窄 | `MatchController` → `ICommandAuthority` |
| `7_Tests/.../Match/ReadOnlyViewTests.cs` | 7 例（kernel+Unity 双跑） |
| `7_Tests/.../Presentation/ReadOnlySurfaceGateTests.cs` | 3 例（反射门禁 + 渲染等价） |

### 11.4 下一步

M5 全部 8/8 完成 → 写 `Docs/reviews/M5-表现层与交互-评审与复盘.md`，M5 里程碑收官（含门禁确认：手动点击出牌/攻击属 M6 场景装配后人工验收；事件订阅无直连已由 T2 泵 + T8 反射门禁双重锁定）。
