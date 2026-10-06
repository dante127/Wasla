using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Channels.Infrastructure;

public sealed class ChannelsDesignTimeFactory : IDesignTimeDbContextFactory<ChannelsDbContext>
{
    public ChannelsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChannelsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=***",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "channels"))
            .Options;

        return new ChannelsDbContext(options, UnresolvedTenantContext.Instance);
    }
}
