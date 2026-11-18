using Game.UI.Foundation;

namespace Game.UI
{
    public sealed class PlacementDocument : DocumentView
    {
        public PlacementViewModel ViewModel => (PlacementViewModel)Model;

        protected override void BindElements()
        {
            PlacementViewModel model = ViewModel;
            Text("placementMessage", model.Message);
            Visible("placementPanel", model.PanelVisible);
            Visible("confirmPlacement", model.ConfirmVisible);
            Enabled("confirmPlacement", model.CanConfirm);
            Visible("useFlat", model.FallbackVisible);
            Visible("trackingLost", model.TrackingLost);
            Button("confirmPlacement", model.Confirm);
            Button("useFlat", model.UseFlat);
        }
    }
}
