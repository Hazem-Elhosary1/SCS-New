# Research: Notifications Engine

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Decisions taken while planning. Each says what was chosen, why, and what was set aside.

## R1 — Where the engine lives

**Decision**: a new server module, `Scs.Notifications`, with `Scs.Notifications.Contracts`,
owning schema `notify`. The table of events lives in `Scs.SharedKernel/Notifications`, like the
permission catalogue and the audit catalogue, so every module can name an event without
referencing a module. The one way to raise an event lives in `Scs.BuildingBlocks/Notifications`,
for the reason the audit writer does (R2).

**Rationale**: the module rules of the constitution; the same shape as spec 007, so a developer
meets one habit.

## R2 — Raising an event is part of the caller's transaction

**Decision**: a module raises on its own context — `db.Notify(new RaisedEvent(…))` — and the
next `SaveChangesAsync` writes the notifications with the change, in one transaction, exactly as
`db.Record(…)` writes audit entries. The writer is in `Scs.BuildingBlocks/Notifications`.

Inside that save the writer: checks the event is in the table and has its line; asks who is told
(R3); checks the clinic's email switch when the event's email kind is "switch"; and inserts, in
one statement that reads its recipients from a JSON parameter, one `notify.Notification` row per
recipient and, where the event is emailed, one `notify.OutgoingEmail` row per recipient.

**Rationale**: FR-006 — no notification of a change that did not take effect — needs the same
transaction. One statement for all recipients keeps an event told to two hundred people within
the tenth of a second of SC-008; no email is sent inside the transaction (FR-007).

**Alternatives**: an outbox row fanned out by a job (rejected: the bell would depend on the job
server being up, and "sees it at once" of SC-001 would become "sees it when the worker gets to
it"; recipients would be resolved seconds later than the event); an event bus (rejected: nothing
else needs one).

## R3 — Who is told

**Decision**: the writer asks `INotificationRecipients` (in `Scs.BuildingBlocks`, implemented by
Identity, which owns accounts and roles): given a rule — the holders of a permission, everyone,
named accounts, or named accounts together with the holders of a permission — it returns the
distinct accounts of the established clinic that can sign in (state `active`). The owner role
holds every permission, so its holders are always among "the holders of a permission".

Recipients are those at the moment of the save. A notification row is the delivery: it is not
re-evaluated when roles change later (FR-013).

**Rationale**: FR-010 to FR-013; recipients by permission and ownership, never by role name
(ق-٣٤). Identity is asked through an interface, so no module reads its tables.

## R4 — Telling once

**Decision**: a raised event carries an optional occasion — a short text such as `14-days` for a
reminder, empty otherwise. `notify.Notification` has a unique index on (clinic, recipient,
event, subject, occasion) for rows that have an occasion, and the insert skips what is already
there. An event with no occasion is a happening of its own every time it is raised — two
replies on one ticket are two notifications (FR-008a); callers that can repeat (a reminder job
that runs twice) always pass an occasion.

**Rationale**: FR-008; a job must be idempotent (constitution, conventions).

## R5 — A notification is facts, read as words

**Decision**: a row stores the event's key, the subject (kind, id, the identifier a person
reads), a flat JSON object of facts checked against the keys the event allows, and where it
leads (a path inside the console, built from the event's destination template when the row is
written). The title and the line are produced when it is read, in the reader's language, from
the table and the stored facts — the mechanism of spec 007's sentences, moved to
`Scs.SharedKernel` where both modules use it (number forms, Arabic-Indic digits, the device
text).

Facts never include a key of the never-list of spec 007 (`password`, `code`, `token`, `link`,
`secret`, `key`, `email`, `proof`, `cookie`), and an event marked clinical allows no fact at all
beyond the record's identifier (FR-017, FR-018).

**Rationale**: FR-019, FR-019a (the facts are those of the moment), Constitution VI.

## R6 — The bell asks, it is not pushed to

**Decision**: the console asks a small address, `GET /v1/notifications/state`, every thirty
seconds while its tab is visible, when the tab becomes visible, and when the bell is opened. It
answers whether anything is unread and the time of the newest notification; the panel's list is
fetched when the bell is opened or when that time changes while it is open. Marking as read on
one browser therefore shows on another within the same thirty seconds.

**Rationale**: FR-026 asks for "within a minute". Asking is the simplest thing that meets it and
has no new infrastructure; one indexed read per person per thirty seconds is cheap.

**Alternatives**: server-sent events or web sockets (rejected for now: a long-lived connection
per open tab through the load balancer, for a figure nobody asked to be faster than a minute).

**Noted for a later spec**: the clinic's inactivity time-out (settings S11) must not count this
address as activity, or no session would ever time out.

## R7 — Email leaves after the change, from a job

**Decision**: `notify.OutgoingEmail` holds what is owed: the notification, the recipient, the
language is not stored. A per-clinic job, `NotificationEmailJob`, sends what is waiting: for each
row it asks Identity for the recipient's address, language and whether the account can still
sign in (`IStaffDirectory.ContactsAsync`); drops the email when it cannot (FR-035a); renders
subject and body in the recipient's language; sends through the shared `IEmailSender`; and marks
the row sent, or leaves it for the next try with a growing wait, giving up twenty-four hours
after the event (FR-035).

The job is enqueued for the clinic when a save wrote an email, through `INotificationDispatch`
(in `Scs.BuildingBlocks`, implemented by the module), and a platform job every minute enqueues
it for every clinic that still has email waiting — found through a declared crossing that reads
clinic ids only.

No address is stored in `notify`: it is read from the account at the moment of sending, so a
changed address is honoured and a removed account gets nothing.

**Rationale**: FR-007, FR-031 to FR-035a, SH-094. The same shape as spec 004's account emails.

## R8 — The words of an email

**Decision**: one layout for every notification email, in one language: the subject is the
event's title; the body is the title, the line, and a button that leads to the same place the
bell does (or to the console's home when the notification leads nowhere); a last line says the
email cannot be answered. The recipient's language is the one they chose in the console (spec
006), else Arabic until a clinic has a language of its own (أ-ق٢٥, a later spec).

No email frame exists in the design file; this is recorded as undrawn. The emails of specs 003
and 004 are left exactly as they are (FR-036).

## R9 — The clinic's switches

**Decision**: `notify.EmailSwitch` holds a row only for a switch that was changed; an event with
no row is on (FR-037). `GET /v1/settings/notification-emails` (permission `settings.view`)
answers the four switchable events with their names and state; `PUT
/v1/settings/notification-emails/{event}` (permission `settings.notifications`) sets one. A
change records a new audit action, `settings.notification-email-changed`, category `changes`,
with before and after. An event that is not switchable is refused as a validation failure.

The switch is read when the event is raised: an email already owed is not withdrawn by turning
the switch off a second later.

**Rationale**: FR-037 to FR-041. The page is spec 084's; the address is here so that page has
something to ask and the rule is testable now.

## R10 — A new device

**Decision**: a browser is recognised by a mark of its own: a random value in a cookie
(`__Host-scs-device`, HTTP-only, secure, thirteen months), written when a sign-in completes.
Identity keeps `identity.KnownDevice`: the account and a hash of the mark. When a sign-in
completes:

- the browser shows a mark the account already has → nothing; its last-seen time moves;
- otherwise the mark is added (a new one is issued when the browser showed none), and if the
  account had at least one known device before, «الدخول من جهاز جديد» is raised for its owner
  in the same transaction as the session.

So an account's first sign-in ever is silent, and so is the first sign-in of every existing
account after this spec ships: nothing is "new" against nothing (FR-043). The network plays no
part (clarified). A cleared cookie makes a browser new again; that is the honest answer.

A known device not seen for thirteen months is removed by the daily clean-up.

**Rationale**: FR-042 to FR-044. The cookie is written in `SessionCookies`, the one place
cookies are protected (architecture rule of spec 003).

**Alternatives**: recognising a device from its user agent and address (rejected: a browser
update would be a new device, and two people behind one address would not be).

## R11 — The table of events

**Decision**: `NotificationEvents` in `Scs.SharedKernel/Notifications`: key, group, name in two
languages, mark, recipient rule (with its permission where it has one), email kind, days before,
the fact keys its line may use, the line's template in two languages, the destination template,
and whether it is clinical. Keys are frozen by `notification-events.lock`, as audit actions are.

The twenty events of Chapter 13 §9-1 are in the table now with everything the BRD fixes. A line
and a destination are given only to the one event raised here; an event without a line cannot be
raised, and the spec that builds its cause adds the line (and a test fails if it raises without
one). Permissions are named by the constants of the permission catalogue; a unit test fails if
one is not in it.

Marks for the kinds the design does not draw (spec, Undrawn): `warning` for what stopped, ran
out or failed; `notice` for the rest; recorded as an owner item.

## R12 — Reading, paging, and the ninety days

**Decision**: `GET /v1/notifications` answers the person's notifications of the last ninety
days, newest first, twenty at a time, with a cursor (the time and id of the last one) for the
next page as the panel is scrolled. Index: (clinic, recipient, raised-at descending, id) with
the read mark included; a second, filtered on unread, for the dot. `POST
/v1/notifications/{id}/read` and `POST /v1/notifications/read-all` are the two marks. All four
are open to any signed-in person about their own notifications, are added to the written list of
own-account routes of spec 005, and declare that they record nothing in the audit trail.

A daily per-clinic job removes notifications and email rows older than ninety days (clarified).

**Rationale**: FR-019b to FR-028, SC-007.

## R13 — The console's side

**Decision**: the default `NotificationsSource` of `core` becomes the real one: it asks the
addresses of R6 and R12, keeps the list, and on `open(item)` marks it read and goes to its link
through the router. It gains two members the panel needs — whether more can be loaded, and
loading more — and an item gains its link. The panel loads more when it is scrolled to its end.
The sample source of the builders' pages keeps working.

The browser tests' made-up platform (`e2e/staff-platform.ts`) answers the new addresses, empty
by default, so every existing test sees the bell it saw before.

## R14 — Tests

Written first, because they are isolation and permission (constitution): a notification of one
clinic or person is unreachable from another by every address; the three new `notify` tables and
`identity.KnownDevice` have their policies and samples; the switches refuse without their
permissions. Then: an event and its change together, with a forced failure on each side; every
recipient rule across two clinics; telling once; the bell's four addresses; email by kind and
switch with the made-up mail sender, retried, given up, dropped for a disabled account; the new
device; the table's lock and two languages; a thousand notifications read in under a second and
two hundred recipients in under a tenth.

Browser tests: the bell filled from the platform's addresses, marking, opening, loading more,
the dot arriving without a reload, in both languages; and pictures of the panel for the
comparison with its frames (SC-012). Principle VII's real-stack check: sign in from a fresh
browser twice and find the notification, the email in Mailpit, and nothing the second time.

## R15 — What is not built

The page «التنبيهات» (084); the support team's notices, their dialog and banner (083); the
weekly summary (060); any event but the new device; operators' notifications; live push.
