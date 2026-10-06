using Wasla.BuildingBlocks.Domain;
using Wasla.Tenancy.Domain;

namespace Wasla.Tenancy.Application.Abstractions;

public interface ITenantRepository
{
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<Tenant?> GetByIdAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
}
