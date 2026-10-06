using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships", "identity");

        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id)
            .HasConversion(id => id.Value, value => new MembershipId(value));
        builder.Property(membership => membership.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(membership => membership.UserId)
            .HasConversion(id => id.Value, value => new UserId(value));
        builder.Property(membership => membership.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(membership => membership.JoinedAt).IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.UserId }).IsUnique();

        builder.HasMany(membership => membership.Roles)
            .WithOne()
            .HasForeignKey(role => role.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MembershipRoleConfiguration : IEntityTypeConfiguration<MembershipRole>
{
    public void Configure(EntityTypeBuilder<MembershipRole> builder)
    {
        builder.ToTable("MembershipRoles", "identity");

        builder.HasKey(role => new { role.MembershipId, role.RoleId });
        builder.Property(role => role.MembershipId)
            .HasConversion(id => id.Value, value => new MembershipId(value));
        builder.Property(role => role.RoleId)
            .HasConversion(id => id.Value, value => new RoleId(value));
    }
}
