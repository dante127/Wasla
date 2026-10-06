using Microsoft.AspNetCore.Authorization;

namespace Wasla.BuildingBlocks.Web.Permissions;

/// <summary>Requires a permission code from the closed Wasla permission catalog.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
