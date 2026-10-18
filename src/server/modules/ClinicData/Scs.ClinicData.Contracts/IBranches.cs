namespace Scs.ClinicData.Contracts;

/// <summary>
/// The established clinic's branches (spec 015, FR-026; BRD Ops §3-4). Clinic creation makes the
/// main branch; spec 028 adds the others and their management.
/// </summary>
public interface IBranches
{
    /// <summary>
    /// Creates the clinic's main branch with the names and address it is given (the clinic's own at
    /// creation). Refused with <c>CLINIC_HAS_MAIN_BRANCH</c> when the clinic has one already.
    /// </summary>
    Task<Guid> CreateMainAsync(string nameAr, string nameEn, string address, CancellationToken cancellation);

    /// <summary>
    /// Removes a main branch an add made before it failed, so that nothing of the add remains
    /// (spec 015, SC-003). Only an add's undo calls it; a branch that is gone already is no refusal.
    /// </summary>
    Task UndoMainAsync(Guid branchId, CancellationToken cancellation);

    /// <summary>How many branches the established clinic has («عدد الأطباء / الفروع»).</summary>
    Task<int> CountAsync(CancellationToken cancellation);
}
