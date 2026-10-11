# Implementation Plan: Notifications Engine

**Branch**: `008-notifications-engine` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-notifications-engine/spec.md`

## Summary

One mechanism by which the platform tells people what concerns them: a table of events kept
with the product, one way to raise an event that stands or falls with the change that caused
it, a notification in the bell of each person the event names, an email where the event calls
for one, and the clinic's switches for the four emails it may turn off.

The approach: a new server module, `Scs.Notifications`, owning schema `notify` (notifications,
emails owed, switches). The table of events is in the shared kernel, frozen by a lock file. A
module raises on its own context — `db.Notify(…)` — and the save writes one notification per
recipient in the same transaction, as spec 007's `db.Record(…)` writes audit entries; Identity
answers who the recipients are. Emails are sent afterwards by a job that reads the address at
the moment of sending. The console's bell, built by spec 006 against an empty source, is given
the real one: it asks a small address every thirty seconds. The first event raised is
«الدخول من جهاز جديد», recognised by a mark of its own in each browser.

No new screen.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript on Angular 22 (as pinned by spec 001).

**Primary Dependencies**: none new. EF Core, Hangfire through the platform's schedulers, the
shared `IEmailSender`, Transloco and the `ui` library already in place.

**Storage**: SQL Server 2022. New schema `notify` with three clinic-owned tables —
`Notification`, `OutgoingEmail`, `EmailSwitch`; one new clinic-owned table in `identity`,
`KnownDevice`. All with the isolation policy of spec 002 in the migration that creates them.

**Testing**: xUnit on the SQL Server Testcontainers fixture; isolation and permission tests
written first. Vitest for the console's source. Playwright for the bell with real answers, and
pictures of the panel beside its frames. The made-up mail sender of the test host for email.

**Target Platform**: the platform API and its worker; the clinic console.

**Project Type**: web application — a server module, its contracts, changes in Identity and the
shared projects, and the console's notifications source.

**Performance Goals**: the panel and "mark all" under one second at a thousand notifications
(SC-007); raising an event told to two hundred people adds under a tenth of a second (SC-008);
a new notification on an open console within one minute (SC-001).

**Constraints**: an event and its change are one transaction (FR-006); no email is sent inside
it (FR-007); recipients by permission and ownership, never by role name (FR-010); no address
stored outside Identity and none in operational logs (SH-094); nothing clinical, no password and
no code in a notification or an email (FR-017, FR-018, FR-034); read state is each person's own
(FR-025).

**Scale/Scope**: 51 functional requirements (five added by clarification); 20 events in the
table, 1 raised; 4 tables; 6 addresses; 3 jobs; 1 audit action; the console's source and the
panel's scrolling.

**Open items carried from research**: none that block. For the owner: the marks of ten kinds of
event and every email are undrawn (R8, R11); the BRD's ق-٥٧ and CL-SB-149 say "a device or a
network" and the clarified answer is a browser (R10); the clinic's own language does not exist
yet, so an email falls back to Arabic (R8).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, open items | Requirements cite SH-072, Chapter 13 §9 and its rules, Chapter 01 §أ-٥-٤. The bell's frames (`5999:831798`, `5999:834947`, `5999:832473`, `5999:835622`, component `4262:1056`) were read; the panel with real notifications is compared with them in both languages (SC-012). Undrawn items and the one departure from the BRD (ق-٥٧) are listed for the owner |
| II. Tenant isolation in the database (non-negotiable) | Pass | Four new tables, all clinic-owned, each with its policy in the migration that creates it and its sample in the isolation suite. The one reading across clinics — which clinics have email waiting — is a declared crossing that returns ids only |
| III. The server decides; deny by default | Pass | The bell's addresses are own-account routes, added to the written list of spec 005; the switches need `settings.view` to read and `settings.notifications` to change; recipients are decided on the server by permission and ownership |
| IV. Financial records are immutable; changes are audited | Pass | No money moves. Changing a switch is a change to a clinic's setting and records an audit action with before and after, in the same save |
| V. Secrets and patient data | Pass | Facts are a white list per event with the never-list of spec 007; a clinical event carries no fact; no address is stored in `notify`; the device mark is kept as a hash; emails name a record by its identifier only |
| VI. Bilingual, complete screens | Pass | Titles, lines and emails in Arabic and English, checked by a test; the panel's empty, loading and error states exist from spec 006 and are exercised with real answers |
| VII. Tested before merge, verified after implementation | Pass | Full suites; the real-stack walk of quickstart "By hand" with Mailpit; the panel laid beside its frames in both languages, recorded in `validation.md` |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged. Two things stated plainly. The writer that
raises an event is in the shared project, not in the module's contracts, for the reason found in
spec 007: a contracts project may not use the database library. And Identity raises an event
into `notify` through that writer on its own connection — one module's save writing another
module's table, by the one sanctioned way, as audit entries are written.

## Project Structure

### Documentation (this feature)

```text
specs/008-notifications-engine/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── notifications-engine.md          # how a later spec adds and raises an event
│   └── notifications-api.openapi.yaml   # the bell's addresses and the switches
├── checklists/requirements.md
└── tasks.md                             # /speckit-tasks
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.SharedKernel/
│   ├── Notifications/
│   │   ├── NotificationEvents.cs         # the table: key, group, names, mark, recipients, email, line
│   │   └── notification-events.lock      # keys, frozen
│   ├── Wording/                          # number forms, digits, device text — moved here from Scs.Audit
│   └── Audit/AuditActions.cs             # + settings.notification-email-changed
├── shared/Scs.BuildingBlocks/
│   ├── Notifications/
│   │   ├── RaisedEvent.cs                # what a caller passes
│   │   └── NotificationWriter.cs         # checks, asks who is told, inserts in the caller's transaction
│   ├── Tenancy/ClinicAwareDbContext.cs   # + Notify(…), flushed by SaveChangesAsync
│   ├── Tenancy/Crossing.cs               # + list-clinics-with-email-waiting
│   └── Security/SessionCookies.cs        # + the device mark cookie
├── modules/Notifications/
│   ├── Scs.Notifications.Contracts/      # what the bell and the switches answer
│   └── Scs.Notifications/
│       ├── NotificationsModule.cs
│       ├── Domain/                       # Notification, OutgoingEmail, EmailSwitch
│       ├── Infrastructure/
│       │   ├── Persistence/NotificationsDbContext.cs
│       │   ├── Migrations/…_InitialNotifications.cs
│       │   ├── Bell.cs                   # list, state, mark read, mark all
│       │   ├── EmailSwitches.cs          # read, change, audit
│       │   ├── NotificationWords.cs      # title and line in the reader's language
│       │   ├── NotificationEmails.cs     # the one layout, one language
│       │   ├── NotificationEmailJob.cs   # sends what is owed; drops, retries, gives up
│       │   ├── EmailSweepJob.cs          # every minute: clinics with email waiting
│       │   └── CleanupJob.cs             # ninety days
│       └── Api/NotificationEndpoints.cs
├── modules/Identity/
│   ├── Scs.Identity.Contracts/StaffContracts.cs     # IStaffDirectory + ContactsAsync
│   └── Scs.Identity/
│       ├── Domain/KnownDevice.cs
│       ├── Infrastructure/KnownDevices.cs           # recognise, add, raise the event
│       ├── Infrastructure/NotificationRecipients.cs # INotificationRecipients
│       ├── Infrastructure/SignInService.cs          # a completed sign-in asks KnownDevices
│       ├── Infrastructure/CleanupJob.cs             # + devices not seen for thirteen months
│       ├── Infrastructure/Migrations/…_KnownDevices.cs
│       └── Api/AuthEndpoints.cs                     # reads and writes the device mark
├── modules/Audit/Scs.Audit/Infrastructure/          # Sentences, DeviceText use Scs.SharedKernel/Wording
├── host/Scs.Api/ModuleRegistration.cs, tools/Scs.Migrator/Migrator.cs
└── tests/
    ├── Scs.UnitTests/Notifications/      # the table and its lock, words, emails
    ├── Scs.ArchitectureTests/            # the module rules; who may use the writer's internals
    └── Scs.IntegrationTests/Notifications/, Isolation/NotificationSamples.cs

src/web/
├── projects/core/src/lib/shell/
│   ├── sources.ts                        # NotificationsSource: an item's link, more to load
│   └── platform-notifications.ts         # the real source: asks, keeps, marks, opens
├── projects/clinic/src/app/shell/bell-panel.component.ts   # loads more when scrolled to its end
├── projects/clinic/public/i18n/{ar,en}.json                # nothing new expected; checked
└── e2e/
    ├── staff-platform.ts                 # answers the notifications addresses, empty by default
    └── notifications.spec.ts             # the bell with real answers, both languages, pictures
```

**Structure Decision**: one new module beside Audit, with the same split — the table in the
shared kernel, the writer in the shared project, the tables and their reading in the module.
Identity supplies who people are and raises the first event. The console changes in one place:
the source the bell already reads from.

**Terms**: the spec's "event" is a row of `NotificationEvents`; a "notification" is a row of
`notify.Notification`; "the line" is the second line of a bell item; "the mark" is its icon and
tint.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| The writer uses the caller's `DbContext` connection to insert into `notify` | An event and the change that caused it must commit together (FR-006) | An outbox fanned out by a job makes the bell depend on the worker and resolves recipients later than the event |
| A second crossing of the clinic boundary (`list-clinics-with-email-waiting`) | A retry must not wait for the clinic's next event, and enqueuing every clinic every minute does not scale | A per-clinic recurring job for every clinic every minute is thousands of empty jobs |
| Wording helpers move from `Scs.Audit` to the shared kernel | Two modules now turn stored facts into words in two languages | Copying them would let the two drift |
