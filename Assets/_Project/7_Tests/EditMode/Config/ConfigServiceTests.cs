using System;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T6：热加载语义——成功换库、失败保旧、版本号递增。</summary>
    public sealed class ConfigServiceTests
    {
        [Test]
        public void Ctor_WhenArgumentsMissing_ThrowsArgumentNullException()
        {
            CardDatabase database = new CardDatabase(ConfigTestBundles.WithCards("LIVE"));

            Assert.Throws<ArgumentNullException>(() => new ConfigService(null!, () => null!));
            Assert.Throws<ArgumentNullException>(() => new ConfigService(database, null!));
        }

        [Test]
        public void Ctor_WhenCreated_ExposesInitialDatabaseAndZeroVersion()
        {
            CardDatabase database = new CardDatabase(ConfigTestBundles.WithCards("LIVE"));

            ConfigService service = new ConfigService(database, () => null!);

            Assert.That(service.Current, Is.SameAs(database));
            Assert.That(service.Version, Is.EqualTo(0));
            Assert.That(service.LastReload, Is.Null);
        }

        [Test]
        public void TryReload_WhenSourceSucceeds_NewCardAppearsInCurrent()
        {
            ConfigService service = new ConfigService(
                new CardDatabase(ConfigTestBundles.WithCards("LIVE")),
                () => ConfigLoadResult.Success(ConfigTestBundles.WithCards("LIVE", "BRAND_NEW")));

            bool reloaded = service.TryReload();

            Assert.That(reloaded, Is.True);
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(service.Current.RequireCard("BRAND_NEW").Key, Is.EqualTo("BRAND_NEW"), "新卡必须立刻可查");
            Assert.That(service.LastReload!.Succeeded, Is.True);
        }

        [Test]
        public void TryReload_WhenSourceFails_KeepsOldDatabase()
        {
            CardDatabase original = new CardDatabase(ConfigTestBundles.WithCards("LIVE"));
            ConfigService service = new ConfigService(
                original,
                () => ConfigLoadResult.Failure(new[] { "cards.json 解析失败：坏数据" }));

            bool reloaded = service.TryReload();

            Assert.That(reloaded, Is.False);
            Assert.That(service.Current, Is.SameAs(original), "失败必须保留旧卡池，不能清空");
            Assert.That(service.Version, Is.EqualTo(0));
            Assert.That(service.Current.RequireCard("LIVE").Key, Is.EqualTo("LIVE"));
            Assert.That(service.LastReload!.Errors[0], Does.Contain("坏数据"));
        }

        [Test]
        public void TryReload_WhenCalledTwice_IncrementsVersionEachSuccess()
        {
            int calls = 0;
            ConfigService service = new ConfigService(
                new CardDatabase(ConfigTestBundles.WithCards("LIVE")),
                () =>
                {
                    calls++;
                    return ConfigLoadResult.Success(ConfigTestBundles.WithCards("LIVE" + calls));
                });

            service.TryReload();
            service.TryReload();

            Assert.That(service.Version, Is.EqualTo(2));
            Assert.That(service.Current.RequireCard("LIVE2").Key, Is.EqualTo("LIVE2"));
        }

        [Test]
        public void TryReload_WhenFailureThenSuccess_Recovers()
        {
            int calls = 0;
            ConfigService service = new ConfigService(
                new CardDatabase(ConfigTestBundles.WithCards("LIVE")),
                () =>
                {
                    calls++;
                    return calls == 1
                        ? ConfigLoadResult.Failure(new[] { "临时故障" })
                        : ConfigLoadResult.Success(ConfigTestBundles.WithCards("LIVE", "RECOVERED"));
                });

            Assert.That(service.TryReload(), Is.False);
            Assert.That(service.TryReload(), Is.True);
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(service.Current.RequireCard("RECOVERED").Key, Is.EqualTo("RECOVERED"));
        }

        [Test]
        public void TryReload_WhenSourceReturnsNull_ThrowsArgumentNullException()
        {
            ConfigService service = new ConfigService(
                new CardDatabase(ConfigTestBundles.WithCards("LIVE")),
                () => null!);

            Assert.Throws<ArgumentNullException>(() => service.TryReload());
        }

        [Test]
        public void TryReload_SwapsToADifferentDatabaseInstance()
        {
            CardDatabase original = new CardDatabase(ConfigTestBundles.WithCards("LIVE"));
            ConfigService service = new ConfigService(
                original,
                () => ConfigLoadResult.Success(ConfigTestBundles.WithCards("LIVE", "EXTRA")));

            service.TryReload();

            Assert.That(service.Current, Is.Not.SameAs(original));
            Assert.That(original.RequireCard("LIVE").Key, Is.EqualTo("LIVE"), "旧库对象本身仍可用");
            Assert.That(service.Current.CardCount, Is.EqualTo(2));
        }
    }
}
