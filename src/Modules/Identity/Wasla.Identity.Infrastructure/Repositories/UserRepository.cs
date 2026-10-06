using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Repositories;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(user, cancellationToken);

    public Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(EmailAddress email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public async Task<List<User>> GetByIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();

        return await dbContext.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<User>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var ids = await dbContext.Memberships
            .Where(membership => membership.TenantId == tenantId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        return await dbContext.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);
    }
}
