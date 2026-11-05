using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Game.Net
{
    [CreateAssetMenu(fileName = "Network Prefab List", menuName = "Game/Configs/Network Prefab List")]
    public sealed class NetworkPrefabList : ScriptableObject
    {
        [SerializeField] private HeroNet _hero;
        [SerializeField] private MatchStateNet _matchState;

        public HeroNet Hero => _hero;
        public MatchStateNet MatchState => _matchState;

        public IEnumerable<GameObject> Prefabs
        {
            get
            {
                yield return _hero != null ? _hero.gameObject : null;
                yield return _matchState != null ? _matchState.gameObject : null;
            }
        }

        public void Validate()
        {
            var assetIds = new HashSet<uint>();
            foreach (GameObject prefab in Prefabs)
            {
                if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity identity))
                    throw new InvalidOperationException("Every network prefab must have a root NetworkIdentity.");
                if (identity.assetId == 0 || identity.sceneId != 0)
                    throw new InvalidOperationException($"'{prefab.name}' must be a prefab asset with a nonzero assetId.");
                if (prefab.GetComponentsInChildren<NetworkIdentity>(true).Length != 1)
                    throw new InvalidOperationException($"'{prefab.name}' must have exactly one NetworkIdentity.");
                if (!assetIds.Add(identity.assetId))
                    throw new InvalidOperationException("Network prefab assetIds must be unique.");
            }
        }
    }
}
