using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Application.Abstractions;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task<Customer?> GetByIdAsync(TenantId tenantId, CustomerId customerId, CancellationToken cancellationToken);

    Task<(List<Customer> Items, int Total)> SearchAsync(
        TenantId tenantId,
        string? query,
        IReadOnlyCollection<Guid>? tagIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Customer?> FindByIdentityAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        CancellationToken cancellationToken);

    Task<bool> IdentityExistsAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        CancellationToken cancellationToken);

    Task<bool> ContactExistsAsync(
        TenantId tenantId,
        ContactType type,
        string value,
        CancellationToken cancellationToken);
    Task<List<Customer>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<CustomerId> customerIds,
        CancellationToken cancellationToken);

}
