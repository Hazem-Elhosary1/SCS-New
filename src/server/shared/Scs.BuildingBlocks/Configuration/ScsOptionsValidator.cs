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

        if (!options.IsLocal && options.Cors.OriginList.Length > 0)
        {
            failures.Add("SCS_CORS__ORIGINS must be empty outside the local environment.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
