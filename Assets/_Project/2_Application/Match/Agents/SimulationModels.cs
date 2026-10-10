using System.Collections.Generic;
using System.Text;
using Card.Domain.Match;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// M7-T4 批量模拟选项：局数、种子基值、单局提交硬上限（防死循环）、回合守卫参数透传。
    /// </summary>
    public sealed class AiVsAiSimulatorOptions
    {
        /// <summary>批量局数。默认 500（M7 门禁口径）。</summary>
        public int MatchCount { get; init; } = 500;

        /// <summary>种子基值：第 i 局种子 = BaseSeed + i（确定性、可复现）。</summary>
        public int BaseSeed { get; init; } = 1;

        /// <summary>单局提交硬上限：超过即中止该局记失败（TurnGuard 之外的批量级死循环保险）。</summary>
        public int MaxSubmissionsPerMatch { get; init; } = 2000;

        /// <summary>透传给两侧 GreedyAiAgent 的回合守卫参数；null = TurnGuard 内置默认。</summary>
        public TurnGuardOptions? GuardOptions { get; init; }
    }

    /// <summary>单局结果记录：<see cref="Failure"/> 非 null 表示异常/超限局（Result 保持 Ongoing）。</summary>
    public sealed class AiVsAiMatchRecord
    {
        public int Seed { get; init; }

        public MatchResult Result { get; init; }

        /// <summary>获胜座位；平局为 null。</summary>
        public int? WinnerId { get; init; }

        public int TurnNumber { get; init; }

        public int SubmissionCount { get; init; }

        public int InvalidCount { get; init; }

        /// <summary>失败原因（异常类型+消息或超限说明）；null = 正常完成。</summary>
        public string? Failure { get; init; }

        public bool Succeeded => Failure == null;
    }

    /// <summary>
    /// 批量报告（M7-T4）：胜负统计 + 平均回合数 + 异常清单。
    /// 守恒：Total = P0Wins + P1Wins + Draws + Failures。
    /// 胜率与平均回合只按"完成局"（Total - Failures）计。
    /// </summary>
    public sealed class AiVsAiSimulationReport
    {
        public AiVsAiSimulationReport(IReadOnlyList<AiVsAiMatchRecord> matches)
        {
            Matches = matches;
            Total = matches.Count;
            foreach (AiVsAiMatchRecord record in matches)
            {
                if (!record.Succeeded)
                {
                    Failures++;
                    continue;
                }

                switch (record.Result)
                {
                    case MatchResult.Player0Wins: P0Wins++; break;
                    case MatchResult.Player1Wins: P1Wins++; break;
                    default: Draws++; break;
                }

                TurnSum += record.TurnNumber;
            }

            int completed = Total - Failures;
            AverageTurns = completed > 0 ? (double)TurnSum / completed : 0;
            Player0WinRate = completed > 0 ? (double)P0Wins / completed : 0;
        }

        public IReadOnlyList<AiVsAiMatchRecord> Matches { get; }

        public int Total { get; }

        public int P0Wins { get; }

        public int P1Wins { get; }

        public int Draws { get; }

        public int Failures { get; }

        /// <summary>完成局平均回合数；无完成局为 0。</summary>
        public double AverageTurns { get; }

        /// <summary>P0 胜率（P0Wins / 完成局数）；无完成局为 0。</summary>
        public double Player0WinRate { get; }

        private int TurnSum { get; }

        /// <summary>人可读统计摘要：胜率、平均回合、异常日志逐条列出。</summary>
        public string Summary()
        {
            StringBuilder builder = new StringBuilder();
            int completed = Total - Failures;
            builder.Append("AI vs AI 模拟报告：共 ").Append(Total).AppendLine(" 局");
            builder.Append("P0 胜率：").Append(Percent(Player0WinRate))
                .Append("（").Append(P0Wins).Append('/').Append(completed).AppendLine("）");
            builder.Append("P1 胜率：").Append(Percent(completed > 0 ? (double)P1Wins / completed : 0))
                .Append("（").Append(P1Wins).Append('/').Append(completed).AppendLine("）");
            builder.Append("平局：").Append(Draws).Append("；失败：").AppendLine(Failures.ToString());
            builder.Append("平均回合数：").Append(AverageTurns.ToString("F1", InvariantCulture)).AppendLine("（完成局口径）");
            builder.AppendLine("异常日志：");
            bool hasFailure = false;
            foreach (AiVsAiMatchRecord record in Matches)
            {
                if (record.Succeeded)
                {
                    continue;
                }

                hasFailure = true;
                builder.Append("  seed ").Append(record.Seed).Append(": ").AppendLine(record.Failure);
            }

            if (!hasFailure)
            {
                builder.AppendLine("  （无）");
            }

            return builder.ToString();
        }

        private static string Percent(double value)
        {
            return (value * 100).ToString("F1", InvariantCulture) + "%";
        }

        private static System.Globalization.CultureInfo InvariantCulture =>
            System.Globalization.CultureInfo.InvariantCulture;
    }
}
