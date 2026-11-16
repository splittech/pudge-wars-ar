using System;
using Game.Core.Configs;
using Game.UI.Foundation;
using UnityEngine;

namespace Game.UI
{
    public sealed class MatchInterface : MonoBehaviour
    {
        private MatchPresenter _presenter;
        [SerializeField] private UiState _runtimeModel;
        public static void Create(GameObject owner, HeroConfig hero, HookConfig hook, EconomyConfig economy,
            Action leave, Action resetTraining, Func<string> trainingStatus)
        {

            var root = new GameObject("Match Presenter");
            root.transform.SetParent(owner.transform, false);
            var host = root.AddComponent<MatchInterface>();
            var view = ToolkitRuntime.Create(root, "Match", "MatchState");
            host._runtimeModel = view.Model;
            host._presenter = new MatchPresenter(view, hero.ToData(), hook.ToData(), economy.ToData(),
                leave, resetTraining, trainingStatus);
        }
        private void LateUpdate() => _presenter?.Tick();
        private void OnDestroy() => _presenter?.Dispose();
    }
}
