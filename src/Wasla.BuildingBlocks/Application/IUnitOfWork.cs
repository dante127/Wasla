namespace Wasla.BuildingBlocks.Application;

/// <summary>Commits all changes tracked by a module's persistence layer.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
