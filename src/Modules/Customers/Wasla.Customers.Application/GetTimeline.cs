using Wasla.BuildingBlocks.Application;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Paged Customer 360 timeline (activity feed), newest first.</summary>
public sealed class GetTimelineHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers)
{
    public async Task<Result<CustomerTimelineResponse>> HandleAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerTimelineResponse>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerTimelineResponse>(
                new Error("customers.not_found", "Customer not found."));
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize is < 1 or > 100 ? 25 : pageSize;

        var activities = customer.Activities
            .OrderByDescending(activity => activity.OccurredAt)
            .ToList();

        var items = activities
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(activity => new CustomerTimelineItem(
                activity.Id.Value,
                activity.Type,
                activity.Data,
                activity.ActorUserId?.Value,
                activity.OccurredAt))
            .ToList();

        return Result.Success(new CustomerTimelineResponse(items, normalizedPage, normalizedPageSize, activities.Count));
    }
}
