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
