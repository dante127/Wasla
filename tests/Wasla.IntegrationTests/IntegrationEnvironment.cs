using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
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
using Wasla.Identity.Application.Roles;
using Wasla.Identity.Domain;
using Wasla.Identity.Infrastructure;
using Wasla.Teams.Infrastructure;
using Wasla.Tenancy.Domain;
using Wasla.Tenancy.Infrastructure;

namespace Wasla.IntegrationTests;

public sealed record AuthResult(string AccessToken, string RefreshToken, IReadOnlyList<string> Permissions);

/// <summary>
/// Shared integration environment: one PostgreSQL test container (Testcontainers), the API
/// host with test configuration, applied migrations and two seeded tenants (alpha/beta).
/// </summary>
public sealed class IntegrationEnvironment : IAsyncLifetime
{
    public const string Password = "Test@12345";
    public const string OwnerAEmail = "owner-a@test.dev";
    public const string AgentAEmail = "agent-a@test.dev";
    public const string OwnerBEmail = "owner-b@test.dev";
    public const string BetaOnlyRoleName = "BetaOnly";

    public const string AlphaCustomerName = "Alice Johnson";

    public const string AlphaCustomerPhone = "+963111111111";

    public const string BetaCustomerName = "Zed Beta";

    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;

    public Guid AlphaTenantId { get; private set; }

    public Guid BetaTenantId { get; private set; }

    public Guid BetaOwnerUserId { get; private set; }

    public Guid AlphaCustomerId { get; private set; }

    public Guid BetaCustomerId { get; private set; }

    public Guid AlphaVipTagId { get; private set; }

    public HttpClient CreateClient() => GetFactory().CreateClient();

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("wasla_tests")
            .WithUsername("wasla")
            .WithPassword("wasla_test_password")
            .Build();

        await _postgres.StartAsync();

        _factory = new ApiFactory(_postgres.GetConnectionString());
        _ = _factory.CreateClient(); // starts the host

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<TenancyDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<TeamsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
await services.GetRequiredService<ConversationsDbContext>().Database.MigrateAsync();
await services.GetRequiredService<CustomersDbContext>().Database.MigrateAsync();

        await SeedAsync(services);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    public async Task<AuthResult> LoginAsync(HttpClient client, string email)
    {
        // A login must never carry a previous session's bearer token.
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Login failed for {email}: {(int)response.StatusCode} {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = payload.GetProperty("accessToken").GetString()!;
        var refreshToken = payload.GetProperty("refreshToken").GetString()!;
        var permissions = payload.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!).ToList();

        return new AuthResult(accessToken, refreshToken, permissions);
    }

    private WebApplicationFactory<Program> GetFactory() =>
        _factory ?? throw new InvalidOperationException("The environment has not been initialized.");

    private async Task SeedAsync(IServiceProvider services)
    {
        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var tenancyDb = services.GetRequiredService<TenancyDbContext>();
        var identityDb = services.GetRequiredService<IdentityDbContext>();

        var alpha = Tenant.Create("Alpha Ltd", TenantSlug.Create("alpha"), "en", "UTC", now);
        var beta = Tenant.Create("Beta Ltd", TenantSlug.Create("beta"), "en", "UTC", now);

        tenancyDb.Tenants.Add(alpha);
        tenancyDb.Tenants.Add(beta);
        await tenancyDb.SaveChangesAsync();

        AlphaTenantId = alpha.Id.Value;
        BetaTenantId = beta.Id.Value;

        var roleSeeder = services.GetRequiredService<DefaultRoleSeeder>();
        await roleSeeder.SeedAsync(alpha.Id, CancellationToken.None);
        await roleSeeder.SeedAsync(beta.Id, CancellationToken.None);

        var roleRepository = services.GetRequiredService<IRoleRepository>();
        var userRepository = services.GetRequiredService<IUserRepository>();
        var membershipRepository = services.GetRequiredService<IMembershipRepository>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();

        var alphaOwnerRole = await roleRepository.GetByNameAsync(alpha.Id, SystemRoles.Owner, CancellationToken.None);
        var alphaAgentRole = await roleRepository.GetByNameAsync(alpha.Id, SystemRoles.Agent, CancellationToken.None);
        var betaOwnerRole = await roleRepository.GetByNameAsync(beta.Id, SystemRoles.Owner, CancellationToken.None);

        var betaOnlyRole = Role.Create(beta.Id, BetaOnlyRoleName, [PermissionCatalog.Campaigns.Read], isSystem: false);
        identityDb.Roles.Add(betaOnlyRole);

        var alphaOwner = User.Register(EmailAddress.Create(OwnerAEmail), "Alpha Owner", passwordHasher.Hash(Password), now);
        var alphaAgent = User.Register(EmailAddress.Create(AgentAEmail), "Alpha Agent", passwordHasher.Hash(Password), now);
        var betaOwner = User.Register(EmailAddress.Create(OwnerBEmail), "Beta Owner", passwordHasher.Hash(Password), now);

        await userRepository.AddAsync(alphaOwner, CancellationToken.None);
        await userRepository.AddAsync(alphaAgent, CancellationToken.None);
        await userRepository.AddAsync(betaOwner, CancellationToken.None);

        BetaOwnerUserId = betaOwner.Id.Value;

        var alphaOwnerMembership = Membership.Create(alpha.Id, alphaOwner.Id, now);
        if (alphaOwnerRole is not null)
        {
            alphaOwnerMembership.ReplaceRoles([alphaOwnerRole.Id], now);
        }

        var alphaAgentMembership = Membership.Create(alpha.Id, alphaAgent.Id, now);
        if (alphaAgentRole is not null)
        {
            alphaAgentMembership.ReplaceRoles([alphaAgentRole.Id], now);
        }

        var betaOwnerMembership = Membership.Create(beta.Id, betaOwner.Id, now);
        if (betaOwnerRole is not null)
        {
            betaOwnerMembership.ReplaceRoles([betaOwnerRole.Id], now);
        }

        await membershipRepository.AddAsync(alphaOwnerMembership, CancellationToken.None);
        await membershipRepository.AddAsync(alphaAgentMembership, CancellationToken.None);
        await membershipRepository.AddAsync(betaOwnerMembership, CancellationToken.None);

        await identityDb.SaveChangesAsync();

        var conversationsDb = services.GetRequiredService<ConversationsDbContext>();
        var customersDb = services.GetRequiredService<CustomersDbContext>();
        var tagRepository = services.GetRequiredService<ITagRepository>();
        var customerRepository = services.GetRequiredService<ICustomerRepository>();

        var vipTag = Tag.Create(alpha.Id, "vip", "VIP", "#7c3aed", now);
        var leadTag = Tag.Create(alpha.Id, "new-lead", "New Lead", null, now);
        var betaTag = Tag.Create(beta.Id, "beta-only", "Beta Only", null, now);

        await tagRepository.AddAsync(vipTag, CancellationToken.None);
        await tagRepository.AddAsync(leadTag, CancellationToken.None);
        await tagRepository.AddAsync(betaTag, CancellationToken.None);
        await conversationsDb.SaveChangesAsync();

        AlphaVipTagId = vipTag.Id.Value;

        var alice = Customer.Create(alpha.Id, AlphaCustomerName, now);
        alice.AddIdentity(ChannelType.WhatsApp, AlphaCustomerPhone, AlphaCustomerPhone, now);
        alice.AddContact(ContactType.Email, "alice@test.dev", now);
        alice.AddTag(vipTag.Id.Value, now);
        alice.AddNote(null, "VIP customer.", now);

        var bob = Customer.Create(alpha.Id, "Bob Smith", now);
        bob.AddIdentity(ChannelType.Telegram, "@bob_smith", "@bob_smith", now);

        var zed = Customer.Create(beta.Id, BetaCustomerName, now);
        zed.AddIdentity(ChannelType.WhatsApp, "+963999999999", "+963999999999", now);

        await customerRepository.AddAsync(alice, CancellationToken.None);
        await customerRepository.AddAsync(bob, CancellationToken.None);
        await customerRepository.AddAsync(zed, CancellationToken.None);
        await customersDb.SaveChangesAsync();

        AlphaCustomerId = alice.Id.Value;
        BetaCustomerId = zed.Id.Value;
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Redis:ConnectionString"] = "localhost:6379",
                    ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
                    ["Telemetry:OtlpEndpoint"] = null,
                });
            });
        }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationEnvironment>
{
}
