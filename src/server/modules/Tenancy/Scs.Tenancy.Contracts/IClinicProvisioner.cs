namespace Scs.Tenancy.Contracts;

/// <summary>
/// Something a module gives every clinic from the moment it exists — its ready roles, for
/// example (spec 005). The clinic registry runs each provisioner right after it registers a
/// clinic, in a scope of its own with that clinic established, so the work is done under
/// ordinary isolation and not on the operator connection. It must be safe to run again: when
/// one fails, the registration reports the failure and the work is repeated.
/// </summary>
public interface IClinicProvisioner
{
    Task ProvisionAsync(CancellationToken cancellation);
}
