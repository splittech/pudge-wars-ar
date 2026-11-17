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
        private Action _toggleArenaMode;
        private Func<bool> _useAR;
        [SerializeField] private UiState _runtimeModel;

        [Inject]
        public void Construct(NetworkSessionController controller) => _controller = controller;
        public void SetTrainingAction(Action training) => _training = training;
        public void SetArenaModeActions(Action toggle, Func<bool> useAR) { _toggleArenaMode = toggle; _useAR = useAR; }

        private void Start()
        {

            var document = ToolkitRuntime.Create(gameObject, "Menu", "MenuState");
            _runtimeModel = document.Model;
            _presenter = new MenuPresenter(_controller, document, () => _training?.Invoke(), _toggleArenaMode, _useAR);
        }

        private void Update() => _presenter?.Tick();
        private void OnDestroy() { _presenter?.Dispose(); _training = null; }
    }
}
