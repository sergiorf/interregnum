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

        var result = engine.Execute(new RecruitArmy(PlayerId, OwnedProvinceId));

        Assert.False(result.Succeeded);
        Assert.Equal(CommandErrorCode.CommandNotAvailable, result.ErrorCode);
        Assert.Same(original, result.State);
        Assert.Same(original, engine.State);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void EndTurn_ProcessesApprovedEconomyAndReconcilesPlayerReport()
    {
        var original = LoadWorld();
        var engine = new SimulationEngine(original);

        var result = engine.Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.State.Turn);
        Assert.Equal(1, original.Turn);
        var report = Assert.IsType<TurnReport>(result.TurnReport);
        Assert.Equal(1, report.CompletedTurn);
        Assert.Equal(2, report.NextTurn);
        Assert.Equal(
            ["frankish-kingdom", "roman-domain", "visigothic-kingdom"],
            report.Factions.Select(item => item.FactionId.Value));

        var player = Assert.Single(report.Factions, item => item.FactionId == PlayerId);
        Assert.Equal(160, player.OpeningFood);
        Assert.Equal(105, player.FoodProduction);
        Assert.Equal(112, player.FoodDemand);
        Assert.Equal(112, player.FoodConsumed);
        Assert.Equal(0, player.UnmetFood);
        Assert.Equal(153, player.ClosingFood);
        Assert.Equal(220, player.OpeningTreasury);
        Assert.Equal(52, player.TaxRevenue);
        Assert.Equal(8, player.UpkeepDue);
        Assert.Equal(8, player.UpkeepPaid);
        Assert.Equal(0, player.UpkeepUnpaid);
        Assert.Equal(28, player.AdministrationDue);
        Assert.Equal(28, player.AdministrationPaid);
        Assert.Equal(0, player.AdministrationUnpaid);
        Assert.Equal(236, player.ClosingTreasury);
        Assert.Equal(153, result.State.Factions[PlayerId].FoodReserves);
        Assert.Equal(236, result.State.Factions[PlayerId].Treasury);

        Assert.Equal(
            player.OpeningFood + player.FoodProduction - player.FoodConsumed,
            player.ClosingFood);
        Assert.Equal(
            player.OpeningTreasury + player.TaxRevenue - player.UpkeepPaid - player.AdministrationPaid,
            player.ClosingTreasury);
    }

    [Fact]
    public void EndTurn_UsesStablePipelineAndEntityEventOrder()
    {
        var result = new SimulationEngine(LoadWorld()).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        Assert.IsType<ProvinceFoodProduced>(result.Events[0]);
        Assert.Equal(
            ["alemannia", "belgica", "ripuaria"],
            result.Events.OfType<ProvinceFoodProduced>().Take(3).Select(item => item.ProvinceId.Value));

        var eventKinds = result.Events.Select(item => item.GetType()).Distinct().ToArray();
        Assert.Equal(
            [
                typeof(ProvinceFoodProduced),
                typeof(FactionFoodConsumed),
                typeof(ProvinceTaxCollected),
                typeof(ArmyUpkeepPaid),
                typeof(ProvinceAdministrationPaid),
                typeof(TurnAdvanced)
            ],
            eventKinds);
        Assert.Equal(new TurnAdvanced(1, 2), Assert.IsType<TurnAdvanced>(result.Events[^1]));
    }

    [Fact]
    public void EndTurn_ReportsFoodShortageAndPartiallyPaidMandatoryCosts()
    {
        var original = LoadWorld();
        var player = original.Factions[PlayerId] with { FoodReserves = 0, Treasury = 0 };
        var factions = original.Factions.ToDictionary(item => item.Key, item => item.Value);
        factions[PlayerId] = player;
        var poorWorld = new WorldState(
            original.ScenarioId,
            original.Name,
            original.StartYear,
            original.Turn,
            factions,
            original.Provinces,
            original.Armies);

        var result = new SimulationEngine(poorWorld).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        var report = Assert.Single(result.TurnReport!.Factions, item => item.FactionId == PlayerId);
        Assert.Equal(105, report.FoodConsumed);
        Assert.Equal(7, report.UnmetFood);
        Assert.Equal(0, report.ClosingFood);
        Assert.Equal(52, report.TaxRevenue);
        Assert.Equal(8, report.UpkeepPaid);
        Assert.Equal(28, report.AdministrationPaid);
        Assert.Equal(16, report.ClosingTreasury);

        var zeroRevenueProvinces = original.Provinces.ToDictionary(
            item => item.Key,
            item => item.Value.OwnerId == PlayerId
                ? new Province(
                    item.Value.Id,
                    item.Value.Name,
                    item.Value.OwnerId,
                    item.Value.Population,
                    item.Value.AgriculturalProduction,
                    0,
                    item.Value.Loyalty,
                    item.Value.AdministrativeBurden,
                    item.Value.TaxRate,
                    item.Value.Settlement,
                    item.Value.Neighbors)
                : item.Value);
        var insolventWorld = new WorldState(
            original.ScenarioId,
            original.Name,
            original.StartYear,
            original.Turn,
            factions,
            zeroRevenueProvinces,
            original.Armies);

        var insolvent = new SimulationEngine(insolventWorld).Execute(new EndTurn(PlayerId));
        var insolventReport = Assert.Single(
            insolvent.TurnReport!.Factions, item => item.FactionId == PlayerId);
        Assert.Equal(8, insolventReport.UpkeepUnpaid);
        Assert.Equal(28, insolventReport.AdministrationUnpaid);
        Assert.Equal(0, insolventReport.ClosingTreasury);
    }

    [Fact]
    public void EndTurn_RejectsOverflowWithoutChangingState()
    {
        var original = LoadWorld();
        var factions = original.Factions.ToDictionary(item => item.Key, item => item.Value);
        factions[PlayerId] = factions[PlayerId] with { Treasury = int.MaxValue };
        var overflowWorld = new WorldState(
            original.ScenarioId,
            original.Name,
            original.StartYear,
            original.Turn,
            factions,
            original.Provinces,
            original.Armies);
        var engine = new SimulationEngine(overflowWorld);

        var result = engine.Execute(new EndTurn(PlayerId));

        Assert.False(result.Succeeded);
        Assert.Equal(CommandErrorCode.ArithmeticOverflow, result.ErrorCode);
        Assert.Same(overflowWorld, result.State);
        Assert.Same(overflowWorld, engine.State);
        Assert.Null(result.TurnReport);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void EndTurn_ReplaysIdenticallyForTwentyTurns()
    {
        var first = new SimulationEngine(LoadWorld());
        var second = new SimulationEngine(LoadWorld());

        for (var turn = 0; turn < 20; turn++)
        {
            var firstResult = first.Execute(new EndTurn(PlayerId));
            var secondResult = second.Execute(new EndTurn(PlayerId));

            Assert.True(firstResult.Succeeded);
            Assert.True(secondResult.Succeeded);
            Assert.Equal(firstResult.Events, secondResult.Events);
            Assert.Equal(firstResult.State.Turn, secondResult.State.Turn);
            Assert.Equal(
                firstResult.State.Factions.OrderBy(item => item.Key.Value).Select(item => item.Value),
                secondResult.State.Factions.OrderBy(item => item.Key.Value).Select(item => item.Value));
        }
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
