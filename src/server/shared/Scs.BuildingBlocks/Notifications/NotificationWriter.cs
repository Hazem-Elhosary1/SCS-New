using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;

namespace Scs.BuildingBlocks.Notifications;

/// <summary>What an event is about: the record, and the identifier a person reads for it.</summary>
/// <param name="Label">Such as <c>T-1046</c>; null when the record has none a person reads (an account).</param>
public sealed record EventSubject(string Kind, Guid Id, string? Label = null)
{
    public const string Account = "account";
}

/// <summary>
/// What a caller says when something happened that people should be told (spec 008, FR-005):
/// which event, about which record, the facts its line needs, and — where the event's rule
/// names an owner — who that is. Who is told beyond that, by which channel, the title and the
/// time are the engine's and cannot be supplied.
/// </summary>
/// <param name="Occasion">
/// What makes a repeat distinct, for anything that may be raised twice for one happening — a
/// reminder's day-count. With an occasion, a recipient already told for the same event, record
/// and occasion is not told again. Empty: every raise is a happening of its own.
/// </param>
public sealed record RaisedEvent(
    string Event,
    EventSubject? Subject = null,
    IReadOnlyDictionary<string, object?>? Facts = null,
    IReadOnlyList<Guid>? Owners = null,
    string Occasion = "");

/// <summary>
/// Says who is told. Implemented by the Identity module, which owns accounts and roles; defined
/// here because the shared project may not reference a module. Answers only accounts of the
/// established clinic that can sign in, each once.
/// </summary>
public interface INotificationRecipients
{
    Task<IReadOnlyList<Guid>> ResolveAsync(RecipientRule rule, IReadOnlyCollection<Guid> owners, CancellationToken cancellation);
}

/// <summary>Told when a save left email waiting for a clinic, so it can be sent without delay.</summary>
public interface INotificationDispatch
{
    void EmailsWaiting(Guid clinicId);
}

/// <summary>The event could not be raised because of a defect in whoever raised it.</summary>
public sealed class NotificationRefusedException(string message) : InvalidOperationException(message);

/// <summary>The engine's tables, owned by the Notifications module and written from here (spec 008).</summary>
public static class NotifySql
{
    public const string Schema = "notify";
    public const string Notification = "Notification";
    public const string OutgoingEmail = "OutgoingEmail";
    public const string EmailSwitch = "EmailSwitch";

    public const int SubjectLabelMaxLength = 64;
    public const int OccasionMaxLength = 32;
    public const int LinkMaxLength = 256;
    public const int FactTextMaxLength = 200;

    public const string Waiting = "waiting";
    public const string Sent = "sent";
    public const string GaveUp = "gave-up";
    public const string Dropped = "dropped";

    /// <summary>
    /// One notification for each recipient in the list, and one email owed for each notification
    /// written when the event is emailed — always, or while the clinic has not turned its switch
    /// for the event off (a clinic with no row for the event has it on). With an occasion, a
    /// recipient already told for the same event, record and occasion is skipped.
    /// </summary>
    internal const string Insert =
        $"""
         DECLARE @written TABLE (Id uniqueidentifier NOT NULL);

         INSERT INTO [{Schema}].[{Notification}]
             (Id, ClinicId, RecipientAccountId, EventKey, Mark, SubjectKind, SubjectId, SubjectLabel,
              Occasion, Facts, Link, RaisedAtUtc)
         OUTPUT inserted.Id INTO @written
         SELECT NEWID(), @clinic, CAST(r.[value] AS uniqueidentifier), @event, @mark, @subjectKind, @subjectId, @label,
                @occasion, @facts, @link, @at
         FROM OPENJSON(@recipients) r
         WHERE @occasion = N''
            OR NOT EXISTS (
                SELECT 1 FROM [{Schema}].[{Notification}] n
                WHERE n.ClinicId = @clinic
                  AND n.RecipientAccountId = CAST(r.[value] AS uniqueidentifier)
                  AND n.EventKey = @event
                  AND n.Occasion = @occasion
                  AND (n.SubjectId = @subjectId OR (n.SubjectId IS NULL AND @subjectId IS NULL)));

         IF @switchable = 1 AND EXISTS (
                SELECT 1 FROM [{Schema}].[{EmailSwitch}] s
                WHERE s.ClinicId = @clinic AND s.EventKey = @event AND s.Enabled = 0)
             SET @email = 0;

         IF @email = 1
             INSERT INTO [{Schema}].[{OutgoingEmail}] (Id, ClinicId, NotificationId, State, Attempts, NextAttemptAtUtc, CreatedAtUtc)
             SELECT NEWID(), @clinic, w.Id, '{Waiting}', 0, @at, @at FROM @written w;

         SELECT CASE WHEN @email = 1 THEN COUNT(*) ELSE 0 END FROM @written;
         """;
}

public static class NotificationExtensions
{
    public static IServiceCollection AddScsNotifications(this IServiceCollection services)
    {
        services.TryAddSingleton<INotificationCatalogue, NotificationCatalogue>();
        return services;
    }
}

/// <summary>
/// Writes notifications on the connection and inside the transaction of the change that caused
/// them, so no notification exists of a change that did not take effect (spec 008, FR-006). No
/// email is sent here: one is written as owed, and a job sends it afterwards (FR-007).
/// </summary>
internal static class NotificationWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <returns>True when at least one email was written as owed.</returns>
    public static async Task<bool> WriteAsync(
        DbContext db, Guid clinicId, IReadOnlyList<RaisedEvent> raised, DateTimeOffset now, CancellationToken cancellation)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction()
                          ?? throw new NotificationRefusedException("An event is raised only inside the transaction of its change.");

        var services = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
        var catalogue = services?.GetService<INotificationCatalogue>() ?? new NotificationCatalogue();
        var recipients = services?.GetService<INotificationRecipients>()
                         ?? throw new NotificationRefusedException("Nothing here can say who is told: the event cannot be raised on this context.");
        var emailWritten = false;

        foreach (var one in raised)
        {
            var known = Check(one, catalogue);
            var told = await recipients.ResolveAsync(known.Recipients, one.Owners ?? [], cancellation).ConfigureAwait(false);

            // A rule that finds nobody tells nobody, and fails nothing.
            if (told.Count == 0)
            {
                continue;
            }


            var command = db.Database.GetDbConnection().CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = transaction;
                command.CommandText = NotifySql.Insert;
                Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
                Add(command, "@event", SqlDbType.VarChar, known.Key);
                Add(command, "@mark", SqlDbType.VarChar, known.Mark);
                Add(command, "@subjectKind", SqlDbType.VarChar, one.Subject?.Kind);
                Add(command, "@subjectId", SqlDbType.UniqueIdentifier, one.Subject?.Id);
                Add(command, "@label", SqlDbType.NVarChar, Cut(one.Subject?.Label, NotifySql.SubjectLabelMaxLength));
                Add(command, "@occasion", SqlDbType.NVarChar, one.Occasion);
                Add(command, "@facts", SqlDbType.NVarChar, one.Facts is { Count: > 0 } ? JsonSerializer.Serialize(one.Facts, Json) : null);
                Add(command, "@link", SqlDbType.NVarChar, Link(known, one));
                Add(command, "@at", SqlDbType.DateTimeOffset, now);
                Add(command, "@email", SqlDbType.Bit, known.Email != NotificationEmail.Never);
                Add(command, "@switchable", SqlDbType.Bit, known.Email == NotificationEmail.Switch);
                Add(command, "@recipients", SqlDbType.NVarChar, JsonSerializer.Serialize(told.Distinct()));

                var emails = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false),
                    System.Globalization.CultureInfo.InvariantCulture);
                emailWritten |= emails > 0;
            }
        }

        return emailWritten;
    }

    /// <summary>The checks of FR-004, FR-005, FR-017, FR-018 and FR-045. A failure is a defect in the caller.</summary>
    private static NotificationEvent Check(RaisedEvent one, INotificationCatalogue catalogue)
    {
        var known = catalogue.Find(one.Event)
                    ?? throw new NotificationRefusedException($"'{one.Event}' is not an event of the notification table.");

        if (!known.HasLine)
        {
            throw new NotificationRefusedException($"The event '{known.Key}' has no line yet: give it one where its cause is built.");
        }

        if (known.Recipients.NamesAnOwner && one.Owners is not { Count: > 0 })
        {
            throw new NotificationRefusedException($"The event '{known.Key}' is told to an owner, and none was named.");
        }

        if (one.Occasion.Length > NotifySql.OccasionMaxLength)
        {
            throw new NotificationRefusedException($"The occasion of '{known.Key}' is longer than {NotifySql.OccasionMaxLength} characters.");
        }

        foreach (var (key, value) in one.Facts ?? new Dictionary<string, object?>())
        {
            if (known.Clinical)
            {
                throw new NotificationRefusedException($"The event '{known.Key}' is clinical and carries no fact.");
            }

            if (!known.Facts.Contains(key, StringComparer.Ordinal)
                || AuditActions.NeverKeys.Any(never => key.Contains(never, StringComparison.OrdinalIgnoreCase)))
            {
                throw new NotificationRefusedException($"The event '{known.Key}' does not allow the fact '{key}'.");
            }

            var allowed = value is null or bool or int or long or decimal
                          || value is string { Length: <= NotifySql.FactTextMaxLength };
            if (!allowed)
            {
                throw new NotificationRefusedException($"The fact '{key}' of '{known.Key}' is not a short text, a number or a yes/no.");
            }
        }

        return known;
    }

    private static string? Link(NotificationEvent known, RaisedEvent one) =>
        known.Destination is null || one.Subject is null
            ? known.Destination
            : Cut(known.Destination.Replace("{id}", one.Subject.Id.ToString(), StringComparison.Ordinal), NotifySql.LinkMaxLength);

    private static string? Cut(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];

    private static void Add(DbCommand command, string name, SqlDbType type, object? value) =>
        command.Parameters.Add(new SqlParameter(name, type) { Value = value ?? DBNull.Value });
}
