namespace Interregnum.Core.Domain;

public sealed record Settlement(string Name);

public sealed record Province(
    ProvinceId Id,
    string Name,
    FactionId OwnerId,
    int Population,
    int AgriculturalProduction,
    int TaxRevenue,
    int Loyalty,
    int AdministrativeBurden,
    TaxRate TaxRate,
    Settlement? Settlement,
    IReadOnlySet<ProvinceId> Neighbors);
