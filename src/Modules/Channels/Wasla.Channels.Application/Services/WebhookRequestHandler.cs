using System.Security.Cryptography;
using System.Text;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application.Services;

/// <summary>
/// The provider-neutral webhook fast path (docs/webhooks.md): size limit → channel
/// resolution (server-side, from the opaque channel id) → signature verification →
/// raw persist with dedupe. All heavy work happens later in the inbox worker.
/// </summary>
public sealed class WebhookRequestHandler(
    IChannelRepository channels,
    IChannelAdapterRegistry adapters,
    IInboxRepository inbox,
    IChannelsUnitOfWork unitOfWork,
    IClock clock) : IWebhookRequestHandler
{
    public const int MaxPayloadBytes = 256 * 1024;

    public async Task<WebhookResult> HandleAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        if (request.Body.Length > MaxPayloadBytes)
        {
            return new WebhookResult(413, "payload.too_large");
        }

        var channel = await channels.GetByIdForSystemAsync(new ChannelId(request.ChannelId), cancellationToken);

        if (channel is null)
        {
            return new WebhookResult(404, "channel.not_found");
        }

        var adapter = adapters.Resolve(channel.Type);

        if (adapter is null)
        {
            return new WebhookResult(501, "channel.adapter_unavailable");
        }

        var context = new ChannelWebhookContext(
            channel.Id.Value,
            channel.TenantId,
            request.HttpMethod,
            request.Headers,
            request.Query,
            request.Body,
            clock.UtcNow);

        var verification = await adapter.VerifyWebhookAsync(context, cancellationToken);

        if (!verification.IsValid)
        {
            return request.HttpMethod == "GET"
                ? new WebhookResult(403, "verification.failed")
                : new WebhookResult(401, "signature.invalid");
        }

        if (request.HttpMethod == "GET")
        {
            // Subscription verification: echo the challenge as plain text.
            return new WebhookResult(200, verification.Challenge ?? string.Empty);
        }

        var bodyHash = Convert.ToHexString(SHA256.HashData(request.Body)).ToLowerInvariant();

        if (await inbox.ExistsByBodyHashAsync(channel.Id.Value, bodyHash, cancellationToken))
        {
            // Duplicate provider delivery: acknowledge without a second row.
            return new WebhookResult(200, "duplicate");
        }

        var inboxEvent = InboxEvent.Create(
            channel.TenantId,
            channel.Id.Value,
            channel.Type,
            externalEventId: null,
            Encoding.UTF8.GetString(request.Body),
            bodyHash,
            ComputeHeadersFingerprint(request.Headers),
            clock.UtcNow);

        await inbox.AddAsync(inboxEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WebhookResult(200, "ok");
    }

    private static string ComputeHeadersFingerprint(IReadOnlyDictionary<string, string> headers)
    {
        var canonical = string.Join(
            "\n",
            headers
                .Where(pair => !pair.Key.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key.ToLowerInvariant()}:{pair.Value}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
