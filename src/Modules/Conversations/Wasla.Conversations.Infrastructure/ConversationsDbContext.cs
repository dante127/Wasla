using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

public sealed class ConversationsDbContext(DbContextOptions<ConversationsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<Tag>(modelBuilder);
        ApplyTenantFilter<Conversation>(modelBuilder);
        ApplyTenantFilter<QuickReply>(modelBuilder);
    }
}
