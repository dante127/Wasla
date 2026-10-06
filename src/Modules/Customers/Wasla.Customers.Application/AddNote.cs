using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Adds an agent note to a customer.</summary>
public sealed class AddNoteHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CustomerNoteItem>> HandleAsync(
        Guid customerId,
        AddNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerNoteItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerNoteItem>(new Error("customers.not_found", "Customer not found."));
        }

        var authorUserId = currentUser.UserId is { } userId ? new UserId(userId) : (UserId?)null;

        CustomerNote note;

        try
        {
            note = customer.AddNote(authorUserId, request.Body, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<CustomerNoteItem>(new Error("customers.invalid", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CustomerNoteItem(
            note.Id.Value,
            note.AuthorUserId?.Value,
            note.Body,
            note.CreatedAt));
    }
}
