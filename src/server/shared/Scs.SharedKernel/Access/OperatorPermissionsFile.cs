using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scs.SharedKernel.Access;

/// <summary>
/// The operators' catalogue as their console reads it: one generated file, so the console and
/// the server cannot disagree about a name, a group or what a permission needs (spec 011,
/// FR-001). Written by <c>Scs.Migrator ops-catalogue</c>; a test fails when the checked-in file
/// differs from this.
/// </summary>
public static class OperatorPermissionsFile
{
    /// <summary>Where the file lives, from the repository root.</summary>
    public const string RepositoryPath = "src/web/projects/ops/src/app/access/operator-permissions.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The nine groups' names, as the role editor draws them.</summary>
    public static (string Ar, string En) GroupName(string group) => group switch
    {
        OperatorPermissions.Groups.Dashboard => ("لوحة التحكم", "Dashboard"),
        OperatorPermissions.Groups.Clinics => ("العيادات", "Clinics"),
        OperatorPermissions.Groups.Tickets => ("التذاكر", "Tickets"),
        OperatorPermissions.Groups.Notices => ("التنبيهات", "Notices"),
        OperatorPermissions.Groups.Billing => ("الباقات والاشتراكات", "Plans & Subscriptions"),
        OperatorPermissions.Groups.Operators => ("المشغلون", "Operators"),
        OperatorPermissions.Groups.Reports => ("التقارير", "Reports"),
        OperatorPermissions.Groups.Security => ("الأمان", "Security"),
        OperatorPermissions.Groups.Settings => ("الإعدادات", "Settings"),
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    public static string Write()
    {
        var content = new
        {
            Catalogue = "operators",
            Groups = OperatorPermissions.Groups.All.Select(group => new
            {
                Key = group,
                NameAr = GroupName(group).Ar,
                NameEn = GroupName(group).En,
            }),
            Permissions = OperatorPermissions.All.Select(permission => new
            {
                permission.Code,
                permission.Group,
                permission.NameAr,
                permission.NameEn,
                permission.Needs,
            }),
            SystemRoles = OperatorPermissions.SystemRoles.Keys.Select(key => new
            {
                Key = key,
                NameAr = OperatorPermissions.SystemRoles.NameOf(key).Ar,
                NameEn = OperatorPermissions.SystemRoles.NameOf(key).En,
            }),
        };

        return JsonSerializer.Serialize(content, Json).ReplaceLineEndings("\n") + "\n";
    }
}
