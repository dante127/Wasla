using Microsoft.Extensions.Options;
using Wasla.Analytics.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;

namespace Wasla.Api.Workers;

/// <summary>Periodically recomputes analytics fact tables and rollups for all tenants.</summary>
public sealed class AnalyticsRecomputeWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<AnalyticsRecomputeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.AnalyticsEnabled)
        {
            logger.LogInformation("Analytics worker is disabled by configuration.");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int recomputed;

                using (var scope = scopeFactory.CreateScope())
                {
                    recomputed = await scope.ServiceProvider
                        .GetRequiredService<TenantAnalyticsRecomputer>()
                        .RecomputeAllTenantsAsync(stoppingToken);
                }

                logger.LogInformation("Analytics recompute finished ({Count} conversations).", recomputed);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Analytics recompute cycle failed; continuing.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(Math.Max(1, options.Value.AnalyticsIntervalMinutes)),
                stoppingToken);
        }
    }
}
