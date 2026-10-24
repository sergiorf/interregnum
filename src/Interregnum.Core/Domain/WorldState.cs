namespace Interregnum.Core.Domain;

public sealed class WorldState
{
    public WorldState(
        string scenarioId,
        string name,
        int startYear,
        int turn,
        IReadOnlyDictionary<FactionId, Faction> factions,
        IReadOnlyDictionary<ProvinceId, Province> provinces,
        IReadOnlyDictionary<ArmyId, Army> armies)
    {
        ScenarioId = scenarioId;
        Name = name;
        StartYear = startYear;
        Turn = turn;
        Factions = factions;
        Provinces = provinces;
        Armies = armies;
    }

    public string ScenarioId { get; }

    public string Name { get; }

    public int StartYear { get; }

    public int Turn { get; }

    public IReadOnlyDictionary<FactionId, Faction> Factions { get; }

    public IReadOnlyDictionary<ProvinceId, Province> Provinces { get; }

    public IReadOnlyDictionary<ArmyId, Army> Armies { get; }
}
