using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure;

public sealed class MessagesDbContext(DbContextOptions<MessagesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<Message>(modelBuilder);
        ApplyTenantFilter<MediaFile>(modelBuilder);
        ApplyTenantFilter<OutboxMessage>(modelBuilder);
    }
}
