using Microsoft.EntityFrameworkCore;
using Wasla.Analytics.Application.Abstractions;
using Wasla.Analytics.Domain;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Analytics.Infrastructure;

public sealed class AnalyticsRepository(AnalyticsDbContext dbContext) : IAnalyticsRepository
{
    public async Task<List<FactConversation>> ListFactsAsync(
        TenantId tenantId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.Facts.Where(fact => fact.TenantId == tenantId);

        if (!tracking)
        {
            queryable = queryable.AsNoTracking();
        }

        return await queryable.ToListAsync(cancellationToken);
    }

    public async Task<List<DailyRollup>> ListRollupsAsync(
        TenantId tenantId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.Rollups.Where(rollup => rollup.TenantId == tenantId);

        if (!tracking)
        {
            queryable = queryable.AsNoTracking();
        }

        return await queryable.OrderBy(rollup => rollup.Date).ToListAsync(cancellationToken);
    }

    public async Task AddFactAsync(FactConversation fact, CancellationToken cancellationToken) =>
        await dbContext.Facts.AddAsync(fact, cancellationToken);

    public async Task AddRollupAsync(DailyRollup rollup, CancellationToken cancellationToken) =>
        await dbContext.Rollups.AddAsync(rollup, cancellationToken);

    public void RemoveRollups(IEnumerable<DailyRollup> rollups) =>
        dbContext.Rollups.RemoveRange(rollups);
}
