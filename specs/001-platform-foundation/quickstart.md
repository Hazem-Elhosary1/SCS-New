# Quickstart: Validating the Platform Foundation

How to prove the foundation works. Each scenario maps to a user story in [spec.md](spec.md).
Paths and commands are the ones [plan.md](plan.md) defines; shapes are in [contracts/](contracts/).

## Prerequisites

- .NET SDK 10, the Node LTS pinned in `.nvmrc`, Docker Desktop, Git.
- Nothing else: no cloud account and no production credential is needed for scenarios 1, 2a, 3
  and 5.

## Scenario 1 — Run everything from a clean copy (Story 1, SC-001)

```bash
git clone <repository-url> scs
```

```bash
cd scs && ./scripts/dev.ps1
```

Expected, in under 30 minutes on a clean machine:

| Check | Expected |
|---|---|
| `http://localhost:5080/health/live` | `200`, `Healthy` |
| `http://localhost:5080/health/ready` | `200`, checks `database` and `jobs` healthy |
| `http://localhost:5080/v1/meta/version` | `environment: "local"` |
| `http://localhost:4201` (clinic), `:4202` (patient), `:4203` (ops) | Placeholder page, "platform reachable" |

Then stop the database:

```bash
docker compose -f deploy/compose/docker-compose.yml stop sqlserver
```

Expected: `live` stays `200`; `ready` becomes `503` naming `database`; each placeholder page
shows the error state with a reference, not a blank page.

## Scenario 2a — The gates block a bad change (Story 2, SC-002)

Run the same checks the pull request runs:

```bash
dotnet test src/server/Scs.sln
```

```bash
npm --prefix src/web run ci
```

Then open three throwaway pull requests, each breaking one rule, and confirm each is blocked
with the failing check named:

1. the `Platform` module's Domain folder references an Infrastructure type → architecture test;
2. a key added to `ar.json` only → translation check;
3. `ms-4` replaced by `ml-4` in a template → lint.

## Scenario 2b — Same package to test, then production (Story 2, SC-003 to SC-005)

Needs the cloud environments.

1. Merge a passing pull request to `develop`. Within 15 minutes
   `https://app.test.scs-heta.com/api/v1/meta/version` reports the merged commit, and the same
   path answers on `https://test.scs-heta.com` and `https://test.hetasystems-scsadmin.app`.
2. Fast-forward `main` to `develop`. The production workflow waits for approval and has no build
   step. After approval, production reports the **same** `commit` and `builtAt` as test.
3. During step 2, run a loop requesting `/v1/meta/version` once a second: zero failed requests.
4. Run the rollback workflow: within 15 minutes production reports the previous commit.

## Scenario 3 — Trace a failure, find no personal data (Story 3, SC-006, SC-007)

```bash
dotnet test src/server/tests/Scs.IntegrationTests --filter Category=LogRedaction
```

The test submits a name, an Egyptian mobile number, an email, a national ID and a bearer token
through every input of every endpoint (body, query, headers, path), on success and on failure,
then scans all captured log output. Expected: zero occurrences.

By hand: post a malformed body to `/v1/client-errors`; the answer is a Problem with
`code: "MALFORMED_REQUEST"` and a `traceId`; searching the logs for that `traceId` returns the
entries of that request and no others. In the cloud: scale the test API to zero and confirm an
alert naming `test` arrives within 5 minutes.

## Scenario 4 — Backup and tested restore (Story 4, SC-009, SC-010)

Needs the production (or a production-like) environment. Follow `ops/runbooks/restore.md`:

1. confirm a backup exists for each of the last 7 days;
2. restore the latest into the throwaway restore environment;
3. run `ops/restore/verify.sql` (migration history complete, heartbeat row present and recent
   relative to the backup time);
4. write `ops/restore-tests/<date>.md`; destroy the instance.

Expected: verification passes within 4 hours of starting. Separately, disable the backup window
for one day in test and confirm the `backup-missed` alert.

## Scenario 5 — Shared conventions (Story 5, SC-012)

```bash
npm --prefix src/web run e2e
```

For each of the three apps, in `ar` and in `en`, the suite checks: `dir` matches the language;
no untranslated text; the date shows Cairo time and the amount shows EGP; each of the six states
renders; a forced server error shows `title` and the reference.

## Done

The foundation is accepted when scenarios 1, 2a, 3 and 5 pass locally and in CI, and 2b and 4
have each been run once against the cloud environments with the result recorded.
