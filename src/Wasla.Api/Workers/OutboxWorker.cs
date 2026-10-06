using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Messages.Application;

namespace Wasla.Api.Workers;

/// <summary>Background worker driving the outbound message (outbox) dispatcher.</summary>
public sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.OutboxEnabled)
        {
            logger.LogInformation("Outbox worker is disabled by configuration.");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;

                using (var scope = scopeFactory.CreateScope())
                {
                    processed = await scope.ServiceProvider
                        .GetRequiredService<OutboxProcessor>()
                        .ProcessPendingAsync(null, stoppingToken);
                }

                var delay = processed > 0
                    ? TimeSpan.FromMilliseconds(200)
                    : TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox worker cycle failed; continuing.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
