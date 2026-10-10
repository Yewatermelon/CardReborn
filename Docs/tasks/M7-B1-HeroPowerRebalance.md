# 任务卡 · M7-B1 英雄技能再平衡（胜率标定）

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-B1 |
| 所属里程碑 | M7（未关闭问题表登记项，P1 调整记录） |
| 上游需求 | Docs/02 M7 门禁"胜率分布落入 [45%, 55%] 或给出调整记录"→ 本任务执行调整；Docs/01 §8 平衡性基线 |
| 规则依据 | M7-T4 批量模拟 500 局实测：P0(Mage) 67.8% vs P1(Warrior) 32.2% |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M7-T4（AiVsAiSimulator 即标定工具） |

## 2. 目标（一句话）

> 调整英雄技能配置使 AI vs AI 500 局基线胜率回到 [45%, 55%]，为 M7-T5 难度曲线标定提供对称基准。

## 3. 范围（做什么）

- **根因**：Mage 技能（2 费任意目标打 1，可点脸）是进攻资源，Warrior（2 费叠甲 2）纯防御；双方 AI/牌库/先后手均对称，技能强度差 → 67.8/32.2 失衡。
- **调整**：`Config/Excel/HeroPowers.csv` 中 `HERO_POWER_ARMOR` 由 `None / GainArmorEffect:2` 改为攻防混合（多效果 `|` 分隔，ConfigRowParser.OptionalList 支持）；同步 Localization 技能文案。
- **标定**：改后跑 `Tools/coverage.ps1`（含 AiVsAiSimulatorTests 500 局门禁）读报告数字，不达区间则迭代数值（备选：纯镜像打 1 / 叠甲 3 / Warrior 血量调整），以最终标定结果为准并记录每次迭代。
- **测试**：500 局门禁测试本身即标定证据（kernel 全量零回归）；如 HeroConfigValidator / ConfigContentTests 对技能有断言则同步修正。

## 4. 明确不做（防止范围蔓延）

- 不做难度分级（M7-T5，本任务完成后再开）
- 不动卡池 / 卡牌数值 / AI 决策权重（失衡根因在技能，AI 与卡组完全对称）
- 不动 MatchEvaluator / RuleEngine / GreedyAiAgent
- 不做正式本地化系统（沿用 Localization.csv 简表同步文案）

## 5. 接口约定

纯配置变更，无代码接口变化；允许范围内同步修正受影响的配置断言测试。

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 500 局标定 | P0 胜率落入 [45%, 55%]（AiVsAiSimulatorTests Gate500 报告数字为准） |
| AC-2 | 零回归 | kernel 全量 + check.ps1 全绿；500 局仍零失败、零被拒、全终局 |
| AC-3 | 文案同步 | Localization.csv 技能描述与实际效果一致 |
| AC-4 | Unity 留证 | EditMode 实跑通过（数字回报） |
| AC-5 | 文档同步 | 任务卡标定记录 + PROGRESS M7-B1 状态更新 |

## 7. 测试要求

- 标定证据：coverage.ps1 的 Gate500 报告（每次迭代数字记入任务卡）
- 受影响既有测试同步修正须说明理由（配置口径变更，非改测试凑绿）

## 8. 涉及文档与配置

- `Config/Excel/HeroPowers.csv`（源表，kernel 测试直接读取）
- `Config/Excel/Localization.csv`（技能文案）
- `Assets/_Project/Config/` JSON 生成物：**不手改**；Unity 编辑器下次导入时同步（运行时实际生效值，需在 AC-4 前由编辑器导入或确认 JSON 同步路径）
- PROGRESS 未关闭问题表 M7-B1 → ✅

## 9. 完成定义（DoD 勾选）

- [ ] 满足全部 AC 且附证据
- [ ] 测试通过且覆盖边界
- [ ] 编译 0 error / 0 warning
- [ ] 通过 `Docs/03` 铁律与禁止清单自查
- [ ] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [ ] 涉及文档已同步更新

---

## 10. 待确认问题

1. **JSON 生成物同步**：kernel 测试读 CSV 源表，但 Unity 运行时读 `Assets/_Project/Config/` JSON（另有 `Assets/StreamingAssets/CardConfig/` 部署副本）——需用户在 Unity 编辑器执行菜单 **`Tools/Card/导入配置`** + **`Card/M6/1. 部署运行时配置到 StreamingAssets`** 同步，否则实机与测试口径不一致。（生成物勿手改）

---

## 11. 标定记录（完成后回填）

迭代均以 `AiVsAiSimulatorTests` Gate500 报告为准（P0 = Mage，500 局零失败零被拒）：

| 迭代 | 配置（HeroPowers.csv） | P0(Mage) 胜率 | 结论 |
| --- | --- | --- | --- |
| 基线 | `HERO_POWER_ARMOR` 2费 None `GainArmorEffect:2` | 67.8% | 失衡起点（M7-T4 实测） |
| 1 | 2费 Any `DamageEffect:1\|GainArmorEffect:1` | 35.0% | 反超（W 65.0%）：+1 甲 ≈ +15% 胜率 |
| 2 | 1费 None `GainArmorEffect:2` | 34.6% | 反超（W 65.4%）：省 1 费 ≈ 同级强增益 |
| 3 | 2费 `GainArmorEffect:2` + Warrior 35 血 | 65.6% | **血量补偿效率极低**（+5 血 ≈ 2%），且碎 `MatchControllerScriptTests` 疲劳硬编码（血量回 30 后自愈） |
| 4 | 3费 Any `DamageEffect:1\|GainArmorEffect:1` | 79.0% | 费用惩罚 > 效果增益（AI 每回合无条件用技能，贵 1 费 = 少 1 法力场面投资） |
| **5 终选** | **`HERO_POWER_BASH` 2费 Any `DamageEffect:1`（镜像）** | **48.4%** | ✅ 落入 [45,55]；51.6% 差值来自种子先手分布，镜像同时验证了模拟器对称性 |

**标定结论**：技能攻防混合档（+1甲）与费用减免档（省1费）均跳过 45–55 区间（直接 65%），离散效果无法微调；血量旋钮效率过低。最小配置改动路径 = 镜像技能对称锚点，职业差异化（防御特色）留 M9 正式平衡基线（4 英雄 / 60 卡）时重新设计。

---

## 12. 任务结论（完成后回填）

**完成日期**：2026-10-10
**结论**：✅ 完成。500 局基线 67.8/32.2 → **48.4/51.6**（[45%,55%] 达标），kernel 837/837 全绿。

### 交付清单

| 类别 | 文件 | 内容 |
| --- | --- | --- |
| 配置 | `Config/Excel/HeroPowers.csv` | `HERO_POWER_ARMOR`（2费叠甲2）→ `HERO_POWER_BASH`（2费任意目标打 1，镜像对称） |
| 配置 | `Config/Excel/Heroes.csv` | `HERO_WARRIOR.HeroPowerKey` → `HERO_POWER_BASH`（血量保持 30） |
| 测试 | `7_Tests/EditMode/Match/MatchFactoryTests.cs` | 技能 Key 断言同步 `HERO_POWER_ARMOR` → `HERO_POWER_BASH`（配置口径变更，非改测试凑绿） |

### 门禁结果

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| 500 局标定 | ✅ 48.4% / 51.6% | Gate500 报告（见标定记录迭代 5），零失败、零被拒、平均回合 24.3 |
| kernel 837/837 | ✅ | coverage.ps1 全绿，覆盖率 `0_Core 96.52%` / `Domain + App 92.96%`（不变） |
| check.ps1 | ✅ PASS | 324 文件 |
| Unity EditMode 实跑留证 | ⏳ | 待用户 Test Runner 回报（预期 1047 不变，仅断言 Key 名变更） |
| JSON 生成物同步 | ⏳ | 待用户编辑器执行 `Tools/Card/导入配置` + `Card/M6/1. 部署运行时配置到 StreamingAssets` |

### 经验沉淀

- **技能费用是最强调平衡旋钮**（AI 每回合无条件用技能）：2 费打 1 与 1 费叠甲 2、3 费打 1+甲 1 之间的胜率摆幅达 44 个百分点。
- **血量补偿效率极低**：+5 血仅 ≈2% 胜率，且会碎真实配置疲劳脚本测试的硬编码终局数字（M4-T2 定位"真实源表同种子可复现"，配置一变即门禁报警——特性而非缺陷）。
- 后续 T5 难度标定可在此基础上做同英雄镜像对局（完全消除英雄偏置）。
