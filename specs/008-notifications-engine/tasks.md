---

description: "Task list for 008-notifications-engine"
---

# Tasks: Notifications Engine

**Input**: Design documents from `/specs/008-notifications-engine/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 007 merged into `develop` and Docker running.

**Tests**: Included, and first. Isolation and permissions are test-first areas (constitution, testing): each of those test tasks is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Design**: no new screen. The bell's panel of spec 006 is filled with real notifications and compared with frames `5999:831798`, `5999:834947`, `5999:832473`, `5999:835622` and component `4262:1056` of Figma file `4HLWEqlna9sbn68276LHYt`, in both languages.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US8)
- Paths are relative to the repository root

## Path Conventions

- Notifications module: `src/server/modules/Notifications/Scs.Notifications/` (`Domain/`, `Infrastructure/`, `Api/`) and `src/server/modules/Notifications/Scs.Notifications.Contracts/`
- Table of events: `src/server/shared/Scs.SharedKernel/Notifications/`; the writer: `src/server/shared/Scs.BuildingBlocks/Notifications/`
- Identity: `src/server/modules/Identity/Scs.Identity/` and `Scs.Identity.Contracts/`
- Server tests: `src/server/tests/Scs.IntegrationTests/Notifications/`, `src/server/tests/Scs.UnitTests/Notifications/`
- Web: `src/web/projects/core/src/lib/shell/`, `src/web/projects/clinic/src/app/shell/`, `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: A known-good start and the empty module.

- [X] T001 Confirm the branch is `008-notifications-engine` from `develop` with spec 007 merged, and run `dotnet test src/server/Scs.sln` (Debug) and `npm --prefix src/web run ci` before any change, recording the counts (temp folder on `E:` as `docs/setup.md`)
- [X] T002 Create the projects `src/server/modules/Notifications/Scs.Notifications/Scs.Notifications.csproj` and `src/server/modules/Notifications/Scs.Notifications.Contracts/Scs.Notifications.Contracts.csproj` like the Audit module's, add them to `src/server/Scs.sln`, reference `Scs.Notifications` from `src/server/host/Scs.Api/Scs.Api.csproj`, `src/server/tools/Scs.Migrator/Scs.Migrator.csproj` and `src/server/tests/Scs.UnitTests/Scs.UnitTests.csproj`, add `NotificationsModule` to `src/server/host/Scs.Api/ModuleRegistration.cs` and to the ordered list in `src/server/tools/Scs.Migrator/Migrator.cs` after `AuditModule` and before `IdentityModule` (Identity raises an event into its tables)
- [X] T003 Move the wording helpers two modules now share from `src/server/modules/Audit/Scs.Audit/Infrastructure/Sentences.cs` and `DeviceText.cs` into `src/server/shared/Scs.SharedKernel/Wording/` (`Wording.Number`, the language constants, `DeviceText.Describe`), leave the audit sentences themselves where they are calling the shared helpers, move their unit tests' cases for the moved members to `src/server/tests/Scs.UnitTests/Wording/`, and see `dotnet test` unchanged in count and green

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The table of events, the tables, and the one way to raise an event.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 [P] Write failing `src/server/tests/Scs.UnitTests/Notifications/NotificationEventsTests.cs`: the table holds exactly the twenty keys of `data-model.md` §5 with their groups, recipient rules, email kinds and marks; every key is in `notification-events.lock` and every locked key is in the table; keys are unique; every event has a non-empty `NameAr` and `NameEn`; every permission a rule names is in `PermissionCatalogue`; `subscription.ending` repeats on 14, 7 and 1 days and `licence.expiring` on 60, 30 and 7; exactly four events have email kind `switch` (`invoice.issued`, `payment.received`, `subscription.ending`, `licence.expiring`); `result.out-of-range` is clinical, email `never`, and allows no fact; no event allows a fact key of the audit never-list; an event that has a line has it in both languages
- [X] T005 Create `src/server/shared/Scs.SharedKernel/Notifications/NotificationEvents.cs` — groups `support`, `billing`, `channels`, `services`, `patients`, `accounts`; marks `ticket`, `invoice`, `subscription`, `feedback`, `notice`, `warning`, `incident`; recipient rules `Permission(code)`, `Owner`, `OwnerAnd(code)`, `Everyone`; email kinds `always`, `switch`, `never`; the twenty events of `data-model.md` §5 with constants for their keys; `Find(key)`; `INotificationCatalogue` replaceable in a test — and `notification-events.lock` embedded in the assembly; give `account.new-device` its line `{device}`, its fact key `device` and no destination; T004 passes
- [X] T006 [P] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/NotificationIsolationTests.cs` and `src/server/tests/Scs.IntegrationTests/Isolation/NotificationSamples.cs` (samples for `notify.Notification`, `notify.OutgoingEmail`, `notify.EmailSwitch`): the three tables have their policies; a notification written for one clinic is not returned for another by a query, by its id, or with no clinic established; an insert with another clinic's id is refused
- [X] T007 Create `src/server/modules/Notifications/Scs.Notifications/Domain/Notification.cs`, `OutgoingEmail.cs` and `EmailSwitch.cs` with the columns of `data-model.md` §1 to §3 — `EventKey` varchar(64), `Mark` varchar(16) "`ticket`, `invoice`, `subscription`, `feedback`, `notice`, `warning`, `incident`; check constraint", `SubjectKind` varchar(32) null, `SubjectLabel` nvarchar(64) null, `Occasion` varchar(32) not null "empty, or what makes a repeat distinct", `Facts` nvarchar(max) null with an `ISJSON` check, `Link` nvarchar(256) null, `ReadAtUtc` null "null while unread"; `OutgoingEmail.State` varchar(8) "`waiting`, `sent`, `gave-up`, `dropped`; check constraint", `Attempts` tinyint default 0, foreign key to the notification with cascade on delete; `EmailSwitch` keyed by `(ClinicId, EventKey)` — as `IClinicOwned`; `Infrastructure/Persistence/NotificationsDbContext.cs` with the indexes of `data-model.md` (`(ClinicId, RecipientAccountId, RaisedAtUtc DESC, Id DESC)` including `ReadAtUtc`; `(ClinicId, RecipientAccountId)` filtered on `ReadAtUtc IS NULL`; unique `(ClinicId, RecipientAccountId, EventKey, SubjectId, Occasion)` filtered on `Occasion <> ''`; `(ClinicId, NextAttemptAtUtc)` filtered on `State = 'waiting'`); the migration `InitialNotifications` with `CreateClinicIsolation` for each of the three tables; `NotificationsModule.cs`; T006 passes
- [X] T008 Create `src/server/shared/Scs.BuildingBlocks/Notifications/RaisedEvent.cs` (`RaisedEvent(Event, Subject, Facts, Owners, Occasion)`, `EventSubject(Kind, Id, Label)`), `INotificationRecipients` (`ResolveAsync(rule, permission, owners, ct)` returning the distinct accounts of the established clinic that can sign in), `INotificationSwitches` (`IsEmailOnAsync(db, eventKey, ct)`, read on the caller's connection), `INotificationDispatch` (`EmailsWaiting(clinicId)`), and `NotificationWriter.cs`: inside the caller's transaction, check the event is in the table and has a line, the fact keys are allowed and none is of the never-list, a clinical event carries no fact, owners are present when the rule names an owner; ask who is told; decide whether an email is owed (always, or switch and on — with nothing registered to answer for the switches, every switch is on); insert with one statement that reads recipients from a JSON parameter one `notify.Notification` per recipient — skipping, only when an occasion is given, a recipient already told for the same event, subject and occasion — and one `notify.OutgoingEmail` per row inserted when an email is owed; add `Notify(RaisedEvent)` to `src/server/shared/Scs.BuildingBlocks/Tenancy/ClinicAwareDbContext.cs`, flushed by `SaveChangesAsync` in the same transaction as the change and the audit entries, and call `INotificationDispatch.EmailsWaiting` after the save when an email was written

**Checkpoint**: An event can be raised, belongs to one clinic, and its table is fixed.

---

## Phase 3: User Story 2 - Only the people the event names are told (Priority: P1)

**Goal**: Recipients by permission, ownership, or everyone; nobody else, nobody of another clinic.

**Independent Test**: raise events of each rule in a clinic with people of different roles, beside a second clinic.

- [X] T009 [P] [US2] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/RecipientsTests.cs`, with events added by the test's own catalogue for each rule: `Permission` tells every active account whose role holds the code, the holders of the owner role included, and nobody else; `Owner` tells the named accounts; `OwnerAnd` tells both and a person who is both once; `Everyone` tells every active account of the clinic; a disabled account and an invited one are told nothing; nobody of a second clinic is told; a rule that finds nobody writes nothing and fails nothing; a person who loses the permission afterwards keeps the notification, and one who gains it afterwards does not get it
- [X] T010 [US2] Implement `INotificationRecipients` in `src/server/modules/Identity/Scs.Identity/Infrastructure/NotificationRecipients.cs` (accounts in state `active`; holders of a code are accounts whose role holds it or holds everything), register it in `src/server/modules/Identity/Scs.Identity/IdentityModule.cs`; T009 passes

**Checkpoint**: Whoever is told is exactly whom the table names.

---

## Phase 4: User Story 1 - What concerns a person reaches their bell (Priority: P1) 🎯 MVP

**Goal**: An event raised with its change is a notification the person reads in the bell.

**Independent Test**: raise an event for a clinic and read the bell of a person who should be told.

- [X] T011 [P] [US1] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/RaisingTests.cs`: an event raised with a change and saved writes the change and one notification per recipient; when the change fails no notification remains; when the writer refuses, the change is not saved; an unknown key, an event with no line, a fact key the event does not allow, a never-list key, any fact on a clinical event, and a missing owner are each refused; raising with no clinic established is refused; the same event, subject and named occasion raised twice tells each recipient once, and a different occasion tells again; an event raised twice with no occasion about the same record is two notifications; the notification's time is the platform's clock; the facts stored are those passed, and a later change to the record does not change them; an event added by the test alone is raised with no change to the module (SC-009)
- [X] T012 [P] [US1] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/BellTests.cs`: `GET /v1/notifications` answers 401 with no session and, for a signed-in person, their notifications newest first with `id`, `kind`, `event`, `title`, `text`, `at`, `read`, `link` in the shape of `contracts/notifications-api.openapi.yaml`; twenty at a time by default, `size` from 1 to 50, `next` leading to older ones without repeating or skipping one when several share a time; a notification older than ninety days is not returned and not counted; `GET /v1/notifications/state` answers `unread` and `latestAt`; an unreadable `before` or `size` is `VALIDATION_FAILED`; the four addresses are own-account routes and need no permission
- [X] T013 [US1] Create `src/server/modules/Notifications/Scs.Notifications.Contracts/NotificationContracts.cs` (the views the addresses answer), `src/server/modules/Notifications/Scs.Notifications/Infrastructure/NotificationWords.cs` (title from the table, line from the template and the stored facts, in the reader's language, with `{label}` and `{device}`), `Infrastructure/Bell.cs` (list with a cursor on `(RaisedAtUtc, Id)`, state) and `Api/NotificationEndpoints.cs` mapping `GET /v1/notifications` and `GET /v1/notifications/state` as `.OwnAccountOnly()`; add the two to the written list of own-account routes in `src/server/tests/Scs.IntegrationTests/Access/RouteDeclarationTests.cs` and in `specs/005-permissions-engine/contracts/access-engine.md` §1; T011 and T012 pass

**Checkpoint**: Stories 2 and 1 are the MVP: an event raised anywhere is read in the right bells.

---

## Phase 5: User Story 3 - Reading a notification is the person's own business (Priority: P1)

**Goal**: Mark one, mark all, each person's own.

**Independent Test**: give two people the same notification; one reads it; check both.

- [X] T014 [US3] Add to `src/server/tests/Scs.IntegrationTests/Notifications/BellTests.cs`, failing: `POST /v1/notifications/{id}/read` marks it read and answers 204, and again changes nothing; `POST /v1/notifications/read-all` marks every unread one of the person and `state.unread` becomes false; one person's reading leaves a colleague's copy unread; another person's notification, and another clinic's, answers 404 exactly as one that does not exist, and stays unread; both addresses carry `.RecordsNothing(…)` and write no audit entry
- [X] T015 [US3] Add `MarkReadAsync` and `MarkAllReadAsync` to `src/server/modules/Notifications/Scs.Notifications/Infrastructure/Bell.cs` and map the two `POST` routes in `Api/NotificationEndpoints.cs` as `.OwnAccountOnly().RecordsNothing("a person's own read marks")`; add them to the two written lists of T013 and to `Declared` in `src/server/tests/Scs.IntegrationTests/Access/RouteAuditDeclarationTests.cs`; T014 passes
- [X] T016 [P] [US3] Write failing unit tests `src/web/projects/core/src/lib/shell/platform-notifications.spec.ts`: the source asks `/v1/notifications/state` when it starts, every thirty seconds while the tab is visible, when the tab becomes visible, and not while it is hidden; it fetches the list when the bell is opened and when `latestAt` changes while it is open; `markAllRead()` posts and marks every item read at once; `open(item)` posts its read mark, marks it read, and goes to its `link` — or stays when it has none; `loadMore()` asks with `next` and appends; a failed load is `failed` and `retry()` asks again; after sign-out it asks nothing; while there is no connection it marks nothing and asks nothing, and asks again when the connection returns
- [X] T017 [US3] Create `src/web/projects/core/src/lib/shell/platform-notifications.ts` and make it the default `NotificationsSource` in `src/web/projects/core/src/lib/shell/sources.ts` (an item gains `link`; the source gains `hasMore` and `loadMore()` and a signal for the dot); keep the builders' sample source of `src/web/projects/clinic/src/app/dev/sample-sources.ts` working; make `src/web/projects/clinic/src/app/shell/bell-panel.component.ts` take the dot from the source and load more when the list is scrolled to its end; T016 passes and the clinic's unit tests stay green
- [X] T018 [US3] Make the made-up platform of `src/web/e2e/staff-platform.ts` answer `GET /v1/notifications/state`, `GET /v1/notifications`, `POST /v1/notifications/*/read` and `POST /v1/notifications/read-all` — empty by default, a given list when a test supplies one, recording the calls — and run `npx playwright test` to see every existing test still pass
- [X] T019 [US3] Write `src/web/e2e/notifications.spec.ts`: with notifications from the platform the bell has its dot and the panel lists them newest first, unread ones tinted, in Arabic and in English; a notification arriving while the console is open shows its dot without a reload, within the polling time; pressing one marks it read, closes the panel and opens its link, and one without a link stays; «تعليم الكل كمقروء» clears the dot with no passing notice; scrolling to the end loads older ones; no notifications shows «لا إشعارات جديدة» with no «تعليم الكل كمقروء»; a failed load shows the error state and «إعادة المحاولة» loads; the time reads as the frames write it for an item of two hours ago, of earlier today, of yesterday and of last month, in both languages; and, when `SCS_SHOTS` is set, pictures of the panel filled and empty in both languages at 1440×900

**Checkpoint**: All P1 stories are done; the bell is real.

---

## Phase 6: User Story 4 - Email follows the event, and never carries anything clinical (Priority: P2)

**Goal**: Always, switch, never; sent after the change; retried; dropped for an account that can no longer sign in.

**Independent Test**: raise one event of each email kind and look at what the made-up mail sender received.

- [X] T020 [P] [US4] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/EmailTests.cs`: an `always` event owes one email per recipient and a `never` event none; nothing is sent inside the raising save, and the save succeeds while the mail sender fails; running the clinic's email job sends what is waiting — subject the event's title, body the title, the line and a link to the console — in the recipient's chosen language, Arabic when none was chosen, and in one language only; a failed send stays waiting with a later next attempt, and is `gave-up` once twenty-four hours have passed since it was created; an account disabled after the event is `dropped` and receives nothing, while one that only lost the permission still receives it; no address, password or code appears in `notify.OutgoingEmail`, in `notify.Notification` or in the captured logs; an `always` event raised for a suspended clinic still has its email sent by the job; the emails of specs 003 and 004 are unchanged and have no bell item
- [X] T021 [US4] Add `ContactsAsync(accountIds)` to `IStaffDirectory` in `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs` and `Infrastructure/StaffDirectory.cs` (address, chosen language, whether the account can sign in; an address is personal data and never logged)
- [X] T022 [US4] Create `src/server/modules/Notifications/Scs.Notifications/Infrastructure/NotificationEmails.cs` (the one layout of research R8, text and HTML, one language) and `NotificationEmailJob.cs` (`IClinicJob`, `[RecordsNothing(…)]`: sends rows whose `NextAttemptAtUtc` has come, asks contacts, drops, sends through `IEmailSender`, waits one, five and thirty minutes and then an hour between tries, gives up after twenty-four hours); implement `INotificationDispatch` in the module by enqueuing the job for the clinic; T020 passes
- [X] T023 [US4] Create `src/server/modules/Notifications/Scs.Notifications/Infrastructure/EmailSweepJob.cs` (every minute, `[RecordsNothing(…)]`: the ids of clinics with email waiting, each enqueued) with a new declared crossing `list-clinics-with-email-waiting` in `src/server/shared/Scs.BuildingBlocks/Tenancy/Crossing.cs` and the count in `src/server/tests/Scs.ArchitectureTests/DeclaredCrossingsTests.cs`; register both jobs in `NotificationsModule.cs`; add to `EmailTests.cs` that the sweep finds a clinic with email waiting and no other

**Checkpoint**: What the bell says, the mail says too, when the event calls for it.

---

## Phase 7: User Story 6 - A clinic decides which billing and licence emails it gets (Priority: P2)

**Goal**: Four switches, on by default, for the whole clinic, changed only with the permission, audited.

**Independent Test**: change a switch through the platform with and without the permission; raise the event.

- [X] T024 [P] [US6] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/EmailSwitchAccessTests.cs` (first, permissions): `GET /v1/settings/notification-emails` answers 401 with no session, 403 `PERMISSION_REQUIRED` with `settings.view` without it, 200 with it; `PUT /v1/settings/notification-emails/{event}` answers 403 with `settings.notifications` for a person holding `settings.view` alone, 200 for a holder; both routes declare those permissions; a holder in one clinic never reads or changes another clinic's switch
- [X] T025 [P] [US6] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/EmailSwitchTests.cs`: a new clinic reads four switches, all on, with their names in the reader's language and their groups; turning one off stops the email of that event from the next raise on and never the bell notification; an email already owed is still sent; another clinic's emails are unaffected; a change writes one audit entry `settings.notification-email-changed` in category `changes` with `enabled` before and after and the event as target, in the same transaction, and setting a switch to the value it has writes none; an event whose email is `always` or `never` answers `VALIDATION_FAILED` with `event` `NOT_SWITCHABLE`; an unknown event answers 404; a missing `enabled` is `REQUIRED`
- [X] T026 [US6] Add the action `settings.notification-email-changed` (category `changes`, needs before and after, name «تغيير بريد تنبيه» · "Notification email changed", sentences of `data-model.md` §7) to `src/server/shared/Scs.SharedKernel/Audit/AuditActions.cs` and `audit-actions.lock`, with its cases in `src/server/tests/Scs.UnitTests/Audit/AuditActionsTests.cs` and `SentencesTests.cs`; create `src/server/modules/Notifications/Scs.Notifications/Infrastructure/EmailSwitches.cs` (read; change with `db.Record(…)`; implement `INotificationSwitches`) and map the two routes in `Api/NotificationEndpoints.cs` with `.Requires(Permissions.SettingsView)` and `.Requires(Permissions.SettingsNotifications).Records(AuditActions.SettingsNotificationEmailChanged)`; add the `PUT` to `Declared` in `RouteAuditDeclarationTests.cs`; T024 and T025 pass

**Checkpoint**: Who can switch it off is true for every event.

---

## Phase 8: User Story 5 - A sign-in from a new device tells the account's owner (Priority: P2)

**Goal**: The first real event, end to end.

**Independent Test**: sign in to an account from a fresh browser, then again from the same one.

- [X] T027 [US5] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/NewDeviceTests.cs` and add `identity.KnownDevice` to `src/server/tests/Scs.IntegrationTests/Isolation/NotificationSamples.cs`: an account's first completed sign-in ever sets the device cookie and raises nothing; the same browser again raises nothing; a second browser raises one `account.new-device` notification for the owner — line the device in words, no link — and owes one email, in the same transaction as the session; the same second browser again raises nothing; a wrong password, a wrong code and a refused sign-in set no cookie and raise nothing; a different network with the same browser raises nothing; an account that signed in many times before this spec and has no known device raises nothing on its next sign-in; the email owed says the device in words and when, in the owner's language; the cookie is HTTP-only, secure and lasts thirteen months, and its value is not stored — only its hash; turning every switch off does not stop the email; the notification is read in English as "Sign-in from a new device" with "Chrome on Windows"
- [X] T028 [US5] Create `src/server/modules/Identity/Scs.Identity/Domain/KnownDevice.cs` (`MarkHash` binary(32) marked secret; unique `(AccountId, MarkHash)`), map it in `Infrastructure/Persistence/IdentityDbContext.cs`, add the migration `KnownDevices` with its isolation policy; add the device mark cookie `__Host-scs-device` to `src/server/shared/Scs.BuildingBlocks/Security/SessionCookies.cs` (read, write; never cleared by sign-out); create `Infrastructure/KnownDevices.cs` (recognise, add, raise when the account already had one) called where a sign-in completes in `Infrastructure/SignInService.cs`, with `Api/AuthEndpoints.cs` reading the mark and writing a new one; remove devices not seen for thirteen months in `Infrastructure/CleanupJob.cs`; T027 passes

**Checkpoint**: The engine has told a real person about a real thing.

---

## Phase 9: User Story 7 - A later spec adds its event without touching the engine (Priority: P3)

**Goal**: The lock, the reminders told once, and the ninety days.

**Independent Test**: add an event in a test alone and raise it; raise a reminder twice.

- [X] T029 [P] [US7] Write failing `src/server/tests/Scs.IntegrationTests/Notifications/CleanupTests.cs`: the daily clean-up removes notifications older than ninety days, read or not, and their email rows with them, and leaves newer ones; running it twice changes nothing more; it writes no audit entry; and add to `RaisingTests.cs` that a reminder raised twice by a job for the same record and the same day-count tells once while the next day-count tells again
- [X] T030 [US7] Create `src/server/modules/Notifications/Scs.Notifications/Infrastructure/CleanupJob.cs` (`IClinicJob`, daily, `[RecordsNothing(…)]`) and register it in `NotificationsModule.cs`; add `src/server/tests/Scs.ArchitectureTests/NotificationRulesTests.cs`: the module reaches accounts only through Identity's contracts; rows of `notify.Notification` are inserted by the shared writer alone; no module but Notifications reads `notify` tables; T029 passes

**Checkpoint**: Ninety specs can add their events.

---

## Phase 10: User Story 8 - A notification reads in the language of whoever reads it (Priority: P3)

**Goal**: Titles, lines and emails in Arabic and English, from stored facts.

**Independent Test**: read the same notifications in Arabic and in English.

- [X] T031 [P] [US8] Write failing `src/server/tests/Scs.UnitTests/Notifications/NotificationWordsTests.cs`: each event's title is its name in the reader's language; the line of `account.new-device` reads «متصفح Chrome على Windows» · "Chrome on Windows" and «جهاز غير معروف» · "Unknown device"; `{label}` keeps its Latin letters in both languages; a number placed in a line is Arabic-Indic in Arabic and Western in English; a name a person wrote is as written; an English title and line contain no Arabic letter and the reverse, except identifiers and what a person wrote; the email for an event has its subject and body in one language and names no address, password or code
- [X] T032 [US8] Complete `src/server/modules/Notifications/Scs.Notifications/Infrastructure/NotificationWords.cs` and `NotificationEmails.cs` to pass T031, and add to `BellTests.cs` a case reading one page in both languages by `Accept-Language`

**Checkpoint**: All stories are complete.

---

## Phase 11: Polish & Cross-Cutting Concerns

- [X] T033 [P] Write `src/server/tests/Scs.IntegrationTests/Notifications/NotificationVolumeTests.cs`: a person with a thousand notifications reads the first page with the state in under one second and marks all read in under one second (SC-007); raising an event told to two hundred people adds under a tenth of a second to the save that raises it (SC-008); the figures are written to the test's output
- [X] T034 [P] Update `docs/adding-a-module.md` (telling people: the steps of `contracts/notifications-engine.md` §1 and §2, and its rules for the caller), `docs/setup.md` (the bell on the local stack, the worker that sends email, the switches' address) and `ops/runbooks/data-handling.md` (what `notify` holds, that no address is stored there, the ninety days)
- [X] T035 Run every gate with nothing else running: `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T036 Walk the real stack as `quickstart.md` "By hand": the seven steps with `owner@shifa.example` in two browsers and `reception@shifa.example`, in Arabic and English, with the email read in Mailpit
- [X] T037 Lay the panel beside its frames: `SCS_SHOTS=<folder> npx playwright test notifications`, then compare the filled and the empty panel in Arabic and English with frames `5999:831798`, `5999:834947`, `5999:832473`, `5999:835622` and the items of component `4262:1056` — icon marks, tint, dot, spacing, the time's wording — and fix what differs
- [X] T038 Write `specs/008-notifications-engine/validation.md`: what was run with its counts, what was walked, the measured times, what was compared with the design and what differs, what was built differently from the plan, and what was not done; record the owner items — the undrawn marks and emails, the BRD's ق-٥٧ and CL-SB-149, the clinic's language — and that FR-028's read-only clinic cannot be tested until the subscription specs, and that FR-032 needs nothing built

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → stories.
- **US2 (3)** needs only the foundation; **US1 (4)** needs US2 (a notification needs a recipient).
- **US3 (5)** needs US1. Its web tasks (T016–T019) need the addresses of T013 and T015 only as a contract and can be written beside them.
- **US4 (6)** needs US1. **US6 (7)** needs US4 (a switch is seen in the email). **US5 (8)** needs US1 and US4.
- **US7 (9)** and **US8 (10)** need US1; US8's email cases need US4.
- **Polish (11)** last.

### Within Each Story

- The test task first, seen to fail; then the implementation that makes it pass.
- T006 and T024 are written before any code they test exists (isolation, permission).

### Parallel Opportunities

- T004 and T006 together; T011 and T012 together; T016 beside T014–T015; T024 and T025 together; T033 and T034 together.
- The web tasks of US3 beside the server tasks of US4 and US6.

## Parallel Example: User Story 1

```text
T011  RaisingTests.cs      (integration)
T012  BellTests.cs         (integration)
```

## Implementation Strategy

### MVP First

1. Phases 1 and 2 — the table, the tables, the writer.
2. Phases 3 and 4 — recipients and the bell's reading.
3. **Stop and validate**: raise an event in a test and read it at `GET /v1/notifications`.

### Incremental Delivery

4. Phase 5 — marking, and the console's bell with real answers.
5. Phases 6 and 7 — email and the switches.
6. Phase 8 — the new device; the real stack can now be walked.
7. Phases 9 and 10 — clean-up, the rules for later specs, the two languages.
8. Phase 11 — volume, documents, gates, the walk, the design comparison, `validation.md`.

## Notes

- Requirement coverage: FR-001–004 T004–T005 · FR-005–009 T008, T011, T029 · FR-010–014 T006, T009–T010 · FR-015–019b T007, T011, T013, T029–T030 · FR-020–029 T012–T019 · FR-030–036 T020–T023 · FR-035a T020–T022 · FR-037–041 T024–T026 · FR-042–044 T027–T028 · FR-045–046 T004, T031–T032 · SC-007, SC-008 T033 · SC-012 T019, T037.
- The implementation is not complete until T035 to T038 are done and written down (constitution VII).

## Phase 12: Convergence

- [X] T039 Make a panel that is open show what was read elsewhere: in `src/web/projects/core/src/lib/shell/platform-notifications.ts` fetch the list again when the state's `unread` turns false while the panel is open and an item held is still unread (today only the dot goes out and the items stay tinted until the panel is reopened); add the case to `platform-notifications.spec.ts` and to `src/web/e2e/notifications.spec.ts` per FR-026 (partial)
- [X] T040 Make the email of «الدخول من جهاز جديد» say when the sign-in happened, in Cairo time and in the recipient's language and digits (today the email holds the title, the device and the link, and the moment is only in the bell): `src/server/modules/Notifications/Scs.Notifications/Infrastructure/NotificationEmails.cs` and `NotificationWords.cs`, with a case in `src/server/tests/Scs.IntegrationTests/Notifications/NewDeviceTests.cs` per FR-042, US6/AC1 (partial)
