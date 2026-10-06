using Wasla.BuildingBlocks.Application;
using Wasla.Tenancy.Application.Abstractions;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Tenancy.Application;

/// <summary>Returns the tenant of the current (authenticated) context.</summary>
public sealed class GetCurrentTenantHandler(
    ITenantContext tenantContext,
    ITenantRepository tenants)
{
    public async Task<Result<TenantSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantSummary>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var tenant = await tenants.GetByIdAsync(tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantSummary>(
                new Error("tenancy.not_found", "Tenant not found."));
        }

        return Result.Success(new TenantSummary(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug.Value,
            tenant.Status.ToString(),
            tenant.DefaultCulture,
            tenant.TimeZone));
    }
}
