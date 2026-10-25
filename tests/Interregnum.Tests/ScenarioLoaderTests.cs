using System.Text;
using Interregnum.Core.Domain;
using Interregnum.Core.Scenarios;
using Xunit;

namespace Interregnum.Tests;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void GaulScenario_LoadsTwelveProvincesAndThreeFactions()
    {
        var definition = LoadGaulDefinition();
        var world = ScenarioLoader.CreateWorld(definition);

        Assert.Equal("gaul-480", world.ScenarioId);
        Assert.Equal(480, world.StartYear);
        Assert.Equal(12, world.Provinces.Count);
        Assert.Equal(3, world.Factions.Count);
        Assert.Equal(3, world.Armies.Count);
        Assert.Single(world.Factions.Values, faction => faction.IsPlayable);
    }

    [Fact]
    public void GaulScenario_PreservesProvinceOwnershipAndConnections()
    {
        var world = ScenarioLoader.CreateWorld(LoadGaulDefinition());
        var aurelianum = world.Provinces[new ProvinceId("aurelianum")];

        Assert.Equal(new FactionId("roman-domain"), aurelianum.OwnerId);
        Assert.Contains(new ProvinceId("parisii"), aurelianum.Neighbors);
        Assert.Contains(new ProvinceId("aurelianum"), world.Provinces[new ProvinceId("parisii")].Neighbors);
    }

    [Fact]
    public void InvalidScenario_ReportsUnknownOwnerAndMalformedPolygon()
    {
        const string json = """
            {
              "id": "invalid", "name": "Invalid", "startYear": 480,
              "factions": [{
                "id": "known", "name": "Known", "color": "#112233",
                "treasury": 0, "foodReserves": 0, "militaryStrength": 0,
                "stability": 50, "isPlayable": true
              }],
              "provinces": [{
                "id": "p1", "name": "P1", "ownerId": "missing",
                "population": 1, "agriculturalProduction": 1, "taxRevenue": 1,
                "loyalty": 50, "administrativeBurden": 1, "taxRate": "Normal",
                "neighbors": [], "mapPolygon": [{"x":0,"y":0},{"x":1,"y":0}]
              }],
              "armies": []
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.LoadDefinition(stream));

        Assert.Contains(exception.Errors, error => error.Contains("unknown owner", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("at least three points", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidScenario_RejectsAsymmetricConnections()
    {
        var definition = LoadGaulDefinition();
        var altered = definition with
        {
            Provinces = definition.Provinces.Select(province => province.Id == "armorica"
                ? province with { Neighbors = ["parisii"] }
                : province).ToList()
        };

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Validate(altered));

        Assert.Contains(exception.Errors, error => error.Contains("aquitania' to 'armorica", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{", "Invalid JSON")]
    [InlineData("""
        {
          "id": "invalid", "name": "Invalid", "startYear": 480,
          "factions": [], "provinces": [], "armies": [], "unexpected": true
        }
        """, "unexpected")]
    [InlineData("""
        {
          "id": "invalid", "name": "Invalid", "startYear": 480,
          "factions": [{
            "id": "known", "name": "Known", "color": "#112233",
            "treasury": 0, "foodReserves": 0, "militaryStrength": 0,
            "stability": 50, "isPlayable": true
          }],
          "provinces": [{
            "id": "p1", "name": "P1", "ownerId": "known",
            "population": 1, "agriculturalProduction": 1, "taxRevenue": 1,
            "loyalty": 50, "administrativeBurden": 1, "taxRate": 99,
            "neighbors": [], "mapPolygon": [{"x":0,"y":0},{"x":1,"y":0},{"x":0,"y":1}]
          }],
          "armies": []
        }
        """, "taxRate")]
    public void InvalidJson_UsesPublicScenarioValidationError(string json, string expectedErrorText)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.LoadDefinition(stream));

        Assert.Contains(exception.Errors, error => error.Contains(expectedErrorText, StringComparison.OrdinalIgnoreCase));
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public void InvalidScenario_ReportsNullCollectionsWithoutCrashing()
    {
        const string json = """
            {
              "id": "invalid", "name": "Invalid", "startYear": 480,
              "factions": null, "provinces": null, "armies": null
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.LoadDefinition(stream));

        Assert.Contains(exception.Errors, error => error.Contains("Factions collection cannot be null", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("Provinces collection cannot be null", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("Armies collection cannot be null", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void InvalidScenario_RequiresExactlyOnePlayableFaction(int playableFactionCount)
    {
        var definition = LoadGaulDefinition();
        var altered = definition with
        {
            Factions = definition.Factions.Select((faction, index) =>
                faction with { IsPlayable = index < playableFactionCount }).ToList()
        };

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Validate(altered));

        Assert.Contains(exception.Errors, error => error.Contains("Exactly one playable faction", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidScenario_RejectsNonFinitePolygonCoordinates()
    {
        var definition = LoadGaulDefinition();
        var altered = definition with
        {
            Provinces = definition.Provinces.Select(province => province.Id == "armorica"
                ? province with { MapPolygon = [new MapPoint(float.NaN, 0), .. province.MapPolygon.Skip(1)] }
                : province).ToList()
        };

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Validate(altered));

        Assert.Contains(exception.Errors, error => error.Contains("non-finite point", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidScenario_AggregatesDuplicateIdsRangesAndMissingReferences()
    {
        var definition = LoadGaulDefinition();
        var firstFaction = definition.Factions[0];
        var altered = definition with
        {
            Factions =
            [
                firstFaction with { Treasury = -1, Stability = 101 },
                definition.Factions[1] with { Id = firstFaction.Id },
                definition.Factions[2]
            ],
            Provinces = definition.Provinces.Select(province => province.Id == "armorica"
                ? province with { Population = -1, Loyalty = 101, OwnerId = "missing-faction" }
                : province).ToList(),
            Armies = definition.Armies.Select(army => army.Id == "roman-field-army"
                ? army with { ProvinceId = "missing-province", Strength = 0 }
                : army).ToList()
        };

        var exception = Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Validate(altered));

        Assert.Contains(exception.Errors, error => error.Contains("Duplicate faction id", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("resources cannot be negative", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("stability must be between", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("economic values cannot be negative", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("loyalty must be between", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("unknown owner", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("unknown province", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("positive strength", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatedWorld_DoesNotRetainMutableDefinitionCollections()
    {
        var definition = LoadGaulDefinition();
        var world = ScenarioLoader.CreateWorld(definition);
        var armoricaId = new ProvinceId("armorica");

        definition.Factions.Clear();
        definition.Provinces.Clear();
        definition.Armies.Clear();

        Assert.Equal(3, world.Factions.Count);
        Assert.Equal(12, world.Provinces.Count);
        Assert.Equal(3, world.Armies.Count);
        Assert.Contains(new ProvinceId("parisii"), world.Provinces[armoricaId].Neighbors);
    }

    [Fact]
    public void WorldStateAndProvince_DefensivelySnapshotCallerOwnedCollections()
    {
        var neighbors = new HashSet<ProvinceId> { new("neighbor") };
        var province = new Province(
            new ProvinceId("province"), "Province", new FactionId("faction"),
            1, 1, 1, 50, 1, TaxRate.Normal, null, neighbors);
        var factions = new Dictionary<FactionId, Faction>
        {
            [new FactionId("faction")] = new(
                new FactionId("faction"), "Faction", "#112233", 0, 0, 0, 50, true)
        };
        var provinces = new Dictionary<ProvinceId, Province> { [province.Id] = province };
        var armies = new Dictionary<ArmyId, Army>();
        var world = new WorldState("scenario", "Scenario", 480, 1, factions, provinces, armies);

        neighbors.Clear();
        factions.Clear();
        provinces.Clear();
        armies[new ArmyId("late-army")] = new(
            new ArmyId("late-army"), new FactionId("faction"), province.Id, 1, 0);

        Assert.Contains(new ProvinceId("neighbor"), province.Neighbors);
        Assert.Single(world.Factions);
        Assert.Single(world.Provinces);
        Assert.Empty(world.Armies);
    }

    private static ScenarioDefinition LoadGaulDefinition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "gaul-480.json");
        using var stream = File.OpenRead(path);
        return ScenarioLoader.LoadDefinition(stream);
    }
}
