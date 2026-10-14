using System;
using System.Collections.Generic;
using System.IO;
using Card.Application.Match;
using Card.Application.Replay;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Replay
{
    /// <summary>
    /// M15-T1 回放持久化仓库测试（5 例）：覆盖 Save→List→Load→Delete 往返 +
    /// 边界（空目录、不存在文件、损坏 JSON、连续 Save 不互相破坏）。
    /// </summary>
    [TestFixture]
    public sealed class ReplayFileRepositoryTests
    {
        private static readonly CardDatabase Db = MatchControllerFixtures.BuildDatabase();

        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "CardRebornReplayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        [Test]
        public void Save_List_Load_Delete_RoundTrip()
        {
            var repo = new ReplayFileRepository();
            MatchRecording recording = BuildRecording(seed: 42, cmdCount: 3);

            string savedPath = repo.Save(recording, _dir, TestTimestamp);
            Assert.That(File.Exists(savedPath), Is.True, "Save 应写入文件");

            IReadOnlyList<ReplayFileMeta> metas = repo.List(_dir);
            Assert.That(metas.Count, Is.EqualTo(1));
            Assert.That(metas[0].Seed, Is.EqualTo(42));
            Assert.That(metas[0].CommandCount, Is.EqualTo(3));
            Assert.That(metas[0].FilePath, Is.EqualTo(savedPath));

            MatchRecording loaded = repo.Load(savedPath);
            Assert.That(loaded.Seed, Is.EqualTo(42));
            Assert.That(loaded.Commands.Count, Is.EqualTo(3));
            Assert.That(loaded.Commands[0], Is.EqualTo(recording.Commands[0]));
            Assert.That(loaded.Seat0.HeroKey, Is.EqualTo(recording.Seat0.HeroKey));
            Assert.That(loaded.Seat1.HeroKey, Is.EqualTo(recording.Seat1.HeroKey));

            Assert.That(repo.Delete(savedPath), Is.True);
            Assert.That(File.Exists(savedPath), Is.False);

            IReadOnlyList<ReplayFileMeta> after = repo.List(_dir);
            Assert.That(after.Count, Is.EqualTo(0));
        }

        [Test]
        public void List_NonexistentDir_ReturnsEmpty()
        {
            var repo = new ReplayFileRepository();
            string ghost = Path.Combine(_dir, "does-not-exist");

            IReadOnlyList<ReplayFileMeta> metas = repo.List(ghost);
            Assert.That(metas, Is.Not.Null);
            Assert.That(metas.Count, Is.EqualTo(0));
        }

        [Test]
        public void Load_NonexistentFile_Throws()
        {
            var repo = new ReplayFileRepository();
            string ghost = Path.Combine(_dir, "ghost.json");

            try
            {
                repo.Load(ghost);
                Assert.Fail("应抛 FileNotFoundException");
            }
            catch (FileNotFoundException)
            {
                // 期望
            }
        }

        [Test]
        public void Load_CorruptJson_Throws()
        {
            var repo = new ReplayFileRepository();
            string path = Path.Combine(_dir, "broken.json");
            File.WriteAllText(path, "{ this is not valid json");

            try
            {
                repo.Load(path);
                Assert.Fail("应抛 ArgumentException");
            }
            catch (ArgumentException)
            {
                // 期望
            }
        }

        [Test]
        public void Save_Twice_BothPathsLoadSameRecording()
        {
            var repo = new ReplayFileRepository();
            MatchRecording recording = BuildRecording(seed: 99, cmdCount: 2);

            string path1 = repo.Save(recording, _dir, TestTimestamp);
            string path2 = repo.Save(recording, _dir, TestTimestamp);

            Assert.That(path1, Is.Not.EqualTo(path2), "两次 Save 应返回不同文件路径");

            MatchRecording load1 = repo.Load(path1);
            MatchRecording load2 = repo.Load(path2);
            Assert.That(load1.Seed, Is.EqualTo(recording.Seed));
            Assert.That(load2.Seed, Is.EqualTo(recording.Seed));
            Assert.That(load1.Commands.Count, Is.EqualTo(recording.Commands.Count));
            Assert.That(load2.Commands.Count, Is.EqualTo(recording.Commands.Count));
            Assert.That(load1.Commands[0], Is.EqualTo(recording.Commands[0]));
            Assert.That(load2.Commands[0], Is.EqualTo(recording.Commands[0]));
        }

        // ===== 助手 =====

        /// <summary>固定写入时间戳（UTC）：同值两次 Save 仅靠 GUID 短缀区分文件名。</summary>
        private static readonly DateTime TestTimestamp =
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        private static MatchRecording BuildRecording(int seed, int cmdCount)
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var commands = new List<string>(cmdCount);
            for (int i = 0; i < cmdCount; i++)
            {
                commands.Add("{\"type\":\"EndTurn\",\"playerId\":0}");
            }
            return new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                seed,
                commands);
        }
    }
}
