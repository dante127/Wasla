using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Contracts;

namespace Wasla.Customers.Application.Services;

/// <summary>
/// Contract-facing wrapper around <see cref="CustomerIdentityResolver"/> for cross-module use.
/// Resolution stays exact-match (no auto-merge); callers get the stable customer id.
/// </summary>
public sealed class CustomerResolver(CustomerIdentityResolver resolver) : ICustomerResolver
{
    public async Task<Guid> ResolveOrCreateByChannelIdentityAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        string? displayNameHint,
        CancellationToken cancellationToken)
    {
        var (customer, _) = await resolver.ResolveOrCreateAsync(
            tenantId,
            channelType,
            externalId,
            displayValue: externalId,
            displayNameHint,
            cancellationToken);

        return customer.Id.Value;
    }
}
