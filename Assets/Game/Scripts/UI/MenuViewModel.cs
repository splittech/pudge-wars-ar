using System;
using System.Collections.Generic;
using Game.Net;
using Game.UI.Foundation;
using R3;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Menu ViewModel")]
    public sealed class MenuViewModel : UiViewModel
    {
        private NetworkSessionController _controller;
        private Action _training;
        private Action _toggleArenaMode;
        private Func<bool> _useAR;
        private string[] _addresses = Array.Empty<string>();

        private ReactiveProperty<string> _nickname;
        public ReactiveProperty<string> Nickname => _nickname ??= Property<string>("");

        private ReactiveProperty<string> _address;
        public ReactiveProperty<string> Address => _address ??= Property<string>("");

        private ReactiveProperty<string> _message;
        public ReactiveProperty<string> Message => _message ??= Property<string>("");

        private ReactiveProperty<string> _arenaModeText;
        public ReactiveProperty<string> ArenaModeText => _arenaModeText ??= Property<string>("Режим: плоский");

        private ReactiveProperty<bool> _canStart;
        public ReactiveProperty<bool> CanStart => _canStart ??= Property<bool>(true);

        private ReactiveProperty<bool> _canCancel;
        public ReactiveProperty<bool> CanCancel => _canCancel ??= Property<bool>(false);

        private ReactiveProperty<bool> _hostDisconnected;
        public ReactiveProperty<bool> HostDisconnected => _hostDisconnected ??= Property<bool>(false);

        private ReactiveProperty<IReadOnlyList<string>> _servers;
        public ReactiveProperty<IReadOnlyList<string>> Servers => _servers ??= Property<IReadOnlyList<string>>(Array.Empty<string>());

        public void Initialize(
            NetworkSessionController controller,
            Action training,
            Action toggleArenaMode,
            Func<bool> useAR)
        {
            Disconnect();
            _controller = controller;
            _training = training;
            _toggleArenaMode = toggleArenaMode;
            _useAR = useAR;
            Nickname.Value = controller.Nickname;
            Address.Value = controller.LastAddress;
            Tick();
        }

        public override void Tick()
        {
            if (_controller == null)
            {
                return;
            }

            CanStart.Value = _controller.CanStart;
            CanCancel.Value = _controller.CanCancel;
            HostDisconnected.Value = _controller.HostDisconnected;
            Message.Value = _controller.Message;
            Nickname.Value = _controller.Nickname;
            bool useAR = _useAR?.Invoke() ?? false;
            ArenaModeText.Value = useAR ? "Режим: AR" : "Режим: плоский";

            IReadOnlyList<LanServer> servers = _controller.Servers;
            int count = _controller.IsSearching ? servers.Count : 0;
            bool changed = _addresses.Length != count;

            for (int i = 0; !changed && i < count; i++)
            {
                changed = _addresses[i] != servers[i].Address;
            }

            if (changed)
            {
                _addresses = new string[count];

                for (int i = 0; i < count; i++)
                {
                    _addresses[i] = servers[i].Address;
                }

                Servers.Value = Array.AsReadOnly(_addresses);
            }
        }

        public void SetNickname(string value)
        {
            _controller?.SetNickname(value);
            Nickname.Value = value;
        }

        public void SetAddress(string value)
        {
            Address.Value = value;
        }

        public void CreateGame()
        {
            _controller?.CreateGame();
            Tick();
        }

        public void Join()
        {
            _controller?.Connect(Address.Value);
            Tick();
        }

        public void Search()
        {
            _controller?.SearchGames();
            Tick();
        }

        public void Cancel()
        {
            _controller?.Cancel();
            Tick();
        }

        public void AcknowledgeDisconnect()
        {
            _controller?.AcknowledgeHostDisconnected();
            Tick();
        }

        public void Train()
        {
            if (CanStart.Value && _controller != null)
            {
                _training?.Invoke();
            }
        }

        public void ToggleArenaMode()
        {
            if (CanStart.Value && _controller != null)
            {
                _toggleArenaMode?.Invoke();
                Tick();
            }
        }

        public void JoinServer(int index)
        {
            if (index >= 0 && index < _addresses.Length)
            {
                Address.Value = _addresses[index];
                Join();
            }
        }

        public override void Disconnect()
        {
            if (_controller != null && _controller.IsSearching)
            {
                _controller.Cancel();
            }

            _controller = null;
            _training = null;
            _toggleArenaMode = null;
            _useAR = null;
            _addresses = Array.Empty<string>();
            Servers.Value = Array.Empty<string>();
        }
    }
}
