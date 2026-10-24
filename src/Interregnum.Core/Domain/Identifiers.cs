namespace Interregnum.Core.Domain;

public readonly record struct FactionId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ProvinceId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ArmyId(string Value)
{
    public override string ToString() => Value;
}
