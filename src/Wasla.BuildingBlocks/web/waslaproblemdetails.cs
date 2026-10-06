using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wasla.BuildingBlocks.Application;

namespace Wasla.BuildingBlocks.Web;

/// <summary>Builds RFC 9457 problem details from application errors.</summary>
public static class WaslaProblemDetails
{
    public const string ErrorTypeBaseUri = "https://wasla.app/errors/";

    public static ProblemDetails FromError(Error error, int statusCode, HttpContext? httpContext = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var problemDetails = new ProblemDetails
        {
            Type = $"{ErrorTypeBaseUri}{error.Code}",
            Title = error.Message,
            Status = statusCode,
        };

        if (httpContext is not null)
        {
            problemDetails.Instance = httpContext.Request.Path;
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        }

        return problemDetails;
    }
}
