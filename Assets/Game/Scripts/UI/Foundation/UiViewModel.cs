using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Game.UI.Foundation
{
    public abstract class UiViewModel : ScriptableObject
    {
        private readonly List<IDisposable> _resources = new();

        public virtual void Tick()
        {
        }

        public virtual void Disconnect()
        {
        }

        protected ReactiveProperty<T> Property<T>(T initialValue)
        {
            var property = new ReactiveProperty<T>(initialValue);
            _resources.Add(property);
            return property;
        }

        protected virtual void OnDestroy()
        {
            Disconnect();

            foreach (IDisposable resource in _resources)
            {
                resource.Dispose();
            }

            _resources.Clear();
        }
    }
}
