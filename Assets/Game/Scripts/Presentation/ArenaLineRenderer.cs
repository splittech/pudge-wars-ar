using Game.Core.Hook;
using UnityEngine;

namespace Game.Presentation
{
    public static class ArenaLineRenderer
    {
        public static void Initialize(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.widthCurve = AnimationCurve.Constant(0, 1, 1);
        }

        public static void Draw(
            LineRenderer line,
            Transform arena,
            Vector2 owner,
            HookPath path,
            int count,
            Vector2 tip,
            float height,
            float width)
        {
            int first = 0;
            if (count > 0)
            {
                var start = path.Get(0);
                if ((owner - new Vector2(start.x, start.y)).sqrMagnitude < 1e-10f)
                {
                    first = 1;
                }
            }

            line.widthMultiplier = width * Mathf.Abs(arena.lossyScale.x);
            line.positionCount = count - first + 2;
            line.SetPosition(0, arena.TransformPoint(new Vector3(owner.x, height, owner.y)));
            for (int i = first; i < count; i++)
            {
                var point = path.Get(i);
                line.SetPosition(i - first + 1, arena.TransformPoint(new Vector3(point.x, height, point.y)));
            }

            line.SetPosition(line.positionCount - 1, arena.TransformPoint(new Vector3(tip.x, height, tip.y)));
        }
    }
}
