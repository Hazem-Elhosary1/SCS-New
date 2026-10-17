using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.Plans.Contracts;
using Scs.Plans.Domain;
using Scs.Plans.Infrastructure.Persistence;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>A plan as the tests read it back from <c>plans.Plan</c>, with its grid.</summary>
public sealed record PlanRowRead(
    Guid Id, string Code, string NameAr, string NameEn, string Description, decimal MonthlyEgp, decimal AnnualEgp, decimal MonthlyUsd,
    decimal AnnualUsd, string? Badge, string State, string? DisabledReason, DateTimeOffset? DisabledAtUtc, int Version,
    IReadOnlyDictionary<string, int?> Features);

/// <summary>One entry of the platform's trail about a plan.</summary>
public sealed record PlanEntry(string Action, string ActorKind, Guid? Actor, string? TargetName, string Facts, string Changes);

/// <summary>
/// What the tests of spec 013 share: plans put straight into <c>plans.Plan</c>, read back, the
/// trail's entries about them, and a stand-in for the clinics on plans. The database is shared, so
/// each test names its plans with a name of its own; the badge is the platform's one, so a test
/// about it first takes it from whoever holds it (<see cref="ClearBadgesAsync"/>).
/// </summary>
public static class PlanFixtures
{
    public const string PlansPath = "/v1/ops/plans";

    /// <summary>An English name nobody else's test uses, so its code is the test's own.</summary>
    public static string NewName(string start = "Test Plan") => $"{start} {Guid.NewGuid():N}";

    /// <summary>A body for adding a plan, with what a test changes.</summary>
    public static Dictionary<string, object?> NewPlan(string? nameEn = null, IEnumerable<object>? features = null, string? badge = null) =>
        new(StringComparer.Ordinal)
        {
            ["nameAr"] = "باقة تجريبية",
            ["nameEn"] = nameEn ?? NewName(),
            ["description"] = "باقة للاختبار.",
            ["monthlyEgp"] = 499m,
            ["annualEgp"] = 4990m,
            ["monthlyUsd"] = 25m,
            ["annualUsd"] = 250m,
            ["badge"] = badge,
            ["features"] = features?.ToList() ?? [],
        };

    /// <summary>A grid row.</summary>
    public static Dictionary<string, object?> Row(string key, int? limit = null) =>
        new(StringComparer.Ordinal) { ["key"] = key, ["limit"] = limit };

    /// <summary>Puts a plan straight into the table, with the grid given.</summary>
    public static async Task<Guid> PutPlanAsync(
        this ApiFactory factory, string? nameEn = null, string? badge = null, bool disabled = false, decimal monthly = 499m,
        params (string Key, int? Limit)[] grid)
    {
        ArgumentNullException.ThrowIfNull(factory);
        nameEn ??= NewName();
        var plan = Plan.Create(
            Guid.NewGuid(),
            PlanRules.BaseCode(nameEn),
            new PlanFields("باقة تجريبية", nameEn, "باقة للاختبار.", monthly, 0m, 0m, 0m, badge),
            DateTimeOffset.UtcNow);
        plan.ReplaceFeatures(grid);
        if (disabled)
        {
            plan.Disable(null, DateTimeOffset.UtcNow);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlansDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    public static async Task<PlanRowRead?> PlanAsync(this ApiFactory factory, Guid id)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlansDbContext>();
        var p = await db.Plans.AsNoTracking().Include(x => x.Features).SingleOrDefaultAsync(x => x.Id == id);
        return p is null
            ? null
            : new PlanRowRead(
                p.Id, p.Code, p.NameAr, p.NameEn, p.Description, p.MonthlyEgp, p.AnnualEgp, p.MonthlyUsd, p.AnnualUsd, p.Badge, p.State,
                p.DisabledReason, p.DisabledAtUtc, p.Version,
                p.Features.ToDictionary(f => f.FeatureKey, f => f.Limit, StringComparer.Ordinal));
    }

    /// <summary>How many plans and grid rows the database holds: a refused save leaves both as they were.</summary>
    public static async Task<(int Plans, int Rows)> PlanCountsAsync(this ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlansDbContext>();
        return (await db.Plans.CountAsync(), await db.PlanFeatures.CountAsync());
    }

    /// <summary>Takes the platform's one badge from whoever holds it, so a test starts from none.</summary>
    public static async Task ClearBadgesAsync(this SqlServerFixture sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        await TestDatabase.ExecuteAsync(sql.Main.Admin, "UPDATE [plans].[Plan] SET [Badge] = NULL WHERE [Badge] IS NOT NULL");
    }

    /// <summary>The trail's entries about one plan, oldest first.</summary>
    public static async Task<List<PlanEntry>> PlanEntriesAsync(this SqlServerFixture sql, string code, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var rows = await TestDatabase.ReadColumnAsync(
            sql.Main.Admin,
            $"""
             SELECT CONCAT(ActionCode, CHAR(31), ActorKind, CHAR(31), ActorAccountId, CHAR(31), TargetName, CHAR(31), Facts, CHAR(31), Changes)
             FROM [audit].[PlatformEntry]
             WHERE TargetKind = 'plan' AND JSON_VALUE(Facts, '$.plan') = '{code}' {(action is null ? "" : $"AND ActionCode = '{action}'")}
             ORDER BY OccurredAtUtc, Id
             """);

        return
        [
            .. rows.Select(row => row.Split('\u001f')).Select(parts => new PlanEntry(
                parts[0], parts[1], Guid.TryParse(parts[2], out var actor) ? actor : null,
                parts[3].Length == 0 ? null : parts[3], parts[4], parts[5])),
        ];
    }

    /// <summary>Replaces the "no clinics" answer with <paramref name="clinics"/> for one test's host.</summary>
    public static ApiFactory WithSubscribers(this ApiFactory factory, FakeSubscribers clinics)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.WithServices(services => services.AddScoped<IPlanSubscribers>(_ => clinics));
    }
}

/// <summary>Clinics on plans as a test gives them — what spec 015 and 016 will count for real.</summary>
public sealed class FakeSubscribers : IPlanSubscribers
{
    public Dictionary<Guid, PlanSubscriberCounts> Counts { get; } = [];

    public Dictionary<Guid, List<PlanClinic>> Clinics { get; } = [];

    public PlanRevenue? Revenue { get; set; }

    public Task<IReadOnlyDictionary<Guid, PlanSubscriberCounts>> CountsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyDictionary<Guid, PlanSubscriberCounts>>(
            Counts.Where(c => planIds.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value));

    public Task<PlanRevenue?> RevenueAsync(CancellationToken cancellation) => Task.FromResult(Revenue);

    public Task<IReadOnlyList<PlanClinic>> ClinicsAsync(Guid planId, CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyList<PlanClinic>>(Clinics.TryGetValue(planId, out var list) ? list : []);
}
