using NUnit.Framework;
using UnityEngine;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class CardViewPoolTests
    {
        private GameObject _root = null!;
        private Transform _poolRoot = null!;
        private CardView _prefab = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Pool_Test");
            var poolGo = new GameObject("PoolRoot");
            poolGo.transform.SetParent(_root.transform);
            poolGo.SetActive(false);
            _poolRoot = poolGo.transform;
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void Rent_NoPrewarm_CreatesOneActiveInstance()
        {
            var pool = new CardViewPool(_prefab, _poolRoot);

            CardView view = pool.Rent(_root.transform);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.parent, Is.EqualTo(_root.transform));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(0));
        }

        [Test]
        public void Return_RentedView_DeactivatesAndParksUnderPoolRoot()
        {
            var pool = new CardViewPool(_prefab, _poolRoot);
            CardView view = pool.Rent(_root.transform);

            pool.Return(view);

            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.transform.parent, Is.EqualTo(_poolRoot));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void RentAfterReturn_ReusesSameInstanceWithoutNewCreate()
        {
            var pool = new CardViewPool(_prefab, _poolRoot);
            CardView first = pool.Rent(_root.transform);
            pool.Return(first);

            CardView second = pool.Rent(_root.transform);

            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
        }

        [Test]
        public void Prewarm_CreatesIdleInstancesUpfront()
        {
            var pool = new CardViewPool(_prefab, _poolRoot, prewarmCount: 3);

            Assert.That(pool.CreatedCount, Is.EqualTo(3));
            Assert.That(pool.IdleCount, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(_poolRoot.childCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(_poolRoot.GetChild(i).gameObject.activeSelf, Is.False);
            }
        }

        [Test]
        public void RentWithinPrewarmCapacity_DoesNotCreateNew()
        {
            var pool = new CardViewPool(_prefab, _poolRoot, prewarmCount: 2);

            CardView a = pool.Rent(_root.transform);
            CardView b = pool.Rent(_root.transform);

            Assert.That(pool.CreatedCount, Is.EqualTo(2));
            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(a, Is.Not.SameAs(b));
        }

        [Test]
        public void Return_Null_ThrowsArgumentNullException()
        {
            var pool = new CardViewPool(_prefab, _poolRoot);

            Assert.That(() => pool.Return(null!), Throws.ArgumentNullException);
        }
    }
}
