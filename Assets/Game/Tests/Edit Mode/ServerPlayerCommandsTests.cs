using Game.Core.Hero;
using Game.Core.Hook;
using Game.Core.Match;
using Game.Net;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class ServerPlayerCommandsTests
    {
        private MatchSimulation _simulation;
        private ServerPlayerCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _simulation = Setup.MatchSimulation();
            _commands = new ServerPlayerCommands(_simulation, Setup.Arena());
        }

        [Test]
        public void TryMove_MultipleTargets_UsesLastTargetWithoutExtraMovementSteps()
        {
            // Arrange.
            _commands.TryMove(1, new float2(-3, 0), 0, true, out _);
            float2 target = new(-4, 2);

            // Act.
            bool accepted = _commands.TryMove(1, target, 0, true, out _);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(_simulation.GetHero(0).MoveTarget, Is.EqualTo(target));
            Assert.That(math.distance(_simulation.GetHero(0).Pos, new float2(-4, 0)),
                Is.EqualTo(Setup.HeroConfig().MoveSpeed * Setup.TickDeltaTime).Within(0.00001));
        }

        [Test]
        public void TryMove_TargetOutsideOwnHalf_ClampsTarget()
        {
            // Act.
            _commands.TryMove(1, new float2(100, 100), 0, true, out _);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(_simulation.GetHero(0).MoveTarget, Is.EqualTo(new float2(-2.25f, 6.5f)));
        }

        [Test]
        public void TryMove_AnotherPlayersConnection_RejectsInput()
        {
            // Act.
            bool accepted = _commands.TryMove(1, new float2(-3, 0), 0, false, out string reason);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Sender does not own hero"));
            Assert.That(_simulation.GetHero(0).Pos, Is.EqualTo(new float2(-4, 0)));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryMove_NonFiniteCoordinate_RejectsInput(float value)
        {
            // Act.
            bool accepted = _commands.TryMove(1, new float2(value, 0), 0, true, out string reason);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Non-finite input"));
        }

        [Test]
        public void TryMove_RateExceeded_RejectsUntilNextWindow()
        {
            // Arrange.
            for (int i = 0; i < 30; i++)
                _commands.TryMove(1, new float2(-3, 0), 0, true, out _);

            // Act.
            bool rejected = _commands.TryMove(1, new float2(-3, 0), 0.5, true, out _);
            bool accepted = _commands.TryMove(1, new float2(-3, 0), 1, true, out _);

            // Assert.
            Assert.That(rejected, Is.False);
            Assert.That(accepted, Is.True);
        }

        [Test]
        public void TryThrow_NonUnitDirection_NormalizesOnServer()
        {
            // Act.
            bool accepted = _commands.TryThrow(1, new float2(5, 0), 0, true, out _);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(_simulation.GetHook(0).Dir, Is.EqualTo(new float2(1, 0)));
            Assert.That(_simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Extending));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.MaxValue)]
        public void TryThrow_InvalidDirection_RejectsInput(float value)
        {
            // Act.
            bool accepted = _commands.TryThrow(1, new float2(value, 0), 0, true, out _);

            // Assert.
            Assert.That(accepted, Is.False);
        }

        [Test]
        public void TryThrow_ThrowAlreadyPending_RejectsDuplicate()
        {
            // Arrange.
            _commands.TryThrow(1, new float2(1, 0), 0, true, out _);

            // Act.
            bool accepted = _commands.TryThrow(1, new float2(1, 0), 0, true, out string reason);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Throw already pending"));
        }

        [Test]
        public void TryThrow_HookAlreadyActive_RejectsInput()
        {
            // Arrange.
            _commands.TryThrow(1, new float2(1, 0), 0, true, out _);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);
            _commands.CompleteStep();

            // Act.
            bool accepted = _commands.TryThrow(1, new float2(1, 0), _simulation.Time, true, out string reason);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Hook is busy"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompleteStep_QueuedThrow_ReportsActualSimulationOutcome(bool canAct)
        {
            // Arrange.
            int completed = 0;
            bool result = false;
            uint resultId = 0;
            uint resultEpoch = 0;
            _commands.ThrowCompleted += (player, id, epoch, accepted, reason) =>
            {
                completed++;
                result = accepted;
                resultId = id;
                resultEpoch = epoch;
            };
            _commands.TryThrow(1, new float2(1, 0), 0, true, 9, 7, out _);
            if (!canAct)
            {
                _simulation.RemoveHero(1);
                _simulation.AddHero(Setup.Hero(alive: false));
            }

            // Act.
            _commands.Flush(0);
            Assert.That(completed, Is.Zero);
            _simulation.Step(Setup.TickDeltaTime);
            _commands.CompleteStep();
            _commands.CompleteStep();

            // Assert.
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(canAct));
            Assert.That(resultId, Is.EqualTo(9));
            Assert.That(resultEpoch, Is.EqualTo(7));
        }

        [Test]
        public void TryThrow_CooldownNotExpired_RejectsInput()
        {
            // Arrange.
            HeroState hero = Setup.Hero();
            hero.HookCooldownEnd = 5;
            var simulation = Setup.MatchSimulation(new[] { hero });
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());

            // Act.
            bool accepted = commands.TryThrow(1, new float2(1, 0), 1, true, out string reason);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Hook cooldown"));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void TryMove_HeroCannotAct_RejectsInput(bool alive, bool pulled)
        {
            // Arrange.
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero(alive: alive, isPulled: pulled) });
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());

            // Act.
            bool accepted = commands.TryMove(1, new float2(-3, 0), 0, true, out string reason);

            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("Hero cannot act"));
        }

        [Test]
        public void Remove_PendingInput_DiscardsDisconnectedPlayersActions()
        {
            // Arrange.
            _commands.TryMove(1, new float2(-3, 0), 0, true, out _);
            _commands.TryThrow(1, new float2(1, 0), 0, true, out _);

            // Act.
            _commands.Remove(1);
            _commands.Flush(0);
            _simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(_simulation.GetHero(0).Pos, Is.EqualTo(new float2(-4, 0)));
            Assert.That(_simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
        }
    }
}
