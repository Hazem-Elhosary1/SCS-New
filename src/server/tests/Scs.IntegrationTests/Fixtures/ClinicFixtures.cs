using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>
/// What the tests of spec 015 share: the clinics' address, a body for adding one, and reads of the
/// rows an add leaves, made through the crossing login (clinic-owned rows read as admin show
/// nothing: isolation).
/// </summary>
public static class ClinicFixtures
{
    public const string ClinicsPath = "/v1/ops/clinics";

    public static string[] Viewer { get; } = [OperatorPermissions.ClinicsView];

    public static string[] Editor { get; } = [OperatorPermissions.ClinicsView, OperatorPermissions.ClinicsEdit];

    /// <summary>Signs in a new operator who may edit clinics, and says who it is.</summary>
    public static async Task<(Guid Id, HttpClient Browser)> SignInEditorAsync(this ApiFactory factory)
    {
        var (id, email, _) = await factory.CreateOperatorWithAsync(Editor);
        return (id, await factory.SignInOperatorAsync(email));
    }

    /// <summary>An email nobody else's test uses.</summary>
    public static string NewEmail() => $"owner-{Guid.NewGuid():N}@clinic.example";

    /// <summary>A body for adding a clinic, with what a test changes.</summary>
    public static Dictionary<string, object?> NewClinic(Guid planId, string? nameEn = null, string? email = null) =>
        new(StringComparer.Ordinal)
        {
            ["nameAr"] = "عيادة الاختبار",
            ["nameEn"] = nameEn ?? $"Test Clinic {Guid.NewGuid():N}"[..30],
            ["ownerName"] = "هيثم محمود",
            ["email"] = email ?? NewEmail(),
            ["mobile"] = "+20 100 123 4567",
            ["address"] = "14 شارع التحرير، القاهرة",
            ["planId"] = planId,
            ["taxNumber"] = "123456789",
            ["specialty"] = "dental",
        };

    /// <summary>Adds a clinic through the route and returns what it answers.</summary>
    public static async Task<(Guid Id, string Slug)> AddClinicAsync(this HttpClient editor, Dictionary<string, object?> body)
    {
        ArgumentNullException.ThrowIfNull(editor);
        using var response = await editor.OpsPostAsync(ClinicsPath, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"POST {ClinicsPath} answered {(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("slug").GetString()!);
    }

    /// <summary>How many rows of a table match, read across clinics.</summary>
    public static async Task<int> CountAsync(this SqlServerFixture sql, string table, string where)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM {table} WHERE {where}");
    }

    /// <summary>One value, read across clinics.</summary>
    public static Task<T?> ValueAsync<T>(this SqlServerFixture sql, string query)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return TestDatabase.ScalarAsync<T>(sql.Main.Crossing, query);
    }

    /// <summary>
    /// Every row an add of the owner's email would leave, in the five tables and the trails: zero
    /// means the add left nothing behind (SC-003).
    /// </summary>
    public static async Task<int> LeftoversAsync(this SqlServerFixture sql, string email, string nameEn)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var clinic = $"(SELECT Id FROM tenancy.Clinic WHERE NameEn = N'{nameEn.Replace("'", "''", StringComparison.Ordinal)}')";
        return await sql.CountAsync("tenancy.Clinic", $"Id IN {clinic}")
               + await sql.CountAsync("tenancy.ClinicDirectory", $"ClinicId IN {clinic}")
               + await sql.CountAsync("subscriptions.ClinicPlan", $"ClinicId IN {clinic}")
               + await sql.CountAsync("clinicdata.Branch", $"ClinicId IN {clinic}")
               + await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'")
               + await sql.CountAsync("audit.PlatformEntry", $"TargetId IN {clinic}");
    }
}
