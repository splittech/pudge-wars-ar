using System;
using System.Reflection;
using System.Threading;
using Game.AR;
using Game.Core.Configs;
using Game.Input.Gestures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TrackingState = UnityEngine.XR.ARSubsystems.TrackingState;

namespace Game.Tests.EditMode
{
    public sealed class ARPlacementTests
    {
        private GameObject _root;
        private GameObject _anchorObject;
        private ARAnchor _anchor;
        private ARSessionState _previousState;
        private ARArenaAnchorProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _previousState = ARSession.state;
            _root = new GameObject("Test arena");
            _anchorObject = new GameObject("Test anchor");
            _anchorObject.SetActive(false);
            _anchor = _anchorObject.AddComponent<ARAnchor>();
            _root.transform.SetParent(_anchor.transform);
            SetAnchorTracking(TrackingState.Tracking);
            SetSessionState(ARSessionState.SessionTracking);

            var config = new InputConfigData(0.3f, 10f, 1.5f, 4f, 0.02f, 0.08f);
            _provider = new ARArenaAnchorProvider(
                null,
                null,
                null,
                null,
                null,
                _root.transform,
                null,
                config,
                new Vector2(24f, 14f));
            typeof(ARArenaAnchorProvider).GetField("_anchor", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_provider, _anchor);
            _provider.RefreshTracking();
        }

        [TearDown]
        public void TearDown()
        {
            _provider.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_anchorObject);
            SetSessionState(_previousState);
        }

        [Test]
        public void SessionRecovers_AnchorStillLimited_RemainsUntracked()
        {
            SetAnchorTracking(TrackingState.Limited);
            _provider.RefreshTracking();
            Assert.That(_provider.CanConfirm, Is.False);

            SetSessionState(ARSessionState.Ready);
            SetSessionState(ARSessionState.SessionTracking);
            Assert.That(_provider.IsTracking.CurrentValue, Is.False);

            SetAnchorTracking(TrackingState.Tracking);
            _provider.RefreshTracking();
            Assert.That(_provider.CanConfirm, Is.True);
        }

        [Test]
        public void Confirm_FreezesScaleAndRotationImmediately()
        {
            ApplyGesture(new PinchTwistGesture(0.001f, 45f));
            Assert.That(_root.transform.localScale.x, Is.EqualTo(0.02f));
            ApplyGesture(new PinchTwistGesture(100f, 45f));
            Assert.That(_root.transform.localScale.x, Is.EqualTo(0.08f));
            Quaternion rotation = _root.transform.localRotation;
            Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));

            _provider.Confirm();
            ApplyGesture(new PinchTwistGesture(0.5f, 90f));
            Assert.That(_provider.CanConfirm, Is.False);
            Assert.That(_root.transform.localScale.x, Is.EqualTo(0.08f));
            Assert.That(Quaternion.Angle(rotation, _root.transform.localRotation), Is.LessThan(0.01f));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(1f, float.NaN)]
        public void InvalidGesture_DoesNotChangePose(float scale, float angle)
        {
            _root.transform.localScale = Vector3.one * 0.04f;
            ApplyGesture(new PinchTwistGesture(scale, angle));

            Assert.That(_root.transform.localScale.x, Is.EqualTo(0.04f));
            Assert.That(_root.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Acquire_CancelledBeforeStart_DoesNotAccessArSession()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var task = _provider.AcquireArenaPoseAsync(cancellation.Token);
                Assert.That(() => task.GetAwaiter().GetResult(), Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public void Dispose_Repeatedly_DetachesArenaAndRejectsConfirmation()
        {
            _provider.Dispose();
            Assert.That(_root.transform.parent, Is.Null);
            Assert.That(_provider.CanConfirm, Is.False);
            Assert.That(() => _provider.Dispose(), Throws.Nothing);
        }

        private void ApplyGesture(PinchTwistGesture gesture)
        {
            typeof(ARArenaAnchorProvider).GetMethod("OnPinchTwist", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_provider, new object[] { gesture });
        }

        private void SetAnchorTracking(TrackingState state)
        {
            // Inject subsystem observations without starting a device XR loader in EditMode.
            var data = new XRAnchor(new TrackableId(1, 1), Pose.identity, state, IntPtr.Zero);
            typeof(ARTrackable<XRAnchor, ARAnchor>)
                .GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_anchor, data);
        }

        private static void SetSessionState(ARSessionState state)
        {
            typeof(ARSession).GetProperty(nameof(ARSession.state)).SetValue(null, state);
        }
    }
}
