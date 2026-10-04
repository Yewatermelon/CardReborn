# 配置源表（Config/Excel）

> 这些是**源表**，位于 `Assets/` 之外（Unity 不会导入它们）。
> 导入器会把它们转成运行时使用的 `Assets/_Project/Config/*.json`（生成物，**禁止手改**）。
> 转换流程见 [Docs/03 第 9.1 节](../Docs/03-开发规范文档.md)。

## 1. 为什么是 CSV 而不是 .xlsx

决策记录见 [Docs/00 ADR-19](../Docs/00-现状解构与架构再设计.md)。一句话：
**Excel 能直接打开、编辑、另存 CSV**，而导入器读 CSV 只需 BCL，不引入第三方表格库；
代价是没有单元格下拉校验与多 Sheet 说明，由本文件承担说明职责。

## 2. 填写铁律（违反会被导入器拒绝）

1. **第一行必须是表头**，列名与代码契约完全一致（大小写敏感）。
2. **单元格里不能出现逗号**（CSV 用逗号分列，且本格式不支持引号转义）。需要多值时用 `|` 分隔，
   例如关键词列 `Taunt|Charge`、效果列 `Effects` 用 `|` 分隔多个效果。
3. **`Id` 与 `Key` 一经发布不得修改**；要废弃请把 `Enabled` 改为 `FALSE`，不要删行。
4. **枚举列必须写枚举名**（如 `Minion`、`Legendary`），**禁止写数字**（如 `1`）。
5. **不要依赖行序**：代码一律通过 `Id` / `Key` 查询，行顺序可以随便调整。
6. 数值范围与引用完整性由导入器的校验器负责（Docs/01 第 7.3 节第 5 条）。

## 3. 各表列说明

### `Cards.csv`

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 唯一编号，稳定不变 |
| `Key` | string | 稳定英文键（资源与代码引用），如 `MAGE_FIREBALL` |
| `NameKey` / `DescKey` | string | 本地化键（不写中文正文） |
| `Cost` | int | 法力消耗（≥ 0） |
| `Type` | enum | `Minion` / `Spell` / `Weapon`（预留）/ `Hero`（预留） |
| `Rarity` | enum | `Common` / `Rare` / `Epic` / `Legendary` |
| `Class` | enum | `Neutral` / `Mage` / `Warrior` |
| `Attack` / `Health` | int | 随从数值；法术填 0 |
| `Keywords` | 分隔列表 | `Taunt` / `Charge` / `DivineShield` / `Battlecry` / `Deathrattle` / `Windfury` / `Stealth` / `Poisonous` / `Frozen` / `Rush` / `SpellPower` / `Lifesteal`，多个用 `|` 分隔；没有就留空 |
| `TargetRule` | enum | `None` / `Any` / `Enemy` / `Friendly` / `EnemyMinion` / `FriendlyMinion` / `AnyMinion` |
| `Effects` | 分隔列表 | 效果描述串（如 `DamageEffect:6`）；M4 才解析为效果组件 |

> **效果串约定**（M4 解析；导入器只按 `|` 切分，不做语义校验）：
> 单个效果写作 `效果名[:参数1/参数2]`；**多个效果用 `|` 分隔**；**效果内部参数用 `/` 分隔**；
> 复合效果写作 `CompositeEffect:子效果1+子效果2`。
> 例如 `DamageEffect:5/GainArmorEffect:5` 是「一个效果带两个参数」，而 `DestroyEffect|DrawEffect:1` 是「两个效果」；
> **不要**在效果名或参数里再用 `|`（那是多效果分隔符）。
| `SetKey` | string | 所属系列（卡池筛选用） |
| `ArtKey` / `AudioKey` | string | 资源键 |
| `Enabled` | bool | `TRUE` / `FALSE`（也可写 `1` / `0`） |

### `Heroes.csv`

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Id` / `Key` / `NameKey` | — | 同上 |
| `Health` | int | 初始生命（默认 30） |
| `HeroPowerKey` | string | **必须能在 `HeroPowers.csv` 的 `Key` 中找到** |
| `Class` | enum | 同 `Cards.Class` |

### `HeroPowers.csv`

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Id` / `Key` | — | 唯一键，被 `Heroes.HeroPowerKey` 引用 |
| `Cost` | int | 默认 2 |
| `TargetRule` | enum | 同 `Cards.TargetRule` |
| `Effects` | 分隔列表 | 同 `Cards.Effects` |

### `RarityWeights.csv`

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Rarity` | enum | 四个稀有度**都要有一行** |
| `Weight` | int | 相对权重（如 70 / 22 / 6 / 2），必须 > 0 |
| `MinPerPack` | int | 单包至少出现几张（如稀及以上为 1） |

### `GachaConfig.csv`

单行表：`PackSize`（每包张数）、`CoinCost`（单包金币）、`PityCount`（保底包数）、`PityRarity`（保底稀有度，枚举）。

### `Rules.csv`

单行表：`HeroHealth`、`HandLimit`、`BoardLimit`、`ManaLimit`。
**这些数值必须来自本表**，代码里不得硬编码（铁律 6）。

## 4. 常见错误示例

| 写法 | 结果 | 正确写法 |
| --- | --- | --- |
| `Rarity` 列写 `1` | 拒绝：配置表禁止用数字代替枚举 | `Rare` |
| 关键词列写 `Taunt,Charge` | 拒绝：单元格内不能有逗号 | `Taunt\|Charge` |
| `HeroPowerKey` 指向不存在的技能 | 拒绝：外键悬空 | 先在 `HeroPowers.csv` 建行 |
| 删掉废弃卡的行 | 拒绝：破坏 `Id`/`Key` 稳定性 | `Enabled` 改 `FALSE` |

## 5. 导入前会校验什么（M2-T4 已实现）

导入器会在写生成物之前跑一遍校验，**一次列出全部问题**（形如 `Cards.csv 第 3 行 [Cost]：必须是整数，实际为 'abc'`），只要有一条就不产出任何文件。

| 层 | 规则 |
| --- | --- |
| 表 | 六张表都必须存在；`GachaConfig.csv` 与 `Rules.csv` 必须是单行表 |
| 行 | 每个字段都能解析（枚举禁止写数字、整数列必须是整数、`Enabled` 只接受 `TRUE`/`FALSE`/`1`/`0`、关键词/效果用 `\|` 分隔且关键词必须在枚举里） |
| 唯一性 | 同一张表内 `Id` 与 `Key` 不得重复 |
| 外键 | `Heroes.HeroPowerKey` 必须存在于 `HeroPowers.Key` |
| 数值范围 | 卡片费用 ≥ 0 且 ≤ `Rules.ManaLimit`；随从生命 ≥ 1、攻击 ≥ 0；非随从不得有攻击/生命；英雄生命 1..100；英雄技能费用 > 0；`HandLimit ≤ 10`、`BoardLimit ≤ 7`、`ManaLimit ≤ 10`、`HeroHealth` 1..100；稀有度权重 > 0 且四档齐全；抽卡 `PackSize`/`CoinCost`/`PityCount` > 0 |
| 废弃数据 | `Enabled = FALSE` 的卡**只校验可解析性**，数值越界不报错（允许保留废弃内容） |

## 6. 怎么导入

**在 Unity 里**：菜单 `Tools > Card > 导入配置`。成功会提示写出的文件数，失败会弹出**全部**问题清单且不写任何文件。

**无界面（CI / 自动化）**：

```powershell
Unity.exe -batchmode -quit -projectPath <工程根目录> `
  -executeMethod Card.Editor.ConfigPipeline.ConfigImportMenu.ImportForAutomation `
  -logFile <日志路径>
```

生成物落在 `Assets/_Project/Config/`：`cards.json`、`heroes.json`、`hero_powers.json`、`rarity_weights.json`、`gacha.json`、`rules.json`。
每个文件首行是 `"_generated": "由 Tools/Card/导入配置 生成，请勿手改"`——**要改数据请改本目录的 CSV，然后重新导入**。

## 7. 改完数据后怎么让运行时用上

两步（菜单都在 `Tools > Card` 下）：

1. **导入配置**：CSV → JSON 生成物（校验不过就不写文件）；
2. **重载配置**：重新加载生成物并重建卡池，**无需重启**。

运行时侧由 `Card.Domain.Config.ConfigService` 承担：重载成功才换新卡池（新卡立刻可查，`Version` +1），
失败则保留旧卡池并报出全部问题——不会因为一次坏配置把卡池清空。
