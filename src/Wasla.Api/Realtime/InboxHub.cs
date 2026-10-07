using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Application.Security;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Api.Realtime;

/// <summary>
/// The unified-inbox realtime hub. Connections authenticate with the JWT (header on
/// negotiate, access_token query for websocket upgrades) and join a single tenant-scoped
/// group; every broadcast is addressed to that group only, so tenants can never observe
/// each other's traffic (ADR-0006). Presence is tracked in Redis, best-effort.
/// </summary>
[Authorize]
public sealed class InboxHub(IConnectionMultiplexer redis, ILogger<InboxHub> logger) : Hub
{
    public static string TenantGroup(TenantId tenantId) => $"tenant:{tenantId.Value}";

    private static string PresenceKey(TenantId tenantId) => $"presence:{tenantId.Value}";

    public override async Task OnConnectedAsync()
    {
        if (!TryGetIdentity(out var tenantId, out var userId))
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
        await UpdatePresenceAsync(tenantId, userId, delta: 1, online: true);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetIdentity(out var tenantId, out var userId))
        {
            await UpdatePresenceAsync(tenantId, userId, delta: -1, online: false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Relays an agent's typing signal to the rest of the tenant (never echoed to the sender).</summary>
    public Task Typing(Guid conversationId)
    {
        if (!TryGetIdentity(out var tenantId, out var userId))
        {
            return Task.CompletedTask;
        }

        return Clients.OthersInGroup(TenantGroup(tenantId)).SendAsync(
            RealtimeEvents.Typing,
            new TypingEvent(conversationId, userId, DateTimeOffset.UtcNow));
    }

    private bool TryGetIdentity(out TenantId tenantId, out Guid userId)
    {
        tenantId = default;
        userId = default;

        var tenantClaim = Context.User?.FindFirst(WaslaClaimTypes.TenantId)?.Value;

        if (!Guid.TryParse(tenantClaim, out var tenantGuid))
        {
            return false;
        }

        if (!Guid.TryParse(Context.User?.FindFirst(WaslaClaimTypes.Subject)?.Value, out userId))
        {
            return false;
        }

        tenantId = new TenantId(tenantGuid);

        return true;
    }

    private async Task UpdatePresenceAsync(TenantId tenantId, Guid userId, int delta, bool online)
    {
        try
        {
            var database = redis.GetDatabase();
            var key = PresenceKey(tenantId);
            var remaining = await database.HashIncrementAsync(key, userId.ToString(), delta);

            if (remaining <= 0)
            {
                await database.HashDeleteAsync(key, userId.ToString());
            }

            await database.KeyExpireAsync(key, TimeSpan.FromHours(24));

            await Clients.OthersInGroup(TenantGroup(tenantId)).SendAsync(
                RealtimeEvents.PresenceChanged,
                new PresenceChangedEvent(userId, online && remaining > 0, DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            // Presence is best-effort telemetry; a Redis outage must not break live messaging.
            logger.LogWarning(exception, "Presence update failed for tenant {TenantId}.", tenantId.Value);
        }
    }
}
