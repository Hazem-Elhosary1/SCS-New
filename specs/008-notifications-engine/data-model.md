# Data Model: Notifications Engine

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## 1. `notify.Notification` — clinic-owned

One event told to one person. Isolation policy of spec 002 in the same migration.

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier (v7) | no | key |
| `ClinicId` | uniqueidentifier | no | from the clinic context |
| `RecipientAccountId` | uniqueidentifier | no | an account of the clinic; no foreign key across modules |
| `EventKey` | varchar(64) | no | a key of the table of events (FR-004) |
| `Mark` | varchar(16) | no | `ticket`, `invoice`, `subscription`, `feedback`, `notice`, `warning`, `incident`; copied from the event; check constraint |
| `SubjectKind` | varchar(32) | yes | `account`, later `ticket`, `invoice`, … |
| `SubjectId` | uniqueidentifier | yes | |
| `SubjectLabel` | nvarchar(64) | yes | the identifier a person reads (`T-1046`); null when the subject is an account |
| `Occasion` | varchar(32) | no | empty, or what makes a repeat distinct (`14-days`) |
| `Facts` | nvarchar(max) | yes | JSON object; keys within the event's allowed keys; `ISJSON` check |
| `Link` | nvarchar(256) | yes | a path inside the console; null when it leads nowhere |
| `RaisedAtUtc` | datetimeoffset | no | the platform's clock |
| `ReadAtUtc` | datetimeoffset | yes | null while unread |

Indexes: `(ClinicId, RecipientAccountId, RaisedAtUtc DESC, Id DESC)` including `ReadAtUtc`;
`(ClinicId, RecipientAccountId)` filtered on `ReadAtUtc IS NULL`; unique `(ClinicId,
RecipientAccountId, EventKey, SubjectId, Occasion)` filtered on `Occasion <> ''`.

The only change a row ever takes is `ReadAtUtc` going from null to a time, by its recipient.
Removed after ninety days.

## 2. `notify.OutgoingEmail` — clinic-owned

One email owed for one notification.

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier (v7) | no | key |
| `ClinicId` | uniqueidentifier | no | |
| `NotificationId` | uniqueidentifier | no | foreign key, cascade on delete |
| `State` | varchar(8) | no | `waiting`, `sent`, `gave-up`, `dropped`; check constraint |
| `Attempts` | tinyint | no | default 0 |
| `NextAttemptAtUtc` | datetimeoffset | no | when it may be tried |
| `CreatedAtUtc` | datetimeoffset | no | |
| `SettledAtUtc` | datetimeoffset | yes | when it left `waiting` |

Index: `(ClinicId, NextAttemptAtUtc)` filtered on `State = 'waiting'`. No address and no text is
stored: both are produced at the moment of sending.

State: `waiting` → `sent` | `gave-up` (twenty-four hours after creation) | `dropped` (the
account can no longer sign in).

## 3. `notify.EmailSwitch` — clinic-owned

A clinic's choice for one switchable event. A row exists only once the switch was changed.

| Column | Type | Null | Rule |
|---|---|---|---|
| `ClinicId` | uniqueidentifier | no | key, with `EventKey` |
| `EventKey` | varchar(64) | no | an event whose email kind is `switch` |
| `Enabled` | bit | no | |
| `ChangedAtUtc` | datetimeoffset | no | |
| `ChangedByAccountId` | uniqueidentifier | yes | |

## 4. `identity.KnownDevice` — clinic-owned

A browser an account has completed a sign-in from.

| Column | Type | Null | Rule |
|---|---|---|---|
| `Id` | uniqueidentifier (v7) | no | key |
| `ClinicId` | uniqueidentifier | no | |
| `AccountId` | uniqueidentifier | no | foreign key to the account |
| `MarkHash` | binary(32) | no | a hash of the browser's mark; secret |
| `FirstSeenAtUtc` | datetimeoffset | no | |
| `LastSeenAtUtc` | datetimeoffset | no | |

Unique `(AccountId, MarkHash)`. Removed when not seen for thirteen months.

## 5. The table of events — code, `Scs.SharedKernel/Notifications`

| Field | Meaning |
|---|---|
| `Key` | frozen once used (`notification-events.lock`) |
| `Group` | `support`, `billing`, `channels`, `services`, `patients`, `accounts` |
| `NameAr`, `NameEn` | the event's name: the notification's title and the email's subject |
| `Mark` | how the bell draws it |
| `Recipients` | `Permission(code)`, `Owner`, `OwnerAnd(code)`, `Everyone` |
| `Email` | `always`, `switch`, `never` |
| `DaysBefore` | for an event that repeats before a date |
| `Facts` | the keys its line may use |
| `LineAr`, `LineEn` | the line's template; absent until the event has a cause |
| `Destination` | the path it leads to, with `{id}`; absent when it leads nowhere |
| `Clinical` | true: no fact beyond the record's identifier, and never emailed |

The twenty events of this spec (BRD numbering; recipients and email as Chapter 13 §9-1):

| # | Key | Group | Recipients | Email | Mark |
|---|---|---|---|---|---|
| ن-٠١ | `ticket.support-replied` | support | owner and `tickets.viewAll` | always | ticket |
| ن-٠٢ | `ticket.awaiting-reply` | support | owner | always | ticket |
| ن-٠٣ | `ticket.resolved` | support | owner | always | ticket |
| ن-٠٤ | `invoice.issued` | billing | `subscription.invoices.view` | switch | invoice |
| ن-٠٥ | `payment.received` | billing | `subscription.invoices.view` | switch | invoice |
| ن-٠٦ | `payment.failed` | billing | `subscription.invoices.view` | always | warning |
| ن-٠٧ | `subscription.ending` | billing | `subscription.invoices.view` | switch | subscription |
| ن-٠٨ | `subscription.state-changed` | billing | everyone | always | subscription |
| ن-٠٩ | `channel.disconnected` | channels | `settings.channels` | always | warning |
| ن-١٠ | `messages.balance-exhausted` | channels | `settings.channels` | always | warning |
| ن-١١ | `booking-page.stopped` | services | `settings.bookingPage` | always | warning |
| ن-١٢ | `teleconsult.minutes-exhausted` | services | `settings.teleconsult` | always | warning |
| ن-١٣ | `xray.analyses-exhausted` | services | `settings.ai` | always | warning |
| ن-١٤ | `call.refunded` | services | `money.refund` | always | notice |
| ن-١٥ | `offer.expired` | patients | `bookings.manage` | never | notice |
| ن-١٦ | `recall.due` | patients | `patients.recall.log` | never | notice |
| ن-١٧ | `result.out-of-range` | patients | owner | never (clinical) | warning |
| ن-١٨ | `account.new-device` | accounts | owner | always | warning |
| ن-١٩ | `account.support-action` | accounts | `admin.users.manage` | never | notice |
| ن-٢٠ | `licence.expiring` | accounts | owner and `admin.licences.manage` | switch | warning |

Permission codes are written here as the catalogue of spec 005 spells them; the constants are
used in code and a test fails for one that is not in the catalogue. Days before: ن-٠٧ — 14, 7,
1; ن-٢٠ — 60, 30, 7.

Raised in this spec: `account.new-device` alone.

| | Arabic | English |
|---|---|---|
| Title | الدخول من جهاز جديد | Sign-in from a new device |
| Line | `{device}` — «متصفح Chrome على Windows» | "Chrome on Windows" |
| Facts | `device` (the user agent, described when read) | |
| Leads to | nowhere: no account page exists yet | |

## 6. What a caller passes — `RaisedEvent`

| Field | Rule |
|---|---|
| `Event` | a key of the table |
| `Subject` | kind, id, and the identifier a person reads |
| `Facts` | checked against the event's keys |
| `Owners` | the account or accounts, when the rule names an owner |
| `Occasion` | what makes a repeat distinct; empty otherwise |

Recipients, marks, channels, the title and the time are the engine's.

## 7. Audit

One new action in the catalogue of spec 007, with its line in `audit-actions.lock`:

| Code | Category | Target | Before and after |
|---|---|---|---|
| `settings.notification-email-changed` | changes | the event, by its name | `enabled` before and after |

Name: «تغيير بريد تنبيه» · "Notification email changed". Sentence: «غُيِّر بريد «{target}» من
«يعمل» إلى «متوقف»» · "Email for “{target}” was changed from “on” to “off”" — one sentence, with
the value before and after, as every action that needs them reads.

## 8. Declarations

| Route or job | Access | Audit |
|---|---|---|
| `GET /v1/notifications`, `GET /v1/notifications/state` | own account | — (reads) |
| `POST /v1/notifications/{id}/read`, `POST /v1/notifications/read-all` | own account | records nothing: a person's own read marks |
| `GET /v1/settings/notification-emails` | `settings.view` | — |
| `PUT /v1/settings/notification-emails/{event}` | `settings.notifications` | `settings.notification-email-changed` |
| job `NotificationEmailJob`, `notify-email-sweep` | — | records nothing: sends what is owed |
| job `notify-cleanup` | — | records nothing: removes notifications past ninety days |

## 9. Migrations

1. `notify` — `InitialNotifications`: the three tables, their checks, indexes and policies.
2. `identity` — `KnownDevices`: the table and its policy.

The Notifications module is migrated after Tenancy and before Identity, because Identity raises
an event into its tables.
