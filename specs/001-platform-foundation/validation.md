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

## 2026-10-10 — Local parts of User Stories 2, 3 and 4

Tasks T047–T053, T066–T075, T080, T081, T086, on branch `001-platform-foundation`.

### Automated checks

| Check | Result |
|---|---|
| Server build (warnings as errors) and `dotnet format --verify-no-changes` | Pass |
| Architecture tests | 9 of 9 pass |
| Integration tests, against a real SQL Server container | 86 of 86 pass |
| Web `npm run ci` (lint, unit tests, three builds, `version.json`) | Pass; 37 of 37 tests |

### What was exercised

| Item | Evidence |
|---|---|
| **SC-007**, no personal data or secret in logs | `LogRedactionTests` sends a name, two mobile forms, an email, a national ID, a bearer token and a connection string through the path, query, headers, cookies and body of every mapped endpoint, on success and failure, and logs our own objects as structure and as text. Zero occurrences in the captured output |
| Leaks the test found and that are now closed | A record logged as plain text printed every member; the web framework attached the real request path to every entry |
| Request reference on every entry | `TraceCorrelationTests`: 25 concurrent requests, one entry each, no shared reference |
| `POST /v1/client-errors` | 24 contract cases: accepted, each field's limit and one over, unknown field, unreadable body, 16 KB limit, 31st request in a minute |
| Front-end error reporting | `ScsErrorHandler` spec: route template only, contract fields only, once a minute per message, no loop when reporting fails |
| Newer release detection | `VersionCheckService` spec: update flagged without reload; reload at the next navigation |
| Container images | `Dockerfile.api` and `Dockerfile.migrator` build; the migrator image applied the structure to the local database; the API image answered ready, reported the stamped version and commit, ran as an unprivileged user and logged a heartbeat job with `jobId` and `jobName` |
| `ops/restore/verify.sql` | Run against the local database: passes for the true backup time; fails, with a non-zero exit, when the copy is stale and when the stated time is before the data |
| Telemetry endpoint that is down | The platform still starts and serves |

### Not exercised

- `.github/workflows/pr.yml` and `restore-reminder.yml` have not run: the repository has no
  remote. Their steps were run by hand locally (format, build, tests, web `ci`).
- `deploy/smoke/smoke.sh`: syntax and argument handling only; it needs a deployed environment
  that serves the API under `/api`.
- **T054** is open: the repository settings are written in `ops/runbooks/repository-settings.md`
  but cannot be applied until the repository exists on a host.

### Differences from the task text, and why (this pass)

| Task | Difference | Reason |
|---|---|---|
| T071 | The scrubber is a log **formatter** (`ScrubbingJsonFormatter`), not an enricher | An enricher cannot change exception text or the rendered message; the formatter sees the whole line |
| T070 | A second piece, `ClassifiedScalarEnricher`, was added | Objects logged as plain text and the framework's `RequestPath` were not reachable by the destructuring policy |
| T075 | No SQL client or Hangfire instrumentation; the failed-jobs counter is our own | Those instrumentation packages are pre-release |
| T074 | An unhandled platform failure is reported by code and reference only, with no stack | The stack of a refused request says nothing useful and the title is user-facing text |
| — | `pr.yml` also checks formatting with Prettier | The web `ci` script does not include it |
| — | `.gitleaks.toml` allows the sample env file and test files | They hold made-up credentials on purpose |

### Known limit

The scrubber recognises patterns. It cannot recognise a person's name inside free text that a
developer chooses to log. Names are kept out by never logging request data and by masking
classified members; the rule for developers is to log ids, not people.

## 2026-10-10 — User Story 5 and local Polish

Tasks T089–T097, T099–T104, T106.

| Check | Result |
|---|---|
| Server tests (real SQL Server container) | 115 of 115 pass |
| Web lint, including the logical-properties rule | Pass; the rule was shown to reject `ml-`, `md:pr-`, `text-left`, `[class.mr-1]` and an `ngClass` key |
| Translation check (`npm run i18n:check`) and its own tests | Pass; 10 of 10 |
| Web unit tests | 60 of 60 pass |
| Browser tests (`npm run e2e`, Chromium) | 22 of 22 pass |

**SC-012** is evidenced by the browser tests: for `clinic`, `patient` (at `/noor`) and `ops`, in
Arabic and English — direction matches the language, no text is shown as its key, the date is
Cairo time and the amount is in EGP, each of the six states renders, and a failed request shows
the platform's `title` and `traceId`. Also covered: switching language without reload, the
language remembered on the next visit, the offline state and its recovery, and the bilingual
start-up failure page.

A gap the tests found and that is now closed: failed responses went out without the protective
headers, because error handling clears headers set earlier.

Differences from the task text:

| Task | Difference | Reason |
|---|---|---|
| T098 (open) | Clinic and patient tokens come from the design file; ops tokens are neutral placeholders | The ops Figma file exposed no variables on the nodes read |
| T102 | Error titles are in a code catalogue (`ErrorMessages.cs`), not `.resx` files | Nine short strings; one file keeps the two languages side by side and a test proves every code has both |
| T097 | Both pipes are in `format/format.ts` | They share the locale and digits setting |
| T090 | Arabic medium dates render numerically (`٠١/٠٧/٢٠٢٦`), not with a month name | That is the `ar-EG` convention of the browser's formatter; changeable in one place |
| T096 | The rule is a small local ESLint plugin in `eslint.config.js` | No published rule covers Tailwind's physical utilities in Angular templates |

Open and local: T001 (tick only), T046 (clean-machine timing), T054 and T064 (need the hosted
repository), T078 (alerts runbook, written with the monitoring), T098 (ops tokens), T105
(performance numbers), T107–T109 (owner decisions and final walk-through).

## 2026-10-10 — Convergence (Phase 9)

`/speckit-converge` appended T110–T126. Closed in this pass: T113–T116, T118, T122–T126.

| Check | Result |
|---|---|
| Server unit tests (new project content) | 58 of 58 pass |
| Server architecture tests | 9 of 9 pass |
| Server integration tests that need no database | 98 of 98 pass, five consecutive runs |
| Server integration tests that need a database (17, including the new `FailingJobTests`) | Not run in this pass (Docker stopped responding when the C: drive filled up). **Run later the same day once Docker was back: the full suite passes, 181 of 181** (58 unit, 9 architecture, 114 integration) |
| Web `npm run ci` | Pass; 67 unit tests, 10 tool tests |
| Browser tests | 23 of 23 pass |

What changed:

- **Traces** carry the route template only; path, query, user agent and caller address are
  removed whatever kind the span was given (T113). The first version keyed on the span kind and
  was skipped one run in three.
- **Forwarded headers** are tested: believed from the private network, ignored from outside; the
  rate limit counts the forwarded caller and cannot be escaped by inventing addresses (T114).
- **The Cairo typeface is served by the sites themselves**; a browser test asserts the page makes
  no request to any other host (T115).
- **The placeholder page shows the front end's own version** (T116).
- **Missing translations** fall back to the other language and are reported once (T118).
- `Scs.UnitTests` now holds the tests that need no host (T123).

Still open from Phase 9: T110 (BRD SH-003, owner), T111 (workflow assertions), T112 (seed
scripts, from spec 002), T119–T121 (cloud).

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
