using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Contracts;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Adds catalog tags to a customer.</summary>
public sealed class AddTagsHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ITagInfoProvider tags,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<CustomerTagItem>>> HandleAsync(
        Guid customerId,
        AddTagsRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<CustomerTagItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var tagIds = request.TagIds.Distinct().ToList();

        if (tagIds.Count == 0)
        {
            return Result.Failure<IReadOnlyList<CustomerTagItem>>(
                new Error("customers.invalid", "At least one tag id is required."));
        }

        if (!await tags.AllExistAsync(tenantId, tagIds, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CustomerTagItem>>(
                new Error("tags.not_found", "One or more tags were not found."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<IReadOnlyList<CustomerTagItem>>(
                new Error("customers.not_found", "Customer not found."));
        }

        foreach (var tagId in tagIds)
        {
            customer.AddTag(tagId, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var lookup = await tags.GetManyAsync(tenantId, tagIds, cancellationToken);

        return Result.Success<IReadOnlyList<CustomerTagItem>>(
            lookup.Values.Select(SearchCustomersHandler.ToTagItem).ToList());
    }
}

/// <summary>Removes a tag from a customer.</summary>
public sealed class RemoveTagHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ICustomersUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(Guid customerId, Guid tagId, CancellationToken cancellationToken)
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

        if (!customer.RemoveTag(tagId, clock.UtcNow))
        {
            return Result.Failure(new Error("customers.tag_not_found", "The customer does not have this tag."));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
