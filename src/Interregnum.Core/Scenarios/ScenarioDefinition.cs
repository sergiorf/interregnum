using Interregnum.Core.Domain;

namespace Interregnum.Core.Scenarios;

public sealed record ScenarioDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int StartYear { get; init; }

    public int StartingTurn { get; init; } = 1;

    public List<FactionDefinition> Factions { get; init; } = [];

    public List<ProvinceDefinition> Provinces { get; init; } = [];

    public List<ArmyDefinition> Armies { get; init; } = [];
}

public sealed record FactionDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    public int Treasury { get; init; }

    public int FoodReserves { get; init; }

    public int MilitaryStrength { get; init; }

    public int Stability { get; init; }

    public bool IsPlayable { get; init; }
}

public sealed record ProvinceDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public int Population { get; init; }

    public int AgriculturalProduction { get; init; }

    public int TaxRevenue { get; init; }

    public int Loyalty { get; init; }

    public int AdministrativeBurden { get; init; }

    public TaxRate TaxRate { get; init; } = TaxRate.Normal;

    public bool IsRebelling { get; init; }

    public string? SettlementName { get; init; }

    public List<string> Neighbors { get; init; } = [];

    public List<MapPoint> MapPolygon { get; init; } = [];
}

public sealed record ArmyDefinition
{
    public string Id { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string ProvinceId { get; init; } = string.Empty;

    public int Strength { get; init; }

    public int Upkeep { get; init; }
}

public sealed record MapPoint(float X, float Y);
