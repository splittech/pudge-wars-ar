using System;
using Game.Net;
using Game.UI.Foundation;

namespace Game.UI
{
    public sealed class MenuPresenter : IDisposable
    {
        private static readonly string[] StartKeys = { "create", "join", "search", "nickname", "address", "training" };
        private readonly NetworkSessionController _controller;
        private readonly ReactiveDocument _view;
        private readonly UiState _model;
        private readonly Action _training;
        private string[] _addresses = Array.Empty<string>();

        public MenuPresenter(NetworkSessionController controller, ReactiveDocument view, Action training)
        {
            _controller = controller; _view = view; _model = view.Model; _training = training;
            _model.Text("nickname", controller.Nickname);
            _model.Text("address", controller.LastAddress);
            _view.ActionRequested += Handle;
            Tick();
        }

        public void Tick()
        {
            bool canStart = _controller.CanStart;
            foreach (string key in StartKeys)
                _model.Enabled(key, canStart);
            _model.Text("nickname", _controller.Nickname);
            _model.Text("message", _controller.Message);
            _model.Visible("cancel", _controller.CanCancel);
            _model.Visible("disconnected", _controller.HostDisconnected);
            var servers = _controller.Servers;
            int count = _controller.IsSearching ? servers.Count : 0;
            bool changed = _addresses.Length != count;
            for (int i = 0; !changed && i < count; i++) changed = _addresses[i] != servers[i].Address;
            if (changed)
            {
                _addresses = new string[count];
                for (int i = 0; i < count; i++) _addresses[i] = servers[i].Address;
                _model.Rows("servers", _addresses);
            }
            _model.Enabled("servers", canStart);
        }

        private void Handle(string action, string value)
        {
            switch (action)
            {
                case "nickname": _controller.SetNickname(value); break;
                case "address": _model.Text("address", value); break;
                case "addressSubmit":
                case "join": _controller.Connect(_model.Read("address").Text); break;
                case "create": _controller.CreateGame(); break;
                case "search": _controller.SearchGames(); break;
                case "cancel": _controller.Cancel(); break;
                case "acknowledge": _controller.AcknowledgeHostDisconnected(); break;
                case "training": if (_controller.CanStart) _training?.Invoke(); break;
                case "servers":
                    if (int.TryParse(value, out int index) && index >= 0 && index < _addresses.Length)
                    { _model.Text("address", _addresses[index]); _controller.Connect(_addresses[index]); }
                    break;
            }
            Tick();
        }

        public void Dispose()
        {
            _view.ActionRequested -= Handle;
            if (_controller.IsSearching) _controller.Cancel();
        }
    }
}
