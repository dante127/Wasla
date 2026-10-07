using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Analytics.Application;

/// <summary>
/// Runs the analytics recompute for every tenant, each in its own tenant-resolved DI
/// scope (worker scope is tenant-unresolved; sources filter by explicit tenant id).
/// </summary>
public sealed class TenantAnalyticsRecomputer(
    IServiceScopeFactory scopeFactory,
    ITenantDirectory tenantDirectory)
{
    public async Task<int> RecomputeAllTenantsAsync(CancellationToken cancellationToken)
    {
        var tenantIds = await tenantDirectory.ListTenantIdsAsync(cancellationToken);
        var total = 0;

        foreach (var tenantId in tenantIds)
        {
            using var scope = scopeFactory.CreateScope();

            scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(new TenantId(tenantId));

            total += await scope.ServiceProvider.GetRequiredService<AnalyticsRecomputeService>()
                .RecomputeAsync(cancellationToken);
        }

        return total;
    }
}
