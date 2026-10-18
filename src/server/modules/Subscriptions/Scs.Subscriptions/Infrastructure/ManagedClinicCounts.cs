using Scs.BuildingBlocks.Security;
using Scs.Operators.Contracts;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// «عيادات تم إدارتها» on the operator's page (spec 015, FR-030; spec 011's clarification): the
/// visible clinics whose account manager the operator is now.
/// </summary>
internal sealed class ManagedClinicCounts(IClinicRegistry registry, ICurrentOperator current) : IManagedClinicCounts
{
    public async Task<int?> CountAsync(Guid operatorId, CancellationToken cancellation)
    {
        var clinics = await registry.ListAsync(current.OperatorId.ToString(), cancellation).ConfigureAwait(false);
        return clinics.Count(c => c.AccountManagerId == operatorId);
    }
}
