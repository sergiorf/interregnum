using System.Collections.ObjectModel;

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
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(provinces);
        ArgumentNullException.ThrowIfNull(armies);

        ScenarioId = scenarioId;
        Name = name;
        StartYear = startYear;
        Turn = turn;
        Factions = Snapshot(factions);
        Provinces = Snapshot(provinces);
        Armies = Snapshot(armies);
    }

    public string ScenarioId { get; }

    public string Name { get; }

    public int StartYear { get; }

    public int Turn { get; }

    public IReadOnlyDictionary<FactionId, Faction> Factions { get; }

    public IReadOnlyDictionary<ProvinceId, Province> Provinces { get; }

    public IReadOnlyDictionary<ArmyId, Army> Armies { get; }

    internal WorldState WithProvince(Province province)
    {
        var provinces = Provinces.ToDictionary();
        provinces[province.Id] = province;
        return new WorldState(ScenarioId, Name, StartYear, Turn, Factions, provinces, Armies);
    }

    internal WorldState WithFactionsAndTurn(
        IReadOnlyDictionary<FactionId, Faction> factions,
        int turn) =>
        new(ScenarioId, Name, StartYear, turn, factions, Provinces, Armies);

    private static IReadOnlyDictionary<TKey, TValue> Snapshot<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(source.ToDictionary());
}
