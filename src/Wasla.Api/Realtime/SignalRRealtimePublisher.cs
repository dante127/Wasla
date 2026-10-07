using Microsoft.AspNetCore.SignalR;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Api.Realtime;

/// <summary>
/// SignalR-backed realtime fan-out. Failures are logged and swallowed: realtime must
/// never fail the business operation it reports.
/// </summary>
public sealed class SignalRRealtimePublisher(
    IHubContext<InboxHub> hubContext,
    ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    public async Task PublishToTenantAsync(
        TenantId tenantId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .Group(InboxHub.TenantGroup(tenantId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Realtime publish of {EventName} failed.", eventName);
        }
    }
}
