# 任务卡 · M6-T4 缺陷清理

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M6-T4 |
| 所属里程碑 | M6 垂直切片打通（Demo Gate） |
| 上游需求 | Docs/02 M6-T4：产出缺陷清单；无 P0 遗留、P1 有明确排期；M6 门禁 = 不借助调试手段完整打完一局 |
| 依赖任务 | M6-T3（缺陷 M6-B1/B2/B3 来源） |

## 2. 目标

> 全量缺陷分诊后，修掉挡 Demo Gate 的最小集：B2（卡面显示本地化 Key → 中文文本）、B1（费用与卡名重叠）。B3（命令拒绝提示）排到本任务后半程或 M9；历史 P3 维持原排期。

## 3. 本轮范围（B2 + B1）

### 3.1 B2：最小中文文本表（不做正式本地化框架）
- 新增 `Config/Excel/Localization.csv`（表头 `Key,ZhCn`）：38 张卡的 NAME/DESC + 2 个英雄名 = 78 条。
- M6SceneSetup「部署运行时配置」额外把该 CSV 复制到 `StreamingAssets/CardConfig/localization.csv`。
- Presentation 新增 `ITextResolver`（key→文本，找不到回退 key 本身）+ `KeyPassthroughTextResolver`（测试/无文本环境）。
- Bootstrap 新增 `CsvTextResolver`：用 `CsvTable` 解析 CSV 文本为字典（纯 C# 可 EditMode 测试，文件读取留在 Bootstrap  MonoBehaviour）。
- `CardViewData.FromDefinition/FromInstance` 增加 `ITextResolver` 参数；`BattleViewSynchronizer` 持 resolver，英雄名同样翻译。
- `BattleSceneBootstrap` 从 StreamingAssets 读 CSV 构造 resolver；文件缺失/解析失败时回退 passthrough（不阻断开局，GameLog.Warn）。

### 3.2 B1：卡牌预制件布局修复
- `BattleUiPrefabs.BuildCardPrefab`：费用增加底板色块；名字框右移避让费用（不再重叠）。
- 纯布局调整，无逻辑改动。

## 4. 明确不做

- **不做正式本地化系统**：多语言切换、语言代码、标准化文本目录、CSV 导入器/校验器接入 M2 管线——全部 M8。本轮的 CSV 直读方案 M8 整体替换。
- **不承诺未实现机制**：潜行（025 刺客）、法术强度（030 大法师）规则层未结算，两张卡 DESC 留空，不写效果文案；登记为 M6-B4。
- **B3（命令拒绝提示）本轮不做**：保留 🔴 待处理，争取 M6-T4 后半程；若排不下排 M9，需在 PROGRESS 写明。
- 不改规则三层、不做美术资源替换（灰板仍是色块）。

## 5. 验收标准

| 编号 | 场景 | 期望 |
| --- | --- | --- |
| AC-1 | MainMenu→Play 开局 | 手牌/战场卡牌显示中文卡名与描述，不再出现 CARD_xxx_KEY |
| AC-2 | 双方英雄 | 头像旁显示中文名（法师/战士） |
| AC-3 | 缺本地化文件 | 开局不崩，回退显示 key，Console 有 Warn |
| AC-4 | 手牌卡面 | 费用数字（带底板）与卡名互不重叠，小尺寸下可读 |
| AC-5 | 未翻译 key | 回退显示 key 本身（不空白、不异常） |
| AC-6 | 门禁 | check.ps1 PASS；coverage.ps1 kernel 数不下降；Unity EditMode 全绿、0 warning |

## 6. 测试要求

- `CsvTextResolverTests`（EditMode/Bootstrap）：正常解析、缺列容错、重复 key 后写覆盖、找不到回退 key、空文本不崩。
- `CardViewDataTests` 等受签名影响的既有测试改注入测试 resolver，断言翻译文本。
- 布局无自动化测试，人工验收 AC-4。

## 7. DoD

- [x] 满足全部 AC 且附证据
- [x] 编译 0 error / 0 warning
- [x] check.ps1 + coverage.ps1 通过
- [x] 缺陷清单（含 B3 与历史 P3 排期）在 PROGRESS 保持最新

## 8. 结论与证据（M6-T4 收官）

### 8.1 范围内完成项（B1 + B2）

- **B2 本地化文本表**：新建 `Config/Excel/Localization.csv`（38 张卡 + 2 英雄共 78 条中文文本，
  描述严格对齐已实现机制；潜行/法术强度 DESC 留空）；`Assets/StreamingAssets/CardConfig/localization.csv` 部署副本入库。
  Presentation 新增 `ITextResolver`/`KeyPassthroughTextResolver`；Bootstrap 新增 `CsvTextResolver`
  （CsvTable 解析，缺 key 回退、重复 key 后写覆盖）；`CardViewData.FromDefinition/FromInstance`、
  `BattleViewSynchronizer.PushPlayer` 全部经 resolver 翻译卡名/描述/英雄名；`BattleSceneBootstrap.LoadTextResolver`
  文件缺失/损坏回退 passthrough 并 Warn；`M6SceneSetup.DeployRuntimeConfig` 同步复制 CSV。
- **B1 卡牌布局**：`BattleUiPrefabs.BuildCardPrefab` 费用独立底板（左上 30×28）+ 名字右移（13,62 宽 74），
  两框不再重叠。

### 8.2 范围外但本轮顺手修掉（用户实机发现的 P1 关键 bug）

- **B5 召唤失调漏写**：`PlayCardSettler` 把无 Charge/Rush 的随从加入战场时未设置 `SummoningSickness`，
  导致所有随从登场回合即可攻击。`PlayCardSettler.Settle` 在 `Board.Add` 后按 `Keywords.Charge/Rush` 判定加状态。
  集成测试 `PlayCardSummoningSicknessTests` 验证 OnPlay→OnPlay 攻击应被拒绝、Charge 随从不受限。
- **B6 亡语配置误用 OnPlay**：`Cards.csv` CARD_029 Effects 列原为 `SummonEffect:NEUTRAL_PANGO/2`（无 OnDeath 前缀），
  按解析器默认走 OnPlay → 古树守卫变成战吼召唤。CSV/JSON 三处统一改为 `OnDeath:SummonEffect:NEUTRAL_PANGO/2`。
- **B7 隐形卡**：`CardView.SetData` 复用池化 view 时未复位 `CardFadeOutView` 与 `CanvasGroup.alpha`，
  导致死亡淡出中的 view 被复用为新卡时 alpha 仍渐减到 0 → 新卡不可见但仍接收点击触发攻击。
  `SetData` 末尾补 `fade.Stop()` + `group.alpha = 1f`。`CardViewTests.SetData_AfterDeathFade_StopsFadeAndRestoresAlpha` 防回归。
  热座切换双方视角不一致很可能由本 bug 引发（某次 Push 复用 view 时未复位，alpha 渐减的卡在一方视角不可见、另一方仍可见）；
  EditMode `HotSeatSynchronizerTests` 已证明 Synchronizer 逻辑本身正确，故 #1 视角不一致不单独登记。

### 8.3 本轮新增测试（共 16 例）

- `CsvTextResolverTests` ×9（解析、缺 key 回退、重复 key 后写覆盖、BOM、缺列抛错等）
- `CardViewDataTests` +3（FromDefinition/FromInstance 注入 resolver、null 守卫、翻译断言）
- `PlayCardSummoningSicknessTests` ×2（无 Charge 不能攻击、Charge 可攻击）
- `HotSeatSynchronizerTests` ×1（SwitchSeats 后 Push 双方战场正确互换）
- `CardViewTests` +1（SetData 后死亡淡出被中止、alpha 复位）

### 8.4 验证证据

- check.ps1：**PASS**（298 文件）
- coverage.ps1（kernel 口径）：**761 passed / 0 failed**，覆盖率 0_Core 96.52% / Domain+App 91.47%
- Unity EditMode：用户确认全绿（含 958 例：942 + 9 CsvTextResolver + 3 CardViewData + 2 SummoningSickness + 1 HotSeatSynchronizer + 1 CardView B7）
- Unity PlayMode：3 passed
- 实机 AC-1~6 验收通过：中文卡面/英雄名、费用不重叠、无 Charge 不能登场攻击、古树守卫死亡时召唤、隐形卡消失、热座切换双方视角一致

### 8.5 遗留项登记

| 编号 | 描述 | 级别 | 排期 |
| --- | --- | --- | --- |
| M6-B3 | 命令被拒绝（嘲讽等）无 UI 提示 | P2 | M9 |
| M6-B4 | 潜行/法术强度规则层未结算，025/030 DESC 留空 | P2 | M9 或后续 |
| M6-B8 | 死亡淡出在卡数不变复用时被 SetData 中止，无死亡视觉动画 | P3 | M9 打磨 |
| M4-OBS-1/2、M2-R1/R3、M0-R2 | 历史 P3 | P3 | 维持原排期 |
