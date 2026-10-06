using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Abstractions;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Infrastructure;

/// <summary>Module contract implementation: batched customer summaries for other modules.</summary>
public sealed class CustomerInfoProvider(ICustomerRepository customers) : ICustomerInfoProvider
{
    public async Task<CustomerSummary?> GetAsync(
        TenantId tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(tenantId, new CustomerId(customerId), cancellationToken);

        return customer is null ? null : ToSummary(customer);
    }

    public async Task<IReadOnlyDictionary<Guid, CustomerSummary>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        var ids = customerIds.Distinct().Select(id => new CustomerId(id)).ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, CustomerSummary>();
        }

        var found = await customers.GetByIdsAsync(tenantId, ids, cancellationToken);

        return found.ToDictionary(customer => customer.Id.Value, ToSummary);
    }

    private static CustomerSummary ToSummary(Customer customer) =>
        new(
            customer.Id.Value,
            customer.DisplayName,
            customer.Contacts.FirstOrDefault(contact => contact.Type == ContactType.Phone)?.Value,
            customer.Contacts.FirstOrDefault(contact => contact.Type == ContactType.Email)?.Value);
}
