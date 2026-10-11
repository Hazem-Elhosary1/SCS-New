using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures.Domain;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>Endpoints that exist only in tests, to exercise the host pipeline.</summary>
public sealed class ProbeEndpoints : IEndpointContributor
{
    public const string InternalDetail = @"C:\srv\app\Secret.cs SELECT * FROM platform.JobHeartbeat";

    public sealed record ProbeBody(string Name, int Count);

    /// <summary>How many times the handler of each <c>/probe/needs/&lt;code&gt;</c> route ran.</summary>
    public static ConcurrentDictionary<string, int> Runs { get; } = new(StringComparer.Ordinal);

    public static SemaphoreSlim SlowEntered { get; } = new(0);

    public static SemaphoreSlim SlowRelease { get; } = new(0);

    /// <summary>The account named in the route is the record's owner; "missing" names no record.</summary>
    public sealed class ProbeOwnerCheck : IRecordOwnerCheck
    {
        public ValueTask<bool> IsOwnerAsync(HttpContext context, CancellationToken cancellation)
        {
            ArgumentNullException.ThrowIfNull(context);
            var named = context.Request.RouteValues["accountId"] as string;
            var asking = context.User.FindFirst(StaffClaims.AccountId)?.Value;
            return ValueTask.FromResult(named is not null && named != "missing" && named == asking);
        }
    }

    /// <summary>A type with classified members, as an application DTO would have.</summary>
    public sealed record ClassifiedCustomer(
        int Id,
        [property: PersonalData] string FullName,
        [PersonalData] string Mobile,
        [property: Secret] string Token,
        string Plan);

    public void Map(IEndpointRouteBuilder root)
    {
        root.MapGet("/probe/open", () => Results.Ok(new { ok = true })).AllowAnonymous();

        // Declares nothing about who may call it: refused with no session by the fallback
        // policy, and for every signed-in person by the access gate (spec 005, FR-019).
        root.MapGet("/probe/protected", () => Results.Ok(new { ok = true }));

        // One route per permission of the catalogue, counting how often its handler ran: a
        // refusal must leave the count as it was.
        foreach (var permission in PermissionCatalogue.All)
        {
            var code = permission.Code;
            root.MapGet($"/probe/needs/{code}", () =>
                {
                    Runs.AddOrUpdate(code, 1, (_, ran) => ran + 1);
                    return Results.Ok(new { code });
                })
                .Requires(code);
        }

        // Needs a permission and names a record; only record 1 exists.
        root.MapGet("/probe/needs-record/{id}", (string id) => id == "1" ? Results.Ok(new { id }) : Results.NotFound())
            .Requires(Permissions.PatientsManage);

        // A record that belongs to an account: its owner reaches it without the permission.
        root.MapGet("/probe/own-record/{accountId}", (string accountId) => Results.Ok(new { accountId }))
            .RequiresOrOwner<ProbeOwnerCheck>(Permissions.PatientsRecordsView);

        // Role operations run as the signed-in person, as the pages of specs 026 and 027 will.
        root.MapPost("/probe/assign/{accountId:guid}/{roleId:guid}", async (
                Guid accountId, Guid roleId, Scs.Identity.Contracts.IClinicRoles roles, CancellationToken cancellation) =>
            {
                await roles.AssignAsync(accountId, roleId, cancellation);
                return Results.NoContent();
            })
            .Requires(Permissions.AdminUsersManage);

        root.MapPost("/probe/role-without-view", async (Scs.Identity.Contracts.IClinicRoles roles, CancellationToken cancellation) =>
            {
                await roles.CreateAsync("بلا عرض", null, [Permissions.AdminUsersManage], cancellation);
                return Results.NoContent();
            })
            .Requires(Permissions.AdminRolesManage);

        // Passes the gate, says so, and answers only when released: a request under way while
        // its role is changed.
        root.MapGet("/probe/slow-collect", async (CancellationToken cancellation) =>
            {
                SlowEntered.Release();
                await SlowRelease.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
                return Results.Ok(new { ok = true });
            })
            .Requires(Permissions.MoneyCollect);

        // Needs a session and no permission: what a guarded route is timed against.
        root.MapGet("/probe/reach-free", () => Results.Ok(new { ok = true })).OwnAccountOnly();

        // How wide the person asking may see through a view.
        root.MapGet("/probe/reach/{view}", async (string view, IStaffAccess access, CancellationToken cancellation) =>
                Results.Ok(new { reach = (await access.ReachOfAsync(view, cancellation)).ToString() }))
            .OwnAccountOnly();

        root.MapGet("/probe/throws", IResult () => throw new InvalidOperationException(InternalDetail))
            .AllowAnonymous();

        root.MapPost("/probe/body", (ProbeBody body) => Results.Ok(body)).AllowAnonymous();

        root.MapGet("/probe/invalid", IResult () => throw ScsProblemException.Validation(
                new Dictionary<string, string[]> { ["email"] = ["REQUIRED"] }))
            .AllowAnonymous();

        root.MapGet("/probe/missing", () => Results.NotFound()).AllowAnonymous();

        root.MapGet("/probe/items/{id}", (string id) => Results.Ok(new { id })).AllowAnonymous();

        // Reports the clinic the work was established for. Not patient-surface: the header is ignored.
        root.MapGet("/probe/clinic", (IClinicContext clinic) => Results.Ok(new { clinicId = clinic.ClinicId }))
            .AllowAnonymous();

        // The same on the patient surface, plus how many clinic records the work can see.
        root.MapGet("/probe/patient-clinic", async (IClinicContext clinic, IClinicConnection connection, CancellationToken cancellation) =>
            {
                await using var open = await connection.OpenAsync(cancellation);
                await using var command = open.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM tenancy.Clinic";
                var visible = (int)(await command.ExecuteScalarAsync(cancellation))!;
                return Results.Ok(new { clinicId = clinic.ClinicId, clinicsVisible = visible });
            })
            .AllowAnonymous()
            .ForPatientSurface();

        // Needs a signed-in person (no AllowAnonymous). Says who is asking, where the clinic came
        // from, and how many clinic records the work can see.
        root.MapGet("/probe/whoami", async (
                Scs.Identity.Contracts.ICurrentStaff staff, IClinicContext clinic, IClinicConnection connection,
                CancellationToken cancellation) =>
            {
                await using var open = await connection.OpenAsync(cancellation);
                await using var command = open.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM tenancy.Clinic";
                var visible = (int)(await command.ExecuteScalarAsync(cancellation))!;
                return Results.Ok(new
                {
                    accountId = staff.AccountId,
                    sessionId = staff.SessionId,
                    clinicId = staff.ClinicId,
                    contextClinicId = clinic.ClinicId,
                    source = clinic.Source,
                    clinicsVisible = visible,
                });
            })
            .OwnAccountOnly();

        // The same person, on an endpoint of the patient surface: the header must agree.
        root.MapGet("/probe/whoami-patient", (IClinicContext clinic) => Results.Ok(new { clinicId = clinic.ClinicId }))
            .OwnAccountOnly()
            .ForPatientSurface();

        // Logs our own objects every way a developer might, then fails with a message that
        // carries pattern-shaped data. Used by LogRedactionTests.
        root.MapPost("/probe/log", (ILoggerFactory loggers) =>
            {
                var logger = loggers.CreateLogger("Scs.Probe");
                var customer = new ClassifiedCustomer(
                    7, Leak.Name, Leak.MobileLocal, Leak.Token, "basic");
                var patient = new ProbePatient(42, Leak.Name, Leak.NationalId);

#pragma warning disable CA1848, CA1873 // Test probe: clarity over logging performance.
                logger.LogInformation("Structured customer {@Customer}", customer);
                logger.LogInformation("Customer as text {Customer}", customer);
                logger.LogInformation("Structured patient {@Patient}", patient);
                logger.LogInformation("Patient as text {Patient}", patient);
                logger.LogInformation(
                    "Free text {Note}",
                    $"mail {Leak.Email} call {Leak.MobileLocal} or {Leak.MobileInternational} id {Leak.NationalId} auth {Leak.Bearer} db {Leak.Connection}");
#pragma warning restore CA1848, CA1873

                throw new InvalidOperationException(
                    $"Failed for {Leak.Email} / {Leak.MobileInternational} / {Leak.NationalId} / {Leak.Bearer} / {Leak.Connection}");
            })
            .AllowAnonymous();
    }
}

/// <summary>Made-up personal data and secrets that must never appear in a log.</summary>
public static class Leak
{
    public const string Name = "Mariam Abdelrahman-Zzq";
    public const string NameToken = "Abdelrahman-Zzq";
    public const string MobileLocal = "01012345678";
    public const string MobileInternational = "+201012345678";
    public const string MobileDigits = "1012345678";
    public const string Email = "mariam.zzq@example.org";
    public const string NationalId = "29001011234567";
    public const string Token = "tok_zzq_5f2a9c";
    public const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6enEifQ.c2lnbmF0dXJlenpx";
    public const string Bearer = "Bearer " + Jwt;
    public const string Password = "Sup3rS3cretZzq";
    public const string Connection = "Server=db.internal;Database=scs;User Id=scs_app;Password=" + Password;

    /// <summary>Fragments that prove a leak if any appears in the captured output.</summary>
    public static IReadOnlyList<string> Needles { get; } =
    [
        NameToken, MobileDigits, Email, "mariam.zzq", NationalId, Token, Jwt, "eyJzdWIiOiJ6enEifQ", Password,
    ];
}
