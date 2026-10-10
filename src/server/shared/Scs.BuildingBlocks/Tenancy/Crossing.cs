using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// Hands out the operator connection, the only way the application works across clinics. It
/// never sets a clinic: the login it uses is a member of the role the isolation function
/// honours. Only the types listed in <see cref="DeclaredCrossings"/> may use it, directly or
/// through a context built on it; the architecture tests fail for anything else.
/// </summary>
public interface ICrossClinicConnectionFactory
{
    void Configure(DbContextOptionsBuilder builder);
}

public sealed class CrossClinicConnectionFactory : ICrossClinicConnectionFactory
{
    private readonly string _connection;

    public CrossClinicConnectionFactory(IOptions<ScsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connection = options.Value.Db.CrossingConnection;
    }

    /// <summary>For tools that run outside the application host (the migrator's seed command).</summary>
    public CrossClinicConnectionFactory(string crossingConnection) => _connection = crossingConnection;

    public void Configure(DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSqlServer(_connection);
    }
}

/// <summary>
/// The one list of operations allowed to cross the clinic boundary (spec 002, FR-025, FR-026).
/// Adding a line is a reviewed decision. The architecture tests check that the types named
/// here, and no others, use the operator connection.
/// </summary>
public static class DeclaredCrossings
{
    public const string RegisterClinic = "register-clinic";
    public const string RenameClinic = "rename-clinic";
    public const string ListClinics = "list-clinics";
    public const string ListClinicIdsForJobs = "list-clinic-ids-for-jobs";
    public const string PurgeAccessRecords = "purge-access-records";

    /// <summary>Operation, the type that performs it, whether each use is recorded, and why it exists.</summary>
    public static IReadOnlyList<(string Operation, string Type, bool Recorded, string Reason)> All { get; } =
    [
        (RegisterClinic, "Scs.Tenancy.Infrastructure.ClinicRegistry", true,
            "An operator creates a clinic; no clinic exists yet to work on behalf of."),
        (RenameClinic, "Scs.Tenancy.Infrastructure.ClinicRegistry", true,
            "Only Heta's support team sets a clinic's short name (SH-003)."),
        (ListClinics, "Scs.Tenancy.Infrastructure.ClinicRegistry", true,
            "Operators see every clinic."),
        (ListClinicIdsForJobs, "Scs.Tenancy.Infrastructure.ClinicRegistry", false,
            "A per-clinic job needs the ids to take clinics one by one. Identifiers only; logged, not recorded."),
        (PurgeAccessRecords, "Scs.Tenancy.Infrastructure.PurgeAccessRecordsJob", false,
            "Access records older than the retention period are removed (FR-032). Logged with the count."),
    ];

    /// <summary>The distinct types that may use the operator connection.</summary>
    public static IReadOnlyCollection<string> Types { get; } = [.. All.Select(entry => entry.Type).Distinct()];
}
