using System.Collections.Generic;
using Game.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MatchDocument : DocumentView
    {
        private readonly Dictionary<uint, ProgressBar> _bars = new();
        private readonly HashSet<uint> _visibleBars = new();
        private Camera _camera;

        public MatchViewModel ViewModel => (MatchViewModel)Model;

        protected override void BindElements()
        {
            Element<VisualElement>("worldBars").Clear();
            _bars.Clear();
            MatchViewModel model = ViewModel;
            Visible("trainingPanel", model.TrainingVisible);
            Visible("lobby", model.LobbyVisible);
            Visible("results", model.ResultsVisible);
            Visible("hud", model.HudVisible);
            Text("trainingStatus", model.TrainingStatus);
            Text("timer", model.Timer);
            Text("health", model.Health);
            Text("countdown", model.Countdown);
            Text("feed", model.Feed);
            Text("score", model.Score);
            Text("teamA", model.TeamA);
            Text("teamB", model.TeamB);
            Text("ready", model.ReadyText);
            Text("gold", model.Gold);
            Text("statistics", model.Statistics);
            Progress("cooldown", model.Cooldown);
            Visible("start", model.StartVisible);
            Enabled("start", model.CanStart);
            Visible("rematch", model.HostActionsVisible);
            Visible("returnToLobby", model.HostActionsVisible);
            Text("buyRange", model.RangeUpgradeText);
            Enabled("buyRange", model.CanBuyRange);
            Button("buyRange", model.BuyRange);
            Text("buySpeed", model.SpeedUpgradeText);
            Enabled("buySpeed", model.CanBuySpeed);
            Button("buySpeed", model.BuySpeed);
            Text("buyRadius", model.RadiusUpgradeText);
            Enabled("buyRadius", model.CanBuyRadius);
            Button("buyRadius", model.BuyRadius);
            Text("buyDamage", model.DamageUpgradeText);
            Enabled("buyDamage", model.CanBuyDamage);
            Button("buyDamage", model.BuyDamage);
            Button("selectA", model.SelectTeamA);
            Button("selectB", model.SelectTeamB);
            Button("ready", model.ToggleReady);
            Button("start", model.StartMatch);
            Button("rematch", model.Rematch);
            Button("returnToLobby", model.ReturnToLobby);
            Button("leave", model.Leave);
            Button("resetTraining", model.ResetTraining);
        }

        protected override void UpdateView()
        {
            if (Root?.panel == null)
            {
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;

                if (_camera == null && Camera.allCamerasCount > 0)
                {
                    _camera = Camera.allCameras[0];
                }
            }

            _visibleBars.Clear();

            if (_camera != null)
            {
                foreach (WorldHealthBar state in ViewModel.HealthBars.Value)
                {
                    Vector3 screen = _camera.WorldToScreenPoint(state.Position);

                    if (screen.z <= 0)
                    {
                        continue;
                    }

                    if (!_bars.TryGetValue(state.PlayerId, out ProgressBar bar))
                    {
                        bar = new ProgressBar
                        {
                            name = "world_" + state.PlayerId,
                            lowValue = 0,
                            highValue = 1,
                            pickingMode = PickingMode.Ignore
                        };
                        bar.AddToClassList("world-health");
                        Element<VisualElement>("worldBars").Add(bar);
                        _bars.Add(state.PlayerId, bar);
                    }

                    Vector2 point = RuntimePanelUtils.ScreenToPanel(
                        Root.panel,
                        new Vector2(screen.x, Screen.height - screen.y));
                    bar.style.translate = new Translate(point.x - 50, point.y, 0);
                    bar.value = state.Health;
                    bar.EnableInClassList("hidden", false);
                    _visibleBars.Add(state.PlayerId);
                }
            }

            foreach (KeyValuePair<uint, ProgressBar> entry in _bars)
            {
                if (!_visibleBars.Contains(entry.Key))
                {
                    entry.Value.EnableInClassList("hidden", true);
                }
            }
        }
    }
}
