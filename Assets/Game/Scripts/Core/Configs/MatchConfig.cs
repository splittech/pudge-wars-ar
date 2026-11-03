using Alchemy.Inspector;
using UnityEngine;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Match Config", menuName = "Game/Configs/Match Config", order = 0)]
    public sealed class MatchConfig : ScriptableObject
    {
        [BoxGroup("Match")]
        [SerializeField] private int _tickRate = 30;

        [BoxGroup("Match")]
        [SerializeField] private int _killsToWin = 15;

        [BoxGroup("Match")]
        [SerializeField] private float _matchDuration = 480f;

        [BoxGroup("Match")]
        [SerializeField] private float _countdownDuration = 3f;

        [BoxGroup("Match")]
        [SerializeField] private float _placementTimeout = 120f;

        [BoxGroup("Match")]
        [SerializeField] private int _maxPlayers = 4;

        public MatchConfigData ToData()
        {
            return new MatchConfigData(_tickRate, _killsToWin, _matchDuration, _countdownDuration, _placementTimeout, _maxPlayers);
        }
    }
}
