using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Infrastructure;

public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pg_trgm");
        ApplyTenantFilter<Customer>(modelBuilder);
        ApplyTenantFilter<CustomerIdentity>(modelBuilder);
        ApplyTenantFilter<CustomerContact>(modelBuilder);
    }
}
