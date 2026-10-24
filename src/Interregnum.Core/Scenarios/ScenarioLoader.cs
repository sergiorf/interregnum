using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Interregnum.Core.Domain;

namespace Interregnum.Core.Scenarios;

public static partial class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ScenarioDefinition LoadDefinition(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var definition = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions)
            ?? throw new ScenarioValidationException(["The document is empty."]);

        Validate(definition);
        return definition;
    }

    public static WorldState CreateWorld(ScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Validate(definition);

        var factions = definition.Factions.ToDictionary(
            item => new FactionId(item.Id),
            item => new Faction(
                new FactionId(item.Id), item.Name, item.Color, item.Treasury,
                item.FoodReserves, item.MilitaryStrength, item.Stability, item.IsPlayable));

        var provinces = definition.Provinces.ToDictionary(
            item => new ProvinceId(item.Id),
            item => new Province(
                new ProvinceId(item.Id), item.Name, new FactionId(item.OwnerId), item.Population,
                item.AgriculturalProduction, item.TaxRevenue, item.Loyalty, item.AdministrativeBurden,
                item.TaxRate,
                item.SettlementName is null ? null : new Settlement(item.SettlementName),
                item.Neighbors.Select(id => new ProvinceId(id)).ToHashSet()));

        var armies = definition.Armies.ToDictionary(
            item => new ArmyId(item.Id),
            item => new Army(new ArmyId(item.Id), new FactionId(item.OwnerId),
                new ProvinceId(item.ProvinceId), item.Strength, item.Upkeep));

        return new WorldState(
            definition.Id,
            definition.Name,
            definition.StartYear,
            definition.StartingTurn,
            new ReadOnlyDictionary<FactionId, Faction>(factions),
            new ReadOnlyDictionary<ProvinceId, Province>(provinces),
            new ReadOnlyDictionary<ArmyId, Army>(armies));
    }

    public static void Validate(ScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();

        RequireText(definition.Id, "Scenario id", errors);
        RequireText(definition.Name, "Scenario name", errors);
        if (definition.StartingTurn < 1) errors.Add("Starting turn must be at least 1.");
        if (definition.Factions.Count == 0) errors.Add("At least one faction is required.");
        if (definition.Provinces.Count == 0) errors.Add("At least one province is required.");

        ValidateUniqueIds(definition.Factions.Select(item => item.Id), "faction", errors);
        ValidateUniqueIds(definition.Provinces.Select(item => item.Id), "province", errors);
        ValidateUniqueIds(definition.Armies.Select(item => item.Id), "army", errors);

        var factionIds = definition.Factions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var provinceById = new Dictionary<string, ProvinceDefinition>(StringComparer.Ordinal);
        foreach (var province in definition.Provinces)
        {
            provinceById.TryAdd(province.Id, province);
        }

        foreach (var faction in definition.Factions)
        {
            RequireText(faction.Name, $"Faction '{faction.Id}' name", errors);
            if (!HexColorRegex().IsMatch(faction.Color)) errors.Add($"Faction '{faction.Id}' has an invalid HTML color.");
            if (faction.Treasury < 0 || faction.FoodReserves < 0 || faction.MilitaryStrength < 0)
                errors.Add($"Faction '{faction.Id}' resources cannot be negative.");
            if (faction.Stability is < 0 or > 100) errors.Add($"Faction '{faction.Id}' stability must be between 0 and 100.");
        }

        foreach (var province in definition.Provinces)
        {
            RequireText(province.Name, $"Province '{province.Id}' name", errors);
            if (!factionIds.Contains(province.OwnerId)) errors.Add($"Province '{province.Id}' has unknown owner '{province.OwnerId}'.");
            if (province.Population < 0 || province.AgriculturalProduction < 0 || province.TaxRevenue < 0 || province.AdministrativeBurden < 0)
                errors.Add($"Province '{province.Id}' economic values cannot be negative.");
            if (province.Loyalty is < 0 or > 100) errors.Add($"Province '{province.Id}' loyalty must be between 0 and 100.");
            if (province.MapPolygon.Count < 3) errors.Add($"Province '{province.Id}' map polygon needs at least three points.");
            if (province.Neighbors.Contains(province.Id, StringComparer.Ordinal)) errors.Add($"Province '{province.Id}' cannot neighbor itself.");
            if (province.Neighbors.Count != province.Neighbors.Distinct(StringComparer.Ordinal).Count()) errors.Add($"Province '{province.Id}' has duplicate neighbors.");

            foreach (var neighborId in province.Neighbors)
            {
                if (!provinceById.TryGetValue(neighborId, out var neighbor))
                {
                    errors.Add($"Province '{province.Id}' has unknown neighbor '{neighborId}'.");
                }
                else if (!neighbor.Neighbors.Contains(province.Id, StringComparer.Ordinal))
                {
                    errors.Add($"Province connection '{province.Id}' to '{neighborId}' must be symmetric.");
                }
            }
        }

        foreach (var army in definition.Armies)
        {
            if (!factionIds.Contains(army.OwnerId)) errors.Add($"Army '{army.Id}' has unknown owner '{army.OwnerId}'.");
            if (!provinceById.TryGetValue(army.ProvinceId, out var province)) errors.Add($"Army '{army.Id}' has unknown province '{army.ProvinceId}'.");
            else if (!string.Equals(province.OwnerId, army.OwnerId, StringComparison.Ordinal)) errors.Add($"Army '{army.Id}' must begin in a friendly province.");
            if (army.Strength <= 0 || army.Upkeep < 0) errors.Add($"Army '{army.Id}' must have positive strength and non-negative upkeep.");
        }

        if (errors.Count > 0) throw new ScenarioValidationException(errors);
    }

    private static void ValidateUniqueIds(IEnumerable<string> ids, string kind, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            RequireText(id, $"{kind} id", errors);
            if (!seen.Add(id)) errors.Add($"Duplicate {kind} id '{id}'.");
        }
    }

    private static void RequireText(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{label} is required.");
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
