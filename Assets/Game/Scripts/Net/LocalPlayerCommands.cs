using Game.Core.Commands;
using Game.Core.Economy;
using Unity.Mathematics;

namespace Game.Net
{
    public sealed class LocalPlayerCommands : IPlayerCommands, ILocalHero
    {
        private HeroNet _hero;

        public void Bind(HeroNet hero) => _hero = hero;
        public void Unbind(HeroNet hero)
        {
            if (_hero == hero)
                _hero = null;
        }

        public void MoveTo(float2 point)
        {
            if (_hero != null)
                _hero.MoveTo(point);
        }

        public void ThrowHook(float2 direction)
        {
            if (_hero != null)
                _hero.ThrowHook(direction);
        }

        public void BuyUpgrade(UpgradeId id)
        {
            if (_hero != null)
                _hero.BuyUpgrade(id);
        }

        public bool TryGetPosition(out float2 position)
        {
            position = default;
            if (_hero == null || !_hero.IsLocalHero || !_hero.Alive)
                return false;
            var local = _hero.transform.localPosition;
            position = new float2(local.x, local.z);
            return true;
        }
    }
}
