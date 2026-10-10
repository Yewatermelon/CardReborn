using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T2 共享夹具：GreedyAiAgent 的迷你配置库（含伤害/护甲技能与 targeted 法术）、
    /// 手工局面构造、单回合驱动与命令记录/拒绝注入工具。
    /// </summary>
    internal static class GreedyAiAgentFixtures
    {
        public const string HeroA = "GREEDY_HERO_A";
        public const string HeroB = "GREEDY_HERO_B";
        public const string PingPower = "GREEDY_POWER_PING";     // 任意目标打 1
        public const string ArmorPower = "GREEDY_POWER_ARMOR";   // 无目标叠甲 2

        public const string Card2Cost = "GREEDY_MINION_C2";      // 2 费 2/2
        public const string Card5Cost = "GREEDY_MINION_C5";      // 5 费 5/5
        public const string SpellArmor = "GREEDY_SPELL_ARMOR";   // 1 费无目标叠甲
        public const string SpellEnemyMinion = "GREEDY_SPELL_EMINION"; // 2 费打敌方随从 3

        /// <summary>
        /// 占位随从键：TriggerDispatcher 在回合开始/结束与死亡时按 CardKey 反查定义（TriggerDispatcher.RaiseCardEffects），
        /// 测试用 MatchTestCards.Minion 临时造的场上随从必须在此登记，否则回合流转抛 ConfigLookupException。
        /// 实例数值以 AddToBoard 传入的定义为准；此处仅需键存在、效果为空。
        /// </summary>
        private static readonly string[] PlaceholderMinionKeys =
        {
            "ATK", "T_A", "T_B", "BIG", "PREY", "WALL", "POI", "SNEAK", "SICK", "CHG", "FRZ", "WF", "BRUTE",
        };

        public static CardDatabase BuildDatabase()
        {
            List<CardDefinition> cards = new List<CardDefinition>
            {
                new CardDefinition { Id = 201, Key = Card2Cost, Cost = 2, Type = CardType.Minion, Attack = 2, Health = 2 },
                new CardDefinition { Id = 205, Key = Card5Cost, Cost = 5, Type = CardType.Minion, Attack = 5, Health = 5 },
                new CardDefinition
                {
                    Id = 301, Key = SpellArmor, Cost = 1, Type = CardType.Spell,
                    TargetRule = TargetRule.None, Effects = new[] { "GainArmorEffect:1" }
                },
                new CardDefinition
                {
                    Id = 302, Key = SpellEnemyMinion, Cost = 2, Type = CardType.Spell,
                    TargetRule = TargetRule.EnemyMinion, Effects = new[] { "DamageEffect:3" }
                },
            };
            for (int i = 0; i < PlaceholderMinionKeys.Length; i++)
            {
                cards.Add(new CardDefinition { Id = 400 + i, Key = PlaceholderMinionKeys[i], Type = CardType.Minion });
            }

            for (int i = 0; i < 7; i++)
            {
                cards.Add(new CardDefinition { Id = 450 + i, Key = "FILLER_" + i, Type = CardType.Minion });
            }

            return new CardDatabase(new ConfigBundle(
                cards: cards,
                heroes: new[]
                {
                    new HeroDefinition { Id = 1, Key = HeroA, Health = 30, HeroPowerKey = PingPower },
                    new HeroDefinition { Id = 2, Key = HeroB, Health = 30, HeroPowerKey = ArmorPower }
                },
                heroPowers: new[]
                {
                    new HeroPowerDefinition
                    {
                        Id = 1, Key = PingPower, Cost = 2, TargetRule = TargetRule.Any,
                        Effects = new[] { "DamageEffect:1" }
                    },
                    new HeroPowerDefinition
                    {
                        Id = 2, Key = ArmorPower, Cost = 2, TargetRule = TargetRule.None,
                        Effects = new[] { "GainArmorEffect:2" }
                    },
                },
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100, MinPerPack = 0 } },
                gacha: new GachaConfig { PackSize = 5, CoinCost = 100, PityCount = 10, PityRarity = CardRarity.Legendary },
                rules: new RulesConfig()));
        }

        /// <summary>手工局面：座位 0 主阶段行动中，法力/技能已用可按需指定。</summary>
        public static MatchState BuildState(int mana0 = 10, int mana1 = 10, bool powerUsed0 = false)
        {
            PlayerState p0 = new PlayerState(0, new HeroState(HeroA, PingPower, 30), new ManaPool(mana0, mana0), new RulesConfig());
            p0.Hero.PowerUsedThisTurn = powerUsed0;
            PlayerState p1 = new PlayerState(1, new HeroState(HeroB, ArmorPower, 30), new ManaPool(mana1, mana1), new RulesConfig());
            return new MatchState(p0, p1, 0) { Phase = TurnPhase.Main, TurnNumber = 1 };
        }

        /// <summary>装配"座位 0 = GreedyAiAgent、座位 1 = 空脚本"并跑完座位 0 的一个回合。
        /// guardOptions/clock 透传给 GreedyAiAgent（M7-T3 回合守卫），默认与 M7-T2 行为一致。</summary>
        public static RecordingAuthority RunSeatZeroTurn(
            MatchState state, CardDatabase database, out MatchController controller,
            TurnGuardOptions? guardOptions = null, IClock? clock = null)
        {
            controller = new MatchController(state, database);
            RecordingAuthority recorder = new RecordingAuthority(controller);
            IPlayerAgent[] agents =
            {
                new GreedyAiAgent(0, database, guardOptions, clock),
                new ScriptedPlayerAgent(1, null),
            };
            AgentMatchRunner runner = new AgentMatchRunner(recorder, controller.View, agents);
            runner.Start();
            return recorder;
        }

        /// <summary>真实配置整局：双 GreedyAiAgent 打到终局，返回命令记录器。</summary>
        public static RecordingAuthority RunRealGame(int seed, out MatchController controller)
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(database, deck, seed);
            controller = new MatchController(state, database);
            RecordingAuthority recorder = new RecordingAuthority(controller);
            IPlayerAgent[] agents =
            {
                new GreedyAiAgent(0, database),
                new GreedyAiAgent(1, database),
            };
            AgentMatchRunner runner = new AgentMatchRunner(recorder, controller.View, agents);
            runner.Start();
            return recorder;
        }
    }

    /// <summary>命令记录装饰器：记录每条命令签名并统计被拒数（M7-T2 零非法断言用）。</summary>
    internal sealed class RecordingAuthority : ICommandAuthority
    {
        private readonly ICommandAuthority _inner;

        public RecordingAuthority(ICommandAuthority inner)
        {
            _inner = inner;
        }

        public List<string> Signatures { get; } = new List<string>();

        public int InvalidCount { get; private set; }

        public CommandResult Submit(IGameCommand command)
        {
            CommandResult result = _inner.Submit(command);
            Signatures.Add(Sign(command));
            if (result.IsInvalid)
            {
                InvalidCount++;
            }

            return result;
        }

        public static string Sign(IGameCommand command)
        {
            switch (command)
            {
                case PlayCardCommand p:
                    return "PlayCard|" + p.PlayerId + "|card=" + p.CardInstanceId + "|" + Target(p.Target);
                case AttackCommand a:
                    return "Attack|" + a.PlayerId + "|attacker=" + a.AttackerInstanceId + "|" + Target(a.Target);
                case UseHeroPowerCommand h:
                    return "HeroPower|" + h.PlayerId + "|" + Target(h.Target);
                case EndTurnCommand e:
                    return "EndTurn|" + e.PlayerId;
                default:
                    return command.GetType().Name + "|" + command.PlayerId;
            }
        }

        private static string Target(TargetRef target)
        {
            return target.Kind + ":" + target.TargetId;
        }
    }

    /// <summary>
    /// 直达权威的上下文：不做激活路由（区别于 AgentMatchRunner——其 Submit accepted 后会 Pump 重入激活）。
    /// 供注入类用例单独驱动某个 agent 的 OnTurnActivated，避免嵌套重入污染断言。
    /// </summary>
    internal sealed class DirectAgentContext : IAgentContext
    {
        private readonly ICommandAuthority _authority;

        public DirectAgentContext(ICommandAuthority authority, IReadOnlyMatchState view)
        {
            _authority = authority;
            View = view;
        }

        public int ActivePlayerId => View.ActivePlayerId;

        public IReadOnlyMatchState View { get; }

        public CommandResult Submit(IGameCommand command)
        {
            return _authority.Submit(command);
        }
    }

    /// <summary>拒绝注入上下文：前 N 条 PlayCard 不上行、直接返回 Invalid，验证"被拒候选不重试"。</summary>
    internal sealed class FailPlaysContext : IAgentContext
    {
        private readonly IAgentContext _inner;
        private int _failPlaysRemaining;

        public FailPlaysContext(IAgentContext inner, int failPlays)
        {
            _inner = inner;
            _failPlaysRemaining = failPlays;
        }

        public int FailedPlays { get; private set; }

        public int ActivePlayerId => _inner.ActivePlayerId;

        public IReadOnlyMatchState View => _inner.View;

        public CommandResult Submit(IGameCommand command)
        {
            if (command is PlayCardCommand && _failPlaysRemaining > 0)
            {
                _failPlaysRemaining--;
                FailedPlays++;
                return CommandResult.Invalid(CommandError.InvalidTarget, "测试注入的拒绝。");
            }

            return _inner.Submit(command);
        }
    }
}
