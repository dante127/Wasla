namespace Wasla.Tenancy.Application.Contracts;

/// <summary>
/// Module contract: enumerate tenant ids for platform-wide background jobs
/// (e.g. the analytics recompute worker).
/// </summary>
public interface ITenantDirectory
{
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken);
}
