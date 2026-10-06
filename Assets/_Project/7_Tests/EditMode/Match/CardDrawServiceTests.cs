using System;
using NUnit.Framework;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T8 失败测试（红）：CardDrawService/DrawOutcome 尚未实现，应先编译失败。</summary>
    [TestFixture]
    public sealed class CardDrawServiceTests
    {
        private static PlayerState CreatePlayer(RulesConfig? rules = null, int health = 30)
        {
            HeroState hero = new HeroState("HERO_A", "POWER_A", 30);
            hero.Health = health;
            return new PlayerState(0, hero, new ManaPool(), rules ?? new RulesConfig());
        }

        private static CardInstance AddToDeck(PlayerState player, int instanceId)
        {
            CardDefinition def = MatchTestCards.Minion("CARD_" + instanceId);
            CardInstance card = CardInstance.FromDefinition(def, instanceId, player.Id);
            player.Deck.Add(card);
            return card;
        }

        private static CardInstance AddToHand(PlayerState player, int instanceId)
        {
            CardDefinition def = MatchTestCards.Minion("HAND_" + instanceId);
            CardInstance card = CardInstance.FromDefinition(def, 1000 + instanceId, player.Id);
            player.Hand.Add(card);
            return card;
        }

        // ---------------------------------------------------------------
        // 疲劳
        // ---------------------------------------------------------------

        [Test]
        public void Draw_EmptyDeck_FirstTime_DealsOneFatigue()
        {
            PlayerState player = CreatePlayer();

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Hero.Health, Is.EqualTo(29));
            Assert.That(player.FatigueCounter, Is.EqualTo(1));
            Assert.That(outcome.FatigueDamage, Is.EqualTo(1));
            Assert.That(outcome.FinalFatigueCounter, Is.EqualTo(1));
            Assert.That(outcome.HeroDied, Is.False);
        }

        [Test]
        public void Draw_EmptyDeck_SecondTime_DealsTwoFatigue()
        {
            PlayerState player = CreatePlayer();
            Card.Application.Match.CardDrawService.Draw(player, 1);

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Hero.Health, Is.EqualTo(27));
            Assert.That(outcome.FatigueDamage, Is.EqualTo(2));
            Assert.That(outcome.FinalFatigueCounter, Is.EqualTo(2));
        }

        [Test]
        public void Draw_EmptyDeck_ThirdTime_DealsThreeFatigue()
        {
            PlayerState player = CreatePlayer();
            Card.Application.Match.CardDrawService.Draw(player, 1);
            Card.Application.Match.CardDrawService.Draw(player, 1);

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Hero.Health, Is.EqualTo(24));
            Assert.That(outcome.FatigueDamage, Is.EqualTo(3));
            Assert.That(outcome.FinalFatigueCounter, Is.EqualTo(3));
        }

        [Test]
        public void Draw_EmptyDeck_ThreeAtOnce_DealsSixTotal()
        {
            PlayerState player = CreatePlayer();

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 3);

            Assert.That(player.Hero.Health, Is.EqualTo(24));
            Assert.That(outcome.FatigueDamage, Is.EqualTo(6));
            Assert.That(outcome.FinalFatigueCounter, Is.EqualTo(3));
            Assert.That(outcome.DrawnInstanceIds, Is.Empty);
            Assert.That(outcome.BurnedInstanceIds, Is.Empty);
        }

        [Test]
        public void Draw_FatigueCanKillHero()
        {
            PlayerState player = CreatePlayer(health: 1);

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Hero.Health, Is.EqualTo(0));
            Assert.That(outcome.HeroDied, Is.True);
        }

        // ---------------------------------------------------------------
        // 正常抽牌
        // ---------------------------------------------------------------

        [Test]
        public void Draw_WithCards_MovesTopCardToHand()
        {
            PlayerState player = CreatePlayer();
            CardInstance bottom = AddToDeck(player, instanceId: 0);
            CardInstance top = AddToDeck(player, instanceId: 1); // 后加入=牌库顶

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Deck.Count, Is.EqualTo(1));
            Assert.That(player.Hand.Count, Is.EqualTo(1));
            Assert.That(outcome.DrawnInstanceIds, Is.EqualTo(new[] { top.InstanceId }));
            Assert.That(outcome.FatigueDamage, Is.EqualTo(0));
            Assert.That(player.Deck.Contains(bottom), Is.True);
        }

        // ---------------------------------------------------------------
        // 爆牌
        // ---------------------------------------------------------------

        [Test]
        public void Draw_HandFull_BurnsCardInsteadOfDrawing()
        {
            PlayerState player = CreatePlayer();
            for (int i = 0; i < player.Hand.Capacity; i++)
            {
                AddToHand(player, i);
            }

            CardInstance top = AddToDeck(player, instanceId: 500);

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(player.Hand.Count, Is.EqualTo(player.Hand.Capacity));
            Assert.That(player.Hand.Contains(top), Is.False);
            Assert.That(player.Deck.Count, Is.EqualTo(0));
            Assert.That(player.Graveyard.Count, Is.EqualTo(1));
            Assert.That(player.Graveyard.Contains(top), Is.True);
            Assert.That(outcome.BurnedInstanceIds, Is.EqualTo(new[] { top.InstanceId }));
            Assert.That(outcome.DrawnInstanceIds, Is.Empty);
        }

        [Test]
        public void Draw_BurnDoesNotTriggerFatigue()
        {
            PlayerState player = CreatePlayer();
            for (int i = 0; i < player.Hand.Capacity; i++)
            {
                AddToHand(player, i);
            }

            AddToDeck(player, instanceId: 500);

            DrawOutcome outcome = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(outcome.FatigueDamage, Is.EqualTo(0));
            Assert.That(player.FatigueCounter, Is.EqualTo(0));
        }

        [Test]
        public void Draw_FatigueStartsAtOneAfterBurnedCardEmptiesDeck()
        {
            PlayerState player = CreatePlayer();
            for (int i = 0; i < player.Hand.Capacity; i++)
            {
                AddToHand(player, i);
            }

            AddToDeck(player, instanceId: 500);

            Card.Application.Match.CardDrawService.Draw(player, 1); // 爆牌，牌库变空
            DrawOutcome fatigue = Card.Application.Match.CardDrawService.Draw(player, 1);

            Assert.That(fatigue.FatigueDamage, Is.EqualTo(1));
            Assert.That(player.FatigueCounter, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------
        // 参数校验
        // ---------------------------------------------------------------

        [Test]
        public void Draw_WhenPlayerNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => Card.Application.Match.CardDrawService.Draw(null!, 1));
        }

        [Test]
        public void Draw_WhenCountNotPositive_Throws()
        {
            PlayerState player = CreatePlayer();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => Card.Application.Match.CardDrawService.Draw(player, 0));
        }
    }
}
