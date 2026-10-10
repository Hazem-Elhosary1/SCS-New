using Microsoft.Extensions.Options;

namespace Scs.BuildingBlocks.Configuration;

/// <summary>
/// Stops start-up when a required value is missing or invalid. Messages name the environment
/// variable and never contain a configured value (FR-017).
/// </summary>
public sealed class ScsOptionsValidator : IValidateOptions<ScsOptions>
{
    private static readonly string[] Environments = [ScsOptions.Local, ScsOptions.Test, ScsOptions.Production];
    private static readonly string[] Roles = ["api", "worker", "all"];

    public ValidateOptionsResult Validate(string? name, ScsOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Environment))
        {
            failures.Add("Missing required configuration value SCS_ENVIRONMENT.");
        }
        else if (!Environments.Contains(options.Environment, StringComparer.Ordinal))
        {
            failures.Add("SCS_ENVIRONMENT must be one of: local, test, production.");
        }

        if (!Roles.Contains(options.Role, StringComparer.Ordinal))
        {
            failures.Add("SCS_ROLE must be one of: api, worker, all.");
        }

        if (string.IsNullOrWhiteSpace(options.Db.Connection))
        {
            failures.Add("Missing required configuration value SCS_DB__CONNECTION.");
        }

        if (string.IsNullOrWhiteSpace(options.Db.ReportConnection))
        {
            failures.Add("Missing required configuration value SCS_DB__REPORT_CONNECTION.");
        }

        if (string.IsNullOrWhiteSpace(options.Db.CrossingConnection))
        {
            failures.Add("Missing required configuration value SCS_DB__CROSSING_CONNECTION.");
        }

        if (options.Retention.CrossClinicAccessDays < RetentionOptions.MinimumDays)
        {
            failures.Add($"SCS_RETENTION__CROSS_CLINIC_ACCESS_DAYS must be at least {RetentionOptions.MinimumDays}.");
        }

        ValidateAuth(options.Auth, failures);

        if (string.IsNullOrWhiteSpace(options.Mail.Host))
        {
            failures.Add("Missing required configuration value SCS_MAIL__HOST.");
        }

        if (string.IsNullOrWhiteSpace(options.Mail.From))
        {
            failures.Add("Missing required configuration value SCS_MAIL__FROM.");
        }

        if (options.Mail.Port is < 1 or > 65535)
        {
            failures.Add("SCS_MAIL__PORT must be between 1 and 65535.");
        }

        Range(options.Mail.TimeoutSeconds, 1, 60, "SCS_MAIL__TIMEOUT_SECONDS", failures);

        if (!options.IsLocal && options.Cors.OriginList.Length > 0)
        {
            failures.Add("SCS_CORS__ORIGINS must be empty outside the local environment.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateAuth(AuthOptions auth, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(auth.CodeKey))
        {
            failures.Add("Missing required configuration value SCS_AUTH__CODE_KEY.");
        }
        else if (auth.CodeKeyBytes().Length < 32)
        {
            failures.Add("SCS_AUTH__CODE_KEY must be base64 of at least 32 bytes.");
        }

        Range(auth.MaxFailedAttempts, 3, 10, "SCS_AUTH__MAX_FAILED_ATTEMPTS", failures);
        Range(auth.AttemptWindowMinutes, 1, 1440, "SCS_AUTH__ATTEMPT_WINDOW_MINUTES", failures);
        Range(auth.LockMinutes, 1, 1440, "SCS_AUTH__LOCK_MINUTES", failures);
        Range(auth.CodeMinutes, 1, 30, "SCS_AUTH__CODE_MINUTES", failures);
        Range(auth.CodeAttempts, 3, 10, "SCS_AUTH__CODE_ATTEMPTS", failures);
        Range(auth.MaxSpentCodes, 1, 10, "SCS_AUTH__MAX_SPENT_CODES", failures);
        Range(auth.ResendSeconds, 15, 600, "SCS_AUTH__RESEND_SECONDS", failures);
        Range(auth.MaxResends, 0, 10, "SCS_AUTH__MAX_RESENDS", failures);
        Range(auth.SessionHours, 1, 168, "SCS_AUTH__SESSION_HOURS", failures);
        Range(auth.AccessMinutes, 1, 30, "SCS_AUTH__ACCESS_MINUTES", failures);
        Range(auth.RenewalGraceSeconds, 5, 120, "SCS_AUTH__RENEWAL_GRACE_SECONDS", failures);
        Range(auth.RequestsPerMinute, 5, 120, "SCS_AUTH__REQUESTS_PER_MINUTE", failures);
        Range(auth.RenewalsPerMinute, 30, 1200, "SCS_AUTH__RENEWALS_PER_MINUTE", failures);

        var path = auth.CookiePathBase;
        if (path.Length > 0 && (!path.StartsWith('/') || path.EndsWith('/')))
        {
            failures.Add("SCS_AUTH__COOKIE_PATH_BASE must be empty, or start with a slash and not end with one.");
        }
    }

    private static void Range(int value, int minimum, int maximum, string variable, List<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{variable} must be between {minimum} and {maximum}.");
        }
    }
}
