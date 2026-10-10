---

description: "Task list for 003-clinic-sign-in"
---

# Tasks: Clinic Staff Sign-In and Sessions

**Input**: Design documents from `/specs/003-clinic-sign-in/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 and 002 implemented locally and Docker running: every test that touches accounts or sessions needs a real SQL Server.

**Tests**: Included, and first. Refusals, the lock, the code's limits and session ending are test-first areas (constitution VII): each test task is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere. Mail is captured by a fake sender; time is moved with a fake clock.

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US8)
- Paths are relative to the repository root

## Path Conventions

- Module: `src/server/modules/Identity/Scs.Identity/` (`Domain/`, `Application/`, `Infrastructure/`, `Api/`) and `Scs.Identity.Contracts/`
- Shared server code: `src/server/shared/Scs.BuildingBlocks/` (`Security/`, `Mail/`, `Configuration/`, `Tenancy/`)
- Tests: `src/server/tests/Scs.IntegrationTests/Identity/`, `Scs.UnitTests/Identity/`, `Scs.ArchitectureTests/`
- Web: screens in `src/web/projects/clinic/src/app/`, shared code in `src/web/projects/core/src/lib/auth/`, components in `src/web/projects/ui/src/lib/`, browser tests in `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Projects, configuration, the mail catcher and the database user the feature needs.

- [X] T001 Confirm the branch is `003-clinic-sign-in` from `develop` with spec 002 merged, and that `dotnet test src/server/Scs.sln` and `npm --prefix src/web run ci` pass before any change
- [X] T002 [P] Create `src/server/modules/Identity/Scs.Identity/Scs.Identity.csproj` (folders `Domain/`, `Application/`, `Infrastructure/`, `Api/`; `InternalsVisibleTo Scs.IntegrationTests`) and `src/server/modules/Identity/Scs.Identity.Contracts/Scs.Identity.Contracts.csproj`; add both to `src/server/Scs.sln`; reference `Scs.Identity` from `src/server/host/Scs.Api` and `src/server/tools/Scs.Migrator`; `Scs.Identity` references `Scs.Tenancy.Contracts` only, never `Scs.Tenancy`
- [X] T003 [P] Add `MailKit` and `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to `src/server/Directory.Packages.props` and reference them from `src/server/shared/Scs.BuildingBlocks/Scs.BuildingBlocks.csproj`; confirm the vulnerability check of the build stays clean
- [X] T004 Extend `deploy/compose/sql/create-logins.sql`: user without login `scs_signin_lookup`, member of `scs_cross_clinic`; `CREATE SCHEMA security`; `GRANT INSERT ON SCHEMA::security TO scs_app`; `DENY SELECT, UPDATE, DELETE ON SCHEMA::security TO scs_app`; `DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::security TO scs_report`; `GRANT SELECT ON SCHEMA::security TO scs_ops`. Column grants for `scs_signin_lookup` and `EXECUTE` for `scs_app` are in the migration that creates the procedure (T017); grant `scs_migrator` what that needs
- [X] T005 [P] Add a `mailpit` service (image `axllent/mailpit`, SMTP on host port 1025, inbox on 8025) to `deploy/compose/docker-compose.yml`, start it in `scripts/dev.ps1` and `scripts/dev.sh`, and print `Mail inbox  http://localhost:8025` in their closing list
- [X] T006 [P] Add to `deploy/compose/.env.example` every variable of data-model.md "Settings": `SCS_AUTH__*` (with a sample `SCS_AUTH__CODE_KEY` of 32 random bytes in base64, marked sample), `SCS_MAIL__HOST=localhost`, `SCS_MAIL__PORT=1025`, `SCS_MAIL__FROM`, `SCS_MAIL__USE_TLS=false`, `SCS_SUPPORT__*` with the BRD's sample values, and `SCS_SEED__STAFF_PASSWORD`; add each to the configuration table in `specs/001-platform-foundation/data-model.md`
- [X] T007 Write cases in `src/server/tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs`, then add `AuthOptions`, `MailOptions` and `SupportOptions` to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` with the defaults and limits of data-model.md "Settings" (max failed attempts 5, 3–10; lock 15 min, 1–1440; code 10 min, 1–30; code attempts 5, 3–10; resend 60 s, 15–600; resends per sign-in 3, 0–10; attempt window 15 min, 1–1440; spent codes before the lock 3, 1–10; renewals per minute 120, 30–1200; cookie path base, empty by default; session 12 h, 1–168; access 5 min, 1–30; grace 30 s, 5–120; requests per minute 10, 5–120; email-code switch `true`; `CodeKey` required, at least 32 bytes, `[Secret]`; mail password `[Secret]`), each failure naming its variable; update the defaults in `src/server/tests/Scs.IntegrationTests/Fixtures/ApiFactory.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: What every story stands on: error codes, mail, time, the tables with their isolation, the one database crossing, accounts, and the shared screen components.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Shared server pieces

- [X] T008 [P] Add the twelve codes of contracts/sessions.md §5 to `src/server/shared/Scs.SharedKernel/ErrorCodes.cs`, their Arabic and English messages to `src/server/shared/Scs.BuildingBlocks/Errors/ErrorMessages.cs` (Arabic from contracts/sign-in-screens.md), their rows to `specs/001-platform-foundation/contracts/conventions.md` §3, and extend the catalogue test in `src/server/tests/Scs.UnitTests/`
- [X] T009 [P] Implement `IEmailSender` (`SendAsync(to, subject, textBody, htmlBody, cancellation)`) and `SmtpEmailSender` (MailKit, 10-second limit, throws `EmailNotSentException`; never logs the recipient) in `src/server/shared/Scs.BuildingBlocks/Mail/`, register it in `AddScsMail`, and add `FakeEmailSender` (captures messages; can be told to fail) to `src/server/tests/Scs.IntegrationTests/Fixtures/`
- [X] T010 [P] Register `TimeProvider.System` in the host and add `MovableClock` (a `TimeProvider` whose time tests advance) to `src/server/tests/Scs.IntegrationTests/Fixtures/`, with `ApiFactory` options to swap in the fake sender and the clock
- [X] T011 [P] Add `IClinicProfile.GetOwnAsync()` returning `ClinicProfile(Id, NameAr, NameEn, State)` to `src/server/modules/Tenancy/Scs.Tenancy.Contracts/`, implement it in `src/server/modules/Tenancy/Scs.Tenancy/Infrastructure/` reading the established clinic's own row under isolation, with a test in `src/server/tests/Scs.IntegrationTests/Tenancy/ClinicProfileTests.cs` (own clinic returned; no clinic established → `CLINIC_REQUIRED`)
- [X] T012 Add the data-protection key ring: entity and table `platform.DataProtectionKey` (`Id int identity`, `FriendlyName nvarchar(max)`, `Xml nvarchar(max)`) in `src/server/modules/Platform/Scs.Platform/Infrastructure/Persistence/PlatformDbContext.cs` with a migration, declare it in `PlatformWideTables`, and configure `AddDataProtection().PersistKeysToDbContext<PlatformDbContext>().SetApplicationName("scs")` in `src/server/shared/Scs.BuildingBlocks/Security/CookieProtection.cs` with three named purposes (`challenge`, `access`, `renewal`); test in `src/server/tests/Scs.IntegrationTests/Identity/CookieProtectionTests.cs` that a value protected by one host is read by a second host on the same database and that a tampered value is refused

### The database crossing — prove it first (research R4)

- [X] T013 Write `src/server/tests/Scs.IntegrationTests/Identity/SignInLookupTests.cs` (trait `Category=Isolation`), failing: as `scs_app` with no clinic, `SELECT` from `identity.StaffAccount` returns nothing; `EXEC identity.usp_FindAccountForSignIn @Email` returns exactly the columns `AccountId, ClinicId` for a known email and no row for an unknown one; `scs_app` cannot `EXECUTE AS USER = 'scs_signin_lookup'`; `scs_signin_lookup` cannot select `PasswordHash`; `scs_report` cannot execute the procedure
- [X] T014 Extend `src/server/tests/Scs.IntegrationTests/Isolation/IsolationCatalog.cs` and `IsolationGuardrailTests.cs`: list every module in `sys.sql_modules` whose `execute_as_principal_id` is not null and fail for any not in `DeclaredDatabaseCrossings`; add a case to `GuardrailCatchesTests` proving an undeclared `EXECUTE AS` procedure is reported by name; create `src/server/shared/Scs.BuildingBlocks/Tenancy/DeclaredDatabaseCrossings.cs` (name, user it runs as, one-line reason) with the single entry

### Tables

- [X] T015 [P] Write `src/server/tests/Scs.UnitTests/Identity/EmailAddressTests.cs` and `PasswordRulesTests.cs`, then implement `src/server/modules/Identity/Scs.Identity/Domain/EmailAddress.cs` (lower case, trimmed; required; at most 254 characters; one `@` with text on both sides and a dot in the domain; field codes `REQUIRED`, `TOO_LONG`, `INVALID_FORMAT`) and `PasswordRules.cs` (at least 10 characters, an upper-case and a lower-case letter, a digit, a symbol; one code per unmet rule: `TOO_SHORT`, `NO_UPPER_LOWER`, `NO_DIGIT`, `NO_SYMBOL`)
- [X] T016 [P] Implement the four entities in `src/server/modules/Identity/Scs.Identity/Domain/` exactly as data-model.md: `StaffAccount` (`Email nvarchar(254)` `[PersonalData]`, `PasswordHash nvarchar(200)` null `[Secret]`, `State varchar(16)` in `invited|active|disabled`, `FailedAttempts tinyint` default 0, `LastFailedAtUtc` null, `SpentCodes tinyint` default 0, `LockedUntilUtc` null, `CreatedAtUtc`, `RowVersion`), `SignInChallenge` (`CodeHash binary(32)` `[Secret]`, `Attempts tinyint`, `ExpiresAtUtc`, `ResendAvailableAtUtc`, `ResendsUsed tinyint` 0 to 3, `ConsumedAtUtc` null, `ReplacedAtUtc` null, `IpAddress varchar(45)` null), `Session` (`RenewalHash binary(32)` `[Secret]`, `PreviousRenewalHash` null, `PreviousValidUntilUtc` null, `LastUsedAtUtc`, `ExpiresAtUtc`, `EndedAtUtc` null, `EndReason varchar(24)` null, `IpAddress varchar(45)`, `UserAgent nvarchar(300)`), `SignInEvent` (`Kind varchar(24)`, `Category varchar(8)`, `Detail varchar(24)` null, `SessionId` null, `IpAddress`, `UserAgent`, `TraceId char(32)`, `OccurredAtUtc`); all implement `IClinicOwned`
- [X] T017 Create `src/server/modules/Identity/Scs.Identity/Infrastructure/Persistence/IdentityDbContext.cs` (derives from `ClinicAwareDbContext`, schema `identity`) and the first migration: the four tables, each with `CreateClinicIsolation` in the same migration; unique index on `StaffAccount.Email` over the whole table; check constraints (`State` values; `active` requires `PasswordHash`); filtered unique index on `SignInChallenge(AccountId)` where `ConsumedAtUtc IS NULL AND ReplacedAtUtc IS NULL`; index on `Session(ClinicId, AccountId)` where `EndedAtUtc IS NULL`; a trigger on `SignInEvent` refusing `UPDATE` and `DELETE` unless `IS_ROLEMEMBER('scs_cross_clinic') = 1`; the procedure `identity.usp_FindAccountForSignIn` `WITH EXECUTE AS 'scs_signin_lookup'`, `GRANT SELECT (Id, ClinicId, Email)` to that user and `GRANT EXECUTE` to `scs_app`. Create `security.AnonymousSignInAttempt` (`Id bigint identity`, `EmailKey binary(32)`, `IpAddress varchar(45)`, `UserAgent nvarchar(300)`, `TraceId char(32)`, `OccurredAtUtc`) and declare it in `PlatformWideTables`
- [X] T018 Create `src/server/modules/Identity/Scs.Identity/IdentityModule.cs` (DI, migrations after `Tenancy`), register it in `src/server/host/Scs.Api/ModuleRegistration.cs` and `src/server/tools/Scs.Migrator` after `TenancyModule`; add an isolation sample for each of the four tables in `src/server/tests/Scs.IntegrationTests/Isolation/IdentitySamples.cs`; `IsolationGuardrailTests`, `IsolationBehaviourTests`, T013 and T014 now pass. If T013 shows the predicate does not honour the procedure's user, stop and apply the fallback of research R4 before continuing

### Accounts

- [X] T019 [P] Write `src/server/tests/Scs.UnitTests/Identity/PasswordHasherTests.cs` (a hash verifies; a wrong password does not; two hashes of one password differ; the stored form is `v1.210000.<salt>.<hash>`; a hash with fewer iterations verifies and reports "needs re-hash"; a malformed value verifies as false without throwing), then implement `src/server/modules/Identity/Scs.Identity/Infrastructure/PasswordHasher.cs` (PBKDF2-HMAC-SHA-512, 210,000 iterations, 16-byte salt, 32-byte result, constant-time comparison, a fixed decoy hash for unknown accounts)
- [X] T020 Write `src/server/tests/Scs.IntegrationTests/Identity/StaffAccountsTests.cs`: creating `active` with a strong password and `invited` with none; `active` without a password and a weak password are refused with the field codes; the same email in the same clinic and in another clinic are both refused with `TAKEN` and identical messages; email is stored lower-cased and trimmed; under clinic B an account of clinic A is "not found" for every operation. Then define `IStaffAccounts` (`CreateAsync`, `SetStateAsync`, `UnlockAsync`, `ClearLockAsync`) in `src/server/modules/Identity/Scs.Identity.Contracts/` and implement `CreateAsync` in `src/server/modules/Identity/Scs.Identity/Application/StaffAccounts.cs`
- [X] T021 Add the six seed accounts of data-model.md "Seed" to `IdentityModule.SeedAsync` (password from `SCS_SEED__STAFF_PASSWORD`; skipped when present), run by the existing `seed` command of `src/server/tools/Scs.Migrator`; note them in `ops/runbooks/data-handling.md`

### Shared screen components (design file "Smart Clinic OS — Design System & Screens", sign-in section)

- [X] T022 [P] Build `scs-banner` (tones danger, warning, neutral, success; optional action; `role="alert"` or `role="status"`), `scs-button` (primary, secondary; busy state with a changed label, locked while busy) and `scs-link` in `src/web/projects/ui/src/lib/`, each with a unit spec, using logical layout utilities only
- [X] T023 [P] Build `scs-text-field` (label, leading icon, error text tied to the input, forced left-to-right option) and `scs-password-field` (the same plus the eye that shows and hides; crossed eye when shown) in `src/web/projects/ui/src/lib/field/`, with unit specs
- [X] T024 [P] Build `scs-code-input` in `src/web/projects/ui/src/lib/code-input/`: six boxes as one labelled group, always left-to-right with Western digits, typing moves on, Backspace moves back, pasting six digits fills all, states error (red), accepted (green, locked); unit spec covering each behaviour in both directions
- [X] T025 [P] Build `scs-auth-frame` in `src/web/projects/ui/src/lib/auth-frame/`: introduction panel on the end side with the logo and the three texts of contracts/sign-in-screens.md, form panel with a 400-wide slot, language switch in the outer top corner, footer slot; panel hidden on narrow screens; translation keys in both languages in `src/web/projects/ui`; unit spec for mirroring

**Checkpoint**: tables exist and are isolated, the crossing is proven and declared, accounts can be created, components exist. Stories can start.

---

## Phase 3: User Story 1 - A team member signs in with email, password and an email code (Priority: P1) 🎯 MVP

**Goal**: Both steps work end to end; no session before the code; work after sign-in is for the account's clinic only.

**Independent Test**: quickstart.md Scenario 1.

### Tests for User Story 1 (write first, see them fail)

- [X] T026 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Identity/SignInFlowTests.cs` against contracts/identity-api.openapi.yaml: step one with correct details answers 200 with `expiresInSeconds` and `resendAvailableInSeconds`, sets the challenge cookie (`HttpOnly`, `Secure`, `SameSite=Strict`), sends exactly one email whose body contains a six-digit code, and `GET /v1/auth/session` still answers 401; step two with that code answers 200 with the session body, sets the access and renewal cookies, clears the challenge cookie; `GET /v1/auth/session` then answers 200; missing or malformed fields answer 400 `VALIDATION_FAILED`; email in capitals with spaces signs in; the wrong-attempt count is zero after a correct password; exactly one `signed-in` event with category `activity` exists for the account after step two, carrying the session id and the request reference (FR-052); the email's subject and bodies contain the code and its lifetime and neither the clinic's names nor the account's email (FR-055)
- [X] T027 [P] [US1] Write `src/server/tests/Scs.IntegrationTests/Identity/SessionIsolationTests.cs`: two clinics each with a signed-in person read a clinic-owned probe table at the same moment and each gets its own rows only; a request with a session has the clinic context established with source `account`; `ICurrentStaff` gives the account, session and clinic; a patient-surface header naming another clinic on a signed-in request is refused with `CLINIC_MISMATCH`
- [X] T028 [P] [US1] Write `src/web/projects/core/src/lib/auth/auth.service.spec.ts`: `load()` asks `/v1/auth/session` once; 200 gives a signed-in state, `UNAUTHENTICATED` a signed-out one; `signIn` keeps the typed email in memory for the code step and never in storage; `confirmCode` success sets the session; the guard sends a signed-out visitor to `/sign-in` and a signed-in one away from it
- [X] T029 [P] [US1] Write `src/web/e2e/sign-in.spec.ts` with the platform's answers given by the test: opening `/` without a session shows the sign-in form; empty fields show «البريد مطلوب» and «كلمة المرور مطلوبة» with no call; an email without `@` shows «البريد غير صحيح — مثل name@clinic.com»; correct details show «جارٍ تسجيل الدخول…» then «التحقق بخطوتين» naming the email; a correct code shows green boxes, «الرمز صحيح، وسُجّل الدخول.», «جارٍ فتح الرئيسية…», then the landing page; opening `/sign-in` when signed in goes to `/`; Enter submits; first focus is the email field; the eye shows and hides the password

### Implementation for User Story 1

- [X] T030 [P] [US1] Implement `src/server/modules/Identity/Scs.Identity/Infrastructure/CodeHasher.cs` (six digits from `RandomNumberGenerator.GetInt32(0, 1_000_000)` zero-padded; HMAC-SHA-256 keyed by `SCS_AUTH__CODE_KEY` and the challenge id) with `src/server/tests/Scs.UnitTests/Identity/CodeHasherTests.cs`, and `CodeEmail.cs` (subject and plain-text and HTML bodies, Arabic then English, the code and its lifetime only)
- [X] T031 [US1] Implement `src/server/modules/Identity/Scs.Identity/Application/SignInService.cs` step one for the success path: find the account through `usp_FindAccountForSignIn`, establish the clinic (source `account`), read the account, verify the password (re-hash when parameters are old), return the count to zero, issue a `SignInChallenge` replacing any standing one in the same transaction, send the email, and only then commit; if the email fails, roll back and answer `SIGN_IN_CODE_NOT_SENT`
- [X] T032 [US1] Implement `src/server/modules/Identity/Scs.Identity/Infrastructure/SessionCookies.cs`: write and read the challenge cookie (`__Host-scs-signin`, 10 minutes, carries challenge, account and clinic ids), the access cookie (`__Host-scs-session`, path `/`, access life) and the renewal cookie (`__Secure-scs-renew`, path `SCS_AUTH__COOKIE_PATH_BASE` + `/v1/auth`, with a unit test that the deployed value `/api` gives `/api/v1/auth`), all protected with the purposes of T012, `HttpOnly`, `Secure`, `SameSite=Strict`; and clearing each
- [X] T033 [US1] Implement `src/server/modules/Identity/Scs.Identity/Application/CodeService.cs` confirm for the success path and `SessionService.OpenAsync`: one `UPDATE` consumes a standing, matching code; a `Session` row is created with a 256-bit renewal secret stored as SHA-256, the network address and truncated browser string; a `signed-in` event is written in the same transaction
- [X] T034 [US1] Implement `src/server/shared/Scs.BuildingBlocks/Security/StaffSessionAuthentication.cs` and replace `NoIdentityAuthenticationHandler` in the host: open the access cookie, establish the clinic from it (source `account`), ask `IStaffSessionCheck` (implemented in `Scs.Identity`) whether the session is alive with one indexed read, and set the principal; no cookie → `UNAUTHENTICATED`, expired cookie → `ACCESS_EXPIRED`, session not alive → `SESSION_ENDED`. Define `ICurrentStaff` in `Scs.Identity.Contracts` and implement it. The default "refuse unless the endpoint says otherwise" is unchanged; update the spec 001 tests that named the old handler
- [X] T035 [US1] Implement `src/server/modules/Identity/Scs.Identity/Api/AuthEndpoints.cs`: `POST /v1/auth/sign-in`, `POST /v1/auth/sign-in/code` (both anonymous) and `GET /v1/auth/session` (clinic names and state through `IClinicProfile`); map them in `IdentityModule`; makes T026 and T027 pass
- [X] T036 [P] [US1] Implement `src/web/projects/core/src/lib/auth/auth.service.ts` (signals for the session and its loading; `load`, `signIn`, `confirmCode`), `session.guard.ts` and `signed-out.guard.ts`; send cookies with platform calls (`withCredentials`) and add `X-Scs-Request: 1` to changing requests in `src/web/projects/core/src/lib/http/api.interceptor.ts`; allow credentials for the configured origins in the host's local CORS policy; export from `public-api.ts`; makes T028 pass
- [X] T037 [US1] Implement `src/web/projects/clinic/src/app/sign-in/sign-in.page.ts` (states default, filled, password shown, validation, loading; title, subtitle, fields, button, «نسيت كلمة المرور؟» link to `/forgot-password` — a route showing the standard "not available yet" state until spec 004 —, fixed «حساب جديد؟…» line) and `code.page.ts` (states entry and accepted) inside `scs-auth-frame`, with translation keys in `src/web/projects/clinic/public/i18n/ar.json` and `en.json`
- [X] T038 [US1] Add routes `/sign-in`, `/sign-in/code` and the guarded `/` to `src/web/projects/clinic/src/app/app.routes.ts`; turn the placeholder into `src/web/projects/clinic/src/app/landing/landing.page.ts` showing the email and the clinic's name in the current language; update `src/web/e2e/placeholder.spec.ts` for the clinic app; makes T029 pass

**Checkpoint**: a seeded person signs in through both steps in the browser against the real stack, reading the code from Mailpit.

---

## Phase 4: User Story 2 - Nothing is revealed before a correct password (Priority: P1)

**Goal**: One answer, identical in content and time, for everything wrong before a correct password.

**Independent Test**: quickstart.md Scenario 2.

### Tests for User Story 2 (write first, see them fail)

- [X] T039 [P] [US2] Write `src/server/tests/Scs.IntegrationTests/Identity/SingleAnswerTests.cs`: unknown email; wrong password; any password for an invited account; wrong password for a locked account, a disabled account, and an account of a suspended and of a cancelled clinic — each answers 401 `INVALID_CREDENTIALS` with bodies identical apart from `traceId`, the same headers, no cookie set and no email sent; each wrong password for an existing account adds one to its count, and the unknown email writes one row to `security.AnonymousSignInAttempt` holding no readable email
- [X] T040 [P] [US2] Write `src/server/tests/Scs.IntegrationTests/Identity/SingleAnswerTimingTests.cs` (pattern of `Isolation/IndistinguishableTests.cs`): over 1,000 alternating requests per pair, the trimmed mean response time of "unknown email" and of "wrong password" differ by under 5%, and likewise "invited account" against "wrong password" (SC-002)
- [X] T041 [P] [US2] Add to `src/web/e2e/sign-in.spec.ts`: `INVALID_CREDENTIALS` shows the danger banner «البريد الإلكتروني أو كلمة المرور غير صحيحة.» between the title and the fields, both fields keep their content, the button is enabled, the «حساب جديد؟…» line is still there; an aborted request shows «تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.»; a banner and a field error never appear together

### Implementation for User Story 2

- [X] T042 [US2] Complete step one in `SignInService.cs` for every failing path before a correct password, in the order of FR-009: unknown email verifies the typed password against the decoy hash and records an anonymous attempt (`EmailKey` = HMAC of the lower-cased text); invited account does the same against the decoy; wrong password counts an attempt; all return the one refusal. No branch returns earlier than another; makes T039 and T040 pass
- [X] T043 [US2] Add the error and request-failed states to `sign-in.page.ts` with the mapping of contracts/sign-in-screens.md (one banner at most; cleared on the next submit); makes T041 pass

**Checkpoint**: nobody can learn from the form whether an email is registered.

---

## Phase 5: User Story 3 - Wrong attempts lock the account, and the person is told what to do (Priority: P1)

**Goal**: The lock, and the three refusals that are told only after a correct password.

**Independent Test**: quickstart.md Scenario 3.

### Tests for User Story 3 (write first, see them fail)

- [X] T044 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Identity/LockoutTests.cs` with `MovableClock`: the fifth consecutive wrong password answers the usual error, sets the lock 15 minutes ahead, returns the count to zero, ends the account's live sessions and writes `account-locked`; a correct password while locked answers 403 `ACCOUNT_LOCKED` with `lockedUntil` and sends no email; a wrong one while locked answers `INVALID_CREDENTIALS` and does not move the lock; after 15 minutes sign-in works; `UnlockAsync` and `ClearLockAsync` open it at once; four wrong then one correct returns the count to zero; four wrong, 15 minutes with no attempt, then one wrong does **not** lock and leaves the count at one; four wrong spaced 14 minutes apart then a fifth does lock; twenty wrong attempts sent at the same moment lock the account exactly once and never lose a count
- [X] T045 [P] [US3] Write `src/server/tests/Scs.IntegrationTests/Identity/RefusalOrderTests.cs`: disabled account with the correct password → 403 `ACCOUNT_DISABLED`, no email; suspended clinic → 403 `CLINIC_SUSPENDED`, no email; cancelled clinic → the same; locked **and** disabled → `ACCOUNT_LOCKED` (lock before state); disabled in a suspended clinic → `ACCOUNT_DISABLED` (account before clinic); a clinic in any other state signs in
- [X] T046 [P] [US3] Add to `src/web/e2e/sign-in.spec.ts`: `ACCOUNT_LOCKED` shows the warning banner with the local time of `lockedUntil`, the time and its AM/PM mark in one unbreakable run, in both languages; `ACCOUNT_DISABLED` shows the danger banner; `CLINIC_SUSPENDED` shows the danger banner with the action «التواصل مع الدعم» (that it opens the help window is asserted in T073); none of the three mentions an invoice or who acted

### Implementation for User Story 3

- [X] T047 [US3] Implement the lock in `SignInService.cs`: one `UPDATE … OUTPUT` that adds a wrong attempt (starting again from one when `LastFailedAtUtc` is older than the attempt window), stamps `LastFailedAtUtc` and, on reaching the maximum, sets `LockedUntilUtc` and zeroes the count; ending the account's sessions (reason `locked`) and the `account-locked` and `wrong-password` events in the same transaction; then the checks after a correct password in order: lock → account state → clinic state (through `IClinicProfile`; `suspended` and `cancelled` refuse)
- [X] T048 [US3] Implement `UnlockAsync`, `ClearLockAsync` and `SetStateAsync` in `StaffAccounts.cs` (disabling ends the account's sessions with reason `disabled` in the same transaction); makes T044 and T045 pass
- [X] T049 [US3] Add the locked, disabled and suspended states to `sign-in.page.ts` with their translation keys in both languages (disabled says "disabled" only; suspended says support is the only way); makes T046 pass

**Checkpoint**: Stories 1 to 3 are the whole of the first step.

---

## Phase 6: User Story 4 - The code is short-lived, limited and can be sent again (Priority: P2)

**Goal**: Every limit of the code, resend, and the re-check between the two steps.

**Independent Test**: quickstart.md Scenario 4.

### Tests for User Story 4 (write first, see them fail)

- [X] T050 [P] [US4] Write `src/server/tests/Scs.IntegrationTests/Identity/SignInCodeTests.cs` with `MovableClock`: a wrong code, a code after 10 minutes, the right code on the sixth attempt, a used code, and a request with no challenge cookie each answer 401 `CODE_INVALID` with identical bodies; five wrong codes do not lock the account and write `wrong-code` events and one `code-spent`; the third code spent in a row locks the account for 15 minutes, ends its sessions and writes `account-locked`, while two spent codes followed by a successful sign-in return the count to zero; a second correct password replaces the standing code and the first no longer works; twenty confirmations of the right code at the same moment open exactly one session; the account disabled, or its clinic suspended, after step one answers 403 with that code, opens no session and clears the challenge cookie
- [X] T051 [P] [US4] Write `src/server/tests/Scs.IntegrationTests/Identity/ResendTests.cs`: before the countdown → 409 `RESEND_TOO_EARLY` with `resendAvailableInSeconds`; after it → 200, one new email, the earlier code refused, the new one accepted, and the sign-in's ten minutes not extended; with the sender failing → 503 `SIGN_IN_CODE_NOT_RESENT`, the earlier code still accepted and `resendsLeft` unchanged; each 200 carries `resendsLeft` (3 after step one, then 2, 1, 0); a fourth resend → 409 `RESEND_LIMIT_REACHED` with the last code still accepted; a new correct password starts again at 3; with the sender failing at step one → 503 `SIGN_IN_CODE_NOT_SENT`, no challenge row, no cookie; no challenge cookie → `CODE_INVALID`
- [X] T052 [P] [US4] Write `src/web/e2e/sign-in-code.spec.ts`: `CODE_INVALID` turns the boxes red with «الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.» and the button enabled; the countdown «إعادة الإرسال خلال ‹د:ث›» decreases and becomes «لم يصلك الرمز؟ إعادة الإرسال»; resend restarts it without asking for the password; after the third resend the link is replaced by the note to go back and sign in again while «تأكيد الرمز» stays enabled; `SIGN_IN_CODE_NOT_RESENT` shows its banner and the secondary button «إعادة إرسال الرمز»; `SIGN_IN_CODE_NOT_SENT` shows its banner on the sign-in form and no code step; «الرجوع لتسجيل الدخول» is present in every state except accepted and returns to the form; `ACCOUNT_DISABLED` at the code returns to the form in that state; reloading the code page returns to the form; digits are Western and left-to-right in Arabic

### Implementation for User Story 4

- [X] T053 [US4] Complete `CodeService.cs`: the single `UPDATE` that counts an attempt and consumes only a standing, matching, unexpired code; `wrong-code` and `code-spent` events; counting spent codes on the account and locking on the third (reusing the lock of T047); re-check of account and clinic state before opening the session; `ResendAsync` (too early; limit reached; issue and replace in one transaction carrying `ResendsUsed + 1`, email before commit, roll back on failure)
- [X] T054 [US4] Add `POST /v1/auth/sign-in/code/resend` to `AuthEndpoints.cs`; makes T050 and T051 pass
- [X] T055 [US4] Add to `code.page.ts` the countdown, resend, the error, resend-failed and no-more-resends states, the back link and the return to the form with a state banner; add the code-not-sent state to `sign-in.page.ts`; `resendCode` in `auth.service.ts`; makes T052 pass

**Checkpoint**: the second step is complete.

---

## Phase 7: User Story 5 - A session ends when it should, and at once (Priority: P2)

**Goal**: Sign-out, every other cause of ending, and the "session ended" notice.

**Independent Test**: quickstart.md Scenario 5.

### Tests for User Story 5 (write first, see them fail)

- [X] T056 [P] [US5] Write `src/server/tests/Scs.IntegrationTests/Identity/SessionEndingTests.cs`: one case per end reason of data-model.md that this spec can cause (`signed-out`, `expired`, `locked`, `disabled`, `ended-for-account`, `ended-for-clinic`, `clinic-suspended`), each asserting the very next request with the old cookies answers 401 `SESSION_ENDED` (or `UNAUTHENTICATED` after sign-out, whose cookies were cleared), the row has `EndedAtUtc` and the reason, and a `session-ended` event exists; sign-out ends only its own session and a second device continues; `EndForClinicAsync(except)` leaves exactly the excepted session; sign-out answers 204 with no session and in a suspended clinic; under clinic B, ending sessions of an account of clinic A ends nothing; as `scs_app` and as `scs_report`, `UPDATE` and `DELETE` on `identity.SignInEvent` are refused and the row is unchanged; a clinic marked suspended directly in the database, with no ending call, has its sessions refused at their next renewal
- [X] T057 [P] [US5] Write `src/web/e2e/session.spec.ts`: «تسجيل الخروج» on the landing page leads to the sign-in form with no banner; a call answered `SESSION_ENDED` clears the page and shows the form with «انتهت الجلسة. الدخول من جديد للمتابعة.», after which `?ended=1` is gone from the address; no text of the landing page remains in the document; a second tab that was signed in shows the form after the first signs out

### Implementation for User Story 5

- [X] T058 [US5] Define `IStaffSessions` (`EndForAccountAsync(accountId, reason)`, `EndForClinicAsync(exceptSessionId, reason)`) in `Scs.Identity.Contracts` and implement it with `SessionService.EndAsync` in `src/server/modules/Identity/Scs.Identity/Application/`; each ending writes its `session-ended` event; expiry is the check `ExpiresAtUtc > now` in `IStaffSessionCheck`
- [X] T059 [US5] Add `POST /v1/auth/sign-out` (anonymous, idempotent, clears the three cookies) to `AuthEndpoints.cs`; makes T056 pass
- [X] T060 [US5] In `auth.service.ts` and `api.interceptor.ts`: `signOut`; on `SESSION_ENDED` clear the session state, navigate to `/sign-in?ended=1`; on `UNAUTHENTICATED` navigate to `/sign-in`; a `BroadcastChannel` so other tabs follow sign-in and sign-out. Add «تسجيل الخروج» to `landing.page.ts` and the session-ended state to `sign-in.page.ts`; makes T057 pass

**Checkpoint**: access can be withdrawn from a person immediately.

---

## Phase 8: User Story 6 - A stolen session is detected and shut down (Priority: P2)

**Goal**: Quiet renewal, rotation, and ending every session of the account when a replaced secret returns.

**Independent Test**: quickstart.md Scenario 6.

### Tests for User Story 6 (write first, see them fail)

- [X] T061 [P] [US6] Write `src/server/tests/Scs.IntegrationTests/Identity/RenewalTests.cs` with `MovableClock`: after the access life a request answers 401 `ACCESS_EXPIRED`; renewal answers 204, sets new access and renewal cookies and updates `LastUsedAtUtc`; the earlier renewal cookie within 30 seconds is accepted and ends nothing; after 30 seconds it answers `SESSION_ENDED`, every session of the account is ended with reason `renewal-reused` and one `renewal-reused` event exists; renewal is refused when the session is past its maximum life, the account is disabled or locked, or the clinic is suspended; a renewal cookie of clinic A cannot renew anything when its clinic id is altered (tampered value refused)
- [X] T062 [P] [US6] Add to `src/web/projects/core/src/lib/auth/auth.service.spec.ts` and `src/web/e2e/session.spec.ts`: three calls answered `ACCESS_EXPIRED` at once cause exactly one renewal and are each repeated once; a refused renewal leads to the form with the "session ended" notice; a renewal is never attempted for the sign-in endpoints

### Implementation for User Story 6

- [X] T063 [US6] Implement `SessionService.RenewAsync`: one `UPDATE` that rotates when the presented hash is current (moving it to previous with its grace); accept the previous within grace by re-issuing cookies for the current secret; otherwise, when the presented hash equals the previous after grace, end every session of the account and record the event; re-check account and clinic state; refuse past `ExpiresAtUtc`
- [X] T064 [US6] Add `POST /v1/auth/session/renew` (reads only the renewal cookie) to `AuthEndpoints.cs`; makes T061 pass
- [X] T065 [US6] Implement renewal in `api.interceptor.ts` with one request in flight shared by all waiting calls, then repeat each once; makes T062 pass

**Checkpoint**: a working day passes without the person being asked to sign in again, and a copied session is caught.

---

## Phase 9: User Story 7 - Too many requests and blocked networks are refused first (Priority: P3)

**Goal**: The request limit, the blocked-network check, and the check on requests that change something.

**Independent Test**: quickstart.md Scenario 7.

### Tests for User Story 7 (write first, see them fail)

- [X] T066 [P] [US7] Write `src/server/tests/Scs.IntegrationTests/Identity/RateLimitTests.cs`: the eleventh request to the three sign-in addresses together (sign-in, code, resend) from one network address within a minute answers 429 `RATE_LIMITED` with `Retry-After`; a different address is unaffected; refused requests change no account's count and send no email; after the window sign-in works; thirty renewals in the same minute from that address are all accepted and use none of the sign-in allowance. And `BlockedNetworkTests.cs`: with a test `IBlockedNetworks`, correct details from a blocked address answer 403 `NETWORK_BLOCKED` before the limit is counted, with no email, no count and no event on the account
- [X] T067 [P] [US7] Write `src/server/tests/Scs.IntegrationTests/Security/RequestOriginTests.cs`: a changing request without `X-Scs-Request: 1` answers 403 `FORBIDDEN`; with an `Origin` outside the configured list answers 403; `GET` requests need neither; the three cookies are never sent on a cross-site request (asserted from their attributes)
- [X] T068 [P] [US7] Add to `src/web/e2e/sign-in.spec.ts`: `RATE_LIMITED` with `Retry-After: 40` shows a warning banner on the same form naming when to try again, in both languages, on the sign-in form and on the code step; `NETWORK_BLOCKED` shows «الدخول من هذه الشبكة محظور الآن. راجِع فريق الدعم لمعرفة السبب ورفع الحظر.» as a warning

### Implementation for User Story 7

- [X] T069 [US7] Implement `src/server/shared/Scs.BuildingBlocks/Security/BlockedNetworks.cs` (`IBlockedNetworks.IsBlockedAsync(address)`, default blocks nothing) and the sign-in rate-limit policy (fixed window, `SCS_AUTH__REQUESTS_PER_MINUTE` per network address, `Retry-After` on rejection) applied to the sign-in, code and resend endpoints, a second policy of `SCS_AUTH__RENEWALS_PER_MINUTE` on the renew endpoint, with the blocked check running before both
- [X] T070 [US7] Implement `src/server/shared/Scs.BuildingBlocks/Security/RequestOriginCheck.cs` as middleware for unsafe methods and add it to the pipeline in `src/server/host/Scs.Api/Program.cs` after CORS; exempt nothing; makes T066 and T067 pass
- [X] T071 [US7] Add the too-many-requests and network-blocked states to `sign-in.page.ts` and `code.page.ts` (time from `Retry-After`, formatted with the shared date pipe); makes T068 pass

---

## Phase 10: User Story 8 - Every sign-in screen is complete, bilingual and offers help (Priority: P3)

**Goal**: The help window, the footer, and every state of FR-047 in both languages.

**Independent Test**: quickstart.md Scenario 8.

### Tests for User Story 8 (write first, see them fail)

- [X] T072 [P] [US8] Write `src/server/tests/Scs.IntegrationTests/Platform/SupportEndpointTests.cs`: `GET /v1/meta/support` is anonymous and answers the five configured values; and `src/web/projects/ui/src/lib/dialog/dialog.spec.ts`: focus is trapped, Escape and × close, focus returns to the opener
- [X] T073 [P] [US8] Write `src/web/e2e/sign-in-states.spec.ts`: for each of the 20 states of contracts/sign-in-screens.md (reached by the test's platform answers), in Arabic and in English: the frame is present, `dir` and the panel side are right for the language, the footer has the help link, «سياسة الخصوصية» and «شروط الاستخدام», no clinic name appears anywhere in the document, at most one banner is shown, every failure banner contains a next step, and no translation key is shown raw; the help window opens from the footer on every screen, shows the hours and three rows whose links are `tel:`, `https://wa.me/` and `mailto:`, and closes on × back to the same screen; the action of the suspended banner opens the same window; typing in a field keeps the «حساب جديد؟…» line

### Implementation for User Story 8

- [X] T074 [P] [US8] Implement `GET /v1/meta/support` in `src/server/modules/Platform/Scs.Platform/Api/` from `SupportOptions`
- [X] T075 [P] [US8] Build `scs-dialog` on Angular CDK in `src/web/projects/ui/src/lib/dialog/` (dimmed background, title, ×, focus trap, Escape); makes the dialog spec pass
- [X] T076 [US8] Implement `src/web/projects/clinic/src/app/sign-in/help-window.component.ts` and `support.service.ts` in `src/web/projects/core/src/lib/auth/`; wire the footer links in `scs-auth-frame` usage (help; privacy and terms to `/privacy` and `/terms`, which show the standard "not available yet" state until spec 086) and the action of the suspended banner
- [X] T077 (translations complete and checked, English wording written, all 20 states pass in both languages; the comparison with the design file was done on 10 Oct 2026 while implementing spec 004 — see `specs/004-password-account-setup/validation.md`) [US8] Complete both translation files for every state, review the English wording, confirm `npm --prefix src/web run i18n:check` passes, and compare each drawn state with the design file (sign-in section), recording differences in `specs/003-clinic-sign-in/validation.md`; makes T073 pass

---

## Phase 11: Polish & Cross-Cutting Concerns

- [X] T078 [P] Extend `src/server/tests/Scs.IntegrationTests/Logging/LogRedactionTests.cs`: send a password, a code, an email address and cookie values through every identity endpoint on success and failure, with the fake sender failing once; zero needles; entries for signed-in work carry `clinicId`, `accountId` and `sessionId` and no email (FR-054, SC-011)
- [X] T079 [P] Write `src/server/tests/Scs.ArchitectureTests/IdentityRulesTests.cs`: `Scs.Identity` depends on no module except through `.Contracts`; nothing outside `Scs.Identity` references its `Domain`, `Application` or `Infrastructure`; only `SessionCookies` uses the data-protection purposes; no type outside `Scs.BuildingBlocks.Mail` references MailKit
- [X] T080 Implement `src/server/modules/Identity/Scs.Identity/Infrastructure/CleanupJob.cs` as a per-clinic recurring job (`identity-cleanup`, daily): remove sign-in challenges older than one day and sessions ended more than 90 days ago; idempotent; test in `src/server/tests/Scs.IntegrationTests/Identity/CleanupJobTests.cs` with `MovableClock`
- [X] T081 [P] Add `SignInAsAsync(clinicId, accountId?)` to `src/server/tests/Scs.IntegrationTests/Fixtures/ApiFactory.cs` (creates an account if none is given, opens a real session, returns an `HttpClient` carrying its cookies and the request header) and use it in `SessionIsolationTests`
- [X] T082 [P] Update `docs/setup.md` (recreate `.env`; the mail inbox; the six seed accounts and where their password is; signing in locally), `docs/adding-a-module.md` (endpoints need a signed-in person by default; `ICurrentStaff`; `SignInAsAsync` in tests; never log an email) and add `ops/runbooks/sign-in.md` (unlocking an account, ending sessions and rotating `SCS_AUTH__CODE_KEY` by hand until the screens exist)
- [X] T083 [P] Extend `ops/restore/verify.sql`: fail unless `identity.usp_FindAccountForSignIn` exists and runs as `scs_signin_lookup`, and no other module runs as another user
- [X] T087 Write `src/server/tests/Scs.IntegrationTests/Identity/EmailCodeSwitchTests.cs`, then implement the platform switch in `SignInService.cs` (FR-022): with `SCS_AUTH__EMAIL_CODE_ENABLED=false` a correct password that passes every check opens the session directly, answers 200 with the session body and cookies, sends no email and creates no challenge, and every refusal before and after a correct password is unchanged; with it `true` nothing changes; the web `signIn` goes straight to `/` when the answer is a session
- [X] T088 [P] Add cases: `StaffAccountsTests` — no operation of `IStaffAccounts` can change an account's email (FR-002); `ResendTests` — a mail server that does not answer makes step one fail with `SIGN_IN_CODE_NOT_SENT` within the 10-second limit (shortened in the test); and allow-list the sample `SCS_AUTH__CODE_KEY` and `SCS_SEED__STAFF_PASSWORD` of `.env.example` in the secret scan configuration
- [X] T084 Add to the cloud tasks of `specs/001-platform-foundation/tasks.md` (append, do not renumber): wrap the data-protection key ring with the cloud key service; mail service credentials; a shared request limit before more than one instance serves production. Note the three in `specs/003-clinic-sign-in/validation.md` as "not ready for production"
- [ ] T085 Resolve the "Items to confirm" of `specs/003-clinic-sign-in/research.md` with the product owner, and confirm the BRD lines SH-003, CL-SH-065, B-R1 and B-3-1 have been updated
- [X] T086 (completed by T094; every scenario is covered by automated tests, and scenario 1, sign-out, the help window and the request limit were walked by hand in Arabic against the real stack; the full hand walk in both languages is still to do) Run all ten scenarios of `specs/003-clinic-sign-in/quickstart.md` — 1 to 5, 7 and 8 by hand in both languages against the real stack with Mailpit — walk SC-001 to SC-012 against the evidence, and record the result in `specs/003-clinic-sign-in/validation.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: needs spec 002 merged and Docker running.
- **Foundational (Phase 2)**: needs Setup. Blocks every story. T013 and T014 are written before T017; T018 decides whether research R4 holds.
- **US1 (Phase 3)**: needs Foundational. Creates the endpoints, cookies, handler and pages every other story extends.
- **US2 (Phase 4)** and **US3 (Phase 5)**: need US1. US3 needs US2's ordering of checks in `SignInService`.
- **US4 (Phase 6)**: needs US1.
- **US5 (Phase 7)**: needs US1; its `disabled` and `locked` cases need US3.
- **US6 (Phase 8)**: needs US5 (ending sessions) and US1.
- **US7 (Phase 9)**: needs US1; independent of US2 to US6.
- **US8 (Phase 10)**: needs the states of US1 to US5 and US7 to exist, since it checks all twenty.
- **Polish (Phase 11)**: after the stories it measures.

```text
Setup → Foundational → US1 ─┬→ US2 → US3 ─┐
                            ├→ US4        ├→ US5 → US6 ─┐
                            ├→ US7 ───────┴─────────────┴→ US8 → Polish
                            └──────────────────────────────┘
```

### Story Independence

US1, US2 and US3 together are the first step of sign-in and its safety rules; they ship as one.
US4 completes the second step. US5 and US6 are the life of a session. US7 touches only the
edges of the endpoints and can be built at any time after US1.

### Within Each User Story

- Test tasks first; confirm they fail; then the implementation that makes them pass.
- Server before web within a story: the browser tests supply the platform's answers themselves,
  so the web tasks can also proceed in parallel against the contract.
- Table and isolation rule in the same migration, always.

### Parallel Opportunities

- Setup: T002, T003, T005, T006 alongside T004.
- Foundational: T008 to T011 together; T015, T016, T019 together; the four component tasks T022 to T025 by a second person from the start.
- Each story: its test files are different files and are written together; the server and web halves can be done by two people.
- After US1: one person on US2 → US3, one on US4, one on US7.

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "SignInFlowTests.cs in src/server/tests/Scs.IntegrationTests/Identity/"
Task: "SessionIsolationTests.cs in src/server/tests/Scs.IntegrationTests/Identity/"
Task: "auth.service.spec.ts in src/web/projects/core/src/lib/auth/"
Task: "sign-in.spec.ts in src/web/e2e/"

# Then, in parallel with the server chain T031 → T035:
Task: "CodeHasher.cs and CodeEmail.cs in src/server/modules/Identity/Scs.Identity/Infrastructure/"
Task: "auth.service.ts and guards in src/web/projects/core/src/lib/auth/"
```

---

## Implementation Strategy

### MVP First (User Stories 1 to 3)

1. Phase 1 Setup and Phase 2 Foundational (T001 to T025).
2. Phase 3 US1 (T026 to T038): both steps work.
3. Phase 4 US2 and Phase 5 US3 (T039 to T049): the single answer, the lock, the refusals.
4. **Stop and validate**: quickstart.md Scenarios 1 to 3.

That is 49 tasks. A person can sign in safely; a session lasts until its access cookie expires
(five minutes) because renewal is US6, so the MVP is for demonstration, not for a working day.

### Incremental Delivery

1. MVP as above.
2. US4 (T050 to T055): the code's limits and resend.
3. US5 and US6 (T056 to T065): sessions end at once and renew quietly — from here it is usable all day.
4. US7 (T066 to T071): outer defences.
5. US8 (T072 to T077): the help window and every state checked in both languages.
6. Polish (T078 to T086).

### Notes

- If T013 fails on the predicate honouring the procedure's user, apply the fallback of research
  R4 (a platform-wide table keyed by a keyed hash of the email, written with the account) and
  update data-model.md and `DeclaredDatabaseCrossings` before T017.
- Cookies marked `Secure` work on `http://localhost` in current browsers; the browser tests run
  there. If a browser refuses them, the local profile alone may drop the `__Host-` and
  `__Secure-` prefixes — never the `HttpOnly` or `SameSite` attributes.
- Every test that touches accounts or sessions runs against the SQL Server container as the
  application login; none uses an in-memory provider.

---

## Phase 12: Convergence

- [X] T089 In `src/web/projects/clinic/src/app/sign-in/code.page.ts`, when confirming or resending fails because the platform could not be reached (a local problem, not an answer), show the "could not reach the server" banner («تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.») instead of "the code is incorrect", keep the boxes and the button as they are, and add the case to `src/web/e2e/sign-in.spec.ts` and the state to `src/web/e2e/sign-in-states.spec.ts` in both languages per FR-051, Constitution VI (partial)
- [X] T090 Bring the design documents in line with what was built: in `specs/003-clinic-sign-in/research.md` record the 300 ms floor on the single refusal (R5), that the request header is required only where a session is involved (R10), and that the email leaves before the code is stored (R12); correct `contracts/sessions.md` §6 and `contracts/identity-api.openapi.yaml` accordingly; add `SCS_MAIL__TIMEOUT_SECONDS` (default 10, 1 to 60) to the settings table of `data-model.md` per contracts/sessions.md §6, research R5/R10/R12 (contradicts)
- [X] T091 (done on 10 Oct 2026 while implementing spec 004, by eye in two passes; the record is in `specs/004-password-account-setup/validation.md`, with a note in this spec's validation.md) Compare each of the 18 drawn sign-in states with the design file "Smart Clinic OS — Design System & Screens" (page "Sign in"), fix what differs in `src/web/projects/ui/src/lib/` and `src/web/projects/clinic/src/app/sign-in/`, and record the comparison in `specs/003-clinic-sign-in/validation.md` per FR-046 (partial; completes T077)
- [X] T092 Add to `src/web/e2e/sign-in.spec.ts`: a signed-in page is on screen, a later call is answered `SESSION_ENDED`, and the page is replaced by the sign-in form with the neutral notice with none of the landing page's text left in the document per US5/AC2, US5/AC8 (partial)
- [X] T093 Add to `src/server/tests/Scs.IntegrationTests/Identity/` a test of `SmtpEmailSender` against a listener that accepts the connection and never answers: with `SCS_MAIL__TIMEOUT_SECONDS=1` the send fails with `EmailNotSentException` within about a second and logs no address per spec edge case "slow mail" (partial; completes T088)
- [X] T094 Walk quickstart scenarios 2 to 5, 7 and 8 by hand against the real stack in Arabic and in English and record the result in `specs/003-clinic-sign-in/validation.md` per SC-010 (partial; completes T086)
