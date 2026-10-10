using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 单局提交数超限异常（M7-T4 批量级防死循环硬保险）：由 <see cref="CountingAuthority"/> 抛出，
    /// 模拟器捕获后记失败局并继续批量——不中断、不吞日志（原因进报告异常清单）。
    /// </summary>
    public sealed class SimulationAbortException : Exception
    {
        public SimulationAbortException(string message) : base(message) { }
    }

    /// <summary>提交计数装饰器：统计提交总数与被拒数；提交数超上限即中止本局。</summary>
    internal sealed class CountingAuthority : ICommandAuthority
    {
        private readonly ICommandAuthority _inner;
        private readonly int _maxSubmissions;

        public CountingAuthority(ICommandAuthority inner, int maxSubmissions)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
            if (maxSubmissions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSubmissions), "提交上限必须为正数。");
            }

            _maxSubmissions = maxSubmissions;
        }

        public int SubmissionCount { get; private set; }

        public int InvalidCount { get; private set; }

        public CommandResult Submit(IGameCommand command)
        {
            SubmissionCount++;
            if (SubmissionCount > _maxSubmissions)
            {
                throw new SimulationAbortException(
                    "提交数超过上限 " + _maxSubmissions + "，疑似死循环，中止本局。");
            }

            CommandResult result = _inner.Submit(command);
            if (result.IsInvalid)
            {
                InvalidCount++;
            }

            return result;
        }
    }

    /// <summary>
    /// AI vs AI 批量模拟器（M7-T4，M7 门禁工具）：固定种子逐局装配双 <see cref="GreedyAiAgent"/>，
    /// 单次调用栈同步跑完整局（<see cref="AgentMatchRunner.Start"/>，与真实对局同一条命令路径），
    /// 统计胜率、平均回合数与异常日志。单局异常/超限记录后继续下一局（批量容错的明确失败路径）。
    /// </summary>
    public static class AiVsAiSimulator
    {
        public static AiVsAiSimulationReport Run(
            CardDatabase database,
            MatchSetupRequest setup0, MatchSetupRequest setup1,
            AiVsAiSimulatorOptions options)
        {
            Guard.NotNull(database, nameof(database));
            Guard.NotNull(setup0, nameof(setup0));
            Guard.NotNull(setup1, nameof(setup1));
            Guard.NotNull(options, nameof(options));
            if (options.MatchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MatchCount 必须为正数。");
            }

            if (options.MaxSubmissionsPerMatch <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MaxSubmissionsPerMatch 必须为正数。");
            }

            List<AiVsAiMatchRecord> matches = new List<AiVsAiMatchRecord>(options.MatchCount);
            for (int i = 0; i < options.MatchCount; i++)
            {
                matches.Add(RunOne(database, setup0, setup1, options.BaseSeed + i, options));
            }

            return new AiVsAiSimulationReport(matches);
        }

        private static AiVsAiMatchRecord RunOne(
            CardDatabase database,
            MatchSetupRequest setup0, MatchSetupRequest setup1,
            int seed, AiVsAiSimulatorOptions options)
        {
            CountingAuthority? counter = null;
            try
            {
                MatchState state = MatchFactory.Create(
                    database, setup0, setup1, new SeededRandomProvider(seed));
                MatchController controller = new MatchController(state, database);
                counter = new CountingAuthority(controller, options.MaxSubmissionsPerMatch);
                IPlayerAgent[] agents =
                {
                    new GreedyAiAgent(0, database, options.GuardOptions),
                    new GreedyAiAgent(1, database, options.GuardOptions),
                };
                AgentMatchRunner runner = new AgentMatchRunner(counter, controller.View, agents);
                runner.Start();
                return FinishRecord(seed, controller.LastOutcome, counter);
            }
            catch (SimulationAbortException ex)
            {
                return AbortedRecord(seed, counter, ex.Message);
            }
            catch (Exception ex)
            {
                return AbortedRecord(seed, counter, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static AiVsAiMatchRecord FinishRecord(
            int seed, MatchOutcome? outcome, CountingAuthority counter)
        {
            if (outcome == null || outcome.Result == MatchResult.Ongoing)
            {
                return new AiVsAiMatchRecord
                {
                    Seed = seed,
                    SubmissionCount = counter.SubmissionCount,
                    InvalidCount = counter.InvalidCount,
                    Failure = "对局未终局。",
                };
            }

            return new AiVsAiMatchRecord
            {
                Seed = seed,
                Result = outcome.Result,
                WinnerId = outcome.WinnerId,
                TurnNumber = outcome.TurnNumber,
                SubmissionCount = counter.SubmissionCount,
                InvalidCount = counter.InvalidCount,
            };
        }

        private static AiVsAiMatchRecord AbortedRecord(int seed, CountingAuthority? counter, string failure)
        {
            return new AiVsAiMatchRecord
            {
                Seed = seed,
                SubmissionCount = counter?.SubmissionCount ?? 0,
                InvalidCount = counter?.InvalidCount ?? 0,
                Failure = failure,
            };
        }
    }
}
