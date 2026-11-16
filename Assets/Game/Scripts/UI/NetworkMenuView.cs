using System;
using Game.Net;
using Game.UI.Foundation;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public sealed class NetworkMenuView : MonoBehaviour
    {
        private NetworkSessionController _controller;
        private MenuPresenter _presenter;
        private Action _training;
        [SerializeField] private UiState _runtimeModel;

        [Inject]
        public void Construct(NetworkSessionController controller) => _controller = controller;
        public void SetTrainingAction(Action training) => _training = training;

        private void Start()
        {

            var document = ToolkitRuntime.Create(gameObject, "Menu", "MenuState");
            _runtimeModel = document.Model;
            _presenter = new MenuPresenter(_controller, document, () => _training?.Invoke());
        }

        private void Update() => _presenter?.Tick();
        private void OnDestroy() { _presenter?.Dispose(); _training = null; }
    }
}
