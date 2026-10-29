using System.Collections.ObjectModel;
using Interregnum.Core.Domain;

namespace Interregnum.Core.Simulation;

public enum CommandErrorCode
{
    None,
    InvalidActor,
    UnknownProvince,
    ForeignProvince,
    InvalidTaxRate,
    TaxRateUnchanged,
    ArithmeticOverflow,
    CommandNotAvailable
}

public abstract record DomainEvent;

public sealed record ProvinceTaxRateChanged(
    FactionId ActorId,
    ProvinceId ProvinceId,
    TaxRate PreviousRate,
    TaxRate NewRate) : DomainEvent;

public sealed record ProvinceFoodProduced(
    FactionId FactionId,
    ProvinceId ProvinceId,
    int Amount) : DomainEvent;

public sealed record FactionFoodConsumed(
    FactionId FactionId,
    int Demand,
    int Consumed,
    int Unmet) : DomainEvent;

public sealed record ProvinceTaxCollected(
    FactionId FactionId,
    ProvinceId ProvinceId,
    int BaseRevenue,
    TaxRate TaxRate,
    bool SuppressedByRebellion,
    int Amount) : DomainEvent;

public sealed record ArmyUpkeepPaid(
    FactionId FactionId,
    ArmyId ArmyId,
    int Due,
    int Paid,
    int Unpaid) : DomainEvent;

public sealed record ProvinceAdministrationPaid(
    FactionId FactionId,
    ProvinceId ProvinceId,
    int Due,
    int Paid,
    int Unpaid) : DomainEvent;

public sealed record ProvinceLoyaltyChanged(
    FactionId FactionId,
    ProvinceId ProvinceId,
    int OpeningLoyalty,
    int TaxPolicyModifier,
    int FoodShortageModifier,
    int AdministrationModifier,
    int ClampAdjustment,
    int ClosingLoyalty) : DomainEvent;

public sealed record ProvinceRebellionChanged(
    FactionId FactionId,
    ProvinceId ProvinceId,
    bool IsRebelling,
    int Loyalty) : DomainEvent;

public sealed record FactionStabilityChanged(
    FactionId FactionId,
    int OpeningStability,
    int RecoveryModifier,
    int RebellionModifier,
    int FoodShortageModifier,
    int UpkeepModifier,
    int AdministrationModifier,
    int ClampAdjustment,
    int ClosingStability) : DomainEvent;

public sealed record TurnAdvanced(int CompletedTurn, int NextTurn) : DomainEvent;

public sealed record CommandResult
{
    private CommandResult(
        bool succeeded,
        CommandErrorCode errorCode,
        string? errorMessage,
        WorldState state,
        IReadOnlyList<DomainEvent> events,
        TurnReport? turnReport)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        State = state;
        Events = events;
        TurnReport = turnReport;
    }

    public bool Succeeded { get; }
    public CommandErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }
    public WorldState State { get; }
    public IReadOnlyList<DomainEvent> Events { get; }
    public TurnReport? TurnReport { get; }

    internal static CommandResult Success(WorldState state, params DomainEvent[] events) =>
        new(true, CommandErrorCode.None, null, state, Array.AsReadOnly(events), null);

    internal static CommandResult Success(
        WorldState state,
        TurnReport turnReport,
        IReadOnlyList<DomainEvent> events) =>
        new(true, CommandErrorCode.None, null, state,
            new ReadOnlyCollection<DomainEvent>(events.ToArray()), turnReport);

    internal static CommandResult Failure(
        WorldState state,
        CommandErrorCode errorCode,
        string errorMessage) =>
        new(false, errorCode, errorMessage, state, new ReadOnlyCollection<DomainEvent>([]), null);
}
