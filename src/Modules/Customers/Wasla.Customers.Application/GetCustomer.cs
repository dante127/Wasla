using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Contracts;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application;

/// <summary>Returns the Customer 360 detail view.</summary>
public sealed class GetCustomerHandler(
    ITenantContext tenantContext,
    ICustomerRepository customers,
    ITagInfoProvider tags)
{
    public async Task<Result<CustomerDetail>> HandleAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<CustomerDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDetail>(new Error("customers.not_found", "Customer not found."));
        }

        var tagIds = customer.Tags.Select(tag => tag.TagId).Distinct().ToList();
        var tagLookup = await tags.GetManyAsync(tenantId, tagIds, cancellationToken);

        return Result.Success(BuildDetail(customer, tagLookup));
    }

    internal static CustomerDetail BuildDetail(
        Customer customer,
        IReadOnlyDictionary<Guid, TagInfo> tagLookup) =>
        new(
            customer.Id.Value,
            customer.DisplayName,
            customer.Status.ToString(),
            customer.CreatedAt,
            customer.UpdatedAt,
            customer.Identities
                .OrderBy(identity => identity.ChannelType)
                .Select(identity => new CustomerIdentityItem(
                    identity.Id.Value,
                    identity.ChannelType.ToString(),
                    identity.ExternalId,
                    identity.DisplayValue,
                    identity.LastSeenAt))
                .ToList(),
            customer.Contacts
                .Select(contact => new CustomerContactItem(
                    contact.Id.Value,
                    contact.Type.ToString(),
                    contact.Value,
                    contact.IsVerified))
                .ToList(),
            customer.Notes
                .OrderByDescending(note => note.CreatedAt)
                .Select(note => new CustomerNoteItem(
                    note.Id.Value,
                    note.AuthorUserId?.Value,
                    note.Body,
                    note.CreatedAt))
                .ToList(),
            customer.Tags
                .Where(tag => tagLookup.ContainsKey(tag.TagId))
                .Select(tag => SearchCustomersHandler.ToTagItem(tagLookup[tag.TagId]))
                .ToList());
}
