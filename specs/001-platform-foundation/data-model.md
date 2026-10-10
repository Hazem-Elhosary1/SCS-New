# Data Model: Platform Foundation

This spec stores almost nothing in the database. Most of its entities are operational records
that live in configuration, logs, the pipeline or the cloud provider. Each is listed with where
it lives, so nobody builds a table for something that is not one.

No entity here is clinic-owned. Nothing in this spec is subject to Principle II; spec 002
introduces the first clinic-owned table and the isolation policy with it.

## Stored in the database

### `platform.JobHeartbeat`

Proof that background work runs, read by readiness reporting and by the restore check.

| Field | Type | Rules |
|---|---|---|
| `Id` | smallint, PK | Always `1`; single row, enforced by a check constraint |
| `JobName` | nvarchar(100) | `platform-heartbeat` |
| `LastRunAtUtc` | datetimeoffset | Set by the job on every run |
| `LastRunVersion` | nvarchar(40) | Version of the release that ran it |

- Created and seeded by the first migration of the `Platform` module.
- Written only by the heartbeat job. Stale when `LastRunAtUtc` is older than three intervals.

### `platform.__EFMigrationsHistory` — Structure change

Maintained by EF Core; one row per applied migration. One history table per module schema.
Rule (research R16): migrations are additive within a release; removal happens one release later.

### Schema `hangfire`

Owned and created by Hangfire. Not modelled, not touched by application code.

## Not stored in the database

### Environment

| Attribute | Values / rule |
|---|---|
| `name` | `local`, `test`, `production` — exposed as `SCS_ENVIRONMENT` |
| data | Its own database; no login valid in two environments |
| configuration | Its own variable file (`deploy/infra/env/<name>.tfvars`) and its own secrets |

Lives in: Terraform state and variable files; `.env` locally.

### Configuration value

Read from environment variables, bound to typed options, validated at start-up (FR-017).

| Key | Required | Secret | Purpose |
|---|---|---|---|
| `SCS_ENVIRONMENT` | yes | no | Environment name |
| `SCS_ROLE` | no (default `all`) | no | `api`, `worker` or `all` |
| `SCS_DB__CONNECTION` | yes | yes | Application database login (no structure rights) |
| `SCS_DB__MIGRATION_CONNECTION` | migration task only | yes | Login that may alter structure |
| `SCS_CORS__ORIGINS` | `local` only | no | Allowed front-end origins for local development; must be empty in test and production, where calls are same-origin |
| `SCS_OTEL__ENDPOINT` | no | no | OTLP collector address |
| `SCS_APIDOCS__ENABLED` | no (default `false`) | no | Ignored when environment is `production` |
| `SCS_VERSION`, `SCS_COMMIT`, `SCS_BUILT_AT` | set at build | no | Reported by the version endpoint |

A key marked secret is never logged and never returned by any endpoint.

### Release

| Attribute | Rule |
|---|---|
| `commit` | Git SHA; the identity of the release |
| `version` | `<semver>+<short sha>` |
| `builtAt` | UTC instant of the single build |
| packages | One API image and three front-end packages, all labelled with `commit` |
| state | `built` → `in-test` → `approved` → `in-production` → `superseded`; `rolled-back` from `in-production` |

Lives in: container registry and package bucket (by SHA), pipeline run history.
Transitions: `approved` requires a named human reviewer; `rolled-back` redeploys the previous
`superseded` release unchanged.

### Health status

| Field | Values |
|---|---|
| `status` | `Healthy`, `Unhealthy` |
| `checks[]` | `name` (`database`, `jobs`), `status`, `durationMs` |

Computed on request; shape in [contracts/platform-api.openapi.yaml](contracts/platform-api.openapi.yaml).
`live` never has checks. `ready` is `Unhealthy` when any check is.

### Request reference

The 32-hex-character W3C trace id of the request. Created at the edge of the first component
that sees the request, carried in `traceparent`, returned as `traceId`. Not stored on its own.

### Operational log entry

One JSON object per line on standard output. Field list and rules in
[contracts/conventions.md](contracts/conventions.md). Kept 30 days in the log store.

### Alert

| Attribute | Rule |
|---|---|
| `environment` | Always present in the message |
| `condition` | `unavailable`, `error-rate`, `slow`, `backup-missed`, `job-failures` |
| `state` | `ok` → `alarm` → `ok` |

Lives in: the monitoring service. Delivered to one notification topic per environment.

### Backup

| Attribute | Rule |
|---|---|
| kind | `automated` (daily, point-in-time) or `native` (weekly, portable) |
| retention | 30 days (automated), 90 days (native) |
| protection | Encrypted; readable only by the operations role; native copy in a separate account |

Lives in: the database service and the backup bucket.

### Restore test record

One Markdown file per trial at `ops/restore-tests/YYYY-MM-DD.md`.

| Field | Rule |
|---|---|
| `date` | Day the trial ran |
| `operator` | Who ran it |
| `backupTakenAt` | Which backup was restored |
| `durationMinutes` | From start of restore to passing verification |
| `result` | `pass` or `fail` |
| `notes` | Required when `fail`; a failure opens a production incident |

Validation: a quarter with no record opens a reminder issue automatically.

## Relationships

```text
Environment 1 ── * Release (deployed)        Release 1 ── * Structure change (carried)
Environment 1 ── * Alert                     Environment(production) 1 ── * Backup
Backup 1 ── * Restore test record            Request reference 1 ── * Operational log entry
```
