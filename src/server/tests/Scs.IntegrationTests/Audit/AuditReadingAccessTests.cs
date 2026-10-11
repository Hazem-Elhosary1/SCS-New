using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 6 (FR-030, FR-034; constitution II and III): the trail is read only by
/// someone who holds «عرض سجل الأنشطة», and only their own clinic's. Written before the address existed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditReadingAccessTests(SqlServerFixture sql)
{
    private static readonly Uri Entries = new("/v1/audit/entries", UriKind.Relative);

    [Fact]
    public async Task With_no_session_it_answers_unauthenticated()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();

        using var response = await nobody.GetAsync(Entries);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Without_the_permission_it_is_refused_naming_the_permission()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var staff = await world.Factory.SignInWithAsync(
            world.Clinic, Fixtures.Access.AllCodesExcept(Permissions.ReportsActivity));

        using var response = await staff.Browser.GetAsync(Entries);
        var (status, code, body) = await response.ReadAsync();

        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
        Assert.Equal(Permissions.ReportsActivity, body.GetProperty("permission").GetString());
        Assert.False(body.TryGetProperty("entries", out _));
    }

    [Fact]
    public async Task A_refusal_does_not_depend_on_what_was_asked()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var staff = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        // Nonsense that a holder would be told about is not looked at for someone who is not one.
        using var response = await staff.Browser.GetAsync(new Uri("/v1/audit/entries?category=everything&page=0", UriKind.Relative));

        var (status, code, _) = await response.ReadAsync();

        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
    }

    [Fact]
    public async Task With_the_permission_alone_it_answers()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var staff = await world.Factory.SignInWithAsync(world.Clinic, Permissions.ReportsActivity);

        using var response = await staff.Browser.GetAsync(Entries);
        var (status, _, body) = await response.ReadAsync();

        Assert.Equal(200, status);

        // Their own sign-in is the one thing a person of this clinic has done.
        var entry = Assert.Single(body.GetProperty("entries").EnumerateArray());
        Assert.Equal(AuditActions.SignInSucceeded, entry.GetProperty("action").GetProperty("code").GetString());
        Assert.Equal(staff.Email, entry.GetProperty("actor").GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_owner_and_the_manager_hold_it_and_the_doctor_does_not()
    {
        Assert.Contains(Permissions.ReportsActivity, ReadySets.Manager);
        Assert.DoesNotContain(Permissions.ReportsActivity, ReadySets.Doctor);

        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        using var doctor = await world.Factory.SignInWithRoleAsync(world.Clinic, await world.Factory.ReadyRoleAsync(world.Clinic, "doctor"));

        using var forOwner = await owner.Browser.GetAsync(Entries);
        using var forDoctor = await doctor.Browser.GetAsync(Entries);

        Assert.Equal(HttpStatusCode.OK, forOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forDoctor.StatusCode);
    }

    [Fact]
    public async Task The_address_declares_the_permission_it_needs()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var route = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/v1/audit/entries");
        var rule = route.Metadata.GetMetadata<AccessRuleMetadata>();

        Assert.Equal((AccessRuleKind.Permission, Permissions.ReportsActivity), (rule?.Kind, rule?.Permission));
        Assert.Equal(["GET"], route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public async Task A_holder_in_one_clinic_never_receives_an_entry_of_another_whatever_is_asked()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var now = world.Factory.Clock.GetUtcNow();

        // The other clinic's trail: a person's sign-in and a deleted role.
        var theirPerson = Guid.NewGuid();
        var theirs = new[]
        {
            await sql.Main.PutEntryAsync(other, AuditActions.SignInSucceeded, now.AddHours(-2), actor: theirPerson, actorName: "zzq.other@clinic.example", target: theirPerson, targetKind: "account"),
            await sql.Main.PutEntryAsync(other, AuditActions.RoleDeleted, now.AddHours(-1), actor: theirPerson, actorName: "zzq.other@clinic.example", targetKind: "role", targetName: "دورهم"),
        };

        using var staff = await world.Factory.SignInWithAsync(world.Clinic, Permissions.ReportsActivity);

        string[] questions =
        [
            "?size=100",
            $"?actor={theirPerson}",
            "?action=role.deleted",
            "?action=signin.succeeded&size=100",
            "?q=zzq.other",
            "?category=users",
            $"?from={Uri.EscapeDataString(now.AddDays(-300).UtcDateTime.ToString("o"))}&size=100",
        ];

        foreach (var question in questions)
        {
            using var response = await staff.Browser.GetAsync(new Uri("/v1/audit/entries" + question, UriKind.Relative));
            var (status, _, body) = await response.ReadAsync();
            var text = body.GetRawText();

            Assert.Equal(200, status);
            Assert.All(theirs, id => Assert.DoesNotContain(id.ToString(), text, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("zzq.other", text, StringComparison.Ordinal);
            Assert.DoesNotContain("دورهم", text, StringComparison.Ordinal);
            Assert.True(body.GetProperty("total").GetInt32() <= 1, question);
        }
    }
}
