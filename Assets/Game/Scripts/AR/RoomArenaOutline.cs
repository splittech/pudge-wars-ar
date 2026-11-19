using System.Collections.Generic;
using Game.Core.Geometry;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AR
{
    public sealed class RoomArenaOutline : System.IDisposable
    {
        private readonly GameObject _root = new("Room geometry preview");
        private readonly List<LineRenderer> _lines = new();
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties = new();

        public RoomArenaOutline()
        {
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        public void Draw(Transform arena, IReadOnlyList<IReadOnlyList<float2>> polygons, bool visible)
        {
            _root.SetActive(visible);
            _root.transform.SetPositionAndRotation(arena.position, arena.rotation);
            _root.transform.localScale = arena.lossyScale;
            while (_lines.Count < polygons.Count)
            {
                var child = new GameObject("Room contour");
                child.transform.SetParent(_root.transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = 0.06f;
                line.sharedMaterial = _material;
                _lines.Add(line);
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer line = _lines[i];
                line.enabled = i < polygons.Count;
                if (!line.enabled)
                {
                    continue;
                }

                IReadOnlyList<float2> polygon = polygons[i];
                line.positionCount = polygon.Count;
                Color color = i == 0 ? Color.cyan : new Color(1, 0.5f, 0.1f);
                line.startColor = color;
                line.endColor = color;
                _properties.SetColor("_BaseColor", color);
                line.SetPropertyBlock(_properties);
                for (int j = 0; j < polygon.Count; j++)
                {
                    line.SetPosition(j, new Vector3(polygon[j].x, 0.03f, polygon[j].y));
                }
            }
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(_root);
                Object.Destroy(_material);
            }
            else
            {
                Object.DestroyImmediate(_root);
                Object.DestroyImmediate(_material);
            }
        }
    }
}
