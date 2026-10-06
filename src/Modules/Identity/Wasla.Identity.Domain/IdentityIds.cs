using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>Strongly-typed identifiers owned by the Identity module.</summary>
public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct RoleId(Guid Value)
{
    public static RoleId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct RefreshTokenId(Guid Value)
{
    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
