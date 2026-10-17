using Scs.Plans.Domain;

namespace Scs.UnitTests.Plans;

/// <summary>
/// Spec 013, FR-010 to FR-013, FR-017, FR-021, FR-022 and the rules of data-model.md: a plan's
/// code, names, description, prices, badge and states; and its grid checked against the
/// catalogue before anything is written (ق-٤٢).
/// </summary>
public class PlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static PlanFields Fields(string? badge = null, decimal monthly = 499m) =>
        new("الباقة الاحترافية", "Professional Plan", "باقة للعيادات المتوسطة.", monthly, 4990m, 25m, 250m, badge);

    private static Dictionary<string, string> Problems(PlanFields fields) =>
        PlanRules.Check(fields).ToDictionary(p => p.Key, p => p.Value[0], StringComparer.Ordinal);

    private static Plan Active(string? badge = null) => Plan.Create(Guid.NewGuid(), "professional-plan", Fields(badge), Now);

    // ------------------------------------------------------------ the code

    [Theory]
    [InlineData("Pro Plan", "pro-plan")]
    [InlineData("  Pro  ++ Plan 2 ", "pro-plan-2")]
    [InlineData("Professional Plan", "professional-plan")]
    [InlineData("Plan 2026", "plan-2026")]
    [InlineData("--Basic--", "basic")]
    [InlineData("!!!", "plan")]
    [InlineData("الباقة الأساسية", "plan")]
    [InlineData("", "plan")]
    public void The_code_comes_from_the_english_name(string nameEn, string code) =>
        Assert.Equal(code, PlanRules.BaseCode(nameEn));

    [Fact]
    public void A_taken_code_takes_the_next_free_number()
    {
        Assert.Equal("pro-plan", PlanRules.FreeCode("pro-plan", []));
        Assert.Equal("pro-plan-2", PlanRules.FreeCode("pro-plan", ["pro-plan"]));
        Assert.Equal("pro-plan-3", PlanRules.FreeCode("pro-plan", ["pro-plan", "pro-plan-2"]));
        Assert.Equal("pro-plan-2", PlanRules.FreeCode("pro-plan", ["pro-plan", "pro-plan-3"]));
    }

    [Fact]
    public void A_numbered_code_of_the_longest_name_fits_its_column()
    {
        var longest = PlanRules.BaseCode(new string('a', PlanRules.NameMaxLength));
        Assert.Equal(PlanRules.NameMaxLength, longest.Length);
        Assert.True(PlanRules.FreeCode(longest, [longest]).Length <= PlanRules.CodeMaxLength);
    }

    [Fact]
    public void Editing_never_changes_the_code()
    {
        var plan = Active();
        plan.Edit(Fields() with { NameEn = "Another Name" }, Now);
        Assert.Equal("professional-plan", plan.Code);
    }

    // ------------------------------------------------------------ the fields

    [Fact]
    public void A_complete_plan_has_no_problem() => Assert.Empty(PlanRules.Check(Fields()));

    [Theory]
    [InlineData("nameAr")]
    [InlineData("nameEn")]
    [InlineData("description")]
    public void A_required_text_left_empty_or_blank_is_refused(string field)
    {
        var fields = field switch
        {
            "nameAr" => Fields() with { NameAr = "   " },
            "nameEn" => Fields() with { NameEn = null },
            _ => Fields() with { Description = "" },
        };
        Assert.Equal(PlanRules.Required, Problems(fields)[field]);
    }

    [Fact]
    public void Names_have_up_to_one_hundred_characters_after_trimming()
    {
        Assert.Empty(PlanRules.Check(Fields() with { NameAr = "  " + new string('ب', 100) + "  " }));
        Assert.Equal(PlanRules.TooLong, Problems(Fields() with { NameEn = new string('b', 101) })["nameEn"]);
    }

    [Fact]
    public void The_description_has_up_to_five_hundred_characters()
    {
        Assert.Empty(PlanRules.Check(Fields() with { Description = new string('و', 500) }));
        Assert.Equal(PlanRules.TooLong, Problems(Fields() with { Description = new string('و', 501) })["description"]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("499")]
    [InlineData("499.9")]
    [InlineData("499.99")]
    [InlineData("9999999999.99")]
    public void An_amount_from_zero_to_the_ceiling_with_two_decimals_at_most_is_taken(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Empty(PlanRules.Check(Fields() with { MonthlyEgp = value, AnnualEgp = value, MonthlyUsd = value, AnnualUsd = value }));
    }

    [Theory]
    [InlineData("-1", "monthlyEgp")]
    [InlineData("499.999", "annualEgp")]
    [InlineData("10000000000", "monthlyUsd")]
    [InlineData("0.001", "annualUsd")]
    public void Any_other_amount_is_refused_at_its_field(string amount, string field)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var fields = field switch
        {
            "monthlyEgp" => Fields() with { MonthlyEgp = value },
            "annualEgp" => Fields() with { AnnualEgp = value },
            "monthlyUsd" => Fields() with { MonthlyUsd = value },
            _ => Fields() with { AnnualUsd = value },
        };
        Assert.Equal(PlanRules.Invalid, Problems(fields)[field]);
    }

    [Fact]
    public void Every_amount_is_required()
    {
        var problems = Problems(Fields() with { MonthlyEgp = null, AnnualEgp = null, MonthlyUsd = null, AnnualUsd = null });
        Assert.Equal(PlanRules.Required, problems["monthlyEgp"]);
        Assert.Equal(PlanRules.Required, problems["annualEgp"]);
        Assert.Equal(PlanRules.Required, problems["monthlyUsd"]);
        Assert.Equal(PlanRules.Required, problems["annualUsd"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PlanBadges.Best)]
    [InlineData(PlanBadges.MostPopular)]
    [InlineData(PlanBadges.BestSelling)]
    public void The_badge_is_none_or_one_of_three(string? badge) => Assert.Empty(PlanRules.Check(Fields(badge)));

    [Fact]
    public void Another_badge_is_refused() => Assert.Equal(PlanRules.Invalid, Problems(Fields("gold"))["badge"]);

    [Fact]
    public void A_change_note_has_up_to_five_hundred_characters()
    {
        Assert.Null(PlanRules.Note("  "));
        Assert.Equal("سبب", PlanRules.Note(" سبب "));
        Assert.True(PlanRules.IsNote(new string('n', 500)));
        Assert.False(PlanRules.IsNote(new string('n', 501)));
    }

    // ------------------------------------------------------------ made, edited

    [Fact]
    public void A_new_plan_is_active_trimmed_and_at_its_first_version()
    {
        var plan = Plan.Create(Guid.NewGuid(), "pro-plan", Fields() with { NameAr = "  الباقة  " }, Now);
        Assert.Equal(PlanStates.Active, plan.State);
        Assert.Equal("الباقة", plan.NameAr);
        Assert.Equal(1, plan.Version);
        Assert.Equal(Now, plan.CreatedAtUtc);
        Assert.Equal(Now, plan.UpdatedAtUtc);
        Assert.Null(plan.DisabledReason);
        Assert.Null(plan.DisabledAtUtc);
    }

    [Fact]
    public void A_plan_cannot_be_made_with_broken_fields() =>
        Assert.Throws<ArgumentException>(() => Plan.Create(Guid.NewGuid(), "pro-plan", Fields() with { NameAr = "" }, Now));

    [Fact]
    public void An_edit_raises_the_version_and_the_time()
    {
        var plan = Active();
        var later = Now.AddHours(1);
        plan.Edit(Fields(monthly: 599m), later);
        Assert.Equal(2, plan.Version);
        Assert.Equal(599m, plan.MonthlyEgp);
        Assert.Equal(later, plan.UpdatedAtUtc);
    }

    // ------------------------------------------------------------ the badge

    [Fact]
    public void Losing_the_badge_raises_the_version()
    {
        var plan = Active(PlanBadges.Best);
        plan.LoseBadge(Now.AddMinutes(1));
        Assert.Null(plan.Badge);
        Assert.Equal(2, plan.Version);
    }

    [Fact]
    public void A_disabled_plan_takes_no_badge()
    {
        var plan = Active();
        plan.Disable(null, Now);
        Assert.Throws<InvalidOperationException>(() => plan.Edit(Fields(PlanBadges.Best), Now));
        plan.Edit(Fields(), Now);
        Assert.Null(plan.Badge);
    }

    // ------------------------------------------------------------ disabled, enabled

    [Fact]
    public void Disabling_keeps_the_reason_and_time_and_removes_the_badge_without_a_new_version()
    {
        var plan = Active(PlanBadges.MostPopular);
        Assert.True(plan.Disable("  قديمة  ", Now));
        Assert.Equal(PlanStates.Disabled, plan.State);
        Assert.Equal("قديمة", plan.DisabledReason);
        Assert.Equal(Now, plan.DisabledAtUtc);
        Assert.Null(plan.Badge);
        Assert.Equal(1, plan.Version);
    }

    [Fact]
    public void An_empty_reason_is_no_reason()
    {
        var plan = Active();
        plan.Disable("   ", Now);
        Assert.Null(plan.DisabledReason);
    }

    [Fact]
    public void A_reason_has_up_to_five_hundred_characters()
    {
        Assert.True(PlanRules.IsReason(new string('س', 500)));
        Assert.False(PlanRules.IsReason(new string('س', 501)));
        Assert.Throws<ArgumentException>(() => Active().Disable(new string('س', 501), Now));
    }

    [Fact]
    public void Disabling_again_keeps_the_first_reason_and_time()
    {
        var plan = Active();
        plan.Disable("الأول", Now);
        Assert.False(plan.Disable("الثاني", Now.AddDays(1)));
        Assert.Equal("الأول", plan.DisabledReason);
        Assert.Equal(Now, plan.DisabledAtUtc);
    }

    [Fact]
    public void Enabling_clears_the_reason_and_time_and_does_not_bring_the_badge_back()
    {
        var plan = Active(PlanBadges.Best);
        plan.Disable("قديمة", Now);
        Assert.True(plan.Enable(Now.AddDays(1)));
        Assert.Equal(PlanStates.Active, plan.State);
        Assert.Null(plan.DisabledReason);
        Assert.Null(plan.DisabledAtUtc);
        Assert.Null(plan.Badge);
        Assert.Equal(1, plan.Version);
        Assert.False(plan.Enable(Now.AddDays(2)));
    }

    // ------------------------------------------------------------ the grid (test-first: plan entitlements)

    private static readonly Dictionary<string, GridFeature> Catalogue = new(StringComparer.Ordinal)
    {
        ["max_users"] = new("max_users", IsLimit: true, IsSold: true, IsRetired: false),
        ["allow_whatsapp"] = new("allow_whatsapp", IsLimit: false, IsSold: true, IsRetired: false),
        ["core_clinic"] = new("core_clinic", IsLimit: false, IsSold: false, IsRetired: false),
        ["old_channel"] = new("old_channel", IsLimit: false, IsSold: true, IsRetired: true),
    };

    private static GridVerdict Grid(IReadOnlySet<string>? already = null, params GridRow[] rows) =>
        PlanGrid.Check(rows, Catalogue, already ?? new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public void A_grid_of_sold_features_is_taken_and_an_empty_limit_means_no_limit()
    {
        var verdict = Grid(null, new("max_users", null), new("allow_whatsapp", null));
        Assert.Null(verdict.Refusal);
        Assert.Empty(verdict.Errors);
    }

    [Fact]
    public void A_key_the_catalogue_does_not_hold_is_refused() =>
        Assert.Equal(PlanGrid.FeatureUnknown, Grid(null, new GridRow("max_rooms", null)).Refusal);

    [Fact]
    public void A_number_on_a_switch_is_refused() =>
        Assert.Equal(PlanGrid.SwitchHasLimit, Grid(null, new GridRow("allow_whatsapp", 5)).Refusal);

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_001)]
    [InlineData(-3)]
    public void A_limit_outside_one_to_a_million_is_refused_at_its_row(int limit) =>
        Assert.Equal(PlanRules.Invalid, Grid(null, new GridRow("allow_whatsapp", null), new GridRow("max_users", limit)).Errors["features[1].limit"][0]);

    [Fact]
    public void A_key_sent_twice_is_refused_at_its_second_row() =>
        Assert.Equal(PlanRules.Invalid, Grid(null, new GridRow("max_users", 3), new GridRow("max_users", 5)).Errors["features[1].key"][0]);

    [Theory]
    [InlineData("core_clinic")]
    [InlineData("old_channel")]
    public void A_feature_not_sold_or_retired_is_refused_when_the_plan_did_not_have_it(string key) =>
        Assert.Equal(PlanGrid.FeatureNotSold, Grid(null, new GridRow(key, null)).Refusal);

    [Theory]
    [InlineData("core_clinic")]
    [InlineData("old_channel")]
    public void A_feature_not_sold_or_retired_the_plan_already_had_is_kept(string key) =>
        Assert.Null(Grid(new HashSet<string>([key], StringComparer.Ordinal), new GridRow(key, null)).Refusal);

    [Fact]
    public void An_empty_grid_is_a_plan_that_includes_nothing() => Assert.Null(Grid(null).Refusal);
}
