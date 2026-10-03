using NUnit.Framework;

namespace Card.Tests.EditMode
{
    /// <summary>
    /// M0-T5 示例测试：证明 EditMode 测试工程可用。
    /// 真正的规则测试从 M1 开始按 Docs/02 的任务表补充。
    /// </summary>
    public sealed class SmokeTests
    {
        [Test]
        public void TestRunner_IsWired_Up()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
    }
}
