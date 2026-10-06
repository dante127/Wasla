using Wasla.Tenancy.Application;

namespace Wasla.Api.Endpoints;

public static class TenancyEndpoints
{
    public static void MapTenancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy");

        group.MapGet("/current", async (
            GetCurrentTenantHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization();
    }
}
