using Microsoft.EntityFrameworkCore;
using Wasla.Audit.Infrastructure;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;
using Wasla.Conversations.Infrastructure;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;
using Wasla.Customers.Infrastructure;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;
using Wasla.Identity.Infrastructure;
using Wasla.Teams.Application.Abstractions;
using Wasla.Teams.Domain;
using Wasla.Teams.Infrastructure;
using Wasla.Tenancy.Application.Abstractions;
using Wasla.Tenancy.Domain;
using Wasla.Tenancy.Infrastructure;

namespace Wasla.Api.Seeding;

/// <summary>Applies migrations and idempotently seeds development data (run with --seed).</summary>
public static class DatabaseSeeder
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await services.GetRequiredService<TenancyDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<TeamsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
await services.GetRequiredService<ConversationsDbContext>().Database.MigrateAsync();
await services.GetRequiredService<CustomersDbContext>().Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        var tenancyDb = services.GetRequiredService<TenancyDbContext>();
        var identityDb = services.GetRequiredService<IdentityDbContext>();
        var teamsDb = services.GetRequiredService<TeamsDbContext>();

        var tenantRepository = services.GetRequiredService<ITenantRepository>();

        if (await tenantRepository.GetBySlugAsync("demo", CancellationToken.None) is not null)
        {
            logger.LogInformation("Demo tenant already exists; seeding skipped.");
            return;
        }

        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var tenant = Tenant.Create("Demo Company", TenantSlug.Create("demo"), "en", "Asia/Damascus", now);
        await tenantRepository.AddAsync(tenant, CancellationToken.None);
        await tenancyDb.SaveChangesAsync();

        var roleEntities = SystemRoles.Definitions
            .Select(definition => Role.Create(tenant.Id, definition.Key, definition.Value, isSystem: true))
            .ToList();

        var roleRepository = services.GetRequiredService<IRoleRepository>();
        await roleRepository.AddRangeAsync(roleEntities, CancellationToken.None);
        await identityDb.SaveChangesAsync();

        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var password = app.Configuration["Seed:Password"] ?? "Dev@Wasla123";

        var ownerRole = roleEntities.Single(role => role.Name == SystemRoles.Owner);
        var managerRole = roleEntities.Single(role => role.Name == SystemRoles.Manager);
        var agentRole = roleEntities.Single(role => role.Name == SystemRoles.Agent);

        var userRepository = services.GetRequiredService<IUserRepository>();
        var membershipRepository = services.GetRequiredService<IMembershipRepository>();

        var owner = User.Register(EmailAddress.Create("owner@wasla.dev"), "Owner", passwordHasher.Hash(password), now);
        var manager = User.Register(EmailAddress.Create("manager@wasla.dev"), "Manager", passwordHasher.Hash(password), now);
        var agent = User.Register(EmailAddress.Create("agent@wasla.dev"), "Agent", passwordHasher.Hash(password), now);

        await userRepository.AddAsync(owner, CancellationToken.None);
        await userRepository.AddAsync(manager, CancellationToken.None);
        await userRepository.AddAsync(agent, CancellationToken.None);

        var ownerMembership = Membership.Create(tenant.Id, owner.Id, now);
        ownerMembership.ReplaceRoles([ownerRole.Id], now);

        var managerMembership = Membership.Create(tenant.Id, manager.Id, now);
        managerMembership.ReplaceRoles([managerRole.Id], now);

        var agentMembership = Membership.Create(tenant.Id, agent.Id, now);
        agentMembership.ReplaceRoles([agentRole.Id], now);

        await membershipRepository.AddAsync(ownerMembership, CancellationToken.None);
        await membershipRepository.AddAsync(managerMembership, CancellationToken.None);
        await membershipRepository.AddAsync(agentMembership, CancellationToken.None);
        await identityDb.SaveChangesAsync();

        var teamRepository = services.GetRequiredService<ITeamRepository>();
        var supportTeam = Team.Create(tenant.Id, "Support", "Customer support team", now);
        supportTeam.ReplaceMembers([(agent.Id, TeamRole.Lead)], now);

        await teamRepository.AddAsync(supportTeam, CancellationToken.None);
        await teamsDb.SaveChangesAsync();

        var conversationsDb = services.GetRequiredService<ConversationsDbContext>();
        var customersDb = services.GetRequiredService<CustomersDbContext>();
        var tagRepository = services.GetRequiredService<ITagRepository>();
        var customerRepository = services.GetRequiredService<ICustomerRepository>();

        var vipTag = Tag.Create(tenant.Id, "vip", "VIP", "#7c3aed", now);
        var leadTag = Tag.Create(tenant.Id, "new-lead", "New Lead", null, now);
        var complaintTag = Tag.Create(tenant.Id, "complaint", "Complaint", "#dc2626", now);

        await tagRepository.AddAsync(vipTag, CancellationToken.None);
        await tagRepository.AddAsync(leadTag, CancellationToken.None);
        await tagRepository.AddAsync(complaintTag, CancellationToken.None);
        await conversationsDb.SaveChangesAsync();

        var layla = Customer.Create(tenant.Id, "Layla Haddad", now);
        layla.AddIdentity(ChannelType.WhatsApp, "+963991234567", "+963 991 234 567", now);
        layla.AddIdentity(ChannelType.Telegram, "@layla_h", "@layla_h", now);
        layla.AddContact(ContactType.Email, "layla@example.com", now);
        layla.AddTag(vipTag.Id.Value, now);

        var omar = Customer.Create(tenant.Id, "Omar Khaled", now);
        omar.AddIdentity(ChannelType.Telegram, "@omar_k", "@omar_k", now);
        omar.AddTag(leadTag.Id.Value, now);

        var sara = Customer.Create(tenant.Id, "Sara Nasser", now);
        sara.AddIdentity(ChannelType.Phone, "+963988765432", "+963 988 765 432", now);
        sara.AddTag(complaintTag.Id.Value, now);

        await customerRepository.AddAsync(layla, CancellationToken.None);
        await customerRepository.AddAsync(omar, CancellationToken.None);
        await customerRepository.AddAsync(sara, CancellationToken.None);
        await customersDb.SaveChangesAsync();

        logger.LogInformation(
            "Seeded tenant '{Slug}' with users owner@wasla.dev / manager@wasla.dev / agent@wasla.dev and team 'Support'.",
            tenant.Slug.Value);
    }
}
