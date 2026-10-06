using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Customers.Infrastructure;

public sealed class CustomersDesignTimeFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=***",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "customers"))
            .Options;

        return new CustomersDbContext(options, UnresolvedTenantContext.Instance);
    }
}
