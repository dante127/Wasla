using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Teams.Infrastructure;

public sealed class TeamsDesignTimeFactory : IDesignTimeDbContextFactory<TeamsDbContext>
{
    public TeamsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TeamsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=wasla_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "teams"))
            .Options;

        return new TeamsDbContext(options, UnresolvedTenantContext.Instance);
    }
}
