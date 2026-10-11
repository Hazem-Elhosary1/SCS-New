# Contract: The Access Engine (for every module)

**Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

What a module gets, and what it must do, from this spec on.

## 1. Every route declares who may call it

Exactly one of:

| Declaration | Who | Use |
|---|---|---|
| `.AllowAnonymous()` | everyone | pages before sign-in, health, public clinic page |
| `.OwnAccountOnly()` | any signed-in person | only what a person does to their own account |
| `.Requires(Permissions.X)` | holders of `X` | everything else |
| `.RequiresOrOwner<TCheck>(Permissions.X)` | holders of `X`, or the owner of the record named by the request | a person's own record (SH-055) |

A route with none of them is refused for everyone with 403 `FORBIDDEN`, and the gates fail
naming it. A route with two of them, or with a code that is not in the catalogue, fails the
gates too; an unknown code also stops the application from starting.

**The own-account list** — the whole of it. Adding to it changes this file and the test:

- `GET /v1/auth/session`
- `POST /v1/auth/password/change`
- `PUT /v1/account/preferences` (spec 006: what a person keeps between sessions)
- `GET /v1/notifications`, `GET /v1/notifications/state`, `POST /v1/notifications/{id}/read`,
  `POST /v1/notifications/read-all` (spec 008: a person's own bell)

## 2. What a refused person gets

| Situation | Answer |
|---|---|
| no session | 401, as spec 003 |
| temporary password | 403 `PASSWORD_CHANGE_REQUIRED`, as spec 004 |
| the role does not hold the permission; or no role | 403 `PERMISSION_REQUIRED`, `permission: "<code>"` |
| the route declares nothing | 403 `FORBIDDEN` |

In that order. The permission is checked before the handler runs: nothing is read or changed,
and the answer is the same whether or not the record asked for exists. The body names the code
and nothing about the record. A handler never repeats the check.

## 3. Asking inside a handler

`IStaffAccess` (scoped; `Scs.BuildingBlocks.Security`):

| Member | Answer |
|---|---|
| `HasAsync(code)` | does the person asking hold it, right now |
| `ReachOfAsync(viewCode)` | `None`, `Own` (the view alone) or `All` (with its scope permission, or when the view has none) |
| `GetAsync()` | their role's id and name, and every code they hold |

Read once per request, never cached between requests: a change to a role is seen by the next
request. Use `ReachOf` to decide how wide a list is; never compare role names.

## 4. The record's owner

```text
.RequiresOrOwner<MyOwnerCheck>(Permissions.X)
MyOwnerCheck : IRecordOwnerCheck   →   IsOwnerAsync(HttpContext, CancellationToken) : bool
```

Asked only when the permission is missing. `false` — including "no such record" — is answered
with the same `PERMISSION_REQUIRED`. It opens that one record and nothing else.

## 5. Roles: `IClinicRoles` (`Scs.Identity.Contracts`)

For specs 015, 026 and 027. No endpoint here except the list of §6. Every operation runs for the
established clinic, under isolation, in one transaction. **Whether the caller may do it is the
caller's business** — including "nobody grants a permission they do not hold" and "nobody edits
the role they hold", which belong to the pages of 026 and 027.

| Operation | Does | Refuses with |
|---|---|---|
| `ProvisionAsync()` | creates the three ready roles if the clinic has none; safe to repeat | — |
| `ListAsync()` | the clinic's roles: id, name, ready key, category, fixed, count | — |
| `CreateAsync(name, description, codes)` | a custom role | 400 on `name` (`REQUIRED`, `TOO_LONG`, `TAKEN`) and `permissions` (`REQUIRED`, `UNKNOWN`, `DUPLICATE`, `MISSING_REQUIREMENT`) |
| `UpdateAsync(roleId, name, description, codes)` | replaces all three; returns what was added and removed | the same, and 409 `ROLE_FIXED` |
| `RemoveAsync(roleId)` | removes a role nobody holds | 409 `ROLE_FIXED`, 409 `ROLE_IN_USE` |
| `AssignAsync(accountId, roleId)` | gives the account that role; ends its sessions when the role changed | 409 `OWN_ROLE`, 409 `LAST_OWNER`, 404 for a role or account of another clinic |

`MISSING_REQUIREMENT` comes with `missing: [{ "permission": "...", "requires": "..." }]`, `UNKNOWN` with `unknown: ["..."]` and `DUPLICATE` with `duplicate: ["..."]`: a refused set names the codes that are wrong.

A change made by `UpdateAsync` is seen by the role's holders on their next request; their
sessions are not ended. `AssignAsync` does end sessions (SH-045).

A clinic gets its ready roles when it is registered (`IClinicProvisioner`, called by the clinic
registry). Spec 015 gives the first account «مالك العيادة» with `AssignAsync`.

## 6. What the screen is told

`GET /v1/auth/session` → also `role` and `permissions`
([access-api.openapi.yaml](access-api.openapi.yaml)). The catalogue itself reaches the client as
a generated file, not a request.

## 7. What is not a permission

Plan entitlements, the read-only clinic, the clinic's shape and a record's state are decided by
their own specs with their own answers. Do not add them to the catalogue and do not answer them
with `PERMISSION_REQUIRED`.
