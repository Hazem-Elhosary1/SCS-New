using System.Globalization;
using System.Text;
using Scs.BuildingBlocks.Audit;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Audit.Infrastructure;

/// <summary>What a sentence is filled from, beside the action itself.</summary>
/// <param name="Target">The target's name, already in the reader's language.</param>
/// <param name="Attempts">For a run of failed sign-in attempts.</param>
internal sealed record SentenceParts(
    string? Target,
    IReadOnlyList<AuditChange> Changes,
    int Attempts = 1,
    bool Locked = false,
    int LockMinutes = 0);

/// <summary>
/// Turns a stored entry into words in the reader's language (spec 007, FR-036): the action's
/// name and one sentence in the passive, as specs/007-audit-trail/contracts/audit-trail.md §7.
/// Nothing here is stored; the same entry reads in Arabic for one reader and English for another.
/// </summary>
internal static class Sentences
{
    public const string Arabic = "ar";
    public const string English = "en";

    /// <summary>How many permissions a sentence names before it says "and n more".</summary>
    public const int NamedPermissions = 3;

    public static string NameOf(AuditAction action, string language) =>
        language == English ? action.NameEn : action.NameAr;

    public static string UnknownActor(string language) => language == English ? "Unknown" : "غير معروف";

    public static string DeletedAccount(string language) => language == English ? "deleted account" : "حساب محذوف";

    /// <summary>A ready role under its own name is named in the reader's language (FR-037).</summary>
    public static string? RoleName(string? name, string? readyKey, string language) => readyKey switch
    {
        "owner" => language == English ? "Clinic owner" : "مالك العيادة",
        "manager" => language == English ? "Clinic manager" : "مدير العيادة",
        "doctor" => language == English ? "Doctor" : "طبيب",
        _ => name,
    };

    public static string Render(AuditAction action, SentenceParts parts, string language)
    {
        var english = language == English;
        var text = new StringBuilder(english ? action.SentenceEn : action.SentenceAr);

        text.Replace("{target}", parts.Target ?? "");
        text.Replace("{attempts}", Attempts(parts.Attempts, english));
        text.Replace("{locked}", parts.Locked ? Locked(parts.LockMinutes, english) : "");

        var permissions = parts.Changes.FirstOrDefault(change => change.Field == "permissions");
        text.Replace("{count}", Permissions(permissions?.Added?.Count ?? 0, english));
        text.Replace("{changes}", RoleChanges(parts.Changes, permissions, english));

        foreach (var change in parts.Changes)
        {
            text.Replace($"{{before:{change.Field}}}", Value(change.Before, change.BeforeKey, change.Field, language));
            text.Replace($"{{after:{change.Field}}}", Value(change.After, change.AfterKey, change.Field, language));
        }

        return text.ToString();
    }

    /// <summary>A whole number as the reader's language writes it: Arabic-Indic digits in Arabic.</summary>
    public static string Number(int value, bool english)
    {
        var latin = value.ToString(CultureInfo.InvariantCulture);
        return english ? latin : string.Concat(latin.Select(digit => char.IsAsciiDigit(digit) ? (char)('٠' + (digit - '0')) : digit));
    }

    /// <summary>A person who held no role before is said to have had none.</summary>
    public static string NoRole(string language) => language == English ? "no role" : "بلا دور";

    private static string Value(string? value, string? readyKey, string field, string language) =>
        field == "role" ? RoleName(value, readyKey, language) ?? NoRole(language) : value ?? "";

    private static string Attempts(int count, bool english) => (count, english) switch
    {
        ( <= 1, true) => "1 attempt",
        (2, true) => "2 attempts",
        (_, true) => $"{Number(count, true)} attempts in a row",
        ( <= 1, false) => "محاولة واحدة",
        (2, false) => "محاولتان",
        ( <= 10, false) => $"{Number(count, false)} محاولات متتالية",
        _ => $"{Number(count, false)} محاولة متتالية",
    };

    private static string Locked(int minutes, bool english) => english
        ? $"; locked for {Number(minutes, true)} minutes"
        : $"، فقُفل {Number(minutes, false)} دقيقة";

    private static string Permissions(int count, bool english) => (count, english) switch
    {
        (1, true) => "1 permission",
        (_, true) => $"{Number(count, true)} permissions",
        (1, false) => "صلاحية واحدة",
        (2, false) => "صلاحيتين",
        ( >= 3 and <= 10, false) => $"{Number(count, false)} صلاحيات",
        _ => $"{Number(count, false)} صلاحية",
    };

    /// <summary>What an edit did to a role: a new name, permissions added, permissions removed.</summary>
    private static string RoleChanges(IReadOnlyList<AuditChange> changes, AuditChange? permissions, bool english)
    {
        var language = english ? English : Arabic;
        var said = new List<string>();

        if (changes.FirstOrDefault(change => change.Field == "name") is { Before: not null, After: not null } renamed)
        {
            var before = RoleName(renamed.Before, renamed.BeforeKey, language);
            said.Add(english ? $"renamed from “{before}”" : $"غُيِّر اسمه من «{before}»");
        }

        if (permissions?.Added is { Count: > 0 } added)
        {
            said.Add((english ? "added " : "أُضيف ") + List(added, english));
        }

        if (permissions?.Removed is { Count: > 0 } removed)
        {
            said.Add((english ? "removed " : "نُزع ") + List(removed, english));
        }

        if (said.Count == 0)
        {
            return "";
        }

        return english ? ": " + string.Join("; ", said) : ": " + string.Join("، و", said);
    }

    /// <summary>Permissions by their names in the reader's language: the first three, then how many more.</summary>
    private static string List(IReadOnlyList<string> codes, bool english)
    {
        var names = codes.Take(NamedPermissions)
            .Select(code => PermissionCatalogue.Find(code) is { } found ? (english ? found.NameEn : found.NameAr) : code);
        var listed = string.Join(english ? ", " : "، ", names);
        var more = codes.Count - NamedPermissions;

        return more <= 0
            ? listed
            : english ? $"{listed} and {Number(more, true)} more" : $"{listed} و{Number(more, false)} أخرى";
    }
}
