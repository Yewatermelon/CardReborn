using System;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// AI 评估策略（M7-T5）：Greedy = 当前贪心行为，Evaluated = 评估函数多维度打分选最优。
    /// </summary>
    public enum AiEvaluationPolicy
    {
        /// <summary>当前 M7-T2 行为：出牌高费优先、攻击先解场后打脸、技能必用。</summary>
        Greedy,

        /// <summary>M7-T5 Hard：出牌/攻击候选枚举后用评估函数打分选最优。</summary>
        Evaluated,
    }

    /// <summary>
    /// AI 难度配置（M7-T5，FR-6.3）：控制 GreedyAiAgent 的决策行为——
    /// 出牌顺序、目标选择、失误率、评估策略与前瞻深度。
    ///
    /// 三档预设：Easy（确定性劣化）/ Normal（当前行为）/ Hard（评估函数增强）。
    /// 默认 GreedyAiAgent 不传 difficulty 时等价 Normal（向后兼容 T4 基线）。
    ///
    /// 规则三层纯 BCL：本类型不引用 UnityEngine、不依赖 Unity 程序集。
    /// </summary>
    public sealed record AiDifficultyProfile(
        string Key = "Normal",
        AiEvaluationPolicy Policy = AiEvaluationPolicy.Greedy,
        bool PlayHighCostFirst = true,
        bool RandomTarget = false,
        float MistakeRate = 0f,
        int SearchDepth = 0)
    {
        /// <summary>Easy：劣化策略——先抽到先出、目标选最后一个、30% 跳过前序候选。</summary>
        public static AiDifficultyProfile Easy { get; } = new(
            Key: "Easy",
            Policy: AiEvaluationPolicy.Greedy,
            PlayHighCostFirst: false,
            RandomTarget: true,
            MistakeRate: 0.3f);

        /// <summary>Normal：M7-T2 贪心行为——高费优先、目标选最优、零跳过。</summary>
        public static AiDifficultyProfile Normal { get; } = new(
            Key: "Normal",
            Policy: AiEvaluationPolicy.Greedy,
            PlayHighCostFirst: true,
            RandomTarget: false,
            MistakeRate: 0f);

        /// <summary>Hard：评估函数——多维度打分选最优候选。</summary>
        public static AiDifficultyProfile Hard { get; } = new(
            Key: "Hard",
            Policy: AiEvaluationPolicy.Evaluated,
            PlayHighCostFirst: true,
            RandomTarget: false,
            MistakeRate: 0f);

        /// <summary>按 Key 字符串返回预设；未知值回退 Normal（关卡配置化与 MainMenu 难度选择通用）。</summary>
        public static AiDifficultyProfile FromKey(string key) => key switch
        {
            "Easy" => Easy,
            "Hard" => Hard,
            _ => Normal,
        };
    }
}
