using R3;
using UnityEngine;
using Game.UI.Foundation;
using Game.AR;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Placement ViewModel")]
    public sealed class PlacementViewModel : UiViewModel
    {
        private IArenaPlacementService _source;
        private ReactiveProperty<bool> _roomControlsVisible;
        public ReactiveProperty<bool> RoomControlsVisible => _roomControlsVisible ??= Property<bool>(false);
        private ReactiveProperty<bool> _roomEditing;
        public ReactiveProperty<bool> RoomEditing => _roomEditing ??= Property<bool>(false);

        private ReactiveProperty<string> _message;
        public ReactiveProperty<string> Message => _message ??= Property<string>("Подготовка арены…");

        private ReactiveProperty<bool> _panelVisible;
        public ReactiveProperty<bool> PanelVisible => _panelVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _confirmVisible;
        public ReactiveProperty<bool> ConfirmVisible => _confirmVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _canConfirm;
        public ReactiveProperty<bool> CanConfirm => _canConfirm ??= Property<bool>(false);

        private ReactiveProperty<bool> _fallbackVisible;
        public ReactiveProperty<bool> FallbackVisible => _fallbackVisible ??= Property<bool>(false);

        private ReactiveProperty<bool> _trackingLost;
        public ReactiveProperty<bool> TrackingLost => _trackingLost ??= Property<bool>(false);

        public override void Tick()
        {
            var room = _source as IRoomPlacementControls;
            RoomControlsVisible.Value = room?.IsRoomMode == true;
            RoomEditing.Value = room?.CanEditRoom == true;
        }

        public void AddObstacle()
        {
            (_source as IRoomPlacementControls)?.AddRoomObstacle();
        }

        public void Undo()
        {
            (_source as IRoomPlacementControls)?.UndoRoomPoint();
        }

        public void Realign()
        {
            (_source as IRoomPlacementControls)?.RealignRoom();
        }

        public void Initialize(IArenaPlacementService source)
        {
            Disconnect();
            _source = source;
            _source.StatusChanged += Apply;
            Apply(_source.Status);
        }

        public override void Disconnect()
        {
            if (_source != null)
            {
                _source.StatusChanged -= Apply;
                _source = null;
            }
        }

        public void Confirm()
        {
            if (CanConfirm.Value)
            {
                _source?.Confirm();
            }
        }

        public void UseFlat()
        {
            _source?.UseFlat();
        }

        private void Apply(ArenaPlacementStatus state)
        {
            Message.Value = state.Message;
            PanelVisible.Value = state.PanelVisible;
            ConfirmVisible.Value = state.ConfirmVisible;
            CanConfirm.Value = state.CanConfirm;
            FallbackVisible.Value = state.FallbackVisible;
            TrackingLost.Value = state.TrackingLost;
        }
    }
}
