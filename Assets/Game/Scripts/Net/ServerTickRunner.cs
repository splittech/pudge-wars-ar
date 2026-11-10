using System;
using Game.Core.Match;
using Mirror;
using VContainer.Unity;

namespace Game.Net
{
    public sealed class ServerTickRunner : ITickable
    {
        public const float StepDelta = 1f / 30f;
        public const int MaxStepsPerFrame = 4;
        private readonly MatchSimulation _simulation;
        private readonly IMatchStateSink _sink;
        private readonly NetworkPrefabFactory _factory;
        private readonly ServerPlayerCommands _commands;
        private double _accumulator;

        public ServerTickRunner(MatchSimulation simulation, IMatchStateSink sink, NetworkPrefabFactory factory,
            ServerPlayerCommands commands)
        {
            _simulation = simulation;
            _sink = sink;
            _factory = factory;
            _commands = commands;
        }

        public void Tick()
        {
            Advance(UnityEngine.Time.unscaledDeltaTime, NetworkTime.time, NetworkServer.active);
        }

        public int Advance(double frameDelta, double now, bool serverActive)
        {
            if (!serverActive)
            {
                _accumulator = 0;
                return 0;
            }
            if (double.IsNaN(frameDelta) || double.IsInfinity(frameDelta) || frameDelta < 0 ||
                double.IsNaN(now) || double.IsInfinity(now))
                throw new ArgumentOutOfRangeException(nameof(frameDelta));
            _accumulator += frameDelta;
            int steps = (int)Math.Min(Math.Floor(_accumulator / StepDelta), MaxStepsPerFrame);
            if (steps == 0)
                return 0;
            _factory.ExpireDisconnectedPlayers(now);
            for (int i = 0; i < steps; i++)
            {
                // A new simulation can be created immediately before this render frame.
                double start = Math.Max(_simulation.Time, now - (steps - i) * (double)StepDelta);
                _commands.Flush(start);
                _simulation.Step(StepDelta, start);
                _commands.CompleteStep();
                _sink.Apply(_simulation);
            }
            _accumulator %= StepDelta;
            return steps;
        }
    }
}
