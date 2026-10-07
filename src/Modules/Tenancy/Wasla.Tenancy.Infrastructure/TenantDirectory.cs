using Microsoft.EntityFrameworkCore;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Tenancy.Infrastructure;

/// <summary>Module contract implementation: tenant id enumeration for platform jobs.</summary>
public sealed class TenantDirectory(TenancyDbContext dbContext) : ITenantDirectory
{
    public async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants
            .AsNoTracking()
            .Select(tenant => tenant.Id.Value)
            .ToListAsync(cancellationToken);
}
