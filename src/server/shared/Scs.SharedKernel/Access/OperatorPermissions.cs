// Written from specs/011-operators-roles/data-model.md §1; change the two together.
// OperatorPermissionsTests holds them equal.
namespace Scs.SharedKernel.Access;

/// <summary>One permission of the operators console: its group, its names, and what it needs.</summary>
/// <param name="Needs">The code that is kept with this one whenever a role is saved; null for none.</param>
public sealed record OperatorPermissionEntry(string Code, string Group, string NameAr, string NameEn, string? Needs = null);

/// <summary>
/// The closed catalogue of what an operator may do (spec 011, FR-001; BRD chapter 17 §2-2,
/// SH-051): 28 permissions in nine groups, named as the role editor draws them. A route of the
/// operators console names one of these; a role holds only these. Nothing here is a clinic's:
/// an operator's permission never satisfies a clinic's route, and the reverse.
/// </summary>
public static class OperatorPermissions
{
    public const string DashboardView = "dashboard.view";

    public const string ClinicsView = "clinics.view";
    public const string ClinicsEdit = "clinics.edit";
    public const string ClinicsLifecycle = "clinics.lifecycle";
    public const string ClinicsPayments = "clinics.payments";
    public const string ClinicsIntegrations = "clinics.integrations";
    public const string ClinicsUsers = "clinics.users";
    public const string ClinicsTopups = "clinics.topups";

    public const string TicketsView = "tickets.view";
    public const string TicketsViewAll = "tickets.viewAll";
    public const string TicketsReply = "tickets.reply";
    public const string TicketsClose = "tickets.close";
    public const string TicketsAssign = "tickets.assign";
    public const string TicketsCreate = "tickets.create";

    public const string NoticesSend = "notices.send";

    public const string BillingView = "billing.view";
    public const string BillingManagePlans = "billing.managePlans";

    public const string OperatorsView = "operators.view";
    public const string OperatorsManage = "operators.manage";
    public const string OperatorsManageRoles = "operators.manageRoles";

    public const string ReportsFinancial = "reports.financial";
    public const string ReportsSecurity = "reports.security";
    public const string ReportsOperators = "reports.operators";
    public const string ReportsActivity = "reports.activity";
    public const string ReportsExport = "reports.export";

    public const string SecurityManage = "security.manage";

    public const string SettingsView = "settings.view";
    public const string SettingsEdit = "settings.edit";

    /// <summary>The nine groups, in the order the role editor draws them.</summary>
    public static class Groups
    {
        public const string Dashboard = "dashboard";
        public const string Clinics = "clinics";
        public const string Tickets = "tickets";
        public const string Notices = "notices";
        public const string Billing = "billing";
        public const string Operators = "operators";
        public const string Reports = "reports";
        public const string Security = "security";
        public const string Settings = "settings";

        public static IReadOnlyList<string> All { get; } =
            [Dashboard, Clinics, Tickets, Notices, Billing, Operators, Reports, Security, Settings];
    }

    /// <summary>Every permission, in the editor's order.</summary>
    public static IReadOnlyList<OperatorPermissionEntry> All { get; } =
    [
        new(DashboardView, Groups.Dashboard, "عرض لوحة التحكم", "View Dashboard"),

        new(ClinicsView, Groups.Clinics, "عرض العيادات", "View Clinics"),
        new(ClinicsEdit, Groups.Clinics, "تعديل بيانات العيادات", "Edit Clinic Data", ClinicsView),
        new(ClinicsLifecycle, Groups.Clinics, "إيقاف العيادات وإنهاء اشتراكها", "Suspend Clinics & End Subscriptions", ClinicsView),
        new(ClinicsPayments, Groups.Clinics, "إدارة بيانات الدفع والفواتير", "Manage Payments & Invoices", ClinicsView),
        new(ClinicsIntegrations, Groups.Clinics, "إدارة تكاملات العيادة", "Manage Clinic Integrations", ClinicsView),
        new(ClinicsUsers, Groups.Clinics, "إدارة مستخدمي العيادة", "Manage Clinic Users", ClinicsView),
        new(ClinicsTopups, Groups.Clinics, "إضافة رصيد إضافي للعيادات", "Add Extra Quota to Clinics", ClinicsView),

        new(TicketsView, Groups.Tickets, "عرض التذاكر المُسندة إليّ", "View Assigned Tickets"),
        new(TicketsViewAll, Groups.Tickets, "عرض كل التذاكر", "View All Tickets", TicketsView),
        new(TicketsReply, Groups.Tickets, "الرد على التذاكر", "Reply to Tickets", TicketsView),
        new(TicketsClose, Groups.Tickets, "إغلاق وإلغاء التذاكر", "Close & Cancel Tickets", TicketsView),
        new(TicketsAssign, Groups.Tickets, "إسناد التذاكر للمشغلين", "Assign Tickets to Operators", TicketsView),
        new(TicketsCreate, Groups.Tickets, "فتح التذاكر", "Open Tickets", TicketsView),

        new(NoticesSend, Groups.Notices, "إرسال إشعارات للعيادات", "Send Notices to Clinics", ClinicsView),

        new(BillingView, Groups.Billing, "عرض الباقات والفواتير", "View Plans & Billing"),
        new(BillingManagePlans, Groups.Billing, "إدارة الباقات والاشتراكات", "Manage Plans & Subscriptions", BillingView),

        new(OperatorsView, Groups.Operators, "عرض المشغلين", "View Operators"),
        new(OperatorsManage, Groups.Operators, "إضافة وتعديل المشغلين", "Add & Edit Operators", OperatorsView),
        new(OperatorsManageRoles, Groups.Operators, "إدارة الأدوار والصلاحيات", "Manage Roles & Permissions", OperatorsView),

        new(ReportsFinancial, Groups.Reports, "عرض التقارير المالية", "View Financial Reports"),
        new(ReportsSecurity, Groups.Reports, "عرض تقارير الأمان", "View Security Reports"),
        new(ReportsOperators, Groups.Reports, "عرض تقارير أداء المشغلين", "View Operator Performance"),
        new(ReportsActivity, Groups.Reports, "عرض تقارير نشاط العيادات", "View Clinic Activity"),
        new(ReportsExport, Groups.Reports, "تصدير التقارير", "Export Reports"),

        // Not tied to the security report when a role is saved: the BRD asks for that report
        // only on the actions that start from the events log, which is their routes' rule.
        new(SecurityManage, Groups.Security, "مراجعة الأحداث الأمنية وحظر عناوين IP", "Review Security Events & Block IP Addresses"),

        new(SettingsView, Groups.Settings, "عرض الإعدادات", "View Settings"),
        new(SettingsEdit, Groups.Settings, "تعديل إعدادات المنصة", "Edit Platform Settings", SettingsView),
    ];

    private static readonly Dictionary<string, OperatorPermissionEntry> ByCode =
        All.ToDictionary(permission => permission.Code, StringComparer.Ordinal);

    /// <summary>The order of a code in the catalogue, for showing a role's permissions as it draws them.</summary>
    private static readonly Dictionary<string, int> Order =
        All.Select((permission, index) => (permission.Code, index)).ToDictionary(pair => pair.Code, pair => pair.index, StringComparer.Ordinal);

    public static bool IsKnown(string? code) => code is not null && ByCode.ContainsKey(code);

    /// <exception cref="ArgumentException">The code is not in the catalogue.</exception>
    public static OperatorPermissionEntry Require(string code) =>
        ByCode.TryGetValue(code ?? "", out var permission)
            ? permission
            : throw new ArgumentException($"'{code}' is not a permission of the operators console.", nameof(code));

    /// <summary>The codes given and every code they need, in the catalogue's order, each once.</summary>
    /// <exception cref="ArgumentException">A code is not in the catalogue.</exception>
    public static IReadOnlyList<string> WithRequired(IEnumerable<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var permission = Require(code);
            kept.Add(permission.Code);
            if (permission.Needs is { } needed)
            {
                kept.Add(needed);
            }
        }

        return InOrder(kept);
    }

    /// <summary>The codes that need the one given: while any of them is held, it cannot be dropped.</summary>
    public static IReadOnlyList<string> NeededBy(string code) =>
        [.. All.Where(permission => permission.Needs == code).Select(permission => permission.Code)];

    public static IReadOnlyList<string> InOrder(IEnumerable<string> codes) =>
        [.. codes.Distinct(StringComparer.Ordinal).OrderBy(code => Order.GetValueOrDefault(code, int.MaxValue))];

    /// <summary>The three roles the platform itself defines (FR-010): they cannot be edited or deleted.</summary>
    public static class SystemRoles
    {
        public const string SuperAdmin = "super-admin";
        public const string BillingAdmin = "billing-admin";
        public const string Viewer = "viewer";

        public static IReadOnlyList<string> Keys { get; } = [SuperAdmin, BillingAdmin, Viewer];

        /// <summary>Fixed identifiers, so that a migration and a test name the same rows.</summary>
        public static Guid IdOf(string key) => key switch
        {
            SuperAdmin => new Guid("0b11a000-0000-7000-8000-000000000001"),
            BillingAdmin => new Guid("0b11a000-0000-7000-8000-000000000002"),
            Viewer => new Guid("0b11a000-0000-7000-8000-000000000003"),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

        public static (string Ar, string En) NameOf(string key) => key switch
        {
            SuperAdmin => ("مسؤول عام", "Super Admin"),
            BillingAdmin => ("مسؤول الفوترة", "Billing Manager"),
            Viewer => ("مشاهد فقط", "Viewer Only"),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

        /// <summary>What each holds (spec 011, clarified): everything; eleven for billing; every view.</summary>
        public static IReadOnlyList<string> CodesOf(string key) => key switch
        {
            SuperAdmin => [.. All.Select(permission => permission.Code)],
            BillingAdmin =>
            [
                DashboardView, ClinicsView, ClinicsPayments, ClinicsTopups, TicketsView, TicketsReply,
                BillingView, BillingManagePlans, ReportsFinancial, ReportsExport, SettingsView,
            ],
            Viewer =>
            [
                DashboardView, ClinicsView, TicketsView, TicketsViewAll, BillingView, OperatorsView,
                ReportsFinancial, ReportsSecurity, ReportsOperators, ReportsActivity, SettingsView,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
    }
}
