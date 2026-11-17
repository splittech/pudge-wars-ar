using System;
using Game.UI.Foundation;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode
{
    public sealed class ReactiveUiTests
    {
        private GameObject _root;
        private UiState _state;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            if (_state != null)
            {
                UnityEngine.Object.DestroyImmediate(_state);
            }
        }

        [Test]
        public void Model_PublishesOnlyChangedParametersAndOwnsRowCopies()
        {
            _state = ScriptableObject.CreateInstance<UiState>();
            int changes = 0;
            using var subscription = _state.Changed.Subscribe(_ => changes++);
            _state.Text("health", "100");
            _state.Text("health", "100");
            Assert.That(changes, Is.EqualTo(1));
            string[] rows = { "127.0.0.1" };
            _state.Rows("servers", rows);
            rows[0] = "mutated";
            Assert.That(_state.Read("servers").Rows[0], Is.EqualTo("127.0.0.1"));
        }

        [TestCase("Menu", "MenuState")]
        [TestCase("Match", "MatchState")]
        [TestCase("Loading", "LoadingState")]
        [TestCase("Overlay", "OverlayState")]
        [TestCase("Placement", "PlacementState")]
        public void RuntimeDocument_LoadsLayoutAndUsesIndependentModel(string layout, string template)
        {
            _root = new GameObject("UI Test");
            var original = Resources.Load<UiState>("UI/" + template);
            var view = ToolkitRuntime.Create(_root, layout, template);
            Assert.That(view.Root, Is.Not.Null);
            Assert.That(view.Model, Is.Not.SameAs(original));
            view.Model.Text("testKey", "runtime only");
            Assert.That(original.Read("testKey").Text, Is.Null);
        }

        [Test]
        public void View_ReactivelyUpdatesTextProgressVisibilityAndEnabledWithoutEchoingInput()
        {
            _root = new GameObject("UI Test");
            var view = ToolkitRuntime.Create(_root, "Menu", "MenuState");
            int actions = 0;
            view.ActionRequested += (_, _) => actions++;
            view.Model.Text("nickname", "Tester");
            view.Model.Text("message", "Ready");
            view.Model.Enabled("create", false);
            view.Model.Visible("cancel", true);
            Assert.That(view.Root.Q<TextField>("nickname").value, Is.EqualTo("Tester"));
            Assert.That(view.Root.Q<Label>("message").text, Is.EqualTo("Ready"));
            Assert.That(view.Root.Q<Button>("create").enabledSelf, Is.False);
            Assert.That(view.Root.Q<Button>("create").text, Is.Not.Empty);
            Assert.That(view.Root.Q("cancel").ClassListContains("hidden"), Is.False);
            Assert.That(actions, Is.Zero);
        }

        [Test]
        public void View_RecreatedDocumentBindsCurrentModelAgain()
        {
            _root = new GameObject("UI Test");
            var view = ToolkitRuntime.Create(_root, "Loading", "LoadingState");
            var document = view.GetComponent<UIDocument>();
            document.enabled = false;
            document.enabled = true;
            typeof(ReactiveDocument).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(view, null);
            view.Model.Text("description", "Recreated");
            view.Model.Number("progress", 0.5f);
            Assert.That(document.rootVisualElement.Q<Label>("description").text, Is.EqualTo("Recreated"));
            Assert.That(document.rootVisualElement.Q<ProgressBar>("progress").value, Is.EqualTo(0.5f));
            Assert.That(document.rootVisualElement.Q<ProgressBar>("progress").Q(className: "unity-progress-bar__progress"), Is.Not.Null);
        }

        [Test]
        public void View_RebindUnsubscribesPreviousModelAndReenableDisplaysLatestState()
        {
            _root = new GameObject("UI Test");
            var view = ToolkitRuntime.Create(_root, "Menu", "MenuState");
            _state = ScriptableObject.CreateInstance<UiState>();
            var previous = ScriptableObject.CreateInstance<UiState>();
            try
            {
                view.Bind(previous);
                view.Bind(_state);
                _state.Text("message", "new");
                previous.Text("message", "old");
                Assert.That(view.Root.Q<Label>("message").text, Is.EqualTo("new"));
                view.enabled = false;
                _state.Text("message", "while disabled");
                view.enabled = true;
                Assert.That(view.Root.Q<Label>("message").text, Is.EqualTo("while disabled"));
            }
            finally { UnityEngine.Object.DestroyImmediate(previous); }
        }
    }
}
