# Validation: Operator Sign-in (spec 010)

**Date**: 12 Oct 2026 · **Branch**: `010-operator-sign-in` · **Constitution**: v1.2.0, Principle VII

What was run, what was measured, what was walked on the real stack, what was laid beside the
design, what was built differently from the plan, and what was not done.

## 1. What was run

| Gate | Result |
|---|---|
| `dotnet test src/server/Scs.sln` (Debug) | unit **566** · architecture **58** · integration **748** — all passed |
| `npm --prefix src/web run ci` (lint, translation check, tools, unit tests, build of the three apps) | passed — clinic **68**, core **164**, ui **102**, ops **11**, patient 1; clinic's first download **456.86 kB**, ops **313.33 kB**, no size warning |
| `npx playwright test`, with pictures on | **467 passed**, none skipped, none on a second try (the run before it: see below) |

All three were run whole on the final code, after the fourth convergence (section 4e), one
after the other with nothing else running: the server's 566 · 58 · 748 is a fresh whole run.

The browser suite was run twice then. The first run had one failure and one test that passed on
its second try, both timing tests of the clinic's console and neither of this spec:
`clinic.spec.ts` "from asking the platform to the final page takes under half a second" (545 ms)
and `access.spec.ts` "not in your role within a second". Repeated five times each on their own,
the first passed every time and the second needed a second try twice; the whole suite run again
gave 467 passed. They are sensitive to what else the machine is doing and are left as they are;
the owner may want their limits looked at.

**Baseline (T001)**: the three suites were not run again before starting; nothing had changed
since spec 009's last run, whose figures are the baseline: 551 · 51 · 712; clinic 68, core 164,
ui 95; 397 browser tests and 4 skipped. New since: 15 unit, 7 architecture and 32 integration
tests on the server, then 3 more in convergence; 11 unit tests in the ops application and 7 in
the shared library; 72 browser tests for the console (47 of what it does, 25 of how it looks —
section 4a). Six browser tests of the old ops placeholder page
went with the page.

The unit suite was run again after its last file was added (566); the architecture and
integration figures are from the full run before that file, which touches neither.

New test files: `Operators/OperatorPiecesTests` (unit); `Operators/OperatorSignInTests`, `Operators/OperatorPasswordAndSessionTests`
(`OperatorLinkTests`, `OperatorSessionTests`, `OperatorProfileTests`),
`Fixtures/OperatorFixtures` (integration); `OperatorRulesTests` (architecture);
`projects/ops/src/app/ops.spec.ts`; `e2e/ops-platform.ts`, `e2e/ops-sign-in.spec.ts`,
`e2e/ops-console.spec.ts`, `e2e/ops-look.spec.ts`. The test files are grouped by subject, several classes to a file,
and not one file for each name in `tasks.md`.

**Existing tests that had to change**, and why:

- `RouteDeclarationTests`: a route may now declare `OperatorsOnly()`; a new case holds that
  every route under `/v1/ops/` is for operators or for anyone, never for a clinic's session,
  and that no route outside it is declared for operators.
- `RouteAuditDeclarationTests`: the fourteen changing routes of the console, each recording
  nothing in a clinic's trail.
- `IdentityRulesTests`: the rule "cookies are protected in one place" names a second file,
  `OperatorCookies.cs`.
- `ErrorCatalogueTests`: one more code, `STEP_EXPIRED`, 43 in all.
- `ScsOptionsValidatorTests`, `ApiFactory`: the console's address is required outside local.
- `PasswordGeneratorTests`: a `using`, since the password rule moved to the shared project.
- `e2e/placeholder.spec.ts`, `e2e/clinic.spec.ts`: the ops application is no longer a
  placeholder; its rows went, and the "never sends a clinic name" test now meets its sign-in.

**Failures on the way, all fixed**: a browser signing in for the first time was told "new
browser" twice, because its mark was made after the check; the account menu's first item was
focused before the menu was drawn; the address under a session's device was laid out at the
wrong edge.

## 2. What was measured

| What | Figure | Limit |
|---|---|---|
| On the real stack: the code's email in the local mailbox after the password | **0.2 s** | 30 s (SC-002) |
| On the real stack: an untouched console shown "signed in on another device" | **19 s** | 30 s (SC-005) |
| On the real stack: the invitation's email after the command | **35 s and 52 s** (two runs) | the job runs every minute |
| An unknown email, a wrong password, and a wrong password for a locked, a suspended and an invited account: a hundred tries each | every one of the five: median **305 ms**, 5th to 95th of a hundred **302–316 ms**; none under the 300 ms floor | cannot be told apart (SC-003) |

The idle sign-out at thirty minutes was not timed on the real stack; it is held by the
integration test with the clock moved, and by a browser test with the period shortened.

## 3. What was walked on the real stack

With the local API, SQL Server, the mailbox and the operators console, in browsers of its own,
with an operator invited for the walk (`quickstart.md`, "By hand").

| Step | Seen |
|---|---|
| 1. The first operator | an invitation for a mistyped address withdrawn — `withdrawn`, then `nothing-to-withdraw`, and no email ever went to it; the right one `invited`, its email «انضم إلى لوحة التشغيل» within a minute |
| 2. First password | «تعيين كلمة المرور» with the email shown; nine characters kept the form; saved: «تم تفعيل الحساب. سجّل دخولك الآن.»; the link again: «انتهت صلاحية الدعوة أو استُعملت من قبل…» |
| 3–4. Sign in | a wrong password and an unknown email: the same banner, word for word; right password, the code, inside as «منى يوسف» / «مسؤول عام» |
| Cookies | the session and device cookies HttpOnly, Secure; the session cookie dies with the browser when «تذكرني» is not ticked, and outlives it when it is |
| 5. The lock | five wrong, then right: «الحساب مقفل حتى ١٠:٤٦ ص بعد محاولات دخول خاطئة. إعادة تعيين كلمة المرور تفتحه الآن.»; a reset; signed in |
| 6. One session | the first browser, untouched, showed «تم تسجيل الدخول من جهاز آخر، فانتهت هذه الجلسة.» after 19 s |
| 7. A new browser | two browsers, two notices; the second browser signing in again, none |
| 10. Profile | this session «نشط الآن» / «الجلسة الحالية», the earlier one «انتهت منذ دقيقة واحدة»; the switch on and locked; a wrong current password said under its field; the change asked for a code and only then «تم تغيير كلمة المرور بنجاح», still inside |
| 11. Two tabs | "sign out of all devices": both tabs on the sign-in screen |
| 12. English, keyboard | the sign-in screen in English, left to right; filled and sent with the keyboard alone |
| 13. The other console | the operator's session on a clinic's address: 401; nobody signed in on the profile: 401 |
| The request limit | the walk's eleventh request in a minute was refused with "too many attempts", and went on a minute later |

Read in the database afterwards: one open session for the account, its earlier ones ended
`replaced` and `signed-out-everywhere`; a line in `ops.SignInRecord` for each event; no lock
left after the reset.

Step 9 (idle) was not walked: see section 2. The walk left its accounts (`walk10-…@heta.example`)
in the local database. The seeded operator, `operator@heta.example`, was not touched: it had
already been given a password from the preview.

## 4. Beside the design

`design-vs-real.png` in this folder: the twenty-four frames the design draws for this spec — twelve
in Arabic, twelve in English — each beside the application as built: sign-in, refused, the code,
forgot, sent, first password, the account menu, the profile, and change password open, wrong
and done. Every pair was opened.

## 4a. The look was not checked the first time — and what going over it found

This spec was first reported finished with every test passing while the change-password dialog
was cut off at its side and scrolled sideways. **The owner found it, not the tests.** The tests
then held what the screens say and do, not how they look; the pictures had been taken and not
all opened; and this section said "every other screen and state was pictured in both languages
and compared", which was not true. It is corrected here.

Done since: every screen and state of the console — 31 states, in Arabic and in English, at
1440 wide and at a phone's 390 — was pictured, and **every picture opened**: 62 wide ones, pair
by pair, and 62 narrow ones on contact sheets; then the ten drawn frames beside their screens.
`e2e/ops-look.spec.ts` now opens the same 31 states in both languages on every run and fails
when anything scrolls sideways, lies outside its frame or its container's padding, has its words
cut off, or is written against the edge of its box; and it holds where the design puts things.

Found by looking, and fixed:

| Found | Where | How it is held now |
|---|---|---|
| The change-password dialog cut off, scrolling sideways (the owner's report) | profile | no sideways scroll, every control inside the dialog |
| In Arabic, what is typed in an email or password box touched the box's edge | every form | "writes against the edge of its box"; seen failing with the fix taken away |
| In Edge, two eyes in a password box: the browser's own beside the field's (the owner's report) | the shared field, so the clinic too | a test run in Edge itself; seen failing with the fix taken away |
| The rule not met in the change-password dialog was marked but stayed grey | profile | its colour changes |
| At a phone's width the sixth code box ran out of the dialog | the shared code boxes | "runs into the padding of" |
| "Not available yet" stood bare, outside the frame of the other screens | privacy link | pictured in the frame |
| The lone "cancel" under the code, small and in a corner | change password | as wide as the button above it |
| Enter pressed straight after ↓ on the account closed the menu instead of choosing | top bar | the menu test, thirty runs |
| The profile held to 980 wide; its account card bunched at one end | profile | the cards fill the page; name first, button at the far end |
| The switch that is on had its knob at the left in Arabic | profile | at the right, as `2185:567` |
| The dialog 400 wide with a × and a lock in each field; its buttons small at one end | change password | 480, no ×, two buttons of one width, the saving one at the right; "done" 440 with one button across |
| No ✕ on the sign-in banner; no arrow on "back"; "check your email" not in the middle; the rules above the confirmation; no key on "change password"; no line in the account menu | several | each asserted in `ops-look.spec.ts` |

Not looked at: the four emails as a mail program shows them (their words are held by unit tests);
a dark theme (there is none); browsers other than Chromium, and Edge for the one test above.

| | Design | Built |
|---|---|---|
| Sign-in layout | brand panel half the screen, the mark at its top, title and line in its middle | the same — the panel was narrower and the mark beside the title, corrected here |
| Words | every drawn word, both languages | the same, letter for letter |
| Fields | plain, no eye | no leading mark; the show/hide eye kept (BRD B-5-8) |
| The way back on "forgot" | at the left edge in the Arabic frame, in the middle in the English one | in the middle in both, as "check your email" |
| The code boxes | filled from the right in Arabic; the button solid while the code is incomplete | filled from the left in Western digits, as the clinic (BRD CL-SH-086); the button pale until six digits |
| The dialog's field labels | set in from the fields' edge in the Arabic frame, at it in the English | at the fields' edge |
| The sign-in frame in English (`128:54`) | fields narrower than the button, "forgot password" beside the field | as the Arabic frame: fields as wide as the button |
| The refusal banner | a bar at its start edge, no border, the mark in a tinted circle | the shared banner: a border all round |
| Links | underlined | not underlined, as the shared link |
| Field labels | regular weight | semi-bold, as the shared field |
| "Email Address" / "Email address" | both spellings, by frame | "Email address" everywhere; "Email" under the profile's value |
| The invitation's line | "…as an Admin" | no role named, until roles exist (011) |
| The rules on the invitation frame | grey dots | the shared list, each turning to a tick |
| Before sign-in | no language control, no privacy link | both added (FR-054, FR-056) |
| Password rules on the invitation frame | eight characters | ten (FR-029) |
| Side bar | eight sections | «الرئيسية» only, until each is built (FR-049) |
| Top bar | a bell | none until its spec (FR-048) |
| A session's device | «Chrome على Windows» | «متصفح Chrome على Windows», the wording the clinic's notices already use |
| Change password | one step | a second step asks for the code (FR-045a, the owner's decision); not drawn |

States the design does not draw were built from the same parts with drafted words, the owner's
to confirm: the lock warning; a wrong code; the code that could not be sent; too many requests;
the link that no longer works; each reason a session ended; the confirmation of "sign out of
all devices"; the code step of changing a password; the home's greeting; not found; not
available yet; and the four emails.

## 4b. Convergence (T060–T068)

`/speckit-converge` read the spec against the code and found nine things; all are done.

| Task | What was wrong | What was done |
|---|---|---|
| T060 | Two sign-ins of one account finishing at the same moment: one failed with a server error (seen: 500) | the account's row is held first, so the second waits and ends the first's session; six rounds of two concurrent confirmations pass |
| — | Found by T060's test: a sign-in begun on a second device made the first device's code useless, against the spec's "the first may still finish" | a new code ends only the code it replaces, or another of the same session |
| T061 | Before sign-in the browser's language was never consulted | the ops application starts in the browser's language when none was chosen; Arabic when it speaks neither |
| T062 | A run of the deployment command that changed nothing left no record | recorded as `operator_command_changed_nothing`, with the outcome and who ran it, never the email |
| T063 | The five answers were timed over four tries, two cases | a hundred tries each of the five (section 2) |
| T064 | The English screens had not been laid beside their frames | all eleven were; see the table below for what changed and what stays |
| T065 | No test opened "no connection" or the profile while loading | both pictured and held in `ops-look.spec.ts` |
| T066 | The shared library's new options had no unit tests | seven, with the auth frame's panel that T012 asked for |
| T067 | Wrong tries older than fifteen minutes were forgotten, which the spec did not say | the owner kept the rule; it is written into FR-015 |
| T068 | The server suites had not been run since the look pass | all gates on the final code (section 1) |

From the English frames, changed: the dialog's heading at the start of its line, not the middle
(the Arabic frame has it at neither edge; the English one is clear); "Save" at the right in
both languages, as both frames draw it, with the keyboard meeting the buttons in the order they
are read; the account card's name, email and mobile sharing the card's room; the caption
"Email"; and in Arabic an email or a number beginning at the same edge as its caption.

The owner then found that "send again" on "check your email" showed nothing: it was a link
that did nothing for a minute. It now counts the minute down, says "sending", says that a new
link went, and says when one could not be sent; each state is pictured and held by a test.

## 4c. Second convergence (T069–T072)

A second reading found no fault in what the console does, and four things not yet done; all are.

- **T069, the real stack again.** With the API rebuilt, SQL Server, the mailbox and the console,
  and an operator invited for the walk:

  | Seen | |
  |---|---|
  | Two browsers gave the password before either code | each got its own code; the first browser's, sent before the second asked, still opened its session |
  | The second then completed | it holds the session; the first, untouched, showed «تم تسجيل الدخول من جهاز آخر، فانتهت هذه الجلسة.» after **20 s** |
  | An English browser that had chosen nothing | "Sign In", left to right; a French one: «تسجيل الدخول», right to left |
  | "Check your email" | no link to send again; the countdown ٠١:٠٠, then ٠٠:٥٥ five seconds on; the link back **61 s** after the first sending; pressed: «أرسلنا رابطًا جديدًا.» and the countdown again; **two** reset emails in the mailbox |
  | The command run for the now-active account | `already-active`, then `not-invited` for a withdrawal; two lines `operator_command_changed_nothing` in `ops.SignInRecord` with the outcome and the machine user, and the email in none |
  | In the database afterwards | one open session for the account, the other ended `replaced` |

  The walk's picture showed a long email split over two lines at its hyphen; an email named in
  a sentence now stays whole, and a test holds it.
- **T070.** A session that ends while the change-password dialog is open, at its form and at
  its code: the dialog goes with the page, the sign-in screen says why, nothing was changed.
- **T071.** Frames `1849:2791` and `1850:2824` opened: the profile with a switch the operator
  may turn. They differ from what is built in that alone — here the switch is on and locked,
  with its line (FR-008, FR-042) — and are in the picture.
- **T072.** `contracts/operator-screens.md`, `data-model.md` and `quickstart.md` say what is
  built: the four states of "send again", the dialog's layout, the language before sign-in,
  and that a sign-in begun in another browser leaves a standing code good.

## 4d. Third convergence (T073–T075)

Every acceptance scenario of the eleven stories was read against the code and the tests. No
fault in what the console does; three things were not yet proven or marked:

- **T073.** A request in flight had never been opened or pictured. On each of the six forms —
  sign in, its code, forgot, choosing a password, saving a change of password, its code — the
  button is locked, shows its turning mark and its words, keeps its size, and a second press or
  Enter sends nothing more; pictured in both languages, wide and narrow, and every pair opened.
- **T074.** The side bar's current page was marked for the eye alone; it now carries
  `aria-current="page"`, and only while it is the page shown.
- **T075.** The code of a change of password, ten minutes on: refused, the password unchanged,
  the session open, and the change can be begun again.

## 4e. Fourth convergence (T076, T077)

- **T076.** A fault of my own making, in what a screen reader hears: when the countdown was
  added to "send again", the whole block was made a live region, so its seconds — changing
  every second — would have been spoken sixty times a minute, and a button sat inside a status.
  Now only what happens is announced, "sending" and "we sent a new link", in a status that holds
  nothing else; the countdown and the link are read when reached, as on the code step. A test
  holds that neither is in a live region and that the two messages are. Not heard with a real
  screen reader: held by the markup.
- **T077.** The whole server suite run again: 566 · 58 · 748.

## 5. Built differently from the plan

- **The invitation is sent by the platform, not by the command.** `operator invite` notes that
  an invitation is owed; a job of the platform makes the link and sends it within a minute, and
  tries again while the email cannot leave. The command then needs neither the mail server nor
  the keys that protect a link, and never refuses for want of mail settings (research R9 said it
  would send, and refuse without them). `ops.Operator` gains `InvitationOwedSinceUtc`.
- **No new error codes but one.** The contract's eight `OPS_*` codes became the platform's
  existing ones (`INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `ACCOUNT_DISABLED`, `CODE_INVALID`,
  `LINK_INVALID`, `SIGN_IN_CODE_NOT_SENT`, `RATE_LIMITED`, `VALIDATION_FAILED` with its field) and
  one new, `STEP_EXPIRED`. The contract was brought in line.
- **One declaration, not two.** Every route for a signed-in operator declares `OperatorsOnly()`;
  there is no separate "own account" for operators, since no route takes an account's
  identifier.
- **An unknown email** leaves a line in `ops.SignInRecord` with no account, not a row in
  `security.AnonymousSignInAttempt`: that table is another module's.
- **A session's device** is kept as the browser's own description and worded when read; there
  are no separate "browser" and "system" columns.
- **A record is protected by a trigger**: no change, and no removal before thirty days; the
  clean-up removes by age far beyond that.
- **`ChallengePurposes`**: the challenge table serves a sign-in and a change of password, as
  the security decisions asked.
- **`OperatorPasswords.cs`, `OperatorEndpoints.cs`**: one file each, where the plan named
  several.
- **The shared auth frame has no unit test of its new panel** (T012 asked for one): its
  layout and words are held by the console's browser tests.
- **The clinic's session code is switched off in the ops application** by one line in `core`,
  and `provideScsCore` takes an application's own interceptors.
- **The shared `ui` library gained options, all off by default**, so the clinic's screens keep
  their look: a field without its leading mark; a banner that can be closed; a dialog with its
  × or without, a width, and no heading at all. Two changes reach the clinic as well,
  both repairs: Edge's own eye and × are no longer drawn inside a field, and the six code boxes
  give way in a place too narrow for them. The clinic's 68 unit tests and its browser tests pass
  unchanged.

## 6. Not done, and for the owner

- **Suspended and role-changed** cannot be reached through the product until spec 011; both
  are tested by setting the state directly, and their lines on the sign-in screen are in place.
- **Blocking an address** (025) and **the settings screen** (024): their places are kept.
- **Nobody is alerted** to a lock or a reused session value until specs 023 and 025: they are
  read from the table (`ops/runbooks/operators.md`).
- **The only operator who loses both password and mailbox** has no way in through the product;
  the runbook says what is done.
- **The request limit is for an address**, so operators behind one office address share ten
  requests a minute across the addresses before sign-in (security checklist CHK012); it was met
  on the walk.
- **A password is not checked against known leaked passwords** (CHK020).
- **`checklists/security.md`**: thirty-four items, unchecked; they are the reviewer's. Six were
  settled before implementation and are in the spec's Clarifications.
- GitHub cannot run the checks while the account is locked for billing; the figures above are
  from this machine.
