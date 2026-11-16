using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Foundation
{
    public static class ToolkitRuntime
    {
        public static ReactiveDocument Create(GameObject owner, string layout, string model, int order = 0)
        {
            var root = new GameObject(layout + " UI", typeof(UIDocument));
            root.transform.SetParent(owner.transform, false);
            var document = root.GetComponent<UIDocument>();
            var panelTemplate = Resources.Load<PanelSettings>("UI/Panel");
            if (panelTemplate == null) throw new System.InvalidOperationException("Missing UI panel settings: UI/Panel");
            var panel = Object.Instantiate(panelTemplate);
            panel.name = layout + " Panel (Runtime)";
            panel.sortingOrder = order;
            document.panelSettings = panel;
            document.sortingOrder = order;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/" + layout);
            if (document.visualTreeAsset == null) throw new System.InvalidOperationException("Missing UI layout: " + layout);
            var template = Resources.Load<UiState>("UI/" + model);
            if (template == null) throw new System.InvalidOperationException("Missing UI state template: " + model);
            var state = Object.Instantiate(template);
            state.name = model + " (Runtime)";
            var view = root.AddComponent<ReactiveDocument>();
            view.Bind(state, true);
            root.AddComponent<RuntimePanelOwner>().Panel = panel;
            return view;
        }

    }
}
