using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>对局终局判定器（M3-T7）：只读查询，不修改状态。</summary>
    public static class MatchEvaluator
    {
        // MatchEvaluator 只负责"谁赢了/为什么"，不知道对局 Id 和时间 tick
        // —— MatchController 会用完整数据重新构造 MatchOutcome
        private const string DummyId = "evaluator";

        public static MatchOutcome Evaluate(MatchState state)
        {
            PlayerState p0 = state.GetPlayer(0);
            PlayerState p1 = state.GetPlayer(1);

            bool p0Dead = p0.Hero.Health <= 0;
            bool p1Dead = p1.Hero.Health <= 0;

            if (p0Dead && p1Dead)
            {
                return new MatchOutcome(DummyId, MatchResult.Draw, null, state.TurnNumber, "双方英雄同时归零。", 0, 0);
            }

            if (p0Dead)
            {
                return new MatchOutcome(DummyId, MatchResult.Player1Wins, 1, state.TurnNumber, "座 0 英雄生命归零。", 0, 0);
            }

            if (p1Dead)
            {
                return new MatchOutcome(DummyId, MatchResult.Player0Wins, 0, state.TurnNumber, "座 1 英雄生命归零。", 0, 0);
            }

            return new MatchOutcome(DummyId, MatchResult.Ongoing, null, state.TurnNumber, "对局进行中。", 0, 0);
        }
    }
}
