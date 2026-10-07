using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Wasla.Audit.Infrastructure;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;
using Wasla.Conversations.Infrastructure;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Domain;
using Wasla.Customers.Infrastructure;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;
using Wasla.Channels.Application.Services;
using Wasla.Channels.Infrastructure;
using Wasla.Channels.Infrastructure.Adapters.Telegram;
using Wasla.Channels.Infrastructure.Adapters.WhatsApp;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;
using Wasla.Messages.Application;
using Wasla.Messages.Infrastructure;
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

    public const string WhatsAppAppSecret = "test-app-secret";

    public const string WhatsAppVerifyToken = "test-verify-token";

    public const string AlphaWhatsAppExternalId = "963111111111";

    public const string TelegramWebhookSecret = "test-telegram-secret";

    private PostgreSqlContainer? _postgres;

    private RedisContainer? _redis;
    private WebApplicationFactory<Program>? _factory;

    public Guid AlphaTenantId { get; private set; }

    public Guid BetaTenantId { get; private set; }

    public Guid BetaOwnerUserId { get; private set; }

    public Guid AlphaCustomerId { get; private set; }

    public Guid BetaCustomerId { get; private set; }

    public Guid AlphaVipTagId { get; private set; }

    public Guid AlphaConversationId { get; private set; }

    public Guid BetaConversationId { get; private set; }

    public Guid AlphaWhatsAppChannelId { get; private set; }

    public Guid BetaWhatsAppChannelId { get; private set; }

    public Guid AlphaTelegramChannelId { get; private set; }

    public HttpClient CreateClient() => GetFactory().CreateClient();

    public IServiceProvider Services => GetFactory().Services;

    public HttpMessageHandler CreateServerHandler() => GetFactory().Server.CreateHandler();

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("wasla_tests")
            .WithUsername("wasla")
            .WithPassword("wasla_test_password")
            .Build();

        await _postgres.StartAsync();

        _redis = new RedisBuilder("redis:7-alpine").Build();
        await _redis.StartAsync();

        _factory = new ApiFactory(_postgres.GetConnectionString(), _redis.GetConnectionString());
        _ = _factory.CreateClient(); // starts the host

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<TenancyDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<TeamsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
await services.GetRequiredService<ConversationsDbContext>().Database.MigrateAsync();
await services.GetRequiredService<CustomersDbContext>().Database.MigrateAsync();
await services.GetRequiredService<ChannelsDbContext>().Database.MigrateAsync();
await services.GetRequiredService<ConversationsDbContext>().Database.MigrateAsync();
await services.GetRequiredService<MessagesDbContext>().Database.MigrateAsync();

        await SeedAsync(services);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
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

        var channelsDb = services.GetRequiredService<ChannelsDbContext>();
        var messagesDb = services.GetRequiredService<MessagesDbContext>();
        var channelRepository = services.GetRequiredService<IChannelRepository>();
        var conversationRepository = services.GetRequiredService<IConversationRepository>();
        var messageRepository = services.GetRequiredService<IMessageRepository>();
        var quickReplyRepository = services.GetRequiredService<IQuickReplyRepository>();

        var alphaWhatsApp = Channel.Create(alpha.Id, ChannelType.WhatsApp, "Alpha WhatsApp", "+963111111111", now);
        alphaWhatsApp.Activate();
        var betaWhatsApp = Channel.Create(beta.Id, ChannelType.WhatsApp, "Beta WhatsApp", "+963999999999", now);
        betaWhatsApp.Activate();
        await channelRepository.AddAsync(alphaWhatsApp, CancellationToken.None);
        await channelRepository.AddAsync(betaWhatsApp, CancellationToken.None);
        await channelsDb.SaveChangesAsync();

        AlphaWhatsAppChannelId = alphaWhatsApp.Id.Value;
        BetaWhatsAppChannelId = betaWhatsApp.Id.Value;

        var credentialStore = services.GetRequiredService<IChannelCredentialStore>();

        await credentialStore.SetAsync(alpha.Id, alphaWhatsApp.Id.Value, ChannelCredentialKeys.AccessToken, "test-access-token", CancellationToken.None);
        await credentialStore.SetAsync(alpha.Id, alphaWhatsApp.Id.Value, ChannelCredentialKeys.AppSecret, WhatsAppAppSecret, CancellationToken.None);
        await credentialStore.SetAsync(alpha.Id, alphaWhatsApp.Id.Value, ChannelCredentialKeys.VerifyToken, WhatsAppVerifyToken, CancellationToken.None);
        await credentialStore.SetAsync(alpha.Id, alphaWhatsApp.Id.Value, ChannelCredentialKeys.PhoneNumberId, AlphaWhatsAppExternalId, CancellationToken.None);

        await credentialStore.SetAsync(beta.Id, betaWhatsApp.Id.Value, ChannelCredentialKeys.AccessToken, "test-access-token-b", CancellationToken.None);
        await credentialStore.SetAsync(beta.Id, betaWhatsApp.Id.Value, ChannelCredentialKeys.AppSecret, WhatsAppAppSecret, CancellationToken.None);
        await credentialStore.SetAsync(beta.Id, betaWhatsApp.Id.Value, ChannelCredentialKeys.VerifyToken, WhatsAppVerifyToken + "-b", CancellationToken.None);
        await credentialStore.SetAsync(beta.Id, betaWhatsApp.Id.Value, ChannelCredentialKeys.PhoneNumberId, "963999999999", CancellationToken.None);

        var alphaTelegram = Channel.Create(alpha.Id, ChannelType.Telegram, "Alpha Telegram", "@wasla_alpha_bot", now);
        alphaTelegram.Activate();
        await channelRepository.AddAsync(alphaTelegram, CancellationToken.None);
        await channelsDb.SaveChangesAsync();

        AlphaTelegramChannelId = alphaTelegram.Id.Value;

        await credentialStore.SetAsync(alpha.Id, alphaTelegram.Id.Value, ChannelCredentialKeys.AccessToken, "test-telegram-token", CancellationToken.None);
        await credentialStore.SetAsync(alpha.Id, alphaTelegram.Id.Value, ChannelCredentialKeys.VerifyToken, TelegramWebhookSecret, CancellationToken.None);

        var alphaConversation = Conversation.Create(alpha.Id, alice.Id.Value, alphaWhatsApp.Id.Value, now.AddMinutes(-10));
        alphaConversation.AddTag(vipTag.Id.Value, null, now.AddMinutes(-9));
        var alphaInbound = Message.CreateInbound(alpha.Id, alphaConversation.Id.Value, alphaWhatsApp.Id.Value, MessageType.Text, "Hello, I need help with my order", "seed-alpha-1", now.AddMinutes(-8));
        alphaConversation.RecordMessage(alphaInbound.Id.Value, true, alphaInbound.Body, now.AddMinutes(-8));
        await conversationRepository.AddAsync(alphaConversation, CancellationToken.None);
        await conversationsDb.SaveChangesAsync();
        await messageRepository.AddAsync(alphaInbound, CancellationToken.None);
        await messagesDb.SaveChangesAsync();
        AlphaConversationId = alphaConversation.Id.Value;

        var betaConversation = Conversation.Create(beta.Id, zed.Id.Value, betaWhatsApp.Id.Value, now.AddMinutes(-5));
        await conversationRepository.AddAsync(betaConversation, CancellationToken.None);
        await conversationsDb.SaveChangesAsync();
        BetaConversationId = betaConversation.Id.Value;

        var welcomeReply = QuickReply.Create(alpha.Id, "welcome", "Hello {{customer.name}}, how can we help you?", now);
        await quickReplyRepository.AddAsync(welcomeReply, CancellationToken.None);
        await conversationsDb.SaveChangesAsync();
    }

    public static string ComputeSignature(string appSecret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));

        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    public async Task<int> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        using var scope = GetFactory().Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<InboxProcessor>().ProcessPendingAsync(50, cancellationToken);
    }

    public async Task<int> DispatchOutboxAsync(CancellationToken cancellationToken = default)
    {
        using var scope = GetFactory().Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessPendingAsync(50, cancellationToken);
    }

    private sealed class ApiFactory(string connectionString, string redisConnectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Redis:ConnectionString"] = redisConnectionString,
                    ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
                    ["Telemetry:OtlpEndpoint"] = null,
                    ["Security:EncryptionKey"] = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
                    ["WhatsApp:BaseUrl"] = "http://127.0.0.1:9/",
                    ["WhatsApp:SkipConnectVerification"] = "true",
                    ["Workers:InboxEnabled"] = "false",
                    ["Workers:OutboxEnabled"] = "false",
                    ["Telegram:BaseUrl"] = "http://127.0.0.1:9/",
                    ["Telegram:SkipConnectVerification"] = "true",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChannelAdapterRegistry>();
                services.AddScoped<IChannelAdapterRegistry>(provider => new TestChannelAdapterRegistry(provider));
                services.AddSingleton<TestOutboundSink>();
            });
        }
    }
}

    public sealed class TestOutboundSink
    {
        private readonly object _gate = new();
        private readonly List<ChannelOutboundMessage> _messages = [];
        private readonly List<ChannelOutboundMediaMessage> _mediaMessages = [];

        private readonly List<InboundMediaReference> _mediaDownloads = [];

        public IReadOnlyList<ChannelOutboundMessage> Messages
        {
            get { lock (_gate) { return _messages.ToList(); } }
        }

        public IReadOnlyList<ChannelOutboundMediaMessage> MediaMessages
        {
            get { lock (_gate) { return _mediaMessages.ToList(); } }
        }

        public void RecordDownload(InboundMediaReference reference)
        {
            lock (_gate) { _mediaDownloads.Add(reference); }
        }

        public IReadOnlyList<InboundMediaReference> MediaDownloads
        {
            get { lock (_gate) { return _mediaDownloads.ToList(); } }
        }

        public void Record(ChannelOutboundMessage message)
        {
            lock (_gate) { _messages.Add(message); }
        }

        public void RecordMedia(ChannelOutboundMediaMessage message)
        {
            lock (_gate) { _mediaMessages.Add(message); }
        }
    }

    public sealed class TestChannelAdapterRegistry(IServiceProvider provider) : IChannelAdapterRegistry
    {
        public IChannelAdapter? Resolve(ChannelType channelType) =>
            channelType switch
            {
                ChannelType.WhatsApp => new TestChannelAdapter(
                    provider.GetRequiredService<WhatsAppCloudAdapter>(),
                    provider.GetRequiredService<TestOutboundSink>()),
                ChannelType.Telegram => new TestChannelAdapter(
                    provider.GetRequiredService<TelegramBotAdapter>(),
                    provider.GetRequiredService<TestOutboundSink>()),
                _ => null,
            };
    }

    /// <summary>Real verification/normalization; stubbed transport for sends (no live Meta calls).</summary>
    public sealed class TestChannelAdapter(IChannelAdapter inner, TestOutboundSink sink) : IChannelAdapter, IChannelMediaDownloader
    {
        public ChannelType ChannelType => inner.ChannelType;

        public ChannelCapabilities Capabilities => inner.Capabilities;

        public Task<WebhookVerificationResult> VerifyWebhookAsync(ChannelWebhookContext context, CancellationToken cancellationToken) =>
            inner.VerifyWebhookAsync(context, cancellationToken);

        public Task<IReadOnlyList<NormalizedInboundEvent>> NormalizeInboundAsync(ChannelWebhookContext context, CancellationToken cancellationToken) =>
            inner.NormalizeInboundAsync(context, cancellationToken);

        public Task<DownloadedMedia?> DownloadAsync(TenantId tenantId, Guid channelId, InboundMediaReference media, CancellationToken cancellationToken)
        {
            sink.RecordDownload(media);

            return Task.FromResult<DownloadedMedia?>(new DownloadedMedia([137, 80, 78, 71, 13, 10, 26, 10], "image/png", "photo.png"));
        }

        public Task<ChannelSendResult> SendMessageAsync(ChannelOutboundMessage message, CancellationToken cancellationToken)
        {
            sink.Record(message);

            return Task.FromResult(ChannelSendResult.Success($"wamid.TEST-{message.MessageId:N}"));
        }

        public Task<ChannelSendResult> SendMediaAsync(ChannelOutboundMediaMessage message, CancellationToken cancellationToken)
        {
            sink.RecordMedia(message);

            return Task.FromResult(ChannelSendResult.Success($"wamid.TEST-{message.MessageId:N}"));
        }
    }

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationEnvironment>
{
}
