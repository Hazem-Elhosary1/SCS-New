# Quickstart: validating staff sign-in and sessions

How to prove spec 003 works. Details of answers are in
[contracts/identity-api.openapi.yaml](contracts/identity-api.openapi.yaml); screens and states in
[contracts/sign-in-screens.md](contracts/sign-in-screens.md); tables in
[data-model.md](data-model.md).

## Prerequisites

- Everything in `docs/setup.md`.
- Recreate `deploy/compose/.env` once from `.env.example`: this spec adds the mail, sign-in and
  seed values.
- `scripts/dev.ps1` (or `dev.sh`) now also starts the mail catcher and seeds six made-up
  accounts (data-model.md, "Seed"). Their password is `SCS_SEED__STAFF_PASSWORD` in `.env`.
- Inbox for every email the system sends locally: <http://localhost:8025>.

Automated checks:

```bash
dotnet test src/server/Scs.sln
```

```bash
npm --prefix src/web run ci
```

```bash
npm --prefix src/web run e2e
```

## Scenario 1 — Sign in with both steps (Story 1; SC-001, SC-003, SC-008)

1. Open <http://localhost:4201>. Expected: the sign-in form, not the console.
2. Sign in as `owner@noor.example`. Expected: «جارٍ تسجيل الدخول…», then «التحقق بخطوتين» naming
   the email; a message with a six-digit code in the inbox.
3. Before typing the code, open <http://localhost:5080/v1/auth/session>. Expected: 401.
4. Type the code. Expected: green boxes, «الرمز صحيح، وسُجّل الدخول.», then the landing page
   showing the email and «عيادة النور».
5. Open <http://localhost:4201/sign-in>. Expected: taken to the landing page.
6. In a private window sign in as `owner@shifa.example`. Expected: «عيادة الشفاء».

Automated: `SignInFlowTests`, `SessionIsolationTests`, `e2e/sign-in.spec.ts`.

## Scenario 2 — One answer before a correct password (Story 2; SC-002)

Send step one with: an unknown email; a wrong password for `owner@noor.example`; any password
for `new@shifa.example` (invited); a wrong password for `former@noor.example` (disabled).
Expected: the same banner each time, «البريد الإلكتروني أو كلمة المرور غير صحيحة.».

Automated: `SingleAnswerTests` compares the bodies byte for byte and the mean response times
over 1,000 requests per case (difference under 5%).

## Scenario 3 — The lock and the other refusals (Story 3; SC-004)

1. Sign in as `reception@noor.example` in one browser.
2. In another, fail her password five times. Expected: the usual error each time.
3. In the first browser, do anything. Expected: the sign-in form with «انتهت الجلسة. الدخول من
   جديد للمتابعة.».
4. Type her correct password. Expected: the lock banner with a time 15 minutes ahead; no email.
5. Sign in as `former@noor.example` with the correct password. Expected: the disabled banner.
6. Mark Al Shifa suspended (`UPDATE tenancy.Clinic SET State = 'suspended' …` as the operator
   login) and sign in as `owner@shifa.example`. Expected: the suspended banner with «التواصل مع
   الدعم», which opens the help window. Set the state back.

Automated: `LockoutTests` (with a moved clock for the 15 minutes), `RefusalOrderTests`.

## Scenario 4 — The code's limits (Story 4; SC-005)

On the code step: a wrong code (red boxes, the one message); wait for the countdown and resend
(new email; the earlier code is now refused); five wrong codes, then the right one (refused;
return to the form and start again, and the account is not locked). Stop the mail catcher
(`docker compose … stop mailpit`) and try step one (the "could not send" banner, no code step)
and a resend (the "earlier code remains valid" banner, and it does).

Automated: `SignInCodeTests`, `ResendTests`.

## Scenario 5 — Sessions end at once (Story 5; SC-006)

Sign in on two browsers as the same person. Sign out on one: the other continues. Then, from a
test or the database, end her sessions: the next action in the remaining browser shows the
sign-in form with the "session ended" notice and no data on screen.

Automated: `SessionEndingTests` — one case per end reason of data-model.md, each asserting the
very next request is refused.

## Scenario 6 — A reused renewal is treated as theft (Story 6; SC-007)

Automated only: `RenewalTests` renews a session, presents the earlier secret within 30 seconds
(accepted), then after it (every session of the account ended; one `renewal-reused` event).

## Scenario 7 — Outer defences (Story 7; SC-009)

Send eleven step-one requests within a minute. Expected: the eleventh answers 429 with
`Retry-After`, the form shows the warning with the time, and no account's count changed.

Automated: `RateLimitTests`, `BlockedNetworkTests` (with a test list of blocked addresses).

## Scenario 8 — Screens (Story 8; SC-010)

`e2e/sign-in-states.spec.ts` opens every state of FR-047 in Arabic and English, checks the
frame, the footer links, the mirrored layout and that no clinic name appears, and opens and
closes the help window. `npm run i18n:check` passes.

## Scenario 9 — Nothing private in the logs (SC-011)

Automated: the leak test of spec 001 sends a password, a code, an email and cookie values
through every sign-in endpoint; none appears in the captured output.

## Scenario 10 — The crossing is the declared one (FR-056)

Automated: the isolation guardrail lists every database module that runs as another user;
exactly one, `identity.usp_FindAccountForSignIn`, and it is declared. As the application login,
selecting from `identity.StaffAccount` with no clinic returns nothing, and the procedure returns
two identifiers and nothing else.

## Done

All automated checks pass; scenarios 1 to 5, 7 and 8 were walked by hand in both languages; the
results are recorded in `validation.md` against SC-001 to SC-012.
