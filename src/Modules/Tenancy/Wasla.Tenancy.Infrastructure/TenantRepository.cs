using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Tenancy.Application.Abstractions;
using Wasla.Tenancy.Domain;

namespace Wasla.Tenancy.Infrastructure;

public sealed class TenantRepository(TenancyDbContext dbContext) : ITenantRepository
{
    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken) =>
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);

    public Task<Tenant?> GetByIdAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        return dbContext.Tenants.FirstOrDefaultAsync(
            tenant => tenant.Slug == TenantSlug.FromTrusted(normalized),
            cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        return dbContext.Tenants.AnyAsync(
            tenant => tenant.Slug == TenantSlug.FromTrusted(normalized),
            cancellationToken);
    }
}