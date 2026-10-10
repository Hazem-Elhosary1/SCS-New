# Quickstart: validating passwords and account activation

How to prove spec 004 works. Answers are in
[contracts/password-api.openapi.yaml](contracts/password-api.openapi.yaml); screens and states in
[contracts/password-screens.md](contracts/password-screens.md); operations for later specs in
[contracts/account-handover.md](contracts/account-handover.md); tables in
[data-model.md](data-model.md).

## Prerequisites

- Everything in `docs/setup.md` and spec 003's quickstart.
- Add the seven new `SCS_AUTH__*` values from `.env.example` to `deploy/compose/.env`.
- `scripts/dev.ps1` (or `dev.sh`) seeds one more account, `temp@noor.example`.
- Local inbox: <http://localhost:8025>. Clinic app: <http://localhost:4201>.

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

## Scenario 1 — Forgot password, end to end (Story 1; SC-001, SC-005)

1. Sign in as `reception@noor.example` in one browser and leave it open.
2. In another, open the sign-in form, press «نسيت كلمة المرور؟», type her email, press «إرسال
   رابط إعادة التعيين». Expected: «أُرسل الطلب»; within seconds a message with a link in the
   inbox.
3. Open the link. Expected: «تعيين كلمة مرور جديدة» naming her email; the address bar shows
   `/set-password` with nothing after it.
4. Type a new valid password; press the button. Expected: the sign-in form with «غُيّرت كلمة
   المرور»; `GET /v1/auth/session` answers 401; a second message in the inbox says the password
   was changed.
5. In the first browser, do anything. Expected: the sign-in form with the "session ended" notice.
6. Sign in with the new password, then try the old one. Expected: in, then the usual error.

Automated: `PasswordResetTests`, `e2e/password-reset.spec.ts`.

## Scenario 2 — One answer when asking (Story 2; SC-002, SC-003)

Ask for a link for: an unknown address; `former@noor.example` (disabled); `new@shifa.example`
(invited); `owner@noor.example` twice within a minute. Expected: the same screen each time; the
inbox holds exactly one message, for the owner. Ask for the owner six times across more than six
minutes: five messages at most within the hour.

Automated: `ForgotPasswordAnswerTests` compares bodies byte for byte and mean response times
over 18 requests per case in each of up to three runs (difference under 5%) for all six cases — unknown, active, disabled, never activated, asked again within a minute, mail failing;
`ResetEmailLimitTests` moves the clock.

## Scenario 3 — Activate a new account (Story 3; SC-008)

1. Send the invitation to the local inbox:

   ```bash
   curl -X POST http://localhost:5080/v1/dev/staff-invitations -H "Content-Type: application/json" -H "X-Scs-Request: 1" -d '{"email":"new@shifa.example"}'
   ```

   (This address exists only when the environment is `local`.)

2. Open the link. Expected: «مرحبًا بك في عيادة الشفاء», her email, the 72-hour note.
3. Choose a password; press «تفعيل الحساب». Expected: the sign-in form with «فُعِّل الحساب».
4. Sign in with both steps. Expected: the landing page for «عيادة الشفاء».
5. Ask "forgot password" for an account that is still invited. Expected: nothing in the inbox.

Automated: `ActivationTests`, `e2e/activation.spec.ts`.

## Scenario 4 — Links that no longer work (Story 4; SC-004)

Open: the link of scenario 1 again (used); an older reset link after asking for a newer one
(replaced); `/set-password#t=nonsense`; `/set-password` with no token. Expected: «انتهت صلاحية
الرابط أو استُعمل» with «طلب رابط جديد». Open the link of scenario 3 again. Expected: the same
title, the new-account message, no button.

Automated: `PasswordLinkLifeTests` (30 minutes, 72 hours, used, replaced, disabled account,
altered token, double submission, expiry between opening and submitting).

## Scenario 5 — The rules (Story 5; SC-006)

On the reset screen submit `Short1!a`, then a password without a symbol, then the current
password. Expected: the matching line red each time, then the "different password" message; the
link still works afterwards. Send the same three straight to `POST /v1/auth/password/link/set`.
Expected: 400 with the codes of the contract.

Automated: `PasswordRulesEndpointTests`, unit tests of the rules on both sides.

## Scenario 6 — Temporary password (Story 6; SC-007)

1. Sign in as `temp@noor.example` with the seed password and the emailed code. Expected: the
   set-password screen with the temporary-password title — not the landing page.
2. Open <http://localhost:4201/> directly. Expected: back on that screen.
3. Call any endpoint that needs a session other than the two allowed. Expected: 403
   `PASSWORD_CHANGE_REQUIRED`.
4. Submit the seed password itself. Expected: refused.
5. Submit a new password. Expected: the landing page, same session.
6. Sign out and in again. Expected: no set-password screen.

Automated: `TemporaryPasswordTests` (including the 72-hour expiry answered as wrong credentials,
and counting toward the lock), `TemporaryPasswordGateTests` (every mapped endpoint),
`e2e/temporary-password.spec.ts`.

## Scenario 7 — Hand-over operations (Stories 3 and 6; SC-011)

Automated only (no screen until specs 015 and 026): `AccountHandoverTests` covers the three ways
of creating, sending the invitation again, the three ways of re-issuing, the email that never
contains a password, and `emailSent: false` when the mail sender fails.

## Scenario 8 — Screens in both languages (Story 7; SC-009)

`e2e/password-states.spec.ts` opens every state of the screens contract in Arabic and English and
checks the frame, the direction, the footer, the help window, and that a clinic's name appears
only on the new-account form.

## Scenario 9 — Nothing leaks (SC-010)

`SignInLogTests` (its second test): after running scenarios 1 to 6 against a server with captured logs, no
log line contains a password, a token, a link secret or an email address. In the browser, after
a link is opened, `location.href` contains no token.

## Scenario 10 — Isolation and gates

`Isolation/IdentitySamples.cs` has entries for `PasswordLink` and `AccountEmailRequest`; the guardrail of
spec 002 passes with no new crossing declared. A link issued for a clinic-A account cannot be
used to read or change anything of clinic B (`PasswordLinkLifeTests`, "a link of one clinic reads and changes nothing of another").
