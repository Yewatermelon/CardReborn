using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>浮动文字池（M5-T6；03 §5.8）：租还、播完回收、峰值复用零新建。</summary>
    [TestFixture]
    internal sealed class FloatingTextPoolTests
    {
        private FloatingTextView _prefab = null!;
        private Transform _poolRoot = null!;
        private Transform _parent = null!;
        private FloatingTextPool _pool = null!;

        [SetUp]
        public void SetUp()
        {
            _prefab = FeedbackTestPrefabs.CreateFloatingTextView();
            _poolRoot = new GameObject("PoolRoot").transform;
            _parent = new GameObject("Parent", typeof(RectTransform)).transform;
            _pool = new FloatingTextPool(_prefab, _poolRoot, 2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_poolRoot.gameObject);
            Object.DestroyImmediate(_parent.gameObject);
        }

        [Test]
        public void Rent_ActivatesAndReparentsWithoutNewCreation()
        {
            FloatingTextView view = _pool.Rent(_parent);

            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.AreSame(_parent, view.transform.parent);
            Assert.AreEqual(2, _pool.CreatedCount);
            Assert.AreEqual(1, _pool.ActiveCount);
        }

        [Test]
        public void Return_DeactivatesAndParksUnderPoolRoot()
        {
            FloatingTextView view = _pool.Rent(_parent);

            _pool.Return(view);

            Assert.IsFalse(view.gameObject.activeSelf);
            Assert.AreSame(_poolRoot, view.transform.parent);
            Assert.AreEqual(0, _pool.ActiveCount);
        }

        [Test]
        public void ReclaimFinished_ReturnsOnlyFinishedViews()
        {
            FloatingTextView finished = _pool.Rent(_parent);
            FloatingTextView playing = _pool.Rent(_parent);
            finished.Show("-1", Color.red, 0.1f);
            playing.Show("-9", Color.red, 10f);
            finished.Tick(0.2f);

            int reclaimed = _pool.ReclaimFinished();

            Assert.AreEqual(1, reclaimed);
            Assert.AreEqual(1, _pool.ActiveCount);
            Assert.IsFalse(finished.gameObject.activeSelf);
            Assert.AreSame(_poolRoot, finished.transform.parent);
            Assert.IsTrue(playing.gameObject.activeSelf);
        }

        [Test]
        public void PeakReuse_CreatedCountStaysStable()
        {
            for (int round = 0; round < 2; round++)
            {
                var rented = new FloatingTextView[3];
                for (int i = 0; i < rented.Length; i++)
                {
                    rented[i] = _pool.Rent(_parent);
                    rented[i].Show("-1", Color.red, 0.01f);
                    rented[i].Tick(0.02f);
                }

                _pool.ReclaimFinished();
            }

            Assert.AreEqual(3, _pool.CreatedCount);
            Assert.AreEqual(0, _pool.ActiveCount);
        }
    }
}
