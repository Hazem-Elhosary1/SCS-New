using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Scs.BuildingBlocks.Tracing;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Errors;

/// <summary>The one shape of every failure (contracts/conventions.md §4).</summary>
public sealed record ScsProblem(
    string Type,
    string Title,
    int Status,
    string Code,
    string TraceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>The single place that builds and writes a failure response.</summary>
public static class ProblemWriter
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ScsProblem Create(
        HttpContext context, int status, string code, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var language = ErrorMessages.ResolveLanguage(context.Request);
        context.Response.Headers.ContentLanguage = language;

        return new ScsProblem(
            Type: $"urn:scs:error:{code.ToLowerInvariant().Replace('_', '-')}",
            Title: ErrorMessages.Title(code, language),
            Status: status,
            Code: code,
            TraceId: TraceIdMiddleware.Get(context),
            Errors: errors);
    }

    public static async Task WriteAsync(
        HttpContext context, int status, string code, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = Create(context, status, code, errors);
        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, Json, context.RequestAborted);
    }

    /// <summary>The code for a status that reached the client with no body of its own.</summary>
    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        StatusCodes.Status503ServiceUnavailable => ErrorCodes.ServiceUnavailable,
        >= 400 and < 500 => ErrorCodes.MalformedRequest,
        _ => ErrorCodes.InternalError,
    };
}
