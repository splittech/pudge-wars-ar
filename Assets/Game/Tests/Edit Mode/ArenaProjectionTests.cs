using Game.Input.Routing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ArenaProjectionTests
    {
        [TestCase(0.02f, 0f)]
        [TestCase(0.08f, 130f)]
        [TestCase(1f, 270f)]
        public void Projection_ScaledRotatedAndTranslatedRoot_ReturnsArenaCoordinates(float scale, float yaw)
        {
            var root = new GameObject("Projection root");
            var cameraObject = new GameObject("Projection camera");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(5, 2, -3), Quaternion.Euler(20, yaw, 0));
                root.transform.localScale = Vector3.one * scale;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 20;
                camera.transform.position = root.transform.position + root.transform.up * 10;
                camera.transform.rotation = Quaternion.LookRotation(-root.transform.up, root.transform.forward);
                var space = root.AddComponent<FlatArenaSpace>();
                var serialized = new SerializedObject(space);
                serialized.FindProperty("_arenaRoot").objectReferenceValue = root.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                space.SetCamera(camera);
                Vector3 expectedWorld = root.transform.TransformPoint(new Vector3(3, 0, -2));
                Vector3 screen = camera.WorldToScreenPoint(expectedWorld);
                Assert.That(space.TryScreenToArena(screen, out var point), Is.True);
                // At the smallest scale this allows 0.1 mm of world-space float error.
                Assert.That(point.x, Is.EqualTo(3).Within(0.005));
                Assert.That(point.y, Is.EqualTo(-2).Within(0.005));
                Assert.That(Vector3.Distance(space.ArenaToWorld(point), expectedWorld), Is.LessThan(0.001));
                space.InputEnabled = false;
                Assert.That(space.TryScreenToArena(screen, out _), Is.False);
                space.InputEnabled = true;
                root.SetActive(false);
                Assert.That(space.TryScreenToArena(screen, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
