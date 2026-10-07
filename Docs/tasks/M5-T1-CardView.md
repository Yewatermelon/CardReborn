# 任务卡 · M5-T1 `CardView` + 数据绑定

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M5-T1 |
| 所属里程碑 | M5 表现层与交互 |
| 上游需求 | FR-8.1（卡牌视图只读渲染）、FR-2.3（卡牌详情面板） |
| 规则依据 | [02 M5 任务表](../02-开发计划步骤文档.md)（M5-T1：只读渲染名称/费用/攻血/描述/美术；完成标准：无任何规则判断） |
| 预估 | 1 会话 |
| 依赖任务 | M2（`CardDefinition`/`CardDatabase`）、M4（`MatchState`/`CardInstance`） |

## 2. 目标（一句话）

> 在 `Card.Presentation.Battle` 交付 `CardView`：一个只读 `MonoBehaviour` 组件，通过 `ICardViewData` 纯值契约消费数据，渲染卡牌名称、费用、攻/血、描述与美术 Key；无任何规则判断；PVP 时同一套组件可直接渲染服务器下发的 JSON 反序列化数据。

## 3. 范围（做什么）

### 3.1 新增实现（`Assets/_Project/4_Presentation/Battle/`）

1. **`ICardViewData`** — 只读数据契约（`string`/`int`/`CardType` 等可序列化类型，无领域对象引用）。
2. **`CardViewData`** — 不可变值对象实现，含静态工厂 `FromDefinition(CardDefinition)`。
3. **`CardView`** — `MonoBehaviour`，通过 `SetData(ICardViewData)` 刷新 `TMP_Text` 字段；
   - 随从类型显示攻/血面板，非随从隐藏；
   - 传 `null` 抛 `ArgumentNullException`（`Guard.NotNull`）。

### 3.2 配置更新

- `Card.Tests.EditMode.asmdef`：添加 `Card.Presentation`、`Unity.TextMeshPro` 引用，使测试可访问表现层类型。
- `Tools/Coverage/CardReborn.Kernel.Tests.csproj`：`Exclude` 追加 `EditMode/Presentation/**/*.cs`，避免无 Unity 工具链编译失败。

### 3.3 新增测试（`Assets/_Project/7_Tests/EditMode/Presentation/`）

- `CardViewDataTests`：构造/工厂/边界值（`null` 字段回退空串、负费用/攻血允许——规则校验在 `RuleEngine`，View 只渲染）。
- `CardViewTests`：`SetData` 后 `TMP_Text.text` 断言；随从/法术类型区分；`SetData(null)` 抛异常。

## 4. 明确不做（防止范围蔓延）

- **不做 `HandView`/`BoardView` 布局与增删卡逻辑**（M5-T2）。
- **不做对象池**（M5-T3）；`CardView` 仍由 `Instantiate` 创建。
- **不做点击/拖拽/交互**（M5-T4）。
- **不做动画/特效/悬停放大/音效**（M5-T6）。
- **不做本地化文本解析**：`NameKey`/`DescKey` 直接显示为占位字符串；真正多语言由 M8 或后续决定。
- **不做美术资源加载**：`ArtKey` 仅存储，不调用 `Resources.Load<Sprite>`；`IArtProvider` 由 M6 接入。
- **不做关键词图标/稀有度边框/职业色等视觉修饰**（M9 内容打磨）。
- **不做运行时状态同步**：`CardViewData.FromDefinition` 只从配置构造；`FromInstance`（运行时攻血 buff/伤害）留给 M5-T8 完整视图模型。
- **不改任何规则层（Core/Domain/Application）代码**；既有 741 内核用例零改动。

## 5. 接口约定

```csharp
namespace Card.Presentation.Battle
{
    /// <summary>CardView 的只读数据契约。纯值，可序列化，PVP 可直接反序列化。</summary>
    public interface ICardViewData
    {
        string Name { get; }
        string Description { get; }
        int Cost { get; }
        int Attack { get; }
        int Health { get; }
        string ArtKey { get; }
        CardType Type { get; }
    }

    public sealed class CardViewData : ICardViewData
    {
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public int Attack { get; }
        public int Health { get; }
        public string ArtKey { get; }
        public CardType Type { get; }

        public CardViewData(string name, string description, int cost,
                            int attack, int health, string artKey, CardType type);

        public static CardViewData FromDefinition(CardDefinition definition);
    }

    public sealed class CardView : MonoBehaviour
    {
        // internal + = null!（Unity 序列化字段的 NRT 惯用法，消除 CS8618；internal 供测试注入）
        [SerializeField] internal TMP_Text _nameText = null!;
        [SerializeField] internal TMP_Text _descriptionText = null!;
        [SerializeField] internal TMP_Text _costText = null!;
        [SerializeField] internal TMP_Text _attackText = null!;
        [SerializeField] internal TMP_Text _healthText = null!;
        [SerializeField] internal GameObject _attackPanel = null!;
        [SerializeField] internal GameObject _healthPanel = null!;

        public void SetData(ICardViewData data);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | `CardViewData` 构造 | 字段与输入一致；`null` name/desc/artKey 回退为空串 |
| AC-2 | `FromDefinition` 映射 | 从 `CardDefinition` 构造后各字段正确映射（含 `Type=Minion` 时 Attack/Health 来自配置） |
| AC-3 | `CardView.SetData(minion)` | `_nameText`/`_costText`/`_attackText`/`_healthText` 渲染对应值；`_attackPanel` 与 `_healthPanel` 激活 |
| AC-4 | `CardView.SetData(spell)` | `_nameText`/`_costText` 渲染对应值；`_attackPanel` 与 `_healthPanel` 禁用 |
| AC-5 | `CardView.SetData(null)` | 抛 `ArgumentNullException`，消息含参数名 |
| AC-6 | 编译与静态门禁 | 0 error / 0 warning；`check.ps1` PASS；无 Unity 工具链 741 用例全过 |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode`）。
- 最少用例数：8（`CardViewData` 4 例 + `CardView` 4 例）。
- 必须覆盖的边界：`null` 回退、随从/法术类型区分、异常路径。

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`（M5-T1 状态 + 证据）。
- 需更新的配置：`.asmdef`（测试引用）、`.csproj`（coverage 排除路径）。
- 是否影响既有模块：否（纯新增）。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 10. 结论与证据

### 10.1 自检与评审

- **编译**：0 error / 0 warning（无 Unity 工具链 + Unity 编辑器双侧实跑确认）。
- **测试**：无 Unity 工具链 **741 passed / 0 failed**（新增 9 例 Presentation 测试被 coverage 排除，与 Infrastructure 测试同理）；Unity EditMode 补验 **770 passed / 0 failed**（761 + 9 新例，用户实跑 2026-10-07）。
- **静态门禁**：`check.ps1` PASS（214 文件，+5 新文件）。
- **覆盖率**：0_Core **96.52%** / Domain + App **91.39%**（新增 Presentation 代码不在 coverage 扫描范围内，门禁不受影响）。
- **铁律扫描**：
  - R1（内核解耦）：Core/Domain/App 零改动，Presentation 未混入规则代码 ✅
  - R2（编辑器 API）：无 `UnityEditor` / `AssetDatabase` ✅
  - R3（隐式查找）：无 `Find`/`ServiceLocator`/`static Instance` ✅
  - R4（日志规范）：无 `Debug.Log`；异常路径用 `Guard.NotNull` ✅
  - R5（行数/复杂度）：单文件 ≤ 97 行，单方法 ≤ 15 行，圈复杂度 ≤ 4 ✅
  - R6（asmdef 依赖）：`Card.Presentation` 未引用 `Card.Network`，`Card.Tests.EditMode` 正确添加 `Card.Presentation` + `Unity.TextMeshPro` ✅

### 10.2 评审结论

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0 | 0 | — |
| P1 | 0 | — |
| P2 | 0 | — |
| P3 | 0 | `CardView` 字段为 `internal` 以方便测试注入（`InternalsVisibleTo` 既有声明），Unity 中 `[SerializeField] internal` 是常见做法；后续若需严格封装可改为 `private` + 测试反射，当前按最小影响方案保留。 |

> 修复记录：Unity 侧首次编译报 16 条 CS8618（NRT 非空字段未初始化），已按惯用法改为 `= null!;` 初始化（`CardView` 7 字段 + `CardViewTests` 9 字段）；Unity 复验 0 error / 0 warning 通过。

### 10.3 交付物清单

| 文件 | 说明 |
| --- | --- |
| `Assets/_Project/4_Presentation/Battle/ICardViewData.cs` | 只读数据契约 |
| `Assets/_Project/4_Presentation/Battle/CardViewData.cs` | 不可变值实现 + `FromDefinition` 工厂 |
| `Assets/_Project/4_Presentation/Battle/CardView.cs` | MonoBehaviour 渲染组件 |
| `Assets/_Project/7_Tests/EditMode/Presentation/CardViewDataTests.cs` | 5 例（构造/空回退/工厂/异常/法术类型） |
| `Assets/_Project/7_Tests/EditMode/Presentation/CardViewTests.cs` | 4 例（随从渲染/法术隐藏/空抛异常/覆写） |
| `Assets/_Project/7_Tests/EditMode/Card.Tests.EditMode.asmdef` | 添加 `Card.Presentation`、`Unity.TextMeshPro` 引用 |
| `Tools/Coverage/CardReborn.Kernel.Tests.csproj` | `Exclude` 追加 `Presentation/**/*.cs` |
| `Docs/tasks/M5-T1-CardView.md` | 本任务卡 |

### 10.4 下一步

Unity EditMode 补验 **770 passed / 0 failed** 已完成（2026-10-07）。进入 M5-T2 `HandView`/`BoardView`/`HeroView`/`ManaView`。
