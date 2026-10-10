# Validation: Passwords and Account Activation

## First pass — 10 Oct 2026

Run on the developer machine (Windows 11, Docker, SQL Server 2022 and Mailpit in containers).
Nothing was committed by this pass.

| Check | Result |
|---|---|
| `dotnet test src/server/Scs.sln` | 235 unit, 26 architecture, 380 integration: all pass |
| `dotnet format --verify-no-changes` | clean |
| `npm run ci` (lint, translations, unit tests, three builds) | pass; 101 core, 40 ui and 10 app unit tests |
| Formatting of the web code | clean after `prettier --write` on nine files |
| `npx playwright test` | 187 of 187 pass (84 for this spec, 103 earlier) |
| By hand, against the real stack | See "Walked against the real stack" |
| Against the design file | See "Compared with the design file" |

### Success criteria

| Criterion | Evidence |
|---|---|
| **SC-001** back in without help; the email within 30 s | Walked: the reset email reached the local inbox within the same second the job ran, a few seconds after asking (the job server polls every 5 s). Delivery time in the cloud depends on the mail service, which is not chosen |
| **SC-002** one answer when asking | `ForgotPasswordAnswerTests`: seven cases (unknown, active, asked again at once, invited, disabled, locked, mail failing) — status, headers and body identical; no answer sooner than the 300 ms floor. Timed over all six cases after the convergence pass: unknown 309.6 ms, active 309.2, disabled 309.7, never activated 309.7, asked again within a minute 309.3, mail failing 309.2 — a spread of **0.19%** against a limit of 5% |
| **SC-003** mail only to an active account, within the caps | `ForgotPasswordAnswerTests`, `ResetEmailLimitTests`: one a minute, five an hour, the last link still works; ten requests at once and two job runs at once produce one email and one live link |
| **SC-004** a link works once and never late | `PasswordLinkLifeTests`: 30 minutes, 72 hours, used, replaced, disabled account, password set another way, made-up and altered tokens, two submissions at once, expiry between opening and submitting |
| **SC-005** a reset ends everything else | `PasswordResetTests`: both other sessions refused at their next request, the lock lifted, the old password refused, no session and no cookie from the reset itself |
| **SC-006** the server enforces the rules | `PasswordRulesEndpointTests`: nine refusals with exactly the contract's codes, over-long, same-as-current; the link stays live each time. The same table of 15 passwords runs against the server's rules and the screens' own check |
| **SC-007** nothing before the change | `TemporaryPasswordGateTests`: every mapped endpoint that needs a session answers `PASSWORD_CHANGE_REQUIRED` except the two marked; renewal and sign-out still work. `TemporaryPasswordTests`: expiry after 72 hours answered as wrong credentials and counted toward the lock |
| **SC-008** invitation to signed in | `ActivationTests`, `e2e/activation.spec.ts`. Walked up to the new-account form (not submitted, to leave the seed account for the owner to try) |
| **SC-009** every state in both languages | `e2e/password-states.spec.ts`: 28 states × 2 languages — frame, mirrored panel, footer, at most one banner, never two password fields, a clinic's name on the new-account form only, no raw key |
| **SC-010** nothing leaks | `SignInLogTests` (second test): no password, token, generated password or email address in any log line or in the job store; the two new tables hold hashes only. `e2e/password-reset.spec.ts`: no token in the address, history or storage after a link opens |
| **SC-011** later specs can hand over accounts | `AccountHandoverTests`: three ways of creating, sending the invitation again, three ways of re-issuing |

### Walked against the real stack

Local API (restarted on this branch, migration applied), clinic app, Mailpit.

- **Forgot password**: asked for `reception@noor.example` from the app; the result screen showed;
  the job sent one email. The owner then completed that reset from their own browser, signed in,
  and did the whole flow again for `owner@noor.example` — recorded in the events as
  `reset-requested → reset-completed → signed-in` for both. Those two seed accounts now have the
  passwords the owner chose.
- **Invitation**: `POST /v1/dev/staff-invitations` for `new@shifa.example` → the email arrived;
  the live API described the link (`new-account`, both clinic names); the page showed «مرحبًا بك
  في عيادة الشفاء», the address bar held no token, the rule lines reacted, and submitting with
  unmet rules made no call. Not submitted: the account is still `invited`.
- **Temporary password**: signed in as `temp@noor.example` with both steps → landed on
  `/change-password`; opening `/` came back to it; the same password was refused with its message
  (400 from the live API); signed out. Not changed: the account still holds its temporary
  password.

### Measured times (T064, T069)

Taken from the event rows of the walk above (the owner's own two resets, in a real browser):

| What | Measured |
|---|---|
| Asking for a link → the email in the inbox | under 1 second both times (asked 16:18:12, message stamped 16:18:12.5) |
| **SC-001** asking → signed in with the new password, both steps | 57 seconds the first time (`reception@noor.example`), 18 seconds the second (`owner@noor.example`) — against a limit of three minutes |
| Invitation requested → the email in the inbox | within the same second |
| **SC-008** opening the invitation → signed in | **not measured**: the only seeded account that was never activated was left unactivated for the owner to try, and no screen creates another until spec 026. The steps after the email are the same as for a reset, which took 18 to 57 seconds |

### Compared with the design file

The owner reported on 10 Oct 2026 that icons were missing and the sign-in states did not match
the design. That was true: the screens of spec 003 and the first build of this spec were made
from the BRD's wording without reading the design file, and the automated tests check behaviour,
wording, direction and language — not appearance. The comparison tasks (T077 and T091 of spec
003, T063 here) had been left open instead of done.

Done in this pass, reading the page "✓ Sign in" of the clinic design file through the Figma
connector (frames Default, Account Locked, Password Changed, OTP Error, Forgot Password Sent,
Reset Password Error, Reset Link Expired, in Arabic, and Default in English):

| Was | Now, as drawn |
|---|---|
| No icons anywhere | A shared `scs-icon` (mail, lock, eye, warning, check, help, back arrow); mail and lock inside the fields; an icon on every banner by tone; a check or warning circle on each password rule |
| A text link "English" | The segmented language toggle «عربي · EN» in the outer top corner |
| Introduction panel: a plus sign, three lines | The mark «ع», the badge «نظام تشغيل العيادة الذكي», the 34-point headline and the paragraph, with the design's English wording |
| Fields 44 high, 6 radius | 48 high, 10 radius, subtle shadow, 15-point text |
| Buttons 44 high, 6 radius | 48 high, 10 radius, 15 semibold |
| Banners: coloured text, no icon | Tinted background, toned icon at the start, quiet text; a bold title on the two success banners |
| "Request sent" and dead-link screens: plain text at the start | Centred, with the round mark above the title, as drawn |
| Back links without an arrow | An arrow that points back in either direction |
| Code boxes bunched in the middle | Spread across the form, 10 radius |

Kept on purpose although the design file does not draw them: the links to the privacy policy and
the terms beside the help link (BRD, Part D — "two new pages not drawn yet").

### Second pass of the comparison — 10 Oct 2026

The owner asked for the rest. The browser tests can now save a 1440x900 picture of every state
(`SCS_SHOTS=<folder> npx playwright test sign-in-states password-states`: 96 pictures, 48 states
in two languages). They were laid beside the frames Validation, Clinic Suspended, Code Resend
Failed, OTP Verified and Contact Support, and these further differences were closed:

| Was | Now, as drawn |
|---|---|
| A field in error: red border only | The warning mark inside the field, at its end |
| The banner's action in link colour, underlined | In the banner's own tone, bold |
| "Resend the code" button without an icon | With the refresh icon |
| A busy button only changed its words | A small ring turns before the words |
| Code step: the countdown above the confirm button | Confirm first, then the resend line, then the back link |
| Help window: ruled header, bordered rows, label then value | No rule, softer corners; each row a grey card with a round mark (phone, chat, mail), the value above its label |
| English sign-in wording of my own | The design's: "Staff sign-in", "Forgot password?", and the two lines under them |

Looked at and found in line with the design: Default, Session Ended, Filled, Password Shown,
Validation, Error, Request Failed, Code Not Sent, Account Locked, Account Disabled, Clinic
Suspended, Network Blocked; the four code-step frames; the seven reset frames; the four
set-password frames; the support window — in Arabic — and Default, Account Locked, the code
error, the new-account form, the dead link and "request sent" in English.

**What this comparison is not**: it was done by eye on pictures at reduced size, not by
measuring spacing to the pixel, and the remaining English frames were not opened one by one.
The temporary-password screen has no frame to compare with (page review, B-O19).

### Owner decisions during the comparison — 10 Oct 2026

- **Sending the code again is a button, not a text link.** When the countdown is over the code
  step shows the secondary button «إعادة إرسال الرمز» with the refresh icon — the same button
  that follows a failed resend. The text link «لم يصلك الرمز؟ إعادة الإرسال» is gone. This
  differs from the BRD (B-5-2, B-5-3, CL-SH-088: "a countdown, then a link") and from the
  design's "OTP Verification Error" frame, which both show the link; both are to be updated.
  Covered by `e2e/sign-in.spec.ts` (countdown → button → new countdown; failed resend; no
  resends left).
- **The secondary button** everywhere: dark text and icon, medium weight, light border, subtle
  shadow, as the "Code Resend Failed" frame draws it.
- **An empty password field** shows eight dots, as the Default frame draws it.

### Changed from the plan

- The migrator verb `send-link` became a local-only address, `POST /v1/dev/staff-invitations`:
  the migrator has no mail sender and no key ring.
- Validation refusals answer 400, as everywhere in this project; the first draft of the
  contract said 422.
- The link token class lives in the shared project beside `SessionCookies` (the architecture
  rule that only one place creates a protector now allows these two files).
- Dead links are kept 21 days before cleanup, not 7: longer than any life a link can be
  configured to have.
- The success banners reach the sign-in form through the in-memory hand-over the code step
  already used, not through navigation state.

### Open

- **T063** done by eye in two passes (above); a pixel-level review by the designer is still worth having.
- **T065** owner items: the five decisions to add to the BRD; the wording of the temporary-password
  screen, the "different password" message and three emails (page review, B-O19); the staff
  app's address per environment (`SCS_AUTH__STAFF_APP_URL`).
- Spec 003's T077/T091 are covered by the same comparison and have the same remainder.

## Second pass — 10 Oct 2026: convergence tasks T066 to T071

| Task | Result |
|---|---|
| T066 timing over all six cases | Done: spread 0.19% (figures in the SC-002 row above) |
| T067 unit specs for the three pages | Done: `password-pages.spec.ts`, 12 tests — the clinic app now has 20 unit tests |
| T068 quickstart and research in line with the code | Done |
| T069 the two measured times | **Half done**: SC-001 is recorded from the owner's own two resets (57 s and 18 s). SC-008 is not measured — see "Measured times" |
| T070 spec 003's documents | Done: its screens contract, tasks T077 and T091, and a third pass in its validation record |
| T071 the picture option | Done: documented in `docs/setup.md` |

Checks after this pass: the two tests of `ForgotPasswordAnswerTests` pass; clinic unit tests 20
of 20; lint and the translation check clean. No product code changed in this pass, so the full
server and browser suites were not run again (last full runs: 380 integration, 187 browser, all
passing).
