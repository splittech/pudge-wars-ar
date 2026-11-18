using Game.AR;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ArenaCameraSwitcherTests
    {
        private GameObject _root;
        private Camera _flat;
        private Camera _ar;
        private Camera _background;
        private ArenaCameraSwitcher _switcher;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Camera switcher test");
            _root.SetActive(false);
            _flat = CreateCamera("Flat", false);
            _ar = CreateCamera("AR", true);
            _background = CreateCamera("Boot", true);
        }

        [TearDown]
        public void TearDown()
        {
            _switcher?.Dispose();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetMode_FlatAndAr_SelectsExactlyOneCameraAndListener()
        {
            _switcher = new ArenaCameraSwitcher(_flat, _ar, new[] { _background });
            AssertSelected(_flat);
            _switcher.SetMode(true);
            AssertSelected(_ar);
            _switcher.SetMode(false);
            AssertSelected(_flat);
        }

        [Test]
        public void Dispose_RestoresOriginalStatesAndRemovesTemporaryFlatListener()
        {
            _background.enabled = false;
            _background.GetComponent<AudioListener>().enabled = false;
            _switcher = new ArenaCameraSwitcher(_flat, _ar, new[] { _background });
            _switcher.SetMode(true);
            _switcher.Dispose();

            Assert.That(_flat.enabled, Is.True);
            Assert.That(_flat.GetComponent<AudioListener>(), Is.Null);
            Assert.That(_ar.enabled, Is.True);
            Assert.That(_ar.GetComponent<AudioListener>().enabled, Is.True);
            Assert.That(_background.enabled, Is.False);
            Assert.That(_background.GetComponent<AudioListener>().enabled, Is.False);
            Assert.That(() => _switcher.Dispose(), Throws.Nothing);
        }

        [Test]
        public void SetMode_UnownedSimulationCamera_IsNotModified()
        {
            Camera simulation = CreateCamera("XR Simulation", false);
            _switcher = new ArenaCameraSwitcher(_flat, _ar, new[] { _background });
            _switcher.SetMode(true);
            Assert.That(simulation.enabled, Is.True);
            _switcher.Dispose();
            Assert.That(simulation.enabled, Is.True);
        }

        [Test]
        public void Dispose_BackgroundAlreadyDestroyed_DoesNotThrow()
        {
            _switcher = new ArenaCameraSwitcher(_flat, _ar, new[] { _background });
            Object.DestroyImmediate(_background.gameObject);
            Assert.That(() => _switcher.Dispose(), Throws.Nothing);
        }

        private Camera CreateCamera(string name, bool listener)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(_root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            if (listener)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            return camera;
        }

        private void AssertSelected(Camera selected)
        {
            int listeners = 0;
            foreach (var camera in new[] { _flat, _ar, _background })
            {
                Assert.That(camera.enabled, Is.EqualTo(camera == selected));
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null && listener.enabled)
                {
                    listeners++;
                    Assert.That(camera, Is.SameAs(selected));
                }
            }

            Assert.That(listeners, Is.EqualTo(1));
        }
    }
}
