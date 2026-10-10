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

public sealed class ApiDocsOptions
{
    /// <summary><c>SCS_APIDOCS__ENABLED</c>: ignored when the environment is production.</summary>
    public bool Enabled { get; set; }
}
