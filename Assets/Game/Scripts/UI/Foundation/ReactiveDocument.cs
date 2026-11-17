using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using R3;

namespace Game.UI.Foundation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ReactiveDocument : MonoBehaviour
    {
        [SerializeField] private UiState _model;
        private IDisposable _subscription;
        private readonly List<Action> _unbind = new();
        private readonly Dictionary<string, string> _styleClasses = new();
        private VisualElement _root;
        private bool _ownsModel;
        private UIDocument _document;
        public UiState Model => _model;
        public VisualElement Root => _root;
        public event Action<string, string> ActionRequested;

        public void Bind(UiState model, bool ownsModel = false)
        {
            Unbind();
            if (_ownsModel && _model != null && _model != model)
            {
                Release(_model);
            }

            _model = model;
            _ownsModel = ownsModel;
            Attach();
        }

        private void OnEnable()
        {
            if (_model != null)
            {
                Attach();
            }
        }
        private void OnDisable() => Unbind();

        private void Update()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (ReferenceEquals(_document.rootVisualElement, _root))
            {
                return;
            }

            if (_document.rootVisualElement == null)
            {
                Unbind();
                _root = null;
            }
            else
            {
                Attach();
            }
        }

        private void Attach()
        {
            Unbind();
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;
            if (_root == null || _model == null)
            {
                return;
            }

            _root.pickingMode = PickingMode.Ignore;
            _root.Query<Button>().ForEach(button =>
            {
                Action callback = () => ActionRequested?.Invoke(button.name, null);
                button.clicked += callback;
                _unbind.Add(() => button.clicked -= callback);
            });
            _root.Query<TextField>().ForEach(field =>
            {
                EventCallback<ChangeEvent<string>> changed = evt => ActionRequested?.Invoke(field.name, evt.newValue);
                EventCallback<KeyDownEvent> submit = evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        ActionRequested?.Invoke(field.name + "Submit", field.value);
                    }
                };
                field.RegisterValueChangedCallback(changed);
                field.RegisterCallback(submit);
                _unbind.Add(() =>
                {
                    field.UnregisterValueChangedCallback(changed);
                    field.UnregisterCallback(submit);
                });
            });
            foreach (var parameter in _model.Parameters)
            {
                Apply(parameter);
            }

            _subscription = _model.Changed.Subscribe(Apply);
        }

        private void Apply(UiParameter parameter)
        {
            if (string.IsNullOrEmpty(parameter.Name))
            {
                return;
            }

            VisualElement element = _root.Q(parameter.Name);
            if (element == null && parameter.Name.StartsWith("world_", StringComparison.Ordinal))
            {
                var layer = _root.Q("worldBars");
                if (layer == null)
                {
                    return;
                }

                element = new ProgressBar { name = parameter.Name, lowValue = 0, highValue = 1, pickingMode = PickingMode.Ignore };
                element.AddToClassList("world-health");
                layer.Add(element);
            }
            if (element == null)
            {
                return;
            }

            element.EnableInClassList("hidden", !parameter.Visible);
            element.SetEnabled(parameter.Enabled);
            if (_styleClasses.TryGetValue(parameter.Name, out string previousClass) && !string.IsNullOrEmpty(previousClass))
            {
                element.RemoveFromClassList(previousClass);
            }

            if (!string.IsNullOrEmpty(parameter.StyleClass))
            {
                element.AddToClassList(parameter.StyleClass);
            }

            _styleClasses[parameter.Name] = parameter.StyleClass;
            if (element is ProgressBar progress)
            {
                progress.value = parameter.Number;
                if (parameter.Text != null)
                {
                    progress.title = parameter.Text;
                }
            }
            else if (parameter.Text != null && element is TextField field)
            {
                field.SetValueWithoutNotify(parameter.Text);
            }
            else if (parameter.Text != null && element is TextElement text)
            {
                text.text = parameter.Text;
            }

            if (parameter.Name.StartsWith("world_", StringComparison.Ordinal))
            {
                element.style.translate = new Translate(new Length(parameter.Position.x), new Length(parameter.Position.y), 0);
            }

            if (parameter.Rows != null && element is ScrollView)
            {
                element.Clear();
                for (int i = 0; i < parameter.Rows.Length; i++)
                {
                    string index = i.ToString();
                    var row = new Button(() => ActionRequested?.Invoke(parameter.Name, index)) { text = parameter.Rows[i] };
                    row.AddToClassList("server-row");
                    element.Add(row);
                }
            }
        }

        private void Unbind()
        {
            _subscription?.Dispose();
            _subscription = null;
            foreach (var callback in _unbind)
            {
                callback();
            }

            _unbind.Clear();
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private void OnDestroy()
        {
            Unbind();
            ActionRequested = null;
            if (_ownsModel && _model != null)
            {
                Release(_model);
            }
        }
    }
}
