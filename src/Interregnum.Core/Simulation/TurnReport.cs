using System.Collections.ObjectModel;
using Interregnum.Core.Domain;

namespace Interregnum.Core.Simulation;

public sealed record ProvinceFoodReport(
    ProvinceId ProvinceId,
    int Production,
    int Demand);

public sealed record ProvinceTaxReport(
    ProvinceId ProvinceId,
    int BaseRevenue,
    TaxRate TaxRate,
    bool SuppressedByRebellion,
    int Collected);

public sealed record ArmyUpkeepReport(
    ArmyId ArmyId,
    int Due,
    int Paid,
    int Unpaid);

public sealed record ProvinceAdministrationReport(
    ProvinceId ProvinceId,
    int Due,
    int Paid,
    int Unpaid);

public sealed record ProvinceLoyaltyReport(
    ProvinceId ProvinceId,
    int OpeningLoyalty,
    int TaxPolicyModifier,
    int FoodShortageModifier,
    int AdministrationModifier,
    int ClampAdjustment,
    int ClosingLoyalty)
{
    public int Change => ClosingLoyalty - OpeningLoyalty;
}

public sealed record ProvinceRebellionReport(
    ProvinceId ProvinceId,
    bool OpeningIsRebelling,
    bool ClosingIsRebelling)
{
    public bool Changed => OpeningIsRebelling != ClosingIsRebelling;
}

public sealed record FactionStabilityReport(
    int OpeningStability,
    int RecoveryModifier,
    int RebellionModifier,
    int FoodShortageModifier,
    int UpkeepModifier,
    int AdministrationModifier,
    int ClampAdjustment,
    int ClosingStability)
{
    public int Change => ClosingStability - OpeningStability;
}

public sealed class FactionTurnReport
{
    internal FactionTurnReport(
        FactionId factionId,
        int openingFood,
        int closingFood,
        int foodConsumed,
        int unmetFood,
        IReadOnlyList<ProvinceFoodReport> foodByProvince,
        int openingTreasury,
        int closingTreasury,
        IReadOnlyList<ProvinceTaxReport> taxByProvince,
        IReadOnlyList<ArmyUpkeepReport> upkeepByArmy,
        IReadOnlyList<ProvinceAdministrationReport> administrationByProvince,
        IReadOnlyList<ProvinceLoyaltyReport> loyaltyByProvince,
        IReadOnlyList<ProvinceRebellionReport> rebellionByProvince,
        FactionStabilityReport stability)
    {
        FactionId = factionId;
        OpeningFood = openingFood;
        ClosingFood = closingFood;
        FoodConsumed = foodConsumed;
        UnmetFood = unmetFood;
        FoodByProvince = Snapshot(foodByProvince);
        FoodProduction = SumChecked(FoodByProvince.Select(item => item.Production));
        FoodDemand = SumChecked(FoodByProvince.Select(item => item.Demand));
        OpeningTreasury = openingTreasury;
        ClosingTreasury = closingTreasury;
        TaxByProvince = Snapshot(taxByProvince);
        UpkeepByArmy = Snapshot(upkeepByArmy);
        AdministrationByProvince = Snapshot(administrationByProvince);
        LoyaltyByProvince = Snapshot(loyaltyByProvince);
        RebellionByProvince = Snapshot(rebellionByProvince);
        Stability = stability;
        TaxRevenue = SumChecked(TaxByProvince.Select(item => item.Collected));
        UpkeepDue = SumChecked(UpkeepByArmy.Select(item => item.Due));
        UpkeepPaid = SumChecked(UpkeepByArmy.Select(item => item.Paid));
        UpkeepUnpaid = SumChecked(UpkeepByArmy.Select(item => item.Unpaid));
        AdministrationDue = SumChecked(AdministrationByProvince.Select(item => item.Due));
        AdministrationPaid = SumChecked(AdministrationByProvince.Select(item => item.Paid));
        AdministrationUnpaid = SumChecked(AdministrationByProvince.Select(item => item.Unpaid));
    }

    public FactionId FactionId { get; }
    public int OpeningFood { get; }
    public int FoodProduction { get; }
    public int FoodDemand { get; }
    public int FoodConsumed { get; }
    public int UnmetFood { get; }
    public int ClosingFood { get; }
    public IReadOnlyList<ProvinceFoodReport> FoodByProvince { get; }
    public int OpeningTreasury { get; }
    public int TaxRevenue { get; }
    public int UpkeepDue { get; }
    public int UpkeepPaid { get; }
    public int UpkeepUnpaid { get; }
    public int AdministrationDue { get; }
    public int AdministrationPaid { get; }
    public int AdministrationUnpaid { get; }
    public int ClosingTreasury { get; }
    public IReadOnlyList<ProvinceTaxReport> TaxByProvince { get; }
    public IReadOnlyList<ArmyUpkeepReport> UpkeepByArmy { get; }
    public IReadOnlyList<ProvinceAdministrationReport> AdministrationByProvince { get; }
    public IReadOnlyList<ProvinceLoyaltyReport> LoyaltyByProvince { get; }
    public IReadOnlyList<ProvinceRebellionReport> RebellionByProvince { get; }
    public FactionStabilityReport Stability { get; }

    private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> source) =>
        new ReadOnlyCollection<T>(source.ToArray());

    private static int SumChecked(IEnumerable<int> values) =>
        values.Aggregate(0, (total, value) => checked(total + value));
}

public sealed class TurnReport
{
    internal TurnReport(
        int completedTurn,
        int nextTurn,
        IReadOnlyList<FactionTurnReport> factions)
    {
        CompletedTurn = completedTurn;
        NextTurn = nextTurn;
        Factions = new ReadOnlyCollection<FactionTurnReport>(factions.ToArray());
    }

    public int CompletedTurn { get; }
    public int NextTurn { get; }
    public IReadOnlyList<FactionTurnReport> Factions { get; }
}
