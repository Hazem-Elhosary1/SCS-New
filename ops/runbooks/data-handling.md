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

The seed is the `seed` command of the migrator
(`dotnet run --project src/server/tools/Scs.Migrator -- seed`); each module adds its records in
its `SeedAsync`. Spec 002 starts it with two clinics, `noor` («عيادة النور») and `shifa`
(«عيادة الشفاء»). The command registers only what is missing, so running it again changes
nothing, and it is never run against production.

Spec 003 adds six made-up staff accounts, three for each of those clinics (`owner@noor.example`,
`reception@noor.example`, `former@noor.example`, `owner@shifa.example`, `doctor@shifa.example`,
`new@shifa.example`); spec 004 adds `temp@noor.example` and spec 006 `reception@shifa.example`.
They share the password in `SCS_SEED__STAFF_PASSWORD`, a sample value
that exists only in `.env.example`. The `.example` domain cannot receive mail.

## The audit trail

`audit.Entry` (spec 007) holds who did what, to what, in which clinic, when and from where. It
is production data like any other and follows the rule above. Three things to know about it:

- **It holds personal data**: the actor's name of the moment (the email address until accounts
  have names), the network address and the browser's user agent. None of the three is ever
  written to the operational logs. It never holds a password, a code, a link, a session proof,
  or anything from a clinical record.
- **Nothing changes or removes an entry.** The database refuses both, for the application's
  login and for a person at a query window alike. There is no retention period yet (the legal
  decision is open, SH-095), so nothing is pruned; do not write a clean-up for it.
- **Do not "fix" an entry.** An entry that is wrong stays, and what corrects it is a new action
  that is itself recorded. If an entry must be removed for a legal reason, that is a decision
  for the data-protection officer and is done by a reviewed migration, not by hand.

## Notifications

The `notify` schema (spec 008) holds what each person was told and the email owed for it. It is
production data like any other and follows the rule above.

- **`notify.Notification`** — one row per person told: the event, the record it is about (its
  kind, id and label, such as a ticket number), a few short facts of the moment, and when it was
  read. It never holds anything from a clinical record, a password, a code or a link. The words
  a person reads are built when they read, in their language.
- **`notify.OutgoingEmail`** — whether the email for a notification is waiting, sent, given up
  on after a day of trying, or dropped. **No address is stored there**: the address and the
  language are read from the account at the moment of sending, so an account that was disabled
  or removed since is not written to. The operational logs hold counts only.
- **`notify.EmailSwitch`** — the events a clinic turned the email off for.
- **Ninety days.** A job removes notifications older than ninety days every night, read or not,
  with their email rows. Nothing is lost by it: a notification points at something that is
  recorded where it happened. Do not restore removed notifications from a backup.
- `identity.KnownDevice` holds a one-way hash of a mark kept in each browser an account signed
  in from, to tell a new device from a known one. It holds no address and no user agent, and a
  browser not seen for four hundred days is forgotten.

## If it happens anyway

Treat a copy of production patient data outside production as an incident: delete the copy,
record where it was and who could reach it, and report it to the data-protection officer.
