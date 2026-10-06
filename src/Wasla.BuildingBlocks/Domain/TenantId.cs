namespace Wasla.BuildingBlocks.Domain;

/// <summary>Strongly-typed identifier for a tenant.</summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
