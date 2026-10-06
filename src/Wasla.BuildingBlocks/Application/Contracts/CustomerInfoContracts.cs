using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Application.Contracts;

/// <summary>Public, provider-neutral summary of a customer for cross-module use.</summary>
public sealed record CustomerSummary(
    Guid Id,
    string DisplayName,
    string? PrimaryPhone,
    string? PrimaryEmail);

/// <summary>Shared module contract: read customer data without touching Customers internals.</summary>
public interface ICustomerInfoProvider
{
    Task<CustomerSummary?> GetAsync(TenantId tenantId, Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CustomerSummary>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken);

    /// <summary>The customer's external identity on a given channel (recipient resolution for sends).</summary>
    Task<string?> GetChannelExternalIdAsync(
        TenantId tenantId,
        Guid customerId,
        ChannelType channelType,
        CancellationToken cancellationToken);
}