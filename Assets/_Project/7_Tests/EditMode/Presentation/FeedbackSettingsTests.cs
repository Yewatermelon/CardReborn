using Card.Presentation.Battle.Feedback;
using NUnit.Framework;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>反馈设置（M5-T6）：开关默认值、Speed 钳制与时长缩放语义。</summary>
    [TestFixture]
    internal sealed class FeedbackSettingsTests
    {
        [Test]
        public void Defaults_AllEnabledAndSpeedOne()
        {
            var settings = new FeedbackSettings();

            Assert.AreEqual(1f, settings.Speed, 1e-6f);
            Assert.IsTrue(settings.DamageNumbersEnabled);
            Assert.IsTrue(settings.DeathFadeEnabled);
            Assert.IsTrue(settings.TurnBannerEnabled);
            Assert.IsTrue(settings.AudioEnabled);
        }

        [Test]
        public void Speed_ClampsToPositiveMinimum()
        {
            var settings = new FeedbackSettings();

            settings.Speed = 0f;

            Assert.Greater(settings.Speed, 0f);
        }

        [Test]
        public void ScaleDuration_DividesBySpeed()
        {
            var settings = new FeedbackSettings();
            settings.Speed = 2f;

            Assert.AreEqual(0.6f, settings.ScaleDuration(1.2f), 1e-6f);
        }
    }
}
