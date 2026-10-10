using Microsoft.AspNetCore.Http;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>Where the clinic of a piece of work came from.</summary>
public static class ClinicSource
{
    public const string PatientAddress = "patient-address";
    public const string Account = "account";
    public const string Job = "job";
}

/// <summary>
/// Which clinic the current piece of work (one request or one job) is for. Established once;
/// with none established, clinic-owned data is neither visible nor writable.
/// </summary>
public interface IClinicContext
{
    /// <summary>Null until established.</summary>
    Guid? ClinicId { get; }

    string? Source { get; }

    /// <summary>
    /// Fixes the clinic for this piece of work. The same clinic again is accepted; a different
    /// one is refused with <c>CLINIC_MISMATCH</c>.
    /// </summary>
    void Establish(Guid clinicId, string source);

    /// <summary>The clinic, or a refusal with <c>CLINIC_REQUIRED</c>.</summary>
    Guid Require();
}

public sealed class ClinicContext : IClinicContext
{
    public Guid? ClinicId { get; private set; }

    public string? Source { get; private set; }

    public void Establish(Guid clinicId, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (clinicId == Guid.Empty)
        {
            throw new ScsProblemException(
                StatusCodes.Status400BadRequest, ErrorCodes.ClinicRequired, "An empty clinic id was offered.");
        }

        if (ClinicId is { } established && established != clinicId)
        {
            throw new ScsProblemException(
                StatusCodes.Status400BadRequest,
                ErrorCodes.ClinicMismatch,
                $"Work established for one clinic ({Source}) was asked to continue for another ({source}).");
        }

        ClinicId = clinicId;
        Source ??= source;
    }

    public Guid Require() =>
        ClinicId ?? throw new ScsProblemException(
            StatusCodes.Status400BadRequest, ErrorCodes.ClinicRequired, "No clinic is established for this work.");
}
