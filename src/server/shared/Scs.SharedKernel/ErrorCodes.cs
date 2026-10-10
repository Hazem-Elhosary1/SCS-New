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

    /// <summary>Work that needs a clinic was started without one (spec 002).</summary>
    public const string ClinicRequired = "CLINIC_REQUIRED";

    /// <summary>The address names no clinic; the same answer for every unknown name (spec 002).</summary>
    public const string ClinicNotAvailable = "CLINIC_NOT_AVAILABLE";

    /// <summary>The work carries two different clinics, or tries to change clinic (spec 002).</summary>
    public const string ClinicMismatch = "CLINIC_MISMATCH";

    public static IReadOnlyList<string> All { get; } =
    [
        ValidationFailed, MalformedRequest, Unauthenticated, Forbidden, NotFound,
        PayloadTooLarge, RateLimited, InternalError, ServiceUnavailable,
        ClinicRequired, ClinicNotAvailable, ClinicMismatch,
    ];
}
