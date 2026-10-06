namespace Wasla.BuildingBlocks.Domain;

/// <summary>Strongly-typed identifier for a user (global identity, shared across tenants).</summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
