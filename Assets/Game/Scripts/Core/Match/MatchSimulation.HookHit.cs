using Game.Core.Events;
using Game.Core.Hook;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private void DetachHook(int index, double time)
        {
            ref var hook = ref _hooks[index];
            hook.TargetId = 0;
            hook.Origin = hook.Tip;
            hook.Length = 0;
            hook.StartTime = time;
            HookSimulation.Turn(ref hook, HookPhase.Retracting, time);
        }

        private void ResolveHookHit(int owner, int target, double time)
        {
            ref var victim = ref _heroes[target];
            ref var hook = ref _hooks[owner];
            bool intercepted = victim.IsPulled;
            bool headshot = intercepted && (IsRoom || victim.Team != _heroes[owner].Team);
            if (intercepted)
            {
                for (int i = 0; i < _hooks.Length; i++)
                {
                    if (_hooks[i].Phase == HookPhase.Pulling && _hooks[i].TargetId == victim.Id)
                    {
                        DetachHook(i, time);
                    }
                }
            }

            _hits.Add(new HookHit(hook.OwnerId, victim.Id, hook.Tip, time));
            if (Rules != null)
            {
                _heroes[owner].HookHits++;
            }

            int damage = Combat.Combat.ApplyHit(ref victim, _heroes[owner], headshot ? victim.Hp : hook.Damage, IsRoom);
            if (damage > 0)
            {
                _damage.Add(new HeroDamaged(victim.Id, hook.OwnerId, damage, victim.Hp, time));
                if (!victim.Alive)
                {
                    _deaths.Add(new HeroDied(victim.Id, hook.OwnerId, time));
                }
            }
            if (headshot)
            {
                victim.IsPulled = false;
                victim.MoveTarget = victim.Pos;
                victim.EnemyTerritoryExposed = false;
                victim.EnemyTerritoryDamageAt = 0;
                // Cancel the victim's own hook immediately, including any passenger it carries.
                int passenger = Find(_hooks[target].TargetId);
                if (passenger >= 0)
                {
                    _heroes[passenger].IsPulled = false;
                    _heroes[passenger].MoveTarget = _heroes[passenger].Pos;
                }
                _hooks[target] = new HookState { OwnerId = victim.Id };
                DetachHook(owner, time);
                return;
            }
            hook.TargetId = victim.Id;
            HookSimulation.Turn(ref hook, HookPhase.Pulling, time);
            victim.IsPulled = true;
            victim.Pos = MoveInRoom(victim.Pos, hook.Tip, victim.Radius);
            if (IsRoom)
            {
                hook.Tip = victim.Pos;
            }
        }
    }
}
