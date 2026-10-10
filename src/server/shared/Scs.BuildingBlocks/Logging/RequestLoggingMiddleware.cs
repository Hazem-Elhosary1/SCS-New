using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Tenancy;
using Serilog.Context;

namespace Scs.BuildingBlocks.Logging;

/// <summary>
/// Writes one entry per request: method, route template, status and duration. It deliberately
/// records the route <em>template</em> and nothing else about the request, so path values, the
/// query string, headers and bodies never reach the logs (FR-021).
/// </summary>
public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        // Routing has already run; read the endpoint now because error handling clears it.
        var matched = context.GetEndpoint() as RouteEndpoint;
        try
        {
            await next(context);
        }
        finally
        {
            var route = (matched ?? context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            var durationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // The clinic the request was done for, if one was established; an identifier only.
            var clinicId = (context.RequestServices.GetService(typeof(IClinicContext)) as IClinicContext)?.ClinicId;
            using (clinicId is { } id ? LogContext.PushProperty("clinicId", id) : null)
            {
                LogCompleted(logger, context.Request.Method, route, context.Response.StatusCode, durationMs);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "HTTP {method} {route} responded {status} in {durationMs} ms")]
    private static partial void LogCompleted(
        ILogger logger, string method, string route, int status, long durationMs);
}
