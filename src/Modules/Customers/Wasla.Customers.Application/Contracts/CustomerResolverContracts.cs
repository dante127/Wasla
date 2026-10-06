using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Application.Contracts;

/// <summary>
/// Module contract: resolve or create a customer from an inbound channel identity.
/// Used by the Channels module's inbound pipeline.
/// </summary>
public interface ICustomerResolver
{
    Task<Guid> ResolveOrCreateByChannelIdentityAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        string? displayNameHint,
        CancellationToken cancellationToken);
}
