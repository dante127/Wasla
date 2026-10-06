using Microsoft.AspNetCore.Http;
using Wasla.BuildingBlocks.Application.Security;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure;

namespace Wasla.BuildingBlocks.Web;

/// <summary>
/// Establishes the tenant context for the request from the authenticated token's tenant claim.
/// Must run after authentication and before authorization.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst(WaslaClaimTypes.TenantId)?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                tenantContext.Resolve(new TenantId(tenantId));
            }
        }

        await next(context);
    }
}
