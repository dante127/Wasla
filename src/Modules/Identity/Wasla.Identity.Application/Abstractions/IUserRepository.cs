using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(EmailAddress email, CancellationToken cancellationToken);

    Task<List<User>> GetByIdsAsync(IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken);

    Task<List<User>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
}
