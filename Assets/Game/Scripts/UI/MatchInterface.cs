using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.Configs;
using Game.Core.Economy;
using Game.Core.Match;
using Game.Net;
using R3;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class MatchInterface : MonoBehaviour
    {
        private GameObject _lobby, _hud, _results;
        private TMP_Text _teamA, _teamB, _score, _timer, _health, _gold, _countdown, _resultText, _feed;
        private UnityEngine.UI.Button _ready, _start, _rematch, _backToLobby;
        private UnityEngine.UI.Image _cooldown;
        private readonly UnityEngine.UI.Button[] _buy = new UnityEngine.UI.Button[4];
        private readonly TMP_Text[] _buyText = new TMP_Text[4];
        private readonly Dictionary<HeroNet, WorldHealthBar> _bars = new();
        private readonly Queue<string> _kills = new();
        private HeroConfigData _heroConfig;
        private HookConfigData _hookConfig;
        private EconomyConfigData _economy;
        private HeroNet _hero;
        private IMatchActions _actions;
        private MatchStateNet _match;
        private HeroStatus _status;
        private MatchSummary _summary;
        private IDisposable _heroSubscriptions, _matchSubscriptions;
        private int _countdownNumber = -1;
        private bool _contentDirty = true;
        private TMP_Text _readyText;
        private readonly List<HeroNet> _removedBars = new();
        private CancellationTokenSource _countdownCancellation;

        public static void Create(Canvas canvas, HeroConfig hero, HookConfig hook, EconomyConfig economy)
        {
            var root = new GameObject("Match Interface", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)root.transform);
            var view = root.AddComponent<MatchInterface>();
            view._heroConfig = hero.ToData(); view._hookConfig = hook.ToData(); view._economy = economy.ToData();
            view.Build();
        }

        private void Build()
        {
            _lobby = Panel("Lobby", transform, new Vector2(1000, 620));
            Label("Lobby Title", _lobby.transform, "ЛОББИ", new Vector2(0, 245), new Vector2(900, 60), 36);
            _teamA = Label("Team A", _lobby.transform, "", new Vector2(-235, 80), new Vector2(430, 240));
            _teamB = Label("Team B", _lobby.transform, "", new Vector2(235, 80), new Vector2(430, 240));
            Button("Select A", _lobby.transform, "Команда A", new Vector2(-235, -90), () => _actions?.SelectTeam(Team.A));
            Button("Select B", _lobby.transform, "Команда B", new Vector2(235, -90), () => _actions?.SelectTeam(Team.B));
            _ready = Button("Ready", _lobby.transform, "Готов", new Vector2(-235, -205), ToggleReady);
            _readyText = _ready.GetComponentInChildren<TMP_Text>();
            _start = Button("Start", _lobby.transform, "Начать", new Vector2(235, -205), () => _actions?.StartMatch());

            _hud = new GameObject("HUD", typeof(RectTransform)); _hud.transform.SetParent(transform, false);
            Stretch((RectTransform)_hud.transform);
            var top = Panel("Score Bar", _hud.transform, new Vector2(850, 95));
            Anchor((RectTransform)top.transform, new Vector2(0.5f, 1), new Vector2(0, -70));
            _score = Label("Score", top.transform, "", new Vector2(-200, 0), new Vector2(380, 70), 32);
            _timer = Label("Timer", top.transform, "", new Vector2(250, 0), new Vector2(250, 70), 32);
            var bottom = Panel("Player Bar", _hud.transform, new Vector2(880, 120));
            Anchor((RectTransform)bottom.transform, new Vector2(0.5f, 0), new Vector2(0, 85));
            _health = Label("Health", bottom.transform, "", new Vector2(-260, 0), new Vector2(300, 70));
            _gold = Label("Gold", bottom.transform, "", new Vector2(30, 0), new Vector2(250, 70));
            var ring = new GameObject("Hook Cooldown", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            ring.transform.SetParent(bottom.transform, false);
            Rect((RectTransform)ring.transform, new Vector2(325, 0), new Vector2(64, 64));
            _cooldown = ring.GetComponent<UnityEngine.UI.Image>(); _cooldown.raycastTarget = false;
            _cooldown.sprite = MakeRingSprite(); _cooldown.type = UnityEngine.UI.Image.Type.Filled;
            _cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; _cooldown.color = new Color(0.25f, 0.8f, 1);
            Label("Hook Label", bottom.transform, "Хук", new Vector2(325, 0), new Vector2(65, 40), 18);
            var shop = Panel("Shop", _hud.transform, new Vector2(340, 340));
            Anchor((RectTransform)shop.transform, new Vector2(1, 0), new Vector2(-195, 265));
            Label("Shop Title", shop.transform, "УЛУЧШЕНИЯ", new Vector2(0, 130), new Vector2(300, 45));
            for (int i = 0; i < 4; i++)
            {
                var id = (UpgradeId)i;
                _buy[i] = Button("Buy " + id, shop.transform, "", new Vector2(0, 70 - i * 65), () => _actions?.BuyUpgrade(id));
                ((RectTransform)_buy[i].transform).sizeDelta = new Vector2(300, 55);
                _buyText[i] = _buy[i].GetComponentInChildren<TMP_Text>();
            }
            _feed = Label("Kill Feed", _hud.transform, "", Vector2.zero, new Vector2(550, 180), 24);
            Anchor((RectTransform)_feed.transform, new Vector2(1, 1), new Vector2(-300, -180));
            _countdown = Label("Countdown", transform, "", Vector2.zero, new Vector2(250, 200), 100);

            _results = Panel("Results", transform, new Vector2(1050, 680));
            _resultText = Label("Statistics", _results.transform, "", new Vector2(0, 70), new Vector2(960, 470), 28);
            _rematch = Button("Rematch", _results.transform, "Реванш", new Vector2(-240, -240), () => _actions?.Rematch());
            _backToLobby = Button("Return To Lobby", _results.transform, "В лобби", new Vector2(240, -240), () => _actions?.ReturnToLobby());
            _lobby.SetActive(false); _hud.SetActive(false); _results.SetActive(false);
        }

        private void Update()
        {
            Bind();
            if (ReferenceEquals(_match, null) || ReferenceEquals(_hero, null)) return;
            bool lobby = _summary.Phase == MatchPhase.Lobby;
            bool results = _summary.Phase == MatchPhase.Results;
            _lobby.SetActive(lobby); _results.SetActive(results);
            _hud.SetActive(_summary.Phase == MatchPhase.Playing || _summary.Phase == MatchPhase.Countdown);
            if (_contentDirty)
            {
                _teamA.text = TeamText(Team.A); _teamB.text = TeamText(Team.B);
                _readyText.text = IsReady() ? "Не готов" : "Готов";
                if (results) RenderResults();
                _contentDirty = false;
            }
            _start.gameObject.SetActive(_match.IsHost); _start.interactable = _match.CanStart;
            _rematch.gameObject.SetActive(_match.IsHost); _backToLobby.gameObject.SetActive(_match.IsHost);
            _timer.text = FormatTime(Math.Max(0, _summary.MatchEnd - _hero.NetworkNow));
            _cooldown.fillAmount = 1 - Mathf.Clamp01((float)(_hero.HookCooldownEnd - _hero.NetworkNow) / _hookConfig.Cooldown);
            _cooldown.color = _hero.HookState.Phase == Game.Core.Hook.HookPhase.Idle ? new Color(0.25f, 0.8f, 1) : Color.gray;
            if (!_hero.Alive) _health.text = $"Возрождение: {Math.Max(0, _status.RespawnAt - _hero.NetworkNow):0.0}";
            int number = _summary.Phase == MatchPhase.Countdown
                ? Mathf.CeilToInt((float)Math.Max(0, _summary.CountdownEnd - _hero.NetworkNow)) : 0;
            if (number != _countdownNumber)
            {
                _countdownNumber = number;
                _countdownCancellation?.Cancel(); _countdownCancellation?.Dispose();
                _countdownCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                AnimateCountdown(number, _countdownCancellation.Token).Forget();
            }
            UpdateHealthBars();
        }

        private void Bind()
        {
            if (!ReferenceEquals(_match, MatchStateNet.Active))
            {
                _matchSubscriptions?.Dispose(); _match = MatchStateNet.Active;
                _kills.Clear(); _feed.text = string.Empty;
                if (!ReferenceEquals(_match, null)) _matchSubscriptions = Disposable.Combine(
                    _match.ContentRevision.Subscribe(_ => _contentDirty = true),
                    _match.State.Subscribe(value =>
                    {
                        if (_summary.Phase != value.Phase && (value.Phase == MatchPhase.Countdown || value.Phase == MatchPhase.Lobby))
                        { _kills.Clear(); _feed.text = string.Empty; }
                        _summary = value; _score.text = $"A  {value.ScoreA} : {value.ScoreB}  B";
                        _contentDirty = true;
                    }),
                    _match.Events.Kills.Subscribe(value =>
                    {
                        _kills.Enqueue($"{PlayerName(value.KillerId)} → {PlayerName(value.VictimId)}");
                        while (_kills.Count > 4) _kills.Dequeue();
                        _feed.text = string.Join("\n", _kills);
                    }));
            }
            if (ReferenceEquals(_hero, HeroNet.Local)) return;
            _heroSubscriptions?.Dispose(); _hero = HeroNet.Local; _actions = ReferenceEquals(_hero, null) ? null : _hero.MatchActions;
            _contentDirty = true;
            if (!ReferenceEquals(_hero, null)) _heroSubscriptions = Disposable.Combine(
                _hero.Health.Subscribe(value => _health.text = $"HP {value}/{_heroConfig.MaxHp}"),
                _hero.Status.Subscribe(value => { _status = value; RenderShop(); }));
        }

        private void RenderShop()
        {
            _gold.text = $"Золото: {_status.Gold}";
            int[] levels = { _status.RangeLevel, _status.SpeedLevel, _status.RadiusLevel, _status.DamageLevel };
            string[] names = { "Дальность", "Скорость", "Радиус", "Урон" };
            for (int i = 0; i < 4; i++)
            {
                int level = levels[i];
                bool max = level >= _hookConfig.MaxLevel || level >= _economy.UpgradePrices.Count;
                int price = max ? 0 : _economy.UpgradePrices[level];
                _buyText[i].text = $"{names[i]} {level}/{_hookConfig.MaxLevel}  ·  {(max ? "MAX" : price.ToString())}";
                _buy[i].interactable = !max && _status.Gold >= price;
            }
        }

        private string TeamText(Team team)
        {
            var text = new StringBuilder(team == Team.A ? "КОМАНДА A\n\n" : "КОМАНДА B\n\n");
            foreach (var player in _match.PlayerRows)
                if (player.Team == team) text.AppendLine($"{player.Nickname}  {(player.Connected ? player.Ready ? "готов" : "…" : "обрыв")}");
            return text.ToString();
        }

        private bool IsReady()
        {
            foreach (var player in _match.PlayerRows) if (player.SessionPlayerId == _hero.SessionPlayerId) return player.Ready;
            return false;
        }
        private void ToggleReady() { if (!ReferenceEquals(_hero, null) && !ReferenceEquals(_match, null)) _actions.SetReady(!IsReady()); }
        private string PlayerName(uint id)
        {
            if (id == 0) return "Территория";
            foreach (var row in _match.StatRows) if (row.PlayerId == id) return row.Nickname ?? id.ToString();
            return id.ToString();
        }
        private void RenderResults()
        {
            var text = new StringBuilder(_summary.Winner == MatchWinner.Draw ? "НИЧЬЯ" : $"ПОБЕДА КОМАНДЫ {_summary.Winner}");
            text.AppendLine($"\n{_summary.ScoreA} : {_summary.ScoreB}\n\nИгрок · Убийства / Смерти / Помощь · Попадания\n");
            foreach (var row in _match.StatRows)
                text.AppendLine($"{row.Nickname} ({row.Team}) · {row.Kills} / {row.Deaths} / {row.Assists} · {row.HookHits}");
            _resultText.text = text.ToString();
        }
        private async UniTaskVoid AnimateCountdown(int number, CancellationToken token)
        {
            _countdown.text = number > 0 ? number.ToString() : string.Empty;
            _countdown.transform.DOKill();
            if (number <= 0) return;
            _countdown.transform.localScale = Vector3.one * 1.4f;
            _countdown.transform.DOScale(1, 0.4f).SetUpdate(true);
            try { await UniTask.Delay(700, ignoreTimeScale: true, cancellationToken: token); }
            catch (OperationCanceledException) { return; }
            _countdown.text = string.Empty;
        }
        private void UpdateHealthBars()
        {
            _removedBars.Clear();
            foreach (var pair in _bars) if (ReferenceEquals(pair.Key, null) || !HeroNet.ClientHeroes.Contains(pair.Key)) _removedBars.Add(pair.Key);
            foreach (var hero in _removedBars) { Destroy(_bars[hero].gameObject); _bars.Remove(hero); }
            foreach (var hero in HeroNet.ClientHeroes)
            {
                if (ReferenceEquals(hero, null) || _bars.ContainsKey(hero)) continue;
                var root = new GameObject("Hero Health Bar"); root.transform.SetParent(transform, false);
                var bar = root.AddComponent<WorldHealthBar>(); bar.Initialize(hero, _heroConfig.MaxHp);
                _bars.Add(hero, bar);
            }
        }
        private void OnDestroy()
        {
            _heroSubscriptions?.Dispose(); _matchSubscriptions?.Dispose();
            _countdownCancellation?.Cancel(); _countdownCancellation?.Dispose();
            if (_countdown != null) _countdown.transform.DOKill();
            if (_cooldown != null && _cooldown.sprite != null)
            { Destroy(_cooldown.sprite.texture); Destroy(_cooldown.sprite); }
        }

        private static string FormatTime(double seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private static void Rect(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; }
        private static GameObject Panel(string name, Transform parent, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent, false);
            Rect((RectTransform)root.transform, Vector2.zero, size);
            var image = root.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(0.035f, 0.065f, 0.1f, 0.96f);
            image.raycastTarget = true; return root;
        }
        private static TMP_Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize = 28)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            Rect((RectTransform)root.transform, position, size);
            var text = root.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
            text.richText = false;
            return text;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string title, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var root = Panel(name, parent, new Vector2(360, 70)); ((RectTransform)root.transform).anchoredPosition = position;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.27f, 0.4f, 1);
            var button = root.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(action);
            var label = Label("Label", root.transform, title, Vector2.zero, new Vector2(350, 65), 26);
            Stretch((RectTransform)label.transform); return button;
        }
        private static Sprite MakeRingSprite()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                texture.SetPixel(x, y, radius >= 23 && radius <= 31 ? Color.white : Color.clear);
            }
            texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
        }
    }
}
