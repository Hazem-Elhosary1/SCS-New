# Validation: Clinic Staff Sign-In and Sessions

## First pass — 10 Oct 2026

Run on the developer machine (Windows 11, Docker 28.3.3, SQL Server 2022 and Mailpit in
containers). Nothing was committed by this pass.

| Check | Result |
|---|---|
| `dotnet test src/server/Scs.sln` | 188 unit, 26 architecture, 314 integration: all pass |
| `dotnet format --verify-no-changes` | clean |
| `npm run ci` (lint, translations, unit tests, three builds) | pass; 72 core, 36 ui and 3 app unit tests |
| Formatting of the web code | clean |
| `npx playwright test` | 99 of 99 pass (24 sign-in flows, 45 screen states, 30 earlier) |
| By hand, against the real stack | See "Walked by hand" below |

### The assumption the plan could not prove (research R4)

**It holds.** The stored procedure `identity.usp_FindAccountForSignIn` runs as a user without a
login, and isolation honours that user's membership of the cross-clinic role. `SignInLookupTests`
(nine cases) shows: with no clinic the application sees no account; the lookup returns the
account's id and its clinic's id and nothing else; afterwards the same connection still sees no
account; the application and report logins cannot become that user or alter the procedure; the
user holds no permission of its own; one email has one account on the whole platform. The
fallback design was not needed.

### Success criteria

| Criterion | Evidence |
|---|---|
| **SC-001** both steps in under a minute | Walked by hand; the code email reached the local inbox in under a second. Delivery time in the cloud depends on the mail service, which is not chosen |
| **SC-002** one answer before a correct password | `SingleAnswerTests`: seven cases, bodies and headers identical. `SingleAnswerTimingTests`: unknown email 309.8 ms, invited 310.1 ms, wrong password 310.5 ms — a spread of **0.22%** against a limit of 5% |
| **SC-003** no session after step one | `SignInFlowTests`: 401 from the session address and no session row after a correct password |
| **SC-004** the lock | `LockoutTests`: the fifth wrong password locks for 15 minutes and ends the sessions; told only with the correct password; twenty at once lock once and lose no count; attempts more than 15 minutes apart do not add up |
| **SC-005** the code's limits | `SignInCodeTests`: wrong, expired, used, missing and malformed codes give one identical answer; the sixth try fails with the right code; twenty confirmations open one session; the third spent code in a row locks |
| **SC-006** an ended session is refused at once | `SessionEndingTests`: sign-out, expiry, lock, disable, ended for the account (three reasons), ended for the clinic (two reasons), suspended clinic at renewal |
| **SC-007** a reused renewal ends everything | `RenewalTests`: honoured within 30 seconds; after it, both sessions of the account ended and one security event |
| **SC-008** work is the session's clinic's only | `SessionIsolationTests`: two clinics signed in, twenty requests at once, each sees one clinic; a request naming another clinic is refused |
| **SC-009** the eleventh request | `OuterDefenceTests`: 429 with `Retry-After`, no account touched; renewals have their own allowance; a blocked network is refused before the limit is counted |
| **SC-010** every state in both languages | `e2e/sign-in-states.spec.ts`: 20 states × 2 languages, frame, mirrored panel, footer, at most one banner, no clinic name, no raw key |
| **SC-011** nothing private in the logs | `SignInLogTests`: passwords, codes, the email, and every cookie value sent or received — none in the captured output; signed-in entries carry `clinicId`, `accountId`, `sessionId` |
| **SC-012** later specs can build on it | `IStaffAccounts`, `IStaffSessions`, `ICurrentStaff`, `IClinicProfile` and `SignInAsAsync` exist and are exercised by tests |

### Walked by hand (Arabic, real API, real cookies, real mail catcher)

| Step | Seen |
|---|---|
| Open <http://localhost:4201> | The sign-in form; focus on the email field |
| `owner@noor.example` and the seed password | «التحقق بخطوتين», the email named, the countdown running |
| The code from <http://localhost:8025> | The landing page: «عيادة النور», the email, «تسجيل الخروج» |
| Reload, and open `/sign-in` again | Still signed in; taken to the landing page. `document.cookie` is empty: scripts cannot see the session |
| «تسجيل الخروج» | The plain form, no banner; `/` leads back to the form |
| More than ten sign-in requests in a minute | The warning banner on the code step |
| «تحتاج مساعدة؟» | The window, centred over a dimmed page, with the hours and three links (`tel:`, `wa.me`, `mailto:`) |

### Found by the tests and by using it, and fixed

- **The application could not record an attempt on an unknown email.** It may only insert into
  that table, and the ORM read the new row back. It is now a plain insert.
- **The three "wrong" cases differed in time by a few database calls.** The single answer now
  also takes a single minimum time (300 ms), which hides the difference and slows guessing.
- **The request-origin rule broke the front-end failure report of spec 001.** The header is now
  required only where a session is involved; an endpoint open to everyone that never touches a
  session is left alone.
- **Two route guards failed in the browser** (they asked for the router after waiting). Unit
  tests had not caught it; fixed and now covered by the browser tests.
- **The code boxes took only the first digit when a whole code arrived at once** (auto-fill).
  They now fill from wherever the digits land.
- **The help window opened at the top of the page**, not the centre.
- **The Windows start script read the env file in the wrong encoding**, so Arabic values (the
  support hours) arrived garbled. It now reads UTF-8.
- **An unexplained sign-out** was seen twice in the built-in browser a few seconds after signing
  in. The page itself made no such call (verified by tracing every call); it came from a stale
  copy of the page left from before the guard fix, reacting to the tab broadcast. It did not
  recur after a clean load. Noted here in case it is seen again.

### Differences from the plan and the task text

| Item | Difference | Reason |
|---|---|---|
| Services | `SignInService`, `SessionService`, `StaffAccounts` are in `Infrastructure/`, not `Application/` | They use the database context; the layering rule forbids that in `Application` (same as spec 002) |
| `SessionCookies` | In the shared project, not the module | The session handler in the shared project needs it |
| T009, research R12 | The email leaves **before** the code is stored, with no database transaction held open | A failed send then changes nothing, which is what FR-014 and FR-027 ask; a ten-second wait no longer holds a transaction |
| T022, T023 | One `scs-text-field` for text, email and password; buttons and links are directives on real `<button>` and `<a>` | Forms and the keyboard work as they always do |
| T075 | The dialog uses the browser's own `<dialog>`, not Angular CDK | It gives focus trapping, Escape and focus return without a new dependency (CDK is not installed) |
| T040 | 30 timed requests per case and run, not 1,000 | Each refusal now takes 300 ms by design, and ten-per-minute would be the limit; the spread measured is 0.22% |
| T045 | A third state, `read-only`, signs in like any other | No such state exists in the data yet (spec 016) |
| T057, T062 | Their cases are in `e2e/sign-in.spec.ts` | One file per screen area |
| T070 | The header is required on session endpoints, not on every changing request | See "Found … and fixed" |
| T078 | A separate `SignInLogTests` with a database, beside the existing leak test | The existing one runs without a database |

### Still open

| Item | Owner |
|---|---|
| T077: compare each drawn state with the design file "Smart Clinic OS — Design System & Screens" | Next pass. Layout was built from the BRD's description and the existing tokens |
| T086: the full hand walk of quickstart scenarios 2 to 5, 7 and 8 in both languages | Next pass. Each is covered by automated tests |
| T085: confirm the remaining defaults (hashing function; access-cookie life and grace; resend countdown; ten requests per minute when a team shares one address) and update the BRD lines SH-003, CL-SH-065, B-R1, B-3-1, SH-041 (15-minute window), the three-resend limit, the third-spent-code lock, cancelled treated as suspended | Product owner |
| Wording at page review: the disabled and suspended banners, the "no more resends" note, the too-many-requests banner, and all English text | Product owner |

### Not ready for production

Carried to spec 001 as tasks T127 to T129:

1. The key ring that protects session cookies is stored unwrapped; it must be wrapped with the
   cloud key service.
2. The cloud mail service is not chosen.
3. The sign-in request limit is counted per server instance.

## Second pass — 10 Oct 2026: convergence tasks T089 to T094

| Check | Result |
|---|---|
| `dotnet test src/server/Scs.sln` | 188 unit, 26 architecture, 316 integration: all pass |
| `dotnet format --verify-no-changes` | clean |
| `npm run ci` and the web formatting check | pass |
| `npx playwright test` | 103 of 103 pass |

| Task | What was done | Evidence |
|---|---|---|
| T089 (FR-051) | When the platform cannot be reached at the code step, the page says so instead of calling the code wrong; the digits and the button stay as they are | `e2e/sign-in.spec.ts` "the code is not called wrong"; a 21st screen state, in both languages, in `e2e/sign-in-states.spec.ts` |
| T090 | research.md (R5, R10, R12), `contracts/sessions.md` §6, the OpenAPI description and the settings table now say what was built: the 300 ms floor, where the request header is required, the email leaving before the code is stored, `SCS_MAIL__TIMEOUT_SECONDS` | The documents |
| T092 (US5/AC2, AC8) | A signed-in page is on screen; the person presses "try again"; the call is answered "session ended"; the form appears with the neutral notice and none of the landing page's text remains | `e2e/sign-in.spec.ts` "a page already on screen is cleared" |
| T093 (completes T088) | A mail server that accepts the connection and never answers fails the send in about a second with the one answer; so does a server that cannot be reached; nothing about the recipient, the code or the server is logged | `SmtpEmailSenderTests` (2 cases) |
| T094 (completes T086) | Walked by hand against the real stack in English and in Arabic | Below |

### Walked by hand in both languages (real API, real cookies, real mail catcher)

| Scenario | English | Arabic |
|---|---|---|
| 2 — one answer | Unknown email, wrong password and the never-activated account all show "The email or password is incorrect."; the email typed stays | Unknown email shows «البريد الإلكتروني أو كلمة المرور غير صحيحة.»; empty fields show «البريد مطلوب» and «كلمة المرور مطلوبة» |
| 3 — the lock | Five wrong passwords, each the same message; then the correct one: "The account was locked … It opens after 3:01 pm, or at once when the password is reset …", warning tone, time on one line. In the database: locked, count back to zero, five `wrong-password` events and one `account-locked` | — (covered in English and by `LockoutTests`) |
| 3 — disabled | "This account cannot sign in: it is disabled. Whoever manages users in the clinic can re-enable it." | «لا يمكن الدخول بهذا الحساب: هو معطَّل. يعيد تفعيلَه من يدير المستخدمين في العيادة.» |
| 3 — suspended clinic | "The clinic is suspended … Contacting the support team is the only way." with "Contact support", which opened the help window; no mention of an invoice | — |
| 4 — the code | Code step names the email, countdown from 0:59; a wrong code: red boxes and the one message, button still enabled; the right code: inside as "Al Shifa Clinic" | The same; a wrong code typed in Arabic-Indic digits was shown as `000000` and refused; the right code: inside as «عيادة النور» |
| 5 — sign-out | Back on the plain form, no banner | The same; opening `/patients/42` signed out leads to the form |
| 7 — request limit | Seen in the first pass: the warning banner on the code step | — |
| 8 — help window | "Need help?", the hours in English, `tel:`, `wa.me`, `mailto:` links, closes on × | Seen in the first pass, hours in Arabic |

Unlocking the account afterwards was done with the statement in `ops/runbooks/sign-in.md`, and the
made-up clinic was set back to active.

Not walked by hand, covered by automated tests only: resending a code after the countdown;
a session ended from elsewhere while a page is open (T092); renewal after five minutes.

### Still open after this pass

| Item | Owner |
|---|---|
| T091 (completes T077): compare each drawn state with the design file. It needs the export of the Figma sign-in page to be downloaded, which was not done without the product owner's say-so | Product owner to allow; then one pass |
| T085: confirm the remaining defaults and update the BRD lines listed in the first pass | Product owner |
