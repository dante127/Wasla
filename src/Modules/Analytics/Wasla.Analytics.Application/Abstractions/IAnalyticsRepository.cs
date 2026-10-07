using Wasla.BuildingBlocks.Domain;
using Wasla.Analytics.Domain;

namespace Wasla.Analytics.Application.Abstractions;

public interface IAnalyticsRepository
{
    Task<List<FactConversation>> ListFactsAsync(TenantId tenantId, bool tracking, CancellationToken cancellationToken);

    Task<List<DailyRollup>> ListRollupsAsync(TenantId tenantId, bool tracking, CancellationToken cancellationToken);

    Task AddFactAsync(FactConversation fact, CancellationToken cancellationToken);

    Task AddRollupAsync(DailyRollup rollup, CancellationToken cancellationToken);

    void RemoveRollups(IEnumerable<DailyRollup> rollups);
}

public interface IAnalyticsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
