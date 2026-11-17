using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.AR
{
    public sealed class FlatArenaAnchorProvider : IArenaAnchorProvider
    {
        private readonly Pose _pose;
        private readonly ReactiveProperty<bool> _tracking = new(true);
        public ReadOnlyReactiveProperty<bool> IsTracking => _tracking;
        public FlatArenaAnchorProvider(Pose pose) => _pose = pose;
        public UniTask<Pose> AcquireArenaPoseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(_pose);
        }
        public void Dispose() => _tracking.Dispose();
    }
}
