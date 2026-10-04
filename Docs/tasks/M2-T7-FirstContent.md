# 任务卡 · M2-T7 首版配置数据

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M2-T7 |
| 所属里程碑 | M2 配置与数据管线 |
| 上游需求 | FR-1.1（数据驱动）、见 [01 §8 首版内容目标](../01-开发需求文档.md)：卡牌 ≥30（随从 20 + 法术 10）、英雄 2、关键词 5、效果组件 8 |
| 规则依据 | [01 §7.3](../01-开发需求文档.md)（表头/枚举/禁数字/范围校验）、[01 §3.5 / §4.1](../01-开发需求文档.md)（关键词与卡牌字段） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M2-T1…T6（契约、模板、导入器、校验器、卡池、热加载） |

## 2. 目标（一句话）

> 把首版内容真正填进源表：≥30 张可用卡（随从/法术、四档稀有度、五个必备关键词、八种效果组件）、2 个英雄与技能，全部通过校验器，并把"内容达标"写成自动化测试。

## 3. 范围（做什么）

- `Config/Excel/Cards.csv` 扩到 **37 行**（35 启用 + 2 废弃）：随从 23、法术 12；稀有度四档齐全；职业含中立/法师/战士；覆盖 5 个必备关键词（嘲讽/冲锋/圣盾/战吼/亡语）与 8 种效果（`DamageEffect` / `HealEffect` / `GainArmorEffect` / `DrawEffect` / `BuffEffect` / `SummonEffect` / `DestroyEffect` / `CompositeEffect`）。
- `Config/README.md` 补充**效果串约定**（M4 才解析）：`效果名[:参数/参数]`，多个效果用 `|`，复合用 `CompositeEffect:子效果+子效果`。
- 重新导入生成物（走菜单/无界面入口，内部含校验），生成物入库。
- 新增 `ConfigContentTests`（8 例，Domain/Core 侧，可被无 Unity 工具链覆盖）：卡牌数与启用数 ≥30、随从 ≥20 且法术 ≥10、四档稀有度齐全、5 个必备关键词都出现、≥8 种效果、两个英雄都有可查技能、职业池各 ≥5 张、废弃卡不进启用池但仍可按键查到。

## 4. 明确不做（防止范围蔓延）

- **不做** 完整版 60 张卡与 4 个英雄：那是 M9 的内容目标。
- **不做** 关卡/冒险/预设卡组表：`Levels` / `Decks` 属 M9 / M8。
- **不做** 本地化文本：`NameKey`/`DescKey` 仍是键，本地化表（FR-11）尚未建立。
- **不做** 平衡性调优（胜率 45%–55% 的 AI 模拟）：M9 的内容与平衡里程碑。
- **不做** 效果语义校验（如 `SummonEffect` 指向的卡是否存在）：M4 解析效果时再做。

## 5. 接口约定（内容契约）

```text
Config/Excel/Cards.csv      37 行：35 启用 + 2 废弃（Enabled=FALSE）
  · 随从 23（含嘲讽/冲锋/圣盾/战吼/亡语与 8 种效果）
  · 法术 12
  · 稀有度：Common / Rare / Epic / Legendary 全覆盖
  · 职业：Neutral / Mage / Warrior
Config/Excel/Heroes.csv      2 个英雄（法师 / 战士），各指向一个技能
Config/Excel/HeroPowers.csv  2 个技能（2 费）
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 卡牌总量 | `CardCount ≥ 30` 且 `EnabledCardCount ≥ 30` |
| AC-2 | 类型分布 | 启用卡中随从 ≥ 20、法术 ≥ 10 |
| AC-3 | 稀有度 | 四档全部出现 |
| AC-4 | 必备关键词 | `Taunt` / `Charge` / `DivineShield` / `Battlecry` / `Deathrattle` 各至少出现一次 |
| AC-5 | 效果组件 | 至少 8 种不同效果名 |
| AC-6 | 英雄与技能 | 英雄 ≥ 2，且每个英雄的技能都能在技能表里查到 |
| AC-7 | 职业池 | 法师与战士各 ≥ 5 张可用卡 |
| AC-8 | 废弃卡 | 废弃卡不进入 `FilterCards()` 结果，但 `TryGetCard(key)` 仍能查到 |
| AC-9 | 全链路 | 源表 → 校验 → 生成物 → 加载 → 建库 → 查询 全通（导入器 exit=0，加载器与卡池测试通过） |
| AC-10 | 门禁 | 编译 0 error / 0 warning；`check.ps1` PASS；`coverage.ps1` PASS |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Config`，工具链可覆盖）
- 最少用例数：8（内容目标逐条断言）
- 必须覆盖的边界：废弃卡过滤、稀有度/关键词/效果覆盖、职业池规模

## 8. 涉及文档与配置

- 需更新的文档：`Config/README.md`（效果串约定）、`Docs/PROGRESS.md`
- 需更新的配置表：**本任务即产出内容**
- 是否影响既有模块：生成物（`Assets/_Project/Config/*.json`）随内容更新；两条绑定旧条数的测试改为"内容目标"式断言

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10 节）
- [x] 测试通过且覆盖边界（新增 8 例，累计 443 passed / 0 failed）
- [x] 编译 0 error / 0 warning（Unity 清缓存干净重编译）
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新（PROGRESS / Config/README.md）

---

## 10. 证据与评审结论（2026-10-04）

### 10.1 内容与 AC

| 项 | 实测 |
| --- | --- |
| 卡牌行数 | 37（35 启用 + 2 废弃） |
| 随从 / 法术（启用） | 23 / 12 |
| 稀有度 | Common 16 / Rare 9 / Epic 4 / Legendary 3（合计 32 启用，另有 3 张启用的 Expansion 卡） |
| 关键词覆盖 | Taunt / Charge / DivineShield / Battlecry / Deathrattle 全部出现 |
| 效果种类 | DamageEffect / HealEffect / GainArmorEffect / DrawEffect / BuffEffect / SummonEffect / DestroyEffect / CompositeEffect（8 种） |
| 英雄 / 技能 | 2 / 2（法师、战士各一） |
| 生成物 | `cards.json` 17.4 KB（其余 5 份不变或同步更新） |

### 10.2 测试证据

| 阶段 | 结果 |
| --- | --- |
| 导入（无界面入口，含校验） | `exit=0`，无错误输出 |
| Unity 清缓存重编译 | `errors: 0`、`warnings: 0`、`result=Passed total=443 passed=443 failed=0` |
| 无 Unity 工具链 | PASS（含 `ConfigContentTests` 8 例）：`0_Core 96.51%`、`Domain + App 92.36%` |
| 静态门禁 | `Tools/check.ps1` PASS |

> 两条旧测试（`Load_WhenReadingImporterOutput_ReturnsSameContent` 断言 6 张卡、`Load_ThenBuildDatabase_QueriesWork` 断言法师 2 张）因内容扩充而失败，已改为"≥ 内容目标"式断言——内容规模由 `ConfigContentTests` 统一把关，避免每次加卡都要改测试。

### 10.3 评审结论

**通过**：无 P0/P1；门禁全绿；M2 七项任务全部完成（见 [M2 评审与复盘](../reviews/M2-配置与数据管线-评审与复盘.md)）。
