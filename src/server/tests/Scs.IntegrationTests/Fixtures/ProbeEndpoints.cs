using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures.Domain;
using Scs.SharedKernel;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>Endpoints that exist only in tests, to exercise the host pipeline.</summary>
public sealed class ProbeEndpoints : IEndpointContributor
{
    public const string InternalDetail = @"C:\srv\app\Secret.cs SELECT * FROM platform.JobHeartbeat";

    public sealed record ProbeBody(string Name, int Count);

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

        // No authorization metadata at all: must be refused by the fallback policy.
        root.MapGet("/probe/protected", () => Results.Ok(new { ok = true }));

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
