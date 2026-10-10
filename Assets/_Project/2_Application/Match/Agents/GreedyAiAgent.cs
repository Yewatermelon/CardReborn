using System.Collections.Generic;
using System.Text;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 贪心基础 AI（M7-T2，FR-6.2）：激活回调内跑完一整个回合——
    /// 英雄技能（一次）→ 出牌循环（高费优先）→ 攻击循环（先解场后打脸）→ 结束回合。
    ///
    /// 决策只读 <see cref="IAgentContext.View"/> + <see cref="CardDatabase"/>（数值/规则来自配置，铁律 6）；
    /// 命令一律 <see cref="IAgentContext.Submit"/> 上行经 RuleEngine 校验（铁律 5、FR-6.5）。
    /// 无随机：同种子命令序列逐位一致；被拒候选本回合不重试；候选耗尽必发 EndTurn（终止性）。
    ///
    /// 回合终止保障（M7-T3，FR-6.4）：决策循环接入 <see cref="TurnGuard"/>
    /// （可配置步数上限 + IClock 时间上限 + 无进展检测），任一触发即停止决策、必发 EndTurn。
    ///
    /// 逐步模式（M7-OBS-1）：构造 <c>stepMode: true</c> 时 <see cref="OnTurnActivated"/> 只初始化，
    /// 外部逐帧调 <see cref="StepOne"/> 产出单条命令（分帧泵场景）；默认 <c>false</c> 保持 T2
    /// 同步跑完行为（OnTurnActivated 内 while StepOne() 循环 + EndTurn）。
    /// 逐步相关方法见 <see cref="GreedyAiAgent.Stepper.cs"/>（partial）。
    /// </summary>
    public sealed partial class GreedyAiAgent : IPlayerAgent
    {
        private static readonly IReadOnlyList<TriggeredEffect> NoEffects = new TriggeredEffect[0];

        private enum TurnPhase
        {
            HeroPower,
            PlayCards,
            Attack,
            Done,
        }

        private readonly CardDatabase _database;
        private readonly TurnGuard _guard;
        private readonly bool _stepMode;
        private readonly AiDifficultyProfile _difficulty;

        // 回合内状态（提升为实例字段以支持逐步模式）。
        private IAgentContext? _context;
        private readonly HashSet<int> _rejectedCards = new HashSet<int>();
        private readonly HashSet<int> _exhaustedAttackers = new HashSet<int>();
        private readonly Dictionary<int, int> _attacksUsed = new Dictionary<int, int>();
        private TurnPhase _phase;

        public GreedyAiAgent(
            int playerId, CardDatabase database,
            TurnGuardOptions? guardOptions = null, IClock? clock = null,
            bool stepMode = false,
            AiDifficultyProfile? difficulty = null)
        {
            PlayerId = playerId;
            _database = Guard.NotNull(database, nameof(database));
            _guard = new TurnGuard(guardOptions, clock);
            _stepMode = stepMode;
            _difficulty = difficulty ?? AiDifficultyProfile.Normal;
        }

        public int PlayerId { get; }

        public void OnTurnActivated(IAgentContext context)
        {
            Guard.NotNull(context, nameof(context));
            _context = context;
            ResetTurnState();
            _guard.OnActivationStarted();

            if (_stepMode)
            {
                // 分帧模式：只初始化，不 Submit 任何命令；外部逐帧调 StepOne。
                return;
            }

            // 同步模式（T2 行为）：循环 StepOne 跑完回合 + EndTurn。
            while (StepOne()) { }
            if (CanAct(context.View))
            {
                // EndTurn 不受守卫约束：守卫只停止"后续决策"，回合必须收尾（FR-6.4）。
                context.Submit(new EndTurnCommand(PlayerId));
                _guard.RegisterStep(BuildStateSignature(context.View));
            }
        }

        public void OnTurnDeactivated()
        {
            // 同步模式（stepMode=false）：OnTurnActivated 开头已 ResetTurnState()，
            // 此处空操作避免递归返回后状态被破坏（原 T2 行为一致）。
            // 分帧模式（stepMode=true）：外部步进完成后 OnTurnActivated 会 ResetTurnState()。
        }

        // ---------- 私有辅助 ----------

        private void ResetTurnState()
        {
            _rejectedCards.Clear();
            _exhaustedAttackers.Clear();
            _attacksUsed.Clear();
            _phase = TurnPhase.HeroPower;
        }

        private bool CanAct(IReadOnlyMatchState view)
        {
            return !view.IsFinished && view.ActivePlayerId == PlayerId;
        }

        /// <summary>
        /// 决策相关局面签名（确定性遍历、无 Random）：双方英雄/法力/分区张数/疲劳 + 场面每张卡关键值。
        /// 签名不变 ≈ 命令对局面无实质影响（含被拒/合法但无效）——无进展检测的输入面。
        /// </summary>
        private static string BuildStateSignature(IReadOnlyMatchState view)
        {
            var sb = new StringBuilder();
            sb.Append('T').Append(view.TurnNumber)
                .Append(";P").Append(view.ActivePlayerId)
                .Append(";F").Append(view.IsFinished ? 1 : 0);
            foreach (IReadOnlyPlayerState player in view.Players)
            {
                sb.Append("|p").Append(player.Id)
                    .Append(",hp").Append(player.Hero.Health)
                    .Append(",ar").Append(player.Hero.Armor)
                    .Append(",pw").Append(player.Hero.PowerUsedThisTurn ? 1 : 0)
                    .Append(",m").Append(player.Mana.Current).Append('/').Append(player.Mana.Max)
                    .Append(",d").Append(player.Deck.Count)
                    .Append(",h").Append(player.Hand.Count)
                    .Append(",g").Append(player.Graveyard.Count)
                    .Append(",f").Append(player.FatigueCounter);
                foreach (IReadOnlyCardInstance card in player.Board.Cards)
                {
                    sb.Append(",b").Append(card.InstanceId)
                        .Append(':').Append(card.Attack).Append('/').Append(card.Health)
                        .Append(':').Append((int)card.StatusFlags)
                        .Append(':').Append((int)card.KeywordFlags);
                }
            }

            return sb.ToString();
        }
    }
}
