using Alchemy.Inspector;
using UnityEngine;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Economy Config", menuName = "Game/Configs/Economy Config", order = 0)]
    public sealed class EconomyConfig : ScriptableObject
    {
        [BoxGroup("Economy")]
        [SerializeField] private int _killGold = 150;

        [BoxGroup("Economy")]
        [SerializeField] private int _assistGold = 50;

        [BoxGroup("Economy")]
        [SerializeField] private float _assistWindow = 10f;

        [BoxGroup("Economy")]
        [SerializeField] private int _startGold = 0;

        [BoxGroup("Economy")]
        [SerializeField] private int[] _upgradePrices = new[] { 100, 200, 300, 400 };

        public EconomyConfigData ToData()
        {
            return new EconomyConfigData(_killGold, _assistGold, _assistWindow, _startGold, _upgradePrices);
        }
    }
}
