using Microsoft.EntityFrameworkCore;
using Wasla.Audit.Domain;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Persistence;

namespace Wasla.Audit.Infrastructure;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
}
