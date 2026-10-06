using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Conversations.Application.Contracts;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application;

/// <summary>Serialized instruction for delivering one outbound message on its channel.</summary>
public sealed record OutboxSendPayload(
    Guid MessageId,
    Guid ConversationId,
    string Type,
    string? Body,
    List<Guid> MediaFileIds);

/// <summary>Claims due outbox rows for processing (no tenant filter; worker scope).</summary>
public sealed class OutboxClaimer(IOutboxRepository outbox)
{
    public Task<List<OutboxClaim>> ClaimAsync(int batchSize, CancellationToken cancellationToken) =>
        outbox.ClaimPendingAsync(batchSize, cancellationToken);
}

/// <summary>
/// Delivers one claimed outbox row: resolves routing facts, invokes the channel adapter
/// and applies the send outcome to the message (Sent / Failed) and the outbox row
/// (Processed / retry / dead-letter). Outcome-uncertain sends are never blind-retried:
/// they exhaust the attempt budget and are reconciled by status webhooks.
/// </summary>
public sealed class OutboxMessageDispatcher(
    ITenantContext tenantContext,
    IOutboxRepository outbox,
    IMessageRepository messages,
    IConversationInfoProvider conversations,
    IChannelRoutingProvider channels,
    ICustomerInfoProvider customers,
    IChannelAdapterRegistry adapters,
    IMessagesUnitOfWork unitOfWork,
    IOptions<WorkerOptions> workerOptions,
    IClock clock)
{
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var row = await outbox.GetByIdAsync(tenantId, outboxMessageId, cancellationToken);

        if (row is null || row.Status != OutboxMessageStatus.Processing)
        {
            return;
        }

        try
        {
            await DeliverAsync(tenantId, row, cancellationToken);
        }
        catch (Exception exception)
        {
            await TryRecordFailureAsync(row, exception.Message, cancellationToken);
        }
    }

    private async Task DeliverAsync(TenantId tenantId, OutboxMessage row, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        OutboxSendPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<OutboxSendPayload>(row.Payload, PayloadJson);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null)
        {
            row.MarkDeadLettered(now, "Outbox payload is not valid JSON.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var conversation = await conversations.GetAsync(tenantId, payload.ConversationId, cancellationToken);

        if (conversation is null)
        {
            row.MarkDeadLettered(now, "Conversation no longer exists.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var routing = await channels.GetRoutingAsync(tenantId, conversation.ChannelId, cancellationToken);

        if (routing is null)
        {
            row.MarkDeadLettered(now, "Channel no longer exists.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        if (routing.Status == "Disabled")
        {
            await RescheduleAsync(row, now, null, "Channel is disabled; send deferred.", cancellationToken);

            return;
        }

        var adapter = adapters.Resolve(routing.Type);

        if (adapter is null)
        {
            row.MarkDeadLettered(now, $"No adapter registered for channel type '{routing.Type}'.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var message = await messages.GetByIdAsync(tenantId, payload.MessageId, cancellationToken);

        if (message is null)
        {
            row.MarkDeadLettered(now, "Message no longer exists.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        if (message.Status != MessageStatus.Pending)
        {
            // Already delivered or failed (e.g. duplicate outbox row) — nothing to do.
            row.MarkProcessed(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var recipient = await customers.GetChannelExternalIdAsync(
            tenantId,
            conversation.CustomerId,
            routing.Type,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(recipient))
        {
            message.MarkFailed("Customer has no identity on this channel.", now);
            row.MarkDeadLettered(now, "Recipient identity missing.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        ChannelSendResult? result;

        if (message.Type == MessageType.Text || message.Attachments.Count == 0)
        {
            result = await adapter.SendMessageAsync(
                new ChannelOutboundMessage(
                    tenantId,
                    conversation.ChannelId,
                    message.Id.Value,
                    recipient,
                    message.Body ?? string.Empty),
                cancellationToken);
        }
        else
        {
            // Returns null when a local precondition failed (row already dead-lettered).
            result = await SendMediaAsync(tenantId, conversation.ChannelId, message, recipient, adapter, cancellationToken);
        }

        if (result is null)
        {
            return;
        }

        await ApplySendResultAsync(row, message, result, now, cancellationToken);
    }

    private async Task<ChannelSendResult?> SendMediaAsync(
        TenantId tenantId,
        Guid channelId,
        Message message,
        string recipient,
        IChannelAdapter adapter,
        CancellationToken cancellationToken)
    {
        var attachment = message.Attachments[0];
        var media = await messages.GetMediaFileAsync(tenantId, attachment.MediaFileId, cancellationToken);

        if (media is null)
        {
            // Local precondition failure: fail the message and dead-letter (no retry can fix it).
            message.MarkFailed("Attached media file is missing.", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return null;
        }

        return await adapter.SendMediaAsync(
            new ChannelOutboundMediaMessage(
                tenantId,
                channelId,
                message.Id.Value,
                recipient,
                message.Type,
                media.StorageKey,
                media.ContentType,
                media.FileName,
                media.Size,
                Caption(message)),
            cancellationToken);
    }

    private static string? Caption(Message message) =>
        message.Type == MessageType.Text ? null : message.Body;

    private async Task ApplySendResultAsync(
        OutboxMessage row,
        Message message,
        ChannelSendResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (result.IsSuccess && result.ProviderMessageId is not null)
        {
            message.MarkSent(result.ProviderMessageId, now);
            row.MarkProcessed(now);
        }
        else if (result.IsSuccess)
        {
            message.MarkFailed("Provider accepted the request without a message id.", now);
            row.MarkDeadLettered(now, "Provider returned no message id.");
        }
        else if (result.FailureKind is ChannelSendFailureKind.Rejected or ChannelSendFailureKind.Invalid)
        {
            message.MarkFailed(result.FailureReason ?? "Provider rejected the message.", now);
            row.MarkDeadLettered(now, result.FailureReason ?? "Provider rejected the message.");
        }
        else
        {
            await RescheduleAsync(row, now, result.RetryAfter, result.FailureReason, cancellationToken);

            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RescheduleAsync(
        OutboxMessage row,
        DateTimeOffset now,
        TimeSpan? retryAfter,
        string? error,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, workerOptions.Value.MaxAttempts);

        if (row.Attempts >= maxAttempts)
        {
            row.MarkDeadLettered(now, $"Retry budget exhausted after {row.Attempts} attempts: {error}");

            // The message outcome is uncertain; it will be reconciled by status webhooks.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var backoff = retryAfter ?? TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(row.Attempts, 8))));

        row.Reschedule(now, backoff, error);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task TryRecordFailureAsync(OutboxMessage row, string error, CancellationToken cancellationToken)
    {
        try
        {
            var maxAttempts = Math.Max(1, workerOptions.Value.MaxAttempts);

            if (row.Attempts >= maxAttempts)
            {
                row.MarkDeadLettered(clock.UtcNow, $"Retry budget exhausted after {row.Attempts} attempts: {error}");
            }
            else
            {
                row.Reschedule(clock.UtcNow, TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(row.Attempts, 8)))), error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The stale-claim window in ClaimPendingAsync will make the row reclaimable.
        }
    }
}

/// <summary>
/// Processes due outbox rows end-to-end in fresh DI scopes (one tenant-resolved scope per
/// row). Called by the background worker and directly by integration tests.
/// </summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> workerOptions)
{
    public async Task<int> ProcessPendingAsync(int? batchSize, CancellationToken cancellationToken)
    {
        var size = Math.Max(1, batchSize ?? workerOptions.Value.BatchSize);
        List<OutboxClaim> claims;

        using (var claimScope = scopeFactory.CreateScope())
        {
            var claimer = claimScope.ServiceProvider.GetRequiredService<OutboxClaimer>();
            claims = await claimer.ClaimAsync(size, cancellationToken);
        }

        foreach (var claim in claims)
        {
            using var scope = scopeFactory.CreateScope();

            var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantContext.Resolve(claim.TenantId);

            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxMessageDispatcher>();

            try
            {
                await dispatcher.DispatchAsync(claim.Id, cancellationToken);
            }
            catch
            {
                // DispatchAsync records its own failures; never break the batch.
            }
        }

        return claims.Count;
    }
}
