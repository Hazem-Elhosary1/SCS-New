using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Notifications;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel.Notifications;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The browsers an account has completed a sign-in from (spec 008, FR-042 to FR-044). A browser
/// is recognised by a mark of its own, kept in a cookie; only a hash of the mark is stored. A
/// sign-in from a browser the account has not used, when it has used another, tells its owner
/// «الدخول من جهاز جديد» — in the bell and by email, and nothing a clinic can set stops it.
/// The network plays no part.
/// </summary>
internal sealed class KnownDevices(IdentityDbContext db, TimeProvider clock)
{
    /// <summary>A device not seen for this long is forgotten; a return after it is new again.</summary>
    public static readonly TimeSpan ForgetAfter = TimeSpan.FromDays(400);

    /// <summary>
    /// Notes the browser a sign-in is completing from, on the unit of work that opens the
    /// session: the caller saves. Nothing is noted for a caller that names no browser.
    /// </summary>
    public async Task SeenAsync(Guid accountId, RequestOrigin origin, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(origin.DeviceMark))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(origin.DeviceMark));
        var known = await db.KnownDevices
            .Where(d => d.AccountId == accountId)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var same = known.FirstOrDefault(d => CryptographicOperations.FixedTimeEquals(d.MarkHash, hash));
        if (same is not null)
        {
            same.SeenAgain(now);
            return;
        }

        db.KnownDevices.Add(KnownDevice.Seen(accountId, hash, now));

        // Nothing is new against nothing: an account's first sign-in ever, and the first one
        // each existing account completes after this spec shipped, tell nobody (FR-043).
        if (known.Count == 0)
        {
            return;
        }

        db.Notify(new RaisedEvent(
            NotificationEvents.AccountNewDevice,
            new EventSubject(EventSubject.Account, accountId),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["device"] = Session.Truncate(origin.UserAgent, NotifySql.FactTextMaxLength),
            },
            Owners: [accountId]));
    }
}
