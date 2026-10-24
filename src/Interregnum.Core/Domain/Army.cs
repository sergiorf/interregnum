namespace Interregnum.Core.Domain;

public sealed record Army(
    ArmyId Id,
    FactionId OwnerId,
    ProvinceId ProvinceId,
    int Strength,
    int Upkeep);
