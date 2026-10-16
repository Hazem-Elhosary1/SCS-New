using Scs.Plans.Domain;
using Scs.Plans.Infrastructure.Seed;

namespace Scs.UnitTests.Plans;

/// <summary>
/// Spec 012, User Story 5 and FR-022 to FR-024 (data-model.md "The seed"): the 35 features the
/// catalogue starts with, in their groups and order, every one upcoming.
/// </summary>
public class FeatureSeedTests
{
    private static readonly Dictionary<string, Feature> ByKey =
        FeatureSeed.Features().ToDictionary(f => f.Key, StringComparer.Ordinal);

    [Fact]
    public void The_seed_holds_the_thirty_five_keys_in_their_groups_and_order()
    {
        string[][] expected =
        [
            ["core_clinic", "max_users", "max_doctors", "max_branches"],
            ["allow_whatsapp", "allow_telegram", "bot_assistant", "message_credits"],
            ["allow_analytics", "allow_custom_branding"],
            ["allow_insurance", "allow_refunds", "itemized_invoice", "doctor_statement", "deposit_policy"],
            [
                "specialty_exam_modules", "dental_chart", "treatment_plans", "dental_lab", "drug_directory", "informed_consent",
                "licences_registry", "inventory", "sick_leave_referral", "lab_orders",
            ],
            ["ai_xray_analysis", "ai_voice_notes"],
            [
                "reminders_confirm", "queue_screen", "public_booking", "recall_list", "weekly_summary", "teleconsult",
                "teleconsult_minutes", "patient_feedback",
            ],
        ];

        var seed = FeatureSeed.Features();
        Assert.Equal(35, seed.Count);
        Assert.Equal(expected.Sum(g => g.Length), seed.Count);

        for (var g = 0; g < FeatureGroups.All.Count; g++)
        {
            var group = seed.Where(f => f.Group == FeatureGroups.All[g]).ToList();
            Assert.Equal(expected[g], group.Select(f => f.Key));
            Assert.Equal(Enumerable.Range(1, group.Count).Select(i => i * FeatureRules.OrderStep), group.Select(f => f.SortOrder));
        }
    }

    [Fact]
    public void Every_seeded_feature_starts_upcoming_with_both_names_and_passes_the_rules()
    {
        Assert.All(FeatureSeed.Features(), feature =>
        {
            Assert.Equal(FeatureStatuses.Upcoming, feature.Status);
            Assert.False(feature.IsRetired);
            Assert.False(string.IsNullOrWhiteSpace(feature.NameAr));
            Assert.False(string.IsNullOrWhiteSpace(feature.NameEn));
            Assert.Equal(1, feature.Version);
        });
    }

    [Fact]
    public void Two_are_not_sold_and_the_rest_are()
    {
        Assert.Equal(["core_clinic", "specialty_exam_modules"], FeatureSeed.Features().Where(f => !f.IsSold).Select(f => f.Key));
    }

    [Fact]
    public void The_dental_chart_and_lab_follow_dental_and_treatment_plans_every_specialty()
    {
        Assert.Equal(Specialties.Dental, ByKey["dental_chart"].Specialty);
        Assert.Equal(Specialties.Dental, ByKey["dental_lab"].Specialty);
        Assert.Null(ByKey["treatment_plans"].Specialty);
        Assert.Equal(FeatureKinds.Switch, ByKey["treatment_plans"].Kind);
        Assert.Equal(["dental_chart", "dental_lab"], FeatureSeed.Features().Where(f => f.Specialty is not null).Select(f => f.Key));
    }

    [Fact]
    public void The_limits_their_units_and_baselines_are_as_the_brd_gives_them()
    {
        (string Key, string UnitAr, string UnitEn, int? Baseline)[] limits =
        [
            ("max_users", "مستخدم", "users", null),
            ("max_doctors", "طبيب", "doctors", 1),
            ("max_branches", "فرع", "branches", 1),
            ("message_credits", "رسالة/شهر", "msgs / mo", 0),
            ("ai_xray_analysis", "تحليل/شهر", "scans / mo", 0),
            ("ai_voice_notes", "دقيقة/شهر", "min / mo", 0),
            ("queue_screen", "شاشة", "screens", 0),
            ("teleconsult_minutes", "دقيقة/شهر", "min / mo", 0),
        ];

        Assert.Equal(limits.Select(l => l.Key), FeatureSeed.Features().Where(f => f.Kind == FeatureKinds.Limit).Select(f => f.Key));
        Assert.All(limits, l => Assert.Equal((l.UnitAr, l.UnitEn, l.Baseline), (ByKey[l.Key].UnitAr, ByKey[l.Key].UnitEn, ByKey[l.Key].Baseline)));
        Assert.All(FeatureSeed.Features().Where(f => f.Kind == FeatureKinds.Switch), f => Assert.Equal((null, null, null), (f.UnitAr, f.UnitEn, (int?)f.Baseline)));
    }

    [Fact]
    public void The_four_monthly_limits_are_seeded_limits()
    {
        Assert.All(FeatureRules.MonthlyKeys, key => Assert.Equal(FeatureKinds.Limit, ByKey[key].Kind));
        Assert.Equal(FeatureRules.MonthlyKeys, FeatureSeed.Features().Where(f => f.IsMonthly).Select(f => f.Key));
    }
}
