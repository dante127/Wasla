using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

/// <summary>Resolves the effective permission set for a set of roles.</summary>
public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        TenantId tenantId,
        IReadOnlyCollection<RoleId> roleIds,
        CancellationToken cancellationToken);
}
