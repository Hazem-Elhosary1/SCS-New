# Research: Permissions Engine

**Date**: 2026-10-11 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Decisions taken while planning. Nothing is left as "needs clarification"; what the owner still
has to confirm is listed at the end.

What exists before this spec, read from the code: every route that is not `AllowAnonymous()` is
open to **any** signed-in person (the fallback policy of spec 003). Only two such routes exist
(`GET /v1/auth/session`, `POST /v1/auth/password/change`). No route is guarded by a permission,
no account has a role, and `StaffAccount` says "role arrives with spec 005".

## R1 — Where the engine lives

**Decision**: three places, no new module.

- The catalogue — the 83 permissions, their groups, kinds and requirements, the requirement rule
  and the two ready sets — is plain code in `Scs.SharedKernel/Access`. It reads no database.
- Declaring and enforcing who may call a route is in `Scs.BuildingBlocks/Security`, beside the
  session handler and the temporary-password gate.
- Roles and who holds them are data of the `Identity` module (schema `identity`), which already
  owns accounts and sessions.

**Rationale**: every module must name codes and declare routes without referencing another
module, so those two parts belong to the shared projects. A role is a fact about an account, and
"give an account another role" must end its sessions in the same transaction (FR-014) — that is
one database context, Identity's.

**Alternatives considered**: a new `Access` module — it would need a foreign key into Identity's
account table and a second transaction to end sessions. The catalogue as a database table — a
second copy of what the code already says, and a clinic must never be able to add to it.

## R2 — The catalogue is code, generated from the BRD table once

**Decision**: one C# table (`PermissionCatalogue.All`) with one constant per code
(`Permissions.AdminRolesView`). It was produced by a script from the BRD table and is listed in
full in [contracts/permission-catalogue.md](contracts/permission-catalogue.md); the script's
checks passed: 83 rows, no gap, no duplicate code, groups 7·20·7·4·4·7·6·8·13·7, kinds 20 view ·
5 read · 56 write · 2 scope, 61 requirements all pointing to a view or read, manager 59, doctor
40, and both ready sets valid under the requirement rule.

Unit tests hold it there (FR-006): numbers 1–83 without gaps, unique codes, group counts, total,
every requirement exists and is a view or read, every constant is in the table and the reverse,
ready sets of 59 and 40 that are valid sets.

**Rationale**: the catalogue changes only with a release; typed constants make a misspelt code
a compile error, which is the cheapest form of "the build fails for a code that is not in the
catalogue" (FR-020).

## R3 — How the client gets the catalogue

**Decision**: a generated file, `core/src/lib/access/permission-catalogue.json`, checked in. It is
written by `Scs.Migrator catalogue <path>` from the C# table, and a server unit test fails when
the file differs from what the table would write.

**Rationale**: the client needs names in both languages for the two states, before and without
any request. An endpoint would have to be "open to any signed-in person", and FR-021 keeps that
kind for what a person does to their own account. A hand-written second copy would drift.

**Alternatives considered**: an endpoint (above); translation keys per permission in the i18n
files — 166 hand-kept strings that duplicate the catalogue.

## R4 — Declaring who may call a route

**Decision**: four declarations, each a piece of endpoint metadata:

| Declaration | Meaning |
|---|---|
| `.AllowAnonymous()` (as today) | open to everyone |
| `.OwnAccountOnly()` | any signed-in person; for what they do to their own account |
| `.Requires(Permissions.X)` | holders of one permission |
| `.RequiresOrOwner<TCheck>(Permissions.X)` | holders, or the owner of the record (R8) |

`.Requires` takes a constant; an unknown string throws when the route is mapped, so the
application — and every test that hosts it — does not start.

**Rationale**: the existing 20-odd anonymous routes keep their declaration; nothing is renamed.

## R5 — Enforcing: one gate after authorization

**Decision**: `UseScsAccessGate()`, placed after `UseAuthorization()`. The fallback policy of
spec 003 stays, so a request with no session still gets 401 first. For a signed-in request the
gate reads the route's declaration:

- anonymous or own-account → continue;
- a permission → continue when the person holds it, otherwise 403 `PERMISSION_REQUIRED` with
  `permission: "<code>"`;
- **nothing declared → 403 `FORBIDDEN`**, for everyone.

Order of refusals is the order of the middleware: 401 (session) → 403
`PASSWORD_CHANGE_REQUIRED` (spec 004 gate) → 401/authorization → the access gate → the handler
(FR-023). The gate runs before the handler, so nothing is looked up for a refused request and
the answer cannot depend on whether a record exists (FR-022). The refusal is logged with
account, clinic, route and code only (FR-035).

**Rationale**: the same shape as the temporary-password gate, which already proved that a
module written later is covered without opting in.

**Alternatives considered**: ASP.NET authorization policies — the refusal body is written by
the scheme's forbid handler, away from the code that knows which permission was missing, and a
policy per code is 83 registrations. A filter per route group — a route outside the group is
uncovered, which is the failure this spec exists to prevent.

## R6 — "The build fails for a route that says nothing"

**Decision**: an integration test over the real application's `EndpointDataSource` fails,
naming the route, when any route has none of the four declarations, more than one, or a code
outside the catalogue. A second test holds the own-account list to exactly the written list
(R7). A third calls an undeclared probe route as an owner and expects 403.

**Rationale**: routes exist only after the application is built, so the check has to host it;
the integration suite already does and is part of the gates.

## R7 — The own-account list

**Decision**: exactly two routes today: `GET /v1/auth/session` and `POST
/v1/auth/password/change`. The list is written in
[contracts/access-engine.md](contracts/access-engine.md) and in the test; adding a route to it
means changing both, in review.

## R8 — The record's owner

**Decision**: `.RequiresOrOwner<TCheck>(code)` where `TCheck : IRecordOwnerCheck` answers "is
the person asking the owner of the record this request names?". The gate asks it only when the
permission is missing. Not the owner → the same `PERMISSION_REQUIRED` as anywhere, whether or
not the record exists.

**Rationale**: the decision stays in the gate; a handler that forgets to check cannot open a
record. No module needs it yet; it is delivered with a probe route and its tests.

## R9 — Reading what a person may do

**Decision**: `IStaffAccess` (scoped, in BuildingBlocks): `Has(code)`, `ReachOf(viewCode)` →
`None | Own | All`, `RoleName`, `Codes`. It loads once per request, on first use, through
`IStaffAccessSource` implemented by Identity: one indexed read — the account's role and that
role's codes. Nothing is cached between requests, so a change to a role is seen by the very
next request (FR-024, clarified 11 Oct 2026).

`ReachOf` uses the catalogue: `All` when the person holds the scope permission whose requirement
is that view (`bookings.viewAll`, `tickets.viewAll`), `Own` with the view alone, `None` without.

**Rationale**: a cache would need invalidation across servers to keep "next request"; the read
is a primary-key seek and at most 83 short rows. Routes that need no permission do not pay it.

**Alternatives considered**: putting the codes in the access cookie — a change would wait for
the cookie's renewal; a memory cache with a short life — the BRD's quarter hour, which the owner
replaced with "at once".

## R10 — Storing roles

**Decision**: two clinic-owned tables, `identity.Role` and `identity.RolePermission` (one row
per code held), and a nullable `RoleId` on `StaffAccount`. Detail in
[data-model.md](data-model.md).

«مالك العيادة» stores **no** permission rows: its `HoldsEverything` flag makes its effective set
the whole catalogue. A permission added in a later release is held by it at once and nothing
else changes (FR-005, FR-009).

A stored code is checked against the catalogue by the application when a role is saved, and by a
test that fails when any stored code is not in the catalogue (the edge case of a renamed code).

**Rationale**: rows are easy to query ("which roles hold X", spec 027) and to diff for the audit
trail (spec 007). A bit mask would be one read less and unreadable in the database.

## R11 — The ready roles

**Decision**: one idempotent routine, `ReadyRoles.EnsureAsync`, creates the three for the
established clinic when the clinic has no fixed role. Since «مالك العيادة» can never be removed,
its presence means "already provisioned": a clinic that deleted «مدير العيادة» does not get it
back.

It is called from: the clinic-registration hook (R12), the local and test seed, and
`IClinicRoles.ProvisionAsync` for spec 015.

Each ready role carries a `ReadyKey` (`owner`, `manager`, `doctor`) and is stored with its
Arabic name and description from the BRD. While a ready role has not been renamed, the client
shows the name for its key in the page's language — the English frame reads "Clinic owner",
"Clinic manager", "Doctor". A custom role is shown as the clinic wrote it, in both languages.

## R12 — A new clinic gets its roles when it is registered

**Decision**: `IClinicProvisioner` in `Scs.Tenancy.Contracts`; `ClinicRegistry.RegisterAsync`
calls every registered provisioner after the clinic is saved, each in its own scope with the new
clinic established. Identity registers one that calls `ReadyRoles.EnsureAsync`. If it fails the
registration reports the failure; running it again is safe.

**Rationale**: Tenancy may not reference Identity, and registration runs on the operator
connection, which must not write Identity's clinic-owned rows (that would be a new crossing).

## R13 — Clinics and accounts that exist today (FR-036)

**Decision**: the migration that creates the tables also fills them, in SQL, as the migration
login: for every existing clinic the three ready roles with their rows, then «مالك العيادة» for
the oldest account without a role in each clinic — active accounts before others, then by
creation time. It runs once, with the deployment, and needs no application code to be running.

A migration test builds a clinic the old way, migrates, and compares its roles with those of a
clinic provisioned by `ReadyRoles.EnsureAsync`: the two must be identical.

The seed then gives explicit roles (data-model §Seed), including one custom role, «فريق
العيادة», that does not hold `admin.roles.view` — without it nobody could see the no-permission
state, because all three ready roles hold that view.

**Rationale**: a roll-out step outside the migration needs both connections at deploy time and
can be forgotten; a migration cannot.

## R14 — Operations for later specs

**Decision**: `IClinicRoles` in `Scs.Identity.Contracts` — `ProvisionAsync`, `ListAsync`,
`CreateAsync`, `UpdateAsync`, `RemoveAsync`, `AssignAsync`. Each runs for the established clinic
in one transaction, holding the application lock `roles` of that clinic, so "the last owner" and
"a role in use" cannot be raced. Refusals:

| Refusal | Answer |
|---|---|
| empty set, unknown or repeated code, missing requirement | 400, field `permissions`: `REQUIRED`, `UNKNOWN`, `DUPLICATE`, `MISSING_REQUIREMENT` (+ `missing: [{permission, requires}]`) |
| name empty, over 40, used in the clinic | 400, field `name`: `REQUIRED`, `TOO_LONG`, `TAKEN` |
| any change to «مالك العيادة» | 409 `ROLE_FIXED` |
| removing a role an account holds | 409 `ROLE_IN_USE` |
| taking «مالك العيادة» from its last holder | 409 `LAST_OWNER` |
| changing one's own role | 409 `OWN_ROLE` |

`AssignAsync` ends the account's sessions with reason `role-changed` in the same transaction
(the reason exists since spec 003). `UpdateAsync` returns what was added and removed, for the
audit trail of spec 007. Who may call them — and "nobody grants what they do not hold" — is the
caller's business (026, 027).

## R15 — One real guarded route

**Decision**: `GET /v1/roles`, needing `admin.roles.view`: id, name, ready key, category, fixed,
number of permissions. Spec 027 extends it.

**Rationale**: constitution VII asks for the real stack to be walked. With only probe routes in
tests there would be nothing real to call as a person with and without a permission.

## R16 — What the screen is told

**Decision**: `GET /v1/auth/session` gains `role: {id, name, readyKey} | null` and
`permissions: string[]` (FR-027). The client asks again at sign-in, after any
`PERMISSION_REQUIRED`, and every 10 minutes while a page is open — inside the fifteen the spec
allows.

## R17 — The client

**Decision**:

- `AccessService` (core): `holds(code)`, `role`, `nameOf(code)`, `refresh()`; the 10-minute
  timer; on `PERMISSION_REQUIRED` the session interceptor refreshes and re-runs the current
  navigation, so the route is matched again with the new set.
- `holds(code)` as a `canMatch` guard. A destination is two routes with one path: the first
  matches for holders and loads the page; the second is the no-permission page with the code in
  its data. The destination's code is never loaded and no request for its content is made
  (FR-029).
- `*scsCan="'code'"` removes a control from the page — it does not disable it (FR-028).
- ui library: `scs-no-access` (lock mark, title, text); `scs-banner` gains the `info` tone and a
  title, which is the «للعرض فقط» notice.
- clinic app: `/administration/roles` — the no-permission page, or the "not available yet" panel
  with the notice above it for a person without `admin.roles.manage`; the page calls `GET
  /v1/roles` so a refusal by the server is seen for real (FR-032).

## R18 — Design comparison

**Decision**: the two states are compared with frames `6675:20818`, `6675:21209`, `6675:19376`
and `6675:20097` by Playwright pictures at 1440 wide in both languages (`SCS_SHOTS`). What is
compared is the content area: breadcrumb, title, card or notice. The top bar and side bar around
them in the frames belong to spec 006 and are recorded as not built.

## R19 — Tests first

Failing tests before the code for: every route declares (R6); refusal for each of the 83 codes
with nothing read or changed (probe routes, one per code); the requirement rule in both
directions for all 61 requirements; a role change seen by the next request; a role assignment
ending sessions; the last owner; isolation of the two tables (samples of spec 002); the
roll-out migration.

## Still to confirm with the owner

1. The «مقترحة / Proposed» tag beside the page title in both frames: taken as a note of the
   design file, not part of the product, and not built.
2. A custom role's name is shown as written in both languages (the English frame shows a
   translated "Clinic team" for what a clinic would have typed in Arabic).
3. The address `/administration/roles`; spec 006 fixes the console's addresses.
4. The BRD is to gain: the codes and English names of rows 80–83; "at the next request" in place
   of the quarter hour of ق-35; the roll-out rule of FR-036. The design file still counts 79.
