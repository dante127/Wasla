using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application.Services;

/// <summary>
/// Resolves inbound channel identities to customers. Resolution is exact-match on
/// (tenant, channel, external id). There is deliberately no automatic merging or safe-link
/// heuristic (docs/domain-model.md §7); a miss creates a new customer.
/// </summary>
public sealed class CustomerIdentityResolver(
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<Customer?> ResolveAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        CancellationToken cancellationToken) =>
        customers.FindByIdentityAsync(tenantId, channelType, externalId, cancellationToken);

    public async Task<(Customer Customer, bool Created)> ResolveOrCreateAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        string? displayValue,
        string? displayNameHint,
        CancellationToken cancellationToken)
    {
        var existing = await ResolveAsync(tenantId, channelType, externalId, cancellationToken);

        if (existing is not null)
        {
            existing.TouchIdentity(channelType, externalId, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return (existing, false);
        }

        var name = !string.IsNullOrWhiteSpace(displayNameHint)
            ? displayNameHint
            : !string.IsNullOrWhiteSpace(displayValue)
                ? displayValue
                : externalId;

        var customer = Customer.Create(tenantId, name, clock.UtcNow);
        customer.AddIdentity(channelType, externalId, displayValue, clock.UtcNow);

        await customers.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (customer, true);
    }
}
