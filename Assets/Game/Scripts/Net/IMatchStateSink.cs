using Game.Core.Match;

namespace Game.Net
{
    public interface IMatchStateSink
    {
        void Apply(MatchSimulation simulation);
    }
}
