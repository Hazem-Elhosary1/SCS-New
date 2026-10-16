using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.Plans.Domain;
using Scs.Plans.Infrastructure.Persistence;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>A feature as the tests read it back from <c>plans.Feature</c>.</summary>
public sealed record FeatureRowRead(
    string Key, string NameAr, string NameEn, string Group, string Kind, string? UnitAr, string? UnitEn, int? Baseline,
    string? Specialty, string Status, bool IsSold, bool IsRetired, int SortOrder, int Version);

/// <summary>One entry of the platform's trail about a feature.</summary>
public sealed record FeatureEntry(string Action, string ActorKind, Guid? Actor, string? TargetName, string Facts, string Changes);

/// <summary>
/// What the tests of spec 012 share: features put straight into the catalogue, read back, and the
/// trail's entries about them. The database is shared, so each test makes features of its own key.
/// </summary>
public static class FeatureFixtures
{
    public const string Features = "/v1/ops/features";

    /// <summary>A key nobody else's test uses: <c>t_</c> and 32 hexadecimal digits.</summary>
    public static string NewKey() => $"t_{Guid.NewGuid():N}";

    /// <summary>A body for adding a switch, with what a test changes.</summary>
    public static Dictionary<string, object?> NewSwitch(string? key = null, string group = "channels") => new(StringComparer.Ordinal)
    {
        ["key"] = key ?? NewKey(),
        ["nameAr"] = "قناة تجريبية",
        ["nameEn"] = "Test channel",
        ["group"] = group,
        ["kind"] = "switch",
        ["status"] = "upcoming",
        ["isSold"] = true,
    };

    /// <summary>A body for adding a limit with both units and a baseline.</summary>
    public static Dictionary<string, object?> NewLimit(string? key = null, string group = "channels", int? baseline = 5)
    {
        var body = NewSwitch(key, group);
        body["kind"] = "limit";
        body["unitAr"] = "رسالة/شهر";
        body["unitEn"] = "msgs / mo";
        body["baseline"] = baseline;
        return body;
    }

    /// <summary>Puts a feature straight into the catalogue, at an order of the test's choosing.</summary>
    public static async Task<string> PutFeatureAsync(
        this ApiFactory factory, string? key = null, string group = FeatureGroups.Channels, bool sold = true, bool retired = false,
        string kind = FeatureKinds.Switch, int sortOrder = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(factory);
        key ??= NewKey();
        var limit = kind == FeatureKinds.Limit;
        var feature = Feature.Create(
            key,
            new FeatureFields(
                "ميزة تجريبية", "Test feature", null, null, group, kind, limit ? "وحدة" : null, limit ? "units" : null, limit ? 0 : null, null,
                FeatureStatuses.Upcoming, sold),
            sortOrder);
        if (retired)
        {
            feature.Retire();
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlansDbContext>();
        db.Features.Add(feature);
        await db.SaveChangesAsync();
        return key;
    }

    public static async Task<FeatureRowRead?> FeatureAsync(this ApiFactory factory, string key)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlansDbContext>();
        return await db.Features.AsNoTracking().Where(f => f.Key == key)
            .Select(f => new FeatureRowRead(
                f.Key, f.NameAr, f.NameEn, f.Group, f.Kind, f.UnitAr, f.UnitEn, f.Baseline, f.Specialty, f.Status, f.IsSold, f.IsRetired,
                f.SortOrder, f.Version))
            .SingleOrDefaultAsync();
    }

    /// <summary>Signs in a new operator whose role holds exactly these codes and what they need.</summary>
    public static async Task<HttpClient> SignInWithAsync(this ApiFactory factory, params string[] codes)
    {
        var (_, email, _) = await factory.CreateOperatorWithAsync(codes);
        return await factory.SignInOperatorAsync(email);
    }

    /// <summary>The trail's entries about one feature, oldest first.</summary>
    public static async Task<List<FeatureEntry>> FeatureEntriesAsync(this SqlServerFixture sql, string key, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var rows = await TestDatabase.ReadColumnAsync(
            sql.Main.Admin,
            $"""
             SELECT CONCAT(ActionCode, CHAR(31), ActorKind, CHAR(31), ActorAccountId, CHAR(31), TargetName, CHAR(31), Facts, CHAR(31), Changes)
             FROM [audit].[PlatformEntry]
             WHERE TargetKind = 'feature' AND JSON_VALUE(Facts, '$.feature') = '{key}' {(action is null ? "" : $"AND ActionCode = '{action}'")}
             ORDER BY OccurredAtUtc, Id
             """);

        return
        [
            .. rows.Select(row => row.Split('\u001f')).Select(parts => new FeatureEntry(
                parts[0], parts[1], Guid.TryParse(parts[2], out var actor) ? actor : null,
                parts[3].Length == 0 ? null : parts[3], parts[4], parts[5])),
        ];
    }
}
