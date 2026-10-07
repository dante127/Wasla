using System.Text;
using Wasla.Channels.Application.Abstractions;

namespace Wasla.Api.Endpoints;

/// <summary>
/// Provider webhook ingress (docs/webhooks.md §1). Anonymous at the auth layer —
/// protected by per-channel signature verification and an opaque channel id; tenant
/// identity is resolved server-side from the channel. Kept on the fast path: verify,
/// persist raw, 2xx.
/// </summary>
public static class WebhookEndpoints
{
    private const int MaxPayloadBytes = 256 * 1024;

    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/webhooks").WithTags("Webhooks");

        group.MapGet("/whatsapp/{channelId:guid}", HandleAsync);
        group.MapPost("/whatsapp/{channelId:guid}", HandleAsync);
        group.MapPost("/telegram/{channelId:guid}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        Guid channelId,
        HttpContext httpContext,
        IWebhookRequestHandler handler,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(httpContext.Request.Body, cancellationToken);

        var request = new WebhookRequest(
            channelId,
            httpContext.Request.Method,
            httpContext.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString()),
            httpContext.Request.Query.ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToString()),
            body);

        var result = await handler.HandleAsync(request, cancellationToken);

        return Results.Text(
            result.Body ?? string.Empty,
            result.ContentType,
            Encoding.UTF8,
            result.StatusCode);
    }

    private static async Task<byte[]> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;

        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
            buffer.Write(chunk, 0, read);

            if (total > MaxPayloadBytes)
            {
                break; // The handler answers 413; do not buffer oversized bodies.
            }
        }

        return buffer.ToArray();
    }
}
