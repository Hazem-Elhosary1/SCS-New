# Handling data across environments

## The rule

**Production patient data is never copied into the local or test environments** (spec 001,
FR-008). Not a full copy, not a sample, not "just one clinic to reproduce a bug".

This covers the database, backups, exports, attachments, log extracts and screenshots that show
patient details.

| Allowed | Not allowed |
|---|---|
| Made-up data created by seed scripts or by hand | Restoring a production backup anywhere but the restore-test stack |
| A production *structure* (migrations) in any environment | Exporting production rows to a developer machine |
| Reproducing a bug from its request reference and the logs | Attaching a production export to a ticket or a chat |
| The restore test, in its throwaway stack, destroyed afterwards | Keeping the restore-test instance once verification has passed or failed |

## Why

Patient records are health data. Local and test environments have weaker access control, more
people, and no audit trail. The constitution (Principle V) keeps patient data contained, and no
environment can read another's data (FR-007).

## Reproducing a production problem

1. Take the reference shown on the user's error (`traceId`).
2. Find that request's entries in the production logs. They hold the route, the status, the
   timings and the failure — and no personal data (FR-021).
3. Recreate the situation in test with made-up data.

If the logs are not enough, improve the logging in a change that goes through review; do not
reach for the data.

## The one place a production backup is restored

The quarterly restore test (`ops/runbooks/restore.md`) restores the latest backup into an
isolated, throwaway stack that only the operations role can reach. It is verified with
`ops/restore/verify.sql`, recorded in `ops/restore-tests/`, and destroyed the same day.

## Made-up data

Spec 001 has nothing to seed beyond what its migration creates. From spec 002 on, each spec
that adds data adds its made-up records to a seed that `scripts/dev.ps1` and `scripts/dev.sh`
run, so every developer and the test environment start from the same invented clinics and
patients. Names, mobile numbers and national IDs in seeds are invented and must not belong to
real people.

## If it happens anyway

Treat a copy of production patient data outside production as an incident: delete the copy,
record where it was and who could reach it, and report it to the data-protection officer.
