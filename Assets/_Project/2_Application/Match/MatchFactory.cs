using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 开局初始化（M3-T5；Docs/01 §3.1）：牌库构建 + 确定性洗牌 + 掷先后手 + 抽起手 + 幸运币。
    /// 随机调用顺序固定：**座0洗牌 → 座1洗牌 → 掷先手**，故牌序只由种子决定、与先后手结果无关。
    /// InstanceId 按座位分段分配（座0 从 0、座1 从 DeckSize 起，幸运币 = 2*DeckSize），全场唯一。
    /// </summary>
    public static class MatchFactory
    {
        private const int Seat0Id = 0;
        private const int Seat1Id = 1;

        /// <summary>产出先手已进入首回合 Main 的对局（法力 1/1，首回合不抽牌）。</summary>
        public static MatchState Create(
            CardDatabase database,
            MatchSetupRequest seat0,
            MatchSetupRequest seat1,
            IRandomProvider random)
        {
            Guard.NotNull(database, nameof(database));
            Guard.NotNull(seat0, nameof(seat0));
            Guard.NotNull(seat1, nameof(seat1));
            Guard.NotNull(random, nameof(random));

            RulesConfig rules = database.Rules;
            PlayerState firstSeat = BuildPlayer(database, seat0, Seat0Id, rules, random, 0);
            PlayerState secondSeat = BuildPlayer(
                database, seat1, Seat1Id, rules, random, rules.DeckSize);

            int activePlayerId = random.NextInt(Seat0Id, Seat1Id + 1);
            PlayerState active = activePlayerId == Seat0Id ? firstSeat : secondSeat;
            PlayerState other = activePlayerId == Seat0Id ? secondSeat : firstSeat;

            DrawStartingHand(active, rules.StartingHandFirst);
            DrawStartingHand(other, rules.StartingHandSecond);
            AddCoin(database, other, rules, rules.DeckSize * 2);

            active.Mana.BeginTurn(rules.ManaLimit);

            return new MatchState(firstSeat, secondSeat, activePlayerId)
            {
                Phase = TurnPhase.Main,
                TurnNumber = 1,
                ActivePlayerId = activePlayerId,
                NextInstanceId = rules.DeckSize * 2 + 1
            };
        }

        private static PlayerState BuildPlayer(
            CardDatabase database,
            MatchSetupRequest request,
            int seatId,
            RulesConfig rules,
            IRandomProvider random,
            int firstInstanceId)
        {
            RequireDeckSize(request, rules);

            HeroDefinition hero = database.RequireHero(request.HeroKey);
            HeroState heroState = new HeroState(hero.Key, hero.HeroPowerKey, hero.Health);
            PlayerState player = new PlayerState(seatId, heroState, new ManaPool(), rules);

            List<CardInstance> deckCards = new List<CardInstance>(rules.DeckSize);
            for (int i = 0; i < request.DeckCardKeys.Count; i++)
            {
                CardDefinition card = database.RequireCard(request.DeckCardKeys[i]);
                deckCards.Add(CardInstance.FromDefinition(card, firstInstanceId + i, seatId));
            }

            random.Shuffle(deckCards);
            for (int i = 0; i < deckCards.Count; i++)
            {
                player.Deck.Add(deckCards[i]);
            }

            return player;
        }

        private static void RequireDeckSize(MatchSetupRequest request, RulesConfig rules)
        {
            Guard.Require(
                request.DeckCardKeys.Count == rules.DeckSize,
                nameof(request),
                "卡组张数必须为 " + rules.DeckSize + "，实际 " + request.DeckCardKeys.Count);
        }

        private static void DrawStartingHand(PlayerState player, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                CardInstance top = player.Deck.Cards[player.Deck.Count - 1];
                player.Hand.MoveIn(top, player.Deck);
            }
        }

        private static void AddCoin(
            CardDatabase database,
            PlayerState player,
            RulesConfig rules,
            int instanceId)
        {
            Guard.Require(
                !string.IsNullOrWhiteSpace(rules.TheCoinCardKey),
                nameof(rules),
                "Rules 未配置 TheCoinCardKey，无法向局发放幸运币。");

            CardDefinition coin = database.RequireCard(rules.TheCoinCardKey!);
            CardInstance coinCard = CardInstance.FromDefinition(coin, instanceId, player.Id);
            player.Hand.Add(coinCard);
        }
    }
}
