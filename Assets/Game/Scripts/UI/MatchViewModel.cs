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

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Match ViewModel")]
    public sealed class MatchViewModel : UiViewModel
    {
        private static readonly string[] UpgradeNames = { "Дальность", "Скорость", "Радиус", "Урон" };
        private HeroConfigData _heroConfig;
        private HookConfigData _hookConfig;
        private EconomyConfigData _economy;
        private Action _leave;
        private Action _reset;
        private Func<string> _training;
        private readonly Queue<string> _kills = new();
        private readonly List<WorldHealthBar> _healthBarsBuffer = new(4);
        private HeroNet _hero;
        private MatchStateNet _match;
        private MatchSummary _summary;
        private HeroStatus _status;
        private IDisposable _heroSubscriptions;
        private IDisposable _matchSubscriptions;
        private bool _initialized;

        private ReactiveProperty<bool> _trainingVisible;
        public ReactiveProperty<bool> TrainingVisible => _trainingVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _lobbyVisible;
        public ReactiveProperty<bool> LobbyVisible => _lobbyVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _resultsVisible;
        public ReactiveProperty<bool> ResultsVisible => _resultsVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _hudVisible;
        public ReactiveProperty<bool> HudVisible => _hudVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _startVisible;
        public ReactiveProperty<bool> StartVisible => _startVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _canStart;
        public ReactiveProperty<bool> CanStart => _canStart ??= Property<bool>(false);

        private ReactiveProperty<bool> _hostActionsVisible;
        public ReactiveProperty<bool> HostActionsVisible => _hostActionsVisible ??= Property<bool>(false);

        private ReactiveProperty<string> _trainingStatus;
        public ReactiveProperty<string> TrainingStatus => _trainingStatus ??= Property<string>("");

        private ReactiveProperty<string> _timer;
        public ReactiveProperty<string> Timer => _timer ??= Property<string>("");

        private ReactiveProperty<string> _health;
        public ReactiveProperty<string> Health => _health ??= Property<string>("");

        private ReactiveProperty<string> _countdown;
        public ReactiveProperty<string> Countdown => _countdown ??= Property<string>("");

        private ReactiveProperty<string> _feed;
        public ReactiveProperty<string> Feed => _feed ??= Property<string>("");

        private ReactiveProperty<string> _score;
        public ReactiveProperty<string> Score => _score ??= Property<string>("");

        private ReactiveProperty<string> _teamA;
        public ReactiveProperty<string> TeamA => _teamA ??= Property<string>("");

        private ReactiveProperty<string> _teamB;
        public ReactiveProperty<string> TeamB => _teamB ??= Property<string>("");

        private ReactiveProperty<string> _readyText;
        public ReactiveProperty<string> ReadyText => _readyText ??= Property<string>("");

        private ReactiveProperty<string> _gold;
        public ReactiveProperty<string> Gold => _gold ??= Property<string>("");

        private ReactiveProperty<string> _statistics;
        public ReactiveProperty<string> Statistics => _statistics ??= Property<string>("");

        private ReactiveProperty<float> _cooldown;
        public ReactiveProperty<float> Cooldown => _cooldown ??= Property<float>(0f);

        private ReactiveProperty<IReadOnlyList<WorldHealthBar>> _healthBars;
        public ReactiveProperty<IReadOnlyList<WorldHealthBar>> HealthBars => _healthBars ??= Property<IReadOnlyList<WorldHealthBar>>(Array.Empty<WorldHealthBar>());

        private ReactiveProperty<string> _rangeUpgradeText;
        public ReactiveProperty<string> RangeUpgradeText => _rangeUpgradeText ??= Property<string>("");

        private ReactiveProperty<bool> _canBuyRange;
        public ReactiveProperty<bool> CanBuyRange => _canBuyRange ??= Property<bool>(false);

        private ReactiveProperty<string> _speedUpgradeText;
        public ReactiveProperty<string> SpeedUpgradeText => _speedUpgradeText ??= Property<string>("");

        private ReactiveProperty<bool> _canBuySpeed;
        public ReactiveProperty<bool> CanBuySpeed => _canBuySpeed ??= Property<bool>(false);

        private ReactiveProperty<string> _radiusUpgradeText;
        public ReactiveProperty<string> RadiusUpgradeText => _radiusUpgradeText ??= Property<string>("");

        private ReactiveProperty<bool> _canBuyRadius;
        public ReactiveProperty<bool> CanBuyRadius => _canBuyRadius ??= Property<bool>(false);

        private ReactiveProperty<string> _damageUpgradeText;
        public ReactiveProperty<string> DamageUpgradeText => _damageUpgradeText ??= Property<string>("");

        private ReactiveProperty<bool> _canBuyDamage;
        public ReactiveProperty<bool> CanBuyDamage => _canBuyDamage ??= Property<bool>(false);

        public void Initialize(
            HeroConfigData hero,
            HookConfigData hook,
            EconomyConfigData economy,
            Action leave,
            Action reset,
            Func<string> training)
        {
            Disconnect();
            _heroConfig = hero;
            _hookConfig = hook;
            _economy = economy;
            _leave = leave;
            _reset = reset;
            _training = training;
            _initialized = true;
            Tick();
        }

        public override void Tick()
        {
            if (!_initialized)
            {
                return;
            }

            Bind();
            bool network = !ReferenceEquals(_match, null);
            TrainingVisible.Value = !network;
            LobbyVisible.Value = network && _summary.Phase == MatchPhase.Lobby;
            ResultsVisible.Value = network && _summary.Phase == MatchPhase.Results;
            HudVisible.Value = network && (_summary.Phase == MatchPhase.Playing || _summary.Phase == MatchPhase.Countdown);
            if (!network)
            {
                TrainingStatus.Value = _training?.Invoke() ?? string.Empty;
            }

            if (!ReferenceEquals(_hero, null) && network)
            {
                double now = _hero.NetworkNow;
                double seconds = Math.Max(0, _summary.MatchEnd - now);
                Timer.Value = $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
                Cooldown.Value = 1 - Mathf.Clamp01((float)(_hero.HookCooldownEnd - now) / Math.Max(0.001f, _hookConfig.Cooldown));
                if (_hero.Alive)
                {
                    Health.Value = $"HP {_hero.Hp}/{_heroConfig.MaxHp}";
                }
                else
                {
                    double respawnSeconds = Math.Max(0, _status.RespawnAt - now);
                    Health.Value = $"Возрождение: {respawnSeconds:0.0}";
                }

                int count = 0;

                if (_summary.Phase == MatchPhase.Countdown)
                {
                    count = Mathf.CeilToInt((float)Math.Max(0, _summary.CountdownEnd - now));
                }

                Countdown.Value = count > 0 ? count.ToString() : string.Empty;
            }
            else
            {
                Countdown.Value = string.Empty;
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
                Feed.Value = string.Empty;
                if (!ReferenceEquals(_match, null))
                {
                    _matchSubscriptions = Disposable.Combine(
                        _match.State.Subscribe(value =>
                        {
                            if (_summary.Phase != value.Phase && (value.Phase == MatchPhase.Lobby || value.Phase == MatchPhase.Countdown))
                            {
                                _kills.Clear();
                                Feed.Value = string.Empty;
                            }
                            _summary = value;
                            Score.Value = $"A  {value.ScoreA} : {value.ScoreB}  B";
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

                            Feed.Value = string.Join("\n", _kills);
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
                    _hero.Health.Subscribe(value => Health.Value = $"HP {value}/{_heroConfig.MaxHp}"),
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

            TeamA.Value = TeamText(Team.A);
            TeamB.Value = TeamText(Team.B);
            ReadyText.Value = IsReady() ? "Не готов" : "Готов";
            StartVisible.Value = _match.IsHost;
            CanStart.Value = _match.CanStart;
            HostActionsVisible.Value = _match.IsHost;
        }

        private string TeamText(Team team)
        {
            string heading = team == Team.A ? "КОМАНДА A\n\n" : "КОМАНДА B\n\n";
            var text = new StringBuilder(heading);

            foreach (PlayerInfo player in _match.PlayerRows)
            {
                if (player.Team == team)
                {
                    string readiness = player.Ready ? "готов" : "…";

                    if (!player.Connected)
                    {
                        readiness = "обрыв";
                    }

                    text.AppendLine($"{player.Nickname}  {readiness}");
                }
            }

            return text.ToString();
        }

        private bool IsReady()
        {
            if (!ReferenceEquals(_match, null) && !ReferenceEquals(_hero, null))
            {
                foreach (PlayerInfo player in _match.PlayerRows)
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
            Gold.Value = $"Золото: {_status.Gold}";
            int[] levels = { _status.RangeLevel, _status.SpeedLevel, _status.RadiusLevel, _status.DamageLevel };
            ReactiveProperty<string>[] labels = { RangeUpgradeText, SpeedUpgradeText, RadiusUpgradeText, DamageUpgradeText };
            ReactiveProperty<bool>[] enabled = { CanBuyRange, CanBuySpeed, CanBuyRadius, CanBuyDamage };

            for (int i = 0; i < labels.Length; i++)
            {
                int level = levels[i];
                bool max = level >= _hookConfig.MaxLevel || level >= _economy.UpgradePrices.Count;
                int price = max ? 0 : _economy.UpgradePrices[level];
                string priceText = max ? "MAX" : price.ToString();
                labels[i].Value = $"{UpgradeNames[i]} {level}/{_hookConfig.MaxLevel} · {priceText}";
                enabled[i].Value = !max && _status.Gold >= price;
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
                foreach (PlayerMatchStats row in _match.StatRows)
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

            string heading = _summary.Winner == MatchWinner.Draw ? "НИЧЬЯ" : $"ПОБЕДА КОМАНДЫ {_summary.Winner}";
            var text = new StringBuilder(heading);
            text.AppendLine($"\n{_summary.ScoreA} : {_summary.ScoreB}\n\nИгрок · Убийства / Смерти / Помощь · Попадания\n");

            foreach (PlayerMatchStats row in _match.StatRows)
            {
                text.AppendLine($"{row.Nickname} ({row.Team}) · {row.Kills} / {row.Deaths} / {row.Assists} · {row.HookHits}");
            }

            Statistics.Value = text.ToString();
        }

        private void UpdateBars()
        {
            _healthBarsBuffer.Clear();

            foreach (HeroNet hero in HeroNet.ClientHeroes)
            {
                if (ReferenceEquals(hero, null) || !hero.Alive || !hero.CanAct || !hero.IsViewAvailable)
                {
                    continue;
                }

                Vector3 position = hero.WorldViewPosition + Vector3.up * (1.8f * hero.ArenaScale);
                float health = Mathf.Clamp01((float)hero.Hp / _heroConfig.MaxHp);
                _healthBarsBuffer.Add(new WorldHealthBar(hero.SessionPlayerId, position, health));
            }

            IReadOnlyList<WorldHealthBar> current = HealthBars.Value;
            bool changed = current.Count != _healthBarsBuffer.Count;

            for (int i = 0; !changed && i < current.Count; i++)
            {
                changed = !current[i].Equals(_healthBarsBuffer[i]);
            }

            if (!changed)
            {
                return;
            }

            if (_healthBarsBuffer.Count == 0)
            {
                HealthBars.Value = Array.Empty<WorldHealthBar>();
            }
            else
            {
                HealthBars.Value = Array.AsReadOnly(_healthBarsBuffer.ToArray());
            }
        }

        public void Leave()
        {
            _leave?.Invoke();
        }

        public void ResetTraining()
        {
            _reset?.Invoke();
        }

        public void SelectTeamA()
        {
            _hero?.MatchActions?.SelectTeam(Team.A);
        }

        public void SelectTeamB()
        {
            _hero?.MatchActions?.SelectTeam(Team.B);
        }

        public void ToggleReady()
        {
            _hero?.MatchActions?.SetReady(!IsReady());
        }

        public void StartMatch()
        {
            _hero?.MatchActions?.StartMatch();
        }

        public void Rematch()
        {
            _hero?.MatchActions?.Rematch();
        }

        public void ReturnToLobby()
        {
            _hero?.MatchActions?.ReturnToLobby();
        }

        public void BuyRange()
        {
            _hero?.MatchActions?.BuyUpgrade(UpgradeId.Range);
        }

        public void BuySpeed()
        {
            _hero?.MatchActions?.BuyUpgrade(UpgradeId.Speed);
        }

        public void BuyRadius()
        {
            _hero?.MatchActions?.BuyUpgrade(UpgradeId.Radius);
        }

        public void BuyDamage()
        {
            _hero?.MatchActions?.BuyUpgrade(UpgradeId.Damage);
        }

        public override void Disconnect()
        {
            _initialized = false;
            _heroSubscriptions?.Dispose();
            _matchSubscriptions?.Dispose();
            _heroSubscriptions = null;
            _matchSubscriptions = null;
            _hero = null;
            _match = null;
            _leave = null;
            _reset = null;
            _training = null;
            _kills.Clear();
            HealthBars.Value = Array.Empty<WorldHealthBar>();
        }
    }
}
