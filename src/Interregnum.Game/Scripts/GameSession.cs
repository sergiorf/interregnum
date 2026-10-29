using Interregnum.Core.Domain;
using Interregnum.Core.Simulation;

namespace Interregnum.Game;

/// <summary>
/// Owns the authoritative simulation for one local game session and publishes
/// refreshed immutable snapshots to presentation code.
/// </summary>
public sealed class GameSession
{
    private readonly SimulationEngine _engine;

    public GameSession(WorldState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        _engine = new SimulationEngine(initialState);
        State = initialState;
    }

    public WorldState State { get; private set; }

    public event Action<WorldState>? StateChanged;

    public CommandResult Execute(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = _engine.Execute(command);
        State = result.State;
        if (result.Succeeded)
        {
            StateChanged?.Invoke(State);
        }

        return result;
    }
}
