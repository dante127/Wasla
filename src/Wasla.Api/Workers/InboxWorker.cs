using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Channels.Application.Services;

namespace Wasla.Api.Workers;

/// <summary>Background worker driving the inbound webhook inbox processor.</summary>
public sealed class InboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<InboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.InboxEnabled)
        {
            logger.LogInformation("Inbox worker is disabled by configuration.");

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
                        .GetRequiredService<InboxProcessor>()
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
                logger.LogError(exception, "Inbox worker cycle failed; continuing.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
