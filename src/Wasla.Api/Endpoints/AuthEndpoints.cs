using Wasla.Api.Validation;
using Wasla.Identity.Application.Auth;

namespace Wasla.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (
            LoginRequest request,
            LoginHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await handler.HandleAsync(request, cancellationToken);

            return outcome switch
            {
                LoginOutcome.Success success => Results.Ok(success.Response),

                LoginOutcome.SelectionRequired selection => Results.Problem(
                    title: "Select a membership",
                    detail: "This account belongs to multiple tenants. Sign in again with a membershipId.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://wasla.app/errors/membership-selection-required",
                    extensions: new Dictionary<string, object?> { ["memberships"] = selection.Memberships }),

                LoginOutcome.Failure failure => ApiResults.Problem(httpContext, failure.Error),

                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        })
        .AddEndpointFilter<ValidationFilter<LoginRequest>>()
        .RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
            RefreshRequest request,
            RefreshHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
        .RequireRateLimiting("auth");

        group.MapPost("/logout", async (
            RefreshRequest request,
            LogoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(request, cancellationToken);

            return Results.NoContent();
        })
        .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
        .RequireRateLimiting("auth");

        group.MapGet("/me", async (
            MeHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization();
    }
}
