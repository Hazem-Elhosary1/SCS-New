using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// The connection hand-written report queries use. It opens as the read-only report login and
/// already carries the established clinic, so a query written by hand is isolated exactly as
/// one written through the ORM, and cannot write. It refuses to open with no clinic.
/// </summary>
public interface IClinicConnection
{
    Task<DbConnection> OpenAsync(CancellationToken cancellation);
}

internal sealed class ClinicConnection(IOptions<ScsOptions> options, IClinicContext clinic) : IClinicConnection
{
    public async Task<DbConnection> OpenAsync(CancellationToken cancellation)
    {
        var clinicId = clinic.Require();

        var connection = new SqlConnection(options.Value.Db.ReportConnection);
        try
        {
            await connection.OpenAsync(cancellation).ConfigureAwait(false);

            var command = ClinicSession.CreateCommand(connection, clinicId);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

public static class TenancyServiceExtensions
{
    /// <summary>The clinic of the current request or job, and the report connection.</summary>
    public static IServiceCollection AddScsTenancy(this IServiceCollection services)
    {
        services.AddScoped<IClinicContext, ClinicContext>();
        services.AddScoped<IClinicConnection, ClinicConnection>();
        return services;
    }
}
