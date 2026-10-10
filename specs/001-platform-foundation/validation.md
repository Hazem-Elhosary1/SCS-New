# Validation Record: Platform Foundation

Evidence for the success criteria of [spec.md](spec.md), added as each scenario of
[quickstart.md](quickstart.md) is run.

## 2026-10-10 — Setup, Foundational and User Story 1 (T002–T045)

Machine: Windows 11, .NET SDK 10.0.301, Node 26.2.0, Angular 22.2, Docker 28.3.3,
SQL Server 2022 container.

### Automated checks

| Check | Result |
|---|---|
| `dotnet build src/server/Scs.sln` (warnings as errors) | Pass |
| `dotnet format --verify-no-changes` | Pass |
| Architecture tests | 7 of 7 pass |
| Integration tests, against a real SQL Server container | 43 of 43 pass |
| `ng lint` (5 projects) | Pass |
| `ng test` (5 projects) | 23 of 23 pass |
| `prettier --check` | Pass |
| `npm run build:all` (clinic, patient, ops) | Pass; about 62 kB transferred each |

### Quickstart Scenario 1, run through `scripts/dev.ps1`

| Step | Expected | Observed |
|---|---|---|
| `/health/ready` | 200, `database` and `jobs` healthy | 200, both `Healthy` |
| `/v1/meta/version` | `environment: "local"` | As expected |
| Clinic `:4201` | Placeholder, platform reachable | «لوحة العيادة … المنصّة متاحة», version and environment shown, no console errors |
| Patient `:4202/noor` | Works on a clinic path | «واجهات المريض … المنصّة متاحة» |
| Operators `:4203` | Placeholder, platform reachable | «لوحة المشغّلين … المنصّة متاحة» |
| Stop the database | `live` 200, `ready` 503 naming `database` | `live` 200; `ready` 503 in 2 s; `database` and `jobs` `Unhealthy` |
| Page while the database is stopped | Error state with a reference, not blank | «تعذّر الاتصال بالمنصّة», reference shown, retry button |
| Start the database, press retry | Recovers without reload | «المنصّة متاحة» |

### Not yet evidenced

- **SC-001 (T046)**: the timed run on a machine that has never held the project. This run was on
  the machine the code was written on, with packages and the SQL Server image already present.
- `scripts/dev.sh` has not been run (no macOS or Linux machine was available).

### Differences from the task text, and why

| Task | Difference | Reason |
|---|---|---|
| T001 | Git is initialised on `main`; no commit was made and `develop` does not exist yet | Commits are left to the owner of the repository |
| T002 | `AnalysisLevel` is `latest-recommended`, not `latest-all` | `latest-all` with warnings as errors flags generated and framework-idiomatic code; recommended still fails the build on real defects |
| T006 | No `tailwind.config.js`; configuration is in each app's `styles.css` | Tailwind 4 is configured in CSS |
| T007 | `ci` runs lint, tests and builds; `i18n:check` and `e2e` join it when their tools exist (T095, T089) | The tools belong to User Story 5 |
| T009 | Tool manifest is `src/server/dotnet-tools.json` | Location the .NET 10 SDK creates |
| T016 | An unknown route answers 401 `UNAUTHENTICATED`, not 404 | Deny-by-default covers unmatched routes, so routes cannot be probed; contracts/conventions.md updated |
| T022 | The OpenAPI document is served; no viewer page | A viewer needs a third-party package; the document is enough for tooling |
| T036 | The jobs dashboard is mapped in `local` only | Analysis finding F1: it has no sign-in, and test is internet-reachable |
| T036 | The `hangfire` schema is created by the migrator | The application login cannot alter structure |
| T038 | `jobs` readiness means job storage is reachable; staleness is left to the alarm | Analysis finding C2 |
| T020 | Forwarded headers are trusted from private networks only; health probes are exempt from HTTPS redirection | Analysis finding C1 |
| T041–T043 | The three pages wrap one shared component in `ui` | Constitution: shared code lives in a shared library |
| T040 | A platform that is alive but not ready is reported as unreachable | Spec, Story 1 scenario 3: the page must show its error state when the database is stopped |
| — | `Newtonsoft.Json` is pinned to 13.0.4 | Hangfire brings in 11.0.1, which has a known high-severity advisory |
| — | Compose project is named `heta-scs`, database on host port 14333 | `scs` is used by the earlier reference repository's containers; 1433 is taken by a local SQL Server |
