# Data Model: Audit Trail

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## 1. `audit.Entry` — clinic-owned, append-only

One recorded action. Isolation policy of spec 002 in the same migration; a trigger refuses
`UPDATE` and `DELETE`.

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier (v7) | no | key |
| `ClinicId` | uniqueidentifier | no | set from the clinic context; never supplied by the caller |
| `OccurredAtUtc` | datetimeoffset | no | the platform's clock (FR-004) |
| `ActorKind` | varchar(8) | no | `staff`, `operator` or `system`; check constraint. A failed sign-in is `staff` with no `ActorAccountId`: it is about a clinic's account, and the clinic reads it |
| `ActorAccountId` | uniqueidentifier | yes | required when `ActorKind` is `staff` and the actor is known; null for `system` and for a sign-in that failed |
| `ActorName` | nvarchar(254) | yes | the display name at the moment: the email address until spec 026 (FR-003); personal data |
| `ActionCode` | varchar(64) | no | a code of the catalogue (FR-008) |
| `Category` | varchar(16) | no | `security`, `changes`, `records`, `users`; copied from the action so it can be indexed (FR-005) |
| `TargetKind` | varchar(32) | yes | `role`, `account`, … |
| `TargetId` | uniqueidentifier | yes | |
| `TargetName` | nvarchar(200) | yes | the name at the moment; null when the target is an account (FR-018) |
| `TargetReadyKey` | varchar(16) | yes | for a ready role still under its own name, so it is named in the reader's language (FR-037) |
| `Facts` | nvarchar(max) | yes | JSON object; keys within the action's allowed keys (FR-010) |
| `Changes` | nvarchar(max) | yes | JSON list of `{field, before, after}` or `{field, added, removed}`; required when the action needs before and after (FR-011) |
| `IpAddress` | varchar(45) | yes | personal data; as received, truncated |
| `UserAgent` | nvarchar(256) | yes | personal data; as received, truncated |
| `TraceId` | char(32) | no | the request's trace, to tie an entry to the operational logs |

Indexes: `(ClinicId, OccurredAtUtc DESC, Id)` including `Category`, `ActionCode`,
`ActorAccountId`, `ActorKind`; `(ClinicId, TargetKind, TargetId, OccurredAtUtc DESC)` for the
history of one thing (used from spec 026 on).

Checks: `ActorKind` in the three values; `Category` in the four; `Facts` and `Changes` are JSON
when present (`ISJSON`).

No relationship is enforced to accounts or roles: an entry must outlive what it names.

## 2. The catalogue of actions — code, `Scs.SharedKernel/Audit`

| Field | Meaning |
|---|---|
| `Code` | frozen once used (`audit-actions.lock`) |
| `Category` | one of the four |
| `NameAr`, `NameEn` | the action's name |
| `SentenceAr`, `SentenceEn` | the describing sentence, with named placeholders filled from the target, the facts and the changes; wording in [contracts/audit-trail.md](contracts/audit-trail.md) §7, from the design |
| `Facts` | the keys an entry of it may carry |
| `NeedsBeforeAfter` | true for what moves money, changes a permission or changes a plan |

Actions of this spec:

| Code | Category | Target | Facts | Before and after |
|---|---|---|---|---|
| `role.created` | users | role | — | permissions added |
| `role.updated` | users | role | — | name, description; permissions added and removed |
| `role.deleted` | users | role | — | permissions removed |
| `account.role-changed` | users | account | — | role before and after |
| `account.access-reissued` | users | account | `way` (`link`, `temporary-password`) | — |
| `invitation.accepted` | users | account | — | — |
| `signin.succeeded` | security | account | — | — |
| `signin.failed-run` | security | account | — (attempts and lock are counted when read) | — |
| `session.signed-out` | security | account | — | — |
| `sessions.ended` | security | account | `reason`, `scope` (`one`, `all`) | — |
| `password.reset-completed` | security | account | — | — |
| `password.temporary-replaced` | security | account | — | — |

Never-list of fact keys, for every action: `password`, `code`, `token`, `link`, `secret`, `key`,
`email`, `proof`, `cookie`.

## 3. What a caller passes — `AuditRecord`

| Field | Rule |
|---|---|
| `Action` | a catalogue action |
| `Target` | kind, id, and name or ready key |
| `Facts` | optional; checked against the action |
| `Changes` | optional; required by actions that need before and after |
| `Subject` | optional account the entry is about when no person acted (a failed sign-in) |

Everything else on an entry is taken by the trail from the session, the request and the clock.

Where no one is signed in yet, Identity's sign-in code builds its record with `SignInAudit`
(`ByAccount`, `ByNobody`): the actor is the account the entry is about, named by the trail as
it is called at that moment; a failed attempt has no actor. No other module may use it — an
architecture test allows the reference from `Scs.Identity` alone.

## 4. What a reader gets — an entry as words

`id`, `occurredAt`, `category`, `action` (code and name), `sentence`, `actor` (name, or
"unknown" for a failed sign-in, and whether the account still exists), `target` (kind and name),
`ipAddress`, `device`, and for `signin.failed-run`: `attempts`, `locked`.

## 5. Declarations — route metadata

| Declaration | For |
|---|---|
| `Records(code, …)` | a route that changes data and records these actions |
| `RecordsNothing(reason)` | a route that changes nothing a person would trace |
| `RecordedBySignIn()` | a route whose effect is a sign-in event carried into the trail |

Routes that exist today:

| Route | Declaration |
|---|---|
| `POST /v1/auth/sign-in`, `/sign-in/code` | `RecordedBySignIn` |
| `POST /v1/auth/sign-in/code/resend` | `RecordsNothing("sends a sign-in code again; nothing changes until it is used")` |
| `POST /v1/auth/session/renew` | `RecordedBySignIn` — a renewal that finds the session over ends it, and that is an entry |
| `POST /v1/auth/sign-out` | `RecordedBySignIn` |
| `POST /v1/auth/password/forgot` | `RecordsNothing("asks for a link; nothing changes until it is used")` |
| `POST /v1/auth/password/link` | `RecordsNothing("looks at a link without using it")` |
| `POST /v1/auth/password/link/set`, `/password/change` | `RecordedBySignIn` |
| `PUT /v1/account/preferences` | `RecordsNothing("display preferences, ق-٣٩")` |
| `POST /v1/client-errors` | `RecordsNothing("a browser's error report")` |
| development-only routes | `RecordsNothing("local and test environments only")` |
| job `identity-cleanup` | `RecordsNothing("removes expired codes, ended sessions and spent links; no record a person would trace")` |
| job `AccountEmailJob` | `RecordsNothing("sends the emails that were asked for; nothing changes until a link is used")` |
| job `platform-heartbeat` | `RecordsNothing("stamps one row to prove background work runs; nothing a person would trace")` |
| job `tenancy-purge-access-records` | `RecordsNothing("removes cross-clinic access records past their retention; logged with the count (spec 002, FR-032)")` |

## 6. Migrations

1. `audit` — `InitialAuditTrail`: the schema, `Entry`, its checks, indexes, isolation policy and
   append-only trigger. `EntriesAreNeverRemoved`: the trigger refuses a delete for everyone,
   the operator connection and the migrator included (FR-021).
2. `identity` — `SignInEventsIntoTrail`: carries the events recorded before this spec into
   `audit.Entry` (the mapping of research R2; a run's entry at each wrong password that follows
   anything but a wrong password for that account), and records the roles every existing clinic
   already has as `role.created` by the platform, dated at each role's creation. Idempotent: it
   adds only what is not there. This is the one place a module's migration writes another
   module's table; it moves existing data once and is recorded under Complexity Tracking.
