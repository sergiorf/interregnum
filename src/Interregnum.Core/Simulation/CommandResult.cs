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
    CommandNotAvailable
}

public abstract record DomainEvent;

public sealed record ProvinceTaxRateChanged(
    FactionId ActorId,
    ProvinceId ProvinceId,
    TaxRate PreviousRate,
    TaxRate NewRate) : DomainEvent;

public sealed record CommandResult
{
    private CommandResult(
        bool succeeded,
        CommandErrorCode errorCode,
        string? errorMessage,
        WorldState state,
        IReadOnlyList<DomainEvent> events)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        State = state;
        Events = events;
    }

    public bool Succeeded { get; }
    public CommandErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }
    public WorldState State { get; }
    public IReadOnlyList<DomainEvent> Events { get; }

    internal static CommandResult Success(WorldState state, params DomainEvent[] events) =>
        new(true, CommandErrorCode.None, null, state, Array.AsReadOnly(events));

    internal static CommandResult Failure(
        WorldState state,
        CommandErrorCode errorCode,
        string errorMessage) =>
        new(false, errorCode, errorMessage, state, new ReadOnlyCollection<DomainEvent>([]));
}
