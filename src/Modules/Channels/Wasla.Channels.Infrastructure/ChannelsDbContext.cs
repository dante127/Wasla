using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure;

public sealed class ChannelsDbContext(DbContextOptions<ChannelsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Channel> Channels => Set<Channel>();

    public DbSet<ChannelCredential> ChannelCredentials => Set<ChannelCredential>();

    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<Channel>(modelBuilder);
        ApplyTenantFilter<ChannelCredential>(modelBuilder);
        ApplyTenantFilter<InboxEvent>(modelBuilder);
    }
}
