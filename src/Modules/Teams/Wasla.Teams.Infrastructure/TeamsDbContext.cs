using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Infrastructure;

public sealed class TeamsDbContext(DbContextOptions<TeamsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<Team>(modelBuilder);
    }
}
