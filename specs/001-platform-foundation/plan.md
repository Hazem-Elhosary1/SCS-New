# Implementation Plan: Platform Foundation

**Branch**: `001-platform-foundation` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-platform-foundation/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Build the skeleton the other 91 specs stand on: one .NET API organised as a modular monolith,
one SQL Server database, and one Angular workspace with three applications (clinic, patient,
ops), all in one repository. Around it: a pipeline that blocks failing changes and promotes one
built package from test to production; logging that keeps personal data and secrets out by
default; external monitoring with alerts; a daily backup with a quarterly recorded restore test;
and the shared conventions (error shape, request reference, Arabic/English with right-to-left,
Cairo time and EGP, six screen states, background jobs) that later specs reuse.

Technical approach, in one line each (detail in [research.md](research.md)):

- Modules are two projects each (`Scs.<Module>` + `Scs.<Module>.Contracts`); boundaries by the
  compiler, layers by architecture tests. This spec creates the `Platform` module as the template.
- Errors are RFC 9457 Problem Details with `code` and `traceId`; the trace id is the reference.
- The app reads environment variables only; front ends read `config.json` at start-up, so one
  build serves every environment.
- Deny-by-default authorization from the first commit.
- Infrastructure as Terraform; the app speaks OTLP and writes JSON to standard output, so nothing
  in the code is tied to the host.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.x) for the server; TypeScript on the current
stable Angular major for the front ends, with the Node LTS it supports. Exact Angular and Node
versions are pinned in the repository at the first task (research R4).

**Primary Dependencies**: ASP.NET Core, EF Core (SQL Server provider), Hangfire (SQL Server
storage), Serilog, OpenTelemetry; Angular, Angular CDK, Tailwind CSS, Transloco. No third-party
UI component library (constitution).

**Storage**: SQL Server 2022 — RDS in test and production, a container locally. One schema per
module; this spec adds `platform` (one table) and `hangfire`.

**Testing**: xUnit, `WebApplicationFactory`, Testcontainers (real SQL Server), NetArchTest;
Angular CLI unit runner, ESLint, Prettier, Playwright.

**Target Platform**: Linux containers on AWS ECS Fargate behind an application load balancer;
front ends as static files on S3 + CloudFront; AWS Middle East region. Addresses (research R25):
operators `hetasystems-scsadmin.app`, staff `app.scs-heta.com`, patients `scs-heta.com/<clinic>`,
API as `/api` on each. Evergreen desktop and
mobile browsers.

**Project Type**: web application — one API, three single-page applications, infrastructure code.

**Performance Goals**: foundation endpoints answer in under 300 ms at p95; `live` under 50 ms.
Pull-request checks finish in under 15 minutes; merge-to-test in under 15 minutes (SC-003).

**Constraints**: availability 99.5% per month; at most 24 hours of data loss; restore within
4 hours; rollback within 15 minutes; zero interrupted requests during a release; zero personal
data or secrets in logs; nothing in application code that prevents a move to hosting in Egypt.

**Scale/Scope**: launch scale of tens to low hundreds of clinics; two API tasks to start. Scope
is 35 functional requirements, 4 endpoints, 3 placeholder pages, 1 table, 3 environments.

**Open items carried from research** (defaults chosen, none blocks the work): Angular/Node pin,
digits on Arabic screens, source host and CI (GitHub assumed), AWS region (`me-south-1`
assumed), SQL Server edition in production (Standard Multi-AZ assumed), alert thresholds.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. The BRD is the source of truth | Pass | Every FR in the spec cites its `SH-` id. Nothing is ported from the earlier code (FR-002). Numeric targets the BRD does not give are labelled assumptions, not requirements. |
| II. Tenant isolation in the database | Not applicable, prepared | No clinic-owned table is created. Prepared for 002: the app login has no structure rights, migrations run as a separate login, tests already use a real SQL Server, and the job interface has the place where the clinic context is set. |
| III. The server decides | Pass | Fallback policy denies every route; the four foundation endpoints are the only explicit exceptions (R12). No permission or plan logic exists yet to place in a client. |
| IV. Financial records immutable and audited | Not applicable | No money, permission or plan data. |
| V. Patient data, secrets and AI contained | Pass | Four-layer log protection on by default (R9) with an automated scan test; secrets only from the environment, never in the repo, the package or `config.json`; HTTPS with HSTS; front-end errors reported to our own endpoint, not a third party; no AI. |
| VI. Bilingual and complete screens | Pass | Language switch, `dir`, logical layout enforced by lint, missing-key check in CI, `scsDate`/`scsMoney`, and one `<scs-state>` component for the six states. |
| VII. Tested before merge | Pass | Each acceptance scenario maps to a quickstart scenario and an automated test. None of the four test-first areas is touched; the log-redaction and architecture tests are written first anyway because later specs depend on them. |
| Technology constraints | Pass | .NET 10, Angular, SQL Server, modular monolith, monorepo, EF Core, Tailwind + CDK, Hangfire, Docker on Fargate + RDS — all as fixed. Dapper is not introduced. No AWS SDK in application code. |
| Workflow | Pass | `main` + `develop`, spec branch by pull request, English artifacts with Arabic UI text quoted from the BRD. |

**Re-check against constitution v1.1.0** (10 Oct 2026, after the address amendment):

| Rule added in v1.1.0 | Status | How the design complies |
|---|---|---|
| No per-clinic subdomains; clinic from the account (staff) or the first path segment (patients) | Pass, with one open item | Research R25: one address per audience. The patient app serves any path and loads its files from the site root. Resolving the clinic is left to specs 002 and 003. **Open**: BRD SH-003 still states the old addresses (task T110); the address infrastructure is not built before it is updated. |
| Staff and patient screens on different browser origins | Pass | `app.scs-heta.com` and `scs-heta.com` are different origins; the operators console is on a third domain. |
| Each front end calls the API on its own origin under `/api` | Pass | `config.json` is refused unless `apiBaseUrl` is `/api` outside `local`; the platform refuses to start with a cross-origin allowance outside `local`; forwarded headers are trusted from the private network only. |

**Result**: gate passed, no violations. **Post-design re-check** (after Phase 1): unchanged. The
contracts add no anonymous surface beyond the four endpoints, `config.json` carries no secret,
and the one table is platform-owned.

## Project Structure

### Documentation (this feature)

```text
specs/001-platform-foundation/
├── plan.md              # This file
├── research.md          # Phase 0: 25 decisions, 6 items to confirm
├── data-model.md        # Phase 1: one table; where every other entity lives
├── quickstart.md        # Phase 1: five validation scenarios
├── contracts/
│   ├── platform-api.openapi.yaml   # health, version, client-errors, Problem
│   ├── conventions.md              # routes, headers, error codes, log entry, jobs
│   └── frontend-runtime.md         # config.json, version.json, i18n, states, pipes
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/
├── server/
│   ├── Scs.sln
│   ├── Directory.Build.props          # warnings as errors, analyzers, nullable
│   ├── Directory.Packages.props       # central package versions
│   ├── host/
│   │   └── Scs.Api/                   # composition root: pipeline, auth fallback, modules
│   ├── shared/
│   │   ├── Scs.SharedKernel/          # primitives, [PersonalData]/[Secret], error codes
│   │   └── Scs.BuildingBlocks/        # logging, problem details, health, options, jobs
│   ├── modules/
│   │   └── Platform/
│   │       ├── Scs.Platform/          # Domain/ Application/ Infrastructure/ Api/
│   │       └── Scs.Platform.Contracts/
│   ├── tools/
│   │   └── Scs.Migrator/              # migration bundle entry point
│   └── tests/
│       ├── Scs.UnitTests/
│       ├── Scs.IntegrationTests/      # WebApplicationFactory + Testcontainers
│       └── Scs.ArchitectureTests/     # NetArchTest rules
└── web/
    ├── angular.json
    ├── package.json                   # scripts: start, ci, e2e, i18n:check
    ├── tailwind.config.js
    ├── projects/
    │   ├── clinic/                    # app: placeholder page, tokens, ar.json, en.json
    │   ├── patient/
    │   ├── ops/
    │   ├── core/                      # lib: config, http, errors, i18n, pipes, version
    │   └── ui/                        # lib: <scs-state>, shared components
    ├── e2e/                           # Playwright
    └── tools/                         # i18n completeness, literal-text check

deploy/
├── docker/                            # Dockerfile.api, Dockerfile.migrator
├── compose/                           # docker-compose.yml, .env.example
└── infra/                             # Terraform: modules/, env/{test,production}.tfvars

ops/
├── runbooks/                          # release, rollback, restore, alerts
├── restore/                           # verify.sql
└── restore-tests/                     # one record per trial restore

scripts/                               # dev.ps1, dev.sh
.github/workflows/                     # pr.yml, deploy-test.yml, deploy-production.yml,
                                       # rollback.yml, restore-reminder.yml
docs/                                  # setup guide (FR-006)
```

**Structure Decision**: web application in a monorepo. `src/server` is one .NET solution in
which each BRD area becomes a module of two projects under `modules/`; `src/web` is one Angular
workspace with three apps and two shared libraries; `deploy`, `ops` and `.github/workflows`
carry the environments, runbooks and pipeline. Later specs add modules under
`src/server/modules/` and feature folders inside the three apps; they do not add top-level
directories.

## Delivery Order

For `/speckit-tasks`. Each step leaves the repository working.

1. **Repository and server skeleton** — solution, build props, `Scs.Api`, SharedKernel,
   BuildingBlocks, architecture tests (written first). Story 1.
2. **Platform conventions** — problem details, trace id, logging with redaction and its scan
   test (written first), options validation, deny-by-default, health and version endpoints.
   Stories 3 and 5.
3. **Database and jobs** — compose SQL Server, `Platform` module, first migration, migrator,
   Hangfire and the heartbeat job, readiness checks. Stories 1 and 4.
4. **Web workspace** — three apps, `core`, `ui`, runtime config, i18n and lint rules,
   pipes, `<scs-state>`, placeholder pages, client error reporting, Playwright suite.
   Stories 1 and 5.
5. **Local run and setup guide** — `scripts/dev.*`, `docs/setup.md`; time it on a clean machine.
   Story 1.
6. **Pull-request gates** — `pr.yml`, branch protection, secret scan. Story 2.
7. **Infrastructure for test** — Terraform, registry, deploy-to-test workflow, smoke tests.
   Story 2.
8. **Monitoring and alerts** — collector, log shipping, health checks, alarms, topics. Story 3.
9. **Production** — production stack, approval gate, fast-forward promotion, rollback workflow.
   Story 2.
10. **Backup and restore** — retention, native backup, runbook, verify script, first recorded
    trial restore, reminder workflow. Story 4.

Steps 1 to 6 need no cloud account and can start today. Steps 7 to 10 need the AWS account, the
domain and the answers to the region and edition questions.

## Complexity Tracking

No constitution violations to justify.

Two choices are recorded because they add moving parts beyond the simplest thing that works:

| Choice | Why needed | Simpler alternative rejected because |
|---|---|---|
| Two projects per module instead of one | Constitution requires module boundaries that fail the build | One project with folders relies on tests alone to stop a cross-module reference |
| Acceptance scenarios evidenced by a recorded run instead of an automated test: US2/AC1–4 (merge blocked, release to test, promotion with approval, rollback), US3/AC3–4 (outage and error-rate alerts), US4/AC1–5 (daily backup, restore, record, missed-backup alert, backup access) | Constitution VII asks for an automated test per criterion. These outcomes are properties of the hosted repository, the pipeline and the cloud account, not of code that a test process can run | Simulating a branch-protection rule, a production approval or a cloud backup inside a unit test would test the simulation. Each is instead run for real and recorded in `validation.md` (T064, T065, T079, T087, T088); the smoke script and the restore verification script automate the checks inside those runs. Task T111 moves whatever a workflow can assert into the workflows once they exist |
| Second, weekly native backup in another account | SH-090 requires that the system can move to Egypt; automated snapshots cannot leave the provider | Snapshots only would leave no backup restorable elsewhere |
