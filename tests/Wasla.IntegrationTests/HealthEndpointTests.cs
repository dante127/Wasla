using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Wasla.IntegrationTests;

/// <summary>
/// Boots the API for integration tests with deterministic configuration.
/// The tested endpoints here must not require live PostgreSQL/Redis. 
/// </summary>
public sealed class WaslaApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=wasla_tests;Username=tests;Password=tests",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Telemetry:OtlpEndpoint"] = null,
            });
        });
    }
}

public sealed class HealthEndpointTests(WaslaApiFactory factory) : IClassFixture<WaslaApiFactory>
{
    [Fact]
    public async Task Live_health_endpoint_returns_ok_without_external_dependencies()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/definitely-not-a-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
