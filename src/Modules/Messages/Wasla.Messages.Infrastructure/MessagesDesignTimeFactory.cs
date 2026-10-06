using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Messages.Infrastructure;

public sealed class MessagesDesignTimeFactory : IDesignTimeDbContextFactory<MessagesDbContext>
{
    public MessagesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MessagesDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=***",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "messages"))
            .Options;

        return new MessagesDbContext(options, UnresolvedTenantContext.Instance);
    }
}
