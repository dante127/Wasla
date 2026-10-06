using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Web;

namespace Wasla.Api.Endpoints;

/// <summary>Maps application errors to ProblemDetails responses.</summary>
public static class ApiResults
{
    public static IResult Problem(HttpContext httpContext, Error error) =>
        Results.Problem(WaslaProblemDetails.FromError(error, StatusCodeFor(error.Code), httpContext));

    public static int StatusCodeFor(string code)
    {
        if (code is "auth.invalid_credentials" or "auth.refresh_token_invalid" or "auth.unauthorized")
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (code.EndsWith(".not_found", StringComparison.Ordinal)
            || code.EndsWith("_not_found", StringComparison.Ordinal))
        {
            return StatusCodes.Status404NotFound;
        }

        if (code.EndsWith(".conflict", StringComparison.Ordinal)
            || code.EndsWith("_conflict", StringComparison.Ordinal))
        {
            return StatusCodes.Status409Conflict;
        }

        if (code == "media.too_large")
        {
            return StatusCodes.Status413PayloadTooLarge;
        }

        return StatusCodes.Status400BadRequest;
    }
}