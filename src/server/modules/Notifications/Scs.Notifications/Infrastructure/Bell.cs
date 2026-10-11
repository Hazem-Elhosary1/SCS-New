using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.Notifications.Contracts;
using Scs.Notifications.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.Notifications.Infrastructure;

/// <summary>
/// A person's bell (spec 008, FR-020 to FR-028): their notifications of the last ninety days,
/// newest first, and their own read marks. Everything is narrowed to the account given, under
/// the clinic's isolation: another person's notification does not exist here.
/// </summary>
internal sealed class Bell(
    NotificationsDbContext db, IClinicContext clinic, INotificationCatalogue catalogue, TimeProvider clock) : IBell
{
    private DateTimeOffset Since => clock.GetUtcNow().AddDays(-NotificationPage.Days);

    public async Task<BellState> StateAsync(Guid accountId, CancellationToken cancellation)
    {
        clinic.Require();
        var since = Since;

        var unread = await db.Notifications.AsNoTracking()
            .AnyAsync(n => n.RecipientAccountId == accountId && n.ReadAtUtc == null && n.RaisedAtUtc >= since, cancellation)
            .ConfigureAwait(false);

        var latest = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientAccountId == accountId && n.RaisedAtUtc >= since)
            .OrderByDescending(n => n.RaisedAtUtc)
            .Select(n => (DateTimeOffset?)n.RaisedAtUtc)
            .FirstOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        return new BellState(unread, latest);
    }

    public async Task<NotificationPage> ListAsync(
        Guid accountId, string? before, int size, string language, CancellationToken cancellation)
    {
        clinic.Require();

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (size is < 1 or > NotificationPage.MaxSize)
        {
            errors["size"] = ["INVALID"];
        }

        (DateTimeOffset At, Guid Id)? cursor = null;
        if (!string.IsNullOrEmpty(before) && (cursor = Read(before)) is null)
        {
            errors["before"] = ["INVALID"];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var since = Since;
        var mine = db.Notifications.AsNoTracking()
            .Where(n => n.RecipientAccountId == accountId && n.RaisedAtUtc >= since);

        var page = mine;
        if (cursor is { } from)
        {
            // Older than the last one seen; entries of one moment keep one order, so no page
            // repeats or skips one.
            page = page.Where(n => n.RaisedAtUtc < from.At || (n.RaisedAtUtc == from.At && n.Id.CompareTo(from.Id) < 0));
        }

        var rows = await page
            .OrderByDescending(n => n.RaisedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(size + 1)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var unread = await mine.AnyAsync(n => n.ReadAtUtc == null, cancellation).ConfigureAwait(false);
        var shown = rows.Take(size).ToList();
        var reads = Words.Language(language);

        return new NotificationPage(
            [
                .. shown.Select(n =>
                {
                    var known = catalogue.Find(n.EventKey);
                    return new NotificationView(
                        n.Id,
                        n.Mark,
                        n.EventKey,
                        NotificationWords.Title(known, n.EventKey, reads),
                        NotificationWords.Line(known, n.SubjectLabel, n.Facts, reads),
                        n.RaisedAtUtc,
                        n.ReadAtUtc is not null,
                        n.Link);
                }),
            ],
            unread,
            rows.Count > size ? Write(shown[^1].RaisedAtUtc, shown[^1].Id) : null);
    }

    public async Task MarkReadAsync(Guid accountId, Guid notificationId, CancellationToken cancellation)
    {
        clinic.Require();
        var now = clock.GetUtcNow();

        var marked = await db.Notifications
            .Where(n => n.Id == notificationId && n.RecipientAccountId == accountId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, now), cancellation)
            .ConfigureAwait(false);

        if (marked == 0
            && !await db.Notifications.AnyAsync(n => n.Id == notificationId && n.RecipientAccountId == accountId, cancellation)
                .ConfigureAwait(false))
        {
            // Not this person's, or not at all: the same answer.
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }
    }

    public async Task MarkAllReadAsync(Guid accountId, CancellationToken cancellation)
    {
        clinic.Require();
        var now = clock.GetUtcNow();

        await db.Notifications
            .Where(n => n.RecipientAccountId == accountId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, now), cancellation)
            .ConfigureAwait(false);
    }

    private static string Write(DateTimeOffset at, Guid id) =>
        $"{at.UtcTicks.ToString(CultureInfo.InvariantCulture)}_{id:N}";

    private static (DateTimeOffset At, Guid Id)? Read(string cursor)
    {
        var parts = cursor.Split('_');
        return parts.Length == 2
               && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
               && ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks
               && Guid.TryParseExact(parts[1], "N", out var id)
            ? (new DateTimeOffset(ticks, TimeSpan.Zero), id)
            : null;
    }
}
