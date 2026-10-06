using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Contracts;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Searches the tenant's customers (name / identity / contact match, optional tag filter).</summary>
public sealed class SearchCustomersHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ITagInfoProvider tags)
{
    public async Task<Result<CustomerSearchResponse>> HandleAsync(
        CustomerSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerSearchResponse>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 25 : query.PageSize;

        var (items, total) = await customers.SearchAsync(
            tenantId,
            query.Q,
            query.TagIds,
            page,
            pageSize,
            cancellationToken);

        var allTagIds = items.SelectMany(customer => customer.Tags.Select(tag => tag.TagId)).Distinct().ToList();
        var tagLookup = await tags.GetManyAsync(tenantId, allTagIds, cancellationToken);

        var list = items.Select(customer => new CustomerListItem(
                customer.Id.Value,
                customer.DisplayName,
                customer.Status.ToString(),
                customer.Tags
                    .Where(tag => tagLookup.ContainsKey(tag.TagId))
                    .Select(tag => ToTagItem(tagLookup[tag.TagId]))
                    .ToList(),
                customer.Identities.Count,
                customer.UpdatedAt))
            .ToList();

        return Result.Success(new CustomerSearchResponse(list, page, pageSize, total));
    }

    internal static CustomerTagItem ToTagItem(TagInfo info) => new(info.Id, info.Key, info.Name, info.Color);
}
