using System;
using Game.App;
using Game.Services.LoadingScreen;
using Game.UI.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.UI.Editor
{
    public static class MvvmUiMigration
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/UI/";
        private const string ModelFolder = "Assets/Game/UI/ViewModels/";

        [MenuItem("Game/UI/Configure MVVM documents")]
        public static void Configure()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            foreach (SceneSetup entry in setup)
            {
                if (string.IsNullOrEmpty(entry.path) && entry.isLoaded)
                {
                    throw new InvalidOperationException("Save the unnamed scene before configuring UI.");
                }
            }

            bool restoreScenes = Array.Exists(setup, entry => entry.isLoaded && !string.IsNullOrEmpty(entry.path));

            if (restoreScenes)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            try
            {
                EnsureFolder("Assets/Game/UI");
                EnsureFolder("Assets/Game/UI/ViewModels");
                CreateDocument<MenuDocument, MenuViewModel>("Menu", 0);
                CreateDocument<MatchDocument, MatchViewModel>("Match", 0);
                CreateDocument<PlacementDocument, PlacementViewModel>("Placement", 50);
                CreateDocument<OverlayDocument, OverlayViewModel>("Overlay", 100);
                CreateDocument<LoadingDocument, LoadingViewModel>("Loading", 1000);

                ConfigureMenuPrefab();
                ConfigureOverlayPrefab();
                ConfigureBoot();
                ConfigureMenu();
                ConfigureGameplay();
                RemoveLegacyAssets();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (restoreScenes)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        private static void CreateDocument<TView, TModel>(string layout, int order)
            where TView : DocumentView
            where TModel : UiViewModel
        {
            string modelPath = ModelFolder + layout + " ViewModel.asset";
            TModel model = AssetDatabase.LoadAssetAtPath<TModel>(modelPath);

            if (model == null)
            {
                model = ScriptableObject.CreateInstance<TModel>();
                AssetDatabase.CreateAsset(model, modelPath);
            }

            var root = new GameObject(layout + " Document");
            root.SetActive(false);

            try
            {
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/Resources/UI/Panel.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/Game/Resources/UI/" + layout + ".uxml");
                document.sortingOrder = order;
                TView view = root.AddComponent<TView>();
                view.Configure(model);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + layout + " Document.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TView Attach<TView>(GameObject owner, string layout) where TView : DocumentView
        {
            TView existing = owner.GetComponentInChildren<TView>(true);

            if (existing != null)
            {
                return existing;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + layout + " Document.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, owner.transform);
            return instance.GetComponent<TView>();
        }

        private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized field {field} on {owner.GetType().Name}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMenuPrefab()
        {
            EditPrefab("Network Menu", root =>
            {
                RemoveLegacyMenuComponent(root);
                Attach<MenuDocument>(root, "Menu");
            });
        }

        private static void ConfigureOverlayPrefab()
        {
            EditPrefab("Development Overlay", root => Attach<OverlayDocument>(root, "Overlay"));
        }

        private static void EditPrefab(string name, Action<GameObject> configure)
        {
            string path = PrefabFolder + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveLegacyMenuComponent(GameObject root)
        {
            // The removed NetworkMenuView is the only obsolete script on this known UI prefab.
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            throw new InvalidOperationException($"Missing {typeof(T).Name} in {scene.path}.");
        }

        private static void ConfigureBoot()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot Scene.unity");
            ProjectScope scope = Find<ProjectScope>(scene);
            SimpleLoadingScreen loading = Find<SimpleLoadingScreen>(scene);
            DevelopmentOverlay diagnostics = Find<DevelopmentOverlay>(scene);
            LoadingDocument loadingDocument = Attach<LoadingDocument>(loading.gameObject, "Loading");
            OverlayDocument overlayDocument = Attach<OverlayDocument>(diagnostics.gameObject, "Overlay");
            SetReference(scope, "_loadingDocument", loadingDocument);
            SetReference(scope, "_overlayDocument", overlayDocument);
            SetReference(scope, "_diagnostics", diagnostics);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureMenu()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Main Menu Scene.unity");
            MainMenuScope scope = Find<MainMenuScope>(scene);

            MenuDocument document = Find<MenuDocument>(scene);
            RemoveLegacyMenuComponent(document.transform.parent.gameObject);
            SetReference(scope, "_menuDocument", document);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureGameplay()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay Scene.unity");
            GameplayScope scope = Find<GameplayScope>(scene);
            SetReference(scope, "_matchDocument", Attach<MatchDocument>(scope.gameObject, "Match"));
            SetReference(scope, "_placementDocument", Attach<PlacementDocument>(scope.gameObject, "Placement"));
            EditorSceneManager.SaveScene(scene);
        }

        private static void RemoveLegacyAssets()
        {
            foreach (string name in new[] { "Menu", "Match", "Placement", "Overlay", "Loading" })
            {
                AssetDatabase.DeleteAsset("Assets/Game/Resources/UI/" + name + "State.asset");
            }

            foreach (string path in new[]
            {
                "Assets/Game/Scripts/UI/NetworkMenuView.cs",
                "Assets/Game/Scripts/UI/MenuPresenter.cs",
                "Assets/Game/Scripts/UI/MatchPresenter.cs",
                "Assets/Game/Scripts/UI/MatchInterface.cs",
                "Assets/Game/Scripts/UI/Foundation/UiState.cs",
                "Assets/Game/Scripts/UI/Foundation/ReactiveDocument.cs",
                "Assets/Game/Scripts/UI/Foundation/ToolkitRuntime.cs",
                "Assets/Game/Scripts/UI/Foundation/RuntimePanelOwner.cs"
            })
            {
                if (System.IO.File.Exists(path + ".meta"))
                {
                    System.IO.File.Delete(path + ".meta");
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
