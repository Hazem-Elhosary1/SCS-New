namespace Scs.SharedKernel;

/// <summary>
/// Marks an entity that belongs to a clinic. Such an entity is isolated in the database from
/// the migration that creates its table (specs/002-clinic-tenancy-isolation/contracts/isolation.md).
/// Do not assign <see cref="ClinicId"/>: it is stamped from the clinic context when saved.
/// </summary>
public interface IClinicOwned
{
    Guid ClinicId { get; }
}
