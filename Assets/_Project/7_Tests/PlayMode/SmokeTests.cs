using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Card.Tests.PlayMode
{
    /// <summary>
    /// M0-T5 示例测试：证明 PlayMode 测试工程可用。
    /// 真正的端到端冒烟测试从 M5/M6 开始补充。
    /// </summary>
    public sealed class SmokeTests
    {
        [UnityTest]
        public IEnumerator PlayModeRunner_IsWired_Up()
        {
            yield return null;
            Assert.That(UnityEngine.Application.isPlaying, Is.True);
        }
    }
}
