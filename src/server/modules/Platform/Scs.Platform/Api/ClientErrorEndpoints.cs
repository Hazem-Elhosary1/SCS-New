using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Errors;
using Scs.Platform.Contracts;
using Scs.SharedKernel;

namespace Scs.Platform.Api;

/// <summary>
/// <c>POST /v1/client-errors</c>: where the front ends report unhandled failures (FR-025).
/// Anonymous, so it is small, strict and rate-limited; what it receives is logged like any
/// other entry and therefore passes the same scrubbing.
/// </summary>
internal static partial class ClientErrorEndpoints
{
    public const string RateLimitPolicy = "client-errors";

    public static IServiceCollection AddClientErrorRateLimit(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = ClientErrorReport.RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })));

    public static IEndpointRouteBuilder MapClientErrors(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/client-errors", async (HttpContext context, ILoggerFactory loggers) =>
            {
                var report = Validate(await ReadBodyAsync(context).ConfigureAwait(false));
                Log(loggers.CreateLogger("Scs.Platform.ClientErrors"), report);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("ReportClientError");

        return v1;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        if (context.Request.ContentLength > ClientErrorReport.MaxBodyBytes)
        {
            throw TooLarge();
        }

        // Read one byte past the limit so an oversized body without a length is still caught.
        var buffer = new byte[ClientErrorReport.MaxBodyBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length &&
               (read = await context.Request.Body.ReadAsync(buffer.AsMemory(total), context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            total += read;
        }

        if (total > ClientErrorReport.MaxBodyBytes)
        {
            throw TooLarge();
        }

        try
        {
            return JsonDocument.Parse(buffer.AsMemory(0, total));
        }
        catch (JsonException)
        {
            throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest);
        }
    }

    private static ClientErrorReport Validate(JsonDocument document)
    {
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var property in root.EnumerateObject())
            {
                if (!Fields.TryGetValue(property.Name, out var field))
                {
                    errors[property.Name] = ["UNKNOWN"];
                }
                else if (property.Value.ValueKind == JsonValueKind.Null && !field.Required)
                {
                    values[property.Name] = null;
                }
                else if (property.Value.ValueKind != JsonValueKind.String)
                {
                    errors[property.Name] = ["INVALID"];
                }
                else
                {
                    var text = property.Value.GetString()!;
                    values[property.Name] = text;
                    if (text.Length > field.MaxLength)
                    {
                        errors[property.Name] = ["TOO_LONG"];
                    }
                    else if (!field.IsValid(text))
                    {
                        errors[property.Name] = ["INVALID"];
                    }
                }
            }

            foreach (var (name, field) in Fields)
            {
                if (field.Required && string.IsNullOrWhiteSpace(values.GetValueOrDefault(name)) && !errors.ContainsKey(name))
                {
                    errors[name] = ["REQUIRED"];
                }
            }

            if (errors.Count > 0)
            {
                throw ScsProblemException.Validation(errors);
            }

            return new ClientErrorReport(
                values["app"]!,
                values["version"]!,
                values["message"]!,
                values.GetValueOrDefault("route"),
                values.GetValueOrDefault("stack"),
                values.GetValueOrDefault("traceId"),
                values.GetValueOrDefault("language"));
        }
    }

    private static void Log(ILogger logger, ClientErrorReport report)
    {
        // `service` names the front end the entry came from (contracts/conventions.md §5).
        using (logger.BeginScope(new Dictionary<string, object> { ["service"] = $"web-{report.App}" }))
        {
            LogClientError(
                logger, report.App, report.Version, report.Route ?? "", report.Message,
                report.Stack ?? "", report.TraceId ?? "", report.Language ?? "");
        }
    }

    private static ScsProblemException TooLarge() =>
        new(StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge);

    private static readonly Dictionary<string, Field> Fields = new(StringComparer.Ordinal)
    {
        ["app"] = new(true, 10, ClientErrorReport.Apps.Contains),
        ["version"] = new(true, ClientErrorReport.VersionMaxLength, _ => true),
        ["message"] = new(true, ClientErrorReport.MessageMaxLength, _ => true),
        ["route"] = new(false, ClientErrorReport.RouteMaxLength, _ => true),
        ["stack"] = new(false, ClientErrorReport.StackMaxLength, _ => true),
        ["traceId"] = new(false, 32, text => TraceId().IsMatch(text)),
        ["language"] = new(false, 2, ClientErrorReport.Languages.Contains),
    };

    private sealed record Field(bool Required, int MaxLength, Func<string, bool> IsValid);

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceId();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Front-end failure in {app} {clientVersion} at {clientRoute}: {clientMessage} {clientStack} (reference {clientTraceId}, language {clientLanguage})")]
    private static partial void LogClientError(
        ILogger logger, string app, string clientVersion, string clientRoute, string clientMessage,
        string clientStack, string clientTraceId, string clientLanguage);
}
