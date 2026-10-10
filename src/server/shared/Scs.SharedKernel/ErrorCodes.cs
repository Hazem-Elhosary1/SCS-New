namespace Scs.SharedKernel;

/// <summary>
/// Stable error codes returned in the <c>code</c> field of every failure
/// (specs/001-platform-foundation/contracts/conventions.md §4). Clients branch on these.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    public static IReadOnlyList<string> All { get; } =
    [
        ValidationFailed, MalformedRequest, Unauthenticated, Forbidden, NotFound,
        PayloadTooLarge, RateLimited, InternalError, ServiceUnavailable,
    ];
}
