using System;
using UnityEngine;

namespace Game.UI
{
    public readonly struct WorldHealthBar : IEquatable<WorldHealthBar>
    {
        public WorldHealthBar(uint playerId, Vector3 position, float health)
        {
            PlayerId = playerId;
            Position = position;
            Health = health;
        }

        public uint PlayerId { get; }
        public Vector3 Position { get; }
        public float Health { get; }

        public bool Equals(WorldHealthBar other)
        {
            return PlayerId == other.PlayerId
                && Position.Equals(other.Position)
                && Health.Equals(other.Health);
        }
    }
}
