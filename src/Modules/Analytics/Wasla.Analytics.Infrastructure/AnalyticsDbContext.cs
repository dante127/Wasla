using Microsoft.EntityFrameworkCore;
using Wasla.Analytics.Domain;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;

namespace Wasla.Analytics.Infrastructure;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<FactConversation> Facts => Set<FactConversation>();

    public DbSet<DailyRollup> Rollups => Set<DailyRollup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<FactConversation>(modelBuilder);
        ApplyTenantFilter<DailyRollup>(modelBuilder);
    }
}
