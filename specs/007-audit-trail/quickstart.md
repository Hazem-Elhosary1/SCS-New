# Quickstart: Verifying the Audit Trail

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

Temp folder on `E:` first, as [docs/setup.md](../../docs/setup.md) says.

## Automated

```powershell
dotnet test src/server/Scs.sln -c Debug
npm --prefix src/web run ci
```

What must pass that is new:

| Test | Proves |
|---|---|
| `Audit/AuditIsolationTests` | an entry of one clinic is unreadable from another by every way of reading (written first) |
| `Audit/AuditReadingAccessTests` | `GET /v1/audit/entries` refuses without `reports.activity`, answers with it (written first) |
| `Audit/AuditWritingTests` | change and entry together; a forced failure on either side leaves neither |
| `Audit/AuditImmutabilityTests` | update and delete of an entry are refused for the application's login |
| `Audit/AuditFactsTests` | a fact outside the white list is refused; secrets passed alongside are nowhere in what is stored |
| `Audit/RoleEntriesTests` | role created, edited, deleted, a person's role changed: one entry each, with before and after; refused or unchanged ones leave none |
| `Audit/SignInEntriesTests` | sign-in, a run of wrong passwords as one entry with its count and lock, reset, temporary password, invitation, sessions ended, sign-out |
| `Audit/AuditReadingTests` | period, default ninety days, facets, search, counts, paging, the two languages |
| `Audit/AuditVolumeTests` | one million entries: a page and its counts in under a second |
| `Access/RouteAuditDeclarationTests` | every changing route declares what it records |
| `AuditActionsTests` (unit) | codes frozen by the lock file; two languages for every action; no never-list key |
| isolation suite of spec 002 | `audit.Entry` has its policy and its sample |

There is no browser test: this spec builds no screen. The existing browser suite must still pass
unchanged.

## By hand, on the real stack

Start the stack (`deploy/compose`), the API and the clinic app. Sign-in codes arrive in Mailpit
(<http://localhost:8025>).

1. Sign in as `owner@shifa.example`.
2. In a second browser, type a wrong password for `doctor@shifa.example` twice, then the right
   one and its code.
3. As the owner, open `http://localhost:4201` and ask the reading address from the browser's
   console (the session cookie goes with it):

   ```js
   await (await fetch('http://localhost:5080/v1/audit/entries', { credentials: 'include' })).json()
   ```

   Expect, newest first: the doctor's sign-in; one entry for the run of two failed attempts on
   the doctor's account, `attempts: 2`, `locked: false`; the owner's sign-in. `counts.security`
   is at least 3.
4. Ask again with `?category=users`, then with `Accept-Language: en`: the sentences are English
   and nothing Arabic remains except what a person wrote.
5. Sign in as `reception@shifa.example` and ask the address: `403`, `PERMISSION_REQUIRED`,
   `reports.activity`.
6. In the database, as the application's login, try to change and to delete one entry: both are
   refused.

   ```sql
   UPDATE audit.Entry SET ActionCode = 'x' WHERE Id = '<id>';
   DELETE FROM audit.Entry WHERE Id = '<id>';
   ```

Role changes have no screen and no address until spec 027; their entries are verified by the
automated tests, and on the real stack only as the roles the platform gave each clinic — which a
clinic's reading does not return, because the platform is their actor (FR-030). Step 6's query
shows them.

## Measuring

`AuditVolumeTests` prints the time of the page and of the counts over one million entries
(SC-007), and the time recording adds to a role edit (SC-010).
