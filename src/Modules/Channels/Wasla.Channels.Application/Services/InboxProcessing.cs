using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;
using Wasla.Conversations.Application.Contracts;
using Wasla.Customers.Application.Contracts;
using Wasla.Messages.Application;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Channels.Application.Services;

/// <summary>Claims due inbox rows for processing (no tenant filter; worker scope).</summary>
public sealed class InboxClaimer(IInboxRepository inbox)
{
    public Task<List<InboxClaim>> ClaimAsync(int batchSize, CancellationToken cancellationToken) =>
        inbox.ClaimPendingAsync(batchSize, cancellationToken);
}

public interface IInboundEventApplier
{
    Task ApplyAsync(Guid inboxEventId, CancellationToken cancellationToken);
}

/// <summary>
/// Applies one claimed inbox event: normalizes via the channel adapter and drives the
/// domain (Customers identity resolution, Conversation resolution, Message insert) through
/// module contracts. Idempotent at every step: provider-message-id dedupe at the message
/// level and a timeline guard on conversation counters make replays safe.
/// </summary>
public sealed class InboundEventApplier(
    IInboxRepository inbox,
    IChannelRepository channels,
    IChannelAdapterRegistry adapters,
    IMessageRepository messages,
    IMessagesUnitOfWork messagesUnitOfWork,
    ICustomerResolver customers,
    IConversationResolver conversations,
    IConversationWriter conversationWriter,
    InboundMediaRecorder inboundMedia,
    IChannelsUnitOfWork unitOfWork,
    IOptions<WorkerOptions> workerOptions,
    IClock clock,
    IRealtimePublisher realtime) : IInboundEventApplier
{
    private static readonly Dictionary<string, string> EmptyStrings = [];

    public async Task ApplyAsync(Guid inboxEventId, CancellationToken cancellationToken)
    {
        var inboxEvent = await inbox.GetByIdAsync(inboxEventId, cancellationToken);

        if (inboxEvent is null || inboxEvent.Status != InboxEventStatus.Processing)
        {
            return;
        }

        var now = clock.UtcNow;

        try
        {
            var channel = await channels.GetByIdAsync(
                inboxEvent.TenantId,
                new ChannelId(inboxEvent.ChannelId),
                cancellationToken);

            if (channel is null)
            {
                inboxEvent.MarkSkipped(now, "Channel no longer exists.");
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return;
            }

            var adapter = adapters.Resolve(channel.Type);

            if (adapter is null)
            {
                inboxEvent.MarkSkipped(now, $"No adapter registered for channel type '{channel.Type}'.");
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return;
            }

            var context = new ChannelWebhookContext(
                inboxEvent.ChannelId,
                inboxEvent.TenantId,
                "POST",
                EmptyStrings,
                EmptyStrings,
                Encoding.UTF8.GetBytes(inboxEvent.Payload),
                inboxEvent.ReceivedAt);

            var events = await adapter.NormalizeInboundAsync(context, cancellationToken);

            var applied = 0;

            foreach (var normalizedEvent in events)
            {
                switch (normalizedEvent)
                {
                    case InboundMessageEvent messageEvent:
                        await ApplyInboundMessageAsync(inboxEvent, channel.Type, adapter, messageEvent, cancellationToken);
                        applied++;
                        break;

                    case MessageStatusUpdateEvent statusEvent:
                        await ApplyStatusUpdateAsync(inboxEvent.TenantId, statusEvent, cancellationToken);
                        applied++;
                        break;

                    case ChannelHealthEvent:
                        // Health signals are informational in the MVP; ignored here.
                        break;
                }
            }

            if (applied == 0)
            {
                inboxEvent.MarkSkipped(now, "No actionable events in payload.");
            }
            else
            {
                inboxEvent.MarkProcessed(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(inboxEvent, exception.Message, cancellationToken);
        }
    }

    private async Task ApplyInboundMessageAsync(
        InboxEvent inboxEvent,
        ChannelType channelType,
        IChannelAdapter adapter,
        InboundMessageEvent message,
        CancellationToken cancellationToken)
    {
        var tenantId = inboxEvent.TenantId;

        var existing = await messages.FindByProviderMessageIdAsync(
            tenantId,
            message.ProviderMessageId,
            cancellationToken);

        if (existing is not null)
        {
            // Duplicate delivery: the conversation counter guard makes this a no-op
            // unless a previous attempt crashed between the two writes.
            await conversationWriter.RecordInboundMessageAsync(
                tenantId,
                existing.ConversationId,
                existing.Id.Value,
                Preview(message.Body, message.Type),
                message.SentAt,
                cancellationToken);

            await EnsureMediaAsync(inboxEvent, adapter, existing, message, cancellationToken);

            return;
        }

        var customerId = await customers.ResolveOrCreateByChannelIdentityAsync(
            tenantId,
            channelType,
            message.ExternalCustomerId,
            message.CustomerDisplayName,
            cancellationToken);

        var conversationId = await conversations.ResolveOrCreateForInboundAsync(
            tenantId,
            customerId,
            inboxEvent.ChannelId,
            message.SentAt,
            cancellationToken);

        var entity = Message.CreateInbound(
            tenantId,
            conversationId,
            inboxEvent.ChannelId,
            message.Type,
            message.Body,
            message.ProviderMessageId,
            message.SentAt);

        if (!string.IsNullOrWhiteSpace(message.ProviderMetadata))
        {
            entity.AttachProviderMetadata(message.ProviderMetadata);
        }

        await messages.AddAsync(entity, cancellationToken);
        await messagesUnitOfWork.SaveChangesAsync(cancellationToken);

        await conversationWriter.RecordInboundMessageAsync(
            tenantId,
            conversationId,
            entity.Id.Value,
            Preview(message.Body, message.Type),
            message.SentAt,
            cancellationToken);

        await EnsureMediaAsync(inboxEvent, adapter, entity, message, cancellationToken);

        await realtime.PublishToTenantAsync(
            tenantId,
            RealtimeEvents.NewMessage,
            new NewMessageEvent(conversationId, entity.Id.Value, "Inbound", Preview(message.Body, message.Type), message.SentAt),
            cancellationToken);
    }

    private async Task EnsureMediaAsync(
        InboxEvent inboxEvent,
        IChannelAdapter adapter,
        Message entity,
        InboundMessageEvent message,
        CancellationToken cancellationToken)
    {
        if (entity.Attachments.Count > 0 || message.Media.Count == 0)
        {
            return;
        }

        if (adapter is not IChannelMediaDownloader downloader)
        {
            return;
        }

        // Media is best-effort: the message itself is already durable, so a failed
        // download never blocks the event (A22 pipeline; retries on next delivery).
        foreach (var reference in message.Media.Take(3))
        {
            try
            {
                var downloaded = await downloader.DownloadAsync(inboxEvent.TenantId, inboxEvent.ChannelId, reference, cancellationToken);

                if (downloaded is null)
                {
                    continue;
                }

                await inboundMedia.RecordAsync(
                    inboxEvent.TenantId,
                    entity.Id.Value,
                    downloaded.Content,
                    downloaded.ContentType,
                    downloaded.FileName,
                    cancellationToken);
            }
            catch
            {
                // Provider media unavailable; the message stays media-less.
            }
        }
    }

    private async Task ApplyStatusUpdateAsync(
        TenantId tenantId,
        MessageStatusUpdateEvent update,
        CancellationToken cancellationToken)
    {
        var message = await messages.FindByProviderMessageIdAsync(
            tenantId,
            update.ProviderMessageId,
            cancellationToken);

        if (message is null || message.Direction != MessageDirection.Outbound)
        {
            // Unknown message (e.g. retention) or an inbound echo — nothing to update.
            return;
        }

        var now = clock.UtcNow;

        switch (update.Kind)
        {
            case MessageStatusUpdateKind.Sent:
                message.MarkSent(null, now);
                break;

            case MessageStatusUpdateKind.Delivered:
                message.MarkDelivered(now);
                break;

            case MessageStatusUpdateKind.Read:
                message.MarkRead(now);
                break;

            case MessageStatusUpdateKind.Failed:
                message.MarkFailed(update.FailureReason ?? "Provider reported a failure.", now);
                break;
        }

        await messagesUnitOfWork.SaveChangesAsync(cancellationToken);

        await realtime.PublishToTenantAsync(
            tenantId,
            RealtimeEvents.MessageStatusChanged,
            new MessageStatusChangedEvent(message.ConversationId, message.Id.Value, message.Status.ToString(), message.ProviderMessageId, now),
            cancellationToken);
    }

    private async Task RecordFailureAsync(InboxEvent inboxEvent, string error, CancellationToken cancellationToken)
    {
        try
        {
            var now = clock.UtcNow;
            var maxAttempts = Math.Max(1, workerOptions.Value.MaxAttempts);
            var truncated = error.Length <= 500 ? error : error[..500];

            if (inboxEvent.Attempts >= maxAttempts)
            {
                inboxEvent.MarkDeadLettered(now, $"Retry budget exhausted after {inboxEvent.Attempts} attempts: {truncated}");
            }
            else
            {
                var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(inboxEvent.Attempts, 8))));
                inboxEvent.MarkPendingRetry(now, truncated, now + delay);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The stale-claim window in ClaimPendingAsync will make the row reclaimable.
        }
    }

    private static string? Preview(string? body, MessageType type) =>
        string.IsNullOrWhiteSpace(body) ? $"({type})" : body;
}

/// <summary>
/// Processes due inbox rows end-to-end in fresh DI scopes (one tenant-resolved scope per
/// row). Called by the background worker and directly by integration tests.
/// </summary>
public sealed class InboxProcessor(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> workerOptions)
{
    public async Task<int> ProcessPendingAsync(int? batchSize, CancellationToken cancellationToken)
    {
        var size = Math.Max(1, batchSize ?? workerOptions.Value.BatchSize);
        List<InboxClaim> claims;

        using (var claimScope = scopeFactory.CreateScope())
        {
            var claimer = claimScope.ServiceProvider.GetRequiredService<InboxClaimer>();
            claims = await claimer.ClaimAsync(size, cancellationToken);
        }

        foreach (var claim in claims)
        {
            using var scope = scopeFactory.CreateScope();

            var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantContext.Resolve(claim.TenantId);

            var applier = scope.ServiceProvider.GetRequiredService<IInboundEventApplier>();

            try
            {
                await applier.ApplyAsync(claim.Id, cancellationToken);
            }
            catch
            {
                // ApplyAsync records its own failures; never break the batch.
            }
        }

        return claims.Count;
    }
}
