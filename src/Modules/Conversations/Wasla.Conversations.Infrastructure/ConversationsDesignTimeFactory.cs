using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.Conversations.Infrastructure;

public sealed class ConversationsDesignTimeFactory : IDesignTimeDbContextFactory<ConversationsDbContext>
{
    public ConversationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConversationsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=wasla;Username=wasla;Password=***",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "conversations"))
            .Options;

        return new ConversationsDbContext(options, UnresolvedTenantContext.Instance);
    }
}
