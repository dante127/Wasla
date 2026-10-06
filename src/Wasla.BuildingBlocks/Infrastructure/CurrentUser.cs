using Microsoft.AspNetCore.Http;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Security;

namespace Wasla.BuildingBlocks.Infrastructure;

/// <summary>Ambient authenticated user resolved from the current HTTP request, if any.</summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User?.FindFirst(WaslaClaimTypes.Subject)?.Value, out var id)
            ? id
            : null;

    public string? DisplayName =>
        httpContextAccessor.HttpContext?.User?.FindFirst(WaslaClaimTypes.Name)?.Value;
}
