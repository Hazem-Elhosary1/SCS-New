namespace Scs.Operators.Contracts;

/// <summary>The reasons another module may end an operator's sessions for (spec 010, FR-035).</summary>
public static class OperatorSessionEndings
{
    public const string Suspended = "suspended";
    public const string RoleChanged = "role-changed";
}

/// <summary>What another module may know of an operator: enough to say who did something.</summary>
public sealed record OperatorSummary(Guid Id, string FullName, string Language, string State)
{
    public override string ToString() => nameof(OperatorSummary);
}

/// <summary>Ends an operator's sessions when something about the account changes (spec 011 uses it).</summary>
public interface IOperatorSessions
{
    /// <param name="reason">One of <see cref="OperatorSessionEndings"/>.</param>
    Task<int> EndAllAsync(Guid operatorId, string reason, CancellationToken cancellation);
}

/// <summary>Finds an operator by id, for showing "who did this".</summary>
public interface IOperatorDirectory
{
    Task<OperatorSummary?> FindAsync(Guid operatorId, CancellationToken cancellation);

    /// <summary>Every active operator, ordered by name: who may be a clinic's account manager (spec 015, FR-016).</summary>
    Task<IReadOnlyList<OperatorSummary>> ActiveAsync(CancellationToken cancellation);
}

/// <summary>
/// How many clinics an operator looks after now — «عيادات تم إدارتها» on the operator's page
/// (spec 015, FR-030). The operators module reads no clinic: the module that keeps the clinics'
/// account managers answers, and until one does the figure is left out (null).
/// </summary>
public interface IManagedClinicCounts
{
    Task<int?> CountAsync(Guid operatorId, CancellationToken cancellation);
}

/// <summary>The answer before any module counts the clinics: none, so the figure is not shown.</summary>
public sealed class NoManagedClinicCounts : IManagedClinicCounts
{
    public Task<int?> CountAsync(Guid operatorId, CancellationToken cancellation) => Task.FromResult<int?>(null);
}
