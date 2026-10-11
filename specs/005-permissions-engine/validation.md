# Validation: Permissions Engine

**Date**: 2026-10-11 | **Spec**: [spec.md](spec.md) | **Tasks**: [tasks.md](tasks.md)

What was run, what was walked, what was laid beside the design, and what was not checked
(constitution VII). Nothing here was skipped silently; what was not done is in §5.

## 1. Automated suites

All run on the final code, on this machine, against a real SQL Server (Testcontainers) as the
application login.

| Suite | Before this spec | After | Result |
|---|---|---|---|
| Server unit tests | 235 | 401 | all pass |
| Architecture tests | 26 | 30 | all pass |
| Server integration tests | 380 | 430 | all pass |
| Web lint, translation check, tool tests | — | — | pass |
| Web unit tests (clinic · core · ui · patient · ops) | 20 · 101 · 40 · 1 · 1 | 20 · 117 · 48 · 1 · 1 | all pass |
| Web builds (clinic, patient, ops) | — | — | pass |
| Browser tests (Playwright) | 187 | 208 | all pass |

New tests, by what they prove:

| What | Where | Count |
|---|---|---|
| The catalogue is the BRD table: 83 rows, groups, kinds, 61 requirements, constants | `UnitTests/Access/PermissionCatalogueTests` | 29 |
| The requirement rule, both directions, for each of the 61 | `UnitTests/Access/PermissionSetsTests` | 134 |
| The client file is the server's catalogue | `UnitTests/Access/CatalogueFileTests` | 2 |
| Every route declares exactly one thing; the own-account list is the written two; an undeclared route is refused for an owner | `IntegrationTests/Access/RouteDeclarationTests` | 7 |
| Holder served and non-holder refused for each of 83 codes, the handler never run; order of refusals; no role; same answer with and without a record; other clinic; the log entry; the session answer | `IntegrationTests/Access/PermissionGateTests` | 9 |
| Three ready roles on registration, 83·59·40, idempotent, repair after a failed provisioner, stored codes, the roles list | `IntegrationTests/Access/ReadyRolesTests` | 7 |
| Role operations and every refusal; the last owner under a race; own role | `IntegrationTests/Access/ClinicRolesTests` | 11 |
| A change seen by the next request; assignment ends sessions; a request under way | `IntegrationTests/Access/RoleChangeTests` | 6 |
| Reach and the record's owner; the timing | `IntegrationTests/Access/ReachAndOwnerTests` | 5 |
| The roll-out migration, and taking it back | `IntegrationTests/Access/RolesRollOutTests` | 2 |
| Isolation of the two tables | `Isolation/IdentitySamples` (found by the guardrails) | 2 samples |
| Client service, guard, `*scsCan`, the refusal path | `core/access/access.service.spec.ts` | 15 |
| `scs-no-access` and the info banner | `ui/no-access/no-access.component.spec.ts` | 6 |
| The roles address for three people, a stale list, no session | `e2e/access.spec.ts` | 6 |
| Five states in two languages, ready-role names, a 40-character name | `e2e/access-states.spec.ts` | 14 |

**Timing (SC-006)**: 200 calls each on one session, taken in turns. Median with the permission
check 6.91 ms, without 4.12 ms: the check added **2.79 ms** (target: under 5 ms).

**SC-008**: the no-permission state appeared in under one second after opening the address
(asserted in `e2e/access.spec.ts`), with no request for the roles.

## 2. The real stack, walked

Local stack: compose project `heta-scs`, the API and the clinic app restarted on the final code,
the existing local database migrated in place by `RolesAndPermissions`, then seeded.

**The roll-out on real existing data** (not a fresh database): both seeded clinics received three
ready roles with 0, 59 and 40 stored codes; the migration made each clinic's oldest active
account its owner (`owner@noor.example`, `owner@shifa.example`); the seed then gave the other
five accounts their roles and created «فريق العيادة» in Noor with 6 codes. Read back with SQL.

| Step | As | What was seen |
|---|---|---|
| Open `/administration/roles` with no session | nobody | the sign-in form |
| Sign in with both steps (code from Mailpit), open the address | `doctor@shifa.example` | «للعرض فقط» / "View only" with "Changing roles needs “Manage roles & permissions”.", then "This page is not available yet". Session: role `doctor`, 40 codes, no `admin.roles.manage`. `GET /v1/roles`: 200 with owner 83, manager 59, doctor 40 |
| Remove `admin.roles.view` from Shifa's «طبيب» in the database, **without signing in again** | same session | `GET /v1/roles` → 403 `PERMISSION_REQUIRED`, `permission: admin.roles.view`, title "This page is not in your role". Reloading the address: "This page is not in your role … your role “Doctor” does not carry it", and **no** request to `/v1/roles` was made. The session stayed alive |
| Put the row back, switch to Arabic, reload | same session | «للعرض فقط» again, with «التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».» |
| Sign out, sign in with both steps, open the address | `owner@shifa.example` | the page with no notice. Session: role `owner`, 83 codes |
| Call an address that does not exist | owner | 404 (unchanged) |

The database was left as the seed made it: the removed row was restored (40 codes again).

## 3. Beside the design

Pictures taken by `SCS_SHOTS=… npx playwright test access-states` at 1440×900 and laid beside
the frames, read through the Figma connector at the same size. Compared by eye, not pixel by
pixel.

| State | Frame | Matched | Differed |
|---|---|---|---|
| No permission, Arabic | "Roles / No Access / RTL" `6675:20818` | canvas colour; breadcrumb with the arrow pointing onward; title 22 bold; the line under it «… — دورٌ لا يحمل «عرض الأدوار والصلاحيات»»; card border, radius and height (381 against 383); the round mark in the danger tint with the lock; title 16 semi-bold; text 13 in three lines of 24 | the date in the line under the title is today's; the frame's is its own |
| No permission, English | `6675:21209` | the same, mirrored; wording as drawn | the role's name is shown as the clinic wrote it («فريق العيادة»), where the frame shows "Clinic team" (owner item 6) |
| View only, Arabic | "Roles / Roles List Read Only / RTL" `6675:19376` | information tint and border, the info mark, «للعرض فقط» 14 semi-bold, the line under it, height (78 against 77) | under the notice the frame draws the roles list; here it is the "not available yet" panel (spec 027) |
| View only, English | `6675:20097` | the same, mirrored: "View only", "Changing roles needs “Manage roles & permissions”." | as above |

Adjusted after the first comparison: the breadcrumb arrow (it pointed down), the space above
the title and under its line, the notice's line heights (83 → 78), the text width of the card
(English now wraps in five lines, as drawn).

**Not compared, because not built here**: the top bar and the side bar around both states (spec
006), so the content is wider than in the frames; the «مقترحة / Proposed» tag beside the title;
the count in the line under the title of the roles page («ستة أدوار، و٧٩ صلاحية…»), which belongs
to the roles list of spec 027 — the page shows the date only.

## 4. What changed outside the spec's own files

- An undeclared route was open to any signed-in person; it is now refused for everyone. The two
  existing session routes are declared own-account; test probes were declared or left
  undeclared on purpose.
- `ClinicRegistry.RegisterAsync` now runs each module's provisioner after registering a clinic.
- `scs-banner` gained the `info` tone and a title; three design tokens were added
  (`--scs-color-bg-canvas`, `--scs-text-title`, `--scs-spacing-xl`), read from the design file.
- `ScsDatePipe` gained the `long` format of the line under a page title.

## 5. Not done, or done differently — said plainly

1. **Tests first, not everywhere.** The catalogue tests and the catalogue were written
   together, and the integration tests for the gate, the roles and the roll-out were written
   after the code they cover and passed on their first run. They were not seen to fail first.
   The web unit tests did fail first (seven of fifteen) and were then fixed.
2. **`reception@noor.example` was not walked on the real stack.** Its password was changed by
   hand during spec 004 and is not the seed's. The no-permission state was reached on the real
   stack another way (step 3 above) and with the named custom role in the browser tests; the
   database shows the account holds «فريق العيادة».
3. **The local database was not rebuilt from empty.** It had already been migrated when three
   role descriptions were corrected to the frame's wording, so those three rows were updated by
   hand in the local database. The migration and `ReadyRoles` carry the corrected text, and the
   roll-out test compares the two on a fresh database.
4. **The design comparison is by eye** on full-size pictures; no pixel comparison was made.
5. **A stale write is not refused by row version.** Changes to one clinic's roles are taken one
   at a time under a lock; the row version is stored for the editor of spec 027.
6. **`temp@noor.example` and `new@shifa.example`** hold «مدير العيادة» but were not signed in.

## 6. For the owner

| # | Item | Where it stands |
|---|---|---|
| 1 | Codes and English names of rows 80–83 (`data.consents.view/manage`, `patients.lab.view/manage`) | decided 11 Oct 2026; **the BRD still has none** |
| 2 | A change to a role is seen at the holder's next request | decided 11 Oct 2026; the BRD (ق-35) still says "within a quarter of an hour" |
| 3 | At roll-out the oldest active account of each clinic becomes owner; seeds assign the rest | decided 11 Oct 2026; not in the BRD |
| 4 | The design file counts 79 permissions (79, 55, 37); the BRD and the product count 83 (83, 59, 40) | frames to be updated |
| 5 | The «مقترحة / Proposed» tag beside the page title | taken as a note of the design file; not built — to confirm |
| 6 | A custom role's name is shown as the clinic wrote it in both languages; ready roles are shown in the page's language until renamed | to confirm |
| 7 | The sentence for an account with no role: «وحسابك لا يحمل دورًا بعد» / "your account does not hold a role yet", and the line «حساب لا يحمل دورًا» under the title | not drawn — to confirm |
| 8 | The address `/administration/roles` | until spec 006 fixes the console's addresses |
| 9 | English names of the eleven groups; "Profile & subscription" for «الملف والاشتراك» is not in any frame read | to confirm |
| 10 | The one-line descriptions of the three ready roles were taken from frame `6675:19376` | not in the BRD |

## 7. Convergence pass (11 Oct 2026)

`/speckit-converge` found two partial gaps; both are closed (T053, T054). This time the tests
were written first and seen to fail before the code was changed.

| Task | What changed | Seen failing first | Now |
|---|---|---|---|
| T053 | One «للعرض فقط» notice in the shared library: the `view-only` state of `scs-state` now draws the design's information banner with its title and the line passed as `message`; the roles page uses it. The older plain panel is gone | 2 new cases in `scs-state.component.spec.ts` failed | ui 48 pass; the picture of the state is unchanged against frame `6675:19376` (notice 78 px high against 77) |
| T054 | A refused set of permissions names the codes that are wrong: `unknown: [...]` and `duplicate: [...]` beside `missing` | the unit tests did not compile (`Unknown`, `Repeated` did not exist) | unit 401, `ClinicRolesTests` 12 pass |

Two things seen while running every gate again:

- **A timing check failed once under load.** "Within a second" (SC-008) failed in one full
  run of the browser tests and passed alone. It is now its own test with two retries, said so
  in the test: a clock on a machine running two hundred other browser tests is not the app's.
  The full run after that: 208 of 208.
- **One existing test failed once under load, and it is not this spec's.**
  `TraceRedactionTests.Health_probes_are_not_traced` (spec 001) waits for a trace span and
  failed in one run of the integration suite made while the web checks and the browser tests
  were running on the same machine. Alone it passed three times of three, and the whole
  integration suite run again with nothing else running passed 430 of 430. It was not changed.

The real stack was not walked again for these two tasks: T053 is the same notice at the same
address (pictured again by the browser tests in both languages), and T054 has no screen.