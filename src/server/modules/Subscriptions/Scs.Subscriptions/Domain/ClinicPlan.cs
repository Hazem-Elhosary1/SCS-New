using Scs.SharedKernel;

namespace Scs.Subscriptions.Domain;

/// <summary>
/// Which plan a clinic is on (spec 014, FR-001): one row per clinic at most, and no row is the
/// same as no plan. Set by clinic creation (015) and by a plan change (020); everything that asks
/// what a clinic has reads it.
/// </summary>
public sealed class ClinicPlan : IClinicOwned
{
    private ClinicPlan()
    {
    }

    /// <summary>The clinic, and the row's key: offered as the established clinic, checked when saved.</summary>
    public Guid ClinicId { get; private set; }

    /// <summary>The plan of spec 013, or null for none. No key across schemas: a plan that is gone reads as none.</summary>
    public Guid? PlanId { get; private set; }

    public DateTimeOffset SetAtUtc { get; private set; }

    /// <summary>Two sets at once: the second is refused rather than lost.</summary>
    public byte[] Version { get; private set; } = [];

    public static ClinicPlan Create(Guid clinicId, Guid? planId, DateTimeOffset now) =>
        new() { ClinicId = clinicId, PlanId = planId, SetAtUtc = now };

    /// <returns>Whether anything changed.</returns>
    public bool Set(Guid? planId, DateTimeOffset now)
    {
        if (PlanId == planId)
        {
            return false;
        }

        PlanId = planId;
        SetAtUtc = now;
        return true;
    }
}
