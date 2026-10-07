# 任务卡 · M6-T1 对战场景组装

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M6-T1 |
| 所属里程碑 | M6 垂直切片打通（Demo Gate） |
| 上游需求 | Docs/02 M6-T1（`BattleScene`（新）；从主菜单进入即可开局）；Docs/03 §5.6/R4/R8/U-1/U-2/U-10 |
| 规则依据 | Docs/02 第 198–208 行 |
| 预估 | 1–2 会话 |
| 依赖任务 | M5-T1~T8（全部表现组件 + 只读视图模型）；M2（配置 JSON 生成物） |

## 2. 目标（一句话）

> 新增主菜单场景与对战场景：主菜单点击"开始对战"进入 Battle 场景，场景引导器读 StreamingAssets 配置、用 MatchFactory 开局，代码构建全部 UI 并装配 M5 各组件，首帧即渲染双方英雄/法力/手牌/战场，事件泵驱动后续刷新，可结束回合并看到横幅反馈。

## 3. 范围（做什么）

### 3.1 关键设计决定

1. **不手写 .unity YAML**：场景由 Editor 菜单（`6_Editor`）一键生成，场景内仅一个引导 GameObject；所有 UI 在运行时由 `BattleUiFactory` 代码构建——避免脆弱 YAML 引用与 Awake 时序坑（引导器在 Start 内自包含初始化）。
2. **配置走 StreamingAssets 普通文件**：Editor 菜单把 `Assets/_Project/Config/*.json` 复制到 `Assets/StreamingAssets/CardConfig/`；运行时经既有 `ConfigFileLoader.Load(目录)` 读取。不散落 `Resources.Load`（U-2）、不引入 Addressables（U-10 后续替换点在加载边界之内）。
3. **Bootstrap 作为组合根**：Presentation 对其开放 internal（`InternalsVisibleTo("Card.Bootstrap")`，与测试程序集同模式），避免给每个视图加冗余 Initialize。
4. 本地视角座位 0；结束回合按钮按当前 `ActivePlayerId` 提交（热座雏形，T3 正式化双人切换）。

### 3.2 Presentation 加法（`4_Presentation/Battle/`）

- `CardView`：加 `Button`（点击绑定）+ `BindClick(Action)`/`UnbindClick()`。
- `HandView`/`BoardView`：加 `event Action<int> CardClicked`（携带 InstanceId）；租出时绑定、归还时解绑；加 `bool TryGetCardView(int instanceId, out CardView view)`（供定位器/拾取器按 Id 查）。
- `AssemblyInfo`：+ `InternalsVisibleTo("Card.Bootstrap")`。

### 3.3 Bootstrap（`5_Bootstrap/`）

- `Battle/BattleComposition.cs`（纯 C#）：`LoadDatabase(string dir)`（ConfigFileLoader → CardDatabase，错误列表原样返回）、`BuildDeckKeys(db)`（取启用卡前 Rules.DeckSize 个）、`StartMatch(db, deck, seed)` → MatchController（法师 vs 战士，固定种子）。
- `Battle/BattleUiFactory.cs` + `Battle/BattleUi.cs`：代码构建 Canvas/EventSystem/双方英雄法力/双战场/双手牌/技能与结束回合按钮/日志 ScrollRect/横幅/浮动数字层/箭头；返回引用聚合。
- `Battle/BattleViewSynchronizer.cs`：`Push(IReadOnlyMatchState)`——按只读视图刷新 8 个区视图（对手手牌渲染牌背）+ 英雄/法力；随从按关键词位上嘲讽标记（状态数据渲染，非规则判断）。
- `Battle/UiTargetPicker.cs`（ITargetPicker）：指向确认时从两战场 CardView 矩形 + 双英雄锚点命中出 TargetRef。
- `Battle/TableFeedbackLocator.cs`（IFeedbackTargetLocator）：InstanceId→CardView/世界坐标，座位→英雄锚点。
- `Battle/NullAudioCuePlayer.cs`：IAudioCuePlayer 空实现（音频资源属 M9）。
- `Battle/BattleSceneBootstrap.cs`（MonoBehaviour）：Start 组装并首帧渲染；订阅 泵→日志；Update 泵事件→同步视图、`FloatingTextPool.ReclaimFinished()`；点击黏合：手牌→无目标出牌、被拒 `TargetRequired`→进入出牌指向；己方随从→攻击指向；技能按钮同理；结束回合按钮提交当前行动方。
- `Menu/MainMenuBootstrap.cs`：Canvas + 标题 + "开始对战"按钮 → `SceneManager.LoadScene("Battle")`。

### 3.4 Editor（`6_Editor/M6/M6SceneSetup.cs`）

- 菜单 `Card/M6/部署运行时配置`：复制 6 个 JSON → StreamingAssets/CardConfig 并 Refresh。
- 菜单 `Card/M6/生成场景`：新建保存 `Assets/Scenes/MainMenu.unity`、`Assets/Scenes/Battle.unity`（各挂引导器），写入 EditorBuildSettings（MainMenu 序 0）。

### 3.5 测试（EditMode，≥ 7 例，实际 10 例）

- Presentation `CardClickBindingTests`（6 例）：BindClick 触发；Unbind 后不触发；重绑仅最新一次；Hand 携带 InstanceId；移除归还卡不触发；Board 点击 + TryGetCardView。
- Bootstrap `BattleCompositionTests`（放 `EditMode/Bootstrap/`，4 例）：对真实生成物目录 `Assets/_Project/Config` 加载成功且卡数/英雄数 > 0；BuildDeckKeys 长度 == DeckSize 且全部启用；StartMatch 首回合 1 双方手牌非空；目录缺失返回错误。
- kernel 工程排除 `EditMode/Bootstrap/**`（Bootstrap 含 UnityEngine，同 Presentation 排除口径）。

## 4. 明确不做（防止范围蔓延）

- **不做合法行动枚举/可出牌·可攻击高亮**：需 RuleEngine 查询面扩展，留给 T2/T3 需要时单开任务；T1 只渲染嘲讽状态标记。
- **不做真实美术/音频资源/布局精修**：UI 为功能级灰板布局；美术属 M9。
- **不做正式双人热座切换 UI**（T3）；不做 PlayMode 自动化（T2）。
- **不引入 Addressables/第三方包**；不手写场景 YAML；不改规则三层。

## 5. 接口约定

```csharp
// Presentation 加法
public sealed class CardView : MonoBehaviour {
    public void BindClick(System.Action onClick);
    public void UnbindClick();
}
public sealed class HandView / BoardView {
    public event System.Action<int>? CardClicked;
    public bool TryGetCardView(int instanceId, out CardView view);
}

// Bootstrap
public static class BattleComposition {
    public static (CardDatabase? db, IReadOnlyList<string> errors) LoadDatabase(string configDir);
    public static IReadOnlyList<string> BuildDeckKeys(CardDatabase db);
    public static MatchController StartMatch(CardDatabase db, IReadOnlyList<string> deck, int seed);
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 编辑器执行两个 M6 菜单 | StreamingAssets/CardConfig 六文件就位；Scenes 下两场景生成且在 Build Settings |
| AC-2 | Play MainMenu | 显示标题与"开始对战"按钮，点击进入 Battle 场景 |
| AC-3 | Battle 首帧 | 双方英雄生命/护甲、法力水晶、双方手牌（己方见卡面、对方牌背）、战场空区均渲染；无异常日志 |
| AC-4 | 点结束回合 | 权威接受，回合数 +1，横幅显示"你的回合/对手回合"，法力按规则变化并刷新 |
| AC-5 | 点己方手牌/随从 | 进入对应指向态（箭头跟随鼠标），右键/Esc 取消 |
| AC-6 | 点击测试（EditMode ≥ 7 例） | 全部通过；kernel 748 不变；check.ps1 PASS |
| AC-7 | 配置缺失 | 场景显示明确错误文本，不抛空引用穿透 |

## 7. 测试要求

- EditMode ≥ 7 例；kernel 748 全过（规则三层零改动）；场景人工验收（T2 补 PlayMode 自动化）。
- 边界：点击解绑、池复用卡不重复触发、配置加载失败路径。

## 8. 涉及文档与配置

- 更新：`Docs/PROGRESS.md`（M6-T1）。
- 生成物（用户在编辑器执行菜单后入库）：`Assets/Scenes/MainMenu.unity`、`Battle.unity`、`Assets/StreamingAssets/CardConfig/*.json` 及 .meta。
- 影响模块：Presentation 加法（点击）；新增 Bootstrap 运行时代码与 Editor 工具。

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 `Docs/04` 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

## 10. 待确认问题

- 无。固定种子（20261007）开局为显式选择（演示可重复）；后续可在主菜单加种子/随机选项。

---

## 11. 结论与证据（2026-10-07 收官）

### 11.1 自检与评审

- **编译**：0 error / 0 warning（Unity 编辑器实跑确认）。
- **测试**：无 Unity 工具链 kernel **750 passed / 0 failed**（748 + 2 效果解析新例）；覆盖率 `0_Core 96.52%` / `Domain + App 91.52%`；`check.ps1` PASS（288 文件）。Unity EditMode 用户实跑全绿（基线 920 + 新增 11 例：点击 6 + 装配 4 + HeroView SetName 1；功能验收由用户确认通过）。
- **场景人工验收（用户，2026-10-07）**：AC-1~7 全部通过——两菜单执行、MainMenu→Battle 开局、首帧渲染（双方英雄/法力/手牌/战场/日志）、结束回合（回合标签 +1、横幅、法力刷新）、出牌与攻击指向（箭头自卡牌中心射出、右键/Esc 取消）、Buff 牌指向己方随从结算正确、配置缺失显示错误文本。
- **铁律自查**：R8（EventSystem 走静态令牌 `UiEventSystem`，无 Find）；U-1（Editor 代码全在 `6_Editor`）；U-2（无 `Resources.Load`，字体资产由 Editor 菜单生成并设 TMP 默认）；View 只读（嘲讽标记按 StatusFlags 渲染）；规则三层零改动（`GameEffects` 分隔符对齐属解析格式与配置数据一致化，测试先行）；单文件 ≤ 300 行（`BattleUiFactory` 底层图元拆至 `BattleUiPrimitive`）。

### 11.2 验证中发现并修复的缺陷

1. **效果 DSL 多参数分隔符不一致（M4 遗留）**：配置数据 `BuffEffect:2/2`（斜杠）、解析器按逗号切 → 首次打出 Buff 牌即 `ArgumentException`。定案：DSL 参数分隔符统一为 `/`（CSV 单元格免转义）；[Docs/01 §428](../01-开发需求文档.md) 补规范；`Docs/templates/Cards.example.csv` 清理陈旧语法（`CompositeEffect`/参数内竖线）；补参数数量校验测试 2 例。
2. **场景缺相机**：无相机时画布不清屏，攻击箭头每帧叠加留拖影（用户截图证实）。修复：新增 `SceneCamera`，两场景 Start 时自动创建相机并清屏。
3. **编辑器内 OS 动态字体不可用**：`TMP_FontAsset.CreateFontAsset(Font)` 经 `FontEngine.LoadFontFace(Font)` 在编辑器内必然失败（日志证实四个候选全败）→ 中文全部 □。修复：弃运行时 OS 字体方案，Noto Sans CJK SC 入库（SIL OFL 1.1，含许可证），`Card/M6/2` 菜单自动生成 `UiChineseSDF` 动态字体资产并设 TMP 全局默认；删除运行时 `UiFont.cs`，运行时零字体代码。
4. **CS8602/8603 可空告警**：`TryGetComponent(out Button? )` 改 Unity 惯用 `out Button` 非空写法。

### 11.3 P3 观察（不阻塞）

- 对手回合无行动方为设计内：双人热座 M6-T3，AI 属 M10。
- 卡名/描述为配置占位键（`CARD_002_NA`）：本地化文案 M8；卡面美术与英雄立绘 M9。
- 英雄"头像"为灰板色块 + NameKey 文本（T1 加法，缓解指向定位）。
