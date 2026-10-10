namespace Scs.Tenancy.Domain;

/// <summary>
/// One short name a clinic has, or once had. The directory is what turns a patient address into
/// a clinic before any clinic is established, so it belongs to no clinic and holds nothing that
/// is not already in a public address. A name is the key: once given to a clinic it can never be
/// given to another, and an earlier name keeps leading to its clinic for good (FR-006).
/// </summary>
public sealed class ClinicDirectoryEntry
{
    private ClinicDirectoryEntry()
    {
    }

    public ClinicDirectoryEntry(string shortName, Guid clinicId, DateTimeOffset assignedAtUtc)
    {
        ShortName = shortName;
        ClinicId = clinicId;
        IsCurrent = true;
        AssignedAtUtc = assignedAtUtc.ToUniversalTime();
    }

    /// <summary>Canonical form.</summary>
    public string ShortName { get; private set; } = "";

    public Guid ClinicId { get; private set; }

    /// <summary>Exactly one entry per clinic is current.</summary>
    public bool IsCurrent { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public void MakeCurrent() => IsCurrent = true;

    public void Retire() => IsCurrent = false;
}

/// <summary>
/// One use of an operator's cross-clinic operation: who, what, which clinic, when. Identifiers
/// only, never clinic or patient data (FR-027). Kept for the retention period, then removed.
/// </summary>
public sealed class CrossClinicAccess
{
    public const int OperationMaxLength = 60;
    public const int OperatorMaxLength = 100;

    private CrossClinicAccess()
    {
    }

    public CrossClinicAccess(string operation, Guid? clinicId, string operatorId, string? traceId, DateTimeOffset occurredAtUtc)
    {
        Operation = operation;
        ClinicId = clinicId;
        OperatorId = operatorId.Length > OperatorMaxLength ? operatorId[..OperatorMaxLength] : operatorId;
        TraceId = traceId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public long Id { get; private set; }

    public string Operation { get; private set; } = "";

    /// <summary>Null for an operation over all clinics.</summary>
    public Guid? ClinicId { get; private set; }

    public string OperatorId { get; private set; } = "";

    public string? TraceId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
