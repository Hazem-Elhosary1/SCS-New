using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Security;
using Scs.SharedKernel.Files;

namespace Scs.BuildingBlocks.Files;

/// <summary>The engine's table, owned by the Files module and written from here (spec 009).</summary>
public static class FilesSql
{
    public const string Schema = "files";
    public const string StoredFile = "StoredFile";
    public const string RetentionPeriod = "RetentionPeriod";

    public const int NameMaxLength = 255;

    public const string Pending = "pending";
    public const string Clean = "clean";
    public const string Refused = "refused";

    public const string Infected = "infected";
    public const string Unreadable = "unreadable";

    public const string ErasedForRemoved = "removed";
    public const string ErasedForAge = "age";
    public const string ErasedForRefused = "refused";

    /// <summary>How long a removed file can still be restored (FR-030a).</summary>
    public static readonly TimeSpan RestoreFor = TimeSpan.FromDays(30);

    internal const string Table = $"[{Schema}].[{StoredFile}]";
}

/// <summary>What a module asked for on its context, to be done by its next save.</summary>
internal abstract record FileChange
{
    internal sealed record Attach(string Kind, FileRecord Record, IReadOnlyList<Guid> FileIds) : FileChange;

    internal sealed record Remove(string Kind, Guid FileId) : FileChange;

    internal sealed record Restore(string Kind, Guid FileId) : FileChange;

    internal sealed record StartRetention(FileRecord Record, DateTimeOffset From) : FileChange;

    internal sealed record StopRetention(FileRecord Record) : FileChange;

    internal sealed record Hold(FileRecord Record) : FileChange;

    internal sealed record Release(FileRecord Record) : FileChange;
}

/// <summary>
/// Attaches, removes and restores a record's files on the connection and inside the transaction
/// of the save that names them, so a record and its files take effect together or not at all
/// (spec 009, FR-007). What is refused throws, and the save fails with it.
/// </summary>
internal static class FileWriter
{
    public static async Task WriteAsync(
        DbContext db, Guid clinicId, IReadOnlyList<FileChange> changes, DateTimeOffset now, CancellationToken cancellation)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction()
                          ?? throw FileRefusedException.NotAttachable("A record's files change only inside the transaction of its save.");

        var services = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
        var catalogue = services?.GetService<IFileKindCatalogue>() ?? new FileKindCatalogue();
        var actor = Actor(services);
        var connection = db.Database.GetDbConnection();

        foreach (var change in changes)
        {
            switch (change)
            {
                case FileChange.Attach attach:
                    await AttachAsync(connection, transaction, catalogue, clinicId, actor, attach, now, cancellation).ConfigureAwait(false);
                    break;
                case FileChange.Remove remove:
                    await RemoveAsync(connection, transaction, clinicId, actor, remove, now, cancellation).ConfigureAwait(false);
                    break;
                case FileChange.Restore restore:
                    await RestoreAsync(connection, transaction, catalogue, clinicId, actor, restore, now, cancellation).ConfigureAwait(false);
                    break;
                case FileChange.StartRetention start:
                    await OnRecordAsync(
                        connection, transaction, clinicId, start.Record, "RetainFromUtc = @value", start.From, null, cancellation)
                        .ConfigureAwait(false);
                    break;
                case FileChange.StopRetention stop:
                    await OnRecordAsync(connection, transaction, clinicId, stop.Record, "RetainFromUtc = NULL", null, null, cancellation)
                        .ConfigureAwait(false);
                    break;
                case FileChange.Hold hold:
                    await OnRecordAsync(
                        connection, transaction, clinicId, hold.Record,
                        "HeldAtUtc = @value, HeldByAccountId = @actor, ReleasedAtUtc = NULL, ReleasedByAccountId = NULL",
                        now, actor, cancellation, "AND HeldAtUtc IS NULL").ConfigureAwait(false);
                    break;
                case FileChange.Release release:
                    await OnRecordAsync(
                        connection, transaction, clinicId, release.Record,
                        "HeldAtUtc = NULL, HeldByAccountId = NULL, ReleasedAtUtc = @value, ReleasedByAccountId = @actor",
                        now, actor, cancellation, "AND HeldAtUtc IS NOT NULL").ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>On a request, whoever is signed in; in a piece of work, whoever it says it works for.</summary>
    private static Guid? Actor(IServiceProvider? services)
    {
        var http = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        return http is not null
            ? Guid.TryParse(http.User.FindFirstValue(StaffClaims.AccountId), out var signedIn) ? signedIn : null
            : services?.GetService<AuditActorScope>()?.AccountId;
    }

    private static async Task AttachAsync(
        DbConnection connection,
        DbTransaction transaction,
        IFileKindCatalogue catalogue,
        Guid clinicId,
        Guid? actor,
        FileChange.Attach attach,
        DateTimeOffset now,
        CancellationToken cancellation)
    {
        var kind = catalogue.Find(attach.Kind)
                   ?? throw FileRefusedException.NotAttachable($"'{attach.Kind}' is not a kind of the table of files.");
        Check(attach.Record);

        var ids = attach.FileIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        if (actor is not { } uploader)
        {
            throw FileRefusedException.NotAttachable("A file is attached by the person who uploaded it, and nobody is signed in.");
        }

        // The files, locked until the save ends: each must be this person's, of this kind and
        // unattached. Any other answer is said alike. A file the check has already refused is
        // attached too, and reads as refused on its record: what the person sees does not depend
        // on whether the scan or the save came first.
        var found = 0;
        var refused = 0;
        var command = Command(
            connection, transaction,
            $"""
             SELECT f.Kind, f.UploadedByAccountId, f.RecordId, CASE WHEN f.Scan = '{FilesSql.Refused}' THEN 1 ELSE 0 END
             FROM {FilesSql.Table} f WITH (UPDLOCK, HOLDLOCK)
             WHERE f.ClinicId = @clinic AND f.Id IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON(@ids));
             """);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(command, "@ids", SqlDbType.NVarChar, JsonSerializer.Serialize(ids));

            var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    found++;
                    var theirs = reader.GetGuid(1) == uploader;
                    var free = await reader.IsDBNullAsync(2, cancellation).ConfigureAwait(false);
                    if (reader.GetString(0) != kind.Key || !theirs || !free)
                    {
                        throw FileRefusedException.NotAttachable("A file named for this record cannot be attached to it.");
                    }

                    refused += reader.GetInt32(3);
                }
            }
        }

        if (found != ids.Count)
        {
            throw FileRefusedException.NotAttachable("A file named for this record cannot be attached to it.");
        }

        var (held, onRecord, retainFrom) = await RecordStateAsync(connection, transaction, clinicId, attach.Record, cancellation)
            .ConfigureAwait(false);
        if (kind.MaxPerRecord is { } max && onRecord + ids.Count - refused > max)
        {
            throw FileRefusedException.CountReached(max);
        }

        // A file that joins a record on hold, or one whose retention period has started, shares both.
        var update = Command(
            connection, transaction,
            $"""
             UPDATE {FilesSql.Table}
             SET RecordKind = @recordKind, RecordId = @recordId, AttachedAtUtc = @at,
                 HeldAtUtc = CASE WHEN @held = 1 THEN @at ELSE NULL END,
                 HeldByAccountId = CASE WHEN @held = 1 THEN @actor ELSE NULL END,
                 RetainFromUtc = @retainFrom
             WHERE ClinicId = @clinic AND Id IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON(@ids));
             """);
        await using (update.ConfigureAwait(false))
        {
            Add(update, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(update, "@ids", SqlDbType.NVarChar, JsonSerializer.Serialize(ids));
            Add(update, "@recordKind", SqlDbType.VarChar, attach.Record.Kind);
            Add(update, "@recordId", SqlDbType.UniqueIdentifier, attach.Record.Id);
            Add(update, "@at", SqlDbType.DateTimeOffset, now);
            Add(update, "@held", SqlDbType.Bit, held);
            Add(update, "@actor", SqlDbType.UniqueIdentifier, uploader);
            Add(update, "@retainFrom", SqlDbType.DateTimeOffset, retainFrom);
            await update.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
        }
    }

    private static async Task RemoveAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid clinicId,
        Guid? actor,
        FileChange.Remove remove,
        DateTimeOffset now,
        CancellationToken cancellation)
    {
        var command = Command(
            connection, transaction,
            $"""
             UPDATE {FilesSql.Table}
             SET RemovedAtUtc = @at, RemovedByAccountId = @actor
             WHERE ClinicId = @clinic AND Id = @id AND Kind = @kind AND RecordId IS NOT NULL AND RemovedAtUtc IS NULL;
             """);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(command, "@id", SqlDbType.UniqueIdentifier, remove.FileId);
            Add(command, "@kind", SqlDbType.VarChar, remove.Kind);
            Add(command, "@at", SqlDbType.DateTimeOffset, now);
            Add(command, "@actor", SqlDbType.UniqueIdentifier, actor);

            if (await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false) != 1)
            {
                throw FileRefusedException.NotAttachable("There is no such file on a record of this kind to remove.");
            }
        }
    }

    private static async Task RestoreAsync(
        DbConnection connection,
        DbTransaction transaction,
        IFileKindCatalogue catalogue,
        Guid clinicId,
        Guid? actor,
        FileChange.Restore restore,
        DateTimeOffset now,
        CancellationToken cancellation)
    {
        var kind = catalogue.Find(restore.Kind)
                   ?? throw FileRefusedException.NotAttachable($"'{restore.Kind}' is not a kind of the table of files.");

        FileRecord record;
        var read = Command(
            connection, transaction,
            $"""
             SELECT f.RecordKind, f.RecordId, f.RemovedAtUtc, f.ErasedAtUtc
             FROM {FilesSql.Table} f WITH (UPDLOCK, HOLDLOCK)
             WHERE f.ClinicId = @clinic AND f.Id = @id AND f.Kind = @kind AND f.RecordId IS NOT NULL AND f.RemovedAtUtc IS NOT NULL;
             """);
        await using (read.ConfigureAwait(false))
        {
            Add(read, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(read, "@id", SqlDbType.UniqueIdentifier, restore.FileId);
            Add(read, "@kind", SqlDbType.VarChar, restore.Kind);

            var reader = await read.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    throw FileRefusedException.NotAttachable("There is no such removed file on a record of this kind to restore.");
                }

                var erased = !await reader.IsDBNullAsync(3, cancellation).ConfigureAwait(false);
                var removedAt = await reader.GetFieldValueAsync<DateTimeOffset>(2, cancellation).ConfigureAwait(false);
                if (erased || now - removedAt > FilesSql.RestoreFor)
                {
                    throw FileRefusedException.NotKept("A removed file is restored within thirty days, and this one is past them.");
                }

                record = new FileRecord(reader.GetString(0), reader.GetGuid(1));
            }
        }

        var (_, onRecord, _) = await RecordStateAsync(connection, transaction, clinicId, record, cancellation).ConfigureAwait(false);
        if (kind.MaxPerRecord is { } max && onRecord + 1 > max)
        {
            throw FileRefusedException.CountReached(max);
        }

        var update = Command(
            connection, transaction,
            $"""
             UPDATE {FilesSql.Table}
             SET RemovedAtUtc = NULL, RemovedByAccountId = NULL, RestoredAtUtc = @at, RestoredByAccountId = @actor
             WHERE ClinicId = @clinic AND Id = @id;
             """);
        await using (update.ConfigureAwait(false))
        {
            Add(update, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(update, "@id", SqlDbType.UniqueIdentifier, restore.FileId);
            Add(update, "@at", SqlDbType.DateTimeOffset, now);
            Add(update, "@actor", SqlDbType.UniqueIdentifier, actor);
            await update.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether the record is on hold, how many files it holds now — a removed file and one the
    /// check refused take no place — and when its retention period started.
    /// </summary>
    private static async Task<(bool Held, int Count, DateTimeOffset? RetainFrom)> RecordStateAsync(
        DbConnection connection, DbTransaction transaction, Guid clinicId, FileRecord record, CancellationToken cancellation)
    {
        var command = Command(
            connection, transaction,
            $"""
             SELECT COUNT(CASE WHEN f.RemovedAtUtc IS NULL AND f.Scan <> '{FilesSql.Refused}' THEN 1 END),
                    MAX(CASE WHEN f.HeldAtUtc IS NOT NULL THEN 1 ELSE 0 END),
                    MAX(f.RetainFromUtc)
             FROM {FilesSql.Table} f WITH (UPDLOCK, HOLDLOCK)
             WHERE f.ClinicId = @clinic AND f.RecordKind = @recordKind AND f.RecordId = @recordId;
             """);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(command, "@recordKind", SqlDbType.VarChar, record.Kind);
            Add(command, "@recordId", SqlDbType.UniqueIdentifier, record.Id);

            var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await reader.ReadAsync(cancellation).ConfigureAwait(false);
                var held = !await reader.IsDBNullAsync(1, cancellation).ConfigureAwait(false) && reader.GetInt32(1) == 1;
                DateTimeOffset? from = await reader.IsDBNullAsync(2, cancellation).ConfigureAwait(false)
                    ? null
                    : await reader.GetFieldValueAsync<DateTimeOffset>(2, cancellation).ConfigureAwait(false);
                return (held, reader.GetInt32(0), from);
            }
        }
    }

    private static async Task OnRecordAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid clinicId,
        FileRecord record,
        string set,
        DateTimeOffset? value,
        Guid? actor,
        CancellationToken cancellation,
        string and = "")
    {
        Check(record);
        var command = Command(
            connection, transaction,
            $"UPDATE {FilesSql.Table} SET {set} WHERE ClinicId = @clinic AND RecordKind = @recordKind AND RecordId = @recordId {and};");
        await using (command.ConfigureAwait(false))
        {
            Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
            Add(command, "@recordKind", SqlDbType.VarChar, record.Kind);
            Add(command, "@recordId", SqlDbType.UniqueIdentifier, record.Id);
            Add(command, "@value", SqlDbType.DateTimeOffset, value);
            Add(command, "@actor", SqlDbType.UniqueIdentifier, actor);
            await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
        }
    }

    private static void Check(FileRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Kind) || record.Kind.Length > FileRecord.KindMaxLength || record.Id == Guid.Empty)
        {
            throw FileRefusedException.NotAttachable("A file is attached to a record named by a kind and an id.");
        }
    }

    private static DbCommand Command(DbConnection connection, DbTransaction transaction, string text)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        return command;
    }

    private static void Add(DbCommand command, string name, SqlDbType type, object? value) =>
        command.Parameters.Add(new SqlParameter(name, type) { Value = value ?? DBNull.Value });
}
