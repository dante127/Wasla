using Wasla.BuildingBlocks.Application;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Creates a customer profile manually (agent-entered).</summary>
public sealed class CreateCustomerHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CustomerDetail>> HandleAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var now = clock.UtcNow;

        Customer customer;

        try
        {
            customer = Customer.Create(tenantId, request.DisplayName, now);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<CustomerDetail>(new Error("customers.invalid", exception.Message));
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLowerInvariant();

            if (await customers.ContactExistsAsync(tenantId, ContactType.Email, email, cancellationToken))
            {
                return Result.Failure<CustomerDetail>(
                    new Error("customers.contact_conflict", "Another customer already uses this email."));
            }

            customer.AddContact(ContactType.Email, email, now);
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = request.Phone.Trim();

            if (await customers.ContactExistsAsync(tenantId, ContactType.Phone, phone, cancellationToken))
            {
                return Result.Failure<CustomerDetail>(
                    new Error("customers.contact_conflict", "Another customer already uses this phone number."));
            }

            customer.AddContact(ContactType.Phone, phone, now);
        }

        await customers.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GetCustomerHandler.BuildDetail(customer, new Dictionary<Guid, Conversations.Application.Contracts.TagInfo>()));
    }
}
