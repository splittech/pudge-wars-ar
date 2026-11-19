using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AR;
using Game.Core.Geometry;
using Game.Input.Gestures;
using NSubstitute;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    public sealed class RoomPlacementTests
    {
        [UnityTest]
        public IEnumerator AutomaticProposal_ConfirmationFreezesMapBeforeSubmission()
        {
            var root = new GameObject("Automatic room placement test");
            var taps = new Subject<Vector2>();
            var gestures = Substitute.For<ITouchGestures>();
            gestures.Tap.Returns(taps);
            var cancellation = new CancellationTokenSource();
            var provider = new RoomArenaAnchorProvider(
                null, null, null, null, null, root.transform, gestures, true, false);
            try
            {
                UniTask<Pose> placement = provider.AcquireArenaPoseAsync(cancellation.Token);
                provider.BeginAutomaticScan();
                var surfaces = new[]
                {
                    new RoomScanSurface(new UnityEngine.XR.ARSubsystems.TrackableId(1, 0), 0,
                        RoomArenaTests.Rectangle(-6, -6, 6, 6)),
                    new RoomScanSurface(new UnityEngine.XR.ARSubsystems.TrackableId(2, 0), 0.8f,
                        RoomArenaTests.Rectangle(1, 1, 3, 3))
                };
                provider.UpdateScanProposal(surfaces);
                Assert.That(provider.CanConfirm, Is.True);
                provider.Confirm();
                yield return null;
                Assert.That(provider.IsAutomaticScan, Is.False);
                Assert.That(placement.Status, Is.EqualTo(UniTaskStatus.Pending));
                provider.UpdateScanProposal(System.Array.Empty<RoomScanSurface>());
                provider.Confirm();
                for (int i = 0; i < 20 && placement.Status == UniTaskStatus.Pending; i++)
                {
                    yield return null;
                }

                Assert.That(placement.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                placement.GetAwaiter().GetResult();
                Assert.That(provider.Room.PolygonCount, Is.EqualTo(2));
            }
            finally
            {
                cancellation.Cancel();
                provider.Dispose();
                cancellation.Dispose();
                taps.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator FlatScan_BoundaryUndoAndManualFurniture_ProducesValidatedMap()
        {
            var root = new GameObject("Room placement test");
            var cameraObject = new GameObject("Room scan camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10;
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(90, 0, 0));
            var taps = new Subject<Vector2>();
            var gestures = Substitute.For<ITouchGestures>();
            gestures.Tap.Returns(taps);
            var cancellation = new CancellationTokenSource();
            var provider = new RoomArenaAnchorProvider(null, null, null, null, camera, root.transform, gestures, true, false);
            try
            {
                UniTask<Pose> placement = provider.AcquireArenaPoseAsync(cancellation.Token);
                foreach (var point in RoomArenaTests.Rectangle(-6, -6, 6, 6))
                {
                    Vector3 screen = camera.WorldToScreenPoint(new Vector3(point.x, 0, point.y));
                    taps.OnNext(new Vector2(screen.x, screen.y));
                    yield return null;
                }

                provider.Undo();
                Vector3 lastCorner = camera.WorldToScreenPoint(new Vector3(-6, 0, 6));
                taps.OnNext(new Vector2(lastCorner.x, lastCorner.y));
                yield return null;
                Assert.That(provider.CanConfirm, Is.True);
                provider.Confirm();
                yield return null;
                provider.BeginObstacle();
                foreach (var point in RoomArenaTests.Rectangle(-1, -1, 1, 1))
                {
                    Vector3 screen = camera.WorldToScreenPoint(new Vector3(point.x, 0, point.y));
                    taps.OnNext(new Vector2(screen.x, screen.y));
                    yield return null;
                }

                provider.Confirm();
                yield return null;
                provider.Confirm();
                for (int i = 0; i < 20 && placement.Status == UniTaskStatus.Pending; i++)
                {
                    yield return null;
                }

                Assert.That(placement.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                placement.GetAwaiter().GetResult();
                Assert.That(provider.Room.PolygonCount, Is.EqualTo(2));
                Assert.That(provider.Room.Contains(Unity.Mathematics.float2.zero, 0.5f), Is.False);
            }
            finally
            {
                cancellation.Cancel();
                provider.Dispose();
                cancellation.Dispose();
                taps.Dispose();
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ClientPlacement_RequiresReceivedMapBeforeConfirmation()
        {
            var root = new GameObject("Room client placement test");
            var taps = new Subject<Vector2>();
            var gestures = Substitute.For<ITouchGestures>();
            gestures.Tap.Returns(taps);
            var cancellation = new CancellationTokenSource();
            var provider = new RoomArenaAnchorProvider(null, null, null, null, null, root.transform, gestures, false, false);
            try
            {
                UniTask<Pose> placement = provider.AcquireArenaPoseAsync(cancellation.Token);
                Assert.That(provider.CanConfirm, Is.False);
                provider.Confirm();
                yield return null;
                Assert.That(placement.Status, Is.EqualTo(UniTaskStatus.Pending));
                var room = new RoomArena(RoomArenaTests.Rectangle(-6, -6, 6, 6));
                provider.ReceivedRoom = room;
                Assert.That(provider.CanConfirm, Is.True);
                provider.Confirm();
                for (int i = 0; i < 20 && placement.Status == UniTaskStatus.Pending; i++)
                {
                    yield return null;
                }

                Assert.That(placement.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                placement.GetAwaiter().GetResult();
                Assert.That(provider.Room, Is.SameAs(room));
            }
            finally
            {
                cancellation.Cancel();
                provider.Dispose();
                cancellation.Dispose();
                taps.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
