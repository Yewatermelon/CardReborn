using System;
using System.Collections.Generic;
using System.IO;
using Card.Application.Match;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>M6-T1：开局装配（配置目录→数据库→牌组→控制器）。</summary>
    [TestFixture]
    public sealed class BattleCompositionTests
    {
        // 全限定：本文件命名空间在 Card.* 下，裸 Application 会被 Card.Application 命名空间遮蔽。
        private static string GeneratedConfigDir =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Config");

        [Test]
        public void LoadDatabase_FromGeneratedArtifacts_Succeeds()
        {
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);

            Assert.That(database, Is.Not.Null, string.Join("\n", errors));
            Assert.That(database!.EnabledCardCount, Is.GreaterThan(0));
            Assert.That(database.HeroCount, Is.GreaterThan(0));
        }

        [Test]
        public void BuildDeckKeys_MatchesDeckSize_AndAllEnabled()
        {
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);
            AssumeLoaded(database, errors);

            var deck = BattleComposition.BuildDeckKeys(database!);

            Assert.That(deck.Count, Is.EqualTo(database!.Rules.DeckSize));
            foreach (string key in deck)
            {
                Assert.That(database.RequireCard(key).Enabled, Is.True, "牌组中含未启用卡：" + key);
            }
        }

        [Test]
        public void StartMatch_ProducesControllerAtTurnOne()
        {
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);
            AssumeLoaded(database, errors);
            var deck = BattleComposition.BuildDeckKeys(database!);

            MatchController controller = BattleComposition.StartMatch(database!, deck, seed: 42);

            IReadOnlyMatchState view = controller.View;
            Assert.That(view.TurnNumber, Is.EqualTo(1));
            Assert.That(view.Players.Count, Is.EqualTo(2));
            Assert.That(view.ActivePlayer.Hand.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LoadDatabase_MissingDirectory_ReturnsErrors()
        {
            string missing = Path.Combine(
                UnityEngine.Application.temporaryCachePath, "NoSuchCardConfig_" + Guid.NewGuid().ToString("N"));

            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(missing);

            Assert.That(database, Is.Null);
            Assert.That(errors.Count, Is.GreaterThan(0));
        }

        private static void AssumeLoaded(CardDatabase? database, IReadOnlyList<string> errors)
        {
            if (database == null)
            {
                Assert.Fail("生成物配置加载失败：\n" + string.Join("\n", errors));
            }
        }
    }
}
