namespace Wasla.BuildingBlocks.Application.Security;

/// <summary>Claim types used across Wasla access tokens (raw JWT claim names).</summary>
public static class WaslaClaimTypes
{
    public const string Subject = "sub";

    public const string Email = "email";

    public const string Name = "name";

    public const string TenantId = "tenant_id";

    public const string MembershipId = "membership_id";

    public const string Permission = "perm";
}
