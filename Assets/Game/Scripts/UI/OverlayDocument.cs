using Game.UI.Foundation;

namespace Game.UI
{
    public sealed class OverlayDocument : DocumentView
    {
        public OverlayViewModel ViewModel => (OverlayViewModel)Model;

        protected override void BindElements()
        {
            OverlayViewModel model = ViewModel;
            Text("diagnostics", model.Diagnostics);
            Text("arenaMode", model.ArenaModeText);
            Button("arenaMode", model.ToggleArenaMode);
        }
    }
}
