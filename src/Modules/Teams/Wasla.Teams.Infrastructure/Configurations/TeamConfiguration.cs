using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Infrastructure.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams", "teams");
        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).HasConversion(id => id.Value, value => new TeamId(value));
        builder.Property(team => team.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(team => team.Name).IsRequired().HasMaxLength(150);
        builder.Property(team => team.Description).HasMaxLength(500);
        builder.Property(team => team.CreatedAt).IsRequired();

        builder.HasIndex(team => new { team.TenantId, team.Name }).IsUnique();

        builder.HasMany(team => team.Members)
            .WithOne()
            .HasForeignKey(member => member.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers", "teams");
        builder.HasKey(member => new { member.TeamId, member.UserId });

        builder.Property(member => member.TeamId).HasConversion(id => id.Value, value => new TeamId(value));
        builder.Property(member => member.UserId).HasConversion(id => id.Value, value => new UserId(value));
        builder.Property(member => member.Role).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(member => member.JoinedAt).IsRequired();
    }
}
