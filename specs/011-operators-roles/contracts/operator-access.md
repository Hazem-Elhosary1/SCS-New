# Contract: operator access (for every module that adds a route to the console)

**Feature**: 011-operators-roles

What a module gets, and what it must do, from this spec on, for anything under `/v1/ops/`.
The clinic's side has its own contract (spec 005); nothing here applies to a clinic's route,
and an operator's permission never satisfies one.

## 1. Every route declares who may call it

Exactly one of:

| Declaration | Who | Use |
|---|---|---|
| `.Open()` | anyone | before sign-in: sign-in, the code, forgot password, reading and using a link |
| `.OperatorOwnAccount()` | any signed-in operator | only what an operator does to their own account |
| `.RequiresOperator(OperatorPermissions.X)` | operators whose role holds `X` now | everything else |

A route with none is refused for everyone with 403 `FORBIDDEN`, and the architecture tests fail
naming it. A route with two, or with a code outside the catalogue, fails them too; an unknown
code also stops the application from starting.

**The own-account list** — the whole of it. Adding to it changes this file and the test:

- `GET /v1/ops/auth/session`, `POST /v1/ops/auth/sign-out-everywhere`
- `GET /v1/ops/profile`, `POST /v1/ops/profile/password` and its `/confirm`, `/resend`, `/cancel`
- (spec 093 adds the operator's own bell here.)

## 2. What a refused operator gets

| Situation | Answer |
|---|---|
| no operator session (none, a clinic's, an ended one) | 401, as spec 010 |
| the role does not hold the permission | 403 `PERMISSION_REQUIRED`, `permission: "<code>"` |
| the route declares nothing | 403 `FORBIDDEN` |

In that order. The permission is checked before the handler runs: nothing is read or changed,
and the answer is the same whether or not the record asked for exists. A handler never repeats
the check.

## 3. Asking inside a handler

`IOperatorAccess` (scoped):

| Member | Answer |
|---|---|
| `HasAsync(code)` | does the operator asking hold it, right now |
| `GetAsync()` | their role's id, name and system key, and every code they hold |
| `IsSuperAdminAsync()` | is their role the platform's «مسؤول عام» |

Read once per request and never cached between requests: a change to a role is seen by the next
request, and nobody's session ends for it. Never compare role names; never count permissions to
decide who is a super admin.

## 4. View without write

A read that also says what the caller may do (`can { … }`) is how a screen knows which controls
to draw. The server refuses the write all the same. A module adds `can` to its reads rather
than have the console work the rule out.

## 5. Recording

A route that changes something declares `.Records("<action>")`; one that changes nothing
declares `.RecordsNothing("<why>")`. An action of an operator on the platform itself is written
to the platform's trail with the operator as its actor, taken from the session:

```text
db.RecordForPlatform(PlatformEntries.X(...))   // saved with the change, in its transaction
```

Nothing secret goes into an entry: no link, token, password or code.

## 6. In the console

| Piece | Use |
|---|---|
| `OpsAccess.can(code)` | a signal: does the signed-in operator hold the code |
| `opsCan(code)` | a route guard: someone without the code gets the "cannot open this page" state |
| `*opsCan="code"` | draws a control only for whoever holds the code |
| `OPS_SECTIONS` | each section names the permission that shows it in the side bar |

The console reads the session again on navigation and after any 403 `PERMISSION_REQUIRED`.

## 7. The 28 codes

`OperatorPermissions` (data-model §1). A spec that builds a screen puts its routes behind the
codes of its group; the test "every code guards a route" is switched on for a group by the spec
that builds it — the operators group by this one.
