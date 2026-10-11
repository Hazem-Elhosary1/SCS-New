using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// Base class for a module's database context. It is the application's own layer of clinic
/// isolation, on top of the database's: clinic-owned entities are filtered to the established
/// clinic, stamped with it when added, and can never be moved to another. The database remains
/// the authority; this layer exists to refuse a mistake earlier and with a clearer answer.
/// </summary>
public abstract class ClinicAwareDbContext(DbContextOptions options, IClinicContext clinic) : DbContext(options)
{
    /// <summary>Read by the query filter on every clinic-owned entity. Null matches nothing.</summary>
    protected Guid? CurrentClinicId => clinic.ClinicId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.AddInterceptors(new ClinicSessionInterceptor(clinic));
    }

    /// <summary>Call at the end of <c>OnModelCreating</c>.</summary>
    protected void ApplyClinicIsolation(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(IClinicOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(IClinicOwned.ClinicId)).IsRequired();

            // e => (Guid?)e.ClinicId == CurrentClinicId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var owner = Expression.Convert(
                Expression.Property(parameter, nameof(IClinicOwned.ClinicId)), typeof(Guid?));
            var current = Expression.Property(Expression.Constant(this), nameof(CurrentClinicId));
            entity.HasQueryFilter(Expression.Lambda(Expression.Equal(owner, current), parameter));
        }
    }

    private readonly List<AuditRecord> _audit = [];

    // Entries written in one transaction are of one act, and carry one moment.
    private (Guid Transaction, DateTimeOffset At)? _auditMoment;

    /// <summary>
    /// Says what the change being made must leave in the audit trail (spec 007). The entry is
    /// written by the next <see cref="SaveChangesAsync(bool, CancellationToken)"/>, in one
    /// transaction with the change: both take effect or neither does (FR-009).
    /// </summary>
    public void Record(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _audit.Add(record);
    }

    private readonly List<RaisedEvent> _raised = [];

    /// <summary>
    /// Says that the change being made is something people must be told (spec 008). The
    /// notifications are written by the next <see cref="SaveChangesAsync(bool, CancellationToken)"/>,
    /// in one transaction with the change: no notification exists of a change that did not take
    /// effect (FR-006). Emails are sent afterwards, never inside the save.
    /// </summary>
    public void Notify(RaisedEvent raised)
    {
        ArgumentNullException.ThrowIfNull(raised);
        _raised.Add(raised);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (_audit.Count > 0)
        {
            throw new AuditRefusedException("A change that is recorded in the audit trail is saved with SaveChangesAsync.");
        }

        if (_raised.Count > 0)
        {
            throw new NotificationRefusedException("A change people are told about is saved with SaveChangesAsync.");
        }

        EnforceClinicOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceClinicOwnership();

        if (_audit.Count == 0 && _raised.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }

        var records = _audit.ToList();
        _audit.Clear();
        var raised = _raised.ToList();
        _raised.Clear();
        var established = clinic.Require();
        var emailWaiting = false;

        var own = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
            var transaction = Database.CurrentTransaction!.TransactionId;
            if (_auditMoment?.Transaction != transaction)
            {
                _auditMoment = (transaction, AuditWriter.Now(this));
            }

            if (records.Count > 0)
            {
                await AuditWriter.WriteAsync(this, established, records, _auditMoment.Value.At, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (raised.Count > 0)
            {
                emailWaiting = await NotificationWriter
                    .WriteAsync(this, established, raised, _auditMoment.Value.At, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (own is not null)
            {
                await own.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (emailWaiting)
            {
                // Sent by a job, after the change. When the caller holds the transaction the job
                // may look before the commit and find nothing; the sweep of every minute finds it.
                this.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider?
                    .GetService<INotificationDispatch>()?.EmailsWaiting(established);
            }

            return saved;
        }
        finally
        {
            // Disposing a transaction that was not committed rolls it back: the change goes with its entry.
            if (own is not null)
            {
                await own.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void EnforceClinicOwnership()
    {
        foreach (var entry in ChangeTracker.Entries<IClinicOwned>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var established = clinic.Require();
            var owner = entry.Property(nameof(IClinicOwned.ClinicId));

            if (entry.State == EntityState.Added)
            {
                Stamp(owner, established);
            }
            else if (owner.IsModified || (Guid)owner.CurrentValue! != established)
            {
                throw Mismatch();
            }
        }
    }

    private static void Stamp(PropertyEntry owner, Guid established)
    {
        var offered = (Guid)owner.CurrentValue!;
        if (offered == Guid.Empty)
        {
            owner.CurrentValue = established;
        }
        else if (offered != established)
        {
            throw Mismatch();
        }
    }

    private static ScsProblemException Mismatch() => new(
        StatusCodes.Status400BadRequest,
        ErrorCodes.ClinicMismatch,
        "A clinic-owned record was offered for a clinic other than the one established.");
}

/// <summary>
/// Sets the clinic on every connection as it opens. The value is read-only for the life of the
/// connection, so the database itself refuses a second, different clinic; returning the
/// connection to the pool clears it. With no clinic established nothing is set, and the
/// isolation function then matches no row.
/// </summary>
public sealed class ClinicSessionInterceptor(IClinicContext clinic) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (clinic.ClinicId is { } clinicId)
        {
            using var command = ClinicSession.CreateCommand(connection, clinicId);
            command.ExecuteNonQuery();
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (clinic.ClinicId is { } clinicId)
        {
            var command = ClinicSession.CreateCommand(connection, clinicId);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>The one statement that tells the database which clinic a connection works for.</summary>
public static class ClinicSession
{
    public const string Sql = "EXEC sp_set_session_context @key = N'ClinicId', @value = @clinicId, @read_only = 1;";

    public static DbCommand CreateCommand(DbConnection connection, Guid clinicId)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var command = connection.CreateCommand();
        command.CommandText = Sql;
        command.Parameters.Add(new SqlParameter("@clinicId", System.Data.SqlDbType.UniqueIdentifier) { Value = clinicId });
        return command;
    }
}
