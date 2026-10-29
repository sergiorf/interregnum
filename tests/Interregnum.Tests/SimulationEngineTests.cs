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
        Assert.Equal(68, player.Stability.OpeningStability);
        Assert.Equal(1, player.Stability.RecoveryModifier);
        Assert.Equal(69, player.Stability.ClosingStability);
        Assert.Equal(69, result.State.Factions[PlayerId].Stability);

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
                typeof(ProvinceLoyaltyChanged),
                typeof(FactionStabilityChanged),
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
    public void EndTurn_AppliesTaxLoyaltyAndReconcilesClampAdjustment()
    {
        var world = WithProvinceState(
            LoadWorld(),
            new ProvinceId("armorica"),
            loyalty: 99,
            taxRate: TaxRate.Low);

        var result = new SimulationEngine(world).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        var report = Assert.Single(result.TurnReport!.Factions, item => item.FactionId == PlayerId);
        var loyalty = Assert.Single(
            report.LoyaltyByProvince, item => item.ProvinceId == new ProvinceId("armorica"));
        Assert.Equal(99, loyalty.OpeningLoyalty);
        Assert.Equal(4, loyalty.TaxPolicyModifier);
        Assert.Equal(0, loyalty.FoodShortageModifier);
        Assert.Equal(0, loyalty.AdministrationModifier);
        Assert.Equal(-3, loyalty.ClampAdjustment);
        Assert.Equal(100, loyalty.ClosingLoyalty);
        Assert.Equal(
            loyalty.OpeningLoyalty
            + loyalty.TaxPolicyModifier
            + loyalty.FoodShortageModifier
            + loyalty.AdministrationModifier
            + loyalty.ClampAdjustment,
            loyalty.ClosingLoyalty);
    }

    [Fact]
    public void EndTurn_AppliesFactionWideShortageAndUnpaidObligationPenalties()
    {
        var original = LoadWorld();
        var factions = original.Factions.ToDictionary(item => item.Key, item => item.Value);
        factions[PlayerId] = factions[PlayerId] with { FoodReserves = 0, Treasury = 0 };
        var provinces = original.Provinces.ToDictionary(
            item => item.Key,
            item => item.Value.OwnerId == PlayerId
                ? CopyProvince(item.Value, taxRevenue: 0)
                : item.Value);
        var world = new WorldState(
            original.ScenarioId,
            original.Name,
            original.StartYear,
            original.Turn,
            factions,
            provinces,
            original.Armies);

        var result = new SimulationEngine(world).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        var report = Assert.Single(result.TurnReport!.Factions, item => item.FactionId == PlayerId);
        Assert.Equal(7, report.UnmetFood);
        Assert.Equal(8, report.UpkeepUnpaid);
        Assert.Equal(28, report.AdministrationUnpaid);
        Assert.All(report.LoyaltyByProvince, item => Assert.Equal(-1, item.FoodShortageModifier));
        Assert.All(report.LoyaltyByProvince, item => Assert.Equal(-4, item.AdministrationModifier));
        var aurelianum = Assert.Single(
            report.LoyaltyByProvince, item => item.ProvinceId == OwnedProvinceId);
        Assert.Equal(73, aurelianum.ClosingLoyalty);
        Assert.Equal(-1, report.Stability.FoodShortageModifier);
        Assert.Equal(-5, report.Stability.UpkeepModifier);
        Assert.Equal(-5, report.Stability.AdministrationModifier);
        Assert.Equal(57, report.Stability.ClosingStability);
    }

    [Fact]
    public void EndTurn_NewRebellionPaysCurrentTaxThenSuppressesFollowingTurnTax()
    {
        var world = WithProvinceState(
            LoadWorld(), OwnedProvinceId, loyalty: 36, taxRate: TaxRate.High);
        var engine = new SimulationEngine(world);

        var first = engine.Execute(new EndTurn(PlayerId));

        Assert.True(first.Succeeded);
        var firstReport = Assert.Single(first.TurnReport!.Factions, item => item.FactionId == PlayerId);
        var firstTax = Assert.Single(firstReport.TaxByProvince, item => item.ProvinceId == OwnedProvinceId);
        Assert.Equal(20, firstTax.Collected);
        Assert.False(firstTax.SuppressedByRebellion);
        Assert.Equal(30, first.State.Provinces[OwnedProvinceId].Loyalty);
        Assert.True(first.State.Provinces[OwnedProvinceId].IsRebelling);
        Assert.Contains(first.Events, item => item is ProvinceRebellionChanged
        {
            ProvinceId: var provinceId,
            IsRebelling: true,
            Loyalty: 30
        } && provinceId == OwnedProvinceId);

        var second = engine.Execute(new EndTurn(PlayerId));

        Assert.True(second.Succeeded);
        var secondReport = Assert.Single(second.TurnReport!.Factions, item => item.FactionId == PlayerId);
        var secondTax = Assert.Single(secondReport.TaxByProvince, item => item.ProvinceId == OwnedProvinceId);
        Assert.Equal(0, secondTax.Collected);
        Assert.True(secondTax.SuppressedByRebellion);
        Assert.True(second.State.Provinces[OwnedProvinceId].IsRebelling);
    }

    [Fact]
    public void EndTurn_RecoveringProvincePaysTaxOnFollowingTurn()
    {
        var world = WithProvinceState(
            LoadWorld(), OwnedProvinceId, loyalty: 41, taxRate: TaxRate.Low, isRebelling: true);
        var engine = new SimulationEngine(world);

        var recovery = engine.Execute(new EndTurn(PlayerId));

        Assert.True(recovery.Succeeded);
        var recoveryReport = Assert.Single(
            recovery.TurnReport!.Factions, item => item.FactionId == PlayerId);
        var recoveryTax = Assert.Single(
            recoveryReport.TaxByProvince, item => item.ProvinceId == OwnedProvinceId);
        Assert.Equal(0, recoveryTax.Collected);
        Assert.True(recoveryTax.SuppressedByRebellion);
        Assert.Equal(45, recovery.State.Provinces[OwnedProvinceId].Loyalty);
        Assert.False(recovery.State.Provinces[OwnedProvinceId].IsRebelling);

        var following = engine.Execute(new EndTurn(PlayerId));

        var followingReport = Assert.Single(
            following.TurnReport!.Factions, item => item.FactionId == PlayerId);
        var followingTax = Assert.Single(
            followingReport.TaxByProvince, item => item.ProvinceId == OwnedProvinceId);
        Assert.Equal(6, followingTax.Collected);
        Assert.False(followingTax.SuppressedByRebellion);
    }

    [Fact]
    public void EndTurn_MultipleRebellionsApplyOneStabilityPenaltyEach()
    {
        var world = WithProvinceState(
            LoadWorld(), OwnedProvinceId, loyalty: 36, taxRate: TaxRate.High);
        world = WithProvinceState(
            world, new ProvinceId("arvernia"), loyalty: 36, taxRate: TaxRate.High);

        var result = new SimulationEngine(world).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        var report = Assert.Single(result.TurnReport!.Factions, item => item.FactionId == PlayerId);
        Assert.Equal(2, report.RebellionByProvince.Count(item => item.Changed));
        Assert.Equal(-10, report.Stability.RebellionModifier);
        Assert.Equal(58, report.Stability.ClosingStability);
    }

    [Theory]
    [InlineData(31, false, TaxRate.Normal, 30, true, false)]
    [InlineData(32, false, TaxRate.Normal, 31, false, true)]
    [InlineData(40, true, TaxRate.Low, 44, true, false)]
    [InlineData(41, true, TaxRate.Low, 45, false, true)]
    public void EndTurn_UsesRebellionAndRecoveryHysteresis(
        int openingLoyalty,
        bool openingRebellion,
        TaxRate taxRate,
        int expectedLoyalty,
        bool expectedRebellion,
        bool expectedAtRisk)
    {
        var world = WithProvinceState(
            LoadWorld(), OwnedProvinceId, openingLoyalty, taxRate, openingRebellion);

        var result = new SimulationEngine(world).Execute(new EndTurn(PlayerId));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedLoyalty, result.State.Provinces[OwnedProvinceId].Loyalty);
        Assert.Equal(expectedRebellion, result.State.Provinces[OwnedProvinceId].IsRebelling);
        Assert.Equal(expectedAtRisk, result.State.Provinces[OwnedProvinceId].IsAtRisk);
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
            Assert.Equal(
                firstResult.State.Provinces.OrderBy(item => item.Key.Value).Select(item =>
                    (item.Value.Id, item.Value.Loyalty, item.Value.IsRebelling, item.Value.TaxRate)),
                secondResult.State.Provinces.OrderBy(item => item.Key.Value).Select(item =>
                    (item.Value.Id, item.Value.Loyalty, item.Value.IsRebelling, item.Value.TaxRate)));
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

    private static WorldState WithProvinceState(
        WorldState world,
        ProvinceId provinceId,
        int loyalty,
        TaxRate taxRate,
        bool isRebelling = false)
    {
        var provinces = world.Provinces.ToDictionary(item => item.Key, item => item.Value);
        provinces[provinceId] = CopyProvince(
            provinces[provinceId],
            loyalty: loyalty,
            taxRate: taxRate,
            isRebelling: isRebelling);
        return new WorldState(
            world.ScenarioId,
            world.Name,
            world.StartYear,
            world.Turn,
            world.Factions,
            provinces,
            world.Armies);
    }

    private static Province CopyProvince(
        Province province,
        int? taxRevenue = null,
        int? loyalty = null,
        TaxRate? taxRate = null,
        bool? isRebelling = null) =>
        new(
            province.Id,
            province.Name,
            province.OwnerId,
            province.Population,
            province.AgriculturalProduction,
            taxRevenue ?? province.TaxRevenue,
            loyalty ?? province.Loyalty,
            province.AdministrativeBurden,
            taxRate ?? province.TaxRate,
            province.Settlement,
            province.Neighbors,
            isRebelling ?? province.IsRebelling);
}
