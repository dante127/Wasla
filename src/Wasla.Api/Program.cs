using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using Wasla.Analytics.Infrastructure;
using Wasla.Api.Configuration;
using Wasla.Api.Endpoints;
using Wasla.Api.Health;
using Wasla.Api.Realtime;
using Wasla.Api.Seeding;
using Wasla.Audit.Infrastructure;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Application.Security;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.BuildingBlocks.Infrastructure.Security;
using Wasla.Api.Workers;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.BuildingBlocks.Web;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Channels.Infrastructure;
using Wasla.Conversations.Infrastructure;
using Wasla.Customers.Infrastructure;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Infrastructure;
using Wasla.Messages.Infrastructure;
using Wasla.Notifications.Infrastructure;
using Wasla.Tasks.Infrastructure;
using Wasla.Teams.Infrastructure;
using Wasla.Tenancy.Infrastructure;
using Wasla.Tickets.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// WASLA_-prefixed environment variables override configuration (see .env.example).
builder.Configuration.AddEnvironmentVariables(prefix: "WASLA_");

// Structured logging (JSON console) via Serilog; levels come from appsettings.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// ---------------------------------------------------------------- options ---
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Database:ConnectionString is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.SectionName))
    .Validate(
        static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Redis:ConnectionString is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<TelemetryOptions>()
    .Bind(builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services
    .AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName));

builder.Services
    .AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddSingleton<IStringEncryptor, AesGcmStringEncryptor>();
builder.Services.AddHostedService<InboxWorker>();
builder.Services.AddHostedService<OutboxWorker>();

// ------------------------------------------- infrastructure (all lazy) ------
// Nothing below connects at startup; connections happen on first use.
builder.Services.AddSingleton<NpgsqlDataSource>(static serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    return NpgsqlDataSource.Create(options.ConnectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(static serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
    var redisConfiguration = ConfigurationOptions.Parse(options.ConnectionString);
    redisConfiguration.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(redisConfiguration);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IClock, SystemClock>();

// ------------------------------------------------------- authN & authZ ------
// JWT validation is configured through the options pipeline so it always uses the
// same JwtOptions values as token issuance (validated on startup by the Identity module).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = WaslaClaimTypes.Name,
        };

        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // SignalR upgrades cannot send headers; accept the token from the query for hub paths.
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ------------------------------------------------------------- telemetry ----
var telemetryOptions = builder.Configuration
    .GetSection(TelemetryOptions.SectionName)
    .Get<TelemetryOptions>() ?? new TelemetryOptions();

var openTelemetry = builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(telemetryOptions.ServiceName));

openTelemetry.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation());

openTelemetry.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddRuntimeInstrumentation());

if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
{
    var otlpEndpoint = new Uri(telemetryOptions.OtlpEndpoint);

    openTelemetry.WithTracing(tracing => tracing.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint));
    openTelemetry.WithMetrics(metrics => metrics.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint));
}

// -------------------------------------------------------- errors & health ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddHealthChecks()
    .AddCheck("self", static () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

// -------------------------------------------------------------- web bits ----
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
{
    if (corsOrigins.Length > 0)
    {
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

// ---------------------------------------------------------------- modules ---
IModule[] modules =
[
    new TenancyModule(),
    new IdentityModule(),
    new TeamsModule(),
    new CustomersModule(),
    new ConversationsModule(),
    new MessagesModule(),
    new ChannelsModule(),
    new TicketsModule(),
    new TasksModule(),
    new NotificationsModule(),
    new AnalyticsModule(),
    new AuditModule(),
];

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

if (args.Contains("--seed"))
{
    await app.SeedDatabaseAsync();
    return;
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("frontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static registration => registration.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static registration => registration.Tags.Contains("ready"),
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapTenancyEndpoints();
app.MapTeamEndpoints();
app.MapCustomerEndpoints();
app.MapTagEndpoints();
app.MapChannelEndpoints();
app.MapConversationEndpoints();
app.MapMediaEndpoints();
app.MapQuickReplyEndpoints();
app.MapWebhookEndpoints();

app.MapHub<InboxHub>("/hubs/inbox");

app.Run();

/// <summary>Entry point marker so integration tests can use WebApplicationFactory.</summary>
public partial class Program
{
}