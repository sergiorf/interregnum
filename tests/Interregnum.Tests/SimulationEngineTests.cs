using Interregnum.Core.Domain;
using Interregnum.Core.Scenarios;
using Interregnum.Core.Simulation;
using Xunit;

namespace Interregnum.Tests;

public sealed class SimulationEngineTests
{
    private static readonly FactionId PlayerId = new("roman-domain");
    private static readonly ProvinceId OwnedProvinceId = new("aurelianum");

    [Fact]
    public void SetTaxRate_UpdatesOwnedProvinceAndEmitsExactChange()
    {
        var original = LoadWorld();
        var engine = new SimulationEngine(original);

        var result = engine.Execute(new SetProvinceTaxRate(PlayerId, OwnedProvinceId, TaxRate.High));

        Assert.True(result.Succeeded);
        Assert.Equal(CommandErrorCode.None, result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.NotSame(original, result.State);
        Assert.Same(engine.State, result.State);
        Assert.Equal(TaxRate.Normal, original.Provinces[OwnedProvinceId].TaxRate);
        Assert.Equal(TaxRate.High, result.State.Provinces[OwnedProvinceId].TaxRate);
        var changed = Assert.IsType<ProvinceTaxRateChanged>(Assert.Single(result.Events));
        Assert.Equal(new ProvinceTaxRateChanged(
            PlayerId, OwnedProvinceId, TaxRate.Normal, TaxRate.High), changed);
    }

    [Theory]
    [MemberData(nameof(RejectedCommands))]
    public void SetTaxRate_RejectsInvalidCommandsWithoutChangingState(
        GameCommand command,
        CommandErrorCode expectedError)
    {
        var original = LoadWorld();
        var engine = new SimulationEngine(original);

        var result = engine.Execute(command);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Same(original, result.State);
        Assert.Same(original, engine.State);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void RejectedTaxCommand_ProducesStableErrorResult()
    {
        var command = new SetProvinceTaxRate(PlayerId, OwnedProvinceId, TaxRate.Normal);

        var first = new SimulationEngine(LoadWorld()).Execute(command);
        var second = new SimulationEngine(LoadWorld()).Execute(command);

        Assert.Equal(first.ErrorCode, second.ErrorCode);
        Assert.Equal(first.ErrorMessage, second.ErrorMessage);
        Assert.Empty(first.Events);
        Assert.Empty(second.Events);
    }

    [Fact]
    public void FutureCommand_IsRejectedAtomicallyUntilItsRulesAreImplemented()
    {
        var original = LoadWorld();
        var engine = new SimulationEngine(original);

        var result = engine.Execute(new EndTurn(PlayerId));

        Assert.False(result.Succeeded);
        Assert.Equal(CommandErrorCode.CommandNotAvailable, result.ErrorCode);
        Assert.Same(original, result.State);
        Assert.Same(original, engine.State);
        Assert.Empty(result.Events);
    }

    public static TheoryData<GameCommand, CommandErrorCode> RejectedCommands => new()
    {
        {
            new SetProvinceTaxRate(new FactionId("missing"), OwnedProvinceId, TaxRate.High),
            CommandErrorCode.InvalidActor
        },
        {
            new SetProvinceTaxRate(new FactionId("visigoths"), new ProvinceId("aquitania"), TaxRate.High),
            CommandErrorCode.InvalidActor
        },
        {
            new SetProvinceTaxRate(PlayerId, new ProvinceId("missing"), TaxRate.High),
            CommandErrorCode.UnknownProvince
        },
        {
            new SetProvinceTaxRate(PlayerId, new ProvinceId("aquitania"), TaxRate.High),
            CommandErrorCode.ForeignProvince
        },
        {
            new SetProvinceTaxRate(PlayerId, OwnedProvinceId, (TaxRate)99),
            CommandErrorCode.InvalidTaxRate
        },
        {
            new SetProvinceTaxRate(PlayerId, OwnedProvinceId, TaxRate.Normal),
            CommandErrorCode.TaxRateUnchanged
        }
    };

    private static WorldState LoadWorld()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "gaul-480.json");
        using var stream = File.OpenRead(path);
        return ScenarioLoader.CreateWorld(ScenarioLoader.LoadDefinition(stream));
    }
}
