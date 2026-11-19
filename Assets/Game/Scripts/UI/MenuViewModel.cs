using System;
using System.Collections.Generic;
using Game.Core.Match;
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
        private Action _quit;
        private ReactiveProperty<bool> _homeVisible;
        public ReactiveProperty<bool> HomeVisible => _homeVisible ??= Property<bool>(true);
        private ReactiveProperty<bool> _createVisible;
        public ReactiveProperty<bool> CreateVisible => _createVisible ??= Property<bool>(false);
        private ReactiveProperty<bool> _joinVisible;
        public ReactiveProperty<bool> JoinVisible => _joinVisible ??= Property<bool>(false);
        private ReactiveProperty<bool> _settingsVisible;
        public ReactiveProperty<bool> SettingsVisible => _settingsVisible ??= Property<bool>(false);
        private ReactiveProperty<string> _selectedGameText;
        public ReactiveProperty<string> SelectedGameText => _selectedGameText ??= Property<string>("");
        private ReactiveProperty<int> _selectedOption;
        public ReactiveProperty<int> SelectedOption => _selectedOption ??= Property<int>(0);

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
            Func<bool> useAR,
            Action quit = null)
        {
            Disconnect();
            _controller = controller;
            _training = training;
            _toggleArenaMode = toggleArenaMode;
            _useAR = useAR;
            _quit = quit;
            ShowPage(true, false, false);
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
            if (_controller.SelectedGameMode == GameMode.RoomFreeForAll)
            {
                SelectedOption.Value = 2;
                SelectedGameText.Value = "Комната · каждый за себя. Сканирование пола и мебели, проверка границ перед боем.";
            }
            else if (useAR)
            {
                SelectedOption.Value = 1;
                SelectedGameText.Value = "Командная дуэль на столе в AR. Арена разделена рекой на два берега.";
            }
            else
            {
                SelectedOption.Value = 0;
                SelectedGameText.Value = "Командная дуэль с обычной камерой. Размещение и сканирование не нужны.";
            }

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
            if (!CanStart.Value)
            {
                return;
            }

            if (_controller?.SelectedGameMode == GameMode.RoomFreeForAll && _useAR?.Invoke() == false)
            {
                _toggleArenaMode?.Invoke();
            }

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
            ShowPage(true, false, false);
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

        public void OpenCreate()
        {
            if (CanStart.Value)
            {
                ShowPage(false, true, false);
            }
        }

        public void OpenJoin()
        {
            if (CanStart.Value)
            {
                ShowPage(false, false, true);
            }
        }

        public void Back()
        {
            _controller?.Cancel();
            ShowPage(true, false, false);
            Tick();
        }

        public void SelectFlatDuel()
        {
            SelectGame(GameMode.TeamDuel, false);
        }

        public void SelectTableDuel()
        {
            SelectGame(GameMode.TeamDuel, true);
        }

        public void SelectRoom()
        {
            SelectGame(GameMode.RoomFreeForAll, true);
        }

        public void Quit()
        {
            if (_quit != null)
            {
                _quit.Invoke();
            }
            else
            {
                Application.Quit();
            }
        }

        private void SelectGame(GameMode mode, bool useAR)
        {
            if (!CanStart.Value || _controller == null)
            {
                return;
            }

            _controller.SetGameMode(mode);
            if (_useAR?.Invoke() != useAR)
            {
                _toggleArenaMode?.Invoke();
            }

            Tick();
        }

        private void ShowPage(bool home, bool create, bool join)
        {
            HomeVisible.Value = home;
            CreateVisible.Value = create;
            JoinVisible.Value = join;
            SettingsVisible.Value = create || join;
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
            _quit = null;
            _addresses = Array.Empty<string>();
            Servers.Value = Array.Empty<string>();
        }
    }
}
