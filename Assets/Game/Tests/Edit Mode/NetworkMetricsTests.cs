using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Game.Net;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class NetworkMetricsTests
    {
        [Test]
        public void PayloadTransport_LatencyWrapper_UsesKcpCallbacksInsteadOfMissingWrapperEvents()
        {
            // Arrange.
            var root = new GameObject("Metrics transport test");
            root.SetActive(false);
            try
            {
                var kcp = root.AddComponent<kcp2k.KcpTransport>();
                var wrapper = root.AddComponent<Mirror.LatencySimulation>();
                wrapper.wrap = kcp;
                // Act and Assert.
                Assert.That(NetworkStatsRecorder.PayloadTransport(wrapper), Is.SameAs(kcp));
                wrapper.wrap = wrapper;
                Assert.That(NetworkStatsRecorder.PayloadTransport(wrapper), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Latency_StationaryTap_ReportsFirstPresentedMovementOnce()
        {
            // Arrange.
            var probe = new MovementLatencyProbe();
            probe.Begin(Vector2.zero, Vector2.zero, Vector2.right, true, true, 10);
            // Act and Assert.
            Assert.That(probe.TryPresent(Vector2.zero, true, 10.01, out _), Is.False);
            Assert.That(probe.TryPresent(Vector2.right * 0.1f, true, 10.05, out double ms), Is.True);
            Assert.That(ms, Is.EqualTo(50).Within(0.00001));
            Assert.That(probe.TryPresent(Vector2.right, true, 10.1, out _), Is.False);
        }

        [Test]
        public void Latency_ExistingMotionOrForcedMovement_DoesNotFabricateResponse()
        {
            // Arrange.
            var probe = new MovementLatencyProbe();
            // Act and Assert.
            probe.Begin(Vector2.zero, Vector2.left, Vector2.right, true, true, 0);
            Assert.That(probe.TryPresent(Vector2.left * 0.1f, true, 0.01, out _), Is.False);
            probe.Begin(Vector2.zero, Vector2.zero, Vector2.right, true, true, 1);
            Assert.That(probe.TryPresent(Vector2.right, false, 1.1, out _), Is.False);
            Assert.That(probe.TryPresent(Vector2.right, true, 1.2, out _), Is.False);
        }
        [Test]
        public void Take_BroadcastCounts_WeightsBytesByRecipientsAndActualTime()
        {
            // Arrange.
            var window = new NetworkTrafficWindow();
            window.Message(false, 100, 3);
            window.Message(true, 80);
            window.Payload(false, 350);
            window.Payload(true, 90);
            // Act.
            var sample = window.Take(2);
            var empty = window.Take(1);
            // Assert.
            Assert.That(sample.MessageBytesOut, Is.EqualTo(150));
            Assert.That(sample.MessageBytesIn, Is.EqualTo(40));
            Assert.That(sample.MessagesOut, Is.EqualTo(1.5));
            Assert.That(sample.PayloadBytesOut, Is.EqualTo(175));
            Assert.That(sample.BatchesIn, Is.EqualTo(0.5));
            Assert.That(empty.MessageBytesOut, Is.Zero);
        }

        [Test]
        public void Message_LargeBroadcast_DoesNotOverflowIntegerBytes()
        {
            // Arrange.
            var window = new NetworkTrafficWindow();
            // Act.
            window.Message(false, int.MaxValue, 4);
            // Assert.
            Assert.That(window.Take(1).MessageBytesOut, Is.EqualTo((double)int.MaxValue * 4));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void Take_InvalidDuration_Rejects(double seconds)
        {
            // Arrange.
            var window = new NetworkTrafficWindow();
            // Act and Assert.
            Assert.That(() => window.Take(seconds), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Csv_RowHasHeaderWidthEscapesScenarioAndMarksUnavailableSources()
        {
            // Arrange.
            var manager = Setup.NetworkManager(Setup.NetworkSceneLoader(out _, out _), out var root);
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
            try
            {
                var recorder = root.AddComponent<NetworkStatsRecorder>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(string name, object value) => typeof(NetworkStatsRecorder).GetField(name, flags).SetValue(recorder, value);
                Set("_manager", manager); Set("_writer", writer); Set("_runId", "unit"); Set("_runScenario", "comma,quote\"");
                Set("_commit", "unit-commit"); Set("_actualWarmup", 10d);
                // Act.
                typeof(NetworkStatsRecorder).GetMethod("WriteSample", flags).Invoke(recorder, new object[] { 1d });
                writer.Flush();
                string line = Encoding.UTF8.GetString(stream.ToArray()).TrimEnd();
                // Assert.
                Assert.That(Regex.Matches(line, "\"(?:[^\"]|\"\")*\"").Count,
                    Is.EqualTo(NetworkStatsRecorder.Header.Split(',').Length));
                Assert.That(line, Does.Contain("\"comma,quote\"\"\""));
                Assert.That(line, Does.Contain("\"N/A\""));
                Assert.That(line, Does.Not.Contain("ReconnectToken"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
