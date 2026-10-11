using Scs.SharedKernel.Access;

namespace Scs.Identity.Infrastructure.Migrations;

/// <summary>
/// Carries into the audit trail what was recorded before the trail existed (spec 007, research
/// R2 and R12): the sign-in events that belong in it, and the roles every clinic already has.
/// The one place a module's migration writes another module's table; it moves existing data
/// once. Each statement adds only what is not there, so running it again adds nothing.
/// </summary>
internal static class TrailBackfillSql
{
    /// <summary>
    /// "Not there" for an event: no entry of the same action about the same account at the
    /// event's moment or within the few seconds after it. A carried entry has the event's own
    /// moment; an entry written with its event, from this spec on, follows it at once.
    /// </summary>
    private const string NotThere =
        """
        NOT EXISTS (
            SELECT 1 FROM [audit].[Entry] x
            WHERE x.ClinicId = e.ClinicId AND x.ActionCode = e.ActionCode AND x.TargetKind = 'account'
              AND x.TargetId = e.AccountId
              AND x.OccurredAtUtc >= e.OccurredAtUtc AND x.OccurredAtUtc < DATEADD(SECOND, 5, e.OccurredAtUtc))
        """;

    private const string Columns =
        """
        (Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category,
         TargetKind, TargetId, Facts, IpAddress, UserAgent, TraceId)
        """;

    /// <summary>
    /// The events with one entry each, done by the account's owner: a sign-in, a completed
    /// reset, a replaced temporary password, an accepted invitation.
    /// </summary>
    public const string ByTheOwner =
        $$"""
          INSERT INTO [audit].[Entry] {{Columns}}
          SELECT NEWID(), e.ClinicId, e.OccurredAtUtc, 'staff', e.AccountId, a.Email, e.ActionCode, e.Category,
                 'account', e.AccountId, NULL, e.IpAddress, LEFT(e.UserAgent, 256), e.TraceId
          FROM (
              SELECT s.ClinicId, s.AccountId, s.OccurredAtUtc, s.IpAddress, s.UserAgent, s.TraceId, m.ActionCode, m.Category
              FROM [identity].[SignInEvent] s
              JOIN (VALUES
                  ('signed-in', 'signin.succeeded', 'security'),
                  ('reset-completed', 'password.reset-completed', 'security'),
                  ('temp-password-changed', 'password.temporary-replaced', 'security'),
                  ('invitation-accepted', 'invitation.accepted', 'users')
              ) m(Kind, ActionCode, Category) ON m.Kind = s.Kind
          ) e
          LEFT JOIN [identity].[StaffAccount] a ON a.Id = e.AccountId
          WHERE {{NotThere}};
          """;

    /// <summary>
    /// A run of failed attempts is one entry, at its first attempt: a wrong password that does
    /// not follow another on that account. Nobody is known to have acted. A lock carries the
    /// moment of the attempt that caused it, so at one moment the attempt is taken first.
    /// </summary>
    public const string FailedRuns =
        $$"""
          WITH Ordered AS (
              SELECT s.ClinicId, s.AccountId, s.OccurredAtUtc, s.IpAddress, s.UserAgent, s.TraceId, s.Kind,
                     LAG(s.Kind) OVER (
                         PARTITION BY s.AccountId
                         ORDER BY s.OccurredAtUtc, CASE s.Kind WHEN 'wrong-password' THEN 0 ELSE 1 END, s.Id) AS Previous
              FROM [identity].[SignInEvent] s
              WHERE s.Kind IN ('wrong-password', 'signed-in', 'account-locked')
          )
          INSERT INTO [audit].[Entry] {{Columns}}
          SELECT NEWID(), e.ClinicId, e.OccurredAtUtc, 'staff', NULL, NULL, e.ActionCode, 'security',
                 'account', e.AccountId, NULL, e.IpAddress, LEFT(e.UserAgent, 256), e.TraceId
          FROM (
              SELECT o.ClinicId, o.AccountId, o.OccurredAtUtc, o.IpAddress, o.UserAgent, o.TraceId,
                     'signin.failed-run' AS ActionCode
              FROM Ordered o
              WHERE o.Kind = 'wrong-password' AND (o.Previous IS NULL OR o.Previous <> 'wrong-password')
          ) e
          WHERE {{NotThere}};
          """;

    /// <summary>
    /// Ended sessions: one entry for an account's sessions that ended together. A sign-out is
    /// its owner's; a reset is its owner's unless someone else is named; a lock, an expiry and a
    /// suspension are the platform's.
    /// </summary>
    public const string EndedSessions =
        $$"""
          WITH Ended AS (
              SELECT s.ClinicId, s.AccountId, s.OccurredAtUtc, s.TraceId, s.Detail, s.ActorAccountId,
                     MIN(s.IpAddress) AS IpAddress, MIN(s.UserAgent) AS UserAgent
              FROM [identity].[SignInEvent] s
              WHERE s.Kind = 'session-ended'
              GROUP BY s.ClinicId, s.AccountId, s.OccurredAtUtc, s.TraceId, s.Detail, s.ActorAccountId
          ),
          Named AS (
              SELECT n.ClinicId, n.AccountId, n.OccurredAtUtc, n.TraceId, n.Detail, n.IpAddress, n.UserAgent,
                     CASE WHEN n.Detail = 'signed-out' THEN 'session.signed-out' ELSE 'sessions.ended' END AS ActionCode,
                     COALESCE(n.ActorAccountId, CASE WHEN n.Detail IN ('signed-out', 'password-reset') THEN n.AccountId END) AS Actor
              FROM Ended n
          )
          INSERT INTO [audit].[Entry] {{Columns}}
          SELECT NEWID(), e.ClinicId, e.OccurredAtUtc,
                 CASE WHEN e.Actor IS NULL THEN 'system' ELSE 'staff' END, e.Actor, a.Email, e.ActionCode, 'security',
                 'account', e.AccountId,
                 CASE WHEN e.ActionCode = 'sessions.ended'
                      THEN CONCAT(N'{"reason":"', STRING_ESCAPE(ISNULL(e.Detail, N'ended-for-account'), 'json'), N'","scope":"',
                                  CASE WHEN e.Detail = 'expired' THEN N'one' ELSE N'all' END, N'"}')
                 END,
                 e.IpAddress, LEFT(e.UserAgent, 256), e.TraceId
          FROM Named e
          LEFT JOIN [identity].[StaffAccount] a ON a.Id = e.Actor
          WHERE {{NotThere}};
          """;

    /// <summary>Access handed over again: by whoever is named on the event, else by the platform.</summary>
    public const string ReissuedAccess =
        $$"""
          INSERT INTO [audit].[Entry] {{Columns}}
          SELECT NEWID(), e.ClinicId, e.OccurredAtUtc,
                 CASE WHEN e.ActorAccountId IS NULL THEN 'system' ELSE 'staff' END, e.ActorAccountId, a.Email, e.ActionCode, 'users',
                 'account', e.AccountId,
                 CONCAT(N'{"way":"', CASE WHEN e.Detail = 'link' THEN N'link' ELSE N'temporary-password' END, N'"}'),
                 e.IpAddress, LEFT(e.UserAgent, 256), e.TraceId
          FROM (
              SELECT s.ClinicId, s.AccountId, s.OccurredAtUtc, s.IpAddress, s.UserAgent, s.TraceId, s.Detail, s.ActorAccountId,
                     'account.access-reissued' AS ActionCode
              FROM [identity].[SignInEvent] s
              WHERE s.Kind = 'access-reissued'
          ) e
          LEFT JOIN [identity].[StaffAccount] a ON a.Id = e.ActorAccountId
          WHERE {{NotThere}};
          """;

    /// <summary>
    /// Every role a clinic already has, as created by the platform at the role's own moment
    /// (FR-027): the ready roles came with the clinic, and nobody could create another before
    /// the roles pages exist. The owner role stores no rows, so its entry lists the catalogue.
    /// </summary>
    public static string ExistingRoles { get; } =
        $$"""
          INSERT INTO [audit].[Entry]
              (Id, ClinicId, OccurredAtUtc, ActorKind, ActionCode, Category, TargetKind, TargetId, TargetName, TargetReadyKey,
               Changes, TraceId)
          SELECT NEWID(), r.ClinicId, r.CreatedAtUtc, 'system', 'role.created', 'users', 'role', r.Id, r.Name,
                 CASE WHEN r.IsRenamed = 0 THEN r.ReadyKey END,
                 CONCAT(
                     N'[{"field":"name","after":"', STRING_ESCAPE(r.Name, 'json'), N'"',
                     CASE WHEN r.IsRenamed = 0 AND r.ReadyKey IS NOT NULL THEN CONCAT(N',"afterKey":"', r.ReadyKey, N'"') END,
                     N'}',
                     CASE WHEN r.Description IS NOT NULL
                          THEN CONCAT(N',{"field":"description","after":"', STRING_ESCAPE(r.Description, 'json'), N'"}') END,
                     N',{"field":"permissions","added":[',
                     CASE WHEN r.HoldsEverything = 1
                          THEN CAST(N'{{string.Join(",", PermissionCatalogue.All.Select(p => $"\"{p.Code}\""))}}' AS nvarchar(max))
                          ELSE ISNULL((SELECT STRING_AGG(CAST(CONCAT('"', p.Code, '"') AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY p.Code)
                                       FROM [identity].[RolePermission] p WHERE p.RoleId = r.Id), N'')
                     END,
                     N'],"removed":[]}]'),
                 REPLICATE('0', 32)
          FROM [identity].[Role] r
          WHERE NOT EXISTS (
              SELECT 1 FROM [audit].[Entry] x
              WHERE x.ClinicId = r.ClinicId AND x.ActionCode = 'role.created' AND x.TargetKind = 'role' AND x.TargetId = r.Id);
          """;

    public static IReadOnlyList<string> All { get; } = [ByTheOwner, FailedRuns, EndedSessions, ReissuedAccess, ExistingRoles];
}
