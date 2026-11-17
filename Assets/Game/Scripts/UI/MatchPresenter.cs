using System;
using System.Collections.Generic;
using System.Text;
using Game.Core.Configs;
using Game.Core.Economy;
using Game.Core.Match;
using Game.Net;
using Game.UI.Foundation;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchPresenter : IDisposable
    {
        private static readonly string[] BuyKeys = { "buyRange", "buySpeed", "buyRadius", "buyDamage" };
        private static readonly string[] UpgradeNames = { "Дальность", "Скорость", "Радиус", "Урон" };
        private readonly ReactiveDocument _view;
        private readonly UiState _model;
        private readonly HeroConfigData _heroConfig;
        private readonly HookConfigData _hookConfig;
        private readonly EconomyConfigData _economy;
        private readonly Action _leave, _reset;
        private readonly Func<string> _training;
        private readonly Queue<string> _kills = new();
        private readonly HashSet<string> _bars = new();
        private readonly HashSet<string> _visibleBars = new();
        private HeroNet _hero;
        private MatchStateNet _match;
        private MatchSummary _summary;
        private HeroStatus _status;
        private Camera _camera;
        private IDisposable _heroSubscriptions, _matchSubscriptions;

        public MatchPresenter(ReactiveDocument view, HeroConfigData hero, HookConfigData hook, EconomyConfigData economy,
            Action leave, Action reset, Func<string> training)
        {
            _view = view;
            _model = view.Model;
            _heroConfig = hero;
            _hookConfig = hook;
            _economy = economy;
            _leave = leave;
            _reset = reset;
            _training = training;
            _view.ActionRequested += Handle;
            Tick();
        }

        public void Tick()
        {
            Bind();
            bool network = !ReferenceEquals(_match, null);
            _model.Visible("trainingPanel", !network);
            _model.Visible("lobby", network && _summary.Phase == MatchPhase.Lobby);
            _model.Visible("results", network && _summary.Phase == MatchPhase.Results);
            _model.Visible("hud", network && (_summary.Phase == MatchPhase.Playing || _summary.Phase == MatchPhase.Countdown));
            if (!network)
            {
                _model.Text("trainingStatus", _training?.Invoke() ?? string.Empty);
            }

            if (!ReferenceEquals(_hero, null) && network)
            {
                double now = _hero.NetworkNow;
                double seconds = Math.Max(0, _summary.MatchEnd - now);
                _model.Text("timer", $"{(int)seconds / 60:00}:{(int)seconds % 60:00}");
                _model.Number("cooldown", 1 - Mathf.Clamp01((float)(_hero.HookCooldownEnd - now) / Math.Max(0.001f, _hookConfig.Cooldown)));
                _model.Text("cooldown", "Хук");
                _model.Text("health", _hero.Alive ? $"HP {_hero.Hp}/{_heroConfig.MaxHp}"
                    : $"Возрождение: {Math.Max(0, _status.RespawnAt - now):0.0}");
                int count = _summary.Phase == MatchPhase.Countdown ? Mathf.CeilToInt((float)Math.Max(0, _summary.CountdownEnd - now)) : 0;
                _model.Text("countdown", count > 0 ? count.ToString() : string.Empty);
            }
            else
            {
                _model.Text("countdown", string.Empty);
            }

            UpdateBars();
        }

        private void Bind()
        {
            if (!ReferenceEquals(_match, MatchStateNet.Active))
            {
                _matchSubscriptions?.Dispose();
                _matchSubscriptions = null;
                _match = MatchStateNet.Active;
                _kills.Clear();
                _model.Text("feed", string.Empty);
                if (!ReferenceEquals(_match, null))
                {
                    _matchSubscriptions = Disposable.Combine(
                    _match.State.Subscribe(value =>
                    {
                        if (_summary.Phase != value.Phase && (value.Phase == MatchPhase.Lobby || value.Phase == MatchPhase.Countdown))
                        {
                            _kills.Clear();
                            _model.Text("feed", string.Empty);
                        }
                        _summary = value;
                        _model.Text("score", $"A  {value.ScoreA} : {value.ScoreB}  B");
                        RenderLobby();
                        RenderResults();
                    }),
                    _match.ContentRevision.Subscribe(_ =>
                    {
                        RenderLobby();
                        RenderResults();
                    }),
                    _match.Events.Kills.Subscribe(value =>
                    {
                        _kills.Enqueue($"{PlayerName(value.KillerId)} → {PlayerName(value.VictimId)}");
                        while (_kills.Count > 4)
                        {
                            _kills.Dequeue();
                        }

                        _model.Text("feed", string.Join("\n", _kills));
                    }));
                }
            }
            if (ReferenceEquals(_hero, HeroNet.Local))
            {
                return;
            }

            _heroSubscriptions?.Dispose();
            _heroSubscriptions = null;
            _hero = HeroNet.Local;
            if (!ReferenceEquals(_hero, null))
            {
                _heroSubscriptions = Disposable.Combine(
                _hero.Health.Subscribe(value => _model.Text("health", $"HP {value}/{_heroConfig.MaxHp}")),
                _hero.Status.Subscribe(value =>
                {
                    _status = value;
                    RenderShop();
                }));
            }

            RenderLobby();
        }

        private void RenderLobby()
        {
            if (ReferenceEquals(_match, null))
            {
                return;
            }

            _model.Text("teamA", TeamText(Team.A));
            _model.Text("teamB", TeamText(Team.B));
            _model.Text("ready", IsReady() ? "Не готов" : "Готов");
            _model.Visible("start", _match.IsHost);
            _model.Enabled("start", _match.CanStart);
            _model.Visible("rematch", _match.IsHost);
            _model.Visible("returnToLobby", _match.IsHost);
        }

        private string TeamText(Team team)
        {
            var text = new StringBuilder(team == Team.A ? "КОМАНДА A\n\n" : "КОМАНДА B\n\n");
            foreach (var player in _match.PlayerRows)
            {
                if (player.Team == team)
                {
                    text.AppendLine($"{player.Nickname}  {(player.Connected ? player.Ready ? "готов" : "…" : "обрыв")}");
                }
            }

            return text.ToString();
        }

        private bool IsReady()
        {
            if (!ReferenceEquals(_match, null) && !ReferenceEquals(_hero, null))
            {
                foreach (var player in _match.PlayerRows)
                {
                    if (player.SessionPlayerId == _hero.SessionPlayerId)
                    {
                        return player.Ready;
                    }
                }
            }

            return false;
        }

        private void RenderShop()
        {
            _model.Text("gold", $"Золото: {_status.Gold}");
            int[] levels = { _status.RangeLevel, _status.SpeedLevel, _status.RadiusLevel, _status.DamageLevel };
            for (int i = 0; i < BuyKeys.Length; i++)
            {
                int level = levels[i];
                bool max = level >= _hookConfig.MaxLevel || level >= _economy.UpgradePrices.Count;
                int price = max ? 0 : _economy.UpgradePrices[level];
                _model.Text(BuyKeys[i], $"{UpgradeNames[i]} {level}/{_hookConfig.MaxLevel} · {(max ? "MAX" : price.ToString())}");
                _model.Enabled(BuyKeys[i], !max && _status.Gold >= price);
            }
        }

        private string PlayerName(uint id)
        {
            if (id == 0)
            {
                return "Территория";
            }

            if (!ReferenceEquals(_match, null))
            {
                foreach (var row in _match.StatRows)
                {
                    if (row.PlayerId == id)
                    {
                        return row.Nickname ?? id.ToString();
                    }
                }
            }

            return id.ToString();
        }

        private void RenderResults()
        {
            if (ReferenceEquals(_match, null))
            {
                return;
            }

            var text = new StringBuilder(_summary.Winner == MatchWinner.Draw ? "НИЧЬЯ" : $"ПОБЕДА КОМАНДЫ {_summary.Winner}");
            text.AppendLine($"\n{_summary.ScoreA} : {_summary.ScoreB}\n\nИгрок · Убийства / Смерти / Помощь · Попадания\n");
            foreach (var row in _match.StatRows)
            {
                text.AppendLine($"{row.Nickname} ({row.Team}) · {row.Kills} / {row.Deaths} / {row.Assists} · {row.HookHits}");
            }

            _model.Text("statistics", text.ToString());
        }

        private void UpdateBars()
        {
            _visibleBars.Clear();
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
                if (_camera == null && Camera.allCamerasCount > 0)
                {
                    _camera = Camera.allCameras[0];
                }
            }
            Camera camera = _camera;
            if (camera != null && _view.Root?.panel != null)
            {
                foreach (var hero in HeroNet.ClientHeroes)
                {
                    if (ReferenceEquals(hero, null) || !hero.Alive || !hero.CanAct || !hero.IsViewAvailable)
                    {
                        continue;
                    }

                    Vector3 screen = camera.WorldToScreenPoint(hero.WorldViewPosition + Vector3.up * (1.8f * hero.ArenaScale));
                    if (screen.z <= 0)
                    {
                        continue;
                    }

                    Vector2 point = RuntimePanelUtils.ScreenToPanel(_view.Root.panel, new Vector2(screen.x, Screen.height - screen.y));
                    string key = "world_" + hero.SessionPlayerId;
                    _visibleBars.Add(key);
                    _bars.Add(key);
                    _model.Set(new UiParameter
                    {
                        Name = key,
                        Visible = true,
                        Enabled = true,
                        Number = Mathf.Clamp01((float)hero.Hp / _heroConfig.MaxHp),
                        Position = point - new Vector2(50, 0)
                    });
                }
            }

            foreach (string key in _bars)
            {
                if (!_visibleBars.Contains(key))
                {
                    _model.Visible(key, false);
                }
            }
        }

        private void Handle(string action, string value)
        {
            if (action == "leave")
            {
                _leave?.Invoke();
                return;
            }
            if (action == "resetTraining")
            {
                _reset?.Invoke();
                return;
            }
            var actions = !ReferenceEquals(_hero, null) ? _hero.MatchActions : null;
            if (actions == null)
            {
                return;
            }

            switch (action)
            {
                case "selectA":
                    actions.SelectTeam(Team.A);
                    break;
                case "selectB":
                    actions.SelectTeam(Team.B);
                    break;
                case "ready":
                    actions.SetReady(!IsReady());
                    break;
                case "start":
                    actions.StartMatch();
                    break;
                case "rematch":
                    actions.Rematch();
                    break;
                case "returnToLobby":
                    actions.ReturnToLobby();
                    break;
                default:
                    for (int i = 0; i < BuyKeys.Length; i++)
                    {
                        if (action == BuyKeys[i])
                        {
                            actions.BuyUpgrade((UpgradeId)i);
                        }
                    }

                    break;
            }
        }

        public void Dispose()
        {
            _view.ActionRequested -= Handle;
            _heroSubscriptions?.Dispose();
            _matchSubscriptions?.Dispose();
        }
    }
}
