using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Web;

namespace Wasla.Api.Endpoints;

/// <summary>Maps application errors to ProblemDetails responses.</summary>
public static class ApiResults
{
    public static IResult Problem(HttpContext httpContext, Error error) =>
        Results.Problem(WaslaProblemDetails.FromError(error, StatusCodeFor(error.Code), httpContext));

    public static int StatusCodeFor(string code) => code switch
    {
        "auth.invalid_credentials" or "auth.refresh_token_invalid" or "auth.unauthorized"
            => StatusCodes.Status401Unauthorized,
        var value when value.EndsWith(".not_found", StringComparison.Ordinal)
            => StatusCodes.Status404NotFound,
        var value when value.EndsWith(".conflict", StringComparison.Ordinal)
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
