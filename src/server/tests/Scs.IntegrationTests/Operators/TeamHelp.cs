using System.Net.Http.Json;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>One stored entry of the platform's own trail, as text.</summary>
public sealed record PlatformRow(string Action, string ActorKind, Guid? Actor, string? TargetName, string Facts, string Changes);

/// <summary>What the tests of spec 011's addresses share.</summary>
public static class TeamHelp
{
    public const string Operators = "/v1/ops/operators";
    public const string Roles = "/v1/ops/roles";

    public static Guid SuperAdmin { get; } = OpsHelp.SystemRole(OperatorPermissions.SystemRoles.SuperAdmin);

    public static Guid Viewer { get; } = OpsHelp.SystemRole(OperatorPermissions.SystemRoles.Viewer);

    public static Guid BillingAdmin { get; } = OpsHelp.SystemRole(OperatorPermissions.SystemRoles.BillingAdmin);

    public static Task<HttpResponseMessage> OpsPutAsync(this HttpClient browser, string path, object body) =>
        browser.PutAsJsonAsync(path, body);

    public static async Task<JsonElement> JsonAsync(this HttpClient browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.OpsGetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"GET {path} answered {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>The status and the code of an answer, and its whole text for looking into.</summary>
    public static async Task<(int Status, string? Code, string Text)> AnswerAsync(this Task<HttpResponseMessage> sending)
    {
        ArgumentNullException.ThrowIfNull(sending);
        using var response = await sending;
        var text = await response.Content.ReadAsStringAsync();
        string? code = null;
        if (text.StartsWith('{'))
        {
            using var body = JsonDocument.Parse(text);
            code = body.RootElement.TryGetProperty("code", out var found) ? found.GetString() : null;
        }

        return ((int)response.StatusCode, code, text);
    }

    /// <summary>Only the status and the code of an answer.</summary>
    public static async Task<(int Status, string? Code)> RefusalAsync(this Task<HttpResponseMessage> sending)
    {
        var (status, code, _) = await sending.AnswerAsync();
        return (status, code);
    }

    /// <summary>A body for adding an operator, with what a test changes.</summary>
    public static Dictionary<string, object?> NewOperator(Guid roleId, string? email = null) => new(StringComparer.Ordinal)
    {
        ["fullName"] = "نور خالد",
        ["email"] = email ?? $"invited-{Guid.NewGuid():N}@heta.example",
        ["roleId"] = roleId,
        ["language"] = "ar",
        ["twoStep"] = true,
        ["sendInvitation"] = true,
    };

    public static Dictionary<string, object?> With(this Dictionary<string, object?> body, string field, object? value)
    {
        ArgumentNullException.ThrowIfNull(body);
        var copy = new Dictionary<string, object?>(body, StringComparer.Ordinal) { [field] = value };
        return copy;
    }

    /// <summary>The body an edit sends for an operator as they are now, read through the address itself.</summary>
    public static async Task<Dictionary<string, object?>> EditBodyAsync(this HttpClient browser, Guid id)
    {
        var read = await browser.JsonAsync($"{Operators}/{id}");
        string? Text(string name) => read.GetProperty(name).ValueKind == JsonValueKind.String ? read.GetProperty(name).GetString() : null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fullName"] = Text("fullName"),
            ["roleId"] = read.GetProperty("role").GetProperty("id").GetGuid(),
            ["mobile"] = Text("mobile"),
            ["department"] = Text("department"),
            ["jobTitle"] = Text("jobTitle"),
            ["language"] = Text("language"),
            ["version"] = Text("version"),
        };
    }

    public static async Task<List<PlatformRow>> PlatformEntriesAsync(this SqlServerFixture sql, Guid target, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var rows = await TestDatabase.ReadColumnAsync(
            sql.Main.Admin,
            $"""
             SELECT CONCAT(ActionCode, CHAR(31), ActorKind, CHAR(31), ActorAccountId, CHAR(31), TargetName, CHAR(31), Facts, CHAR(31), Changes)
             FROM [audit].[PlatformEntry]
             WHERE TargetId = '{target}' {(action is null ? "" : $"AND ActionCode = '{action}'")}
             ORDER BY OccurredAtUtc, Id
             """);

        return
        [
            .. rows.Select(row => row.Split('\u001f')).Select(parts => new PlatformRow(
                parts[0], parts[1], Guid.TryParse(parts[2], out var actor) ? actor : null,
                parts[3].Length == 0 ? null : parts[3], parts[4], parts[5])),
        ];
    }

    public static Task<List<string>> OpsColumnAsync(this SqlServerFixture sql, string query)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return TestDatabase.ReadColumnAsync(sql.Main.Admin, query);
    }
}
