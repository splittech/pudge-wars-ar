using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Net
{
    public interface INetworkSceneLoader
    {
        bool IsReadyToStart { get; }
        string OfflineScenePath { get; }
        string OnlineScenePath { get; }
        UniTask LoadAsync(string scenePath, CancellationToken ct);
    }
}
