# 任务卡 · M3-T3 关键词与状态集合

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M3-T3 |
| 所属里程碑 | M3 领域模型与规则内核 |
| 上游需求 | FR-5.4（召唤失调：冲锋例外）、FR-5.5（攻击流程：冻结/圣盾参与攻击判定）、FR-5.10 间接 |
| 规则依据 | [01 §3.5](../01-开发需求文档.md)（12 关键词与冻结/圣盾语义）、[01 §3.3-2](../01-开发需求文档.md)（召唤失调，除非冲锋）、[01 §3.4-5](../01-开发需求文档.md)（圣盾抵消一次伤害后消失）、[03 §5.9](../03-开发规范文档.md)（纯数据、BCL） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M3-T2（CardInstance 已存在） |

## 2. 目标（一句话）

> 提供运行时可变的 `KeywordSet`（基于配置 `Keyword` flags 的增删查）与 `StatusSet`（召唤失调/冻结/圣盾位标记，圣盾可"消耗一次"），并把二者接入 `CardInstance`，使随从进场即带配置关键词与召唤失调、圣盾抵挡伤害时被消耗。

## 3. 范围（做什么）

- `1_Domain/Match/StatusFlags.cs`：`[Flags] enum StatusFlags { None=0, SummoningSickness=1, Frozen=2, DivineShield=4 }`。
- `1_Domain/Match/KeywordSet.cs`（sealed，可变）：包装配置 `Keyword` flags；`Has/HasAll/HasAny`、`Add/Remove`、`Toggle(flag, enabled)`、`Clear`、`IsEmpty`、`Flags`。
- `1_Domain/Match/StatusSet.cs`（sealed，可变）：同上口径包装 `StatusFlags`；额外提供 **`ConsumeDivineShield()`**：有圣盾 → 移除圣盾并返回 true；无 → false（"圣盾消耗一次"语义，供 M4 伤害结算调用）。
- `CardInstance` 接入：新增 `KeywordSet Keywords`、`StatusSet Statuses`；`FromDefinition` 用定义的 `Keyword` flags 初始化关键词；随从的圣盾关键词同时在 StatusSet 中建立可消耗状态（见 §5 约定）。
- EditMode 测试：两个集合的位标记语义、圣盾消耗幂等、CardInstance 的关键词/状态初始化。

## 4. 明确不做（防止范围蔓延）

- **不做** 沉默（silence）效果：M4；本任务只提供 Clear/Remove 原语。
- **不做** 冻结"跳过一次攻击后自动解除"的时序结算：M4 回合/攻击系统；Frozen 在此只是标记。
- **不做** 召唤失调的自动判定（"有 Charge/Rush 则无失调"）：T5 创建实例时/T6 校验时处理；本任务在 CardInstance 上只建立集合，不强制写 SummoningSickness（避免与冲锋冲突的双源真相）。
- **不做** 攻击次数（风怒 2 次）/ 已攻击标记：M4/T6。
- **不做** 潜行攻击后解除、剧毒致死等关键词结算：M4。
- **不做** 新增配置关键词（枚举保持 12 个不变）。

## 5. 接口约定

```csharp
namespace Card.Domain.Match
{
    [Flags] public enum StatusFlags { None = 0, SummoningSickness = 1, Frozen = 2, DivineShield = 4 }

    public sealed class KeywordSet
    {
        public KeywordSet(Config.Keyword keywords = Config.Keyword.None);
        public Config.Keyword Flags { get; }
        public bool IsEmpty { get; }
        public bool Has(Config.Keyword keyword);       // (Flags & keyword) == keyword
        public bool HasAll(Config.Keyword keywords);
        public bool HasAny(Config.Keyword keywords);
        public void Add(Config.Keyword keyword);
        public void Remove(Config.Keyword keyword);
        public void Toggle(Config.Keyword keyword, bool enabled);
        public void Clear();
    }

    public sealed class StatusSet
    {
        public StatusSet(StatusFlags flags = StatusFlags.None);
        public StatusFlags Flags { get; }
        public bool IsEmpty { get; }
        public bool Has(StatusFlags flag);
        public void Add(StatusFlags flag);
        public void Remove(StatusFlags flag);
        public void Toggle(StatusFlags flag, bool enabled);
        public void Clear();
        public bool ConsumeDivineShield();             // 原子：检测+移除
    }
}
```

CardInstance 新增：`KeywordSet Keywords { get; }`、`StatusSet Statuses { get; }`。
约定：`FromDefinition` 时若定义含 `DivineShield` 关键词，StatusSet 预置 `DivineShield`（关键词=能力来源，状态=可消耗实例；M4 伤害先查状态）；其余状态（召唤失调/冻结）不由工厂写。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 默认/带参构造 KeywordSet | Flags 正确；空集 IsEmpty=true |
| AC-2 | Add 单个/多个 | Has、HasAll、HasAny 结果正确；重复 Add 幂等 |
| AC-3 | Remove / Toggle / Clear | 标记正确移除；Toggle(false) 等同 Remove；Clear 后 IsEmpty |
| AC-4 | StatusSet 增删查 | 三个标记可独立/组合增删，Has 正确 |
| AC-5 | 圣盾消耗 | 有圣盾：ConsumeDivineShield=true 且标记消失；再次=false；无圣盾=false |
| AC-6 | CardInstance 关键词初始化 | 定义含 Taunt\|Charge 的实例 Has(Taunt/Charge) 为真、其余为假；Statuses 默认空 |
| AC-7 | CardInstance 圣盾预置 | 定义含 DivineShield 时 Statuses.Has(DivineShield)=true，且可被 ConsumeDivineShield 消耗 |
| AC-8 | 纯 C# | 无 Unity 依赖；无 Unity 工具链编译跑通 |

## 7. 测试要求

- 测试类型：EditMode（同时被 `Tools/coverage.ps1` 执行）
- 最少用例数：≥ 20
- 必须覆盖的边界：空集合、位组合（3 标记同时存在后逐个移除）、重复 Add、Remove 不存在标记、Toggle 双向、圣盾二次消耗、None 参数语义、法术牌零关键词

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`；本任务卡回填结论
- 需更新的配置表：无
- 是否影响既有模块：`CardInstance` 新增两个只读属性（工厂内部初始化；既有构造测试不受影响）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（AC-1～AC-8；AC-8 由无 Unity 工具链满足）
- [x] 测试通过且覆盖边界（新增 24 例，491/491）
- [x] 编译 0 error / 0 warning（无 Unity 工具链 dotnet build；**Unity 侧批处理验证暂缓，见 §10**）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（遗留 M3-B1 为 P2 环境问题）
- [x] 涉及文档已同步更新（PROGRESS + 本任务卡）

---

## 10. 六步自检与评审结论（2026-10-05）

### 10.1 先红后绿
- 红：新增测试引用 `KeywordSet`/`StatusSet`/`StatusFlags`（CS0246）及 `CardInstance.Keywords/Statuses`（CS1061）时编译失败。
- 绿：实现三类型并接入 `CardInstance` 后全量通过。

### 10.2 验证证据
| 项 | 结果 |
| --- | --- |
| 无 Unity 工具链 `Tools/coverage.ps1` | **491 passed / 0 failed**（+24 新例） |
| 行覆盖率 | KeywordSet **100%**、StatusSet **100%**、CardInstance **100%** |
| 汇总覆盖率 | `0_Core` 96.51%（门禁 90%）、`Domain + App` 93.41%（门禁 80%） |
| 静态门禁 `check.ps1` | PASS（132 文件，+5） |
| 新增 .meta | 5 个（3 实现 + 2 测试） |

### 10.3 铁律与禁止清单自查
- 三新文件与 `CardInstance` 仅依赖 BCL + `Card.Core` + `Card.Domain.Config`（向下），无 `UnityEngine`/`Debug.Log`/`Mathf` 等。
- 状态只存数据（位标记 + 私有 set），无对象引用；单文件均 ≤ 84 行、方法短、圈复杂度低。
- 关键词（配置来源，`Keyword` flags）与状态（运行时结算实例，`StatusFlags`）分离，避免双源真相。

### 10.4 Unity 批处理验证：暂缓（M3-B1，P2）
- 现象：TRAE 内启动 Unity 批处理，进程退出码 0 但不产生结果文件，`Executer` 的 "Executing tests with settings" 标记不出现。
- 用临时诊断探针（验证后已删除）确认：静态构造运行时 `EditorApplication.isUpdating=True`，而 `EditorApplication.update`/`delayCall` 全程不触发——Unity 在 AssetDatabase 更新阶段后被收尾，从未进入编辑器主循环，故 `TestStarter.UpdateWatch` 不执行。
- 关联：沙箱拦截 `C:\Users\28039\AppData\Local\Unity\Caches\bee\trash`、`upm.log`、许可证缓存等；多轮尝试（全新镜像 / 已编译副本 / 有无 `-quit` / 有无沙箱 / `&` 直调 / `-executeMethod`）均未在 TRAE 内解决。
- 决策：经用户同意，**跳过 Unity 权威验证以保证进度**；功能正确性由无 Unity 工具链 491/491 与 100% 覆盖率保证。待在无沙箱、干净 Library 的环境补跑 Unity EditMode，编号 **M3-B1**（P2，登记于 PROGRESS）。

### 10.5 评审结论
- P0：无。P1：无。
- 唯一遗留 M3-B1 为工具链/环境问题（P2），不影响规则正确性，不阻塞后续任务。
- **结论：M3-T3 通过（在 Unity 验证暂缓的前提下），允许提交并继续 M3-T4。**
