using System.Reflection;

namespace Scs.SharedKernel.Audit;

/// <summary>The four categories a clinic's activity log is split by (BRD Chapter 09 §7-ج-٢).</summary>
public static class AuditCategories
{
    public const string Security = "security";
    public const string Changes = "changes";
    public const string Records = "records";
    public const string Users = "users";

    public static IReadOnlyList<string> All { get; } = [Security, Changes, Records, Users];
}

/// <summary>
/// One action the audit trail can hold (spec 007, FR-006). The code never changes once used; the
/// names and the sentence are what a reader sees, in Arabic and English; <see cref="Facts"/> is
/// the white list of fact keys an entry of this action may carry.
/// </summary>
/// <param name="SentenceAr">A template: <c>{target}</c>, a fact's key, and the parts of
/// specs/007-audit-trail/contracts/audit-trail.md §7.</param>
public sealed record AuditAction(
    string Code,
    string Category,
    string NameAr,
    string NameEn,
    string SentenceAr,
    string SentenceEn,
    IReadOnlyList<string> Facts,
    bool NeedsBeforeAfter);

/// <summary>
/// The closed catalogue of audit actions (spec 007; BRD SH-092, Chapter 17 §3-11). A later spec
/// adds its actions here and to <c>audit-actions.lock</c>, and nowhere else.
/// </summary>
public static class AuditActions
{
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";
    public const string AccountRoleChanged = "account.role-changed";
    public const string AccountAccessReissued = "account.access-reissued";
    public const string InvitationAccepted = "invitation.accepted";
    public const string SignInSucceeded = "signin.succeeded";
    public const string SignInFailedRun = "signin.failed-run";
    public const string SessionSignedOut = "session.signed-out";
    public const string SessionsEnded = "sessions.ended";
    public const string PasswordResetCompleted = "password.reset-completed";
    public const string PasswordTemporaryReplaced = "password.temporary-replaced";
    public const string SettingsNotificationEmailChanged = "settings.notification-email-changed";
    public const string FileDownloaded = "file.downloaded";
    public const string FileScanRefused = "file.scan-refused";

    // Spec 011: what an operator does to the platform's own team and its roles. These are
    // written to the platform's own trail, which has no clinic.
    public const string OperatorInvited = "operator.invited";
    public const string OperatorEdited = "operator.edited";
    public const string OperatorRoleChanged = "operator.role-changed";
    public const string OperatorSuspended = "operator.suspended";
    public const string OperatorReactivated = "operator.reactivated";
    public const string OperatorLinkSent = "operator.link-sent";
    public const string OperatorUnlocked = "operator.unlocked";
    public const string OperatorRoleCreated = "opsrole.created";
    public const string OperatorRoleEdited = "opsrole.edited";
    public const string OperatorRoleDeleted = "opsrole.deleted";

    // Spec 012: the feature catalogue, written to the platform's own trail.
    public const string FeatureCreated = "feature.created";
    public const string FeatureUpdated = "feature.updated";
    public const string FeatureRetired = "feature.retired";
    public const string FeatureResumed = "feature.resumed";

    // Spec 013: the plans, written to the platform's own trail.
    public const string PlanCreated = "plan.created";
    public const string PlanUpdated = "plan.updated";
    public const string PlanDisabled = "plan.disabled";
    public const string PlanEnabled = "plan.enabled";

    /// <summary>A clinic is put on a plan, moved to another, or left with none (spec 014, FR-030).</summary>
    public const string SubscriptionPlanSet = "subscription.plan-set";

    /// <summary>Fact keys no action may allow: nothing secret and no email address is ever a fact (FR-016, FR-018).</summary>
    public static IReadOnlyList<string> NeverKeys { get; } =
        ["password", "code", "token", "link", "secret", "key", "email", "proof", "cookie"];

    public static IReadOnlyList<AuditAction> All { get; } =
    [
        new(RoleCreated, AuditCategories.Users, "إنشاء دور", "Role created",
            "أُنشئ الدور «{target}» بـ{count}", "Role “{target}” was created with {count}", [], NeedsBeforeAfter: true),
        new(RoleUpdated, AuditCategories.Users, "تعديل دور", "Role edited",
            "عُدِّل الدور «{target}»{changes}", "Role “{target}” was edited{changes}", [], NeedsBeforeAfter: true),
        new(RoleDeleted, AuditCategories.Users, "حذف دور", "Role deleted",
            "حُذف الدور «{target}»", "Role “{target}” was deleted", [], NeedsBeforeAfter: true),
        new(AccountRoleChanged, AuditCategories.Users, "تغيير دور مستخدم", "User’s role changed",
            "غُيِّر دور {target} من «{before:role}» إلى «{after:role}»",
            "{target}’s role was changed from “{before:role}” to “{after:role}”", [], NeedsBeforeAfter: true),
        new(AccountAccessReissued, AuditCategories.Users, "إعادة إصدار وسيلة الدخول", "Access re-issued",
            "أُعيد إصدار وسيلة الدخول لحساب {target}", "A way in was re-issued for {target}’s account", ["way"], NeedsBeforeAfter: false),
        new(InvitationAccepted, AuditCategories.Users, "قبول دعوة", "Invitation accepted",
            "قُبلت دعوة {target}", "{target}’s invitation was accepted", [], NeedsBeforeAfter: false),
        new(SignInSucceeded, AuditCategories.Security, "تسجيل دخول", "Sign-in",
            "سُجِّل الدخول إلى حساب {target}", "{target}’s account was signed in to", [], NeedsBeforeAfter: false),
        new(SignInFailedRun, AuditCategories.Security, "محاولة دخول فاشلة", "Failed sign-in",
            "{attempts} على حساب {target}{locked}", "{attempts} on {target}’s account{locked}", [], NeedsBeforeAfter: false),
        new(SessionSignedOut, AuditCategories.Security, "تسجيل خروج", "Sign-out",
            "سُجِّل الخروج من حساب {target}", "{target}’s account was signed out", [], NeedsBeforeAfter: false),
        new(SessionsEnded, AuditCategories.Security, "إنهاء جلسات", "Sessions ended",
            "أُنهيت جلسات {target}", "{target}’s sessions were ended", ["reason", "scope"], NeedsBeforeAfter: false),
        new(PasswordResetCompleted, AuditCategories.Security, "إعادة تعيين كلمة المرور", "Password reset",
            "أُعيد تعيين كلمة مرور {target}", "{target}’s password was reset", [], NeedsBeforeAfter: false),
        new(PasswordTemporaryReplaced, AuditCategories.Security, "استبدال كلمة المرور المؤقتة", "Temporary password replaced",
            "استُبدلت كلمة المرور المؤقتة لحساب {target}", "The temporary password of {target}’s account was replaced", [], NeedsBeforeAfter: false),
        new(SettingsNotificationEmailChanged, AuditCategories.Changes, "تغيير بريد تنبيه", "Notification email changed",
            "غُيِّر بريد «{target}» من «{before:enabled}» إلى «{after:enabled}»",
            "Email for “{target}” was changed from “{before:enabled}” to “{after:enabled}”", [], NeedsBeforeAfter: true),

        // Spec 009: a file leaving the system, and a file the check refused.
        new(FileDownloaded, AuditCategories.Records, "تنزيل ملف", "File downloaded",
            "نُزِّل الملف «{target}»", "File “{target}” was downloaded", ["kind", "record"], NeedsBeforeAfter: false),
        new(FileScanRefused, AuditCategories.Security, "رفض ملف", "File refused",
            "رُفض الملف «{target}» في الفحص الأمني", "File “{target}” was refused by the security check",
            ["kind", "reason", "record", "recordId", "uploadedBy"], NeedsBeforeAfter: false),

        // Spec 011: the operators console's own team and roles.
        new(OperatorInvited, AuditCategories.Users, "دعوة مشغل", "Operator invited",
            "دُعي {target} إلى لوحة التشغيل", "{target} was invited to the Ops Console", ["role", "systemRole", "sent", "by"], NeedsBeforeAfter: false),
        new(OperatorEdited, AuditCategories.Users, "تعديل بيانات مشغل", "Operator edited",
            "عُدِّلت بيانات {target}{changes}", "{target}’s details were edited{changes}", ["note"], NeedsBeforeAfter: true),
        new(OperatorRoleChanged, AuditCategories.Users, "تغيير دور مشغل", "Operator’s role changed",
            "غُيِّر دور المشغل {target}{changes}", "The operator {target}’s role was changed{changes}",
            ["note"], NeedsBeforeAfter: true),
        new(OperatorSuspended, AuditCategories.Users, "تعليق حساب مشغل", "Operator suspended",
            "عُلِّق حساب {target}", "{target}’s account was suspended", ["reason"], NeedsBeforeAfter: false),
        new(OperatorReactivated, AuditCategories.Users, "إعادة تفعيل حساب مشغل", "Operator reactivated",
            "أُعيد تفعيل حساب {target}", "{target}’s account was reactivated", [], NeedsBeforeAfter: false),
        new(OperatorLinkSent, AuditCategories.Security, "إرسال رابط كلمة المرور", "Password link sent",
            "أُرسل رابط كلمة المرور إلى {target}", "{target} was sent a password link", ["kind"], NeedsBeforeAfter: false),
        new(OperatorUnlocked, AuditCategories.Security, "فك قفل حساب مشغل", "Operator unlocked",
            "فُكَّ قفل حساب {target}", "{target}’s account was unlocked", [], NeedsBeforeAfter: false),
        new(OperatorRoleCreated, AuditCategories.Users, "إنشاء دور مشغلين", "Operators’ role created",
            "أُنشئ دور المشغلين «{target}»", "The operators’ role “{target}” was created", ["basedOn"], NeedsBeforeAfter: true),
        new(OperatorRoleEdited, AuditCategories.Users, "تعديل دور مشغلين", "Operators’ role edited",
            "عُدِّل دور المشغلين «{target}»{changes}", "The operators’ role “{target}” was edited{changes}", ["note"], NeedsBeforeAfter: true),
        new(OperatorRoleDeleted, AuditCategories.Users, "حذف دور مشغلين", "Operators’ role deleted",
            "حُذف دور المشغلين «{target}»", "The operators’ role “{target}” was deleted", ["note"], NeedsBeforeAfter: false),
        new(FeatureCreated, AuditCategories.Changes, "إضافة ميزة", "Feature added",
            "أُضيفت الميزة «{target}»", "Feature “{target}” was added", ["feature", "group", "kind"], NeedsBeforeAfter: true),
        new(FeatureUpdated, AuditCategories.Changes, "تعديل ميزة", "Feature edited",
            "عُدِّلت الميزة «{target}»{changes}", "Feature “{target}” was edited{changes}", ["feature"], NeedsBeforeAfter: true),
        new(FeatureRetired, AuditCategories.Changes, "إيقاف ميزة", "Feature retired",
            "أُوقف بيع الميزة «{target}»", "Feature “{target}” was retired", ["feature"], NeedsBeforeAfter: true),
        new(FeatureResumed, AuditCategories.Changes, "إعادة بيع ميزة", "Feature sold again",
            "أُعيدت الميزة «{target}» إلى البيع", "Feature “{target}” was put back on sale", ["feature"], NeedsBeforeAfter: true),
        new(PlanCreated, AuditCategories.Changes, "إضافة باقة", "Plan added",
            "أُضيفت الباقة «{target}»", "Plan “{target}” was added", ["plan", "note"], NeedsBeforeAfter: true),
        new(PlanUpdated, AuditCategories.Changes, "تعديل باقة", "Plan edited",
            "عُدِّلت الباقة «{target}»{changes}", "Plan “{target}” was edited{changes}", ["plan", "note"], NeedsBeforeAfter: true),
        new(PlanDisabled, AuditCategories.Changes, "تعطيل باقة", "Plan disabled",
            "عُطِّلت الباقة «{target}»", "Plan “{target}” was disabled", ["plan"], NeedsBeforeAfter: true),
        new(PlanEnabled, AuditCategories.Changes, "تفعيل باقة", "Plan enabled",
            "فُعِّلت الباقة «{target}»", "Plan “{target}” was enabled", ["plan"], NeedsBeforeAfter: true),
        new(SubscriptionPlanSet, AuditCategories.Changes, "تغيير باقة العيادة", "Clinic plan changed",
            "تغيّرت باقة العيادة{changes}", "The clinic’s plan was changed{changes}", ["plan"], NeedsBeforeAfter: true),
    ];

    private static readonly Dictionary<string, AuditAction> ByCode = All.ToDictionary(a => a.Code, StringComparer.Ordinal);

    public static AuditAction? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>
    /// The codes that are frozen, with their categories: <c>audit-actions.lock</c>, one
    /// <c>code category</c> per line. A code in this file is never renamed, reused or removed.
    /// </summary>
    public static IReadOnlyList<(string Code, string Category)> Locked()
    {
        var assembly = typeof(AuditActions).GetTypeInfo().Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("audit-actions.lock", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);

        return
        [
            .. reader.ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith('#'))
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(parts => (parts[0], parts[1])),
        ];
    }
}

/// <summary>Where the trail looks an action up. Replaced in a test that adds an action of its own.</summary>
public interface IAuditCatalogue
{
    AuditAction? Find(string code);
}

public sealed class AuditCatalogue : IAuditCatalogue
{
    public AuditAction? Find(string code) => AuditActions.Find(code);
}
