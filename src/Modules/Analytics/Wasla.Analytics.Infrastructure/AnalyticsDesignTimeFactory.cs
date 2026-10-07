using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Analytics.Infrastructure;

/// <summary>Design-time factory so `dotnet ef` can build the Analytics context without the host.</summary>
public sealed class AnalyticsDesignTimeFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=***",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "analytics"))
            .Options;

        return new AnalyticsDbContext(options, UnresolvedTenantContext.Instance);
    }
}