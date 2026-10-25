using Interregnum.Core.Domain;

namespace Interregnum.Core.Simulation;

public sealed class SimulationEngine
{
    public SimulationEngine(WorldState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        State = initialState;
    }

    public WorldState State { get; private set; }

    public CommandResult Execute(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!State.Factions.TryGetValue(command.ActorId, out var actor) || !actor.IsPlayable)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.InvalidActor,
                $"Faction '{command.ActorId}' is not the active playable actor.");
        }

        return command switch
        {
            SetProvinceTaxRate setTaxRate => Execute(setTaxRate),
            _ => CommandResult.Failure(
                State,
                CommandErrorCode.CommandNotAvailable,
                $"Command '{command.GetType().Name}' is not available in the current simulation slice.")
        };
    }

    private CommandResult Execute(SetProvinceTaxRate command)
    {
        if (!State.Provinces.TryGetValue(command.ProvinceId, out var province))
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.UnknownProvince,
                $"Province '{command.ProvinceId}' does not exist.");
        }

        if (province.OwnerId != command.ActorId)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.ForeignProvince,
                $"Province '{province.Name}' is not controlled by '{command.ActorId}'.");
        }

        if (!Enum.IsDefined(command.TaxRate))
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.InvalidTaxRate,
                $"Tax rate value '{(int)command.TaxRate}' is invalid.");
        }

        if (province.TaxRate == command.TaxRate)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.TaxRateUnchanged,
                $"Province '{province.Name}' is already using the {command.TaxRate} tax rate.");
        }

        var previousRate = province.TaxRate;
        State = State.WithProvince(province.WithTaxRate(command.TaxRate));
        return CommandResult.Success(
            State,
            new ProvinceTaxRateChanged(command.ActorId, command.ProvinceId, previousRate, command.TaxRate));
    }
}
