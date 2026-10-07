using Wasla.Analytics.Application;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

/// <summary>Analytics reports (permission: report.read) plus an on-demand recompute trigger.</summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics").WithTags("Analytics");

        group.MapGet("/overview", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            GetAnalyticsOverviewHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(from, to, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Reports.Read));

        group.MapGet("/agents", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            GetAnalyticsAgentReportHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(from, to, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Reports.Read));

        group.MapGet("/channels", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            GetAnalyticsChannelReportHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(from, to, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Reports.Read));

        group.MapPost("/recompute", async (
            RecomputeAnalyticsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var recomputed = await handler.HandleAsync(cancellationToken);

            return Results.Ok(new { recomputedConversations = recomputed });
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Reports.Read));
    }
}
