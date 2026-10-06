using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Tenancy.Infrastructure;

public sealed class TenantInfoProvider(TenancyDbContext dbContext) : ITenantInfoProvider
{
    public async Task<TenantSummary?> GetSummaryAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);

        return tenant is null
            ? null
            : new TenantSummary(
                tenant.Id.Value,
                tenant.Name,
                tenant.Slug.Value,
                tenant.Status.ToString(),
                tenant.DefaultCulture,
                tenant.TimeZone);
    }
}