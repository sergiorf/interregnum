using Interregnum.Core.Domain;

namespace Interregnum.Core.Simulation;

public abstract record GameCommand(FactionId ActorId);

public sealed record SetProvinceTaxRate(
    FactionId ActorId,
    ProvinceId ProvinceId,
    TaxRate TaxRate) : GameCommand(ActorId);

public sealed record RecruitArmy(
    FactionId ActorId,
    ProvinceId ProvinceId) : GameCommand(ActorId);

public sealed record MoveArmy(
    FactionId ActorId,
    ArmyId ArmyId,
    ProvinceId DestinationId) : GameCommand(ActorId);

public sealed record EndTurn(FactionId ActorId) : GameCommand(ActorId);
