using Scs.Plans.Domain;
using Scs.SharedKernel.Clinics;

namespace Scs.UnitTests.Plans;

/// <summary>
/// Spec 012, FR-009 to FR-013 and the rules of data-model.md: a feature's key, names,
/// descriptions, group, kind, units, baseline, specialty and status; retiring and selling again;
/// and the two fixed lists the catalogue does not store.
/// </summary>
public class FeatureTests
{
    private static FeatureFields Switch(string group = FeatureGroups.Channels) =>
        new("قناة واتساب", "WhatsApp channel", null, null, group, FeatureKinds.Switch, null, null, null, null, FeatureStatuses.Upcoming, IsSold: true);

    private static FeatureFields Limit(int? baseline = 0) =>
        new("رصيد الرسائل", "Message credits", null, null, FeatureGroups.Channels, FeatureKinds.Limit, "رسالة/شهر", "msgs / mo", baseline, null,
            FeatureStatuses.Upcoming, IsSold: true);

    private static Dictionary<string, string> Problems(string? key, FeatureFields fields) =>
        FeatureRules.Check(key, fields).ToDictionary(p => p.Key, p => p.Value[0], StringComparer.Ordinal);

    [Theory]
    [InlineData("ab")]
    [InlineData("max_users")]
    [InlineData("a1_b2")]
    public void A_key_of_lower_case_letters_digits_and_underscores_starting_with_a_letter_is_taken(string key) =>
        Assert.Empty(FeatureRules.Check(key, Switch()));

    [Theory]
    [InlineData("a")]
    [InlineData("1abc")]
    [InlineData("_abc")]
    [InlineData("Max_users")]
    [InlineData("max-users")]
    [InlineData("max users")]
    [InlineData("خريطة")]
    [InlineData("ab\n")]
    [InlineData("ab ")]
    public void Any_other_key_is_refused(string key) =>
        Assert.Equal(FeatureRules.Invalid, Problems(key, Switch())["key"]);

    [Fact]
    public void A_key_has_two_to_sixty_four_characters()
    {
        Assert.Empty(FeatureRules.Check("a" + new string('b', 63), Switch()));
        Assert.Equal(FeatureRules.Invalid, Problems("a" + new string('b', 64), Switch())["key"]);
        Assert.Equal(FeatureRules.Required, Problems("", Switch())["key"]);
        Assert.Equal(FeatureRules.Required, Problems(null, Switch())["key"]);
    }

    [Fact]
    public void Editing_asks_no_key()
    {
        Assert.Empty(FeatureRules.Check(null, Switch(), editing: true));
    }

    [Fact]
    public void Both_names_are_required_and_at_most_a_hundred_and_twenty()
    {
        Assert.Equal(FeatureRules.Required, Problems("k_a", Switch() with { NameAr = "  " })["nameAr"]);
        Assert.Equal(FeatureRules.Required, Problems("k_a", Switch() with { NameEn = null })["nameEn"]);
        Assert.Empty(FeatureRules.Check("k_a", Switch() with { NameAr = new string('ن', 120), NameEn = new string('n', 120) }));
        Assert.Equal(FeatureRules.TooLong, Problems("k_a", Switch() with { NameEn = new string('n', 121) })["nameEn"]);
    }

    [Fact]
    public void Descriptions_are_at_most_five_hundred()
    {
        Assert.Empty(FeatureRules.Check("k_a", Switch() with { DescriptionAr = new string('و', 500), DescriptionEn = new string('d', 500) }));
        Assert.Equal(FeatureRules.TooLong, Problems("k_a", Switch() with { DescriptionAr = new string('و', 501) })["descriptionAr"]);
    }

    [Fact]
    public void Group_kind_specialty_and_status_come_from_their_lists()
    {
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Switch(group: "billing"))["group"]);
        Assert.Equal(FeatureRules.Required, Problems("k_a", Switch() with { Group = null })["group"]);
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Switch() with { Kind = "counter" })["kind"]);
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Switch() with { Specialty = "surgery" })["specialty"]);
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Switch() with { Status = "done" })["status"]);
        Assert.Empty(FeatureRules.Check("k_a", Switch() with { Specialty = Specialties.Dental, Status = FeatureStatuses.Built }));
    }

    [Fact]
    public void A_limit_needs_both_units_of_at_most_forty_and_a_baseline_in_range_or_none()
    {
        Assert.Empty(FeatureRules.Check("k_a", Limit()));
        Assert.Empty(FeatureRules.Check("k_a", Limit(baseline: null)));
        Assert.Empty(FeatureRules.Check("k_a", Limit(baseline: 1_000_000)));
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Limit(baseline: 1_000_001))["baseline"]);
        Assert.Equal(FeatureRules.Invalid, Problems("k_a", Limit(baseline: -1))["baseline"]);
        Assert.Equal(FeatureRules.TooLong, Problems("k_a", Limit() with { UnitEn = new string('u', 41) })["unitEn"]);

        Assert.Equal(FeatureRules.UnitsRequired, FeatureRules.Shape(Limit() with { UnitAr = " " }));
        Assert.Equal(FeatureRules.UnitsRequired, FeatureRules.Shape(Limit() with { UnitEn = null }));
        Assert.Null(FeatureRules.Shape(Limit()));
    }

    [Fact]
    public void A_switch_takes_no_unit_and_no_baseline()
    {
        Assert.Null(FeatureRules.Shape(Switch()));
        Assert.Equal(FeatureRules.SwitchHasLimit, FeatureRules.Shape(Switch() with { UnitAr = "وحدة" }));
        Assert.Equal(FeatureRules.SwitchHasLimit, FeatureRules.Shape(Switch() with { Baseline = 0 }));
    }

    [Fact]
    public void A_new_feature_is_upcoming_sold_not_retired_at_its_order_and_version_one()
    {
        var feature = Feature.Create("allow_whatsapp", Switch() with { NameAr = "  قناة واتساب " }, sortOrder: 20);

        Assert.Equal(("allow_whatsapp", "قناة واتساب", FeatureStatuses.Upcoming), (feature.Key, feature.NameAr, feature.Status));
        Assert.True(feature.IsSold);
        Assert.False(feature.IsRetired);
        Assert.Equal((20, 1), (feature.SortOrder, feature.Version));
        Assert.False(feature.IsMonthly);
    }

    [Fact]
    public void A_broken_feature_is_never_made()
    {
        Assert.Throws<ArgumentException>(() => Feature.Create("Bad", Switch(), 10));
        Assert.Throws<ArgumentException>(() => Feature.Create("k_a", Switch() with { UnitAr = "x" }, 10));
        Assert.Throws<ArgumentException>(() => Feature.Create("k_a", Limit() with { UnitEn = null }, 10));
    }

    [Fact]
    public void Editing_raises_the_version_keeps_the_key_and_moves_to_the_new_group_s_end()
    {
        var feature = Feature.Create("k_a", Switch(), 10);

        feature.Edit(Switch(group: FeatureGroups.Growth) with { NameEn = "Renamed" }, sortOrder: 70);

        Assert.Equal(("k_a", "Renamed", FeatureGroups.Growth, 70, 2), (feature.Key, feature.NameEn, feature.Group, feature.SortOrder, feature.Version));
    }

    [Fact]
    public void A_limit_edited_into_a_switch_must_lose_its_units_and_baseline()
    {
        var feature = Feature.Create("k_a", Limit(), 10);

        Assert.Throws<ArgumentException>(() => feature.Edit(Switch() with { UnitAr = "رسالة" }, null));
        feature.Edit(Switch(), null);
        Assert.Equal((FeatureKinds.Switch, (string?)null, (int?)null), (feature.Kind, feature.UnitAr, feature.Baseline));
    }

    [Fact]
    public void Only_a_sold_feature_is_retired_and_only_a_retired_one_sold_again_neither_raising_the_version()
    {
        var feature = Feature.Create("k_a", Switch(), 10);

        feature.Retire();
        Assert.True(feature.IsRetired);
        Assert.Equal(1, feature.Version);
        Assert.Throws<InvalidOperationException>(feature.Retire);

        feature.Resume();
        Assert.False(feature.IsRetired);
        Assert.True(feature.IsSold);
        Assert.Throws<InvalidOperationException>(feature.Resume);

        feature.Retire();
        feature.Resume();
        Assert.Equal(1, feature.Version);

        var notSold = Feature.Create("k_b", Switch() with { IsSold = false }, 10);
        Assert.Throws<InvalidOperationException>(notSold.Retire);
    }

    [Fact]
    public void Sold_cannot_be_turned_off_a_retired_feature()
    {
        var feature = Feature.Create("k_a", Switch(), 10);
        feature.Retire();

        Assert.Throws<InvalidOperationException>(() => feature.Edit(Switch() with { IsSold = false }, null));
        feature.Edit(Switch() with { NameEn = "Still retired" }, null);
        Assert.True(feature.IsRetired);
    }

    [Fact]
    public void The_monthly_list_is_the_four_and_the_specialty_decides_one()
    {
        Assert.Equal(["message_credits", "ai_xray_analysis", "ai_voice_notes", "teleconsult_minutes"], FeatureRules.MonthlyKeys);
        Assert.Equal("specialty_exam_modules", FeatureRules.SpecialtyDecidedKey);
        Assert.True(Feature.Create("message_credits", Limit(), 10).IsMonthly);
    }

    [Fact]
    public void The_seven_groups_and_the_fifteen_specialties_are_in_their_order()
    {
        Assert.Equal(["core", "channels", "reports", "money", "clinical", "ai", "growth"], FeatureGroups.All);
        Assert.Equal(
            [
                "dental", "orthopedics", "neurology", "pediatrics", "internal_medicine", "general_surgery", "dermatology", "obgyn",
                "ophthalmology", "ent", "urology", "cardiology", "psychiatry", "nutrition", "physiotherapy",
            ],
            Specialties.All);
    }
}
