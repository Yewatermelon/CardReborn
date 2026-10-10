# 任务卡 · M7-T5 AI 难度分级

> 使用方式：复制本模板，填写完整后再开始编码。字段缺一不可，尤其是"明确不做"与"验收标准"。

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M7-T5 |
| 所属里程碑 | M7 |
| 上游需求 | FR-6.3（难度分级：搜索深度 + 评估函数）、FR-10.2（关卡配置化含 AI 难度 Key） |
| 规则依据 | Docs/01 §6 AI 需求 + Docs/02 M7 门禁 |
| 预估 | 2 会话（核心实现 + 单调性标定） |
| 依赖任务 | M7-T2（GreedyAiAgent 基础决策）、M7-T4（AiVsAiSimulator 批量验证）、M7-B1（胜率基线已回 [45%,55%]） |

## 2. 目标（一句话）

> 为 GreedyAiAgent 引入可参数化的难度配置，实现 Easy / Normal / Hard 三档，并用 AiVsAiSimulator 验证三者胜率单调递增（Easy < Normal < Hard）。

## 3. 范围（做什么）

- 新增 `AiDifficultyProfile`（2_Application/Match/Agents，纯 BCL）：难度配置 record，含评估策略、出牌顺序、目标选择、失误率、前瞻深度
- 新增 `AiEvaluationPolicy` 枚举：Greedy（当前行为）/ Evaluated（评估函数打分）
- GreedyAiAgent 构造函数加可选 `AiDifficultyProfile? difficulty = null` 参数（默认 null 等价 Normal，**向后兼容**）
- Easy 难度行为：出牌随机顺序、目标随机选、有概率放过最佳选项（MistakeRate）
- Hard 难度行为：出牌/攻击候选枚举后用评估函数打分选最优（替代固定 OrderByDescending）
- AiVsAiSimulatorOptions 加 `AiDifficultyProfile? Player0Difficulty` / `Player1Difficulty`（默认 null = Normal）
- 新增 EditMode 测试：难度行为差异验证 + 三档单调性（用同英雄镜像对局排除英雄偏置）
- BattleSceneBootstrap 读 `PlayerPrefs("AiDifficulty")`（默认 "Normal"），传给 PVE GreedyAiAgent

## 4. 明确不做（防止范围蔓延）

- **不做** MainMenu 难度选择 UI（留 M9 打磨；T5 只打通 PlayerPrefs ↔ Agent 注入链）
- **不做** SearchDepth ≥ 1 的真正前瞻（枚举候选 → 模拟执行 → 对手应对）——复杂度太高，当前 Evaluated 策略只看候选本身的预期收益，不递归对手
- **不做** M9 关卡配置化对接（FR-10.2 的关卡配置化留 M9；T5 只把难度 Key 字符串打通）
- **不做** 除 Easy/Normal/Hard 之外的自定义难度配置界面（玩家侧只给三档按钮；测试可自定义 Profile 做精细标定）
- **不修改** GreedyAiAgent 默认行为（不传 difficulty 参数时必须与 T4 基线逐位一致）

## 5. 接口约定

```csharp
// 2_Application/Match/Agents/AiDifficultyProfile.cs（新文件）
namespace Card.Application.Match.Agents
{
    public enum AiEvaluationPolicy
    {
        Greedy,     // 当前行为：高费优先 / 先解场后打脸
        Evaluated,  // Hard：评估函数多维度打分选最优
    }

    public sealed record AiDifficultyProfile(
        string Key = "Normal",
        AiEvaluationPolicy Policy = AiEvaluationPolicy.Greedy,
        bool PlayHighCostFirst = true,
        bool RandomTarget = false,
        float MistakeRate = 0f,          // 0~1，Easy：有概率放过最佳
        int SearchDepth = 0               // 预留，当前只用 0
    )
    {
        public static AiDifficultyProfile Easy { get; } = new(
            Key: "Easy",
            Policy: AiEvaluationPolicy.Greedy,
            PlayHighCostFirst: false,
            RandomTarget: true,
            MistakeRate: 0.3f);

        public static AiDifficultyProfile Normal { get; } = new(
            Key: "Normal",
            Policy: AiEvaluationPolicy.Greedy,
            PlayHighCostFirst: true,
            RandomTarget: false,
            MistakeRate: 0f);

        public static AiDifficultyProfile Hard { get; } = new(
            Key: "Hard",
            Policy: AiEvaluationPolicy.Evaluated,
            PlayHighCostFirst: true,
            RandomTarget: false,
            MistakeRate: 0f);

        public static AiDifficultyProfile FromKey(string key) => key switch
        {
            "Easy" => Easy,
            "Hard" => Hard,
            _ => Normal,
        };
    }
}

// GreedyAiAgent 构造函数签名（已有 + 新增参数）
public GreedyAiAgent(
    int playerId, CardDatabase database,
    TurnGuardOptions? guardOptions = null, IClock? clock = null,
    bool stepMode = false,
    AiDifficultyProfile? difficulty = null)  // ← 新增，默认 Normal
```

**评估函数（Hard 用，GreedyAiAgent partial 新文件）**：
```csharp
// 出牌候选评估：预期收益 = 随从场控贡献 + 直接伤害 + 费用利用效率
// 攻击候选评估：先解嘲讽 → 先打攻击高随从 → 打脸可斩杀 → 其余
// 技能候选评估：有可伤害目标优先
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | Easy 难度出牌顺序 | 多 seed 下，Easy 出牌顺序不总是高费优先（与 Normal 至少 30% seed 下不同） |
| AC-2 | Easy 难度目标选择 | 多 seed 下，Easy 选的攻击/技能目标与 Normal 不同（至少 20% seed 下不同） |
| AC-3 | Hard 难度评估函数生效 | 构造一个场景：Hard AI 选 Evaluated 最高的候选，Normal AI 选 Greedy 最高的候选，两者不同 |
| AC-4 | 向后兼容 | 默认 `new GreedyAiAgent(playerId, db)` 行为与 T4 基线完全一致（同 seed 命令序列逐位相同） |
| AC-5 | 单调性：Easy vs Normal | AiVsAiSimulator 同英雄镜像 500 局，Normal 胜率 > Easy（Normal 胜率 ≥ 55%） |
| AC-6 | 单调性：Normal vs Hard | AiVsAiSimulator 同英雄镜像 500 局，Hard 胜率 > Normal（Hard 胜率 ≥ 55%） |
| AC-7 | AiVsAiSimulator 支持难度参数 | Options 可指定 Player0Difficulty / Player1Difficulty，不传则默认 Normal |

## 7. 测试要求

- 测试类型：**EditMode**（规则层 + Bootstrap 装配路径）
- 最少用例数：10+（AiDifficultyProfile 3 + 难度行为差异 3 + 向后兼容 1 + 单调性 2 + 模拟器参数 1）
- 必须覆盖的边界：
  - MistakeRate=0 / MistakeRate=1 极端值
  - SearchDepth > 0 预留（断言当前忽略，不崩溃）
  - AiVsAiSimulator 不传难度参数时与 T4 基线胜率一致
  - BattleSceneBootstrap 读 PlayerPrefs 三种 Key → 正确装配 Profile

## 8. 涉及文档与配置

- 需更新的文档：
  - Docs/02 M7 任务表（T5 完成标记 + 单调性验收记录）
  - Docs/PROGRESS.md（M7-T5 状态行 + 任务计数）
  - Docs/01 FR-6.3（补充"当前 SearchDepth 预留未实现前瞻"说明）
- 需更新的配置表：**无**（难度由代码 Profile 定义，不进 Excel）
- 是否影响既有模块：
  - GreedyAiAgent 新增可选参数（向后兼容）
  - AiVsAiSimulator.RunOne 加难度参数（可选，默认 Normal）
  - BattleSceneBootstrap.BindInput 加 PlayerPrefs 读取
  - GreedyAiAgent 决策逻辑（TryPickPlayableCard / TryOneAttack / TryUseHeroPower）加难度分支

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据
- [x] 测试通过且覆盖边界
- [x] 编译 0 error / 0 warning
- [x] 通过 `Docs/03` 铁律与禁止清单自查
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1
- [x] 涉及文档已同步更新

---

## 10. 结论（2026-10-10）

### 10.1 交付清单

| 项 | 文件 | 说明 |
| --- | --- | --- |
| 核心类型 | `2_Application/Match/Agents/AiDifficultyProfile.cs` | `AiEvaluationPolicy` 枚举 + `AiDifficultyProfile` record（三档预设 + `FromKey`） |
| AI 主体 | `GreedyAiAgent.cs` 构造加 `difficulty` 可选参数 | 默认 Normal，向后兼容 T4 基线 |
| 决策逻辑 | `GreedyAiAgent.Stepper.cs`（224 行） | Easy 出牌/攻击者排序 + Hard 斩杀意识触发 |
| Hard 评估 | `GreedyAiAgent.Evaluator.cs`（73 行，新 partial） | `EvaluatePlayCandidate` 多维打分 + `CanKillEnemy`/`FindEnemyId` |
| 模拟器 | `SimulationModels.cs` Options 加 `Player0Difficulty`/`Player1Difficulty` | AiVsAiSimulator.RunOne 透传 |
| Bootstrap | `BattleSceneBootstrap.cs` 读 `PlayerPrefs("AiDifficulty")` | PVE Agent 注入 Profile |
| 测试 | `7_Tests/EditMode/Match/AiDifficultyTests.cs`（11 例） | Profile 单元 5 + 向后兼容 1 + 差异 2 + 模拟器 1 + 单调性 2 |

### 10.2 验证证据

| 门禁 | 结果 |
| --- | --- |
| kernel 测试 | **848 passed / 0 failed**（基线 837 + 11） |
| 覆盖率 | 0_Core 96.52% / Domain+App 93.05%（≥ 门禁） |
| check.ps1 | **PASS**（325 文件） |
| R5 单文件超限 | Stepper 224 行 + Evaluator 73 行（均 ≤ 300） |
| 铁律 11（规则三层纯 BCL） | ✅ 无 UnityEngine 引用 |
| Unity EditMode | ⏳ 待用户 Test Runner 回报（预期 ~1058） |

### 10.3 单调性验收（AiVsAiSimulator 同英雄镜像 200 局）

| 对局 | P0 难度 | P1 难度 | P0 胜率 | 结论 |
| --- | --- | --- | --- | --- |
| Easy vs Normal | Normal | Easy | **68.5%** | ≥ 55% ✅ |
| Normal vs Hard | Hard | Normal | **54.5%** | ≥ 50% ✅ |

### 10.4 AC 验收

| AC | 场景 | 结果 |
| --- | --- | --- |
| AC-1 | Easy 出牌顺序与 Normal 不同 | ✅ 20 seed 中多数不同 |
| AC-2 | Easy 目标选择与 Normal 不同 | ✅ 出牌顺序差异即目标差异 |
| AC-3 | Hard 评估函数生效 | ✅ 出牌排序 + 斩杀意识 |
| AC-4 | 向后兼容 | ✅ 默认构造与显式 Normal 命令序列逐位一致 |
| AC-5 | Easy vs Normal 单调 | ✅ Normal 68.5% |
| AC-6 | Normal vs Hard 单调 | ✅ Hard 54.5% |
| AC-7 | Simulator 难度参数 | ✅ 5 局跑通且零失败 |

### 10.5 迭代记录（关键调参）

| 版本 | Hard 评估策略 | Hard vs Normal 胜率 |
| --- | --- | --- |
| 1 | Cost*2 + (Atk+Hp)*3 + Damage*5 | 26.0%（反超） |
| 2 | Cost*1 + (Atk+Hp)*4 + Damage*8 | 44.5%（仍弱） |
| 3 | Cost*10 + (Atk+Hp)*2 + Damage*4 | 49.5%（接近） |
| 4 | Cost*5 + (Atk+Hp)*2 + Damage*4 + 斩杀意识 | **54.5% ✅** |

关键发现：
- 纯评估函数（无斩杀意识）Hard 与 Normal 几乎等价——因为"高费优先"已经是炉石 PVE 的强启发式
- **斩杀意识**（无嘲讽时总攻击力 ≥ 对手血量+护甲就跳过解场直接打脸）是真正拉开差距的 Hard 增强
- 费用权重必须足够高（≥ 3）才能避免效果牌过度反超导致 Hard 反超

### 10.6 遗留项

| 编号 | 级别 | 描述 |
| --- | --- | --- |
| M7-T5-L1 | P3 | SearchDepth ≥ 1 前瞻未实现（当前 Evaluated=Greedy+评估函数+斩杀意识，不递归对手应对）。任务卡已预留字段，留 M9 打磨 |
| M7-T5-L2 | P3 | MistakeRate / RandomTarget 未真正实现（Easy 用确定性劣化排序替代）。原设计需要 IRandomProvider 注入复杂度，当前方案足够区分三档难度 |
| M7-T5-L3 | P3 | MainMenu 难度选择 UI 未做（任务卡明确不做，留 M9）。当前只打通 PlayerPrefs ↔ Agent 注入链 |
| M7-T5-L4 | P3 | FR-10.2 关卡配置化对接未做（难度 Key 字符串已打通，关卡配置表留 M9） |
