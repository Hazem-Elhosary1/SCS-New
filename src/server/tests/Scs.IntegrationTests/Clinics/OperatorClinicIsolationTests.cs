using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, FR-028 (research R4): an operator acting on one clinic works inside that clinic's own
/// isolation. A route for clinic A reads and writes A's rows only, and the work of a request for A
/// cannot be turned to B. Test-first: tenant isolation (constitution VII).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorClinicIsolationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_route_for_one_clinic_reads_and_writes_that_clinic_only()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var emailA = NewEmail();
        var emailB = NewEmail();
        var (a, _) = await editor.AddClinicAsync(NewClinic(plan, email: emailA));
        var (b, _) = await editor.AddClinicAsync(NewClinic(plan, email: emailB));

        var detail = await editor.JsonAsync($"{ClinicsPath}/{a}");
        Assert.Equal(emailA, detail.GetProperty("owner").GetProperty("email").GetString());
        Assert.DoesNotContain(emailB, detail.GetRawText(), StringComparison.Ordinal);

        var addressOfB = await sql.ValueAsync<string>($"SELECT Address FROM tenancy.Clinic WHERE Id = '{b}'");
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = detail.GetProperty("version").GetString(),
            ["nameAr"] = "عيادة الاختبار",
            ["nameEn"] = detail.GetProperty("nameEn").GetString(),
            ["ownerName"] = "هيثم محمود",
            ["email"] = emailA,
            ["mobile"] = "+201001234567",
            ["address"] = "عنوان جديد للعيادة أ",
            ["specialty"] = "dental",
            ["defaultLanguage"] = "ar",
        };
        Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{a}", body).RefusalAsync()).Status);

        Assert.Equal("عنوان جديد للعيادة أ", await sql.ValueAsync<string>($"SELECT Address FROM tenancy.Clinic WHERE Id = '{a}'"));
        Assert.Equal(addressOfB, await sql.ValueAsync<string>($"SELECT Address FROM tenancy.Clinic WHERE Id = '{b}'"));
    }

    [Fact]
    public async Task Work_for_one_clinic_cannot_be_turned_to_another()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();

        using var scope = world.Factory.Services.CreateScope();
        var clinic = scope.ServiceProvider.GetRequiredService<IClinicContext>();
        clinic.Establish(a, ClinicSource.Operator);

        var refusal = Assert.Throws<ScsProblemException>(new Action(() => clinic.Establish(b, ClinicSource.Operator)));
        Assert.Equal("CLINIC_MISMATCH", refusal.Code);
    }

    [Fact]
    public async Task A_clinic_that_is_not_there_answers_as_one_that_cannot_be_read()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(Viewer);

        var unknown = await viewer.OpsGetAsync($"{ClinicsPath}/{Guid.NewGuid()}").AnswerAsync();
        Assert.Equal((404, "NOT_FOUND"), (unknown.Status, unknown.Code));

        // A malformed id is no clinic route at all: it answers as any unknown address of the console.
        var malformed = await viewer.OpsGetAsync($"{ClinicsPath}/not-an-id").AnswerAsync();
        var nowhere = await viewer.OpsGetAsync("/v1/ops/no-such-address").AnswerAsync();
        Assert.Equal(nowhere.Status, malformed.Status);
        Assert.NotEqual(200, malformed.Status);
        using var json = JsonDocument.Parse(unknown.Text);
        Assert.False(json.RootElement.TryGetProperty("errors", out _));
    }
}
