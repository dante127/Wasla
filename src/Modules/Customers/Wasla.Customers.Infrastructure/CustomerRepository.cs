using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Infrastructure;

public sealed class CustomerRepository(CustomersDbContext dbContext) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken) =>
        await dbContext.Customers.AddAsync(customer, cancellationToken);

    public Task<Customer?> GetByIdAsync(TenantId tenantId, CustomerId customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .Include(customer => customer.Identities)
            .Include(customer => customer.Contacts)
            .Include(customer => customer.Notes)
            .Include(customer => customer.Tags)
            .Include(customer => customer.Activities)
            .FirstOrDefaultAsync(customer => customer.TenantId == tenantId && customer.Id == customerId, cancellationToken);

    public async Task<(List<Customer> Items, int Total)> SearchAsync(
        TenantId tenantId,
        string? query,
        IReadOnlyCollection<Guid>? tagIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.Identities)
            .Include(customer => customer.Tags)
            .Where(customer => customer.TenantId == tenantId);

        var term = query?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            var escaped = term
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";

            queryable = queryable.Where(customer =>
                EF.Functions.ILike(customer.DisplayName, pattern, "\\")
                || customer.Identities.Any(identity =>
                    EF.Functions.ILike(identity.DisplayValue, pattern, "\\")
                    || EF.Functions.ILike(identity.ExternalId, pattern, "\\"))
                || customer.Contacts.Any(contact => EF.Functions.ILike(contact.Value, pattern, "\\")));
        }

        if (tagIds is { Count: > 0 })
        {
            var ids = tagIds.Distinct().ToList();

            queryable = queryable.Where(customer => customer.Tags.Any(tag => ids.Contains(tag.TagId)));
        }

        var total = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderBy(customer => customer.DisplayName)
            .ThenBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Customer?> FindByIdentityAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        CancellationToken cancellationToken)
    {
        var normalized = externalId.Trim();

        return dbContext.Customers
            .Include(customer => customer.Identities)
            .FirstOrDefaultAsync(
                customer => customer.TenantId == tenantId
                    && customer.Identities.Any(identity =>
                        identity.ChannelType == channelType && identity.ExternalId == normalized),
                cancellationToken);
    }

    public Task<bool> IdentityExistsAsync(
        TenantId tenantId,
        ChannelType channelType,
        string externalId,
        CancellationToken cancellationToken)
    {
        var normalized = externalId.Trim();

        return dbContext.Customers.AnyAsync(
            customer => customer.TenantId == tenantId
                && customer.Identities.Any(identity =>
                    identity.ChannelType == channelType && identity.ExternalId == normalized),
            cancellationToken);
    }

    public Task<bool> ContactExistsAsync(
        TenantId tenantId,
        ContactType type,
        string value,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(
            customer => customer.TenantId == tenantId
                && customer.Contacts.Any(contact => contact.Type == type && contact.Value == value),
            cancellationToken);
}
