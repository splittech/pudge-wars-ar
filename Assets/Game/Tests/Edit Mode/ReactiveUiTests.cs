using System;
using Game.AR;
using Game.Services.LoadingScreen;
using Game.UI;
using Game.UI.Foundation;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode
{
    public sealed class ReactiveUiTests
    {
        private GameObject _root;
        private UiViewModel _model;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            if (_model != null)
            {
                UnityEngine.Object.DestroyImmediate(_model);
            }
        }

        [TestCase("Menu", typeof(MenuDocument), typeof(MenuViewModel), 0)]
        [TestCase("Match", typeof(MatchDocument), typeof(MatchViewModel), 0)]
        [TestCase("Placement", typeof(PlacementDocument), typeof(PlacementViewModel), 50)]
        [TestCase("Overlay", typeof(OverlayDocument), typeof(OverlayViewModel), 100)]
        [TestCase("Loading", typeof(LoadingDocument), typeof(LoadingViewModel), 1000)]
        public void Prefab_HasConfiguredDocumentAndIndependentRuntimeModel(
            string name,
            Type viewType,
            Type modelType,
            int sortingOrder)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/UI/" + name + " Document.prefab");
            Assert.That(prefab, Is.Not.Null);
            var savedView = (DocumentView)prefab.GetComponent(viewType);
            var serialized = new SerializedObject(savedView);
            Assert.That(serialized.FindProperty("_runtimeModel").objectReferenceValue, Is.Null);

            _root = UnityEngine.Object.Instantiate(prefab);
            var view = (DocumentView)_root.GetComponent(viewType);
            view.RefreshBindings();
            var document = view.GetComponent<UIDocument>();
            Assert.That(document.visualTreeAsset, Is.Not.Null);
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(document.sortingOrder, Is.EqualTo(sortingOrder));
            Assert.That(view.Model, Is.TypeOf(modelType));
            Assert.That(view.Model, Is.Not.SameAs(view.Template));
            Assert.That(view.Root, Is.Not.Null);
        }

        [Test]
        public void ViewModels_AreIndependentAndSuppressDuplicatePropertyNotifications()
        {
            _model = ScriptableObject.CreateInstance<LoadingViewModel>();
            var first = (LoadingViewModel)_model;
            var second = UnityEngine.Object.Instantiate(first);

            try
            {
                int changes = 0;
                using IDisposable subscription = first.Description.Skip(1).Subscribe(_ => changes++);
                first.Description.Value = "Loading";
                first.Description.Value = "Loading";
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(second.Description.Value, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Menu_BindsBothDirectionsWithoutEchoingProgrammaticFieldChanges()
        {
            MenuDocument view = CreateView<MenuDocument>("Menu");
            int fieldChanges = 0;
            TextField address = view.Root.Q<TextField>("address");
            address.RegisterValueChangedCallback(_ => fieldChanges++);
            view.ViewModel.Address.Value = "192.168.1.10";
            Assert.That(address.value, Is.EqualTo("192.168.1.10"));
            Assert.That(fieldChanges, Is.Zero);
            address.value = "192.168.1.20";
            Assert.That(view.ViewModel.Address.Value, Is.EqualTo("192.168.1.20"));
            view.ViewModel.CanStart.Value = false;
            Assert.That(view.Root.Q<Button>("create").enabledSelf, Is.False);
        }

        [Test]
        public void Placement_MapsServiceStateAndSendsTypedCommands()
        {
            _model = ScriptableObject.CreateInstance<PlacementViewModel>();
            var model = (PlacementViewModel)_model;
            var source = new PlacementSource();
            model.Initialize(source);
            model.Confirm();
            Assert.That(source.ConfirmCount, Is.Zero);
            source.Publish(true);
            Assert.That(model.Message.Value, Is.EqualTo("Ready"));
            Assert.That(model.CanConfirm.Value, Is.True);
            model.Confirm();
            model.UseFlat();
            Assert.That(source.ConfirmCount, Is.EqualTo(1));
            Assert.That(source.FlatCount, Is.EqualTo(1));
        }

        [Test]
        public void Placement_ReinitializeAndDestroyReleaseSourceSubscriptions()
        {
            _model = ScriptableObject.CreateInstance<PlacementViewModel>();
            var model = (PlacementViewModel)_model;
            var first = new PlacementSource();
            var second = new PlacementSource();
            model.Initialize(first);
            model.Initialize(second);
            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.EqualTo(1));
            first.Publish(true);
            Assert.That(model.CanConfirm.Value, Is.False);
            UnityEngine.Object.DestroyImmediate(_model);
            _model = null;
            Assert.That(second.SubscriberCount, Is.Zero);
        }

        [Test]
        public void DisabledView_RebindsLatestStateAndRecreatedVisualTree()
        {
            LoadingDocument view = CreateView<LoadingDocument>("Loading");
            var document = view.GetComponent<UIDocument>();
            view.enabled = false;
            view.ViewModel.Description.Value = "While disabled";
            view.enabled = true;
            view.RefreshBindings();
            Assert.That(view.Root.Q<Label>("description").text, Is.EqualTo("While disabled"));
            document.enabled = false;
            document.enabled = true;
            view.RefreshBindings();
            view.ViewModel.Progress.Value = 0.5f;
            Assert.That(view.Root.Q<ProgressBar>("progress").value, Is.EqualTo(0.5f));
            Assert.That(view.Root.Q<Label>("description").text, Is.EqualTo("While disabled"));
        }

        [Test]
        public void Loading_ReceivesCurrentSnapshotAndUnsubscribesOnDisconnect()
        {
            _model = ScriptableObject.CreateInstance<LoadingViewModel>();
            var model = (LoadingViewModel)_model;
            var source = new LoadingSource();
            model.Initialize(source);
            Assert.That(model.Description.Value, Is.EqualTo("Initial"));
            Assert.That(model.Progress.Value, Is.EqualTo(0.25f));
            source.Publish(new LoadingSnapshot("Next", 0.75f, true, false));
            Assert.That(model.ProgressText.Value, Is.EqualTo("75%"));
            Assert.That(model.Visible.Value, Is.True);
            Assert.That(model.Transparent.Value, Is.False);
            model.Disconnect();
            source.Publish(new LoadingSnapshot("Detached", 0, false, true));
            Assert.That(model.Description.Value, Is.EqualTo("Next"));
        }

        private T CreateView<T>(string name) where T : DocumentView
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/UI/" + name + " Document.prefab");
            _root = UnityEngine.Object.Instantiate(prefab);
            T view = _root.GetComponent<T>();
            view.RefreshBindings();
            return view;
        }

        private sealed class PlacementSource : IArenaPlacementService
        {
            private Action<ArenaPlacementStatus> _changed;

            public ArenaPlacementStatus Status { get; private set; }
            public int ConfirmCount { get; private set; }
            public int FlatCount { get; private set; }
            public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;

            public event Action<ArenaPlacementStatus> StatusChanged
            {
                add
                {
                    _changed += value;
                }
                remove
                {
                    _changed -= value;
                }
            }

            public void Confirm()
            {
                ConfirmCount++;
            }

            public void UseFlat()
            {
                FlatCount++;
            }

            public void Publish(bool canConfirm)
            {
                Status = new ArenaPlacementStatus("Ready", true, true, canConfirm, true, false);
                _changed?.Invoke(Status);
            }
        }

        private sealed class LoadingSource : ILoadingStateSource
        {
            public LoadingSnapshot Snapshot { get; private set; } = new("Initial", 0.25f, true, false);
            public event Action<LoadingSnapshot> Changed;

            public void Publish(LoadingSnapshot snapshot)
            {
                Snapshot = snapshot;
                Changed?.Invoke(snapshot);
            }
        }
    }
}
