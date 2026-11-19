namespace Game.AR
{
    public interface IRoomPlacementControls
    {
        bool IsRoomMode { get; }
        bool CanEditRoom { get; }
        void AddRoomObstacle();
        void UndoRoomPoint();
        void RealignRoom();
    }
}
