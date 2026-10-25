using System.Collections.Frozen;

namespace Interregnum.Core.Domain;

public sealed record Settlement(string Name);

public sealed record Province
{
    public Province(
        ProvinceId id,
        string name,
        FactionId ownerId,
        int population,
        int agriculturalProduction,
        int taxRevenue,
        int loyalty,
        int administrativeBurden,
        TaxRate taxRate,
        Settlement? settlement,
        IReadOnlySet<ProvinceId> neighbors)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        Id = id;
        Name = name;
        OwnerId = ownerId;
        Population = population;
        AgriculturalProduction = agriculturalProduction;
        TaxRevenue = taxRevenue;
        Loyalty = loyalty;
        AdministrativeBurden = administrativeBurden;
        TaxRate = taxRate;
        Settlement = settlement;
        Neighbors = neighbors.ToFrozenSet();
    }

    public ProvinceId Id { get; }
    public string Name { get; }
    public FactionId OwnerId { get; }
    public int Population { get; }
    public int AgriculturalProduction { get; }
    public int TaxRevenue { get; }
    public int Loyalty { get; }
    public int AdministrativeBurden { get; }
    public TaxRate TaxRate { get; }
    public Settlement? Settlement { get; }
    public IReadOnlySet<ProvinceId> Neighbors { get; }

    internal Province WithTaxRate(TaxRate taxRate) => new(
        Id,
        Name,
        OwnerId,
        Population,
        AgriculturalProduction,
        TaxRevenue,
        Loyalty,
        AdministrativeBurden,
        taxRate,
        Settlement,
        Neighbors);
}
