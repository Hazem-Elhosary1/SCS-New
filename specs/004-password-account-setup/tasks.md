---

description: "Task list for 004-password-account-setup"
---

# Tasks: Passwords and Account Activation

**Input**: Design documents from `/specs/004-password-account-setup/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 003 implemented locally and Docker running: every test that touches accounts, links or sessions needs a real SQL Server.

**Tests**: Included, and first. The single answer and its timing, the caps, link life and one-time use, and the temporary-password gate are test-first areas (constitution VII): each test task is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere. Mail is captured by `FakeEmailSender`; time is moved with `MovableClock` (both from spec 003).

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US7)
- Paths are relative to the repository root

## Path Conventions

- Module: `src/server/modules/Identity/Scs.Identity/` (`Domain/`, `Infrastructure/`, `Api/`) and `Scs.Identity.Contracts/`
- Shared server code: `src/server/shared/Scs.BuildingBlocks/` (`Security/`, `Configuration/`)
- Tests: `src/server/tests/Scs.IntegrationTests/Identity/`, `Scs.UnitTests/Identity/`, `Scs.ArchitectureTests/`
- Web: screens in `src/web/projects/clinic/src/app/password/`, shared code in `src/web/projects/core/src/lib/auth/`, components in `src/web/projects/ui/src/lib/`, browser tests in `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Settings and error codes the feature needs.

- [X] T001 Confirm the branch is `004-password-account-setup` from `develop` with spec 003 merged, and that `dotnet test src/server/Scs.sln` (Debug) and `npm --prefix src/web run ci` pass before any change
- [X] T002 Write cases in `src/server/tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs`, then add to `AuthOptions` in `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` the seven settings of data-model.md "Settings" with their defaults and limits (reset link 30 min, 5–1440; invite link 72 h, 1–336; temporary password 72 h, 1–336; reset email gap 60 s, 15–3600; reset emails per hour 5, 1–60; generated password length 14, 12–64; `StaffAppUrl` required, absolute `http(s)` address with no path, `https` required outside local and test), each failure naming its variable; set their values in `src/server/tests/Scs.IntegrationTests/Fixtures/ApiFactory.cs`
- [X] T003 [P] Add the seven variables to `deploy/compose/.env.example` (`SCS_AUTH__RESET_LINK_MINUTES`, `__INVITE_LINK_HOURS`, `__TEMP_PASSWORD_HOURS`, `__RESET_EMAIL_GAP_SECONDS`, `__RESET_EMAILS_PER_HOUR`, `__GENERATED_PASSWORD_LENGTH`, `SCS_AUTH__STAFF_APP_URL=http://localhost:4201`) and to the configuration table in `specs/001-platform-foundation/data-model.md`
- [X] T004 [P] Add `LINK_INVALID` (410), `PASSWORD_CHANGE_REQUIRED` (403) and `PASSWORD_CHANGE_NOT_REQUIRED` (409) to `src/server/shared/Scs.SharedKernel/ErrorCodes.cs` with their status mapping, to the code list in `specs/001-platform-foundation/contracts/conventions.md`, and to the Arabic and English error texts in `src/web/projects/clinic/public/i18n/ar.json` and `en.json`; update the test that counts the codes (27)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tables, the token, the one routine that sets a password, and the client pieces every story uses.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add domain types in `src/server/modules/Identity/Scs.Identity/Domain/PasswordLink.cs`: `PasswordLink` (`Id` random GUID; `ClinicId`; `AccountId`; `Kind` `reset` or `new-account`; `SecretHash` 32 bytes `[Secret]`; `CreatedAtUtc`; `ExpiresAtUtc`; `UsedAtUtc` nullable; `ReplacedAtUtc` nullable; `IsLive(now)`) and `AccountEmailRequest` (`Id`; `ClinicId`; `AccountId`; `Kind` `reset-link` or `password-changed`; `RequestedAtUtc`; `HandledAtUtc` nullable; `Outcome` nullable: `sent`, `skipped-limit`, `skipped-state`, `gave-up`); add `MustChangePassword` and `TemporaryPasswordExpiresAtUtc` to `Domain/StaffAccount.cs` with methods that set and clear them together; add the five event kinds (`reset-requested`, `invitation-accepted`, `temp-password-changed` as activity; `reset-completed`, `access-reissued` as security) and nullable `ActorAccountId` to `Domain/SignInRecords.cs`
- [X] T006 Map them in `src/server/modules/Identity/Scs.Identity/Infrastructure/Persistence/IdentityDbContext.cs` and add one migration `PasswordLinksAndTemporaryPasswords` in `Infrastructure/Migrations/`: tables `identity.PasswordLink` (`Kind varchar(12)` with check; `SecretHash binary(32)`; filtered unique index on `AccountId` where `UsedAtUtc IS NULL AND ReplacedAtUtc IS NULL`; index `(AccountId, Kind, CreatedAtUtc)`; FK to `StaffAccount`) and `identity.AccountEmailRequest` (`Kind varchar(20)` with check; `Outcome varchar(16)`; index on `ClinicId` filtered to `HandledAtUtc IS NULL`), both with the isolation policy of spec 002 added in this migration; `StaffAccount.MustChangePassword bit NOT NULL DEFAULT 0`, `TemporaryPasswordExpiresAtUtc datetimeoffset NULL`, check `MustChangePassword = 1 OR TemporaryPasswordExpiresAtUtc IS NULL`, check `MustChangePassword = 0 OR State = 'active'`; `SignInEvent.ActorAccountId uniqueidentifier NULL`; grants for `scs_app` as for the other identity tables
- [X] T007 [P] Add isolation samples for `PasswordLink` and `AccountEmailRequest` to `src/server/tests/Scs.IntegrationTests/Isolation/IdentitySamples.cs` and confirm the isolation suite and the guardrail of spec 002 pass with `DeclaredDatabaseCrossings` unchanged
- [X] T008 [P] Write `src/server/tests/Scs.UnitTests/Identity/PasswordLinkTokenTests.cs` (round trip of kind, clinic, link id and 32-byte secret; an altered, truncated or foreign-purpose token does not open; over 1024 characters refused without work), then implement `PasswordLinkTokens` in `src/server/shared/Scs.BuildingBlocks/Security/PasswordLinkTokens.cs` (beside `SessionCookies`; add the file to the allow-list of the architecture test that limits who creates a protector) with data protection purpose `Scs.PasswordLink.v1`, base64url output, and `SHA-256` of the secret compared in constant time (research R1, R2)
- [X] T009 [P] Write `src/server/tests/Scs.UnitTests/Identity/PasswordGeneratorTests.cs` (length from the setting; at least one upper, lower, digit, symbol; never `0 O o 1 l I |`; passes `PasswordRules`; 10,000 draws all different), then implement `PasswordGenerator` in `src/server/modules/Identity/Scs.Identity/Infrastructure/PasswordGenerator.cs` using `RandomNumberGenerator` (research R10)
- [X] T010 Write `src/server/tests/Scs.IntegrationTests/Identity/PasswordSetterTests.cs` (new hash verifies and the old does not; `LockedUntilUtc`, `FailedAttempts`, `SpentCodes` cleared; every live link of the account replaced; an `invited` account becomes `active`; the mark and its expiry set or cleared as asked; all sessions ended, or all but a given one; a password equal to the current one refused with field code `SAME_AS_CURRENT` and nothing changed; a password breaking a rule refused with one code per unmet rule; the event written with its kind, detail and actor; all in one transaction — a forced failure after the hash leaves nothing changed; most of these are asserted through the endpoints in `PasswordResetTests`, `ActivationTests`, `TemporaryPasswordTests` and `AccountHandoverTests`, the forced failure and two routes at once in `PasswordSetterTests`), then implement `PasswordSetter` in `src/server/modules/Identity/Scs.Identity/Infrastructure/PasswordSetter.cs` (research R9); add an internal "end all but this session" to `Infrastructure/SessionService.cs`
- [X] T011 Cover the link store (in `PasswordLinkLifeTests`, `ResetEmailLimitTests` and `ActivationTests` of `src/server/tests/Scs.IntegrationTests/Identity/`, through the endpoints and the job rather than in a file of its own) (issuing replaces the earlier live link of either kind in the same transaction; two issues at the same moment leave one live link; `ExpiresAtUtc` is created + 30 minutes for `reset` and + 72 hours for `new-account` from the settings; removing a link whose email failed), then implement `PasswordLinks` in `src/server/modules/Identity/Scs.Identity/Infrastructure/PasswordLinks.cs` (send-then-store under an application lock named after the account; inspect by token; consume with the single `UPDATE` of research R5; replace all for an account)
- [X] T012 [P] Add fixtures to `src/server/tests/Scs.IntegrationTests/Fixtures/SignInFixtures.cs`: (in `Fixtures/PasswordFixtures.cs`) `LatestLinkTokenFor(email)` that extracts the token from the captured email's `#t=` address; `RunAccountEmailJobAsync(clinicId)` that runs the job through `ClinicJobRunner`; `ResetTokenForAsync`; `HandOverAsync` for the hand-over operations; a per-address mail failure on `FakeEmailSender`
- [X] T013 [P] Write `src/web/projects/core/src/lib/auth/password-rules.spec.ts`, then implement `checkPassword(text)` in `src/web/projects/core/src/lib/auth/password-rules.ts` returning the four rule results in the fixed order (at least 10 characters; an upper- and a lower-case letter; a digit; a symbol = any character that is not a letter or digit) plus "too long" over 200 — the same cases as the server's `PasswordRules` unit tests, including Arabic letters, spaces and emoji
- [X] T014 [P] Write `src/web/projects/ui/src/lib/password-rules/password-rules.component.spec.ts`, then build `scs-password-rules` in `src/web/projects/ui/src/lib/password-rules/password-rules.component.ts` (inputs: four already-translated labels, the rule results, `submitted`; each line neutral, met or unmet; state given to assistive technology by text, not colour alone; `aria-live="polite"`); export it from `src/web/projects/ui/src/public-api.ts`
- [X] T015 Extend `src/web/projects/core/src/lib/auth/auth.service.ts` and its spec: `requestPasswordReset(email)`, `inspectLink(token)`, `setPasswordWithLink(token, password)`, `changeTemporaryPassword(password)`, and `mustChangePassword` read from `GET /v1/auth/session`; map `410 LINK_INVALID` to a typed result carrying `kind`

**Checkpoint**: tables, token, setter and client pieces exist; stories can start.

---

## Phase 3: User Story 1 - A person who forgot their password gets back in by themselves (Priority: P1) 🎯 MVP

**Goal**: ask for a reset link, open it, set a new password, sign in.

**Independent Test**: quickstart scenario 1.

### Tests for User Story 1 ⚠️ write first, see them fail

- [X] T016 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Identity/PasswordResetTests.cs`: asking for an active account answers 202 with an empty body and, after the job runs, one email whose address is `<StaffAppUrl>/set-password#t=…`; inspecting answers `kind: reset` and the email and no clinic name; setting a valid password answers 200, sets no cookie, opens no session; the new password signs in with both steps and the old one gets `INVALID_CREDENTIALS`; a locked account signs in at once afterwards; sessions on two other browsers are refused with `SESSION_ENDED` at their next request; the link no longer works; a `password-changed` request is queued and its email sent by the job, containing no link and no password; events `reset-requested` and `reset-completed` are written; an empty or malformed email answers 400 on field `email`; when the notice cannot be sent the reset still stands; one account's failed email does not stop another account's link in the same job run
- [X] T017 [P] [US1] Write `src/web/e2e/password-reset.spec.ts` on the intercepted platform of `src/web/e2e/staff-platform.ts` (extended with answers for forgot, link, link/set and change — the browser tests of this project never need a running server): from the sign-in form through «نسيت كلمة المرور؟», the request form, «أُرسل الطلب», opening `/set-password#t=…`, the reset form, the success banner «غُيّرت كلمة المرور» on the sign-in form, and signing in with the new password; the address bar holds no token after the link opens; «الرجوع لتسجيل الدخول» on both forms

### Implementation for User Story 1

- [X] T018 [US1] Implement `ForgotPasswordService` in `src/server/modules/Identity/Scs.Identity/Infrastructure/ForgotPasswordService.cs`: validate the email's shape; find the account with the existing `usp_FindAccountForSignIn`; when found, establish its clinic, write `reset-requested`, and when `active` insert an `AccountEmailRequest` of kind `reset-link` and enqueue `AccountEmailJob` for the clinic (research R6)
- [X] T019 [US1] Implement `AccountEmailJob` (`IClinicJob`) in `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountEmailJob.cs`: take the clinic's waiting requests; for `reset-link`, once per account under a lock on the account row, issue a link and send the email, removing the link and failing the job if the email cannot be handed over, giving up on requests older than 10 minutes; for `password-changed`, send the notice; set `HandledAtUtc` and `Outcome`; idempotent (research R7)
- [X] T020 [P] [US1] Add the reset-link and password-changed messages to `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountEmails.cs`, built like `CodeEmail.cs` (Arabic and English together, text and HTML): the reset one names the action «إعادة تعيين كلمة المرور», carries the address and the 30-minute limit; the notice says the password was changed, when, and to contact support if it was not them; neither names the clinic
- [X] T021 [US1] Add `src/server/modules/Identity/Scs.Identity/Api/PasswordEndpoints.cs` with `POST /v1/auth/password/forgot`, `POST /v1/auth/password/link` and `POST /v1/auth/password/link/set` per contracts/password-api.openapi.yaml (no session; the sign-in rate-limit policy; the token and password read from the body only; `set` checks in the order link → rules → differs, uses `PasswordSetter` with reason `password-reset`, queues `password-changed`); map them in `IdentityModule.cs` and register the new services
- [X] T022 [US1] Build `src/web/projects/clinic/src/app/password/forgot-password.page.ts` (states 1–4 and 6 of contracts/password-screens.md: validation under the field before any call, busy button, result screen with the typed email, unreachable banner) and replace the placeholder route `forgot-password` in `src/web/projects/clinic/src/app/app.routes.ts`
- [X] T023 [US1] Build `src/web/projects/clinic/src/app/password/set-password.page.ts` for the reset form (states 7, 8, 12): read the token from the fragment once, remove it from the address with `history.replaceState`, keep it in memory, inspect, show the email and one `scs-text-field` of type password with `scs-password-rules`; on success hand the notice and the email to the sign-in form in memory (`SignInFlow.announce`) and navigate to `/sign-in`; add route `set-password`
- [X] T024 [US1] Show the success banner «غُيّرت كلمة المرور» / «الدخول الآن بكلمة المرور الجديدة.» and prefill the email from that notice in `src/web/projects/clinic/src/app/sign-in/sign-in.page.ts`; add every Arabic and English text of states 1–8 and 17 to `src/web/projects/clinic/public/i18n/ar.json` and `en.json`

**Checkpoint**: a forgotten password is recovered end to end.

---

## Phase 4: User Story 2 - Asking for a link never reveals whether an account exists (Priority: P1)

**Goal**: one answer, one timing, mail only to an active account within the caps.

**Independent Test**: quickstart scenario 2.

### Tests for User Story 2 ⚠️ write first, see them fail

- [X] T025 [P] [US2] Write `src/server/tests/Scs.IntegrationTests/Identity/ForgotPasswordAnswerTests.cs` on the harness of `SingleAnswerTests`: for an unknown address, a disabled account, an invited account, an active account, an active account asked again within a minute, and an active account with the mail sender failing — status, headers (apart from the trace reference) and body are identical, and the mean response times over 1,000 requests per case differ by under 5%; no email for the first three; no answer is sooner than the floor
- [X] T026 [P] [US2] Write `src/server/tests/Scs.IntegrationTests/Identity/ResetEmailLimitTests.cs` with `MovableClock`: a second request within 60 seconds sends nothing and leaves the first link working; the sixth within an hour sends nothing and the fifth link still works; after the hour passes one is sent again; ten requests at the same moment produce one email and one live link; several waiting requests for one account are handled as one; the 11th request in a minute from one address answers 429 `RATE_LIMITED` with `Retry-After`

### Implementation for User Story 2

- [X] T027 [US2] Apply the fixed floor to every answer of `POST /v1/auth/password/forgot` in `src/server/modules/Identity/Scs.Identity/Api/PasswordEndpoints.cs`, sharing the constant with `SignInService.FailureFloor`, and make sure no code path sends mail or reads more for one case than another beyond what research R6 lists
- [X] T028 [US2] Enforce the two caps in `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountEmailJob.cs`, counted over `PasswordLink` rows of kind `reset` for the account (gap from `ResetEmailGapSeconds`, hourly from `ResetEmailsPerHour`), with outcome `skipped-limit` and the live link untouched; outcome `skipped-state` when the account is no longer `active`
- [X] T029 [US2] Show the too-many-requests warning with the wait (state 5) on `src/web/projects/clinic/src/app/password/forgot-password.page.ts` using `ScsProblemError.retryAfterSeconds`, with its texts in `ar.json` and `en.json`

**Checkpoint**: the request form is safe to expose.

---

## Phase 5: User Story 3 - A new team member activates their account from a link (Priority: P1)

**Goal**: create an invited account with a link, open it, choose a password, sign in.

**Independent Test**: quickstart scenario 3.

### Tests for User Story 3 ⚠️ write first, see them fail

- [X] T030 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Identity/ActivationTests.cs`: `CreateWithLinkAsync` creates an `invited` account and sends one email with the link, the clinic's name, the sign-in address and the 72-hour limit; inspecting answers `kind: new-account`, the email and both clinic names; setting a password makes the account `active`, writes `invitation-accepted`, opens no session, and both sign-in steps then work; `ResendInvitationAsync` sends a new link and the earlier one answers `LINK_INVALID`; "forgot password" for an invited account sends nothing; `emailSent` is false and no link is left when the mail sender fails; the email is refused with `TAKEN` when any clinic holds it; a link inspected and used by a browser signed in as another account works, and that session stays alive
- [X] T031 [P] [US3] Write `src/web/e2e/activation.spec.ts` on the intercepted platform: a new-account link opened (also while signed in as another account — the session is neither used nor ended), the new-account form with «مرحبًا بك في عيادة الشفاء», the 72-hour note, «تفعيل الحساب», the banner «فُعِّل الحساب», and signing in

### Implementation for User Story 3

- [X] T032 [US3] Add `IAccountHandover` and its result records to `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs` per contracts/account-handover.md, and implement `CreateWithLinkAsync` and `ResendInvitationAsync` in `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountHandover.cs` (email sent inside the call; on failure the link is removed and `emailSent` is false); register it in `IdentityModule.cs`
- [X] T033 [P] [US3] Add the new-account-link message to `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountEmails.cs`: the clinic's names from `IClinicProfile`, the address, the sign-in address, the 72-hour limit
- [X] T034 [US3] Make `POST /v1/auth/password/link` return the clinic's names for `new-account`, and `POST /v1/auth/password/link/set` activate an `invited` account through `PasswordSetter` (no "differs" check: there is no current password) in `src/server/modules/Identity/Scs.Identity/Api/PasswordEndpoints.cs`
- [X] T035 [US3] Add the new-account form (state 9) to `src/web/projects/clinic/src/app/password/set-password.page.ts`: the clinic's name in the current language, the note «الرابط صالح ٧٢ ساعة من إرساله.», button «تفعيل الحساب», then navigation to `/sign-in` with notice `account-activated`; show that banner (state 18) in `sign-in.page.ts`; texts in `ar.json` and `en.json`
- [X] T036 [US3] Add `POST /v1/dev/staff-invitations` to `src/server/modules/Identity/Scs.Identity/Api/PasswordEndpoints.cs`, mapped only when the environment is `local` (finds the invited account by email through the sign-in lookup and calls `ResendInvitationAsync`; always answers 204), with a test that it does not exist in `test` and `production`, and a line in `docs/setup.md`. (The migrator has no mail sender and no key ring, so the planned migrator verb was replaced by this.)

**Checkpoint**: an invitation becomes a working account.

---

## Phase 6: User Story 4 - A link that no longer works says so, and says what to do (Priority: P2)

**Goal**: the right "no longer works" screen for every dead link; nothing can be set with one.

**Independent Test**: quickstart scenario 4.

### Tests for User Story 4 ⚠️ write first, see them fail

- [X] T037 [P] [US4] Write `src/server/tests/Scs.IntegrationTests/Identity/PasswordLinkLifeTests.cs` with `MovableClock`, for inspect and for set: a reset link at 30 minutes and one second; a new-account link at 72 hours and one second; a used link; a link replaced by a newer one; a link after the password was set another way; a link whose account was disabled; a new-account link whose account was removed — each answers 410 `LINK_INVALID` with the right `kind` and changes nothing; an altered token, a made-up token and an empty token answer `kind: reset`; the body never says why; two submissions of one link at the same moment set the password once and the other gets 410; a link live at inspection and expired at submission gets 410; a reset link stays live after a successful sign-in with the old password; a reset link of a suspended clinic can still set the password

### Implementation for User Story 4

- [X] T038 [US4] Complete the dead-link answer in `src/server/modules/Identity/Scs.Identity/Infrastructure/PasswordLinks.cs` and `Api/PasswordEndpoints.cs`: one problem shape with `kind` taken from the token (or `reset` when it does not open); the account-state check (`active` for reset, `invited` for new-account); replace the account's live links in `StaffAccounts.SetStateAsync` when disabling (`src/server/modules/Identity/Scs.Identity/Infrastructure/StaffAccounts.cs`)
- [X] T039 [US4] Add the two dead-link screens (states 13, 14) to `src/web/projects/clinic/src/app/password/set-password.page.ts`: shown for a 410 at inspection or on submit, for a missing fragment, and after a reload; «طلب رابط جديد» to `/forgot-password` on the reset one only; «الرجوع لتسجيل الدخول» on both; focus on the title; texts in `ar.json` and `en.json`
- [X] T040 [P] [US4] Add dead-link cases to `src/web/e2e/password-reset.spec.ts` and `src/web/e2e/activation.spec.ts`: used link, replaced link, `#t=nonsense`, no fragment, reload after opening

**Checkpoint**: no dead end on any link.

---

## Phase 7: User Story 5 - The password rules are shown, and enforced by the server (Priority: P2)

**Goal**: four lines that react while typing; the server refuses what the screen would.

**Independent Test**: quickstart scenario 5.

### Tests for User Story 5 ⚠️ write first, see them fail

- [X] T041 [P] [US5] Write `src/server/tests/Scs.IntegrationTests/Identity/PasswordRulesEndpointTests.cs`: sent straight to `link/set` (the same cases for `password/change` are in T045), a 9-character password, one with no upper or no lower case, no digit, no symbol, 201 characters, and several rules at once answer 400 with exactly the codes of the contract; the current password answers `SAME_AS_CURRENT` alone; in every case the link stays live and the hash is unchanged; Arabic letters, spaces and emoji are accepted when the four rules are met
- [X] T042 [P] [US5] Extend `src/server/tests/Scs.UnitTests/Identity/IdentityRulesTests.cs` so the server's `PasswordRules` and the web `password-rules.spec.ts` run the same table of cases (keep the table in both files identical and say so in a comment)

### Implementation for User Story 5

- [X] T043 [US5] Finish rule behaviour on the reset and new-account forms in `src/web/projects/clinic/src/app/password/set-password.page.ts` (states 10, 11): lines neutral before typing, green as typed, red on submit; no call when a rule is unmet; no second error under the field and the button stays enabled; the server's rule codes mapped onto the same lines; `SAME_AS_CURRENT` shown as its own message under the field; exactly one password field with the eye; texts in `ar.json` and `en.json`
- [X] T044 [P] [US5] Add rule cases to `src/web/e2e/password-reset.spec.ts`: each rule unmet in turn, the current password, and that the link still works afterwards

**Checkpoint**: rules are the same on both sides.

---

## Phase 8: User Story 6 - A temporary password must be replaced at first sign-in (Priority: P2)

**Goal**: accounts handed over with a password are held on one screen until they choose their own; access can be re-issued.

**Independent Test**: quickstart scenarios 6 and 7.

### Tests for User Story 6 ⚠️ write first, see them fail

- [X] T045 [P] [US6] Write `src/server/tests/Scs.IntegrationTests/Identity/TemporaryPasswordTests.cs`: after both sign-in steps `GET /v1/auth/session` answers `mustChangePassword: true`; `POST /v1/auth/password/change` with a valid different password answers 204, keeps the same session, ends the account's other sessions, clears the mark and expiry, writes `temp-password-changed`; the temporary password itself answers `SAME_AS_CURRENT` and the mark stays; a password breaking a rule answers 400 with the rule codes; an unmarked account gets 409 `PASSWORD_CHANGE_NOT_REQUIRED`; at the next sign-in the field is false; a temporary password older than 72 hours gets `INVALID_CREDENTIALS` in the same time as a wrong one and counts toward the lock; a person already signed in when the 72 hours pass can still change; completing a reset link clears the mark
- [X] T046 [P] [US6] Write `src/server/tests/Scs.IntegrationTests/Identity/TemporaryPasswordGateTests.cs`: enumerate every mapped endpoint of the API; with a must-change session each one that needs a session answers 403 `PASSWORD_CHANGE_REQUIRED` except `GET /v1/auth/session` and `POST /v1/auth/password/change`; renewal and sign-out still work; endpoints that need no session are unaffected; an ordinary session is never refused by the gate. The same test asserts that only those two endpoints carry the mark (it needs the running application's endpoint list, which the architecture test project does not host)
- [X] T047 [P] [US6] Write `src/server/tests/Scs.IntegrationTests/Identity/AccountHandoverTests.cs`: `CreateWithGeneratedPasswordAsync` returns a password that signs in, meets the rules and appears in no table, log or email; `CreateWithTypedPasswordAsync` refuses a password that breaks a rule; both create `active` accounts marked "must change" with expiry + 72 hours and send the "account created" email with the sign-in address and no password; `ReissueAsync` by `generated` and `typed` replaces the password, ends sessions, cancels links, lifts a lock, marks, emails "access reset", writes `access-reissued` with the actor and method; by `link` sends a reset link (an invitation for an `invited` account) and the old password keeps working; by password on an `invited` account makes it `active`; re-issuing with the account's current password is accepted (no "differs" check); a link sent by re-issue ignores the caps but counts toward them; an account holding a temporary password can be disabled; a disabled account is refused with `ACCOUNT_DISABLED`; an account of another clinic is not found; `emailSent` false when the sender fails
- [X] T048 [P] [US6] Write `src/web/e2e/temporary-password.spec.ts` on the intercepted platform, with a session answering `mustChangePassword: true`: both sign-in steps lead to the temporary-password screen; opening `/` returns to it; the seed password is refused; a new one reaches the landing page; signing out is possible from the screen; the next sign-in goes straight in

### Implementation for User Story 6

- [X] T049 [US6] Change `IStaffSessionCheck` in `src/server/shared/Scs.BuildingBlocks/Security/StaffSessionAuthentication.cs` to return alive and must-change together; record must-change on the request; add `AllowsTemporaryPassword()` endpoint metadata and `UseScsTemporaryPasswordGate` that refuses unmarked session endpoints with `PASSWORD_CHANGE_REQUIRED`; place it right after authentication in `src/server/host/Scs.Api/Program.cs`; read the account's mark in the same query in `src/server/modules/Identity/Scs.Identity/Infrastructure/SessionService.cs` (research R11)
- [X] T050 [US6] In `src/server/modules/Identity/Scs.Identity/Infrastructure/SignInService.cs` treat a correct password whose `TemporaryPasswordExpiresAtUtc` has passed exactly as a wrong one (same answer, floor, counter and event); add `mustChangePassword` to the session answer and mark `GET /v1/auth/session` with `AllowsTemporaryPassword()` in `Api/AuthEndpoints.cs`
- [X] T051 [US6] Add `POST /v1/auth/password/change` to `src/server/modules/Identity/Scs.Identity/Api/PasswordEndpoints.cs` (session required; marked `AllowsTemporaryPassword()`; `PasswordSetter` keeping the current session)
- [X] T052 [US6] Implement `CreateWithGeneratedPasswordAsync`, `CreateWithTypedPasswordAsync` and `ReissueAsync` in `src/server/modules/Identity/Scs.Identity/Infrastructure/AccountHandover.cs`, and the "account created" and "access reset" messages in `Infrastructure/AccountEmails.cs` (sign-in address, no password)
- [X] T053 [US6] Seed `temp@noor.example` (active, must change, expiry pushed 72 hours ahead on every run) in `src/server/modules/Identity/Scs.Identity/IdentityModule.cs` `SeedAsync`, and list it in `docs/setup.md`
- [X] T054 [US6] In `src/web/projects/core/src/lib/auth/`: `sessionGuard` sends a must-change person to `/change-password` and anyone else away from it (inject before any `await`); the session interceptor navigates there on `PASSWORD_CHANGE_REQUIRED`; update their specs
- [X] T055 [US6] Build `src/web/projects/clinic/src/app/password/change-password.page.ts` (states 15, 16): the temporary-password title, the email, one field with the rules, the same-password message, a sign-out link; on success refresh the session and go to `/`; add route `change-password`; texts in `ar.json` and `en.json` (marked as awaiting the page review)

**Checkpoint**: all three hand-over ways work, and none leaves a shared password in use.

---

## Phase 9: User Story 7 - Every one of these screens is complete and bilingual (Priority: P3)

**Goal**: all 18 states in Arabic and English, in the sign-in frame.

**Independent Test**: quickstart scenario 8.

- [X] T056 [P] [US7] Write `src/web/e2e/password-states.spec.ts`: open each state of contracts/password-screens.md in Arabic and English (using route interception for refusals, 429 and unreachable); check the frame, direction, language switch, footer links, help window, focus placement, and that a clinic's name appears only in state 9
- [X] T057 [US7] Fix what T056 finds in `src/web/projects/clinic/src/app/password/*.page.ts`: loading, too-many-requests and unreachable states on the set and change forms (states 12, 16); mirrored layout; no blank page in any failure
- [X] T058 [P] [US7] Run the translation check over the new keys in `src/web/projects/clinic/public/i18n/ar.json` and `en.json` and add unit specs for the three pages' state mapping in `src/web/projects/clinic/src/app/password/`

**Checkpoint**: every state exists twice.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T059 [P] Extend the leak test in `src/server/tests/Scs.IntegrationTests/Identity/SignInLogTests.cs` to the new endpoints, the job and the hand-over operations: no password, token, link secret, generated password or email address in any captured log line or in the job server's tables; add to `password-reset.spec.ts` that `location.href` holds no token after a link opens
- [X] T060 [P] Isolation of links, in `PasswordLinkLifeTests` (`src/server/tests/Scs.IntegrationTests/Identity/ActivationTests.cs`): a token of a clinic-A account reads and changes nothing of clinic B; an altered token does not open (`PasswordLinkTokenTests`)
- [X] T061 [P] Extend `src/server/modules/Identity/Scs.Identity/Infrastructure/CleanupJob.cs` and `HousekeepingTests.cs`: remove `PasswordLink` rows older than 21 days (longer than any life a link can be configured to have) and handled `AccountEmailRequest` rows older than one day
- [X] T062 [P] Update `ops/runbooks/sign-in.md` (a person cannot get in: reset link, invitation again, re-issue; what each email looks like; the caps), `docs/setup.md` (new settings) and `.gitleaks.toml` if the sample values trip it
- [X] T063 Compare the built screens with the design file (clinic file, sign-in page: 7 reset states and 4 set-password states) and record differences in `specs/004-password-account-setup/validation.md` — needs the owner's permission to download the export (partly done on 10 Oct 2026 by reading the design file through the Figma connector, after the owner reported missing icons and mismatched states: the shared components were restyled to the design; a second pass the same day covered the remaining frames by eye — see validation.md)
- [X] T064 Run every scenario of `specs/004-password-account-setup/quickstart.md` against the local stack, in the browser, and record the results in `specs/004-password-account-setup/validation.md`, including the two timings of SC-001 and SC-008
- [ ] T065 Owner items: add to the BRD the five decisions of 10 Oct 2026 (different password, notice email, 72-hour temporary password, five reset emails an hour, re-issuing access); fix the wording of the temporary-password screen, the "different password" message and the three undrawn emails at the page review (B-O19); confirm the staff app's address per environment; update the BRD (CL-SH-088, B-5-2, B-5-3) and the design's code-step frames for the owner's decision of 10 Oct 2026 that sending the code again is a button with an icon, not a text link

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories.
- **US1** needs Phase 2 only. It is the MVP.
- **US2** needs US1 (the endpoint and the job it hardens).
- **US3** needs Phase 2 and the endpoints of US1 (T021) and the page of US1 (T023).
- **US4** needs US1 and US3 (both kinds of link).
- **US5** needs US1 (the form) — its server test for `password/change` waits for T051.
- **US6** needs Phase 2; `ReissueAsync` by link needs US1 and US3.
- **US7** needs every screen: US1, US3, US4, US6.
- **Polish** last.

### Within Each User Story

Tests first and failing; domain before services; services before endpoints; endpoints before pages.

### Parallel Opportunities

- Phase 1: T003, T004 together.
- Phase 2: T007, T008, T009, T012, T013, T014 together once T006 is in.
- In each story the test tasks marked [P] together, before implementation.
- After US1: US2 (server) and US3 can proceed side by side; US6's server tasks (T049–T053) touch different files from US4 and US5.

## Parallel Example: User Story 1

```text
T016 PasswordResetTests.cs        (server test)
T017 password-reset.spec.ts       (browser test)
T020 AccountEmails.cs             (messages)
```

## Implementation Strategy

### MVP First

Phases 1–3: a person who forgot their password recovers it. Then Phase 4 before anything is exposed, because the request form without the single answer breaks constitution III.

### Incremental Delivery

1. Setup + Foundational.
2. US1 → US2: forgot password, safe.
3. US3 → US4: invitations and dead links.
4. US5, US6: rules and temporary passwords.
5. US7, Polish: all states, leak test, runbook, validation.

---

## Phase 11: Convergence

- [X] T066 Extend the timing test in `src/server/tests/Scs.IntegrationTests/Identity/PasswordResetTests.cs` (`ForgotPasswordAnswerTests`) so the mean response time is compared across all six cases the criterion names — unknown, active, disabled, never activated, asked again within a minute, and mail failing — not only three of them per SC-002 (partial)
- [X] T067 Add unit specs for the state mapping of the three pages in `src/web/projects/clinic/src/app/password/` (`forgot-password.page.ts`: form → sent, field errors, banners; `set-password.page.ts`: loading → form / dead / failed and retry; `change-password.page.ts`: refusals and success), which today are covered only through `new-password.spec.ts` and the browser tests per T058 (partial)
- [X] T068 Bring `specs/004-password-account-setup/quickstart.md` and `research.md` in line with what was built: the quickstart cites `PasswordLinkIsolationTests`, `LeakTests` and `IsolationSamples`, none of which exists under those names (the cases live in `PasswordLinkLifeTests`, `SignInLogTests` and `Isolation/IdentitySamples.cs`), and says the answers are timed over 1,000 requests per case where the test uses 30; research R15 still says the success banners travel in router navigation state where the code hands them over in memory per plan: validation guide (partial)
- [ ] T069 Record in `specs/004-password-account-setup/validation.md` the two measured times T064 asked for — forgot-password to signed in (SC-001) and invitation email to signed in (SC-008) — which the walk against the real stack describes but does not time per T064 (partial) — SC-001 recorded on 10 Oct 2026; SC-008 still to be timed, because the only never-activated seed account was left for the owner to try
- [X] T070 Review and record the changes made to spec 003's screens while implementing this spec, which no artifact of either spec called for: the shared sign-in components were restyled to the design file, and sending the code again became a button instead of the text link. Update `specs/003-clinic-sign-in/contracts/sign-in-screens.md` (code entry row, "No more resends" row), close or re-scope its open tasks T077 and T091 in `specs/003-clinic-sign-in/tasks.md` with a pointer to `specs/004-password-account-setup/validation.md`, and add a note to `specs/003-clinic-sign-in/validation.md` per owner decisions of 10 Oct 2026 (unrequested)
- [X] T071 Document the `SCS_SHOTS` option of the browser tests (`pictureFor` in `src/web/e2e/staff-platform.ts`, used by `sign-in-states.spec.ts` and `password-states.spec.ts`) in `docs/setup.md` or the testing guide, or remove it: it was added for the design comparison and nothing in the spec, plan or tasks asks for it per plan: testing approach (unrequested)
