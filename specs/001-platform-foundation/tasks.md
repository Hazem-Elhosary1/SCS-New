---

description: "Task list for 001-platform-foundation"
---

# Tasks: Platform Foundation

**Input**: Design documents from `/specs/001-platform-foundation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Constitution Principle VII requires an automated test for every acceptance
criterion. Test tasks come before the implementation they cover and must be seen to fail first.

**Organization**: Tasks are grouped by user story. Tasks tagged **(cloud)** need the AWS account,
the `scs-heta.com` and `hetasystems-scsadmin.app` domains and the region/edition answers from research.md; everything else
runs on a developer machine.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US5)
- Paths are relative to the repository root, per plan.md "Project Structure"

## Path Conventions

- Server: `src/server/` (solution `src/server/Scs.sln`)
- Web: `src/web/` (apps and libraries under `src/web/projects/`)
- Infrastructure: `deploy/`; operations: `ops/`; pipeline: `.github/workflows/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: An empty repository that builds.

- [ ] T001 (files written and `git init` done on `main`; the first commit and the `develop` branch are left to the repository owner) Initialise git at the repository root with branches `main` and `develop`; add `.gitignore` (.NET, Node, Terraform, `.env`), `.gitattributes` (LF line endings), `.editorconfig` and a root `README.md` that links to `docs/setup.md`
- [X] T002 Create `src/server/Scs.sln`, `src/server/global.json` (SDK 10.0.x, rollForward latestFeature), `src/server/Directory.Build.props` (Nullable enable, TreatWarningsAsErrors true, AnalysisLevel latest-all, CA2254 as error) and `src/server/Directory.Packages.props` (central package versions)
- [X] T003 [P] Create and add to the solution `src/server/host/Scs.Api/Scs.Api.csproj` (ASP.NET Core), `src/server/shared/Scs.SharedKernel/Scs.SharedKernel.csproj` and `src/server/shared/Scs.BuildingBlocks/Scs.BuildingBlocks.csproj`; references: Api → BuildingBlocks → SharedKernel
- [X] T004 [P] Create and add to the solution `src/server/tests/Scs.UnitTests`, `src/server/tests/Scs.IntegrationTests` (xUnit, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.MsSql) and `src/server/tests/Scs.ArchitectureTests` (xUnit, NetArchTest.Rules)
- [X] T005 [P] Create the Angular CLI workspace in `src/web/` with standalone apps `clinic` (port 4201), `patient` (4202), `ops` (4203) and libraries `core` and `ui` under `src/web/projects/`; pin the current stable Angular major and its supported Node LTS in `src/web/package.json` (`engines`) and `src/web/.nvmrc`, and record the chosen versions in `specs/001-platform-foundation/research.md` R4
- [X] T006 [P] Add Tailwind CSS to the workspace in `src/web/tailwind.config.js` (content globs for all five projects) and an empty token file per app at `src/web/projects/{clinic,patient,ops}/src/styles/tokens.css`
- [X] T007 [P] Configure ESLint and Prettier for the workspace in `src/web/eslint.config.js` and `src/web/.prettierrc`; add scripts `lint`, `test`, `build:all`, `ci`, `e2e`, `i18n:check` to `src/web/package.json` (`ci` = lint + test + build:all + i18n:check)
- [X] T008 [P] Create `deploy/compose/docker-compose.yml` with a `sqlserver` service (SQL Server 2022 image, named volume, health check) and `deploy/compose/.env.example` containing sample values only for every key in data-model.md "Configuration value"
- [X] T009 [P] Add the `dotnet-ef` local tool in `src/server/.config/dotnet-tools.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The host pipeline and the shared pieces every story builds on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T010 Create the test fixture `src/server/tests/Scs.IntegrationTests/Fixtures/ApiFixture.cs`: a `WebApplicationFactory<Program>` that sets required configuration, can start a SQL Server Testcontainer on demand, and captures all log output into an in-memory list for assertions
- [X] T011 Write architecture tests (must fail to compile or fail first) in `src/server/tests/Scs.ArchitectureTests/LayeringTests.cs` and `ModuleBoundaryTests.cs`: `*.Domain` depends only on SharedKernel; `*.Application` does not depend on `*.Infrastructure` or `*.Api`; no module assembly references another module's non-`.Contracts` assembly
- [X] T012 [P] Add to `src/server/shared/Scs.SharedKernel/`: `ErrorCodes.cs` with the nine codes of contracts/conventions.md §4 (`VALIDATION_FAILED`, `MALFORMED_REQUEST`, `UNAUTHENTICATED`, `FORBIDDEN`, `NOT_FOUND`, `PAYLOAD_TOO_LARGE`, `RATE_LIMITED`, `INTERNAL_ERROR`, `SERVICE_UNAVAILABLE`), `PersonalDataAttribute.cs` and `SecretAttribute.cs`
- [X] T013 [P] Add `src/server/shared/Scs.BuildingBlocks/Modules/IModule.cs` (methods to register services and map endpoints) and module discovery in `src/server/host/Scs.Api/ModuleRegistration.cs`
- [X] T014 Write `src/server/tests/Scs.IntegrationTests/Configuration/StartupValidationTests.cs`: with `SCS_DB__CONNECTION` or `SCS_ENVIRONMENT` missing, start-up throws, the message names the missing key, and the message contains none of the configured values; with `SCS_CORS__ORIGINS` non-empty and environment `test` or `production`, start-up throws
- [X] T015 Implement typed options in `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` for the keys in data-model.md (`SCS_ENVIRONMENT` required, one of `local|test|production`; `SCS_ROLE` default `all`, one of `api|worker|all`; `SCS_DB__CONNECTION` required; `SCS_CORS__ORIGINS` allowed in `local` only and must be empty in `test` and `production`; `SCS_OTEL__ENDPOINT` optional; `SCS_APIDOCS__ENABLED` default `false`; `SCS_VERSION`, `SCS_COMMIT`, `SCS_BUILT_AT`) with `ValidateDataAnnotations().ValidateOnStart()`; makes T014 pass
- [X] T016 Write `src/server/tests/Scs.IntegrationTests/Errors/ProblemShapeTests.cs`: unknown route → 404 `NOT_FOUND`; unreadable body → 400 `MALFORMED_REQUEST`; a test endpoint that throws → 500 `INTERNAL_ERROR`; every body is `application/problem+json` with `type`, `title`, `status`, `code`, `traceId` (32 hex), the `X-Trace-Id` header equals `traceId`, and no body contains a stack trace, file path, type name or SQL text
- [X] T017 Implement `src/server/shared/Scs.BuildingBlocks/Tracing/TraceIdMiddleware.cs` (continue `traceparent` or start one; set `X-Trace-Id` on every response) and `src/server/shared/Scs.BuildingBlocks/Errors/ProblemDetailsExceptionHandler.cs` plus `ScsProblem.cs` (single place that builds every error); makes T016 pass
- [X] T018 Implement baseline logging in `src/server/shared/Scs.BuildingBlocks/Logging/LoggingSetup.cs`: Serilog compact JSON to standard output with fields `env`, `service`, `version`, `traceId`; one request-completed entry with `route` (route template only), `status`, `durationMs`; never path values, query string, headers or bodies; `Debug` level in `local` only
- [X] T019 Write `src/server/tests/Scs.IntegrationTests/Security/FallbackPolicyTests.cs`: a test endpoint with no authorization metadata answers 401 `UNAUTHENTICATED`; one marked anonymous answers 200
- [X] T020 Compose the host in `src/server/host/Scs.Api/Program.cs`: options, logging, trace id, exception handler, HTTPS redirection, HSTS when environment is not `local`, CORS from `SCS_CORS__ORIGINS` in `local` only (no CORS middleware elsewhere), fallback authorization policy that denies all, `/v1` route group, module registration; makes T019 pass
- [X] T021 Write `src/server/tests/Scs.IntegrationTests/Health/LiveEndpointTests.cs` and implement `GET /health/live` in `src/server/shared/Scs.BuildingBlocks/Health/HealthEndpoints.cs` (anonymous, no dependency checks, body `{"status":"Healthy"}` per contracts/platform-api.openapi.yaml)
- [X] T022 Write `src/server/tests/Scs.IntegrationTests/Security/ApiDocsTests.cs` and implement OpenAPI generation and viewer in `src/server/host/Scs.Api/ApiDocs.cs`: reachable only when `SCS_APIDOCS__ENABLED=true` and environment is not `production`; 404 in `production` whatever the flag
- [X] T023 [P] Implement runtime configuration in `src/web/projects/core/src/lib/config/runtime-config.service.ts` with an app initializer that fetches `config.json` (`environment` one of `local|test|production`; `apiBaseUrl` must be `/api` outside `local`, an absolute `http://localhost` address allowed in `local` only) and fails start-up visibly when missing or invalid; add `src/web/projects/{clinic,patient,ops}/public/config.json` for `local`; unit test in `runtime-config.service.spec.ts`
- [X] T024 [P] Implement the HTTP layer in `src/web/projects/core/src/lib/http/`: `api.interceptor.ts` (prefix `apiBaseUrl`, add `traceparent` and `Accept-Language`), `problem.ts` (type and parser for the Problem shape; a non-Problem failure becomes `SERVICE_UNAVAILABLE` with a locally generated reference); unit tests alongside

**Checkpoint**: The API starts, refuses everything but `/health/live`, and answers every failure in one shape. The three apps build and load their configuration.

---

## Phase 3: User Story 1 - Run the whole system from a clean copy (Priority: P1) 🎯 MVP

**Goal**: One guide takes a developer from a clean machine to the API, its database and three front ends running together.

**Independent Test**: quickstart.md Scenario 1 — on a clean machine follow `docs/setup.md` only; all three apps open and show the platform as reachable; stopping the database turns `ready` to 503 and each app shows its error state.

### Tests for User Story 1 ⚠️

> Write these first and see them fail.

- [X] T025 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Health/ReadyEndpointTests.cs`: with the database up → 200 with checks `database` and `jobs` `Healthy`; with the container stopped → 503, `status` `Unhealthy`, the `database` check `Unhealthy`, while `/health/live` still answers 200
- [X] T026 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Meta/VersionEndpointTests.cs`: `GET /v1/meta/version` is anonymous and returns `version`, `commit` (40 hex), `builtAt` (date-time) and `environment` per contracts/platform-api.openapi.yaml
- [X] T027 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Persistence/MigrationTests.cs`: the migrator applied to an empty database creates schema `platform`, table `platform.JobHeartbeat` with exactly one row (`Id = 1`), and `platform.__EFMigrationsHistory`; running it twice changes nothing; inserting a second row fails the check constraint; the application login cannot `CREATE TABLE` or `ALTER TABLE`
- [X] T028 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Jobs/HeartbeatJobTests.cs`: running the job sets `LastRunAtUtc` to now and `LastRunVersion` to the running version; running it twice leaves one row
- [X] T029 [P] [US1] Write `src/web/projects/core/src/lib/platform/platform-status.service.spec.ts`: reports reachable with version and environment when `/v1/meta/version` answers; reports unreachable with a reference when it fails

### Implementation for User Story 1

- [X] T030 [US1] Create the `Platform` module: `src/server/modules/Platform/Scs.Platform/Scs.Platform.csproj` with folders `Domain/`, `Application/`, `Infrastructure/`, `Api/`, `src/server/modules/Platform/Scs.Platform.Contracts/Scs.Platform.Contracts.csproj`, and `PlatformModule.cs` implementing `IModule`; add both to the solution and reference from `Scs.Api`
- [X] T031 [US1] Create the entity `src/server/modules/Platform/Scs.Platform/Domain/JobHeartbeat.cs` with the fields of data-model.md: `Id` "smallint, PK — Always `1`; single row, enforced by a check constraint"; `JobName` "nvarchar(100) — `platform-heartbeat`"; `LastRunAtUtc` "datetimeoffset — Set by the job on every run"; `LastRunVersion` "nvarchar(40) — Version of the release that ran it"
- [X] T032 [US1] Create `src/server/modules/Platform/Scs.Platform/Infrastructure/Persistence/PlatformDbContext.cs` and `JobHeartbeatConfiguration.cs`: default schema `platform`, migrations history table `platform.__EFMigrationsHistory`, check constraint `Id = 1`, column types exactly as in T031
- [X] T033 [US1] Generate the first migration in `src/server/modules/Platform/Scs.Platform/Infrastructure/Migrations/` (create schema, table, constraint; seed the single row with `JobName = 'platform-heartbeat'`)
- [X] T034 [P] [US1] Create `deploy/compose/sql/create-logins.sql`: login `scs_app` with read/write on data and no structure rights, login `scs_migrator` with structure rights; make the compose `sqlserver` service run it once
- [X] T035 [US1] Create the migration entry point `src/server/tools/Scs.Migrator/Program.cs` (applies every module's migrations in order using `SCS_DB__MIGRATION_CONNECTION`, exits non-zero on failure); confirm the API never migrates on start-up; makes T027 pass
- [X] T036 [US1] Implement background work in `src/server/shared/Scs.BuildingBlocks/Jobs/`: `IJobScheduler.cs`, `HangfireSetup.cs` (SQL Server storage in schema `hangfire`, server started only when `SCS_ROLE` is `worker` or `all`, automatic retry with back-off up to five attempts, failure logged at `Error` with `jobId` and `jobName`, dashboard mapped only when environment is not `production`)
- [X] T037 [US1] Implement `src/server/modules/Platform/Scs.Platform/Application/HeartbeatJob.cs` (idempotent; updates the single row) and register it as recurring job `platform-heartbeat` every minute in `PlatformModule.cs`; makes T028 pass
- [X] T038 [US1] Add readiness checks `database` and `jobs` and `GET /health/ready` in `src/server/shared/Scs.BuildingBlocks/Health/HealthEndpoints.cs` (503 when any check fails, each check with `name`, `status`, `durationMs`); makes T025 pass
- [X] T039 [US1] Implement `GET /v1/meta/version` in `src/server/modules/Platform/Scs.Platform/Api/VersionEndpoints.cs` (anonymous; values from `SCS_VERSION`, `SCS_COMMIT`, `SCS_BUILT_AT`, `SCS_ENVIRONMENT`); makes T026 pass
- [X] T040 [US1] Implement `src/web/projects/core/src/lib/platform/platform-status.service.ts`; makes T029 pass
- [X] T041 [P] [US1] Create the placeholder page `src/web/projects/clinic/src/app/placeholder/placeholder.page.ts` as the default route: app name, platform reachable or not, platform version and environment; on failure a plain error block with the reference (replaced by `<scs-state>` in US5)
- [X] T042 [P] [US1] Create the same placeholder page for `src/web/projects/patient/src/app/placeholder/placeholder.page.ts`
- [X] T043 [P] [US1] Create the same placeholder page for `src/web/projects/ops/src/app/placeholder/placeholder.page.ts`
- [X] T044 [US1] Write `scripts/dev.ps1` and `scripts/dev.sh`: create `deploy/compose/.env` from `.env.example` when absent, start `sqlserver`, wait for health, run `Scs.Migrator`, start the API on port 5080 and the three apps on 4201 to 4203; print the four addresses
- [X] T045 [US1] Write the setup guide `docs/setup.md`: prerequisites with versions, the single command, the expected addresses and results table from quickstart.md Scenario 1, how to stop, common failures
- [ ] T046 [US1] Run quickstart.md Scenario 1 on a machine that has never held the project, following `docs/setup.md` only; record the elapsed time and any undocumented step in `specs/001-platform-foundation/validation.md`; fix the guide until it is under 30 minutes with no extra step (SC-001)

**Checkpoint**: The whole system runs locally from one command. MVP reached.

---

## Phase 4: User Story 2 - A change reaches users only through the gates (Priority: P1)

**Goal**: Failing changes cannot merge; a merged change reaches test automatically; the same package is promoted to production after approval and can be rolled back.

**Independent Test**: quickstart.md Scenarios 2a and 2b — three deliberately broken pull requests are each blocked with the check named; a passing one reaches test within 15 minutes; production later reports the same `commit` and `builtAt`; rollback returns the previous commit within 15 minutes.

### Tests for User Story 2 ⚠️

- [ ] T047 [P] [US2] Write `src/web/projects/core/src/lib/version/version-check.service.spec.ts`: reads `version.json` at start; when a later read shows a different `commit` it flags an update and reloads at the next navigation, never immediately
- [ ] T048 [P] [US2] Write the smoke test `deploy/smoke/smoke.sh`: given the three front-end URLs, assert through each one that `/api/health/ready` is 200 and `/api/v1/meta/version` reports the expected commit, and each front end's `version.json` reports the same commit; exit non-zero otherwise

### Implementation for User Story 2

- [ ] T049 [US2] Create `.github/workflows/pr.yml` with required jobs: `server` (restore, `dotnet format --verify-no-changes`, build, unit, integration and architecture tests), `web` (`npm ci`, `npm run ci`), `secrets` (gitleaks); triggered on pull requests to `develop` and `main`
- [ ] T050 [P] [US2] Create `deploy/docker/Dockerfile.api` (multi-stage, non-root, build args `VERSION`, `COMMIT`, `BUILT_AT` set as `SCS_VERSION`, `SCS_COMMIT`, `SCS_BUILT_AT`) and `deploy/docker/Dockerfile.migrator`
- [ ] T051 [P] [US2] Create `src/web/tools/write-version.mjs` that writes `version.json` (`app`, `version`, `commit`, `builtAt`) into each app's build output, and wire it into `build:all` in `src/web/package.json`
- [ ] T052 [US2] Implement `src/web/projects/core/src/lib/version/version-check.service.ts` (re-read every 10 minutes) and start it from each app's bootstrap; makes T047 pass
- [ ] T053 [US2] Write `docs/database-changes.md`: the expand/contract rule (a release must run against the structure of the release before it; removal ships one release later), with one worked example; link it from `docs/setup.md`
- [ ] T054 [US2] Write `ops/runbooks/repository-settings.md` and apply it: `main` and `develop` protected, pull request required, the three `pr.yml` jobs required, `main` accepts fast-forward from `develop` only, environment `production` with required reviewers
- [ ] T055 [P] [US2] (cloud) Create Terraform module `deploy/infra/modules/network/` (VPC over two availability zones, public and private subnets) and `deploy/infra/modules/registry/` (container registry, package bucket keyed by commit SHA)
- [ ] T056 [P] [US2] (cloud) Create Terraform module `deploy/infra/modules/database/` (RDS for SQL Server 2022 in private subnets, encrypted, Multi-AZ switchable by variable) and `deploy/infra/modules/secrets/` (Secrets Manager entries for the secret keys of data-model.md)
- [ ] T057 [P] [US2] (cloud) Create Terraform module `deploy/infra/modules/api/` (load balancer with managed certificate, HTTP redirected to HTTPS, target group health check on `/health/ready`; ECS Fargate service with at least two tasks, rolling deployment at 100% minimum healthy, deployment circuit breaker with rollback; one-off task definition for the migrator; secrets injected as environment variables)
- [ ] T058 [P] [US2] (cloud) Create Terraform module `deploy/infra/modules/static-site/` (bucket + CDN per front end on its own address per research R25: `ops` on `hetasystems-scsadmin.app`, `clinic` on `app.scs-heta.com`, `patient` on `scs-heta.com` serving `index.html` for any path that is not a file or `/api`, with `test.` variants for test; each distribution forwards `/api/*` to the API load balancer with the prefix stripped and no caching; one certificate and DNS zone per domain; hashed assets cached long; `index.html`, `config.json`, `version.json` not cached; per-environment `config.json` written at deploy; previous release's assets retained)
- [ ] T059 [US2] (cloud) Create `deploy/infra/main.tf`, `deploy/infra/variables.tf`, `deploy/infra/env/test.tfvars` and `deploy/infra/env/production.tfvars` (region as one variable, default `me-south-1`; test single-AZ smallest size; production Multi-AZ) and `deploy/infra/modules/ci-role/` (OIDC role for the pipeline, no stored keys); apply the test environment
- [ ] T060 [US2] (cloud) Create `.github/workflows/deploy-test.yml`: on push to `develop` build the API image, migrator image and three front-end packages once, label and store them by commit SHA, run the migrator task, deploy the API and front ends to test, run `deploy/smoke/smoke.sh`
- [ ] T061 [US2] (cloud) Create `.github/workflows/deploy-production.yml`: on push to `main` verify the commit is the one built on `develop`, fetch the stored packages for that SHA, wait for approval on environment `production`, run the migrator, deploy, run the smoke test; the workflow contains no build step
- [ ] T062 [US2] (cloud) Create `.github/workflows/rollback.yml` (manual; input environment; redeploys the previous SHA for API and front ends and runs the smoke test) and write `ops/runbooks/release.md` and `ops/runbooks/rollback.md`
- [ ] T063 [US2] (cloud) Apply the production environment with `deploy/infra/env/production.tfvars`
- [ ] T064 [US2] Run quickstart.md Scenario 2a: open three pull requests that break, in turn, a layering rule, a translation key and a logical-spacing rule (the last two after US5); confirm each is blocked and names the check; record in `specs/001-platform-foundation/validation.md` (SC-002)
- [ ] T065 [US2] (cloud) Run quickstart.md Scenario 2b: time merge-to-test (SC-003), compare `commit` and `builtAt` between test and production, run the one-request-per-second loop during a production release (SC-004), time a rollback (SC-005); record in `specs/001-platform-foundation/validation.md`

**Checkpoint**: Nothing reaches users except through the pipeline, and the last release can be put back.

---

## Phase 5: User Story 3 - See trouble and trace it (Priority: P2)

**Goal**: A reference on an error leads to the full trail of that request; logs never hold personal data or secrets; the operations team is alerted without anyone reporting.

**Independent Test**: quickstart.md Scenario 3 — the redaction test finds zero occurrences; a `traceId` from a Problem finds exactly that request's entries; stopping the test API produces an alert naming `test` within 5 minutes.

### Tests for User Story 3 ⚠️

- [ ] T066 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Logging/LogRedactionTests.cs` (trait `Category=LogRedaction`): send a name, an Egyptian mobile number, an email, a 14-digit national ID, a bearer token and a connection string through body, query, headers and path of every mapped endpoint, on success and on failure, and through a logged object with `[PersonalData]` and `[Secret]` members; assert zero occurrences in all captured log output (SC-007)
- [ ] T067 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Logging/TraceCorrelationTests.cs`: every log entry caused by a request carries that request's `traceId`; two concurrent requests never share one; an incoming `traceparent` is continued
- [ ] T068 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/ClientErrors/ClientErrorsEndpointTests.cs` against contracts/platform-api.openapi.yaml: valid body → 202; unknown property → 400 `VALIDATION_FAILED`; `app` outside `clinic|patient|ops` → 400; `message` over 1000 or `stack` over 8000 or `route` over 200 characters → 400; body over 16 KB → 413 `PAYLOAD_TOO_LARGE`; the 31st request in a minute from one address → 429 `RATE_LIMITED`; the logged entry has `service` `web-<app>` and is scrubbed
- [ ] T069 [P] [US3] Write `src/web/projects/core/src/lib/errors/global-error.handler.spec.ts`: an unhandled error posts to `/v1/client-errors` with the route template, not the address with values; no form values; at most one report per distinct message per minute

### Implementation for User Story 3

- [ ] T070 [US3] Implement `src/server/shared/Scs.BuildingBlocks/Logging/ClassifiedDataDestructuringPolicy.cs`: members marked `[PersonalData]` or `[Secret]` are written as `***`; types in a `*.Domain` namespace are written as their id only
- [ ] T071 [US3] Implement `src/server/shared/Scs.BuildingBlocks/Logging/ScrubbingEnricher.cs`: masks anything shaped like an email, an Egyptian mobile number, a 14-digit national ID, a bearer token or a connection string in message, properties and exception text; register both in `LoggingSetup.cs`; makes T066 and T067 pass
- [ ] T072 [US3] Add an architecture test in `src/server/tests/Scs.ArchitectureTests/LoggingRulesTests.cs` that fails when any assembly calls `Console.Write*` or references a logging sink directly instead of `ILogger`
- [ ] T073 [US3] Implement `POST /v1/client-errors` in `src/server/modules/Platform/Scs.Platform/Api/ClientErrorEndpoints.cs` with the request model in `Scs.Platform.Contracts/ClientErrorReport.cs` (constraints exactly as T068), a 16 KB body limit and a 30-per-minute-per-address rate limit; makes T068 pass
- [ ] T074 [US3] Implement `src/web/projects/core/src/lib/errors/global-error.handler.ts` and provide it in each app's bootstrap; makes T069 pass
- [ ] T075 [US3] Implement `src/server/shared/Scs.BuildingBlocks/Telemetry/OpenTelemetrySetup.cs`: traces and metrics for ASP.NET Core, HTTP client, SQL client and Hangfire; OTLP export to `SCS_OTEL__ENDPOINT` when set, nothing when unset; a counter `scs.jobs.failed`
- [ ] T076 [P] [US3] (cloud) Extend `deploy/infra/modules/api/` with the collector sidecar and container log shipping to one log group per environment with 30-day retention, searchable by `traceId`, time and level
- [ ] T077 [P] [US3] (cloud) Create `deploy/infra/modules/monitoring/`: external health checks on `/api/health/ready` and `version.json` through each of the three addresses; alarms `unavailable` (failing for 5 minutes), `error-rate` (5xx over 2% for 5 minutes), `slow` (p95 over 2 seconds for 10 minutes), `job-failures`; one notification topic per environment; every alarm name and message includes the environment name
- [ ] T078 [US3] Write `ops/runbooks/alerts.md`: each alert, what it means, first checks, how to find a request by its reference
- [ ] T079 [US3] (cloud) Run quickstart.md Scenario 3 against test: find a request by `traceId` and time it (SC-006); scale the API to zero and time the alert (SC-008); record in `specs/001-platform-foundation/validation.md`

**Checkpoint**: Failures are traceable by reference, logs are clean by default, and outages announce themselves.

---

## Phase 6: User Story 4 - Lose nothing, and prove it (Priority: P2)

**Goal**: A daily backup nobody starts, a restore that has been tried and recorded, and an alert when a backup is missed.

**Independent Test**: quickstart.md Scenario 4 — a backup exists for each of the last 7 days; restoring the latest into a separate environment passes `ops/restore/verify.sql` within 4 hours; a record exists; a missed backup raises `backup-missed`.

- [ ] T080 [P] [US4] Write `ops/restore/verify.sql`: fails unless every expected row of `platform.__EFMigrationsHistory` is present, `platform.JobHeartbeat` has exactly one row, and `LastRunAtUtc` is within 15 minutes before the backup time passed as a variable
- [ ] T081 [P] [US4] Write `ops/restore-tests/TEMPLATE.md` with the fields of data-model.md "Restore test record": `date`, `operator`, `backupTakenAt`, `durationMinutes`, `result` (`pass` or `fail`), `notes` (required when `fail`)
- [ ] T082 [US4] (cloud) Extend `deploy/infra/modules/database/`: automated daily backups with point-in-time recovery, 30-day retention, customer-managed encryption key, backup access limited to the operations role
- [ ] T083 [US4] (cloud) Create `deploy/infra/modules/native-backup/`: a weekly scheduled task that takes a native `.bak` of the production database into an encrypted bucket in a separate account with 90-day retention; no application code involved
- [ ] T084 [US4] (cloud) Add the `backup-missed` alarm to `deploy/infra/modules/monitoring/` (no completed automated backup in 26 hours, or a failed native backup), publishing to the environment topic
- [ ] T085 [US4] (cloud) Create `deploy/infra/restore-test/` (throwaway stack: isolated network and one database instance restored from a chosen backup) and write `ops/runbooks/restore.md` with the exact steps of quickstart.md Scenario 4, including that a failed verification is a production incident
- [ ] T086 [P] [US4] Create `.github/workflows/restore-reminder.yml`: on the first day of each quarter, open an issue if `ops/restore-tests/` has no record dated in the previous quarter
- [ ] T087 [US4] (cloud) Run the first trial restore by `ops/runbooks/restore.md`; write `ops/restore-tests/<date>.md`; confirm backups exist for each day production has run (SC-009) and the duration is under 4 hours (SC-010)
- [ ] T088 [US4] (cloud) In test, prevent one daily backup and confirm the `backup-missed` alert arrives by the next morning; record in `specs/001-platform-foundation/validation.md`

**Checkpoint**: Data can be put back, and there is a dated record that it was.

---

## Phase 7: User Story 5 - Every later feature inherits the same behaviour (Priority: P3)

**Goal**: Language and direction, time and money, the six screen states and localised errors are decided once and demonstrated on each placeholder page.

**Independent Test**: quickstart.md Scenario 5 — `npm --prefix src/web run e2e` passes for three apps in `ar` and `en`: direction matches language, no untranslated text, Cairo time and EGP, six states render, a forced server error shows `title` and the reference.

### Tests for User Story 5 ⚠️

- [ ] T089 [P] [US5] Write `src/web/e2e/placeholder.spec.ts` (Playwright, parameterised over `clinic`, `patient`, `ops` × `ar`, `en`): `<html dir>` is `rtl` for `ar` and `ltr` for `en`; switching language changes every visible text without reload; no text equals its key; the date shows Cairo time and the amount shows EGP; each of `loading`, `empty`, `error`, `offline`, `view-only`, `not-in-plan` renders; a forced 500 shows the server `title` and the `traceId` (SC-012)
- [ ] T090 [P] [US5] Write `src/web/projects/core/src/lib/format/scs-date.pipe.spec.ts` and `scs-money.pipe.spec.ts`: a UTC instant renders in `Africa/Cairo` across a daylight-saving change; formats `date`, `time`, `dateTime`; a decimal string renders as Egyptian pounds; output differs between `ar` and `en`
- [ ] T091 [P] [US5] Write `src/server/tests/Scs.IntegrationTests/Errors/ProblemLocalizationTests.cs`: `Accept-Language: en` gives an English `title` and `Content-Language: en`; `ar`, absent, or any other value gives Arabic; `code` is identical in both
- [ ] T092 [P] [US5] Write `src/web/tools/i18n-check.spec.mjs`: the checker fails when a key exists in only one of `ar.json` and `en.json`, when a value is empty, and when a template contains literal visible text

### Implementation for User Story 5

- [ ] T093 [US5] Set up Transloco in `src/web/projects/core/src/lib/i18n/`: `i18n.providers.ts` (languages `ar` default and `en`), `language.service.ts` (switch without reload, set `<html lang dir>`, remember per browser), `missing-key.handler.ts` (show the other language's text and report through the client error channel)
- [ ] T094 [P] [US5] Create translation files `src/web/projects/{clinic,patient,ops}/public/i18n/{ar,en}.json` and `src/web/projects/ui/i18n/{ar,en}.json` with keys named `area.element.purpose`; Arabic text follows SH-098 (formal Arabic, no gendered address, controls labelled with the verbal noun) and the BRD glossary
- [ ] T095 [US5] Implement `src/web/tools/i18n-check.mjs` and wire it to `npm run i18n:check`; makes T092 pass
- [ ] T096 [US5] Add to `src/web/eslint.config.js` a rule that rejects the physical Tailwind utilities `ml-`, `mr-`, `pl-`, `pr-`, `left-`, `right-`, `text-left`, `text-right` in templates and class bindings; fix any existing use
- [ ] T097 [US5] Implement `src/web/projects/core/src/lib/format/scs-date.pipe.ts` and `scs-money.pipe.ts` on `Intl` (`timeZone: 'Africa/Cairo'`, currency `EGP`, digits taken from one setting in `core`, default `ar-EG`); makes T090 pass
- [ ] T098 [US5] Fill `src/web/projects/clinic/src/styles/tokens.css` and `src/web/projects/patient/src/styles/tokens.css` from the Figma file "Smart Clinic OS — Design System & Screens" (fileKey `4HLWEqlna9sbn68276LHYt`) and `src/web/projects/ops/src/styles/tokens.css` from "SCS Ops Console — Admin Design System" (fileKey `SetO9EhDivNcmurAMfcflY`): colour, type, spacing and radius variables only
- [ ] T099 [US5] Implement `src/web/projects/ui/src/lib/state/scs-state.component.ts` with input `state` (`loading|empty|error|offline|view-only|not-in-plan`) and the required content per contracts/frontend-runtime.md §5 (`error` shows message, reference and retry; `not-in-plan` shows «غير مشمولة في باقتك» and an upgrade slot), styled only through the token variables; unit test alongside
- [ ] T100 [US5] Add `src/web/projects/core/src/lib/network/online.service.ts` and have the `core` HTTP layer surface `offline` and retry when the connection returns
- [ ] T101 [US5] Extend the three placeholder pages (`src/web/projects/{clinic,patient,ops}/src/app/placeholder/placeholder.page.ts`): language switch, one date and one amount through the pipes, `<scs-state>` replacing the plain error block, and controls to force each of the six states that render only when `environment` is not `production`
- [ ] T102 [US5] Localise Problem titles on the server: `src/server/shared/Scs.BuildingBlocks/Errors/ErrorMessages.ar.resx` and `ErrorMessages.en.resx` with one entry per code of T012; resolve from `Accept-Language` (default `ar`) and set `Content-Language`; makes T091 pass
- [ ] T103 [US5] Run `npm --prefix src/web run e2e`; fix until T089 passes for all six combinations; record in `specs/001-platform-foundation/validation.md`

**Checkpoint**: A later spec makes no decision about language, direction, formatting, states or error wording.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T104 [P] Review every response for security headers (HSTS outside `local`, no server banner, content type options) in `src/server/host/Scs.Api/Program.cs` and add a test in `src/server/tests/Scs.IntegrationTests/Security/HeadersTests.cs`
- [ ] T105 [P] Measure the foundation endpoints locally against the plan's goals (p95 under 300 ms; `/health/live` under 50 ms) and record the numbers in `specs/001-platform-foundation/validation.md`
- [ ] T106 [P] Write `docs/adding-a-module.md`: how a later spec copies the `Platform` module (two projects, four folders, `IModule`, own schema and migrations history, own contracts project), referencing the architecture tests
- [ ] T107 Resolve the six "Items to confirm" in `specs/001-platform-foundation/research.md` with the product owner and update research.md, plan.md and the Terraform variable files accordingly
- [ ] T108 Walk every success criterion SC-001 to SC-013 in `specs/001-platform-foundation/spec.md` against `specs/001-platform-foundation/validation.md`; list any not yet evidenced
- [ ] T109 Run all five scenarios of `specs/001-platform-foundation/quickstart.md` end to end one final time and mark the spec accepted in `specs/001-platform-foundation/validation.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: needs Setup. Blocks every story.
- **US1 (Phase 3)**: needs Foundational.
- **US2 (Phase 4)**: T047 to T054 need Foundational only. T055 to T063 and T065 need US1 (there must be something to deploy) and the cloud account.
- **US3 (Phase 5)**: T066 to T075 need Foundational, plus T030 for the endpoint. T076 to T079 need the test environment from US2 (T059, T060).
- **US4 (Phase 6)**: T080, T081, T086 need US1 (T033). The rest need production from US2 (T063) and the topic from US3 (T077).
- **US5 (Phase 7)**: needs US1 (the placeholder pages, T041 to T043).
- **Polish (Phase 8)**: after the stories it checks.

### Story Independence

This is an infrastructure spec, so the stories are less independent than in a business spec.
Local work for US3 and US5 depends only on US1. Anything tagged (cloud) depends on US2's
environments. US1 alone is a complete, demonstrable increment.

```text
Setup → Foundational → US1 ─┬→ US2 (local) → US2 (cloud) ─┬→ US3 (cloud)
                            ├→ US3 (local)                └→ US4 (cloud)
                            ├→ US4 (local)
                            └→ US5
```

### Within Each User Story

- Test tasks first; confirm they fail; then the implementation that makes them pass.
- Entity → context → migration → migrator → jobs → endpoints (US1).
- Terraform modules → environment → workflows → validation run (US2).

### Parallel Opportunities

- Setup: T003 to T009 after T002.
- Foundational: T012, T013, T023, T024 alongside the server sequence T014 to T022.
- US1: the five test tasks T025 to T029; the three placeholder pages T041 to T043; T034.
- US2: T050, T051 and the four Terraform modules T055 to T058.
- US3: the four test tasks T066 to T069; T076 and T077.
- US4: T080, T081, T086.
- US5: the four test tasks T089 to T092; T094.
- Across stories: once US1 is done, US2 (local), US3 (local) and US5 can be worked by three people at once.

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "ReadyEndpointTests in src/server/tests/Scs.IntegrationTests/Health/ReadyEndpointTests.cs"
Task: "VersionEndpointTests in src/server/tests/Scs.IntegrationTests/Meta/VersionEndpointTests.cs"
Task: "MigrationTests in src/server/tests/Scs.IntegrationTests/Persistence/MigrationTests.cs"
Task: "HeartbeatJobTests in src/server/tests/Scs.IntegrationTests/Jobs/HeartbeatJobTests.cs"
Task: "platform-status.service.spec.ts in src/web/projects/core/src/lib/platform/"

# Later, the three placeholder pages together:
Task: "placeholder.page.ts in src/web/projects/clinic/src/app/placeholder/"
Task: "placeholder.page.ts in src/web/projects/patient/src/app/placeholder/"
Task: "placeholder.page.ts in src/web/projects/ops/src/app/placeholder/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup (T001 to T009).
2. Phase 2 Foundational (T010 to T024).
3. Phase 3 US1 (T025 to T046).
4. **Stop and validate**: quickstart.md Scenario 1 on a clean machine.

No cloud account is needed up to here: 46 tasks.

### Incremental Delivery

1. MVP as above → the system runs locally.
2. US2 local tasks (T047 to T054) → pull-request gates protect every later merge. **Do this before starting spec 002.**
3. US5 → conventions in place for the first screens (spec 006).
4. US3 local tasks (T066 to T075) → clean logs before any personal data exists (spec 002 onwards).
5. When the cloud account, domain, region and edition are settled: US2 cloud, then US3 cloud, then US4. These must be complete before the first real clinic is created, not before spec 002 starts.

### Parallel Team Strategy

After US1: one developer on US2 (pipeline and infrastructure), one on US3 (logging and
monitoring), one on US5 (front-end conventions). US4 follows US2's production environment.

---

## Notes

- 109 tasks: Setup 9, Foundational 15, US1 22, US2 19, US3 14, US4 9, US5 15, Polish 6.
- 19 tasks are tagged (cloud); the other 90 can be done on a developer machine and in CI.
- Commit after each task or small group; one pull request per phase is a reasonable size.
- A task that says "makes Txxx pass" is not done until that test is green.
- No task here creates a clinic-owned table. The first one arrives in spec 002 together with its isolation policy.
