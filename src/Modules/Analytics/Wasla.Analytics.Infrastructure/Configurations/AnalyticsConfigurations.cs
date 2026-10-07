using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Analytics.Domain;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Analytics.Infrastructure.Configurations;

public sealed class FactConversationConfiguration : IEntityTypeConfiguration<FactConversation>
{
    public void Configure(EntityTypeBuilder<FactConversation> builder)
    {
        builder.ToTable("FactConversations", "analytics");

        builder.HasKey(fact => new { fact.TenantId, fact.ConversationId });
        builder.Property(fact => fact.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(fact => fact.Status).HasMaxLength(20).IsRequired();
        builder.Property(fact => fact.OpenedAt).IsRequired();
        builder.Property(fact => fact.UpdatedAt).IsRequired();

        builder.HasIndex(fact => new { fact.TenantId, fact.ChannelId });
        builder.HasIndex(fact => new { fact.TenantId, fact.AssignedUserId });
    }
}

public sealed class DailyRollupConfiguration : IEntityTypeConfiguration<DailyRollup>
{
    public void Configure(EntityTypeBuilder<DailyRollup> builder)
    {
        builder.ToTable("DailyRollups", "analytics");

        builder.HasKey(rollup => new { rollup.TenantId, rollup.Date });
        builder.Property(rollup => rollup.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(rollup => rollup.Date).IsRequired();
    }
}
