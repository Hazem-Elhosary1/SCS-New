using Scs.Plans.Infrastructure.Seed;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Domain;

namespace Scs.UnitTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 1 (FR-002 to FR-006; data-model.md "Rules table"): what a clinic has,
/// worked out by the BRD's rules — ق-٣٧ a specialty narrows a plan, ق-٣٨ a limit's number, its
/// absence and the baseline, ق-٤٩ an upcoming feature enforced by nothing. Test-first: plan
/// entitlements (constitution VII).
/// </summary>
public class EntitlementsTests
{
    private static CatalogueRow Switch(string key, bool built = true, string? specialty = null) =>
        new(key, Entitlements.Switch, key, key, null, null, null, specialty, built, Monthly: false);

    private static CatalogueRow Limit(string key, int? baseline, bool built = true, bool monthly = false) =>
        new(key, Entitlements.Limit, key, key, "u", "u", baseline, null, built, monthly);

    private static readonly IReadOnlyList<CatalogueRow> Catalogue =
    [
        Switch("core_clinic", built: false),
        Limit("max_users", baseline: null),
        Limit("max_doctors", baseline: 1),
        Limit("max_branches", baseline: 1),
        Limit("queue_screen", baseline: 0),
        Limit("message_credits", baseline: 0, monthly: true),
        Switch("allow_analytics"),
        Switch("allow_whatsapp"),
        Switch("dental_chart", specialty: "dental"),
        Switch("specialty_exam_modules"),
        Switch("informed_consent", built: false),
    ];

    private static PlanGrid Plan(params (string Key, int? Limit)[] rows) =>
        new(Guid.NewGuid(), "باقة", "Plan", [.. rows.Select(r => new PlanRow(r.Key, r.Limit))]);

    private static IReadOnlyDictionary<string, Entitled> Work(PlanGrid? plan, string? specialty = null) =>
        Entitlements.Work(plan, Catalogue, specialty);

    [Fact]
    public void With_no_plan_a_clinic_has_the_core_and_the_baselines_only()
    {
        var has = Work(plan: null);

        Assert.True(has["core_clinic"].Included);
        Assert.False(has["allow_analytics"].Included);
        Assert.False(has["allow_whatsapp"].Included);
        Assert.Null(has["max_users"].Limit);
        Assert.Equal(1, has["max_doctors"].Limit);
        Assert.Equal(1, has["max_branches"].Limit);
        Assert.Equal(0, has["queue_screen"].Limit);
        Assert.Equal(0, has["message_credits"].Limit);
    }

    [Fact]
    public void An_included_limit_with_no_number_has_no_limit_and_with_one_has_it()
    {
        var has = Work(Plan(("max_users", null), ("max_doctors", 5)));

        Assert.Null(has["max_users"].Limit);
        Assert.Equal(5, has["max_doctors"].Limit);
        Assert.True(has["max_users"].Included);
    }

    [Fact]
    public void A_limit_left_out_of_the_plan_takes_its_baseline()
    {
        var has = Work(Plan(("max_users", 10)));

        Assert.Equal(1, has["max_doctors"].Limit);
        Assert.Equal(0, has["message_credits"].Limit);
    }

    [Fact]
    public void A_switch_is_included_only_when_the_plan_includes_it()
    {
        var has = Work(Plan(("allow_analytics", null)));

        Assert.True(has["allow_analytics"].Included);
        Assert.False(has["allow_whatsapp"].Included);
        Assert.Null(has["allow_analytics"].Limit);
    }

    [Theory]
    [InlineData("dermatology", false)]
    [InlineData(null, false)]
    [InlineData("dental", true)]
    public void A_specialty_narrows_the_plan_and_never_widens_it(string? specialty, bool included)
    {
        var has = Work(Plan(("dental_chart", null)), specialty);

        Assert.Equal(included, has["dental_chart"].Included);
        Assert.False(Work(Plan(), specialty)["dental_chart"].Included);
    }

    [Fact]
    public void The_exam_modules_need_a_specialty()
    {
        Assert.False(Work(Plan(("specialty_exam_modules", null)), specialty: null)["specialty_exam_modules"].Included);
        Assert.True(Work(Plan(("specialty_exam_modules", null)), "dental")["specialty_exam_modules"].Included);
    }

    [Fact]
    public void An_upcoming_feature_says_what_the_plan_holds_and_is_enforced_by_nothing()
    {
        var has = Work(Plan(("informed_consent", null)));

        Assert.True(has["informed_consent"].Included);
        Assert.False(has["informed_consent"].Enforced);
        Assert.False(Work(Plan())["informed_consent"].Included);
        Assert.False(Work(Plan())["informed_consent"].Enforced);
        Assert.True(has["allow_analytics"].Enforced);
    }

    [Fact]
    public void A_key_the_catalogue_does_not_hold_has_no_row_and_the_caller_answers_it_with_nothing()
    {
        var has = Work(Plan(("no_such_feature", 5)));

        Assert.False(has.ContainsKey("no_such_feature"));
    }

    [Fact]
    public void Every_feature_of_the_catalogue_is_answered_and_monthly_kept()
    {
        var has = Work(plan: null);

        Assert.Equal(Catalogue.Select(c => c.Key).Order(), has.Keys.Order());
        Assert.True(has["message_credits"].Monthly);
        Assert.False(has["max_users"].Monthly);
    }

    [Fact]
    public void The_keys_code_refers_to_are_all_in_the_catalogue()
    {
        var seeded = FeatureSeed.Features().Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var named = typeof(FeatureKeys).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.All(named, key => Assert.Contains(key, seeded));
        Assert.Equal(8, FeatureKeys.CountedLimits.Count);
        Assert.All(FeatureKeys.Monthly, key => Assert.Contains(key, FeatureKeys.CountedLimits));
    }
}
