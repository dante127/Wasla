using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Tenancy.Domain;

namespace Wasla.Tenancy.Infrastructure;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilter<Tenant>(modelBuilder);
    }
}
