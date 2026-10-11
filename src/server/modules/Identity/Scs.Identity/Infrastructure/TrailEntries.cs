using Scs.BuildingBlocks.Audit;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.SharedKernel.Audit;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// What each sign-in event leaves in the audit trail, or that it leaves nothing (spec 007,
/// research R2). The detailed event stays in <c>identity.SignInEvent</c>; the entry is what a
/// clinic reads. Both are written by the same save, so neither exists without the other.
/// </summary>
internal static class TrailEntries
{
    public const string ScopeOne = "one";
    public const string ScopeAll = "all";
    public const string WayLink = "link";
    public const string WayTemporaryPassword = "temporary-password";

    public static AuditOrigin From(RequestOrigin origin) => new(origin.IpAddress, origin.UserAgent, origin.TraceId);

    /// <summary>Recorded although no one is signed in yet: the actor is the account itself (FR-014a).</summary>
    public static AuditRecord SignedIn(Guid accountId, RequestOrigin origin) =>
        SignInAudit.ByAccount(AuditActions.SignInSucceeded, accountId, From(origin));

    /// <summary>
    /// The first wrong password of a run. The later ones, and the lock, write nothing: they are
    /// counted into this entry when it is read, because an entry is never changed.
    /// </summary>
    public static AuditRecord FailedRun(Guid accountId, RequestOrigin origin) =>
        SignInAudit.ByNobody(AuditActions.SignInFailedRun, accountId, From(origin));

    /// <param name="byOwner">
    /// The account's owner did it, with or without a session. Otherwise the actor is whoever is
    /// signed in on this request — another person — or the platform itself (a lock, an expiry).
    /// </param>
    public static AuditRecord SessionsEnded(Guid accountId, string reason, string scope, bool byOwner, RequestOrigin origin)
    {
        if (reason == SessionEndReasons.SignedOut)
        {
            return SignInAudit.ByAccount(AuditActions.SessionSignedOut, accountId, From(origin));
        }

        var facts = new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = reason, ["scope"] = scope };

        return byOwner
            ? SignInAudit.ByAccount(AuditActions.SessionsEnded, accountId, From(origin), facts)
            : new AuditRecord(AuditActions.SessionsEnded, AuditTarget.ForAccount(accountId), facts);
    }

    /// <summary>What setting a password leaves, by the kind of sign-in event it records.</summary>
    /// <param name="way">For a re-issue: the handover method.</param>
    public static AuditRecord? PasswordSet(string eventKind, Guid accountId, string? way, RequestOrigin origin) => eventKind switch
    {
        SignInEventKinds.ResetCompleted =>
            SignInAudit.ByAccount(AuditActions.PasswordResetCompleted, accountId, From(origin)),
        SignInEventKinds.TemporaryPasswordChanged =>
            SignInAudit.ByAccount(AuditActions.PasswordTemporaryReplaced, accountId, From(origin)),
        SignInEventKinds.InvitationAccepted =>
            SignInAudit.ByAccount(AuditActions.InvitationAccepted, accountId, From(origin)),
        SignInEventKinds.AccessReissued => AccessReissued(accountId, way),
        _ => null,
    };

    /// <summary>Done by the signed-in person of the request, for someone else's account.</summary>
    public static AuditRecord AccessReissued(Guid accountId, string? method) => new(
        AuditActions.AccountAccessReissued,
        AuditTarget.ForAccount(accountId),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["way"] = method == HandoverMethods.Link ? WayLink : WayTemporaryPassword,
        });
}
