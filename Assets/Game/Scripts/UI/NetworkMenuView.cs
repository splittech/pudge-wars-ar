using Game.Net;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public sealed class NetworkMenuView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _create;
        [SerializeField] private UnityEngine.UI.Button _join;
        [SerializeField] private UnityEngine.UI.Button _cancel;
        [SerializeField] private UnityEngine.UI.Button _training;
        [SerializeField] private TMP_InputField _address;
        [SerializeField] private TMP_InputField _nickname;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private UnityEngine.UI.Button _search;
        [SerializeField] private RectTransform _serverContent;
        [SerializeField] private UnityEngine.UI.Button _serverTemplate;
        [SerializeField] private GameObject _hostDisconnected;
        [SerializeField] private UnityEngine.UI.Button _returnToMenu;
        private NetworkSessionController _controller;
        private readonly List<UnityEngine.UI.Button> _serverRows = new();

        [Inject]
        public void Construct(NetworkSessionController controller) => _controller = controller;

        private void Start()
        {
            _nickname.SetTextWithoutNotify(_controller.Nickname);
            _address.SetTextWithoutNotify(_controller.LastAddress);
            _nickname.onValueChanged.AddListener(SetNickname);
            _create.onClick.AddListener(CreateGame);
            _join.onClick.AddListener(JoinGame);
            _cancel.onClick.AddListener(Cancel);
            _address.onSubmit.AddListener(SubmitAddress);
            _search.onClick.AddListener(SearchGames);
            _returnToMenu.onClick.AddListener(ReturnToMenu);
        }

        private void CreateGame() => _controller.CreateGame();
        private void JoinGame() => _controller.Connect(_address.text);
        private void SubmitAddress(string value) => _controller.Connect(value);
        private void Cancel() => _controller.Cancel();
        private void SetNickname(string value) => _controller.SetNickname(value);
        private void SearchGames() => _controller.SearchGames();
        private void ReturnToMenu() => _controller.AcknowledgeHostDisconnected();

        private void Update()
        {
            if (_controller == null)
                return;
            bool canStart = _controller.CanStart;
            _create.interactable = canStart;
            _join.interactable = canStart;
            _search.interactable = canStart;
            _address.interactable = canStart;
            _nickname.interactable = canStart;
            if (_training != null)
                _training.interactable = canStart;
            _cancel.gameObject.SetActive(_controller.CanCancel);
            if (_status.text != _controller.Message)
                _status.text = _controller.Message;
            _hostDisconnected.SetActive(_controller.HostDisconnected);
            UpdateServerRows(canStart);
        }

        private void UpdateServerRows(bool canStart)
        {
            var servers = _controller.Servers;
            while (_serverRows.Count < servers.Count)
            {
                int index = _serverRows.Count;
                var row = Instantiate(_serverTemplate, _serverContent);
                row.name = $"Server {index + 1}";
                row.onClick.AddListener(() => ConnectServer(index));
                _serverRows.Add(row);
            }
            for (int i = 0; i < _serverRows.Count; i++)
            {
                bool visible = _controller.IsSearching && i < servers.Count;
                var row = _serverRows[i];
                row.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                row.interactable = canStart;
                row.GetComponentInChildren<TMP_Text>().text = $"Join {servers[i].Address}";
            }
        }

        private void ConnectServer(int index)
        {
            if (index < _controller.Servers.Count)
                _controller.Connect(_controller.Servers[index].Address);
        }

        private void OnDisable()
        {
            if (_controller != null && _controller.IsSearching)
                _controller.Cancel();
        }

        private void OnDestroy()
        {
            if (_create != null) _create.onClick.RemoveListener(CreateGame);
            if (_join != null) _join.onClick.RemoveListener(JoinGame);
            if (_cancel != null) _cancel.onClick.RemoveListener(Cancel);
            if (_address != null) _address.onSubmit.RemoveListener(SubmitAddress);
            if (_nickname != null) _nickname.onValueChanged.RemoveListener(SetNickname);
            if (_search != null) _search.onClick.RemoveListener(SearchGames);
            if (_returnToMenu != null) _returnToMenu.onClick.RemoveListener(ReturnToMenu);
        }
    }
}
