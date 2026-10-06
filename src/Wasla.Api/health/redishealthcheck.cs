using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Wasla.Api.Health;

/// <summary>Readiness check: Redis must answer PING.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connectionMultiplexer.GetDatabase().PingAsync();

            return latency >= TimeSpan.Zero
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Unexpected Redis ping latency.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is not reachable.", exception);
        }
    }
}
