# Tasks: Operator Sign-in

**Input**: Design documents from `/specs/010-operator-sign-in/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (operators-api.openapi.yaml, operator-sessions.md, operator-screens.md), quickstart.md

**Tests**: required by the constitution (Principle VII). Tests of who may reach what — no session, the wrong kind of session, an ended session, the single answer, the lock, one session — are written first and must fail before the code that satisfies them.

**Organization**: by user story. Server paths are under `src/server/`, console paths under `src/web/`. `Operators/` means `src/server/modules/Operators/Scs.Operators/`; `ops/` means `src/web/projects/ops/src/app/`; test folders are `src/server/tests/Scs.IntegrationTests/Operators/` and `src/server/tests/Scs.UnitTests/Operators/`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves (US5a is "the first operator chooses their password")

---

## Phase 1: Setup

- [X] T001 Record the baseline before any change: with the local API stopped, run `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci` and `npx playwright test` (in `src/web`), and note the counts for `specs/010-operator-sign-in/validation.md` (expected: 551 · 51 · 712; clinic 68, core 164, ui 95; 397 browser tests and 4 skipped)
- [X] T002 Create the projects `src/server/modules/Operators/Scs.Operators/Scs.Operators.csproj` and `src/server/modules/Operators/Scs.Operators.Contracts/Scs.Operators.Contracts.csproj` as the Files module's are, add both to `src/server/Scs.sln`, and reference them from `host/Scs.Api`, `tools/Scs.Migrator` and the three test projects
- [X] T003 [P] Add `OpsOptions` to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` — `SCS_OPS__IDLE_MINUTES` (default 30, "15 to 10,080"), `SCS_OPS__APP_BASE` ("required outside local"), `SCS_OPS__RESET_LINK_MINUTES` (30), `SCS_OPS__FIRST_LINK_HOURS` (72), `SCS_OPS__RECORD_MONTHS` (12) — with the rules in `ScsOptionsValidator.cs`, cases in `tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs`, and the keys in `deploy/compose/.env.example`

---

## Phase 2: Foundational (blocks every story)

- [X] T004 Move, unchanged in behaviour, from `modules/Identity/Scs.Identity/Infrastructure/` into `shared/Scs.BuildingBlocks/Security/`: the password hasher (`PasswordHasher.cs`), the keyed hash of a sign-in code (`CodeHashing.cs`), the password rule — "at least 10 characters with an upper-case and a lower-case letter, a digit and a symbol, and at most 200" (`PasswordRules.cs`) — and the reading of a browser and system from a request (`BrowserNames.cs`); make Identity call them from there and change nothing else; every existing Identity test passes untouched (research R3)
- [X] T005 [P] Add the eight codes of data-model §9 to `shared/Scs.SharedKernel/ErrorCodes.cs` — `ACCOUNT_DISABLED`, `ACCOUNT_LOCKED`, `CODE_INVALID`, `SIGN_IN_CODE_NOT_SENT`, `STEP_EXPIRED`, `LINK_INVALID`, `VALIDATION_FAILED` (password: `SAME_AS_CURRENT`), `VALIDATION_FAILED` (current: `WRONG`) — with their words in both languages in `shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs` and the count in `tests/Scs.UnitTests/Errors/ErrorCatalogueTests.cs`
- [X] T006 Create the six entities in `Operators/Domain/` exactly as data-model §1–§6 — `Operator` (`FullName` 1–200; `Email` at most 254, trimmed, lower case, unique, never changed; `Mobile` at most 30; `Language` `ar`·`en` default `ar`; `State` `invited`·`active`·`suspended`; counters not negative; `RowVersion`), `SignInChallenge` (`AttemptsLeft` starts at 5, `Resends` at most 3), `OperatorSession` (`EndedFor` one of `signed-out`·`replaced`·`idle`·`expired`·`password-reset`·`suspended`·`signed-out-everywhere`·`reuse`·`role-changed`; `Address` at most 45; `Browser`, `System` at most 60), `PasswordLink` (`Kind` `reset`·`first`), `KnownBrowser`, `SignInRecord` (the thirteen kinds) — with `Operators/Infrastructure/Persistence/OperatorsDbContext.cs` (a plain context, schema `ops`), the filtered unique index "one open session for an operator", and the migration `InitialOperators` with its checks, indexes and the grant that makes `SignInRecord` insert-only for the application's login
- [X] T007 Add the six `ops.*` lines with their reasons (data-model §1–§6) to `shared/Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs`; register the module in `host/Scs.Api/ModuleRegistration.cs` and migrate it after Identity in `tools/Scs.Migrator/Migrator.cs`; create `Operators/OperatorsModule.cs`
- [X] T008 Write failing `tests/Scs.ArchitectureTests/OperatorRulesTests.cs`: every table of schema `ops` is in the platform-wide list with a reason; `Scs.Operators` references no other module but contracts; every route under `/v1/ops/` declares anonymous, `OperatorsOnly()` or own-account; no route outside `/v1/ops/` accepts the `OperatorSession` scheme and none under it accepts a clinic's; the operators' cookie names appear only in `OperatorCookies.cs`
- [X] T009 Create `shared/Scs.BuildingBlocks/Security/OperatorCookies.cs` (the four cookies of operator-sessions.md §2 with their paths, flags and lives, their own data-protection purposes) and `OperatorSessionAuthentication.cs` (the scheme `OperatorSession`, its claims, `IOperatorSessionCheck`, `OperatorsOnly()` and own-account for operators, the answer `401 UNAUTHENTICATED` with `reason`), wire the scheme in `host/Scs.Api/Program.cs` so that it is the only one read under `/v1/ops/` and never read elsewhere, add kinds `ops-reset` and `ops-first` to `PasswordLinkTokens.cs`, and update the rule lists of `tests/Scs.ArchitectureTests/` that name the files allowed to protect a token and — in `IdentityRulesTests` — the files allowed to declare "own account", which now include the operators' profile routes; T008 passes for what exists
- [X] T010 [P] Create `tests/Scs.IntegrationTests/Fixtures/OperatorFixtures.cs`: an operator in a given state with a known password, a browser that keeps the operators' cookies, reading the last code and link from the captured mailbox, the fake clock, and helpers that read `ops.Session` and `ops.SignInRecord`
- [X] T011 [P] Create `Operators/Infrastructure/SignInRecords.cs` — one insert for each kind of data-model §6 with address, browser, system and trace id; facts never hold a password, a code, a token, a link or an email — and `tests/Scs.UnitTests/Operators/SignInRecordTests.cs` for the kinds and the refusal of a secret-shaped fact
- [X] T012 [P] Give the `ui` auth frame what the design's split screen needs — a brand panel with the mark, a title and a line that change by screen, form on the right in Arabic and the left in English, the panel dropped under 900 wide — in `src/web/projects/ui/src/lib/auth-frame/`, with unit tests; the clinic's sign-in screens are unchanged (their browser tests pass)
- [X] T013 [P] Set the operators' brand tokens in `src/web/projects/ops/src/styles.css`, add `ops.*` keys for every word of `contracts/operator-screens.md` to `src/web/projects/ops/public/i18n/ar.json` and `en.json` (Arabic as quoted there; the entries marked "(owner)" as drafted), and create `src/web/e2e/ops-platform.ts` that answers the sixteen addresses of `contracts/operators-api.openapi.yaml` with settable answers

**Checkpoint**: the module exists, migrates and is empty; the scheme refuses everything; the console can be drawn.

---

## Phase 3: User Story 1 - An operator signs in with email, password and an email code (P1) 🎯 MVP

**Goal**: email and password, a code by email, and the operator is inside the console.

**Independent Test**: with an active operator and the mailbox, sign in end to end and arrive in the frame; no session exists after the password alone.

- [X] T014 [P] [US1] Write failing `tests/Scs.IntegrationTests/Operators/SignInTests.cs`: a correct password opens no session, sets the sign-in cookie and sends one email with a six-digit code to the account's address in its language; the right code opens a session, sets the session and renewal cookies, clears the sign-in cookie, sets `LastSignInAtUtc`, clears the count of wrong tries and writes `login_succeeded`; `GET /v1/ops/auth/session` then answers the operator's name, language, role name and `idleMinutes`; with no session it answers 401; an email with capitals and spaces signs in; a password longer than 200 is the single answer
- [X] T015 [US1] Create `Operators/Infrastructure/OperatorSignIn.cs` (step one and step two, the order of checks of research R6, the code of research R7 using the shared code hash and the existing `SCS_AUTH__*` figures), `OperatorSessions.cs` (open a session; the per-request check of the session's row) and `OperatorEmails.cs` (the code's email, in the account's language only, no link), and `Operators/Api/OperatorAuthEndpoints.cs` with `POST /v1/ops/auth/sign-in`, `POST /v1/ops/auth/code` and `GET /v1/ops/auth/session` as the contract; T014 passes
- [X] T016 [P] [US1] Create `ops/auth/ops-auth.service.ts` (sign in, send the code, who is signed in, the session's state as a signal) and `ops/auth/ops-auth.guard.ts` (no session → `/sign-in` keeping where the person was going; signed in on `/sign-in` → inside), with unit tests
- [X] T017 [US1] Create `ops/sign-in/sign-in.page.ts` and `ops/sign-in/code.page.ts` as frames `128:85`/`128:54` and `135:61`/`135:38` — fields, «تذكرني», «نسيت كلمة المرور؟», checks under the fields before sending, the button locked and reading «جارٍ تسجيل الدخول…» while working, the six boxes (digits only, paste fills, the sixth sends) — a first `ops/shell/ops-shell.component.ts` with the top bar's name and a first `ops/home/home.page.ts`, and the routes in `ops/app.routes.ts` and `app.config.ts` replacing the placeholder
- [X] T018 [US1] Write `src/web/e2e/ops-sign-in.spec.ts`: the Arabic and English screens word for word; empty and malformed fields said under them with nothing sent; a second press sends nothing; sign in to the frame; an address opened with no session returns to it after sign-in; a signed-in operator opening `/sign-in` goes inside; pictures of each state when `SCS_SHOTS` is set

**Checkpoint**: an operator can sign in and see the console.

---

## Phase 4: User Story 2 - Nothing is revealed before a correct password (P1)

**Goal**: one answer, in words, form and time, for anyone who does not know the password.

**Independent Test**: an unknown email, a wrong password, and a wrong password for a locked, a suspended and an invited account give the same answer.

- [X] T019 [P] [US2] Write failing `tests/Scs.IntegrationTests/Operators/SingleAnswerTests.cs`: the five cases answer the same status, code and body; an invited account answers it for any password; a suspended account with the correct password answers `ACCOUNT_DISABLED` and sends no code; an unknown email is recorded as a keyed hash in `security.AnonymousSignInAttempt` and never as the address; a wrong password writes `login_failed`; over a hundred tries each, the times for an unknown email and a wrong password overlap
- [X] T020 [US2] Complete `OperatorSignIn.cs`: a full verification against a fixed dummy hash for an unknown email and an invited account, one refusal path that does the same work for every case before a correct password, the state checked only after it; T019 passes
- [X] T021 [US2] Show the banner «بيانات الدخول غير صحيحة» / «تأكد من البريد الإلكتروني وكلمة المرور وحاول مرة أخرى.» between the line and the fields (closable, both fields keep what was typed) and «هذا الحساب موقوف.» in `ops/sign-in/sign-in.page.ts`, as frames `134:54`/`134:15`; add both to `src/web/e2e/ops-sign-in.spec.ts` with pictures

---

## Phase 5: User Story 3 - Wrong tries lock the account (P1)

**Goal**: five wrong passwords lock for fifteen minutes; the right password then explains.

**Independent Test**: five wrong, then right → the lock warning with its time; wait or reset → in.

- [X] T022 [P] [US3] Write failing `tests/Scs.IntegrationTests/Operators/LockTests.cs`: the fifth wrong password locks for fifteen minutes, writes `account_locked` and still answers the single answer; the correct password while locked answers `ACCOUNT_LOCKED` with `until` and sends no code; wrong passwords during the lock do not move `until`; after fifteen minutes a correct sign-in is let in and the count is zero; wrong tries older than the attempt window are forgotten; a completed sign-in clears the count
- [X] T023 [US3] Implement the count, the window and the lock in `Operators/Domain/Operator.cs` and `OperatorSignIn.cs` from the existing `SCS_AUTH__MAX_FAILED_ATTEMPTS`, `ATTEMPT_WINDOW_MINUTES` and `LOCK_MINUTES`; T022 passes
- [X] T024 [US3] Show the lock warning in `ops/sign-in/sign-in.page.ts` — locked until ‹time› in the reader's language and digits, never breaking between the time and its AM/PM mark, and that resetting the password opens it now — with its case and picture in `src/web/e2e/ops-sign-in.spec.ts`

---

## Phase 6: User Story 5a - The first operator chooses their password from an emailed link (P1)

**Goal**: one deployment command creates the first operator and emails a link; the operator chooses a password and can sign in.

**Independent Test**: run the command, open the link from the mailbox, choose a password, sign in; a used and an expired link are refused.

- [X] T025 [P] [US5a] Write failing `tests/Scs.IntegrationTests/Operators/FirstOperatorTests.cs`: the command creates an `invited` account and sends one email in Arabic with a link good for 72 hours; run again for an invited account it sends a new link and the earlier one is refused; for an active account it changes nothing and says so; with no mail settings it refuses and creates nothing; each run writes `operator_invited_by_deployment`; `operator withdraw` removes an invited account that has not chosen a password, ends its links (opening one answers `LINK_INVALID`), writes `operator_invite_withdrawn`, and refuses for an active or suspended account (FR-003a); `POST /v1/ops/password/link` reads a good link as `first` with the email and changes nothing; `POST /v1/ops/password/set` with a good password makes the account `active`, uses the link, writes `first_password_set` and opens no session; a weak password answers `VALIDATION_FAILED` (password: the rules not met) with the rules not met; a link older than 72 hours, used, replaced or malformed answers `LINK_INVALID`; an invited account cannot sign in
- [X] T026 [US5a] Create `Operators/Infrastructure/OperatorInvite.cs` and `OperatorPasswords.cs` (make, read and consume a link with the shared tokens and kinds; set a first password with the shared rule and hasher), the first-password email in `OperatorEmails.cs`, `Operators/Api/OperatorPasswordEndpoints.cs` with `POST /v1/ops/password/link` and `POST /v1/ops/password/set`, and the commands `operator invite --name --email` and `operator withdraw --email` in `tools/Scs.Migrator/Program.cs`, which print the account's state and no secret; make the local seed invite the made-up operator; T025 passes
- [X] T027 [US5a] Create `ops/password/set-password.page.ts` at `/password/first` as frames `167:66`/`167:38`: the email shown and locked, «كلمة المرور الجديدة», «تأكيد كلمة المرور», the four rules turning as each is met (ten characters, not the frame's eight), «تفعيل الحساب», the state for a link that no longer works that points to whoever sent it, and the "account is ready" line on the sign-in screen afterwards; cases and pictures in `src/web/e2e/ops-password.spec.ts`
- [X] T028 [P] [US5a] Write in `docs/setup.md` how the first operator is invited locally and at deployment, and in `ops/runbooks/` what is done for an only operator with no mailbox

**Checkpoint**: the platform can be given its first operator, who signs in.

---

## Phase 7: User Story 4 - The code is short-lived, limited and can be sent again (P2)

**Goal**: ten minutes, five tries, once; a new code after a minute, replacing the old.

**Independent Test**: five wrong codes, an expired code and a resent code each behave as the spec says.

- [X] T029 [P] [US4] Write failing `tests/Scs.IntegrationTests/Operators/CodeTests.cs`: a wrong code answers `CODE_INVALID` with `attemptsLeft` and writes `otp_failed`; after five the sign-in is spent and answers `STEP_EXPIRED`; a code older than ten minutes, a used code and a replaced code answer as a wrong one; a resend before sixty seconds or after the third is `RATE_LIMITED` with `retryAfterSeconds`; a resend sends a new email and the earlier code is refused; a resend that cannot send leaves the earlier code good and answers `SIGN_IN_CODE_NOT_SENT`; a correct password with a mail failure answers `SIGN_IN_CODE_NOT_SENT` and leaves no challenge; a code presented from a browser without the sign-in cookie is refused; cancel ends the challenge; an account suspended between the password and the code is refused at the code; three codes spent in a row lock the account and a completed sign-in clears that count (FR-020a); a fourth new code in one sign-in is refused (FR-021)
- [X] T030 [US4] Complete the code in `OperatorSignIn.cs` and add `POST /v1/ops/auth/code/resend` and `/code/cancel` to `OperatorAuthEndpoints.cs`; T029 passes
- [X] T031 [US4] Complete `ops/sign-in/code.page.ts`: the countdown «يمكنك إعادة إرسال الكود خلال ‹د:ث›» computed from the platform's time, the control to send again at zero, the errors under the boxes with the boxes emptied, tries spent returning to the sign-in form with a line, «الرجوع لتسجيل الدخول» ending the sign-in, the boxes announced as one six-digit code; cases and pictures in `src/web/e2e/ops-sign-in.spec.ts`

---

## Phase 8: User Story 5 - An operator replaces a forgotten password (P2)

**Goal**: a link by email, thirty minutes, a new password; sessions end and a lock lifts.

**Independent Test**: ask, open, choose, sign in; a used and an expired link are refused.

- [X] T032 [P] [US5] Write failing `tests/Scs.IntegrationTests/Operators/ForgotPasswordTests.cs`: `POST /v1/ops/password/forgot` answers 202 for every email after the same work; an email is sent only for an active account, locked or not; none for invited, suspended or unknown; a second within a minute sends none; a newer link ends the older; the link reads as `reset`; setting the password ends every session of the account with `password-reset`, lifts the lock, clears the counts, ends every waiting challenge and link, writes `password_reset_requested` and `password_reset_completed`; the current password answers `VALIDATION_FAILED` (password: `SAME_AS_CURRENT`); a link older than thirty minutes or used answers `LINK_INVALID`; the device with the ended session gets 401 with `reason: password-reset`
- [X] T033 [US5] Add the forgot path and the reset to `OperatorPasswords.cs`, the reset email to `OperatorEmails.cs`, and `POST /v1/ops/password/forgot` to `OperatorPasswordEndpoints.cs`; T032 passes
- [X] T034 [US5] Create `ops/password/forgot.page.ts` as frames `141:53`/`141:38` and `158:52`/`158:38` («إعادة الإرسال» quiet for a minute after each sending), serve `/password/reset` from `set-password.page.ts` with its own title, line and button and a "send a new link" state, and show the "password was changed" line on the sign-in screen; cases and pictures in `src/web/e2e/ops-password.spec.ts`

---

## Phase 9: User Story 6 - One session at a time, ending when it should (P2)

**Goal**: a new sign-in ends the earlier session; sign-out, idleness, age, reset and suspension end one at once; a new browser is told by email.

**Independent Test**: two browsers in turn; sign out; an idle session; each ends as the spec says.

- [X] T035 [P] [US6] Write failing `tests/Scs.IntegrationTests/Operators/SessionTests.cs`: completing a second sign-in ends the first with `replaced` in the same transaction and the first browser's next request is 401 with that reason, read once; two sign-ins completing together leave one open session; sign-out ends the session, clears the cookies and writes `signed_out`, and the old cookies are refused; a request thirty minutes after the last activity is 401 with `idle`; a renewal and a request with `X-Scs-Background: 1` do not move the last activity; twelve hours after sign-in it is `expired` however active; a suspended account's next request is 401 with `suspended`; `remember: false` sets cookies with no expiry and `true` with the session's end; a staff session on `/v1/ops/profile` and an operator's session on a clinic's route are each no session; every ending writes `session_ended` with its reason
- [X] T036 [P] [US6] Write failing `tests/Scs.IntegrationTests/Operators/NewBrowserTests.cs`: a sign-in with no device mark, or one this account has not used, sets the mark, writes `ops.KnownBrowser` and sends one email with the browser, system, address and time; a sign-in from a known browser sends none; a mail failure does not fail the sign-in and writes `new_browser` with "not sent"
- [X] T037 [US6] Complete `OperatorSessions.cs` (one at a time, end with a reason, the idle and age checks from `SCS_OPS__IDLE_MINUTES` and `SCS_AUTH__SESSION_HOURS`, renew with the remembered-or-not cookies), create `Operators/Infrastructure/KnownBrowsers.cs` and the new-browser email, add `POST /v1/ops/auth/session/renew` and `/sign-out` to `OperatorAuthEndpoints.cs`, and implement `IOperatorSessions.EndAllAsync(operatorId, reason)`, `IOperatorDirectory` and `IOperatorAccess` in `Scs.Operators.Contracts`; T035 and T036 pass
- [X] T038 [US6] Complete `ops/auth/ops-auth.service.ts`: renewal in use, the idle clock on keyboard and pointer use that signs out at the idle period, the question "who is signed in" asked every twenty seconds as a background request (paused while the tab is hidden, asked at once when it is shown) so that a session ended elsewhere shows the sign-in screen within thirty seconds, the header `X-Scs-Background: 1` on what a page asks by itself, the reason of an ended session shown once on the sign-in screen with the lines of operator-sessions.md §3, sign-out, and the message between tabs; unit tests, including one that fails a `GET` made from a timer without the header
- [X] T039 [US6] Add to `src/web/e2e/ops-sign-in.spec.ts`: each reason's line on the sign-in screen; sign-out and the browser's back button showing nothing of the console; a second tab following a sign-out; the idle sign-out without a click (with a shortened period); a session ended elsewhere shown with its reason within thirty seconds on a console nobody touches

---

## Phase 10: User Story 7 - A stolen session is detected and shut down (P2)

**Goal**: an old renewal secret presented again ends the session and is recorded.

**Independent Test**: replay an earlier renewal value; both holders are signed out and a record is written.

- [X] T040 [P] [US7] Write failing cases in `tests/Scs.IntegrationTests/Operators/SessionTests.cs`: each renewal replaces the secret and keeps the hash of the one before; presenting the previous one within its short grace is accepted once for two tabs renewing together; presenting an older one ends the session with `reuse`, writes `session_token_reuse`, and both holders get 401; the operator then signs in normally
- [X] T041 [US7] Implement the rotation and the reuse check in `OperatorSessions.cs`; T040 passes

---

## Phase 11: User Story 8 - The operator sees their account and sessions, and changes their password (P2)

**Goal**: the profile page: account details, recent sessions, sign out of all devices, change password.

**Independent Test**: open the profile, read the sessions, change the password with each wrong input and then rightly, sign out of all devices.

- [X] T042 [P] [US8] Write failing `tests/Scs.IntegrationTests/Operators/ProfileTests.cs`: `GET /v1/ops/profile` answers the operator's own name, role name, email, mobile, `twoFactor` on and locked, and at most ten sessions newest first with this one `current`; it never returns another operator's or a hash; `POST /v1/ops/profile/password` with a wrong current password answers `VALIDATION_FAILED` (current: `WRONG`) and counts as a wrong try, and the fifth signs the operator out and locks the account; the same password answers `VALIDATION_FAILED` (password: `SAME_AS_CURRENT`); a weak one `VALIDATION_FAILED` (password: the rules not met); a right current password and a valid new one change nothing and send a code (202); `/v1/ops/profile/password/confirm` with the right code changes the password, keeps this session open and writes `password_changed`; a wrong code answers `CODE_INVALID`, five end the pending change without adding to the count of spent codes, an expired code and `/cancel` leave the password as it was, `/resend` follows the sign-in's rule (FR-045a); `POST /v1/ops/auth/sign-out-everywhere` ends every session of the account and writes `session_ended`; each needs a session
- [X] T043 [US8] Create `Operators/Infrastructure/OperatorProfile.cs`, the two-step change in `OperatorPasswords.cs` (a challenge of purpose `password-change` holding the new password's hash, research R8a), `Operators/Api/OperatorProfileEndpoints.cs` with the four password addresses and `POST /v1/ops/auth/sign-out-everywhere`; T042 passes
- [X] T044 [US8] Create `ops/profile/profile.page.ts` as frames `1849:2791`/`1850:2824` and `2185:567`/`2185:460` — the two cards, the switch on and locked with its line, «الجلسات الأخيرة» with the browser and system over the address, «نشط الآن» and «الجلسة الحالية» or «انتهت منذ ‹مدة›», the "one session" line, «تسجيل الخروج من كل الأجهزة» with its confirmation, and loading, error and no-connection states
- [X] T045 [US8] Create `ops/profile/change-password.dialog.ts` as frames `1851:2003`, `1852:2042`, `1852:2169` and their twins, with the three errors of the note `2185:674` under their fields, the hint turning red when the rules are not met, the code step that follows a right current password (the six boxes, the countdown and "send again" of the code screen; closing abandons the change), and the success state after the right code; it closes with the sign-in screen when the session ends
- [X] T046 [US8] Write `src/web/e2e/ops-profile.spec.ts`: the page and the dialog in both languages, each error, the success state, sign out of all devices, the keyboard alone, and pictures of each state

---

## Phase 12: User Story 9 - The console has a frame, and shows only what exists (P3)

**Goal**: one frame for every page after sign-in.

**Independent Test**: the frame in Arabic and English at desktop and narrow widths; the account menu; a page that does not exist.

- [X] T047 [US9] Complete `ops/shell/ops-shell.component.ts`, `account-menu.component.ts` and `sections.ts` as "Navbar" (`278:2`), "Sidebar Nav Item" (`8:40`) and the account menu (`1844:2699`/`1844:2875`): the mark and «SCS — لوحة التشغيل», the name with the role under it shortened when long, the initials opening a menu with «الملف الشخصي» and «تسجيل الخروج» that closes on Escape and a press elsewhere and works with the keyboard; a side bar that reads its sections from a list later specs add to, lists «الرئيسية» only, marks the page shown, sits on the right in Arabic and the left in English, and goes behind a control under 1024 wide; no bell; and the frame's states for any page inside it — loading (skeleton), error with "try again", no connection, and "not available yet" (which the privacy link shows until spec 086)
- [X] T048 [P] [US9] Complete `ops/home/home.page.ts` (a greeting by name and one line) and create `ops/home/not-found.page.ts` inside the frame with a way home
- [X] T049 [US9] Write `src/web/e2e/ops-shell.spec.ts`: the frame in both languages and directions, the menu by pointer and by keyboard, the narrow width with no sideways scrolling, a long name, the not-found page, and pictures

---

## Phase 13: User Story 10 - Outer defences and complete screens (P3)

**Goal**: an address that asks too often is slowed first; every screen before sign-in is whole.

**Independent Test**: eleven requests in a minute from one address; each screen in both languages, with no connection, with the keyboard only.

- [X] T050 [P] [US10] Write failing `tests/Scs.IntegrationTests/Operators/RequestLimitTests.cs`: the eleventh request in a minute from one address to the sign-in, code, forgot and link addresses is `RATE_LIMITED` with `retryAfterSeconds`, checked before any account so no count of wrong tries changes; every address under `/v1/ops/` that changes something refuses a request without the platform's request header or from another origin, with or without a session (FR-039a); the blocked-address check runs first and its place is kept
- [X] T051 [US10] Apply the platform's request limit and blocked-address guard to the anonymous routes under `/v1/ops/`; T050 passes
- [X] T052 [US10] Complete every screen before sign-in: «تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.» with what was typed kept, the too-many-requests message, «سياسة الخصوصية» in the footer, the language control whose choice is kept for the next visit (last chosen, else the browser's, else Arabic) and the account's language after sign-in, digits following the language, the reading order for the keyboard and errors announced; cases in `src/web/e2e/ops-sign-in.spec.ts` and `ops-password.spec.ts`

---

## Phase 14: Polish & Cross-Cutting Concerns

- [X] T053 [P] Create `Operators/Infrastructure/OperatorsCleanupJob.cs` (daily: spent or expired challenges and links after a day, ended sessions after ninety days, known browsers not seen for four hundred days, `SignInRecord` rows older than `SCS_OPS__RECORD_MONTHS`, twelve by default — FR-052a) with its registration and `tests/Scs.IntegrationTests/Operators/CleanupTests.cs`
- [X] T054 [P] Write `tests/Scs.IntegrationTests/Operators/OperatorSecretsTests.cs`: after every scenario of the suite has run, no password, code, link token, renewal secret or email address is found in the captured logs, and none of the first four in any row of `ops.*` (FR-053, SC-008); and unit tests in `tests/Scs.UnitTests/Operators/` for the four emails — one language each, the platform's name, the code or link once, no link in the code's email
- [X] T055 [P] Update `docs/adding-a-module.md` (a route under `/v1/ops/`: its declaration, `IOperatorAccess`, the background header, adding a section to the side bar) and the tests that list every route's declaration and every route's audit declaration for the new routes
- [X] T056 Run every gate with nothing else running: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T057 Walk the real stack as `quickstart.md` "By hand": the thirteen steps, in Arabic and English, with the first operator invited by the command, reading the database and the API's log at the end
- [X] T058 Lay each screen beside its frame: `SCS_SHOTS=<folder> npx playwright test ops`, compare every screen and state in Arabic and English with the frames named in `contracts/operator-screens.md` — layout, sizes, colour, words — fix what differs, and save the Arabic sign-in beside frame `128:85` as `specs/010-operator-sign-in/design-vs-real.png`
- [X] T059 Write `specs/010-operator-sign-in/validation.md`: what was run with its counts against the baseline of T001, the measured times (the code's email, the single answer's timing, the idle sign-out), what was walked, what was compared with the design and what differs, what was built differently from the plan, what could not be reached until spec 011, and the wording left for the owner

---

## Dependencies & Execution Order

- **Setup (T001–T003)** then **Foundational (T004–T013)**: block everything. T004 before T015; T006 before T007; T008 before T009.
- **US1 (T014–T018)**: needs the foundation. Everything after needs US1's sign-in.
- **US2 (T019–T021)** and **US3 (T022–T024)**: after US1; independent of each other, but both edit `OperatorSignIn.cs` and `sign-in.page.ts`, so in turn.
- **US5a (T025–T028)**: after the foundation; its screen and links are independent of US1, its last step (signing in) needs US1.
- **US4 (T029–T031)**: after US1. **US5 (T032–T034)**: after US5a (the link machinery) and US3 (the lock it lifts).
- **US6 (T035–T039)**: after US1; T035's suspended and reset cases need US5. **US7 (T040–T041)**: after US6.
- **US8 (T042–T046)**: after US6 (sessions to list) and US3 (the wrong-try count).
- **US9 (T047–T049)**: after US1's first shell; before US8's page is laid beside the design.
- **US10 (T050–T052)**: after US1 and US5.
- **Polish (T053–T059)**: last; T056 → T057 → T058 → T059 in order.

### Parallel opportunities

- T003, T005, T010, T011, T012, T013 once T002 is done.
- Each story's test file (T014, T019, T022, T025, T029, T032, T035, T036, T042, T050) can be written while the previous story's console work is finished.
- Server and console tasks of one story touch different trees and can run side by side once the story's addresses exist.
- T053, T054, T055 together.

## Implementation Strategy

1. **MVP**: Setup, Foundational, US1, US2, US3 and US5a — the first operator is invited, chooses a password, signs in with a code, and guessing is stopped. Stop and check on the real stack.
2. Then US4 and US5 (the code's limits; forgot password), US6 and US7 (sessions), US8 (the profile).
3. Then US9 and US10 (the finished frame; outer defences and whole screens), and the polish phase with the gates, the walk, the design comparison and `validation.md`.

## Notes

- Requirements → tasks: FR-001, FR-002 T006 · FR-003 T025, T026 · FR-004, FR-004a T025–T027 · FR-005–FR-007 T014–T018 · FR-008 T014, T042 · FR-009 T015, T050 · FR-010–FR-012 T019–T021 · FR-013 T029 · FR-015–FR-019 T022–T024, T042 · FR-020–FR-024, FR-020a T029–T031, T054 · FR-025–FR-029 T025, T032–T034 · FR-030–FR-033 T035, T037, T038 · FR-033a T036 · FR-034 T040, T041 · FR-035, FR-036 T035, T038, T039 · FR-037 T008, T035 · FR-038 T006, T035 · FR-039, FR-040 T008, T009 · FR-041–FR-046 T042–T046 · FR-047–FR-051 T047–T049 · FR-052 T011 and each story's tests · FR-053 T054 · FR-054–FR-056 T013, T052, T054.
- Success criteria: SC-001 T057 · SC-002 T057, T059 · SC-003 T019 · SC-004 T022 · SC-005, SC-005a T035, T039, T057 · SC-005b T036 · SC-006 T035, T032, T042 · SC-007 T057 · SC-008 T054 · SC-009 T058 · SC-010 T046, T052 · SC-011 T008, T035.
- Cannot be reached through the product until spec 011, and tested by setting the state directly: a suspended operator; a changed role (its reason and line are kept).
- After the security checklist (12 Oct 2026): withdrawing an invitation FR-003a (T025, T026); the code before a password change FR-045a (T042–T045); forged requests FR-039a (T050, T051); twelve months for records FR-052a (T053); the lock by a stranger and the single mailbox are accepted and stated.
- After `/speckit-analyze` (12 Oct 2026): the lock after three spent codes is FR-020a (T029); the console learns of a session ended elsewhere within thirty seconds (FR-030: T038, T039); the privacy link's interim target is FR-056 (T047, T052).
- The implementation is not complete until T056 to T059 are done and written down (constitution VII).

## Phase 15: Convergence

- [X] T060 Make two sign-ins of one account that complete at the same moment leave one open session and send the other browser to the sign-in screen with the "another device" line, instead of failing on the one-open-session index: handle the conflict in `OperatorSessions.OpenAsync` (`src/server/modules/Operators/Scs.Operators/Infrastructure/OperatorSessions.cs`) and hold it with an integration test of two concurrent code confirmations in `src/server/tests/Scs.IntegrationTests/Operators/OperatorPasswordAndSessionTests.cs` per FR-030, Edge Case "Two sign-ins complete at the same moment" (partial)
- [X] T061 Before sign-in in the operators console, fall back to the browser's language when none was chosen in that browser, and to Arabic only after that — in `src/web/projects/ops/` (the clinic's rule in `core` stays as it is) — with a unit test in `src/web/projects/ops/src/app/ops.spec.ts` and a browser test with an English browser and nothing stored in `src/web/e2e/ops-sign-in.spec.ts` per FR-054 (partial)
- [X] T062 Record a line in `ops.SignInRecord` when the deployment command is run for an account that is already active or suspended and changes nothing, so that every use of the operation is recorded: `OperatorInvite.InviteAsync` and `WithdrawAsync` refusals in `src/server/modules/Operators/Scs.Operators/Infrastructure/OperatorInvite.cs`, a new kind in `Domain/SignInRows.cs`, and the assertion in the invitation tests per FR-003, FR-052 (partial)
- [X] T063 Measure the answers that must not be told apart over a hundred tries each — an unknown email, a wrong password, and a wrong password for a locked, a suspended and an invited account — and assert their times overlap, in `src/server/tests/Scs.IntegrationTests/Operators/OperatorSignInTests.cs`; record the figures in `validation.md` (today: four tries each, two of the five cases) per SC-003 (partial)
- [X] T064 Lay every English screen beside its English frame in the operators design file (`128:54`, `134:15`, `135:38`, `141:38`, `158:38`, `167:38`, and the English twins on page `1844:70`), open each pair, fix what differs or record it for the owner, and add the English rows to `specs/010-operator-sign-in/design-vs-real.png` per SC-009, T058 (partial)
- [X] T065 Open, picture and hold in `src/web/e2e/ops-look.spec.ts` the two states of the frame that no test opens today: no connection (the line of `ops-offline`) and the profile while loading, in Arabic and English, wide and narrow per FR-051 (partial)
- [X] T066 Add unit tests in `src/web/projects/ui/` for what the look pass added to the shared library — the field without its leading mark, the banner that can be closed, the dialog with its heading in the middle, a width and no heading, and the panel of the auth frame that T012 asked for — so the clinic's defaults are held where they are defined per T012, plan: shared libraries change only additively (partial)
- [X] T067 Review the rule that wrong tries older than the attempt window are forgotten (`OperatorSignIn.CountWrongPasswordAsync`, test `Wrong_tries_older_than_the_window_are_forgotten`): the spec states no window for operators — either write it into `spec.md` by the owner's decision as the clinic's rule, or remove it; and confirm SC-004 still holds with it per FR-015, SC-004 (unrequested)
- [X] T068 After T060–T067, run every gate again on the final code — `dotnet test src/server/Scs.sln` (not run since before the look pass), `npm --prefix src/web run ci`, `npx playwright test` — and bring `validation.md` up to date with the counts and with what these tasks changed per Constitution VII, T056, T059 (partial)
