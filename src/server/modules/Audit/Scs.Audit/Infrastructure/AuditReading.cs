using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scs.Audit.Contracts;
using Scs.Audit.Domain;
using Scs.Audit.Infrastructure.Persistence;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.SharedKernel.Audit;

namespace Scs.Audit.Infrastructure;

/// <summary>A question that passed its checks: the period is whole and every facet exists.</summary>
internal sealed record AuditWindow(DateTimeOffset From, DateTimeOffset To, string Language, IReadOnlyList<string>? NamedActions);

/// <summary>What the two readings share: checking the question, narrowing, counting and paging.</summary>
internal static class AuditPages
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The refusals of FR-032 and FR-033, as field codes.</summary>
    public static AuditWindow Check(AuditQuery query, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var to = query.To ?? now;
        var from = query.From ?? to.AddDays(-AuditQuery.DefaultDays);

        if (from > to)
        {
            errors["period"] = ["INVALID"];
        }
        else if (to - from > TimeSpan.FromDays(AuditQuery.MaxDays))
        {
            errors["period"] = ["TOO_LONG"];
        }

        if (query.Category is not null && !AuditCategories.All.Contains(query.Category, StringComparer.Ordinal))
        {
            errors["category"] = ["INVALID"];
        }

        if (query.Action is not null && AuditActions.Find(query.Action) is null)
        {
            errors["action"] = ["INVALID"];
        }

        if (query.Page < 1)
        {
            errors["page"] = ["INVALID"];
        }

        if (query.Size is < 1 or > AuditQuery.MaxSize)
        {
            errors["size"] = ["INVALID"];
        }

        var search = query.Search?.Trim();
        if (search?.Length > AuditQuery.SearchMaxLength)
        {
            errors["q"] = ["TOO_LONG"];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var language = query.Language == Sentences.English ? Sentences.English : Sentences.Arabic;

        // The actions whose name, as this reader reads it, contains what was typed.
        IReadOnlyList<string>? named = string.IsNullOrEmpty(search)
            ? null
            : [.. AuditActions.All
                .Where(action => Sentences.NameOf(action, language).Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(action => action.Code)];

        return new AuditWindow(from, to, language, named);
    }

    /// <summary>The period and every facet except the category.</summary>
    public static IQueryable<Entry> Narrow(IQueryable<Entry> entries, AuditQuery query, AuditWindow window)
    {
        entries = entries.Where(e => e.OccurredAtUtc >= window.From && e.OccurredAtUtc <= window.To);

        if (query.Actor is { } actor)
        {
            entries = entries.Where(e => e.ActorAccountId == actor);
        }

        if (query.Action is { } action)
        {
            entries = entries.Where(e => e.ActionCode == action);
        }

        if (window.NamedActions is { } named)
        {
            var search = query.Search!.Trim();
            entries = entries.Where(e => (e.ActorName != null && e.ActorName.Contains(search)) || named.Contains(e.ActionCode));
        }

        return entries;
    }

    public static async Task<AuditCounts> CountAsync(IQueryable<Entry> narrowed, CancellationToken cancellation)
    {
        var perCategory = await narrowed
            .GroupBy(e => e.Category)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Category, row => row.Count, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);

        int Of(string category) => perCategory.GetValueOrDefault(category);

        return new AuditCounts(
            perCategory.Values.Sum(),
            Of(AuditCategories.Security),
            Of(AuditCategories.Changes),
            Of(AuditCategories.Records),
            Of(AuditCategories.Users));
    }

    /// <summary>Newest first; entries of the same moment keep one order, so no page repeats or skips one.</summary>
    public static async Task<List<Entry>> PageAsync(
        IQueryable<Entry> narrowed, AuditQuery query, CancellationToken cancellation)
    {
        if (query.Category is { } category)
        {
            narrowed = narrowed.Where(e => e.Category == category);
        }

        return await narrowed
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.Size)
            .Take(query.Size)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);
    }

    public static int TotalOf(AuditCounts counts, string? category) => category switch
    {
        AuditCategories.Security => counts.Security,
        AuditCategories.Changes => counts.Changes,
        AuditCategories.Records => counts.Records,
        AuditCategories.Users => counts.Users,
        _ => counts.All,
    };

    /// <summary>One entry as words (FR-036).</summary>
    /// <param name="names">The accounts that exist, by id; null when they cannot be asked about.</param>
    public static AuditEntryView Words(
        Entry entry, string language, IReadOnlyDictionary<Guid, string>? names, FailedRun? run)
    {
        var action = AuditActions.Find(entry.ActionCode);
        var changes = entry.Changes is null
            ? []
            : JsonSerializer.Deserialize<List<AuditChange>>(entry.Changes, Json) ?? [];

        var target = Target(entry, language, names);
        var isRun = entry.ActionCode == AuditActions.SignInFailedRun;

        return new AuditEntryView(
            entry.Id,
            entry.OccurredAtUtc,
            entry.Category,
            new AuditActionView(entry.ActionCode, action is null ? entry.ActionCode : Sentences.NameOf(action, language)),
            action is null
                ? ""
                : Sentences.Render(
                    action,
                    new SentenceParts(target?.Name, changes, run?.Attempts ?? 1, run?.Locked ?? false, run?.LockMinutes ?? 0),
                    language),
            entry.ActorAccountId is null
                ? new AuditActorView(false, null, Sentences.UnknownActor(language))
                : new AuditActorView(true, entry.ActorAccountId, entry.ActorName),
            target,
            entry.IpAddress,
            DeviceText.Describe(entry.UserAgent, language),
            isRun ? run?.Attempts ?? 1 : null,
            isRun ? run?.Locked ?? false : null);
    }

    private static AuditTargetView? Target(Entry entry, string language, IReadOnlyDictionary<Guid, string>? names)
    {
        if (entry.TargetKind is null)
        {
            return null;
        }

        if (entry.TargetKind != AuditTarget.Account)
        {
            var name = entry.TargetKind == AuditTarget.Role
                ? Sentences.RoleName(entry.TargetName, entry.TargetReadyKey, language)
                : entry.TargetName;
            return new AuditTargetView(entry.TargetKind, entry.TargetId, name, Exists: true);
        }

        // An account is stored by reference alone (FR-018); it is named as it is called today.
        if (names is null)
        {
            return new AuditTargetView(entry.TargetKind, entry.TargetId, null, Exists: true);
        }

        return entry.TargetId is { } id && names.TryGetValue(id, out var found)
            ? new AuditTargetView(entry.TargetKind, id, found, Exists: true)
            : new AuditTargetView(entry.TargetKind, entry.TargetId, Sentences.DeletedAccount(language), Exists: false);
    }
}

/// <summary>
/// A clinic's reading of its own trail (spec 007, FR-030): what its people did and the sign-in
/// entries on its accounts. What the platform or an operator did is never returned. Under the
/// clinic's isolation, like every read.
/// </summary>
internal sealed class AuditReading(
    AuditDbContext db, IClinicContext clinic, IStaffDirectory directory, TimeProvider clock) : IClinicAuditReading
{
    public async Task<AuditPage> ReadAsync(AuditQuery query, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(query);
        clinic.Require();

        var window = AuditPages.Check(query, clock.GetUtcNow());
        var narrowed = AuditPages.Narrow(
            db.Entries.AsNoTracking().Where(e => e.ActorKind == AuditSql.ActorStaff), query, window);

        var counts = await AuditPages.CountAsync(narrowed, cancellation).ConfigureAwait(false);
        var entries = await AuditPages.PageAsync(narrowed, query, cancellation).ConfigureAwait(false);

        var accounts = entries
            .Where(e => e.TargetKind == AuditTarget.Account && e.TargetId is not null)
            .Select(e => e.TargetId!.Value)
            .Distinct()
            .ToList();
        var names = await directory.NamesAsync(accounts, cancellation).ConfigureAwait(false);
        var runs = await RunsAsync(entries, cancellation).ConfigureAwait(false);

        return new AuditPage(
            window.From,
            window.To,
            AuditPages.TotalOf(counts, query.Category),
            counts,
            query.Page,
            query.Size,
            [.. entries.Select(entry => AuditPages.Words(entry, window.Language, names, runs.GetValueOrDefault(entry.Id)))]);
    }

    /// <summary>
    /// How each run of failed attempts on the page went. An entry cannot be changed, so the
    /// number of attempts and the lock are not on it: Identity counts them, from the run's first
    /// request to the account's next run.
    /// </summary>
    private async Task<Dictionary<Guid, FailedRun>> RunsAsync(List<Entry> entries, CancellationToken cancellation)
    {
        var asked = new List<(Guid EntryId, FailedRunRef Run)>();

        foreach (var entry in entries.Where(e => e.ActionCode == AuditActions.SignInFailedRun && e.TargetId is not null))
        {
            var next = await db.Entries.AsNoTracking()
                .Where(e => e.ActionCode == AuditActions.SignInFailedRun
                            && e.TargetKind == AuditTarget.Account
                            && e.TargetId == entry.TargetId
                            && e.OccurredAtUtc > entry.OccurredAtUtc)
                .OrderBy(e => e.OccurredAtUtc)
                .Select(e => e.TraceId)
                .FirstOrDefaultAsync(cancellation)
                .ConfigureAwait(false);

            asked.Add((entry.Id, new FailedRunRef(entry.TargetId!.Value, entry.TraceId, next)));
        }

        if (asked.Count == 0)
        {
            return [];
        }

        var answers = await directory.FailedRunsAsync([.. asked.Select(a => a.Run)], cancellation).ConfigureAwait(false);
        var found = new Dictionary<Guid, FailedRun>();

        foreach (var (entryId, run) in asked)
        {
            if (answers.FirstOrDefault(a => a.AccountId == run.AccountId && a.TraceId == run.TraceId) is { } answer)
            {
                found[entryId] = answer;
            }
        }

        return found;
    }
}

/// <summary>
/// The operators' reading (FR-031): what operators did, across clinics or in one. It crosses the
/// clinic boundary, on the operator connection, and is declared as such. No route reaches it
/// until the operators' console exists (specs 023 and 025).
/// </summary>
internal sealed class OperatorAuditReading(ICrossClinicConnectionFactory crossing, TimeProvider clock) : IOperatorAuditReading
{
    public async Task<AuditPage> ReadAsync(AuditQuery query, Guid? clinicId, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(query);

        var window = AuditPages.Check(query, clock.GetUtcNow());

        var builder = new DbContextOptionsBuilder<AuditDbContext>();
        crossing.Configure(builder);
        var db = new AuditDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            // No clinic is established here: the application's own filter would match nothing.
            var all = db.Entries.AsNoTracking().IgnoreQueryFilters().Where(e => e.ActorKind == AuditSql.ActorOperator);
            if (clinicId is { } one)
            {
                all = all.Where(e => e.ClinicId == one);
            }

            var narrowed = AuditPages.Narrow(all, query, window);
            var counts = await AuditPages.CountAsync(narrowed, cancellation).ConfigureAwait(false);
            var entries = await AuditPages.PageAsync(narrowed, query, cancellation).ConfigureAwait(false);

            return new AuditPage(
                window.From,
                window.To,
                AuditPages.TotalOf(counts, query.Category),
                counts,
                query.Page,
                query.Size,
                [.. entries.Select(entry => AuditPages.Words(entry, window.Language, names: null, run: null))]);
        }
    }
}
