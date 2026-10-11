using System.Collections.Frozen;

namespace Scs.SharedKernel.Access;

/// <summary>What a permission does (BRD Chapter 10 §3-4).</summary>
public enum PermissionKind
{
    /// <summary>Opens a destination: its item in the side bar and its page, for reading.</summary>
    View = 0,

    /// <summary>Reads something inside a destination that its view alone does not show.</summary>
    Read = 1,

    /// <summary>Changes something.</summary>
    Write = 2,

    /// <summary>Widens a view from "one's own" to "everyone's".</summary>
    Scope = 3,
}

/// <summary>One heading of the side bar under which permissions are listed.</summary>
public sealed record PermissionGroup(string Key, int Order, string NameAr, string NameEn);

/// <summary>The eleven groups, in the order of the side bar (FR-002).</summary>
public static class PermissionGroups
{
    /// <summary>Holds no permission on purpose: the home page opens for every signed-in person (ق-40).</summary>
    public static PermissionGroup Home { get; } = new("home", 1, "الرئيسية", "Home");

    public static PermissionGroup Bookings { get; } = new("bookings", 2, "الحجوزات", "Bookings");

    public static PermissionGroup Patients { get; } = new("patients", 3, "المرضى", "Patients");

    public static PermissionGroup Money { get; } = new("money", 4, "الفواتير والتحصيل", "Invoices & collection");

    public static PermissionGroup Inbox { get; } = new("inbox", 5, "الصندوق الوارد", "Inbox");

    public static PermissionGroup Tickets { get; } = new("tickets", 6, "التذاكر", "Tickets");

    public static PermissionGroup Reports { get; } = new("reports", 7, "التقارير", "Reports");

    public static PermissionGroup Admin { get; } = new("admin", 8, "الإدارة", "Administration");

    public static PermissionGroup Data { get; } = new("data", 9, "إدارة البيانات", "Data management");

    public static PermissionGroup Settings { get; } = new("settings", 10, "الإعدادات", "Settings");

    public static PermissionGroup Subscription { get; } = new("subscription", 11, "الملف والاشتراك", "Profile & subscription");

    public static IReadOnlyList<PermissionGroup> All { get; } =
        [Home, Bookings, Patients, Money, Inbox, Tickets, Reports, Admin, Data, Settings, Subscription];
}

/// <summary>One line of the catalogue. Fixed by the platform; the same for every clinic.</summary>
/// <param name="Number">1 to 83, without gaps; never reused.</param>
/// <param name="Requires">The one permission that must be held with this one, or null (SH-054).</param>
#pragma warning disable CA1711 // "Permission" is the BRD's word for exactly this; it is not a code-access permission.
public sealed record Permission(
    int Number, string Code, string NameAr, string NameEn, PermissionGroup Group, PermissionKind Kind, string? Requires);
#pragma warning restore CA1711

/// <summary>
/// The clinic console's permissions (SH-051): the single list every module reads. A clinic
/// chooses from it and cannot add to it (ق-33). Every count shown about permissions is taken
/// from here, never written by hand.
/// </summary>
public static class PermissionCatalogue
{
    /// <summary>Which catalogue this is. The operators console gets its own with spec 010 (FR-007).</summary>
    public const string Name = "clinic";

    private static readonly Permission[] Rows = PermissionRows.All;

    private static readonly FrozenDictionary<string, Permission> ByCode =
        Rows.ToFrozenDictionary(p => p.Code, StringComparer.Ordinal);

    /// <summary>In number order.</summary>
    public static IReadOnlyList<Permission> All => Rows;

    public static int Count => Rows.Length;

    public static IReadOnlyList<PermissionGroup> Groups => PermissionGroups.All;

    public static Permission? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var found) ? found : null;

    public static bool Contains(string? code) => code is not null && ByCode.ContainsKey(code);

    /// <summary>The permission, or an error naming the code: for code that must not go on with a misspelt one.</summary>
    public static Permission Require(string code) =>
        Find(code) ?? throw new ArgumentException($"'{code}' is not a permission of the catalogue.", nameof(code));

    /// <summary>
    /// The scope permission that widens this view from "one's own" to "everyone's", or null when
    /// the view has none: holding the view then shows everything.
    /// </summary>
    public static Permission? ScopeOf(string viewCode) =>
        Rows.FirstOrDefault(p => p.Kind == PermissionKind.Scope && p.Requires == viewCode);

    /// <summary>The codes given, in catalogue order; codes that are not in the catalogue are left out.</summary>
    public static IReadOnlyList<string> InOrder(IEnumerable<string> codes)
    {
        var held = codes.ToHashSet(StringComparer.Ordinal);
        return [.. Rows.Where(p => held.Contains(p.Code)).Select(p => p.Code)];
    }
}
