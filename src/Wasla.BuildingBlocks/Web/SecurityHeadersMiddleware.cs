using Microsoft.AspNetCore.Http;

namespace Wasla.BuildingBlocks.Web;

/// <summary>
/// Baseline security response headers (hardening phase). Applied to every response —
/// including error paths — as the outermost middleware.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-site";

        await next(context);
    }
}
