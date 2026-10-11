using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Audit;

/// <summary>What an entry is about. An account is named by reference alone (FR-018).</summary>
/// <param name="ReadyKey">For a ready role still under its own name, so it is named in the reader's language.</param>
public sealed record AuditTarget(string Kind, Guid? Id, string? Name = null, string? ReadyKey = null)
{
    public const string Role = "role";
    public const string Account = "account";

    public static AuditTarget ForAccount(Guid accountId) => new(Account, accountId);
}

/// <summary>
/// One thing that changed: a single value before and after, or what was added to a set and
/// removed from it (FR-002).
/// </summary>
/// <param name="BeforeKey">When the value before is a ready role under its own name: its key (FR-037).</param>
/// <param name="AfterKey">The same, for the value after.</param>
public sealed record AuditChange(
    string Field,
    string? Before = null,
    string? After = null,
    IReadOnlyList<string>? Added = null,
    IReadOnlyList<string>? Removed = null,
    string? BeforeKey = null,
    string? AfterKey = null)
{
    public static AuditChange Of(string field, string? before, string? after) => new(field, before, after);

    public static AuditChange OfSet(string field, IEnumerable<string> added, IEnumerable<string> removed) =>
        new(field, Added: [.. added], Removed: [.. removed]);

    /// <summary>Nothing changed: the value is what it was, and nothing was added or removed.</summary>
    public bool IsEmpty =>
        string.Equals(Before, After, StringComparison.Ordinal)
        && string.Equals(BeforeKey, AfterKey, StringComparison.Ordinal)
        && Added is not { Count: > 0 }
        && Removed is not { Count: > 0 };
}

/// <summary>Where a request came from. The address and the user agent are personal data.</summary>
public sealed record AuditOrigin(
    [property: PersonalData] string? IpAddress,
    [property: PersonalData] string? UserAgent,
    string TraceId)
{
    public static AuditOrigin None { get; } = new(null, null, new string('0', 32));
}

/// <summary>
/// What a caller says about an action it is recording: which action, on what, the allowed facts,
/// and before and after. Who did it, for which clinic, from where and when are taken by the
/// trail itself and cannot be supplied (spec 007, FR-014).
/// </summary>
public sealed record AuditRecord(
    string Action,
    AuditTarget? Target,
    IReadOnlyDictionary<string, object?>? Facts = null,
    IReadOnlyList<AuditChange>? Changes = null)
{
    /// <summary>
    /// Work the platform does alone — the roles a clinic starts with, a scheduled job: the
    /// entry names the platform as the actor whoever's request it happens in (FR-015).
    /// </summary>
    public bool BySystem { get; init; }

    /// <summary>Set only through <see cref="SignInAudit"/>, where no one is signed in yet (FR-014a).</summary>
    internal SignInActor? SignIn { get; init; }
}

/// <param name="AccountId">Null for a failed attempt: nobody is known to have acted.</param>
internal sealed record SignInActor(Guid? AccountId, AuditOrigin Origin);

/// <summary>
/// Recording where no one is signed in yet — a sign-in, a failed attempt, a password set by a
/// link, an accepted invitation (FR-014a). The actor is the account the entry is about, or
/// nobody. For the sign-in part of the platform alone: an architecture test allows no other
/// module to use this type.
/// </summary>
public static class SignInAudit
{
    /// <summary>An entry about an account, done by its owner, who has no session yet or no longer.</summary>
    public static AuditRecord ByAccount(
        string action, Guid accountId, AuditOrigin origin, IReadOnlyDictionary<string, object?>? facts = null) =>
        new(action, AuditTarget.ForAccount(accountId), facts)
        {
            SignIn = new SignInActor(accountId, origin),
        };

    /// <summary>An entry about an account that nobody is known to have acted on: a failed attempt.</summary>
    public static AuditRecord ByNobody(string action, Guid accountId, AuditOrigin origin) =>
        new(action, AuditTarget.ForAccount(accountId))
        {
            SignIn = new SignInActor(null, origin),
        };
}
