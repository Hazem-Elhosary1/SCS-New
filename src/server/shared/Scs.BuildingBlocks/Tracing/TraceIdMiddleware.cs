using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Scs.BuildingBlocks.Tracing;

/// <summary>
/// Gives every request its reference: the W3C trace id. An incoming <c>traceparent</c> is
/// continued; otherwise one is started. The id is returned in <c>X-Trace-Id</c> and attached to
/// every log entry the request causes (FR-020).
/// </summary>
public sealed partial class TraceIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Trace-Id";
    private const string ItemKey = "scs.traceId";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Resolve(context);
        context.Items[ItemKey] = traceId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("traceId", traceId))
        {
            await next(context);
        }
    }

    /// <summary>The reference of the current request; a fresh one if the middleware has not run.</summary>
    public static string Get(HttpContext context) =>
        context.Items[ItemKey] as string ?? ActivityTraceId.CreateRandom().ToHexString();

    private static string Resolve(HttpContext context)
    {
        var header = context.Request.Headers.TraceParent.ToString();
        var match = TraceParent().Match(header);
        if (match.Success && match.Groups[1].Value != new string('0', 32))
        {
            return match.Groups[1].Value;
        }

        var current = Activity.Current;
        return current is not null && current.TraceId != default
            ? current.TraceId.ToHexString()
            : ActivityTraceId.CreateRandom().ToHexString();
    }

    [GeneratedRegex("^[0-9a-f]{2}-([0-9a-f]{32})-[0-9a-f]{16}-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}
