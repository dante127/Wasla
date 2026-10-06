using Wasla.BuildingBlocks.Domain;

namespace Wasla.Tenancy.Application.Contracts;

/// <summary>Public, provider-neutral summary of a tenant for other modules.</summary>
public sealed record TenantSummary(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string DefaultCulture,
    string TimeZone);

/// <summary>Module contract: read tenant information without touching Tenancy internals.</summary>
public interface ITenantInfoProvider
{
    Task<TenantSummary?> GetSummaryAsync(TenantId tenantId, CancellationToken cancellationToken);
}
