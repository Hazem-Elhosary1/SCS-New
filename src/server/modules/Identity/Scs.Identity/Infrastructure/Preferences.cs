using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>What a person keeps, as the screen reads it. A banner counts only on the day it was closed.</summary>
internal sealed record PreferencesView(string? Language, bool? SidebarFolded, string? DismissedBanner)
{
    public static PreferencesView Nothing { get; } = new(null, null, null);
}

/// <summary>
/// Reads and saves a person's own preferences (spec 006, FR-017). Every call names the account
/// of the session; there is no way to ask for another's.
/// </summary>
internal sealed class Preferences(IdentityDbContext db, TimeProvider clock)
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>Today's date in Cairo, where every clinic's day begins and ends (SH-001).</summary>
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Cairo).DateTime);

    public async Task<PreferencesView> ReadAsync(Guid accountId, CancellationToken cancellation)
    {
        var row = await db.Preferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.AccountId == accountId, cancellation).ConfigureAwait(false);

        return row is null ? PreferencesView.Nothing : View(row);
    }

    /// <summary>Changes only what is given; the rest stays as it was.</summary>
    public async Task<PreferencesView> SaveAsync(
        Guid accountId, string? language, bool? sidebarFolded, string? dismissedBanner, CancellationToken cancellation)
    {
        var row = await db.Preferences.SingleOrDefaultAsync(p => p.AccountId == accountId, cancellation).ConfigureAwait(false);
        if (row is null)
        {
            row = new StaffPreference(accountId);
            db.Preferences.Add(row);
        }

        row.Choose(language, sidebarFolded, dismissedBanner, Today, clock.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (failure.IsUniqueViolation())
        {
            // Two tabs made the person's first choice at the same moment: the other one made
            // the row; this choice goes onto it.
            db.ChangeTracker.Clear();
            row = await db.Preferences.SingleAsync(p => p.AccountId == accountId, cancellation).ConfigureAwait(false);
            row.Choose(language, sidebarFolded, dismissedBanner, Today, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        return View(row);
    }

    private PreferencesView View(StaffPreference row) => new(row.Language, row.SidebarFolded, row.BannerClosedOn(Today));
}
