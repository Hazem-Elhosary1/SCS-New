namespace Scs.Platform.Contracts;

/// <summary>
/// An unhandled front-end failure, as posted to <c>/v1/client-errors</c>
/// (contracts/platform-api.openapi.yaml, <c>ClientError</c>).
/// </summary>
public sealed record ClientErrorReport(
    string App,
    string Version,
    string Message,
    string? Route = null,
    string? Stack = null,
    string? TraceId = null,
    string? Language = null)
{
    public const int MaxBodyBytes = 16 * 1024;
    public const int RequestsPerMinute = 30;

    public const int VersionMaxLength = 40;
    public const int RouteMaxLength = 200;
    public const int MessageMaxLength = 1000;
    public const int StackMaxLength = 8000;

    public static IReadOnlyList<string> Apps { get; } = ["clinic", "patient", "ops"];

    public static IReadOnlyList<string> Languages { get; } = ["ar", "en"];
}
