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
        ValidateConsoleAddress(options.Auth.StaffAppUrl, "SCS_AUTH__STAFF_APP_URL", options, failures);

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

        Range(options.Ops.IdleMinutes, 15, 10_080, "SCS_OPS__IDLE_MINUTES", failures);
        Range(
            options.Subscriptions.TrialDays, SubscriptionsOptions.MinTrialDays, SubscriptionsOptions.MaxTrialDays,
            "SCS_SUBSCRIPTIONS__TRIAL_DAYS", failures);
        Range(options.Ops.ResetLinkMinutes, 5, 1_440, "SCS_OPS__RESET_LINK_MINUTES", failures);
        Range(options.Ops.FirstLinkHours, 1, 168, "SCS_OPS__FIRST_LINK_HOURS", failures);
        Range(options.Ops.RecordMonths, 1, 120, "SCS_OPS__RECORD_MONTHS", failures);

        ValidateConsoleAddress(options.Ops.AppUrl, "SCS_OPS__APP_URL", options, failures);

        foreach (var (value, key) in new[]
                 {
                     (options.Platform.NameAr, "SCS_PLATFORM__NAME_AR"),
                     (options.Platform.NameEn, "SCS_PLATFORM__NAME_EN"),
                     (options.Platform.SupportEmail, "SCS_PLATFORM__SUPPORT_EMAIL"),
                 })
        {
            if (!options.IsLocal && string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"Missing required configuration value {key}.");
            }
        }

        TooLong(options.Platform.NameAr, PlatformOptions.NameMaxLength, "SCS_PLATFORM__NAME_AR", failures);
        TooLong(options.Platform.NameEn, PlatformOptions.NameMaxLength, "SCS_PLATFORM__NAME_EN", failures);
        TooLong(options.Platform.SupportPhone, PlatformOptions.PhoneMaxLength, "SCS_PLATFORM__SUPPORT_PHONE", failures);
        TooLong(options.Platform.AddressAr, PlatformOptions.AddressMaxLength, "SCS_PLATFORM__ADDRESS_AR", failures);
        TooLong(options.Platform.AddressEn, PlatformOptions.AddressMaxLength, "SCS_PLATFORM__ADDRESS_EN", failures);

        if (!string.IsNullOrWhiteSpace(options.Platform.SupportEmail)
            && !MimeKit.MailboxAddress.TryParse(options.Platform.SupportEmail.Trim(), out _))
        {
            failures.Add("SCS_PLATFORM__SUPPORT_EMAIL must be an email address.");
        }

        if (!options.IsLocal && string.IsNullOrWhiteSpace(options.Files.Root))
        {
            failures.Add("Missing required configuration value SCS_FILES__ROOT.");
        }

        if (!string.IsNullOrWhiteSpace(options.Files.Root) && !Path.IsPathFullyQualified(options.Files.Root.Trim()))
        {
            failures.Add("SCS_FILES__ROOT must be a full path, outside anything the web server serves.");
        }

        if (!options.IsLocal && !options.Scan.HasScanner)
        {
            failures.Add("Missing required configuration value SCS_SCAN__HOST: files are not accepted without a malware scanner.");
        }

        if (options.Scan.Port is < 1 or > 65535)
        {
            failures.Add("SCS_SCAN__PORT must be between 1 and 65535.");
        }

        Range(options.Scan.TimeoutSeconds, 5, 120, "SCS_SCAN__TIMEOUT_SECONDS", failures);

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

        Range(auth.ResetLinkMinutes, 5, 1440, "SCS_AUTH__RESET_LINK_MINUTES", failures);
        Range(auth.InviteLinkHours, 1, 336, "SCS_AUTH__INVITE_LINK_HOURS", failures);
        Range(auth.TempPasswordHours, 1, 336, "SCS_AUTH__TEMP_PASSWORD_HOURS", failures);
        Range(auth.ResetEmailGapSeconds, 15, 3600, "SCS_AUTH__RESET_EMAIL_GAP_SECONDS", failures);
        Range(auth.ResetEmailsPerHour, 1, 60, "SCS_AUTH__RESET_EMAILS_PER_HOUR", failures);
        Range(auth.GeneratedPasswordLength, 12, 64, "SCS_AUTH__GENERATED_PASSWORD_LENGTH", failures);

        var path = auth.CookiePathBase;
        if (path.Length > 0 && (!path.StartsWith('/') || path.EndsWith('/')))
        {
            failures.Add("SCS_AUTH__COOKIE_PATH_BASE must be empty, or start with a slash and not end with one.");
        }
    }

    /// <summary>
    /// Links in emails lead to the two consoles, and a link that carries a secret is never sent
    /// over plain HTTP: an address must be HTTPS, or — on a developer's machine only — plain HTTP
    /// on that machine itself. This is the rule an email's button keeps (spec 094), held here so
    /// that a configuration that starts can always send its emails.
    /// </summary>
    private static void ValidateConsoleAddress(string address, string variable, ScsOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            if (!options.IsLocal)
            {
                failures.Add($"Missing required configuration value {variable}.");
            }

            return;
        }

        var wellFormed =
            Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.AbsolutePath == "/"
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.UserInfo.Length == 0;

        if (!wellFormed)
        {
            failures.Add($"{variable} must be an absolute http(s) address with no path.");
        }
        else if (!options.IsLocal && uri!.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{variable} must use https outside the local environment.");
        }
        else if (!ConsoleAddresses.MayBeLinkedTo(uri!))
        {
            failures.Add($"{variable} must use https, or http on this machine itself (localhost): an email's link is not sent to a plain http address elsewhere.");
        }
    }

    private static void TooLong(string value, int maximum, string variable, List<string> failures)
    {
        if (value.Trim().Length > maximum)
        {
            failures.Add($"{variable} must be at most {maximum} characters.");
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

/// <summary>Where an email's link may lead, said once for the configuration and for the email.</summary>
public static class ConsoleAddresses
{
    /// <summary>HTTPS anywhere; plain HTTP only on the machine itself.</summary>
    public static bool MayBeLinkedTo(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsAbsoluteUri
               && (address.Scheme == Uri.UriSchemeHttps || (address.Scheme == Uri.UriSchemeHttp && address.IsLoopback));
    }
}
