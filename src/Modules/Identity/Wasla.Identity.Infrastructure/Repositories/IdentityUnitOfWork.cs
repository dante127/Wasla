using Wasla.Identity.Application.Abstractions;

namespace Wasla.Identity.Infrastructure.Repositories;

internal sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
