using System;
using System.Collections.Generic;
using System.Linq;
using Alchemy.Editor;
using Alchemy.Inspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.Common
{
    /// <summary>
    /// Base window that rebuilds its entry list from the project on open and on every project change.
    /// </summary>
    internal abstract class AssetManagerWindow : AlchemyEditorWindow
    {
        private const float DefaultWidth = 500f;
        private const float DefaultHeight = 600f;
        private const float EntrySpacing = 10f;

        /// <summary>
        /// Names of the serialized list fields holding the entries.
        /// </summary>
        protected abstract IReadOnlyCollection<string> EntriesPropertyNames { get; }

        protected abstract void CollectEntries();

        /// <summary>
        /// Opens the window; a newly created window gets the default size, an existing one keeps its size.
        /// </summary>
        protected static void Open<T>(string title) where T : AssetManagerWindow
        {
            bool isNew = !HasOpenInstances<T>();
            T window = GetWindow<T>(title);

            if (isNew)
                window.position = new Rect(window.position.position, new Vector2(DefaultWidth, DefaultHeight));
        }

        protected virtual void OnEnable()
        {
            EditorApplication.projectChanged += Rebuild;
        }

        protected virtual void OnDisable()
        {
            EditorApplication.projectChanged -= Rebuild;
        }

        protected override void CreateGUI()
        {
            CollectEntries();
            ExpandEntries();
            base.CreateGUI();
            AddEntrySpacing();
        }

        // Entries are rebuilt from the project on every open, so by default there is nothing to persist.
        protected override void SaveWindowData(string dataPath) { }
        protected override void LoadWindowData(string dataPath) { }

        [Button]
        private void Refresh()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();
            CreateGUI();
        }

        private void ExpandEntries()
        {
            // Foldout state is cached per object and property path, so it applies to the inspector's SerializedObject too.
            SerializedObject serializedObject = new(this);

            foreach (string propertyName in EntriesPropertyNames)
            {
                SerializedProperty entries = serializedObject.FindProperty(propertyName);

                for (int i = 0; i < entries.arraySize; i++)
                    entries.GetArrayElementAtIndex(i).isExpanded = true;
            }
        }

        private void AddEntrySpacing()
        {
            List<ListView> listViews = rootVisualElement.Query<ListView>()
                .Where(view => EntriesPropertyNames.Contains(view.bindingPath))
                .ToList();

            foreach (ListView listView in listViews)
            {
                // Alchemy builds item content in bindItem, so wrap it to style only top-level entries, not nested lists.
                // The spacing goes on the content: DynamicHeight ListView measures items without their own padding.
                Action<VisualElement, int> bindItem = listView.bindItem;
                listView.bindItem = (element, index) =>
                {
                    bindItem(element, index);

                    foreach (VisualElement content in element.Children())
                        content.style.paddingTop = EntrySpacing;
                };
            }
        }
    }
}
