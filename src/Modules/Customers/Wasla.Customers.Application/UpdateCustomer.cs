using Wasla.BuildingBlocks.Application;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Updates the customer's display name.</summary>
public sealed class UpdateCustomerHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(new Error("customers.not_found", "Customer not found."));
        }

        try
        {
            customer.Rename(request.DisplayName, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure(new Error("customers.invalid", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
