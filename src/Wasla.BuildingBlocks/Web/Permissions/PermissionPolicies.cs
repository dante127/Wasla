using Wasla.BuildingBlocks.Web.Permissions;

namespace Wasla.BuildingBlocks.Web.Permissions;

/// <summary>Helper for building permission policy names used with RequireAuthorization.</summary>
public static class PermissionPolicies
{
    public static string For(string permission) => $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
}
