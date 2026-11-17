using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.AR
{
    public interface IArenaAnchorProvider : IDisposable
    {
        ReadOnlyReactiveProperty<bool> IsTracking { get; }
        UniTask<Pose> AcquireArenaPoseAsync(CancellationToken cancellationToken);
    }
}
