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

    private static ScenarioDefinition LoadGaulDefinition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "gaul-480.json");
        using var stream = File.OpenRead(path);
        return ScenarioLoader.LoadDefinition(stream);
    }
}
