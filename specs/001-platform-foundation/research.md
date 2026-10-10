# Phase 0 Research: Platform Foundation

Decisions already fixed by the constitution (v1.0.0) are not re-opened here: .NET 10, Angular,
SQL Server, modular monolith, monorepo, EF Core, Tailwind + Angular CDK, Hangfire, Docker on ECS
Fargate with RDS, `main` + `develop`. This file settles what the constitution leaves open.

Items marked **Confirm** are defaults chosen so planning is not blocked; each can be changed
without redesign and is listed again at the end.

## R1. Repository layout

- **Decision**: one repository with `src/server` (one .NET solution), `src/web` (one Angular CLI
  workspace), `deploy` (containers, infrastructure, compose), `ops` (runbooks, restore records),
  `.github/workflows`.
- **Rationale**: matches the constitution's monorepo rule; a spec that touches API and UI is one
  branch and one pull request.
- **Alternatives considered**: Nx workspace for the front ends (more tooling than three apps
  need); separate repos (rejected by the constitution).

## R2. Server module shape

- **Decision**: each module is two projects: `Scs.<Module>` (internal; folders `Domain`,
  `Application`, `Infrastructure`, `Api`) and `Scs.<Module>.Contracts` (the only thing other
  modules may reference). A host project `Scs.Api` composes modules. `Scs.SharedKernel` holds
  primitives; `Scs.BuildingBlocks` holds cross-cutting infrastructure (logging, errors, health,
  options, jobs).
- **Rationale**: module boundaries are enforced by the compiler (a module cannot see another
  module's internals because it does not reference that project), and layer direction inside a
  module is enforced by architecture tests on namespaces. Four projects per module would mean
  roughly sixty projects by spec 092 for the same guarantee.
- **Alternatives considered**: four projects per module (slow builds, heavy solution); a single
  project with folders (boundaries rely on tests alone).
- **This spec creates** one module, `Platform` (health, version, client error intake, job
  heartbeat), which doubles as the template later specs copy.

## R3. Architecture tests

- **Decision**: NetArchTest in `tests/Scs.ArchitectureTests`, run in CI. Rules: Domain depends on
  nothing but SharedKernel; Application does not depend on Infrastructure or Api; no module
  references another module's non-Contracts assembly; no `ILogger` call with an interpolated
  string (also enforced by analyzer CA2254 as error).
- **Rationale**: constitution, Technology & Platform Constraints ("Automated architecture tests
  MUST fail the build").
- **Alternatives considered**: ArchUnitNET (richer, more setup); convention by review only.

## R4. Front-end workspace

- **Decision**: Angular CLI multi-project workspace, standalone components, three applications
  (`clinic`, `patient`, `ops`) and two libraries: `core` (runtime config, HTTP, errors, i18n,
  formatting, version check) and `ui` (screen states and shared components on Tailwind + CDK).
  Angular and Node versions are pinned in the repo (`package.json` engines, `.nvmrc`) to the
  current stable Angular major and the Node LTS line it supports, checked at task time.
- **Rationale**: three apps sharing libraries is exactly what the CLI workspace supports.
- **Alternatives considered**: three separate workspaces (shared code would be copied).
- **Note**: the clinic file and the ops file are different design systems. `ui` exposes one set
  of components themed by CSS variables; each app supplies its own token file.
- **Pinned at T005 (10 Oct 2026)**: Angular 22.2, TypeScript 6.0, Node `^24.15.0 || >=26.0.0`
  (`.nvmrc` 26), unit tests on Vitest through the Angular CLI, Tailwind CSS 4 (configured in
  CSS, so there is no `tailwind.config.js`).

## R5. Language switching and right-to-left

- **Decision**: Transloco for runtime translation, one JSON file per language per app plus one
  for `ui`. The language switch sets `lang` and `dir` on `<html>`. Layout uses logical
  properties only (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`); a lint rule rejects `ml-`,
  `mr-`, `pl-`, `pr-`, `left-`, `right-`, `text-left`, `text-right`. A CI script fails when a key
  exists in one language file and not the other, or when a template contains literal text.
- **Rationale**: SH-002 needs a per-user language that can change without a different build;
  Angular's built-in i18n produces one build per locale. Transloco is a translation library, not
  a UI component library, so the constitution's ban does not apply.
- **Alternatives considered**: `@angular/localize` (compile-time, reload to switch, two builds
  per app); ngx-translate (works, weaker tooling for missing keys).
- **Fallback**: a missing key falls back to the other language and is reported through the
  client error channel (spec edge case).

## R6. Dates, time and money

- **Decision**: the server stores and returns UTC instants (ISO 8601 with offset). Front ends
  format through two shared pipes built on `Intl` with `timeZone: 'Africa/Cairo'` and currency
  `EGP`. No component formats a date or amount itself.
- **Rationale**: SH-001; one place to change the form.
- **Confirm**: whether Arabic screens show Arabic-Indic digits (٠١٢) or Western digits. The
  Figma annotations use Arabic-Indic; the pipe takes this from one setting. Default: follow the
  Figma (`ar-EG`), settle in spec 006.

## R7. Uniform error shape

- **Decision**: RFC 9457 Problem Details (`application/problem+json`) with three extensions:
  `code` (stable, upper snake case), `traceId`, and for validation `errors`. `title` is localised
  from `Accept-Language` (`ar` default, `en`). A single exception handler produces every error;
  controllers never build one by hand. Unknown exceptions map to `INTERNAL_ERROR` with no detail.
- **Rationale**: FR-018, FR-030; a standard that client tooling already understands.
- **Alternatives considered**: a custom envelope (nothing gained over the standard).

## R8. Request reference

- **Decision**: the W3C trace id of the request is the reference. Front ends send `traceparent`;
  the server continues it or starts one. It is returned as the `traceId` field of every error and
  in the `X-Trace-Id` response header, and is present on every log entry of that request.
- **Rationale**: FR-020; one identifier for logs, traces and the user-visible reference.
- **Alternatives considered**: a separate short human code (a second id to correlate).

## R9. Logging without personal data

- **Decision**: Serilog writing compact JSON to standard output; the container platform ships
  it. Protection is by default, in four layers:
  1. Request logging records method, route **template**, status, duration, trace id — never the
     path values, query string, headers or body.
  2. Members marked `[PersonalData]` or `[Secret]` (attributes in SharedKernel) are replaced by
     `***` by a destructuring policy; types from module `Domain` namespaces are never
     destructured, only their id.
  3. Analyzer CA2254 as error: no interpolated strings in log calls.
  4. A last-resort scrubber masks anything shaped like an email, an Egyptian mobile number, a
     14-digit national ID, a bearer token or a connection string.
- **Rationale**: FR-021 requires protection that does not depend on each developer remembering;
  SC-007 is tested by submitting personal data and secrets to every input and scanning output.
- **Alternatives considered**: `Microsoft.Extensions.Compliance.Redaction` alone (good
  classification model, but no pattern net for free text); logging to a vendor SDK (portability).

## R10. Traces and metrics

- **Decision**: OpenTelemetry SDK in the API, exporting OTLP to an AWS Distro for OpenTelemetry
  collector sidecar, which forwards to CloudWatch and X-Ray. The application knows only OTLP.
- **Rationale**: SH-090 portability: moving hosts changes the collector, not the code.
- **Alternatives considered**: AWS SDK instrumentation directly in the code (AWS lock-in).

## R11. Health, version and interface documentation

- **Decision**: `GET /health/live` (process only), `GET /health/ready` (database and job storage
  reachable), `GET /v1/meta/version` (version, commit, build time, environment). Business routes
  live under `/v1`. OpenAPI is generated by the built-in generator and served with a viewer only
  when the environment is not production (FR-019).
- **Rationale**: FR-004, FR-005; the load balancer uses `ready`, the container runtime `live`.

## R12. Authorization default

- **Decision**: the host sets a fallback authorization policy that denies every request. The
  four endpoints of this spec are the only ones marked anonymous. Spec 003 adds identity and
  spec 005 replaces the fallback with the permission check.
- **Rationale**: constitution Principle III ("a route is open only when explicitly declared
  so") is true from the first commit instead of being retrofitted.

## R13. Configuration and secrets

- **Decision**: the application reads configuration from environment variables only, bound to
  typed options with `ValidateDataAnnotations().ValidateOnStart()`; a missing value stops
  start-up with the key name and no values. In AWS the values are injected into the task from
  Secrets Manager and Parameter Store. Locally they come from a git-ignored `.env` created from a
  committed `.env.example` holding sample values only. A secret scanner (gitleaks) runs in CI.
- **Rationale**: FR-016, FR-017; environment variables are the portable interface.
- **Alternatives considered**: AWS SDK configuration provider in the app (lock-in).

## R14. One package, three environments

- **Decision**: the API image is tagged with the commit SHA and never rebuilt. Front ends are
  built once and read `config.json` at start-up (API base URL, environment name); that file is
  the only thing that differs per environment. Each front end also serves `version.json`.
- **Rationale**: FR-010 ("same package that ran in test"). Angular's build-time `environment.ts`
  would force a rebuild per environment.

## R15. Pipeline

- **Decision** (**Confirm**: GitHub with GitHub Actions, as the reference repository uses):
  - Pull request: server build with warnings as errors, format check, unit, integration (real
    SQL Server via Testcontainers) and architecture tests; web lint, unit tests, build of three
    apps, translation-completeness check; secret scan. All required for merge.
  - Merge to `develop`: build image and front-end packages once, push by SHA, run migrations,
    deploy to test, run smoke tests.
  - `develop` → `main` by fast-forward only, so the SHA is unchanged; the production workflow
    looks up the packages for that SHA, waits for approval from a required reviewer, then runs
    migrations and deploys. It has no build step.
  - AWS access by OIDC role; no stored cloud keys.
- **Rationale**: FR-009 to FR-012; fast-forward is what makes "same package" literally true.

## R16. Database structure changes

- **Decision**: EF Core migrations in each module's `Infrastructure/Migrations`, one schema per
  module (`platform` here). The pipeline runs a migration bundle as a one-off task before the new
  release starts; the application never migrates on start-up and its database login cannot alter
  structure. Every migration follows expand/contract: a release must run against the structure of
  the release before it, so rollback never needs a structure rollback.
- **Rationale**: FR-013 and the rollback edge case; also prepares Principle II, where the
  isolation policy ships in the same migration as the table.

## R17. Release without interruption, and rollback

- **Decision**: at least two API tasks in two availability zones behind a load balancer; ECS
  rolling deployment at 100% minimum healthy with the deployment circuit breaker and automatic
  rollback on failed readiness. Front ends: hashed assets cached long, `index.html`,
  `config.json`, `version.json` not cached; old assets kept for one release so open tabs keep
  working; the app compares `version.json` periodically and reloads at the next navigation.
  Rollback is a workflow that redeploys the previous SHA (API and front ends).
- **Rationale**: FR-011, FR-012, SC-004, SC-005.

## R18. Hosting topology

- **Decision**: per environment — VPC over two zones; ALB with a managed certificate, HTTP
  redirected to HTTPS, HSTS; ECS Fargate service for the API; RDS for SQL Server 2022 in private
  subnets; S3 + CloudFront for each front end; ECR; Secrets Manager; CloudWatch. Defined in
  Terraform under `deploy/infra`, one module set, one variable file per environment.
  Production: Multi-AZ database. Test: single-AZ, smallest size.
- **Rationale**: FR-014, FR-015; Terraform is provider-neutral in tooling, which keeps the move
  to an Egyptian host a rewrite of modules, not of process.
- **Alternatives considered**: AWS CDK (AWS-only); console-built infrastructure (not repeatable).
- **Confirm**: region `me-south-1` (Bahrain) versus `me-central-1` (UAE); it is one variable.
- **Confirm**: SQL Server edition for production. Multi-AZ needs Standard Edition, which is the
  largest single cost in the foundation; Web Edition is cheaper but single-AZ.

## R19. Monitoring and alerts

- **Decision**: Route 53 health checks from outside on the API `ready` endpoint and on each
  front end's `version.json`; CloudWatch alarms on those, on load-balancer 5xx rate (over 2% for
  5 minutes) and p95 response time (over 2 seconds for 10 minutes), and on RDS backup events.
  Alarms publish to one SNS topic per environment; the message carries the environment name.
- **Rationale**: FR-023, FR-024, FR-029, SC-008. Thresholds are working defaults.

## R20. Front-end failures

- **Decision**: a global error handler in `core` posts unhandled failures to
  `POST /v1/client-errors` (app, version, route template, message, stack, trace id). The server
  scrubs and logs them like any entry. The endpoint is anonymous, size-limited and rate-limited.
- **Rationale**: FR-025 without sending anything to a third-party service outside the hosting
  region (Principle V).
- **Alternatives considered**: Sentry or similar (data leaves the region).

## R21. Backup and tested restore

- **Decision**: RDS automated daily backups with point-in-time recovery, 30-day retention,
  encrypted with a customer-managed key; plus a weekly native `.bak` to a separate encrypted
  bucket in a different account, which is the copy that can be restored outside AWS. Restore
  test, quarterly: restore the latest backup to a throwaway instance, run
  `ops/restore/verify.sql`, record the result in `ops/restore-tests/YYYY-MM-DD.md`, destroy the
  instance. A scheduled reminder issue opens when a quarter has no record.
- **Rationale**: FR-026 to FR-029, SC-009, SC-010; the native backup serves SH-090.

## R22. Background work

- **Decision**: Hangfire with SQL Server storage in schema `hangfire`, hosted in the API process
  and switchable to a separate worker by configuration (`SCS_ROLE=api|worker|all`). Automatic
  retry with back-off; failures are logged with the job id and counted in a metric with an alarm.
  The dashboard is enabled outside production only until operator sign-in exists (spec 010). One
  recurring job, `platform-heartbeat`, proves the path end to end.
- **Rationale**: FR-035. Spec 002 adds the filter that sets the clinic context for a job.

## R23. Testing tools

- **Decision**: server — xUnit with its built-in assertions, `WebApplicationFactory`,
  Testcontainers for SQL Server, NetArchTest. Web — the Angular CLI's default unit runner, ESLint,
  Prettier, Playwright for the smoke and state/direction checks (SC-012).
- **Rationale**: constitution Principle VII requires a real SQL Server for anything touching
  isolation; using it from the first spec avoids two test styles.

## R24. Local environment

- **Decision**: `deploy/compose/docker-compose.yml` starts SQL Server and, with a profile, the
  API; `scripts/dev.ps1` and `scripts/dev.sh` create `.env` from the example, start the database,
  apply migrations and start the API and the three front ends on fixed ports.
- **Rationale**: FR-006, SC-001 (30 minutes on a clean machine).

## R25. Addresses and API routing

- **Decision** (constitution v1.1.0, replaces SH-003): operators on `hetasystems-scsadmin.app`;
  staff on `app.scs-heta.com`; patients on `scs-heta.com/<clinic>`. Each address is a CDN
  distribution with two origins: the app's static files, and the one load balancer for `/api/*`
  (prefix stripped at the edge). Test uses `test.hetasystems-scsadmin.app`,
  `app.test.scs-heta.com` and `test.scs-heta.com`. The patient distribution serves `index.html`
  for any path that is not a file or `/api`.
- **Rationale**: staff and patient screens are on different origins, so a flaw in a public page
  cannot reach a staff session; same-origin API calls need no cross-origin allowance and no
  third-party cookies; no wildcard DNS or certificate. `.app` domains are HTTPS-only by browser
  preload, which matches FR-015.
- **Alternatives considered**: everything under `scs-heta.com` by path (staff and patients would
  share an origin); a subdomain per clinic as SH-003 has it (wildcard DNS and certificates, and
  the product owner asked for main domains).
- **Consequences for later specs**: staff sign-in cannot read the clinic from the address, so it
  comes from the account (spec 003; an email used in two clinics needs a rule). Clinic names
  become path segments, so they need a reserved-word list starting with `api` (spec 002).
- **Changes to earlier decisions**: R13 — `SCS_CORS__ORIGINS` is used in `local` only and must be
  empty elsewhere. R14 — `config.json` carries `apiBaseUrl: "/api"` outside `local`. R19 — health
  checks call `/api/health/ready` through each address.

## Items to confirm

| # | Item | Default used | Effect of changing |
|---|---|---|---|
| R4 | Angular major and Node LTS | Current stable, pinned at task time | Version numbers only |
| R6 | Digits on Arabic screens | Follow Figma (`ar-EG`) | One setting in `core` |
| R15 | Source host and CI | GitHub + GitHub Actions | Workflow files only |
| R18 | AWS region | `me-south-1` | One Terraform variable |
| R18 | SQL Server edition in production | Standard, Multi-AZ | Cost and availability |
| R19 | Alert thresholds | 2% errors / 5 min; p95 2 s / 10 min | Alarm settings |
