using Scs.Identity.Contracts;
using Scs.Operators.Contracts;
using Scs.Plans.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>The list's question (contracts/ops-clinics-api.md, <c>GET /clinics</c>).</summary>
internal sealed record ClinicsQuery(
    string? Search, IReadOnlyList<string> Statuses, Guid? Plan, string? Length, int? EndsWithin, string? Sort, int? Page, int? Size);

internal sealed record NamedRef(Guid Id, string NameAr, string NameEn);

internal sealed record OperatorRef(Guid Id, string Name);

internal sealed record LengthView(string Code, int? Days);

internal sealed record ClinicRow(
    Guid Id,
    string Slug,
    string NameAr,
    string NameEn,
    DateTimeOffset CreatedAt,
    OperatorRef? CreatedBy,
    NamedRef? Plan,
    ClinicStatusView Status,
    LengthView? Length,
    DateTimeOffset? EndsAt,
    string? OwnerName,
    string? OwnerEmail,
    string? OwnerMobile);

internal sealed record FollowUpCounts(int Overdue, int ReadOnly, int Suspended);

internal sealed record ClinicCards(int Total, int NewThisMonth, int Active, int ActiveShare, int Trial, bool TrialEndingSoon, FollowUpCounts NeedsFollowUp);

internal sealed record ClinicFacets(IReadOnlyList<string> Statuses, IReadOnlyList<NamedRef> Plans, IReadOnlyList<string> Lengths, IReadOnlyList<int> EndsWithin);

internal sealed record ClinicActions(bool Add, bool Edit);

internal sealed record ClinicsPage(
    IReadOnlyList<ClinicRow> Items, int Total, int Page, int Size, ClinicCards Cards, ClinicFacets Facets, ClinicActions Can);

/// <summary>
/// The operators' list of clinics (spec 015, FR-007 – FR-013; research R13): the visible clinics,
/// their plan links and their owners, each read across clinics by its own module, joined here;
/// then searched, filtered, counted, sorted and paged. Built for a few hundred clinics (SC-007).
/// </summary>
internal sealed class ClinicsReading(
    IClinicRegistry registry,
    OperatorClinicPlans clinicPlans,
    IClinicOwners owners,
    IPlanBook plans,
    IOperatorDirectory operators,
    TimeProvider clock)
{
    public const int DefaultSize = 8;
    public const int MaxSize = 50;
    public const int MaxSearch = 100;

    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private static readonly int[] Windows = [7, 14, 30];

    public async Task<ClinicsPage> ListAsync(ClinicsQuery query, string operatorId, ClinicActions can, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = await RowsAsync(operatorId, cancellation).ConfigureAwait(false);
        var now = clock.GetUtcNow();

        var cards = Cards(rows, now);
        var searched = Search(rows, query.Search);
        var facets = Facets(searched, now, query.EndsWithin);
        if (query.Plan is { } chosen && facets.Plans.All(p => p.Id != chosen)
            && await plans.FindAsync(chosen, cancellation).ConfigureAwait(false) is { } kept)
        {
            // A chosen plan stays offered, so it can be cleared (FR-010).
            facets = facets with { Plans = [.. facets.Plans, new NamedRef(kept.Id, kept.NameAr, kept.NameEn)] };
        }

        var filtered = searched
            .Where(r => query.Statuses.Count == 0 || query.Statuses.Contains(r.Status.Code, StringComparer.Ordinal))
            .Where(r => query.Plan is null || r.Plan?.Id == query.Plan)
            .Where(r => query.Length is null || LengthCode(r) == query.Length)
            .Where(r => query.EndsWithin is not { } days || (r.EndsAt is { } end && end > now && end <= now.AddDays(days)))
            .ToList();

        var sorted = query.Sort switch
        {
            "ends-asc" => filtered.OrderBy(r => r.EndsAt is null).ThenBy(r => r.EndsAt).ThenByDescending(r => r.CreatedAt).ToList(),
            "ends-desc" => filtered.OrderBy(r => r.EndsAt is null).ThenByDescending(r => r.EndsAt).ThenByDescending(r => r.CreatedAt).ToList(),
            _ => filtered.OrderByDescending(r => r.CreatedAt).ToList(),
        };

        var size = Math.Clamp(query.Size ?? DefaultSize, 1, MaxSize);
        var pages = Math.Max(1, (int)Math.Ceiling(sorted.Count / (double)size));
        var page = Math.Clamp(query.Page ?? 1, 1, pages);
        return new ClinicsPage([.. sorted.Skip((page - 1) * size).Take(size)], sorted.Count, page, size, cards, facets, can);
    }

    /// <summary>Every visible clinic as a row, joined from the three modules' reads.</summary>
    public async Task<IReadOnlyList<ClinicRow>> RowsAsync(string operatorId, CancellationToken cancellation)
    {
        var clinics = await registry.ListAsync(operatorId, cancellation).ConfigureAwait(false);
        var links = (await clinicPlans.ListAsync(cancellation).ConfigureAwait(false)).ToDictionary(l => l.ClinicId);
        var people = (await owners.ListForOperatorsAsync(cancellation).ConfigureAwait(false)).ToDictionary(o => o.ClinicId);

        var planNames = new Dictionary<Guid, NamedRef>();
        foreach (var planId in links.Values.Select(l => l.PlanId).OfType<Guid>().Distinct())
        {
            if (await plans.FindAsync(planId, cancellation).ConfigureAwait(false) is { } plan)
            {
                planNames[planId] = new NamedRef(plan.Id, plan.NameAr, plan.NameEn);
            }
        }

        var operatorNames = new Dictionary<Guid, string>();
        foreach (var id in clinics.Select(c => c.CreatedBy).Select(ParseOperator).OfType<Guid>().Distinct())
        {
            if (await operators.FindAsync(id, cancellation).ConfigureAwait(false) is { } found)
            {
                operatorNames[id] = found.FullName;
            }
        }

        var now = clock.GetUtcNow();
        return
        [
            .. clinics.Select(c =>
            {
                links.TryGetValue(c.Id, out var link);
                people.TryGetValue(c.Id, out var owner);
                var status = ClinicStatus.Of(c.State, link?.TrialEndsAtUtc, now);
                var inTrial = status.Code == ClinicStatus.Trial;
                var createdBy = ParseOperator(c.CreatedBy) is { } by && operatorNames.TryGetValue(by, out var name) ? new OperatorRef(by, name) : null;
                return new ClinicRow(
                    c.Id,
                    c.ShortName,
                    c.NameAr,
                    c.NameEn,
                    c.CreatedAtUtc,
                    createdBy,
                    link?.PlanId is { } planId && planNames.TryGetValue(planId, out var plan) ? plan : null,
                    status,
                    inTrial ? new LengthView(ClinicStatus.Trial, TrialDays(link!)) : null,
                    inTrial ? link!.TrialEndsAtUtc : null,
                    owner?.FullName,
                    owner?.Email,
                    owner?.Mobile);
            }),
        ];
    }

    private static Guid? ParseOperator(string? createdBy) => Guid.TryParse(createdBy, out var id) ? id : null;

    /// <summary>The trial's whole length in days, as the frame writes it («تجربة 14 يوم»).</summary>
    private static int TrialDays(ClinicPlanRow link) =>
        link.TrialEndsAtUtc is { } end ? (int)Math.Round((end - link.SetAtUtc).TotalDays) : 0;

    /// <summary>«مدة الاشتراك»: the trial today; monthly and yearly come with spec 018; otherwise «غير محددة».</summary>
    private static string LengthCode(ClinicRow row) => row.Length?.Code ?? "unset";

    private static ClinicCards Cards(IReadOnlyList<ClinicRow> rows, DateTimeOffset now)
    {
        var monthStart = MonthStartInCairo(now);
        var active = rows.Count(r => r.Status.Code == ClinicStatus.Active);
        var trial = rows.Count(r => r.Status.Code == ClinicStatus.Trial);
        return new ClinicCards(
            rows.Count,
            rows.Count(r => r.CreatedAt >= monthStart),
            active,
            rows.Count == 0 ? 0 : (int)Math.Round(active * 100.0 / rows.Count),
            trial,
            rows.Any(r => r.EndsAt is { } end && end > now && end <= now.AddDays(7)),
            new FollowUpCounts(0, 0, 0));
    }

    private static List<ClinicRow> Search(IReadOnlyList<ClinicRow> rows, string? search)
    {
        var text = (search ?? "").Trim();
        if (text.Length == 0)
        {
            return [.. rows];
        }

        if (text.Length > MaxSearch)
        {
            text = text[..MaxSearch];
        }

        var digits = text.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return
        [
            .. rows.Where(r =>
                Has(r.NameAr, text) || Has(r.NameEn, text) || Has(r.OwnerName, text) || Has(r.OwnerEmail, text)
                || (digits.Length > 0 && Has(r.OwnerMobile, digits))),
        ];
    }

    private static bool Has(string? value, string part) =>
        value is not null && value.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static ClinicFacets Facets(IReadOnlyList<ClinicRow> searched, DateTimeOffset now, int? chosenWindow)
    {
        var statuses = new[] { ClinicStatus.Active, ClinicStatus.Trial }
            .Where(code => searched.Any(r => r.Status.Code == code)).ToList();
        var planList = searched.Select(r => r.Plan).OfType<NamedRef>().DistinctBy(p => p.Id).OrderBy(p => p.NameEn, StringComparer.Ordinal).ToList();

        var lengths = new[] { "monthly", "annual", ClinicStatus.Trial, "unset" }
            .Where(code => searched.Any(r => LengthCode(r) == code)).ToList();
        // Only the windows some searched clinic ends in, and the chosen one so it can be cleared (FR-010).
        var windows = Windows
            .Where(days => days == chosenWindow || searched.Any(r => r.EndsAt is { } end && end > now && end <= now.AddDays(days)))
            .ToList();
        return new ClinicFacets(statuses, planList, lengths, windows);
    }

    private static DateTimeOffset MonthStartInCairo(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Cairo);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(start, Cairo.GetUtcOffset(start));
    }
}
