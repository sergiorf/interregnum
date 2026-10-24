namespace Interregnum.Core.Domain;

public sealed record Faction(
    FactionId Id,
    string Name,
    string Color,
    int Treasury,
    int FoodReserves,
    int MilitaryStrength,
    int Stability,
    bool IsPlayable);
