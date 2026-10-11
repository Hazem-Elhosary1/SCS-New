using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Notifications;
using Scs.Identity.Infrastructure.Persistence;
using Scs.Notifications.Infrastructure;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>One row of <c>notify.Notification</c>, exactly as it is stored, with the state of its email if one is owed.</summary>
public sealed record StoredNotification(
    Guid Id,
    Guid ClinicId,
    Guid RecipientAccountId,
    string EventKey,
    string Mark,
    string? SubjectKind,
    Guid? SubjectId,
    string? SubjectLabel,
    string Occasion,
    string? Facts,
    string? Link,
    DateTimeOffset RaisedAtUtc,
    DateTimeOffset? ReadAtUtc,
    string? EmailState,
    int? EmailAttempts)
{
    public JsonElement FactsJson => JsonDocument.Parse(Facts ?? "{}").RootElement;
}

/// <summary>
/// Events only the tests know: one for each way of finding recipients and each kind of email —
/// what a later spec adds to the table. The real events have no line until their cause is
/// built; here the four switchable ones are given one, so their switches can be exercised.
/// </summary>
public sealed class TestEvents : INotificationCatalogue
{
    public const string ToHolders = "test.to-holders";
    public const string ToOwner = "test.to-owner";
    public const string ToOwnerAndHolders = "test.to-owner-and-holders";
    public const string ToEveryone = "test.to-everyone";
    public const string NeverEmailed = "test.never-emailed";
    public const string Clinical = "test.clinical";
    public const string WithoutLine = "test.without-line";
    public const string Reminder = "test.reminder";

    /// <summary>The permission the "holders" events are told to.</summary>
    public const string Permission = Permissions.TicketsViewAll;

    private static readonly int[] None = [];

    private static readonly Dictionary<string, NotificationEvent> Added = new NotificationEvent[]
    {
        new(ToHolders, NotificationGroups.Support, "حدث للحاملين", "Event for holders", NotificationMarks.Ticket,
            RecipientRule.Holders(Permission), NotificationEmail.Always, None, ["subject", "count"],
            "التذكرة {label} · {subject}", "Ticket {label} · {subject}", "tickets/{id}"),
        new(ToOwner, NotificationGroups.Support, "حدث لصاحبه", "Event for its owner", NotificationMarks.Ticket,
            RecipientRule.Owner, NotificationEmail.Always, None, ["subject", "count"],
            "التذكرة {label} · {subject} · {count}", "Ticket {label} · {subject} · {count}", "tickets/{id}"),
        new(ToOwnerAndHolders, NotificationGroups.Support, "حدث لصاحبه وللحاملين", "Event for owner and holders",
            NotificationMarks.Ticket, RecipientRule.OwnerAnd(Permission), NotificationEmail.Always, None, [],
            "التذكرة {label}", "Ticket {label}", "tickets/{id}"),
        new(ToEveryone, NotificationGroups.Billing, "حدث للجميع", "Event for everyone", NotificationMarks.Subscription,
            RecipientRule.Everyone, NotificationEmail.Always, None, [], "للجميع", "For everyone"),
        new(NeverEmailed, NotificationGroups.Patients, "حدث داخل التطبيق فقط", "In-app only event", NotificationMarks.Notice,
            RecipientRule.Owner, NotificationEmail.Never, None, [], "داخل التطبيق", "In the app"),
        new(Clinical, NotificationGroups.Patients, "حدث طبي", "Clinical event", NotificationMarks.Warning,
            RecipientRule.Owner, NotificationEmail.Never, None, [], "السجل {label}", "Record {label}", Clinical: true),
        new(WithoutLine, NotificationGroups.Support, "حدث بلا سطر", "Event without a line", NotificationMarks.Notice,
            RecipientRule.Owner, NotificationEmail.Always, None, []),
        new(Reminder, NotificationGroups.Billing, "تذكير", "Reminder", NotificationMarks.Subscription,
            RecipientRule.Owner, NotificationEmail.Always, [14, 7, 1], ["days"], "خلال {days} يومًا", "In {days} days"),
    }.ToDictionary(e => e.Key, StringComparer.Ordinal);

    public NotificationEvent? Find(string key)
    {
        if (Added.TryGetValue(key, out var added))
        {
            return added;
        }

        var real = NotificationEvents.Find(key);
        return real is { HasLine: false, Email: NotificationEmail.Switch }
            ? real with { LineAr = "الفاتورة {label}", LineEn = "Invoice {label}", Recipients = RecipientRule.Owner }
            : real;
    }
}

/// <summary>Helpers for the notifications engine (spec 008): raising as a later spec would, and reading what is stored.</summary>
public static class Notify
{
    /// <summary>A factory whose table of events also holds <see cref="TestEvents"/>.</summary>
    public static ApiFactory WithTestEvents(this ApiFactory factory) =>
        factory.WithServices(services => services.AddSingleton<INotificationCatalogue, TestEvents>());

    /// <summary>
    /// Raises events the way a module does: on its own context, saved with its change. Returns
    /// what saving threw, if anything.
    /// </summary>
    public static Task<Exception?> RaiseAsync(this ApiFactory factory, Guid clinicId, params RaisedEvent[] raised) =>
        factory.ForClinicAsync<Exception?>(clinicId, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            foreach (var one in raised)
            {
                db.Notify(one);
            }

            return await Record.ExceptionAsync(() => db.SaveChangesAsync());
        });

    public static EventSubject Ticket(string label = "T-1046") => new("ticket", Guid.CreateVersion7(), label);

    /// <summary>Sends the clinic's waiting notification emails exactly as the job server does.</summary>
    public static async Task SendNotificationEmailsAsync(this ApiFactory factory, Guid clinicId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<NotificationEmailJob>>()
            .RunAsync(clinicId, CancellationToken.None);
    }

    /// <summary>The clinic's notifications as stored, oldest first, read with the operator login.</summary>
    public static async Task<List<StoredNotification>> NotificationsAsync(this TestDatabase database, Guid clinicId, Guid? recipient = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var connection = await TestDatabase.OpenAsync(database.Crossing);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT n.Id, n.ClinicId, n.RecipientAccountId, n.EventKey, n.Mark, n.SubjectKind, n.SubjectId, n.SubjectLabel,
                   n.Occasion, n.Facts, n.Link, n.RaisedAtUtc, n.ReadAtUtc, e.State, CAST(e.Attempts AS int)
            FROM [notify].Notification n
            LEFT JOIN [notify].OutgoingEmail e ON e.NotificationId = n.Id
            WHERE n.ClinicId = @clinic AND (@recipient IS NULL OR n.RecipientAccountId = @recipient)
            ORDER BY n.RaisedAtUtc, n.EventKey
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.Add(new SqlParameter("@recipient", System.Data.SqlDbType.UniqueIdentifier) { Value = (object?)recipient ?? DBNull.Value });

        var rows = new List<StoredNotification>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new StoredNotification(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetDateTimeOffset(11),
                reader.IsDBNull(12) ? null : reader.GetDateTimeOffset(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetInt32(14)));
        }

        return rows;
    }
}
