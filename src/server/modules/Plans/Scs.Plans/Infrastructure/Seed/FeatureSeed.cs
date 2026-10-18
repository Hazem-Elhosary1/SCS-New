using System.Globalization;
using System.Text;
using Scs.Plans.Domain;
using Scs.SharedKernel.Clinics;

namespace Scs.Plans.Infrastructure.Seed;

/// <summary>
/// The 35 features the catalogue starts with (spec 012, FR-022; BRD §7-19, OP-A-222;
/// data-model.md "The seed"), every one «قادمة» (FR-024). Applied once by the migration
/// <c>SeedFeatures</c>, each row only where its key is absent, so an operator's edit is never
/// overwritten (FR-023). This list is frozen with that migration: a later release that seeds
/// more features does so in a migration of its own, and a spec that builds a feature marks it
/// built there (docs/database-changes.md).
/// </summary>
internal static class FeatureSeed
{
    private sealed record Row(
        string Key, string Group, string NameAr, string NameEn, string Kind = FeatureKinds.Switch, string? UnitAr = null,
        string? UnitEn = null, int? Baseline = null, string? Specialty = null, bool IsSold = true);

    private static readonly Row[] Rows =
    [
        new("core_clinic", FeatureGroups.Core, "الحجوزات والمرضى والكشف والروشتة", "Bookings, patients, exams & prescriptions", IsSold: false),
        new("max_users", FeatureGroups.Core, "المستخدمون", "Users", FeatureKinds.Limit, "مستخدم", "users"),
        new("max_doctors", FeatureGroups.Core, "الأطباء", "Doctors", FeatureKinds.Limit, "طبيب", "doctors", 1),
        new("max_branches", FeatureGroups.Core, "الفروع", "Branches", FeatureKinds.Limit, "فرع", "branches", 1),

        new("allow_whatsapp", FeatureGroups.Channels, "قناة واتساب", "WhatsApp channel"),
        new("allow_telegram", FeatureGroups.Channels, "قناة تيليجرام", "Telegram channel"),
        new("bot_assistant", FeatureGroups.Channels, "مساعد الحجز", "Booking assistant"),
        new("message_credits", FeatureGroups.Channels, "رصيد الرسائل", "Message credits", FeatureKinds.Limit, "رسالة/شهر", "msgs / mo", 0),

        new("allow_analytics", FeatureGroups.Reports, "التقارير المالية والطبية", "Financial & medical reports"),
        new("allow_custom_branding", FeatureGroups.Reports, "العلامة التجارية المخصصة", "Custom branding"),

        new("allow_insurance", FeatureGroups.Money, "التأمين", "Insurance"),
        new("allow_refunds", FeatureGroups.Money, "الاسترداد", "Refunds"),
        new("itemized_invoice", FeatureGroups.Money, "فاتورة المريض بالبنود", "Itemised patient invoice"),
        new("doctor_statement", FeatureGroups.Money, "كشف حساب الطبيب", "Doctor statement"),
        new("deposit_policy", FeatureGroups.Money, "العربون ورسوم عدم الحضور", "Deposit & no-show fee"),

        new("specialty_exam_modules", FeatureGroups.Clinical, "وحدات الكشف بحسب التخصص", "Specialty exam modules", IsSold: false),
        new("dental_chart", FeatureGroups.Clinical, "خريطة الأسنان", "Dental chart", Specialty: Specialties.Dental),
        new("treatment_plans", FeatureGroups.Clinical, "خطط العلاج", "Treatment plans"),
        new("dental_lab", FeatureGroups.Clinical, "معمل الأسنان", "Dental lab", Specialty: Specialties.Dental),
        new("drug_directory", FeatureGroups.Clinical, "دليل الأدوية", "Drug directory"),
        new("informed_consent", FeatureGroups.Clinical, "الموافقة المستنيرة", "Informed consent"),
        new("licences_registry", FeatureGroups.Clinical, "ملف الرخص والتغطية", "Licences & coverage"),
        new("inventory", FeatureGroups.Clinical, "المخزون والمستهلكات", "Inventory & consumables"),
        new("sick_leave_referral", FeatureGroups.Clinical, "الإجازة المرضية وخطاب التحويل", "Sick leave & referral letter"),
        new("lab_orders", FeatureGroups.Clinical, "طلبات التحاليل والأشعة", "Lab & imaging orders"),

        new("ai_xray_analysis", FeatureGroups.Ai, "تحليل الأشعة بالذكاء الاصطناعي", "X-ray AI analysis", FeatureKinds.Limit, "تحليل/شهر", "scans / mo", 0),
        new("ai_voice_notes", FeatureGroups.Ai, "كتابة الملاحظة بالصوت", "Voice notes", FeatureKinds.Limit, "دقيقة/شهر", "min / mo", 0),

        new("reminders_confirm", FeatureGroups.Growth, "التذكيرات و«أكّد أو ألغِ»", "Reminders & confirm-or-release"),
        new("queue_screen", FeatureGroups.Growth, "شاشة الدور ورابط «دورك»", "Queue screen & \"your turn\" link", FeatureKinds.Limit, "شاشة", "screens", 0),
        new("public_booking", FeatureGroups.Growth, "صفحة الحجز العامة", "Public booking page"),
        new("recall_list", FeatureGroups.Growth, "قائمة المتابعة", "Recall list"),
        new("weekly_summary", FeatureGroups.Growth, "الملخص الأسبوعي للمالك", "Owner weekly summary"),
        new("teleconsult", FeatureGroups.Growth, "الكشف عن بُعد", "Teleconsult"),
        new("teleconsult_minutes", FeatureGroups.Growth, "دقائق الكشف عن بُعد", "Teleconsult minutes", FeatureKinds.Limit, "دقيقة/شهر", "min / mo", 0),
        new("patient_feedback", FeatureGroups.Growth, "تقييم المريض بعد الزيارة", "Patient feedback"),
    ];

    /// <summary>The seed as features: each at its group's next order, 10, 20, 30 ….</summary>
    public static IReadOnlyList<Feature> Features()
    {
        var next = new Dictionary<string, int>(StringComparer.Ordinal);
        return
        [
            .. Rows.Select(row =>
            {
                next[row.Group] = next.GetValueOrDefault(row.Group) + FeatureRules.OrderStep;
                return Feature.Create(
                    row.Key,
                    new FeatureFields(
                        row.NameAr, row.NameEn, null, null, row.Group, row.Kind, row.UnitAr, row.UnitEn, row.Baseline, row.Specialty,
                        FeatureStatuses.Upcoming, row.IsSold),
                    next[row.Group]);
            }),
        ];
    }

    /// <summary>One statement per feature, inserting it only where its key is absent (FR-023).</summary>
    public static IEnumerable<string> Statements() => Features().Select(Statement);

    private static string Statement(Feature f)
    {
        var sql = new StringBuilder();
        sql.Append(CultureInfo.InvariantCulture, $"IF NOT EXISTS (SELECT 1 FROM [plans].[Feature] WHERE [Key] = {Text(f.Key)}) ");
        sql.Append("INSERT INTO [plans].[Feature] ([Key], [NameAr], [NameEn], [DescriptionAr], [DescriptionEn], [Group], [Kind], [UnitAr], [UnitEn], ");
        sql.Append("[Baseline], [Specialty], [Status], [IsSold], [IsRetired], [SortOrder], [Version]) VALUES (");
        sql.AppendJoin(
            ", ",
            Text(f.Key), Text(f.NameAr), Text(f.NameEn), "NULL", "NULL", Text(f.Group), Text(f.Kind), Text(f.UnitAr), Text(f.UnitEn),
            f.Baseline?.ToString(CultureInfo.InvariantCulture) ?? "NULL", Text(f.Specialty), Text(f.Status), f.IsSold ? "1" : "0", "0",
            f.SortOrder.ToString(CultureInfo.InvariantCulture), "1");
        sql.Append(");");
        return sql.ToString();
    }

    private static string Text(string? value) => value is null ? "NULL" : $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
