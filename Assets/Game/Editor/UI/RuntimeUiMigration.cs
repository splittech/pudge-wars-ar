using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static class RuntimeUiMigration
    {
        [MenuItem("Game/UI/Remove legacy Canvas renderers from scenes")]
        public static void MigrateScenes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (var entry in setup)
            {
                if (string.IsNullOrEmpty(entry.path) && entry.isLoaded)
                {
                    throw new InvalidOperationException("Save the unnamed scene to an explicit path before migrating UI.");
                }
            }

            EditorSceneManager.SaveOpenScenes();
            try
            {
                foreach (string path in new[] { "Assets/Game/Prefabs/UI/Network Menu.prefab", "Assets/Game/Prefabs/UI/Development Overlay.prefab" })
                {
                    var prefab = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        Strip(prefab);
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                foreach (string path in new[] { "Assets/Game/Scenes/Boot Scene.unity", "Assets/Game/Scenes/Main Menu Scene.unity", "Assets/Game/Scenes/Gameplay Scene.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        Strip(root);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
        }

        private static void Strip(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                UnityEngine.Object.DestroyImmediate(text);
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                UnityEngine.Object.DestroyImmediate(graphic);
            }

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                UnityEngine.Object.DestroyImmediate(selectable);
            }

            foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                UnityEngine.Object.DestroyImmediate(raycaster);
            }

            foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                UnityEngine.Object.DestroyImmediate(scaler);
            }

            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }

            foreach (var renderer in root.GetComponentsInChildren<CanvasRenderer>(true))
            {
                UnityEngine.Object.DestroyImmediate(renderer);
            }
        }
    }
}
