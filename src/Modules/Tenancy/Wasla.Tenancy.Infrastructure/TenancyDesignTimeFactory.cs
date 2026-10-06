using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Tenancy.Infrastructure;

public sealed class TenancyDesignTimeFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=wasla_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options;

        return new TenancyDbContext(options, UnresolvedTenantContext.Instance);
    }
}
