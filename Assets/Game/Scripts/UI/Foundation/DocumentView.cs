using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Foundation
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class DocumentView : MonoBehaviour
    {
        [SerializeField] private UiViewModel _template;
        [SerializeField] private UiViewModel _runtimeModel;

        private readonly List<IDisposable> _subscriptions = new();
        private readonly List<Action> _unbind = new();
        private UIDocument _document;
        private VisualElement _root;

        public UiViewModel Template => _template;
        public UiViewModel Model => EnsureModel();
        public VisualElement Root => _root;

        public void Configure(UiViewModel template)
        {
            if (_runtimeModel != null)
            {
                throw new InvalidOperationException("Configure the document before creating its runtime model.");
            }

            _template = template;
        }

        public void RefreshBindings()
        {
            Unbind();

            if (_template == null)
            {
                if (Application.isPlaying)
                {
                    throw new InvalidOperationException($"Missing ViewModel template on {gameObject.name}.");
                }

                return;
            }

            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;

            if (_root == null)
            {
                return;
            }

            _root.pickingMode = PickingMode.Ignore;
            BindElements();
        }

        protected abstract void BindElements();

        protected T Element<T>(string name) where T : VisualElement
        {
            T element = _root.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException($"Missing UI element '{name}' in {gameObject.name}.");
            }

            return element;
        }

        protected void Observe<T>(Observable<T> property, Action<T> apply)
        {
            _subscriptions.Add(property.Subscribe(apply));
        }

        protected void Text(string name, Observable<string> property)
        {
            TextElement element = Element<TextElement>(name);
            Observe(property, value => element.text = value);
        }

        protected void Visible(string name, Observable<bool> property)
        {
            VisualElement element = Element<VisualElement>(name);
            Observe(property, value => element.EnableInClassList("hidden", !value));
        }

        protected void Enabled(string name, Observable<bool> property)
        {
            VisualElement element = Element<VisualElement>(name);
            Observe(property, element.SetEnabled);
        }

        protected void Button(string name, Action action)
        {
            UnityEngine.UIElements.Button button = Element<UnityEngine.UIElements.Button>(name);
            button.clicked += action;
            _unbind.Add(() => button.clicked -= action);
        }

        protected void Field(
            string name,
            Observable<string> property,
            Action<string> changed,
            Action submit = null)
        {
            TextField field = Element<TextField>(name);
            Observe(property, field.SetValueWithoutNotify);
            EventCallback<ChangeEvent<string>> callback = evt => changed(evt.newValue);
            EventCallback<KeyDownEvent> key = evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    submit?.Invoke();
                }
            };
            field.RegisterValueChangedCallback(callback);
            field.RegisterCallback(key);
            _unbind.Add(() =>
            {
                field.UnregisterValueChangedCallback(callback);
                field.UnregisterCallback(key);
            });
        }

        protected void Progress(string name, Observable<float> property)
        {
            ProgressBar element = Element<ProgressBar>(name);
            Observe(property, value => element.value = value);
        }

        private UiViewModel EnsureModel()
        {
            if (_runtimeModel == null)
            {
                if (_template == null)
                {
                    throw new InvalidOperationException($"Missing ViewModel template on {gameObject.name}.");
                }

                _runtimeModel = Instantiate(_template);
                _runtimeModel.name = _template.name + " (Runtime)";
            }

            return _runtimeModel;
        }

        private void OnEnable()
        {
            RefreshBindings();
        }

        private void LateUpdate()
        {
            _document ??= GetComponent<UIDocument>();

            if (!ReferenceEquals(_root, _document.rootVisualElement))
            {
                RefreshBindings();
            }

            _runtimeModel?.Tick();
            UpdateView();
        }

        protected virtual void UpdateView()
        {
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Unbind()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();

            foreach (Action callback in _unbind)
            {
                callback();
            }

            _unbind.Clear();
        }

        protected virtual void OnDestroy()
        {
            Unbind();

            if (_runtimeModel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeModel);
                }
                else
                {
                    DestroyImmediate(_runtimeModel);
                }
            }
        }
    }
}
