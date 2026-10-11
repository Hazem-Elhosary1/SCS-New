using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Notifications.Contracts;
using Scs.Notifications.Domain;
using Scs.Notifications.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.Notifications.Infrastructure;

/// <summary>
/// The clinic's email switches (spec 008, FR-037 to FR-041): one for each event whose email a
/// clinic may turn off, on until it is turned off. A switch is for the whole clinic and never
/// touches the notification inside the console.
/// </summary>
internal sealed class EmailSwitches(
    NotificationsDbContext db, IClinicContext clinic, ICurrentStaff staff, TimeProvider clock) : IEmailSwitches
{
    public const string On = "on";
    public const string Off = "off";

    public async Task<IReadOnlyList<EmailSwitchView>> ListAsync(string language, CancellationToken cancellation)
    {
        clinic.Require();

        var changed = await db.Switches.AsNoTracking()
            .ToDictionaryAsync(s => s.EventKey, s => s.Enabled, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);

        return [.. NotificationEvents.Switchable.Select(known => View(known, changed.GetValueOrDefault(known.Key, true), language))];
    }

    public async Task<EmailSwitchView> SetAsync(string eventKey, bool enabled, string language, CancellationToken cancellation)
    {
        clinic.Require();

        var known = NotificationEvents.Find(eventKey)
                    ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

        if (known.Email != NotificationEmail.Switch)
        {
            // Urgent events are always emailed and clinical ones never: neither has a switch.
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["event"] = ["NOT_SWITCHABLE"] });
        }

        var row = await db.Switches.SingleOrDefaultAsync(s => s.EventKey == known.Key, cancellation).ConfigureAwait(false);
        var before = row?.Enabled ?? true;

        if (before != enabled)
        {
            var by = staff.IsSignedIn ? staff.AccountId : (Guid?)null;
            if (row is null)
            {
                db.Switches.Add(new EmailSwitch(known.Key, enabled, by, clock.GetUtcNow()));
            }
            else
            {
                row.Set(enabled, by, clock.GetUtcNow());
            }

            db.Record(new AuditRecord(
                AuditActions.SettingsNotificationEmailChanged,
                new AuditTarget(AuditTarget.NotificationEvent, null, known.Key),
                Changes: [AuditChange.Of("enabled", before ? On : Off, enabled ? On : Off)]));

            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        return View(known, enabled, language);
    }

    private static EmailSwitchView View(NotificationEvent known, bool enabled, string language) =>
        new(known.Key, Words.Language(language) == Words.English ? known.NameEn : known.NameAr, known.Group, enabled);
}
