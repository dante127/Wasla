using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Links a channel identity to a customer (exact-match only; no automatic merging).</summary>
public sealed class AddIdentityHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CustomerIdentityItem>> HandleAsync(
        Guid customerId,
        AddIdentityRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerIdentityItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (!Enum.TryParse<ChannelType>(request.ChannelType, ignoreCase: true, out var channelType))
        {
            return Result.Failure<CustomerIdentityItem>(
                new Error("customers.invalid_channel", $"Unknown channel type '{request.ChannelType}'."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerIdentityItem>(new Error("customers.not_found", "Customer not found."));
        }

        var externalId = request.ExternalId.Trim();

        if (await customers.IdentityExistsAsync(tenantId, channelType, externalId, cancellationToken))
        {
            return Result.Failure<CustomerIdentityItem>(
                new Error("customers.identity_conflict", "This identity is already linked to a customer."));
        }

        CustomerIdentity identity;

        try
        {
            identity = customer.AddIdentity(channelType, externalId, request.DisplayValue, clock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<CustomerIdentityItem>(new Error("customers.identity_conflict", exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<CustomerIdentityItem>(new Error("customers.invalid", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CustomerIdentityItem(
            identity.Id.Value,
            identity.ChannelType.ToString(),
            identity.ExternalId,
            identity.DisplayValue,
            identity.LastSeenAt));
    }
}
