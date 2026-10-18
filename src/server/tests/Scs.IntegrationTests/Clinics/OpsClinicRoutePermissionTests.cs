using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, FR-001 (contracts/ops-clinics-api.md): every clinic route of the operators console is
/// guarded on the server — «عرض العيادات» opens the reads, «تعديل بيانات العيادات» the form and
/// every write — whatever the console shows. Test-first: permission enforcement (constitution VII).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OpsClinicRoutePermissionTests(SqlServerFixture sql)
{
    private static readonly string Some = $"{ClinicsPath}/{Guid.NewGuid()}";

    [Fact]
    public async Task Without_the_view_permission_no_clinic_route_answers()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();
        using var stranger = await world.Factory.SignInWithAsync(Scs.SharedKernel.Access.OperatorPermissions.BillingView);

        Assert.Equal(401, (await nobody.OpsGetAsync(ClinicsPath).RefusalAsync()).Status);
        foreach (var path in new[] { ClinicsPath, Some, $"{Some}/settings", $"{ClinicsPath}/form" })
        {
            Assert.Equal((403, "PERMISSION_REQUIRED"), await stranger.OpsGetAsync(path).RefusalAsync());
        }
    }

    [Fact]
    public async Task The_view_permission_reads_and_never_writes()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(Viewer);

        // Reading is allowed: the answer is anything but a refusal of the permission.
        foreach (var path in new[] { ClinicsPath, Some, $"{Some}/settings" })
        {
            Assert.NotEqual(403, (await viewer.OpsGetAsync(path).RefusalAsync()).Status);
        }

        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsGetAsync($"{ClinicsPath}/form").RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync(ClinicsPath, NewClinic(Guid.NewGuid())).RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPutAsync(Some, new { version = "AA==" }).RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPutAsync($"{Some}/account-manager", new { version = "AA==" }).RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync($"{Some}/invitation").RefusalAsync());
    }

    [Fact]
    public async Task The_edit_permission_opens_the_form_and_the_writes()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var editor = await world.Factory.SignInWithAsync(Editor);

        Assert.Equal(200, (await editor.OpsGetAsync($"{ClinicsPath}/form").RefusalAsync()).Status);
        Assert.NotEqual(403, (await editor.OpsPostAsync($"{Some}/invitation").RefusalAsync()).Status);
        Assert.NotEqual(403, (await editor.OpsPutAsync($"{Some}/account-manager", new { version = "AA==" }).RefusalAsync()).Status);
    }
}
