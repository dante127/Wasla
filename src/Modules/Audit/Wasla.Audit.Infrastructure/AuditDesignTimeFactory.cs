using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Audit.Infrastructure;

public sealed class AuditDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=wasla_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit"))
            .Options;

        return new AuditDbContext(options, UnresolvedTenantContext.Instance);
    }
}
