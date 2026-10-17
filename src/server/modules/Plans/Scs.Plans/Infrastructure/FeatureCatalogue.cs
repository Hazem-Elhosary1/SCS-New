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

/// <param name="Plans">How many plans include it (spec 013).</param>
internal sealed record FeatureRow(
    string Key, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, string Group, string Kind,
    string? UnitAr, string? UnitEn, int? Baseline, string? Specialty, string Status, bool IsSold, bool IsRetired, int SortOrder,
    bool Monthly, int Plans, int Version);

/// <param name="Groups">The groups that have features.</param>
/// <param name="SoldInPlans">Features included in at least one plan.</param>
/// <param name="Plans">The plans those features are in.</param>
internal sealed record FeatureCards(int Total, int Groups, int Built, int Upcoming, int SoldInPlans, int Plans);

internal sealed record FeatureActions(bool Manage);

internal sealed record FeaturesView(IReadOnlyList<FeatureRow> Items, FeatureCards Cards, FeatureActions Can);

/// <param name="State"><c>active</c> or <c>disabled</c>, as the plans say it (spec 013).</param>
internal sealed record PlanUse(Guid Id, string NameAr, string NameEn, string State, int Clinics);

internal sealed record WhereUsed(IReadOnlyList<PlanUse> Items, int Total);

/// <summary>A feature's fields as a request sends them; the key only when adding.</summary>
internal sealed record FeatureInput(
    string? Key, string? NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, string? Group, string? Kind,
    string? UnitAr, string? UnitEn, int? Baseline, string? Specialty, string? Status, bool? IsSold, int? Version)
{
    public FeatureFields Fields => new(
        NameAr, NameEn, DescriptionAr, DescriptionEn, Group, Kind, UnitAr, UnitEn, Baseline, Specialty, Status, IsSold ?? true);
}

/// <summary>
/// The feature catalogue (spec 012): its reads for the console, the one read other modules use,
/// and adding, editing, retiring and selling again. Every write is recorded in the platform's
/// trail with what changed, in the same transaction (FR-017). Nothing here touches a plan or a
/// clinic: retiring a feature changes the feature alone (FR-021).
/// </summary>
internal sealed class FeatureCatalogue(PlansDbContext db, IPlanSubscribers subscribers) : IFeatureCatalogue
{
    // ---------------------------------------------------------------- reads

    /// <summary>The whole catalogue, by group then order, and the cards counted from it.</summary>
    public async Task<FeaturesView> ListAsync(bool canManage, CancellationToken cancellation)
    {
        var features = await OrderedAsync(cancellation).ConfigureAwait(false);
        var uses = await db.PlanFeatures.AsNoTracking().Select(p => new { p.FeatureKey, p.PlanId }).ToListAsync(cancellation)
            .ConfigureAwait(false);
        var counts = uses.GroupBy(u => u.FeatureKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var rows = features.Select(f => Row(f, counts.GetValueOrDefault(f.Key))).ToList();

        return new FeaturesView(
            rows,
            new FeatureCards(
                rows.Count,
                rows.Select(r => r.Group).Distinct(StringComparer.Ordinal).Count(),
                rows.Count(r => r.Status == FeatureStatuses.Built),
                rows.Count(r => r.Status == FeatureStatuses.Upcoming),
                rows.Count(r => r.Plans > 0),
                Plans: uses.Select(u => u.PlanId).Distinct().Count()),
            new FeatureActions(canManage));
    }

    public async Task<FeatureRow?> ReadAsync(string key, CancellationToken cancellation)
    {
        var feature = await db.Features.AsNoTracking().SingleOrDefaultAsync(f => f.Key == key, cancellation).ConfigureAwait(false);
        if (feature is null)
        {
            return null;
        }

        var plans = await db.PlanFeatures.AsNoTracking().CountAsync(p => p.FeatureKey == key, cancellation).ConfigureAwait(false);
        return Row(feature, plans);
    }

    /// <summary>The plans that include the feature, with their state and clinics, active ones first by price (FR-027).</summary>
    public async Task<WhereUsed?> PlansOfAsync(string key, CancellationToken cancellation)
    {
        if (!await db.Features.AsNoTracking().AnyAsync(f => f.Key == key, cancellation).ConfigureAwait(false))
        {
            return null;
        }

        var plans = await db.Plans.AsNoTracking()
            .Where(p => p.Features.Any(f => f.FeatureKey == key))
            .ToListAsync(cancellation)
            .ConfigureAwait(false);
        var counts = await subscribers.CountsAsync([.. plans.Select(p => p.Id)], cancellation).ConfigureAwait(false);
        var items = plans
            .OrderBy(p => p.IsActive ? 0 : 1)
            .ThenBy(p => p.MonthlyEgp)
            .Select(p => new PlanUse(p.Id, p.NameAr, p.NameEn, p.State, counts.TryGetValue(p.Id, out var c) ? c.Clinics : 0))
            .ToList();
        return new WhereUsed(items, items.Sum(p => p.Clinics));
    }

    public async Task<CatalogueFeature?> FindAsync(string key, CancellationToken cancellation)
    {
        var feature = await db.Features.AsNoTracking().SingleOrDefaultAsync(f => f.Key == key, cancellation).ConfigureAwait(false);
        return feature is null ? null : Published(feature);
    }

    public async Task<IReadOnlyList<CatalogueFeature>> SellableAsync(CancellationToken cancellation) =>
        [.. (await OrderedAsync(cancellation).ConfigureAwait(false)).Where(f => f.IsSold && !f.IsRetired).Select(Published)];

    // ---------------------------------------------------------------- writes

    /// <summary>Adds a feature at the end of its group (FR-011). The key is the caller's, once.</summary>
    public async Task<string> AddAsync(FeatureInput input, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = input.Fields;
        Refuse(FeatureRules.Check(input.Key, fields));
        RefuseShape(fields);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            if (await db.Features.AsNoTracking().AnyAsync(f => f.Key == input.Key, cancellation).ConfigureAwait(false))
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureKeyTaken);
            }

            var feature = Feature.Create(input.Key!, fields, await NextOrderAsync(fields.Group!, cancellation).ConfigureAwait(false));
            db.Features.Add(feature);
            await SaveAsync(cancellation).ConfigureAwait(false);

            List<AuditChange> changes = [.. Values(feature).Select(v => AuditChange.Of(v.Field, null, v.Value))];
            changes.RemoveAll(change => change.IsEmpty);
            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.FeatureCreated,
                        Target(feature),
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["feature"] = feature.Key, ["group"] = feature.Group, ["kind"] = feature.Kind,
                        },
                        changes),
                    cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return feature.Key;
        }
    }

    /// <summary>Edits everything but the key (ق-٤٦). A move to another group puts it last there.</summary>
    public async Task EditAsync(string key, FeatureInput input, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = input.Fields;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var feature = await FindForWriteAsync(key, cancellation).ConfigureAwait(false);

            // Left out of the body, "sold" stays what it is: nothing is put on sale by omission.
            if (input.IsSold is null)
            {
                fields = fields with { IsSold = feature.IsSold };
            }

            var errors = new Dictionary<string, string[]>(FeatureRules.Check(null, fields, editing: true), StringComparer.Ordinal);
            if (input.Version is null)
            {
                errors["version"] = [FeatureRules.Required];
            }

            Refuse(errors);
            RefuseShape(fields);

            if (input.Version != feature.Version)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureChangedElsewhere);
            }

            if (feature.IsRetired && !fields.IsSold)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureRetiredMustBeSold);
            }

            var was = Values(feature);
            var order = fields.Group == feature.Group ? (int?)null : await NextOrderAsync(fields.Group!, cancellation).ConfigureAwait(false);
            feature.Edit(fields, order);
            await SaveAsync(cancellation).ConfigureAwait(false);

            var now = Values(feature);
            List<AuditChange> changes = [.. was.Zip(now, (before, after) => AuditChange.Of(before.Field, before.Value, after.Value))];
            changes.RemoveAll(change => change.IsEmpty);

            if (changes.Count > 0)
            {
                await db.RecordForPlatformAsync(
                        new AuditRecord(
                            AuditActions.FeatureUpdated,
                            Target(feature),
                            new Dictionary<string, object?>(StringComparer.Ordinal) { ["feature"] = feature.Key },
                            changes),
                        cancellation)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>No new plan can take it; plans that hold it keep it, and no clinic is touched (FR-021).</summary>
    public Task RetireAsync(string key, CancellationToken cancellation) =>
        SetRetiredAsync(key, retired: true, AuditActions.FeatureRetired, cancellation);

    /// <summary>Offered to every plan again (FR-021a).</summary>
    public Task ResumeAsync(string key, CancellationToken cancellation) =>
        SetRetiredAsync(key, retired: false, AuditActions.FeatureResumed, cancellation);

    // ---------------------------------------------------------------- pieces

    private async Task SetRetiredAsync(string key, bool retired, string action, CancellationToken cancellation)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var feature = await FindForWriteAsync(key, cancellation).ConfigureAwait(false);
            if (retired ? !feature.IsSold || feature.IsRetired : !feature.IsRetired)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureStateChanged);
            }

            if (retired)
            {
                feature.Retire();
            }
            else
            {
                feature.Resume();
            }

            await SaveAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        action,
                        Target(feature),
                        new Dictionary<string, object?>(StringComparer.Ordinal) { ["feature"] = feature.Key },
                        [AuditChange.Of("isRetired", Flag(!retired), Flag(retired))]),
                    cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The group's last order plus ten. The group's rows are locked to the end of the transaction,
    /// so two features added to one group at once never take the same number (research R5).
    /// </summary>
    private async Task<int> NextOrderAsync(string group, CancellationToken cancellation)
    {
        var last = await db.Database
            .SqlQuery<int>($"SELECT ISNULL(MAX([SortOrder]), 0) AS [Value] FROM [plans].[Feature] WITH (UPDLOCK, HOLDLOCK) WHERE [Group] = {group}")
            .SingleAsync(cancellation)
            .ConfigureAwait(false);
        return last + FeatureRules.OrderStep;
    }

    private async Task<Feature> FindForWriteAsync(string key, CancellationToken cancellation) =>
        await db.Features.SingleOrDefaultAsync(f => f.Key == key, cancellation).ConfigureAwait(false)
        ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private async Task<List<Feature>> OrderedAsync(CancellationToken cancellation) =>
        [
            .. (await db.Features.AsNoTracking().ToListAsync(cancellation).ConfigureAwait(false))
                .OrderBy(f => FeatureGroups.IndexOf(f.Group))
                .ThenBy(f => f.SortOrder)
                .ThenBy(f => f.Key, StringComparer.Ordinal),
        ];

    private async Task SaveAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureChangedElsewhere);
        }
        catch (DbUpdateException taken) when (taken.IsUniqueViolation())
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.FeatureKeyTaken);
        }
    }

    private static void Refuse(IReadOnlyDictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }
    }

    private static void RefuseShape(FeatureFields fields)
    {
        switch (FeatureRules.Shape(fields))
        {
            case FeatureRules.UnitsRequired:
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.FeatureUnitsRequired);
            case FeatureRules.SwitchHasLimit:
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.FeatureSwitchHasLimit);
        }
    }

    /// <summary>What a reader of the trail sees changed, field by field.</summary>
    private static List<(string Field, string? Value)> Values(Feature f) =>
    [
        ("nameAr", f.NameAr), ("nameEn", f.NameEn), ("descriptionAr", f.DescriptionAr), ("descriptionEn", f.DescriptionEn),
        ("group", f.Group), ("kind", f.Kind), ("unitAr", f.UnitAr), ("unitEn", f.UnitEn),
        ("baseline", f.Baseline?.ToString(CultureInfo.InvariantCulture)), ("specialty", f.Specialty), ("status", f.Status),
        ("isSold", Flag(f.IsSold)),
    ];

    private static string Flag(bool value) => value ? "true" : "false";

    private static AuditTarget Target(Feature feature) => new(AuditTarget.Feature, null, feature.NameAr);

    private static FeatureRow Row(Feature f, int plans) => new(
        f.Key, f.NameAr, f.NameEn, f.DescriptionAr, f.DescriptionEn, f.Group, f.Kind, f.UnitAr, f.UnitEn, f.Baseline, f.Specialty,
        f.Status, f.IsSold, f.IsRetired, f.SortOrder, f.IsMonthly, plans, f.Version);

    private static CatalogueFeature Published(Feature f) => new(
        f.Key, f.NameAr, f.NameEn, f.Kind, f.UnitAr, f.UnitEn, f.Baseline, f.Specialty, f.Status, f.IsSold, f.IsRetired, f.IsMonthly);
}
