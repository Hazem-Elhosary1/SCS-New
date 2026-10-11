# Quickstart: Checking the Permissions Engine

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

How to prove the feature works. Setup is as [docs/setup.md](../../docs/setup.md): temp folder on
`E:`, compose project `heta-scs`, the previews "api" and "clinic". The seed password is the value
of `SCS_SEED__STAFF_PASSWORD`; sign-in codes arrive in Mailpit (`http://localhost:8025`).

## Automated

```powershell
dotnet test src/server/Scs.sln -c Debug          # unit, architecture, integration
cd src/web; npm run ci                            # lint, i18n check, unit tests, build
npx playwright test                               # every screen state, both languages
$env:SCS_SHOTS = "shots"; npx playwright test access-states   # pictures for the design comparison
```

| # | Scenario | Where | Expected |
|---|---|---|---|
| 1 | The catalogue equals the contract | unit `Access/PermissionCatalogueTests` | 83 rows, numbers without gaps, group counts, kinds, every requirement a view or read |
| 2 | Requirement rule, both directions | unit `Access/PermissionSetsTests` | for each of the 61 requirements: with → accepted, without → `MISSING_REQUIREMENT` naming both |
| 3 | Ready sets | unit | 59 and 40, both valid; counts computed |
| 4 | Client file is the catalogue | unit `Access/CatalogueFileTests` | fails when `permission-catalogue.json` differs |
| 5 | Every route declares | integration `Access/RouteDeclarationTests` | none undeclared, none with two, own-account list is exactly the two |
| 6 | Undeclared route | integration | 403 `FORBIDDEN` even for an owner |
| 7 | Each of 83 codes | integration `Access/PermissionGateTests` | holder 200; non-holder 403 `PERMISSION_REQUIRED` with the code; probe counter shows nothing ran |
| 8 | Order of refusals | integration | no session 401; temporary password `PASSWORD_CHANGE_REQUIRED`; then permission |
| 9 | Same answer with and without a record | integration | identical bodies apart from the trace id |
| 10 | Role edited | integration `Access/RoleChangeTests` | next request follows the new set; session still alive |
| 11 | Role assigned | integration | every session of the account ended, reason `role-changed` |
| 12 | Role operations refuse | integration `Access/ClinicRolesTests` | each row of access-engine.md §5 |
| 13 | Owner or permission | integration | owner reaches own record only |
| 14 | Reach | unit + integration | `None`, `Own`, `All` for both scope permissions |
| 15 | Isolation | integration `Isolation/IdentitySamples` | the two tables pass the guardrails of spec 002 |
| 16 | Roll-out | integration `Persistence/MigrationTests` | an old clinic gets three roles equal to a provisioned clinic's; oldest active account is owner |
| 17 | New clinic | integration | registered → three roles at once |
| 18 | Stored codes | integration | every stored code is in the catalogue |
| 19 | Client service, guard, directive | web unit | hidden not disabled; guard falls to no-access |
| 20 | Screen states | Playwright `access-states` | the five states of access-screens.md §6 in both languages |

## By hand, on the real stack

After `scripts/dev.ps1` (migrate, seed) with the previews running:

1. **Owner** — sign in as `owner@noor.example`, open `http://localhost:4201/administration/roles`:
   title and the "not available yet" panel, no notice.
2. **View only** — sign in as `doctor@shifa.example`, same address: «للعرض فقط» with «إدارة
   الأدوار والصلاحيات».
3. **No permission** — sign in as `reception@noor.example`, same address: «هذه الصفحة ليست في
   دورك», naming «عرض الأدوار والصلاحيات» and «فريق العيادة». In the network panel: no call to
   `/v1/roles`.
4. **The server itself** — with reception's session, call `GET /v1/roles` from the browser
   console: 403 `PERMISSION_REQUIRED`, `permission: admin.roles.view`.
5. **At once** — while signed in as `doctor@shifa.example`, remove `admin.roles.view` from
   «طبيب» of the Shifa clinic in the database (`sql.ps1`), then reload the page: the
   no-permission state, without signing in again. Put the row back.
6. **English** — switch the language on states 2 and 3 and compare with the LTR frames.
7. **Design** — lay the pictures from `SCS_SHOTS` beside frames `6675:20818`, `6675:21209`,
   `6675:19376`, `6675:20097`; write what matched, what differed and what was not compared in
   `validation.md`.

## Measuring SC-006

Run the timing test (`Access/GateTimingTests`): 200 calls to a route that needs a permission and
200 to an own-account route, same session, same machine. Record both medians and the difference
in `validation.md`. Target: the difference under 5 ms.
