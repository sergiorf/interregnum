using Interregnum.Core.Domain;

namespace Interregnum.Core.Simulation;

public sealed class SimulationEngine
{
    public SimulationEngine(WorldState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        State = initialState;
    }

    public WorldState State { get; private set; }

    public CommandResult Execute(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!State.Factions.TryGetValue(command.ActorId, out var actor) || !actor.IsPlayable)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.InvalidActor,
                $"Faction '{command.ActorId}' is not the active playable actor.");
        }

        return command switch
        {
            SetProvinceTaxRate setTaxRate => Execute(setTaxRate),
            EndTurn endTurn => Execute(endTurn),
            _ => CommandResult.Failure(
                State,
                CommandErrorCode.CommandNotAvailable,
                $"Command '{command.GetType().Name}' is not available in the current simulation slice.")
        };
    }

    private CommandResult Execute(EndTurn command)
    {
        try
        {
            var completedTurn = State.Turn;
            var nextTurn = checked(completedTurn + 1);
            var reports = BuildFactionReports();
            var factions = State.Factions.ToDictionary(item => item.Key, item => item.Value);

            foreach (var report in reports)
            {
                var faction = factions[report.FactionId];
                factions[report.FactionId] = faction with
                {
                    FoodReserves = report.ClosingFood,
                    Treasury = report.ClosingTreasury,
                    Stability = report.Stability.ClosingStability
                };
            }

            var provinces = State.Provinces.ToDictionary(item => item.Key, item => item.Value);
            foreach (var report in reports)
            {
                foreach (var loyalty in report.LoyaltyByProvince)
                {
                    var rebellion = report.RebellionByProvince.Single(
                        item => item.ProvinceId == loyalty.ProvinceId);
                    provinces[loyalty.ProvinceId] = provinces[loyalty.ProvinceId]
                        .WithTurnOutcome(loyalty.ClosingLoyalty, rebellion.ClosingIsRebelling);
                }
            }

            var events = BuildTurnEvents(reports, completedTurn, nextTurn);
            State = State.WithTurnOutcome(factions, provinces, nextTurn);
            return CommandResult.Success(
                State,
                new TurnReport(completedTurn, nextTurn, reports),
                events);
        }
        catch (OverflowException)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.ArithmeticOverflow,
                "The turn could not be processed because an economic calculation exceeded its supported range.");
        }
    }

    private IReadOnlyList<FactionTurnReport> BuildFactionReports()
    {
        var reports = new List<FactionTurnReport>();
        foreach (var faction in State.Factions.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            var provinces = State.Provinces.Values
                .Where(item => item.OwnerId == faction.Id)
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .ToArray();
            var armies = State.Armies.Values
                .Where(item => item.OwnerId == faction.Id)
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .ToArray();

            var producedFood = provinces.Aggregate(0,
                (total, province) => checked(total + province.AgriculturalProduction));
            var availableFood = checked(faction.FoodReserves + producedFood);
            var foodReports = provinces.Select(province => new ProvinceFoodReport(
                province.Id,
                province.AgriculturalProduction,
                checked(province.Population / 4 + (province.Population % 4 == 0 ? 0 : 1))))
                .ToArray();
            var foodDemand = foodReports.Aggregate(0, (total, item) => checked(total + item.Demand));
            var foodConsumed = Math.Min(availableFood, foodDemand);
            var unmetFood = checked(foodDemand - foodConsumed);
            availableFood = checked(availableFood - foodConsumed);

            var treasury = faction.Treasury;
            var taxReports = new List<ProvinceTaxReport>();
            foreach (var province in provinces)
            {
                var collected = province.IsRebelling
                    ? 0
                    : ApplyTaxRate(province.TaxRevenue, province.TaxRate);
                treasury = checked(treasury + collected);
                taxReports.Add(new ProvinceTaxReport(
                    province.Id,
                    province.TaxRevenue,
                    province.TaxRate,
                    province.IsRebelling,
                    collected));
            }

            var upkeepReports = new List<ArmyUpkeepReport>();
            foreach (var army in armies)
            {
                var paid = Math.Min(treasury, army.Upkeep);
                treasury = checked(treasury - paid);
                upkeepReports.Add(new ArmyUpkeepReport(
                    army.Id, army.Upkeep, paid, checked(army.Upkeep - paid)));
            }

            var administrationReports = new List<ProvinceAdministrationReport>();
            foreach (var province in provinces)
            {
                var paid = Math.Min(treasury, province.AdministrativeBurden);
                treasury = checked(treasury - paid);
                administrationReports.Add(new ProvinceAdministrationReport(
                    province.Id,
                    province.AdministrativeBurden,
                    paid,
                    checked(province.AdministrativeBurden - paid)));
            }

            var foodShortageModifier = -ScalePenalty(unmetFood, foodDemand, 10);
            var loyaltyReports = new List<ProvinceLoyaltyReport>();
            var rebellionReports = new List<ProvinceRebellionReport>();
            foreach (var province in provinces)
            {
                var taxPolicyModifier = TaxLoyaltyModifier(province.TaxRate);
                var administration = administrationReports.Single(
                    item => item.ProvinceId == province.Id);
                var administrationModifier = -ScalePenalty(
                    administration.Unpaid, administration.Due, 4);
                var rawLoyalty = checked(
                    province.Loyalty
                    + taxPolicyModifier
                    + foodShortageModifier
                    + administrationModifier);
                var closingLoyalty = Math.Clamp(rawLoyalty, 0, 100);
                var clampAdjustment = checked(closingLoyalty - rawLoyalty);
                var isRebelling = province.IsRebelling
                    ? closingLoyalty < Province.RecoveryThreshold
                    : closingLoyalty <= Province.RebellionThreshold;

                loyaltyReports.Add(new ProvinceLoyaltyReport(
                    province.Id,
                    province.Loyalty,
                    taxPolicyModifier,
                    foodShortageModifier,
                    administrationModifier,
                    clampAdjustment,
                    closingLoyalty));
                rebellionReports.Add(new ProvinceRebellionReport(
                    province.Id, province.IsRebelling, isRebelling));
            }

            var upkeepDue = SumChecked(upkeepReports.Select(item => item.Due));
            var upkeepUnpaid = SumChecked(upkeepReports.Select(item => item.Unpaid));
            var administrationDue = SumChecked(administrationReports.Select(item => item.Due));
            var administrationUnpaid = SumChecked(administrationReports.Select(item => item.Unpaid));
            var rebelCount = rebellionReports.Count(item => item.ClosingIsRebelling);
            var rebellionModifier = checked(-5 * rebelCount);
            var upkeepModifier = -ScalePenalty(upkeepUnpaid, upkeepDue, 5);
            var stabilityAdministrationModifier = -ScalePenalty(
                administrationUnpaid, administrationDue, 5);
            var recoveryModifier = rebelCount == 0
                                   && unmetFood == 0
                                   && upkeepUnpaid == 0
                                   && administrationUnpaid == 0
                ? 1
                : 0;
            var rawStability = checked(
                faction.Stability
                + recoveryModifier
                + rebellionModifier
                + foodShortageModifier
                + upkeepModifier
                + stabilityAdministrationModifier);
            var closingStability = Math.Clamp(rawStability, 0, 100);
            var stability = new FactionStabilityReport(
                faction.Stability,
                recoveryModifier,
                rebellionModifier,
                foodShortageModifier,
                upkeepModifier,
                stabilityAdministrationModifier,
                checked(closingStability - rawStability),
                closingStability);

            reports.Add(new FactionTurnReport(
                faction.Id,
                faction.FoodReserves,
                availableFood,
                foodConsumed,
                unmetFood,
                foodReports,
                faction.Treasury,
                treasury,
                taxReports,
                upkeepReports,
                administrationReports,
                loyaltyReports,
                rebellionReports,
                stability));
        }

        return reports;
    }

    private static IReadOnlyList<DomainEvent> BuildTurnEvents(
        IReadOnlyList<FactionTurnReport> reports,
        int completedTurn,
        int nextTurn)
    {
        var events = new List<DomainEvent>();
        events.AddRange(reports.SelectMany(report => report.FoodByProvince.Select(item =>
            new ProvinceFoodProduced(report.FactionId, item.ProvinceId, item.Production))));
        events.AddRange(reports.Select(report => new FactionFoodConsumed(
            report.FactionId, report.FoodDemand, report.FoodConsumed, report.UnmetFood)));
        events.AddRange(reports.SelectMany(report => report.TaxByProvince.Select(item =>
            new ProvinceTaxCollected(
                report.FactionId,
                item.ProvinceId,
                item.BaseRevenue,
                item.TaxRate,
                item.SuppressedByRebellion,
                item.Collected))));
        events.AddRange(reports.SelectMany(report => report.UpkeepByArmy.Select(item =>
            new ArmyUpkeepPaid(report.FactionId, item.ArmyId, item.Due, item.Paid, item.Unpaid))));
        events.AddRange(reports.SelectMany(report => report.AdministrationByProvince.Select(item =>
            new ProvinceAdministrationPaid(
                report.FactionId, item.ProvinceId, item.Due, item.Paid, item.Unpaid))));
        events.AddRange(reports.SelectMany(report => report.LoyaltyByProvince.Select(item =>
            new ProvinceLoyaltyChanged(
                report.FactionId,
                item.ProvinceId,
                item.OpeningLoyalty,
                item.TaxPolicyModifier,
                item.FoodShortageModifier,
                item.AdministrationModifier,
                item.ClampAdjustment,
                item.ClosingLoyalty))));
        events.AddRange(reports.SelectMany(report => report.RebellionByProvince
            .Where(item => item.Changed)
            .Select(item => new ProvinceRebellionChanged(
                report.FactionId,
                item.ProvinceId,
                item.ClosingIsRebelling,
                report.LoyaltyByProvince.Single(
                    loyalty => loyalty.ProvinceId == item.ProvinceId).ClosingLoyalty))));
        events.AddRange(reports.Select(report => new FactionStabilityChanged(
            report.FactionId,
            report.Stability.OpeningStability,
            report.Stability.RecoveryModifier,
            report.Stability.RebellionModifier,
            report.Stability.FoodShortageModifier,
            report.Stability.UpkeepModifier,
            report.Stability.AdministrationModifier,
            report.Stability.ClampAdjustment,
            report.Stability.ClosingStability)));
        events.Add(new TurnAdvanced(completedTurn, nextTurn));
        return events;
    }

    private static int TaxLoyaltyModifier(TaxRate taxRate) => taxRate switch
    {
        TaxRate.Low => 4,
        TaxRate.Normal => -1,
        TaxRate.High => -6,
        _ => throw new InvalidOperationException($"Unsupported tax rate '{taxRate}'.")
    };

    private static int ScalePenalty(int amount, int total, int maximum)
    {
        if (amount == 0 || total == 0) return 0;

        var scaled = checked(amount * maximum);
        return checked(scaled / total + (scaled % total == 0 ? 0 : 1));
    }

    private static int SumChecked(IEnumerable<int> values) =>
        values.Aggregate(0, (total, value) => checked(total + value));

    private static int ApplyTaxRate(int baseRevenue, TaxRate taxRate)
    {
        var percentage = taxRate switch
        {
            TaxRate.Low => 30,
            TaxRate.Normal => 60,
            TaxRate.High => 100,
            _ => throw new InvalidOperationException($"Unsupported tax rate '{taxRate}'.")
        };
        return checked(baseRevenue * percentage) / 100;
    }

    private CommandResult Execute(SetProvinceTaxRate command)
    {
        if (!State.Provinces.TryGetValue(command.ProvinceId, out var province))
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.UnknownProvince,
                $"Province '{command.ProvinceId}' does not exist.");
        }

        if (province.OwnerId != command.ActorId)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.ForeignProvince,
                $"Province '{province.Name}' is not controlled by '{command.ActorId}'.");
        }

        if (!Enum.IsDefined(command.TaxRate))
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.InvalidTaxRate,
                $"Tax rate value '{(int)command.TaxRate}' is invalid.");
        }

        if (province.TaxRate == command.TaxRate)
        {
            return CommandResult.Failure(
                State,
                CommandErrorCode.TaxRateUnchanged,
                $"Province '{province.Name}' is already using the {command.TaxRate} tax rate.");
        }

        var previousRate = province.TaxRate;
        State = State.WithProvince(province.WithTaxRate(command.TaxRate));
        return CommandResult.Success(
            State,
            new ProvinceTaxRateChanged(command.ActorId, command.ProvinceId, previousRate, command.TaxRate));
    }
}
