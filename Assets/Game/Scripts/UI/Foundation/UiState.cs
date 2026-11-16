using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Game.UI.Foundation
{
    [Serializable]
    public struct UiParameter : IEquatable<UiParameter>
    {
        public string Name;
        public string Text;
        public string StyleClass;
        public float Number;
        public bool Enabled;
        public bool Visible;
        public Vector2 Position;
        public string[] Rows;

        public bool Equals(UiParameter other)
        {
            if (Name != other.Name || Text != other.Text || StyleClass != other.StyleClass || Number != other.Number || Enabled != other.Enabled
                || Visible != other.Visible || Position != other.Position) return false;
            int count = Rows?.Length ?? 0;
            if (count != (other.Rows?.Length ?? 0)) return false;
            for (int i = 0; i < count; i++) if (Rows[i] != other.Rows[i]) return false;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Game/UI/State", fileName = "UI State")]
    public sealed class UiState : ScriptableObject
    {
        [SerializeField] private List<UiParameter> _parameters = new();
        private Subject<UiParameter> _changed;
        public Observable<UiParameter> Changed => _changed ??= new Subject<UiParameter>();
        public IReadOnlyList<UiParameter> Parameters => _parameters;

        public UiParameter Read(string key)
        {
            foreach (var parameter in _parameters) if (parameter.Name == key) return parameter;
            return new UiParameter { Name = key, Enabled = true, Visible = true };
        }

        public void Set(UiParameter parameter)
        {
            for (int i = 0; i < _parameters.Count; i++)
                if (_parameters[i].Name == parameter.Name)
                {
                    if (_parameters[i].Equals(parameter)) return;
                    if (parameter.Rows != null) parameter.Rows = (string[])parameter.Rows.Clone();
                    _parameters[i] = parameter;
                    _changed?.OnNext(parameter);
                    return;
                }
            if (parameter.Rows != null) parameter.Rows = (string[])parameter.Rows.Clone();
            _parameters.Add(parameter);
            _changed?.OnNext(parameter);
        }

        public void Text(string key, string value) { var p = Read(key); p.Text = value ?? string.Empty; Set(p); }
        public void Number(string key, float value) { var p = Read(key); p.Number = value; Set(p); }
        public void Visible(string key, bool value) { var p = Read(key); p.Visible = value; Set(p); }
        public void Enabled(string key, bool value) { var p = Read(key); p.Enabled = value; Set(p); }
        public void Rows(string key, string[] value) { var p = Read(key); p.Rows = value; Set(p); }
        public void StyleClass(string key, string value) { var p = Read(key); p.StyleClass = value; Set(p); }

        private void OnValidate()
        {
            if (_changed == null) return;
            foreach (var parameter in _parameters) _changed.OnNext(parameter);
        }

        private void OnDestroy() { _changed?.Dispose(); _changed = null; }
    }
}
