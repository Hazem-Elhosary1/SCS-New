using Microsoft.Extensions.Configuration;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Configuration;

/// <summary>
/// Everything the platform reads from its environment. Bound from the <c>Scs</c> section, which
/// is filled from <c>SCS_*</c> environment variables (see <see cref="ScsConfigurationExtensions"/>).
/// Keys are listed in specs/001-platform-foundation/data-model.md.
/// </summary>
public sealed class ScsOptions
{
    public const string SectionName = "Scs";

    public const string Local = "local";
    public const string Test = "test";
    public const string Production = "production";

    /// <summary><c>SCS_ENVIRONMENT</c>: local, test or production.</summary>
    public string Environment { get; set; } = "";

    /// <summary><c>SCS_ROLE</c>: api, worker or all.</summary>
    public string Role { get; set; } = "all";

    public DatabaseOptions Db { get; set; } = new();

    public CorsOptions Cors { get; set; } = new();

    public OtelOptions Otel { get; set; } = new();

    public ApiDocsOptions ApiDocs { get; set; } = new();

    public RetentionOptions Retention { get; set; } = new();

    public AuthOptions Auth { get; set; } = new();

    public MailOptions Mail { get; set; } = new();

    public SupportOptions Support { get; set; } = new();

    public SeedOptions Seed { get; set; } = new();

    /// <summary><c>SCS_VERSION</c>, set at build.</summary>
    public string Version { get; set; } = "0.0.0-dev";

    /// <summary><c>SCS_COMMIT</c>, set at build.</summary>
    public string Commit { get; set; } = new('0', 40);

    /// <summary><c>SCS_BUILT_AT</c>, set at build.</summary>
    [ConfigurationKeyName("BUILT_AT")]
    public DateTimeOffset BuiltAt { get; set; } = DateTimeOffset.UnixEpoch;

    public bool IsLocal => Environment == Local;

    public bool IsProduction => Environment == Production;

    public bool RunsWorker => Role is "worker" or "all";

    /// <summary>The <c>service</c> field of log entries.</summary>
    public string ServiceName => Role == "worker" ? "worker" : "api";
}

public sealed class DatabaseOptions
{
    /// <summary><c>SCS_DB__CONNECTION</c>: application login, no structure rights.</summary>
    [Secret]
    public string Connection { get; set; } = "";

    /// <summary>
    /// <c>SCS_DB__REPORT_CONNECTION</c>: read-only login for hand-written report queries.
    /// Subject to clinic isolation like the application login.
    /// </summary>
    [Secret]
    [ConfigurationKeyName("REPORT_CONNECTION")]
    public string ReportConnection { get; set; } = "";

    /// <summary>
    /// <c>SCS_DB__CROSSING_CONNECTION</c>: operator login, used only by the declared
    /// cross-clinic operations.
    /// </summary>
    [Secret]
    [ConfigurationKeyName("CROSSING_CONNECTION")]
    public string CrossingConnection { get; set; } = "";
}

public sealed class CorsOptions
{
    /// <summary>
    /// <c>SCS_CORS__ORIGINS</c>: comma-separated origins. Allowed in local only; test and
    /// production call the API on the same origin.
    /// </summary>
    public string Origins { get; set; } = "";

    public string[] OriginList =>
        Origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class OtelOptions
{
    /// <summary><c>SCS_OTEL__ENDPOINT</c>: OTLP collector address; nothing is exported when empty.</summary>
    public string Endpoint { get; set; } = "";
}

/// <summary>
/// How long each kind of record is kept. Platform settings (SH-095), held as configuration until
/// the operators' settings screen exists (spec 024).
/// </summary>
public sealed class RetentionOptions
{
    public const int MinimumDays = 30;

    /// <summary>
    /// <c>SCS_RETENTION__CROSS_CLINIC_ACCESS_DAYS</c>: how long cross-clinic access records are
    /// kept. Five years to start; the lawyer's answer may change it.
    /// </summary>
    [ConfigurationKeyName("CROSS_CLINIC_ACCESS_DAYS")]
    public int CrossClinicAccessDays { get; set; } = 1825;
}

public sealed class ApiDocsOptions
{
    /// <summary><c>SCS_APIDOCS__ENABLED</c>: ignored when the environment is production.</summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Staff sign-in and sessions (spec 003). Platform settings held as configuration until the
/// settings screen of the operators console exists (spec 024). Limits are in
/// <see cref="ScsOptionsValidator"/>.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>
    /// <c>SCS_AUTH__CODE_KEY</c>: base64 of at least 32 bytes. Keys the hashes of sign-in codes
    /// and of emails that match no account.
    /// </summary>
    [Secret]
    [ConfigurationKeyName("CODE_KEY")]
    public string CodeKey { get; set; } = "";

    /// <summary><c>SCS_AUTH__MAX_FAILED_ATTEMPTS</c>: wrong passwords before the lock.</summary>
    [ConfigurationKeyName("MAX_FAILED_ATTEMPTS")]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary><c>SCS_AUTH__ATTEMPT_WINDOW_MINUTES</c>: wrong attempts are forgotten after this long without a new one.</summary>
    [ConfigurationKeyName("ATTEMPT_WINDOW_MINUTES")]
    public int AttemptWindowMinutes { get; set; } = 15;

    /// <summary><c>SCS_AUTH__LOCK_MINUTES</c>: how long a lock lasts.</summary>
    [ConfigurationKeyName("LOCK_MINUTES")]
    public int LockMinutes { get; set; } = 15;

    /// <summary><c>SCS_AUTH__CODE_MINUTES</c>: life of a sign-in code.</summary>
    [ConfigurationKeyName("CODE_MINUTES")]
    public int CodeMinutes { get; set; } = 10;

    /// <summary><c>SCS_AUTH__CODE_ATTEMPTS</c>: tries before a code is spent.</summary>
    [ConfigurationKeyName("CODE_ATTEMPTS")]
    public int CodeAttempts { get; set; } = 5;

    /// <summary><c>SCS_AUTH__MAX_SPENT_CODES</c>: codes spent in a row before the lock.</summary>
    [ConfigurationKeyName("MAX_SPENT_CODES")]
    public int MaxSpentCodes { get; set; } = 3;

    /// <summary><c>SCS_AUTH__RESEND_SECONDS</c>: the countdown before a code may be sent again.</summary>
    [ConfigurationKeyName("RESEND_SECONDS")]
    public int ResendSeconds { get; set; } = 60;

    /// <summary><c>SCS_AUTH__MAX_RESENDS</c>: codes that may be resent in one sign-in.</summary>
    [ConfigurationKeyName("MAX_RESENDS")]
    public int MaxResends { get; set; } = 3;

    /// <summary><c>SCS_AUTH__SESSION_HOURS</c>: the longest a session lives, however active.</summary>
    [ConfigurationKeyName("SESSION_HOURS")]
    public int SessionHours { get; set; } = 12;

    /// <summary><c>SCS_AUTH__ACCESS_MINUTES</c>: life of the access cookie between renewals.</summary>
    [ConfigurationKeyName("ACCESS_MINUTES")]
    public int AccessMinutes { get; set; } = 5;

    /// <summary><c>SCS_AUTH__RENEWAL_GRACE_SECONDS</c>: how long a replaced renewal secret is still honoured.</summary>
    [ConfigurationKeyName("RENEWAL_GRACE_SECONDS")]
    public int RenewalGraceSeconds { get; set; } = 30;

    /// <summary><c>SCS_AUTH__REQUESTS_PER_MINUTE</c>: sign-in requests per network address (SH-041).</summary>
    [ConfigurationKeyName("REQUESTS_PER_MINUTE")]
    public int RequestsPerMinute { get; set; } = 10;

    /// <summary><c>SCS_AUTH__RENEWALS_PER_MINUTE</c>: session renewals per network address.</summary>
    [ConfigurationKeyName("RENEWALS_PER_MINUTE")]
    public int RenewalsPerMinute { get; set; } = 120;

    /// <summary><c>SCS_AUTH__EMAIL_CODE_ENABLED</c>: the second step, for the whole platform (FR-022).</summary>
    [ConfigurationKeyName("EMAIL_CODE_ENABLED")]
    public bool EmailCodeEnabled { get; set; } = true;

    /// <summary>
    /// <c>SCS_AUTH__COOKIE_PATH_BASE</c>: the path the API is served under. Empty locally,
    /// <c>/api</c> when deployed.
    /// </summary>
    [ConfigurationKeyName("COOKIE_PATH_BASE")]
    public string CookiePathBase { get; set; } = "";

    /// <summary><c>SCS_AUTH__RESET_LINK_MINUTES</c>: life of a password reset link (SH-048).</summary>
    [ConfigurationKeyName("RESET_LINK_MINUTES")]
    public int ResetLinkMinutes { get; set; } = 30;

    /// <summary><c>SCS_AUTH__INVITE_LINK_HOURS</c>: life of a new-account link (SH-048).</summary>
    [ConfigurationKeyName("INVITE_LINK_HOURS")]
    public int InviteLinkHours { get; set; } = 72;

    /// <summary><c>SCS_AUTH__TEMP_PASSWORD_HOURS</c>: how long a password chosen by someone else is accepted at sign-in.</summary>
    [ConfigurationKeyName("TEMP_PASSWORD_HOURS")]
    public int TempPasswordHours { get; set; } = 72;

    /// <summary><c>SCS_AUTH__RESET_EMAIL_GAP_SECONDS</c>: the least time between two reset emails to one account.</summary>
    [ConfigurationKeyName("RESET_EMAIL_GAP_SECONDS")]
    public int ResetEmailGapSeconds { get; set; } = 60;

    /// <summary><c>SCS_AUTH__RESET_EMAILS_PER_HOUR</c>: reset emails to one account in any hour.</summary>
    [ConfigurationKeyName("RESET_EMAILS_PER_HOUR")]
    public int ResetEmailsPerHour { get; set; } = 5;

    /// <summary><c>SCS_AUTH__GENERATED_PASSWORD_LENGTH</c>: length of a password the system generates.</summary>
    [ConfigurationKeyName("GENERATED_PASSWORD_LENGTH")]
    public int GeneratedPasswordLength { get; set; } = 14;

    /// <summary>
    /// <c>SCS_AUTH__STAFF_APP_URL</c>: the staff app's address, for links in emails. No path.
    /// Required outside the local environment, where it falls back to the dev server's address.
    /// </summary>
    [ConfigurationKeyName("STAFF_APP_URL")]
    public string StaffAppUrl { get; set; } = "";

    public const string LocalStaffAppUrl = "http://localhost:4201";

    /// <summary>The staff app's address without a trailing slash.</summary>
    public string StaffAppBase() =>
        string.IsNullOrWhiteSpace(StaffAppUrl) ? LocalStaffAppUrl : StaffAppUrl.Trim().TrimEnd('/');

    public byte[] CodeKeyBytes()
    {
        try
        {
            return Convert.FromBase64String(CodeKey);
        }
        catch (FormatException)
        {
            return [];
        }
    }
}

/// <summary>Outgoing mail (spec 003; templates and events arrive with spec 008).</summary>
public sealed class MailOptions
{
    /// <summary><c>SCS_MAIL__HOST</c></summary>
    public string Host { get; set; } = "";

    /// <summary><c>SCS_MAIL__PORT</c></summary>
    public int Port { get; set; } = 587;

    /// <summary><c>SCS_MAIL__USE_TLS</c></summary>
    [ConfigurationKeyName("USE_TLS")]
    public bool UseTls { get; set; } = true;

    /// <summary><c>SCS_MAIL__USER</c>: empty when the server needs no sign-in (the local mail catcher).</summary>
    public string User { get; set; } = "";

    /// <summary><c>SCS_MAIL__PASSWORD</c></summary>
    [Secret]
    public string Password { get; set; } = "";

    /// <summary><c>SCS_MAIL__FROM</c></summary>
    public string From { get; set; } = "";

    /// <summary><c>SCS_MAIL__TIMEOUT_SECONDS</c>: how long a request waits for the mail server.</summary>
    [ConfigurationKeyName("TIMEOUT_SECONDS")]
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>What the help window on the sign-in screens shows. Nothing here is private.</summary>
public sealed class SupportOptions
{
    [ConfigurationKeyName("HOURS_AR")]
    public string HoursAr { get; set; } = "";

    [ConfigurationKeyName("HOURS_EN")]
    public string HoursEn { get; set; } = "";

    public string Hotline { get; set; } = "";

    public string Whatsapp { get; set; } = "";

    public string Email { get; set; } = "";
}

/// <summary>Made-up data for local and test environments only.</summary>
public sealed class SeedOptions
{
    /// <summary><c>SCS_SEED__STAFF_PASSWORD</c>: the one password of the seeded staff accounts.</summary>
    [Secret]
    [ConfigurationKeyName("STAFF_PASSWORD")]
    public string StaffPassword { get; set; } = "";
}
