using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.Plans.Contracts;
using Scs.Plans.Domain;
using Scs.Plans.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;

namespace Scs.Plans.Infrastructure;

/// <summary>A plan as a request sends it: its fields, the grid of what it includes, and an optional change note.</summary>
internal sealed record PlanInput(
    string? NameAr, string? NameEn, string? Description, decimal? MonthlyEgp, decimal? AnnualEgp, decimal? MonthlyUsd,
    decimal? AnnualUsd, string? Badge, string? Note, IReadOnlyList<GridRow>? Features, int? Version)
{
    public PlanFields Fields => new(NameAr, NameEn, Description, MonthlyEgp, AnnualEgp, MonthlyUsd, AnnualUsd, Badge);

    public IReadOnlyList<GridRow> Rows => Features ?? [];
}

/// <summary>A feature the plan form offers (contracts/plans-api.md §1, the grid).</summary>
internal sealed record PlanGridRow(
    string Key, string NameAr, string NameEn, string Group, string Kind, string? UnitAr, string? UnitEn, string Status, bool Retired, bool Sold);

internal sealed record BadgeHolder(Guid Id, string NameAr, string NameEn, string Badge);

internal sealed record PlanGridView(IReadOnlyList<PlanGridRow> Items, BadgeHolder? BadgeHolder);

internal sealed record PlanCreated(Guid Id, string Code);

/// <summary>The users a plan allows: null for no limit.</summary>
internal sealed record PlanUsers(int? Limit);

/// <summary>One plan as the list reads it (contracts/plans-api.md §1).</summary>
internal sealed record PlanListRow(
    Guid Id, string Code, string NameAr, string NameEn, decimal MonthlyEgp, decimal AnnualEgp, decimal MonthlyUsd, decimal AnnualUsd,
    string? Badge, string State, PlanUsers Users, int Clinics, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);

internal sealed record MostWantedPlan(Guid Id, string NameAr, string NameEn, int Clinics);

internal sealed record PlanCards(int Total, int Disabled, int Clinics, int ClinicsNewThisMonth, MostWantedPlan? MostWanted, PlanRevenue? Revenue);

internal sealed record PlanActions(bool Manage);

internal sealed record PlansView(IReadOnlyList<PlanListRow> Items, PlanCards Cards, PlanActions Can);

/// <summary>A feature a plan includes, with its number or none.</summary>
internal sealed record PlanIncludedRow(string Key, int? Limit);

internal sealed record PlanSubscriptions(int Clinics, decimal MonthlyRevenue, int NewThisMonth, int OnTrial);

internal sealed record PlanClinicRow(Guid Id, string NameAr, string NameEn, string StateAr, string StateEn);

/// <summary>One plan in full: its detail and its form (contracts/plans-api.md §1).</summary>
internal sealed record PlanDetailView(
    Guid Id, string Code, string NameAr, string NameEn, decimal MonthlyEgp, decimal AnnualEgp, decimal MonthlyUsd, decimal AnnualUsd,
    string? Badge, string State, PlanUsers Users, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version, string Description,
    string? DisabledReason, DateTimeOffset? DisabledAt, IReadOnlyList<PlanIncludedRow> Features, int Excluded,
    PlanSubscriptions Subscriptions, IReadOnlyList<PlanClinicRow> Clinics);

/// <summary>
/// The plans (spec 013; contracts/plans-api.md): read for whoever may view plans, written for
/// whoever manages them. The clinics on each come from <see cref="IPlanSubscribers"/> — none until
/// spec 015 gives clinics their plans (research R7).
/// </summary>
internal sealed class PlanBook(PlansDbContext db, IPlanSubscribers subscribers, TimeProvider clock) : IPlanBook
{
    private const string UsersKey = "max_users";

    // ---------------------------------------------------------------- reads

    /// <summary>Every plan — active ones first, each by monthly price — and the cards (FR-004, FR-005).</summary>
    public async Task<PlansView> ListAsync(bool canManage, CancellationToken cancellation)
    {
        var plans = await db.Plans.AsNoTracking().Include(p => p.Features).ToListAsync(cancellation).ConfigureAwait(false);
        var usersBaseline = await UsersBaselineAsync(cancellation).ConfigureAwait(false);
        var counts = await subscribers.CountsAsync([.. plans.Select(p => p.Id)], cancellation).ConfigureAwait(false);
        int Clinics(Plan p) => counts.TryGetValue(p.Id, out var c) ? c.Clinics : 0;

        var rows = plans
            .OrderBy(p => p.IsActive ? 0 : 1)
            .ThenBy(p => p.MonthlyEgp)
            .ThenBy(p => p.CreatedAtUtc)
            .Select(p => new PlanListRow(
                p.Id, p.Code, p.NameAr, p.NameEn, p.MonthlyEgp, p.AnnualEgp, p.MonthlyUsd, p.AnnualUsd, p.Badge, p.State,
                Users(p, usersBaseline), Clinics(p), p.CreatedAtUtc, p.UpdatedAtUtc, p.Version))
            .ToList();

        // The plan with the most clinics; on a tie the cheaper, then the newer (analysis A1).
        var most = plans
            .Where(p => Clinics(p) > 0)
            .OrderByDescending(Clinics)
            .ThenBy(p => p.MonthlyEgp)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => new MostWantedPlan(p.Id, p.NameAr, p.NameEn, Clinics(p)))
            .FirstOrDefault();

        var cards = new PlanCards(
            rows.Count,
            rows.Count(r => r.State == PlanStates.Disabled),
            counts.Values.Sum(c => c.Clinics),
            counts.Values.Sum(c => c.NewThisMonth),
            most,
            await subscribers.RevenueAsync(cancellation).ConfigureAwait(false));

        return new PlansView(rows, cards, new PlanActions(canManage));
    }

    /// <summary>One plan in full, or null for an id that is no plan.</summary>
    public async Task<PlanDetailView?> ReadAsync(Guid id, CancellationToken cancellation)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == id, cancellation)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return null;
        }

        var catalogue = await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Key, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);
        var included = Included(plan, catalogue);
        var offered = catalogue.Values.Where(f => f.IsSold && !f.IsRetired).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        offered.UnionWith(included.Select(f => f.Key));

        var counts = await subscribers.CountsAsync([plan.Id], cancellation).ConfigureAwait(false);
        var mine = counts.TryGetValue(plan.Id, out var c) ? c : new PlanSubscriberCounts(0, 0, 0, 0m);
        var clinics = await subscribers.ClinicsAsync(plan.Id, cancellation).ConfigureAwait(false);

        return new PlanDetailView(
            plan.Id, plan.Code, plan.NameAr, plan.NameEn, plan.MonthlyEgp, plan.AnnualEgp, plan.MonthlyUsd, plan.AnnualUsd, plan.Badge,
            plan.State, Users(plan, catalogue.TryGetValue(UsersKey, out var users) ? users.Baseline : null), plan.CreatedAtUtc,
            plan.UpdatedAtUtc, plan.Version, plan.Description, plan.DisabledReason, plan.DisabledAtUtc, included,
            offered.Count - included.Count,
            new PlanSubscriptions(mine.Clinics, mine.MonthlyRevenue, mine.NewThisMonth, mine.OnTrial),
            [.. clinics.Select(k => new PlanClinicRow(k.ClinicId, k.NameAr, k.NameEn, k.StateAr, k.StateEn))]);
    }

    /// <summary>
    /// What a plan's form offers: the catalogue's sold, not retired features by group and order —
    /// and, for a plan being edited, each retired or unsold feature it already includes (ق-٤٨).
    /// Null for an id that is no plan.
    /// </summary>
    public async Task<PlanGridView?> GridAsync(Guid? id, CancellationToken cancellation)
    {
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (id is { } planId)
        {
            var plan = await db.Plans.AsNoTracking().Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == planId, cancellation)
                .ConfigureAwait(false);
            if (plan is null)
            {
                return null;
            }

            kept.UnionWith(plan.Features.Select(f => f.FeatureKey));
        }

        var features = (await db.Features.AsNoTracking().ToListAsync(cancellation).ConfigureAwait(false))
            .Where(f => (f.IsSold && !f.IsRetired) || kept.Contains(f.Key))
            .OrderBy(f => FeatureGroups.IndexOf(f.Group))
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => new PlanGridRow(f.Key, f.NameAr, f.NameEn, f.Group, f.Kind, f.UnitAr, f.UnitEn, f.Status, f.IsRetired, f.IsSold))
            .ToList();

        var holder = await db.Plans.AsNoTracking().Where(p => p.Badge != null)
            .Select(p => new BadgeHolder(p.Id, p.NameAr, p.NameEn, p.Badge!))
            .FirstOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        return new PlanGridView(features, holder);
    }

    // ---------------------------------------------------------------- writes

    /// <summary>
    /// Adds a plan, active, with its code derived once from its English name (OP-A-194) and its grid
    /// as sent — all checked against the catalogue before anything is written (ق-٤٢).
    /// </summary>
    public async Task<PlanCreated> AddAsync(PlanInput input, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = input.Fields;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await CheckAsync(input, already: new HashSet<string>(StringComparer.Ordinal), cancellation).ConfigureAwait(false);
            var now = clock.GetUtcNow();

            // The badge's holder first, then the code's range: every save locks in this order (research R2, R3).
            var former = string.IsNullOrEmpty(fields.Badge) ? null : await TakeBadgeAsync(null, now, cancellation).ConfigureAwait(false);
            var code = await FreeCodeAsync(PlanRules.BaseCode(fields.NameEn), cancellation).ConfigureAwait(false);

            var plan = Plan.Create(Guid.CreateVersion7(now), code, fields, now);
            plan.ReplaceFeatures(input.Rows.Select(r => (r.Key!, r.Limit)));
            db.Plans.Add(plan);
            await SaveAsync(cancellation).ConfigureAwait(false);

            var kinds = await KindsAsync(cancellation).ConfigureAwait(false);
            List<AuditChange> changes = [.. Values(plan).Select(v => AuditChange.Of(v.Field, null, v.Value))];
            changes.AddRange(plan.Features.Select(f => AuditChange.Of($"feature:{f.FeatureKey}", null, Included(f, kinds))));
            changes.RemoveAll(change => change.IsEmpty);

            await RecordAsync(former, cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(
                    new AuditRecord(AuditActions.PlanCreated, Target(plan), Facts(plan, input.Note), changes), cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return new PlanCreated(plan.Id, plan.Code);
        }
    }

    /// <summary>
    /// Edits everything but the code (ق-٤٦): the fields, the badge, and the grid replaced by what
    /// was sent (ق-٤٣). A retired or unsold feature the plan already has is kept when sent (ق-٤٨).
    /// </summary>
    public async Task EditAsync(Guid id, PlanInput input, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = input.Fields;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var plan = await db.Plans.Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == id, cancellation).ConfigureAwait(false)
                       ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

            if (input.Version is null)
            {
                throw ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["version"] = [PlanRules.Required],
                });
            }

            await CheckAsync(input, plan.Features.Select(f => f.FeatureKey).ToHashSet(StringComparer.Ordinal), cancellation)
                .ConfigureAwait(false);

            if (input.Version != plan.Version)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanChangedElsewhere);
            }

            if (!plan.IsActive && !string.IsNullOrEmpty(fields.Badge))
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanBadgeOnDisabled);
            }

            var now = clock.GetUtcNow();
            var kinds = await KindsAsync(cancellation).ConfigureAwait(false);
            var was = Values(plan);
            var hadGrid = plan.Features.ToDictionary(f => f.FeatureKey, f => Included(f, kinds), StringComparer.Ordinal);

            var former = string.IsNullOrEmpty(fields.Badge) ? null : await TakeBadgeAsync(plan.Id, now, cancellation).ConfigureAwait(false);
            plan.Edit(fields, now);
            plan.ReplaceFeatures(input.Rows.Select(r => (r.Key!, r.Limit)));
            await SaveAsync(cancellation).ConfigureAwait(false);

            var hasGrid = plan.Features.ToDictionary(f => f.FeatureKey, f => Included(f, kinds), StringComparer.Ordinal);
            List<AuditChange> changes =
            [
                .. was.Zip(Values(plan), (before, after) => AuditChange.Of(before.Field, before.Value, after.Value)),
                .. hadGrid.Keys.Union(hasGrid.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .Select(key => AuditChange.Of($"feature:{key}", hadGrid.GetValueOrDefault(key), hasGrid.GetValueOrDefault(key))),
            ];

            await RecordAsync(former, cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(
                    new AuditRecord(AuditActions.PlanUpdated, Target(plan), Facts(plan, input.Note), changes), cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// No new clinic is given the plan or moved to it; its clinics keep it (ق-٤٧). Its badge goes.
    /// A plan already disabled keeps its first reason and time, and nothing is recorded (§4-2).
    /// </summary>
    public async Task DisableAsync(Guid id, string? reason, CancellationToken cancellation)
    {
        if (!PlanRules.IsReason(reason))
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["reason"] = [PlanRules.TooLong],
            });
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var plan = await FindForWriteAsync(id, cancellation).ConfigureAwait(false);
            var was = (plan.State, plan.Badge, plan.DisabledReason);
            if (!plan.Disable(reason, clock.GetUtcNow()))
            {
                return;
            }

            await SaveAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.PlanDisabled,
                        Target(plan),
                        Facts(plan, null),
                        [
                            AuditChange.Of("state", was.State, plan.State),
                            AuditChange.Of("badge", was.Badge, plan.Badge),
                            AuditChange.Of("disabledReason", was.DisabledReason, plan.DisabledReason),
                        ]),
                    cancellation)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Given to clinics again; the reason and time go, the badge does not come back (ق-٤٥).</summary>
    public async Task EnableAsync(Guid id, CancellationToken cancellation)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var plan = await FindForWriteAsync(id, cancellation).ConfigureAwait(false);
            var was = (plan.State, plan.DisabledReason);
            if (!plan.Enable(clock.GetUtcNow()))
            {
                return;
            }

            await SaveAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.PlanEnabled,
                        Target(plan),
                        Facts(plan, null),
                        [
                            AuditChange.Of("state", was.State, plan.State),
                            AuditChange.Of("disabledReason", was.DisabledReason, plan.DisabledReason),
                        ]),
                    cancellation)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- published (FR-025)

    public async Task<PlanEntitlements?> FindAsync(Guid id, CancellationToken cancellation)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == id, cancellation)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return null;
        }

        var catalogue = await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Key, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);
        return new PlanEntitlements(
            plan.Id, plan.Code, plan.NameAr, plan.NameEn, plan.State, plan.MonthlyEgp, plan.AnnualEgp, plan.MonthlyUsd, plan.AnnualUsd,
            [.. Included(plan, catalogue).Select(row => new PlanIncluded(row.Key, catalogue[row.Key].Kind, row.Limit))]);
    }

    public async Task<IReadOnlyList<AssignablePlan>> AssignableAsync(CancellationToken cancellation) =>
    [
        .. (await db.Plans.AsNoTracking().Where(p => p.State == PlanStates.Active).ToListAsync(cancellation).ConfigureAwait(false))
            .OrderBy(p => p.MonthlyEgp)
            .ThenBy(p => p.NameEn, StringComparer.Ordinal)
            .Select(p => new AssignablePlan(p.Id, p.Code, p.NameAr, p.NameEn, p.MonthlyEgp, p.AnnualEgp)),
    ];

    // ---------------------------------------------------------------- write pieces

    private async Task<Plan> FindForWriteAsync(Guid id, CancellationToken cancellation) =>
        await db.Plans.SingleOrDefaultAsync(p => p.Id == id, cancellation).ConfigureAwait(false)
        ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    /// <summary>The fields, the note, and the grid against the catalogue as it is now; refused, never corrected.</summary>
    private async Task CheckAsync(PlanInput input, IReadOnlySet<string> already, CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(PlanRules.Check(input.Fields), StringComparer.Ordinal);
        if (!PlanRules.IsNote(input.Note))
        {
            errors["note"] = [PlanRules.TooLong];
        }

        var catalogue = await db.Features.AsNoTracking()
            .Select(f => new GridFeature(f.Key, f.Kind == FeatureKinds.Limit, f.IsSold, f.IsRetired))
            .ToDictionaryAsync(f => f.Key, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);
        var verdict = PlanGrid.Check(input.Rows, catalogue, already);
        foreach (var (field, words) in verdict.Errors)
        {
            errors[field] = words;
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        switch (verdict.Refusal)
        {
            case PlanGrid.FeatureUnknown:
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.PlanFeatureUnknown);
            case PlanGrid.SwitchHasLimit:
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.PlanSwitchHasLimit);
            case PlanGrid.FeatureNotSold:
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanFeatureNotSold);
        }
    }

    /// <summary>
    /// Takes the badge from whoever holds it, the holder's row locked to the end of the transaction
    /// so that two saves asking for it at once follow one another (research R2). Returns the plan
    /// it was taken from, its badge before, for the record.
    /// </summary>
    private async Task<(Plan Plan, string Badge)?> TakeBadgeAsync(Guid? keeper, DateTimeOffset now, CancellationToken cancellation)
    {
        var holderId = await db.Database
            .SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [plans].[Plan] WITH (UPDLOCK, HOLDLOCK) WHERE [Badge] IS NOT NULL")
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var other = holderId.FirstOrDefault(id => id != keeper);
        if (other == Guid.Empty)
        {
            return null;
        }

        var holder = await db.Plans.SingleAsync(p => p.Id == other, cancellation).ConfigureAwait(false);
        var badge = holder.Badge!;
        holder.LoseBadge(now);
        await SaveAsync(cancellation).ConfigureAwait(false);
        return (holder, badge);
    }

    /// <summary>The base code itself, or its next free number; the codes of that base are locked to the end of the transaction.</summary>
    private async Task<string> FreeCodeAsync(string baseCode, CancellationToken cancellation)
    {
        var prefix = baseCode.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
        var taken = await db.Database
            .SqlQuery<string>(
                $"SELECT [Code] AS [Value] FROM [plans].[Plan] WITH (UPDLOCK, HOLDLOCK) WHERE [Code] = {baseCode} OR [Code] LIKE {prefix + "-%"}")
            .ToListAsync(cancellation)
            .ConfigureAwait(false);
        return PlanRules.FreeCode(baseCode, taken);
    }

    /// <summary>The plan.updated of the plan a badge was taken from: its own history says why.</summary>
    private async Task RecordAsync((Plan Plan, string Badge)? former, CancellationToken cancellation)
    {
        if (former is not { } taken)
        {
            return;
        }

        await db.RecordForPlatformAsync(
                new AuditRecord(
                    AuditActions.PlanUpdated,
                    Target(taken.Plan),
                    Facts(taken.Plan, null),
                    [AuditChange.Of("badge", taken.Badge, null)]),
                cancellation)
            .ConfigureAwait(false);
    }

    private async Task SaveAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanChangedElsewhere);
        }
        catch (DbUpdateException taken) when (taken.IsUniqueViolation())
        {
            // The locks make this the last guard: a code or a badge taken in a way they did not see.
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanCodeTaken);
        }
    }

    private async Task<Dictionary<string, string>> KindsAsync(CancellationToken cancellation) =>
        await db.Features.AsNoTracking().ToDictionaryAsync(f => f.Key, f => f.Kind, StringComparer.Ordinal, cancellation)
            .ConfigureAwait(false);

    /// <summary>What a reader of the trail sees of an included feature: on, a limit, or no limit.</summary>
    private static string Included(PlanFeature row, Dictionary<string, string> kinds) =>
        kinds.TryGetValue(row.FeatureKey, out var kind) && kind == FeatureKinds.Limit
            ? row.Limit is { } limit ? $"limit {limit.ToString(CultureInfo.InvariantCulture)}" : "no limit"
            : "on";

    /// <summary>What a reader of the trail sees changed, field by field.</summary>
    private static List<(string Field, string? Value)> Values(Plan p) =>
    [
        ("nameAr", p.NameAr), ("nameEn", p.NameEn), ("description", p.Description),
        ("monthlyEgp", Amount(p.MonthlyEgp)), ("annualEgp", Amount(p.AnnualEgp)),
        ("monthlyUsd", Amount(p.MonthlyUsd)), ("annualUsd", Amount(p.AnnualUsd)), ("badge", p.Badge),
    ];

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static AuditTarget Target(Plan plan) => new(AuditTarget.Plan, null, plan.NameAr);

    /// <summary>The plan by its code — never a fact named <c>key</c> or <c>code</c> — and the note when written.</summary>
    private static Dictionary<string, object?> Facts(Plan plan, string? note)
    {
        var facts = new Dictionary<string, object?>(StringComparer.Ordinal) { ["plan"] = plan.Code };
        if (PlanRules.Note(note) is { } words)
        {
            facts["note"] = words;
        }

        return facts;
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>
    /// What the plan includes, in the catalogue's order. A number left on a feature that has since
    /// become a switch is not read (012's edge case: dropped at the plan's next save).
    /// </summary>
    private static List<PlanIncludedRow> Included(Plan plan, Dictionary<string, Feature> catalogue) =>
    [
        .. plan.Features
            .Where(f => catalogue.ContainsKey(f.FeatureKey))
            .Select(f => (Row: f, Feature: catalogue[f.FeatureKey]))
            .OrderBy(x => FeatureGroups.IndexOf(x.Feature.Group))
            .ThenBy(x => x.Feature.SortOrder)
            .Select(x => new PlanIncludedRow(x.Row.FeatureKey, x.Feature.Kind == FeatureKinds.Limit ? x.Row.Limit : null)),
    ];

    /// <summary>The users' row: its number when included, else the feature's baseline (ق-٣٨).</summary>
    private static PlanUsers Users(Plan plan, int? baseline)
    {
        var row = plan.Features.FirstOrDefault(f => f.FeatureKey == UsersKey);
        return new PlanUsers(row is null ? baseline : row.Limit);
    }

    private async Task<int?> UsersBaselineAsync(CancellationToken cancellation) =>
        await db.Features.AsNoTracking().Where(f => f.Key == UsersKey).Select(f => f.Baseline).SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);
}
