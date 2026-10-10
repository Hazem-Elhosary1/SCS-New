# Feature Specification: Clinic Staff Sign-In and Sessions

**Feature Branch**: `003-clinic-sign-in`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: none typed. Taken from row `003-clinic-sign-in` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "email and password, the email code,
the lock, the refusal cases, sessions and ending them."

**BRD source**: Volume 1 (Clinic Console), Chapter 01, Part B "Sign-in" — flows B-5-1, B-5-2,
B-5-3, B-5-8, B-5-9, B-5-10, B-5-11; rules B-R1 to B-R11 and B-R22 to B-R29; requirements
CL-SH-065 to CL-SH-095, CL-SH-115 to CL-SH-126, CL-SH-191 to CL-SH-195. Part A flows A-5-7
(sign-out) and A-5-10 (session ended while working). Volume 0 (Shared Foundation) §6 — SH-040,
SH-041, SH-042, SH-043, SH-045, SH-047, SH-049; also SH-030 and SH-034 (clinic states) and SH-094
(no personal data in logs).

**Depends on**: 002-clinic-tenancy-isolation.

**Open dependency on the BRD**: SH-003, CL-SH-065 and rule B-R1 say the clinic is known from a
per-clinic sign-in address. The constitution (v1.1.0, owner decision of 10 Oct 2026) replaces
that with one staff address for all clinics, `app.scs-heta.com`, on which the clinic is never
taken from the address (spec 002, FR-009). This spec follows the constitution; how the clinic is
then known at sign-in was decided by the product owner on 10 Oct 2026 (see Clarifications). SH-003, CL-SH-065 and B-R1 must be updated
to match before this spec is approved (spec 001, task T110; spec 002, task T061).

## Overview

Until now nobody can sign in: spec 002 keeps clinics apart, but the system has no way to know
who is asking. This spec lets a member of a clinic's team prove who they are and work for their
clinic, and makes sure that proof can be withdrawn at once.

It delivers three things:

1. **Signing in, in two steps at every sign-in**: email and password, then a six-digit code sent
   to that email. No session exists until the code is accepted.
2. **Refusing safely**: before a correct password the server gives one identical answer whatever
   is wrong, so that nobody can learn whether a clinic or an account exists. After a correct
   password it says plainly why the person cannot enter and what to do.
3. **Sessions that can be ended immediately**: by the person (sign-out), by the system (lock,
   expiry, a stolen session), and through operations that later specs put behind screens
   (disable an account, end a user's sessions, end everyone's sessions, suspend a clinic).

It also fulfils what spec 002 left ready: from now on, staff work is done for the clinic of the
signed-in account, and for no other.

**Out of scope** (each has its own spec): "forgot password", setting a password from a link,
activating a new account, and changing a temporary password at first sign-in (004); permissions
and roles, and what a signed-in person may do (005); the console's bars, account menu and home
page (006); the audit trail (007); the notifications engine and email templates in general
(008); operator sign-in (010); the states of a clinic's subscription and who changes them (016);
the support channels and other platform settings screen (024); the operators' security events
page and blocking a network (025); creating, disabling and unlocking accounts and the "user's
sessions" screen (026); the clinic's activity and security log page (059); the "end everyone's
sessions" button and the idle-timeout setting (085); the privacy policy and terms pages (086);
patient sign-in by mobile number (070); the hidden bot check (SH-042, later).

## Clarifications

### Session 2026-10-10

- Q: All clinics share one staff address and the form has only email and password; how does the system know which clinic a person is signing in to? → A: One email, one clinic — an email address has at most one staff account on the whole platform, so the account decides the clinic. A person who works at two clinics uses a different email at each.
- Q: (from analysis, 10 Oct 2026) Should repeated wrong codes ever lock the account? → A: Yes — three codes spent in a row lock it for 15 minutes.
- Q: (from analysis) How soon must a suspended clinic's sessions stop? → A: At the next renewal at the latest (five minutes), and at once when spec 016's suspend operation ends them.
- Q: (from analysis) Keep the platform switch that turns the email code off? → A: Yes, and build its behaviour.
- Q: How many times may a person ask for the sign-in code to be sent again during one sign-in before they have to go back and type their password again? → A: Up to 3 resends per sign-in; after that the resend link is replaced by a note to go back and sign in again.
- Q: When a clinic's subscription has been cancelled (not just suspended), what should its team see when they sign in with the correct password? → A: Refused, with the same banner as a suspended clinic (contact support).
- Q: If a team member types a wrong password a few times, stops, and comes back later, should those earlier wrong attempts still count towards the five that lock the account? → A: No — wrong attempts are forgotten after 15 minutes without a new wrong attempt.
- Q: How long may a team member stay signed in before they must sign in again with password and email code, even if they are working the whole time? → A: 12 hours — one sign-in per shift; a platform setting.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A team member signs in with email, password and an email code (Priority: P1)

A receptionist opens the staff address. She sees the sign-in form, types her email and password
and presses «تسجيل الدخول». The button reads «جارٍ تسجيل الدخول…» until the server answers. A
six-digit code arrives in her email; the screen now reads «التحقق بخطوتين» and names the email
the code was sent to. She types the code, presses «تأكيد الرمز», sees «الرمز صحيح، وسُجّل
الدخول.» and is taken inside. From that moment everything she does is done for her clinic only.

**Why this priority**: nothing else in the product can be used by a person until this works, and
it is the point at which spec 002's isolation becomes tied to a real identity.

**Independent Test**: with a made-up active account in an active clinic, complete both steps and
reach the signed-in landing page; confirm that no session existed after the first step, and that
data read after sign-in belongs to that account's clinic only.

**Acceptance Scenarios**:

1. **Given** an active account in an active clinic, **When** she types her correct email and
   password and presses «تسجيل الدخول», **Then** the button reads «جارٍ تسجيل الدخول…» and is
   locked, then the «التحقق بخطوتين» screen appears, a code is in her email, and no session
   exists yet.
2. **Given** both fields are empty, **When** she presses the button, **Then** «البريد مطلوب» and
   «كلمة المرور مطلوبة» appear under the fields, with no banner and no call to the server.
3. **Given** an email without `@`, **When** she presses the button, **Then** «البريد غير صحيح —
   مثل name@clinic.com» appears under the email field.
4. **Given** a standing code, **When** she types the correct code and presses «تأكيد الرمز»,
   **Then** the six boxes turn green, «الرمز صحيح، وسُجّل الدخول.» is shown, the button reads
   «جارٍ فتح الرئيسية…», she is taken inside, and a sign-in event is recorded.
5. **Given** she is signed in, **When** she opens the sign-in address again, **Then** she is
   taken inside without being asked to sign in.
6. **Given** nobody is signed in, **When** any page of the console is opened, **Then** the
   sign-in form is shown instead.
7. **Given** a clinic in the read-only state, **When** a member signs in with correct details and
   a correct code, **Then** she enters as usual.
8. **Given** two clinics each with a signed-in member, **When** each reads clinic-owned data,
   **Then** each receives only her own clinic's records.
9. **Given** a password is typed and hidden, **When** she presses the eye icon, **Then** the
   password is shown as text and the icon becomes a crossed eye; pressing again hides it. Nothing
   is sent.

---

### User Story 2 - Nothing is revealed before a correct password (Priority: P1)

Someone who does not belong to a clinic tries emails and passwords on the sign-in form. Whatever
is wrong — the email is unknown, the password is wrong, the account is locked or disabled or was
never activated, the clinic is suspended — the answer is the same sentence, in the same form, in
the same time: «البريد الإلكتروني أو كلمة المرور غير صحيحة.». Only a person who has typed the
correct password is told that the account is locked or disabled, or that the clinic is
suspended.

**Why this priority**: it is a rule of the constitution (SH-043). A sign-in form that confirms
which emails are registered hands an attacker the list of a clinic's staff.

**Independent Test**: send the first step with an unknown email, a wrong password, and a wrong
password for a locked, a disabled, a not-yet-activated account and an account of a suspended
clinic; compare the answers and their timing. They cannot be told apart.

**Acceptance Scenarios**:

1. **Given** an existing account, **When** a wrong password is sent, **Then** the banner «البريد
   الإلكتروني أو كلمة المرور غير صحيحة.» appears between the title and the fields, both fields
   keep what was typed, and the button is enabled.
2. **Given** an email that belongs to no account, **When** any password is sent, **Then** the
   same banner appears, letter for letter.
3. **Given** a locked account, **When** a wrong password is sent, **Then** the same banner
   appears, not the lock banner.
4. **Given** a disabled account, **When** a wrong password is sent, **Then** the same banner
   appears.
5. **Given** an invited account that was never activated, **When** any password is sent,
   **Then** the same banner appears, and the line «حساب جديد؟ افتح الرابط في بريدك لتعيين كلمة
   المرور.» is under the form as always.
6. **Given** an account of a suspended clinic, **When** a wrong password is sent, **Then** the
   same banner appears, not the suspension banner.
7. **Given** any of the cases above, **When** the answers are compared, **Then** they are
   identical apart from the request reference, and their response times cannot be told apart.
8. **Given** the server cannot be reached, **When** she presses the button, **Then** the banner
   reads «تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.» and the button is enabled.

---

### User Story 3 - Wrong attempts lock the account, and the person is told what to do (Priority: P1)

A doctor mistypes his password five times. The fifth wrong attempt locks his account for 15
minutes and ends any session he had open elsewhere. When he next types the correct password he
is told: «أُقفل الحساب بعد محاولات دخول فاشلة. يُفتح بعد الساعة ‹وقت الفتح›، أو فور إعادة تعيين
كلمة المرور من «نسيت كلمة المرور؟».» A person whose account was disabled, or whose clinic is
suspended, is likewise told so — but only after a correct password — and told the way out.

**Why this priority**: the lock is the defence against guessing passwords (SH-041), and a
refusal that does not say what to do sends people to the support line.

**Independent Test**: fail five times, confirm the account is locked and its sessions ended;
sign in with the correct password and read the lock banner with its time; wait out the lock, or
have it lifted, and sign in.

**Acceptance Scenarios**:

1. **Given** four earlier wrong attempts, **When** the fifth is wrong, **Then** the usual error
   banner is shown, the account is locked for 15 minutes, and its live sessions are ended.
2. **Given** a locked account, **When** the correct password is sent, **Then** the lock banner is
   shown as a warning with the time it opens, and no code is sent.
3. **Given** a locked account, **When** 15 minutes have passed, **Then** the person signs in
   normally.
4. **Given** a locked account, **When** the lock is lifted through the unlock operation,
   **Then** the person signs in without waiting.
5. **Given** a disabled account, **When** the correct password is sent, **Then** a danger banner
   says the account is disabled and that whoever manages users in the clinic can re-enable it; it
   does not name who disabled it, and no code is sent.
6. **Given** an account in a suspended clinic, **When** the correct password is sent, **Then** a
   danger banner says the clinic is suspended and that contacting support is the only way, with
   the action «التواصل مع الدعم»; it says nothing about paying an invoice, and no code is sent.
7. **Given** the suspension banner is shown, **When** «التواصل مع الدعم» is pressed, **Then** the
   «تحتاج مساعدة؟» window opens.
8. **Given** a correct password after earlier wrong attempts, **When** the first step succeeds,
   **Then** the count of wrong attempts returns to zero.
9. **Given** the lock banner shows a time, **When** it is displayed in either language, **Then**
   the time and its AM/PM mark stay together on one line.

---

### User Story 4 - The code is short-lived, limited and can be sent again (Priority: P2)

The code does not arrive, or the user mistypes it. A wrong code, a code older than 10 minutes and
a code that was tried five times all get the same answer: the boxes turn red and «الرمز غير صحيح
أو انتهت صلاحيته. يمكنك طلب رمز جديد.». Under the boxes a countdown reads «إعادة الإرسال خلال
‹د:ث›»; when it ends it becomes «لم يصلك الرمز؟ إعادة الإرسال». A new code replaces the old
one. At any time «الرجوع لتسجيل الدخول» returns to the form.

**Why this priority**: the second step is what stops a stolen password from being enough
(SH-040); its limits are what stop the code itself from being guessed.

**Independent Test**: reach the code screen and try a wrong code, an expired code, a sixth
attempt, a used code and a code replaced by a resend; each is refused with the one message.
Resend after the countdown and sign in with the new code.

**Acceptance Scenarios**:

1. **Given** a standing code, **When** a wrong code is typed, **Then** the boxes turn red, the
   banner «الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.» is shown and the button is
   enabled.
2. **Given** 10 minutes have passed, **When** the correct code is typed, **Then** the same
   message is shown.
3. **Given** five wrong attempts, **When** the correct code is typed on the sixth, **Then** the
   same message is shown; the code is spent.
4. **Given** a code that was used to sign in, **When** it is sent again, **Then** it is refused.
5. **Given** the countdown has not ended, **When** she looks, **Then** there is no resend link
   and the countdown decreases.
6. **Given** the countdown has ended, **When** she presses «لم يصلك الرمز؟ إعادة الإرسال»,
   **Then** a new code is in her email, a new countdown starts, and she did not retype her
   password.
7. **Given** a code was resent successfully, **When** she types the earlier code, **Then** it is
   refused with the same message.
8. **Given** the email service is failing, **When** she presses resend, **Then** the banner reads
   «لم نستطع إرسال رمز جديد. الرمز السابق يبقى صالحًا حتى تنتهي مدّته.», «تأكيد الرمز» stays
   enabled, a secondary button «إعادة إرسال الرمز» appears, and the earlier code still works.
   A failed resend does not use up one of the three.
9. **Given** the email service is failing at the first step, **When** correct details are sent,
   **Then** the sign-in form shows «لم نستطع إرسال رمز التحقق إلى بريدك. حاول مرة أخرى بعد
   قليل.», the button is enabled, and the code screen is not shown.
10. **Given** the code screen in any state except success, **When** she presses «الرجوع لتسجيل
    الدخول», **Then** the sign-in form is shown.
11. **Given** the account was disabled, or its clinic suspended, after the first step, **When**
    the correct code is typed, **Then** no session is opened and she is returned to the sign-in
    form with the banner of that state.
12. **Given** the code boxes in Arabic or English, **When** digits are typed, **Then** they are
    entered left to right in Western digits, and focus moves to the next box.

---

### User Story 5 - A session ends when it should, and at once (Priority: P2)

A team member presses «تسجيل الخروج» and is back on the plain sign-in form. Separately, a manager
disables an account while its owner is working: at the owner's very next action the screen is
cleared and the sign-in form appears with the neutral notice «انتهت الجلسة. الدخول من جديد
للمتابعة.». The notice never says why, or who ended it.

**Why this priority**: a person who has left the clinic, or a laptop that was stolen, must lose
access immediately, not when a timer runs out.

**Independent Test**: sign in on two devices; end the sessions by each cause in turn (sign-out,
disable, lock, end user's sessions, end everyone's sessions, clinic suspended, expiry) and
confirm the very next request from an ended session is refused and leads to the sign-in form
with the right notice or none.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** she signs out, **Then** only that session ends, she
   sees the sign-in form in its default state with no banner, and her session on another device
   continues.
2. **Given** a signed-in person, **When** her session is ended by anything other than her own
   sign-out, **Then** her next action clears what is on screen and shows the sign-in form with
   «انتهت الجلسة. الدخول من جديد للمتابعة.».
3. **Given** an account with live sessions, **When** the account is disabled, **Then** all its
   sessions end.
4. **Given** several accounts with live sessions in one clinic, **When** "end everyone's
   sessions" is used by one of them, **Then** every session in the clinic ends except the one
   that asked.
5. **Given** a clinic with live sessions, **When** the clinic becomes suspended, **Then** none of
   its sessions can continue, and signing out is still possible.
6. **Given** a session that has reached the end of its life, **When** it is used, **Then** it is
   refused.
7. **Given** a session was ended, **When** the person signs in again, **Then** both steps are
   required as usual.
8. **Given** a session ended while a page was open, **When** the sign-in form appears, **Then**
   no clinic data remains visible.

---

### User Story 6 - A stolen session is detected and shut down (Priority: P2)

A session stays alive while its owner works by being renewed quietly in the background. Each
renewal replaces the proof the browser holds. If an old proof is presented again after its short
grace period, the system treats it as theft: every session of that account is ended and the
event is recorded.

**Why this priority**: a copied session is as good as a password and a code together; this is
the only defence once one has been copied.

**Independent Test**: renew a session, then present the earlier proof after the grace period;
all sessions of the account are ended and a security event is recorded. Present it within the
grace period; nothing is ended.

**Acceptance Scenarios**:

1. **Given** a live session, **When** it is renewed, **Then** the person notices nothing and the
   earlier proof is replaced.
2. **Given** a proof that was replaced, **When** it is presented again after the grace period,
   **Then** every session of that account ends and a security event is recorded.
3. **Given** two tabs renewing at the same moment, **When** the earlier proof is presented within
   the grace period, **Then** it is accepted and nothing is ended.
4. **Given** a disabled account or a suspended clinic, **When** a renewal is attempted, **Then**
   it is refused.

---

### User Story 7 - Too many requests and blocked networks are refused first (Priority: P3)

A script hammers the sign-in form from one network address. After ten requests in a minute it is
told, on the form it used, to wait, and when it may try again. Separately, when Heta has blocked
a network, a person signing in from it is told «الدخول من هذه الشبكة محظور الآن. راجِع فريق
الدعم لمعرفة السبب ورفع الحظر.» — before anything about the account is checked, so no attempt
is counted against any account.

**Why this priority**: these are outer defences; the lock and the single answer already protect
each account.

**Independent Test**: send eleven first-step requests from one address within a minute; the
eleventh is refused with a retry time and no account is touched. Mark an address as blocked and
sign in from it with correct details; it is refused with the blocked banner and no attempt is
counted.

**Acceptance Scenarios**:

1. **Given** ten sign-in requests from one address within a minute, **When** another is sent in
   the same minute, **Then** a warning banner on the same form says when to try again; there is
   no separate page.
2. **Given** a blocked network, **When** correct details are sent, **Then** the blocked banner is
   shown as a warning, no code is sent, and no attempt is counted on the account.
3. **Given** the limit was reached, **When** the minute has passed, **Then** sign-in works again.

---

### User Story 8 - Every sign-in screen is complete, bilingual and offers help (Priority: P3)

Every sign-in screen has the same frame: the introduction panel on one side with the logo and
the product's three lines of text, the form on the other, the language switch in the outer top
corner, and a footer with «تحتاج مساعدة؟ تواصل مع الدعم الفني», «سياسة الخصوصية» and «شروط
الاستخدام». The help link opens a small window with the support hours and three channels —
«الخط الساخن», «واتساب», «البريد الإلكتروني» — each a link that leaves the application. The
clinic's name appears on none of these screens.

**Why this priority**: constitution VI requires complete, bilingual screens; the help window is
the only way out for someone whose clinic is suspended.

**Independent Test**: open each sign-in state in Arabic and in English; check the frame, the
mirrored layout, the footer links and the help window; confirm no clinic name is shown anywhere
before sign-in.

**Acceptance Scenarios**:

1. **Given** the sign-in form in Arabic, **When** the language switch is pressed, **Then** the
   form is in English and the introduction panel is on the other side.
2. **Given** any sign-in screen, **When** «تحتاج مساعدة؟ تواصل مع الدعم الفني» is pressed,
   **Then** a window titled «تحتاج مساعدة؟» opens over a dimmed background with the support
   hours and the three channels.
3. **Given** the help window is open, **When** the «واتساب» row is pressed, **Then** a WhatsApp
   conversation opens outside the application.
4. **Given** the help window is open, **When** × is pressed, **Then** it closes on the screen it
   was opened from.
5. **Given** any state of the sign-in form, **When** she types in any field, **Then** the line
   «حساب جديد؟ افتح الرابط في بريدك لتعيين كلمة المرور.» remains.
6. **Given** any sign-in screen, **When** it is inspected, **Then** the clinic's name is not
   shown, and a server banner and a field error never appear together.
7. **Given** any failure banner on these screens, **When** it is read, **Then** it says what to
   do next.

---

### Edge Cases

- An account is created with an email that already has an account in another clinic: it is
  refused with the same message as a duplicate in the same clinic.
- The same email and password are sent twice quickly (double press): one code is standing, not
  two; the button is locked while the first request is in flight.
- A correct password is sent while a code is already standing for that account: the new code
  replaces the earlier one.
- A wrong code is typed five times: the code is spent and the person returns to the form and
  starts again; the account is not locked by one spent code. The third code spent in a row
  locks the account for 15 minutes.
- A wrong password is sent while the account is locked: the answer is the single error; the lock
  is not lengthened and no further sessions are affected.
- The code was resent three times in one sign-in: no further resend is offered; the screen says
  to go back and sign in again; the last code still works until it expires; typing the password
  again starts a new sign-in with three resends of its own.
- Four wrong passwords, then nothing for 15 minutes, then one more wrong password: the account
  is not locked; that attempt counts as the first.
- The lock expires while the person is reading the lock banner: the next correct password
  proceeds to the code.
- The fifth wrong attempt and a correct one arrive at the same moment: the account ends either
  locked or signed in at step one, never both; the count is never lost.
- The person closes the browser on the code screen and returns: there is no session; she starts
  from the form.
- The email address is typed in capital letters or with spaces around it: it is matched in lower
  case and trimmed; the password is matched exactly as typed.
- The person is signed in on two devices and signs out on one: the other continues.
- A session is ended while a request of it is in flight: that request is refused or completes,
  and every later one is refused.
- A request reaches a staff page with a session whose clinic no longer matches the clinic the
  work claims: the work is refused (spec 002, FR-011).
- The email service is slow rather than failing: the first step waits a bounded time, then gives
  the "could not send" answer; a code that arrives late still works until it expires.
- The clock shown in the lock banner is the person's local time; the lock itself is measured by
  the server.
- The privacy policy and terms pages do not exist yet (spec 086): the links are present and lead
  to the pages' addresses, which show the standard "not available yet" state until that spec.
- A sign-in screen is opened with the platform unreachable: it shows the error state of spec 001
  with a reference, not a blank page.
- The clinic becomes read-only while people are signed in: their sessions continue.

## Requirements *(mandatory)*

### Functional Requirements

**Accounts and their clinic**

- **FR-001**: The system MUST hold, for each member of a clinic's team, an account with an email
  address, a password kept only in a one-way form, a state (invited, active, disabled), a count
  of wrong attempts and, when locked, the time the lock ends. An account belongs to exactly one
  clinic and is clinic-owned data under spec 002.
- **FR-002**: Email addresses MUST be compared in lower case and without surrounding spaces. An
  account's email MUST NOT change after the account is created.
- **FR-003**: An email address MUST have at most one staff account on the whole platform, so
  that the account alone determines the clinic. The clinic of a sign-in MUST be known without
  the person typing or choosing it, and the sign-in form MUST NOT show any clinic's name
  (CL-SH-067). Creating an account with an email that already has one, in any clinic, MUST be
  refused, and the refusal MUST NOT reveal which clinic holds it.
- **FR-056**: Finding an account by its email before sign-in MUST be able to read only what
  sign-in needs — the account's identifier, its clinic, and what is needed to check the
  password and the account's state — and MUST be a declared exception to isolation under spec
  002 (constitution II).
- **FR-004**: This spec MUST provide the operation that creates an account in a given state for
  a given clinic, for use by seed data, tests and later specs (004, 015, 026). It delivers no
  screen for creating accounts.

**Step one: email and password**

- **FR-005**: The sign-in form MUST have two fields, email and password, and one primary button
  «تسجيل الدخول» as wide as the form. Pressing Enter in either field equals pressing the button;
  first focus is on the email field.
- **FR-006**: The form MUST check on submit, before calling the server: empty email «البريد
  مطلوب»; malformed email «البريد غير صحيح — مثل name@clinic.com»; empty password «كلمة المرور
  مطلوبة». These appear under the field, with no banner. No length rule applies to the password
  on this form.
- **FR-007**: While the request is in flight the button MUST be locked and read «جارٍ تسجيل
  الدخول…».
- **FR-008**: A correct password MUST NOT open a session. It MUST issue a sign-in code, send it
  to the account's email, reset the count of wrong attempts to zero, and move the person to the
  code step.
- **FR-009**: The server MUST check in this order: blocked network → request limit → the account
  exists → the password → the lock → the account's state → the clinic's state → sending the
  code.
- **FR-010**: The server MUST give one identical answer — «البريد الإلكتروني أو كلمة المرور غير
  صحيحة.» — when the email is unknown, when the password is wrong, when the account is invited
  and not yet activated, and when the password is wrong for an account that is locked, disabled
  or in a suspended clinic (SH-043).
- **FR-011**: The answers of FR-010 MUST be indistinguishable to the caller in content, form and
  response time.
- **FR-012**: A server banner MUST appear between the title and the fields; both fields MUST keep
  what was typed and the button MUST be enabled. A server banner and a field error MUST NOT be
  shown together.
- **FR-013**: When the server cannot be reached the form MUST show «تعذّر الاتصال بالخادم. حاول
  مرة أخرى بعد قليل.».
- **FR-014**: When the password is correct and the code cannot be sent, the form MUST show «لم
  نستطع إرسال رمز التحقق إلى بريدك. حاول مرة أخرى بعد قليل.», keep the button enabled, and not
  show the code step.

**The lock and the other refusals**

- **FR-015**: Each wrong password for an existing account MUST count as one wrong attempt. The
  fifth consecutive wrong attempt MUST lock the account for 15 minutes and end all its live
  sessions (SH-041). Wrong attempts MUST be forgotten when 15 minutes pass without a new wrong
  attempt, as well as on a correct password: only wrong attempts less than 15 minutes apart add
  up.
- **FR-016**: A locked account MUST open again when the 15 minutes have passed, or when lifted by
  the unlock operation (SH-049). This spec MUST provide that operation, and the operation that
  clears a lock when a password is reset, for specs 026 and 004 to call; it delivers no screen
  for them.
- **FR-017**: The lock banner MUST be shown only after a correct password, as a warning, with the
  time the lock ends and the statement that resetting the password opens it at once. The time
  and its AM/PM mark MUST NOT break across two lines.
- **FR-018**: A disabled account MUST be refused only after a correct password, with a danger
  banner that says the account is disabled and that whoever manages users in the clinic can
  re-enable it. The banner MUST say "disabled" only, MUST NOT mention "not yet activated", and
  MUST NOT name a role or who disabled it.
- **FR-019**: An account of a suspended clinic MUST be refused only after a correct password,
  with a danger banner that says the clinic is suspended and that contacting support is the only
  way, carrying the action «التواصل مع الدعم» that opens the help window. The banner MUST NOT
  link the return of access to paying an invoice. A cancelled clinic MUST be treated exactly as
  a suspended one, at sign-in, at the code step and at renewal, with the same banner; wherever
  this spec says "suspended", cancelled is included.
- **FR-020**: A clinic in the read-only state MUST allow its team to sign in (SH-034).
- **FR-021**: No "I am not a robot" check may be shown on staff sign-in (SH-042).

**Step two: the email code**

- **FR-022**: The code step MUST be required at every sign-in. No setting of a clinic or of an
  account may turn it off; turning it off is possible only as a switch for the whole platform.
  With the switch off, a correct password that passes every check of FR-009 opens the session
  directly and no code is issued or sent.
- **FR-023**: The code MUST be six digits, valid for 10 minutes, for at most five attempts, and
  usable once (SH-040). It MUST be delivered by email only.
- **FR-024**: The code screen MUST be titled «التحقق بخطوتين», name the email the code was sent
  to, and show six boxes that are filled left to right in Western digits in both languages, with
  focus moving to the next box.
- **FR-025**: A wrong code, an expired code and a spent code MUST get one identical answer: the
  boxes turn red and «الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.»; the button stays
  enabled.
- **FR-026**: Under the boxes a countdown «إعادة الإرسال خلال ‹د:ث›» MUST be shown; when it ends
  it MUST be replaced by the link «لم يصلك الرمز؟ إعادة الإرسال». The length of the countdown is
  a platform setting.
- **FR-027**: Resending MUST send a new code to the same email without asking for the password
  again. A successful resend MUST cancel the earlier code; a failed one MUST leave it valid until
  it expires. At most three codes may be resent in one sign-in; a failed resend does not count.
  After the third, the resend link MUST be replaced by a note that says to go back and sign in
  again, and the last code sent MUST stay usable until it expires. The number is a platform
  setting.
- **FR-028**: When a resend fails the screen MUST show «لم نستطع إرسال رمز جديد. الرمز السابق
  يبقى صالحًا حتى تنتهي مدّته.», keep «تأكيد الرمز» enabled, and show a secondary button «إعادة
  إرسال الرمز».
- **FR-029**: «الرجوع لتسجيل الدخول», with an arrow that follows the reading direction, MUST be
  present in every state of the code screen except success.
- **FR-030**: On a correct code the server MUST check the account's state and the clinic's state
  again. If either no longer permits entry, no session is opened and the person is returned to
  the sign-in form with the banner of that state.
- **FR-031**: On success the boxes MUST turn green and lock, «الرمز صحيح، وسُجّل الدخول.» MUST be
  shown, the button MUST read «جارٍ فتح الرئيسية…», and the person MUST be taken inside.
- **FR-032**: Five wrong codes MUST spend the code. One spent code MUST NOT lock the account;
  three codes spent in a row for one account, with no successful sign-in between them, MUST lock
  it for 15 minutes exactly as five wrong passwords do (FR-015 to FR-017), and a successful
  sign-in returns that count to zero. The number is a platform setting.

**Sessions**

- **FR-033**: A session MUST be opened only by a correct code. It belongs to one account and one
  clinic, and records when it was created, when it was last used, when it expires, and the
  network address and browser it was opened from.
- **FR-034**: Every request made with a session MUST be checked against the session being alive
  at that moment, so that ending a session takes effect on the very next request (CL-SH-124).
- **FR-035**: Work done with a session MUST be done for the session's clinic, established through
  the place spec 002 provides (spec 002, FR-012), and for no other.
- **FR-036**: A person MAY have several live sessions at once (several devices or browsers).
- **FR-037**: A session MUST stay alive while in use by being renewed without the person
  noticing, up to a maximum life of 12 hours from sign-in. The figure is a platform setting,
  changeable without a code change. After it the person signs in again with both steps, however
  active they were. Renewal MUST be refused when the
  account is no longer active or the clinic is suspended.
- **FR-038**: Each renewal MUST replace the proof the browser holds. The replaced proof MUST be
  accepted only for a short grace period; presenting it afterwards MUST end every session of the
  account and be recorded as a security event (CL-SH-125).
- **FR-039**: Signing out MUST end the current session only, need no confirmation, and return the
  person to the sign-in form in its default state with no banner. Signing out MUST remain
  possible in a suspended or read-only clinic.
- **FR-040**: A session MUST end when: the person signs out; its maximum life passes; the account
  is locked; the account is disabled; the account's sessions are ended; everyone's sessions in
  the clinic are ended; the clinic is suspended; a replaced proof is reused. A session of a suspended clinic
  MUST stop at the latest at its next renewal (within five minutes), and at once when the
  operation that suspends the clinic (spec 016) calls "end all sessions of this clinic". This
  spec MUST
  provide the operations "end this account's sessions" and "end all sessions of this clinic
  except one" for specs 004, 005, 026 and 085 to call (SH-045); it delivers no screen for them.
- **FR-041**: When a session was ended by anything other than the person's own sign-out, the next
  action MUST clear what is on screen and show the sign-in form with the neutral notice «انتهت
  الجلسة. الدخول من جديد للمتابعة.». The notice MUST NOT say why or who ended it.
- **FR-042**: Opening any console page without a live session MUST show the sign-in form; opening
  the sign-in form with a live session MUST take the person inside.
- **FR-043**: Until the console shell and home page exist (spec 006), "inside" is a landing page
  that shows the person is signed in and offers «تسجيل الخروج».

**Outer defences**

- **FR-044**: Sign-in requests (both steps and resend) MUST be limited to ten per minute per
  network address (SH-041). Renewing a session is not a sign-in request: it has its own, larger
  limit, so that a clinic's team working behind one address does not use up the allowance. Beyond the limit, a warning banner on the form the request came from
  MUST say when to try again; there is no separate page.
- **FR-045**: A request from a blocked network MUST be refused before anything about the account
  is checked, with the warning «الدخول من هذه الشبكة محظور الآن. راجِع فريق الدعم لمعرفة السبب
  ورفع الحظر.», and MUST NOT count as an attempt on any account. This spec provides the check
  and the refusal; the list of blocked networks is managed by spec 025 and is empty until then.

**The screens**

- **FR-046**: Every sign-in screen MUST have the same frame: the introduction panel (logo, «نظام
  تشغيل العيادة الذكي», «بنية تحتية طبية رقمية متكاملة», and the one descriptive sentence of the
  BRD, with no buttons); the form panel; the language switch in the outer top corner; and a
  footer with «تحتاج مساعدة؟ تواصل مع الدعم الفني», «سياسة الخصوصية» and «شروط الاستخدام»
  (SH-047).
- **FR-047**: Every sign-in screen and every state of it MUST exist in Arabic and in English,
  mirrored correctly (constitution VI). The states are: default, session ended, filled, password
  shown, validation, loading, error, request failed, code not sent, account locked, account
  disabled, clinic suspended, network blocked, too many requests; code entry, code error, resend
  failed, no more resends, code accepted; and the help window.
- **FR-048**: The line «حساب جديد؟ افتح الرابط في بريدك لتعيين كلمة المرور.» MUST be shown under
  the "forgot password" link in every state that shows the sign-in form, and MUST NOT disappear
  while typing. The link «نسيت كلمة المرور؟» is present and leads to the address spec 004 will use,
  which shows the standard "not available yet" state until then.
- **FR-049**: Every password field MUST have an eye icon that shows and hides what was typed.
  Nothing is sent when it is pressed.
- **FR-050**: The help window MUST show the support hours and three channels — «الخط الساخن»,
  «واتساب», «البريد الإلكتروني» — each row a link that leaves the application (a call, a WhatsApp
  conversation, an email). It has no form and sends nothing. Its values are platform settings.
- **FR-051**: Every failure banner on these screens MUST say what the person can do next.

**Records and privacy**

- **FR-052**: A successful sign-in MUST be recorded as a sign-in event for the clinic (CL-SH-094).
- **FR-053**: These MUST be recorded as security events, with the network address and browser: a
  wrong password; an attempt on an email that matches no account; an account locked; a wrong
  code; a code spent after five attempts; a replaced session proof reused (CL-SH-095).
- **FR-054**: Passwords, codes and session proofs MUST never be stored in a readable form, shown
  again, or written to any log. Operational logs MUST carry identifiers only, never an email
  address (SH-094; spec 001, FR-021).
- **FR-055**: The email that carries the code MUST state the code and how long it is valid, and
  nothing that identifies data of the clinic. Nobody can switch this email off.

### Key Entities

- **Staff account**: a member of a clinic's team as the system knows them for sign-in: email,
  password in one-way form, state (invited, active, disabled), count of wrong attempts, lock end
  time. Belongs to one clinic. Its other details (name, role, branch) belong to specs 005 and
  026.
- **Sign-in code**: a six-digit code issued after a correct password, tied to one account, with
  an expiry 10 minutes after issue and a count of attempts. It is standing, used, spent or
  expired. At most one is standing per account.
- **Session**: the fact that a person is signed in on one browser: its account and clinic, when
  it was created, last used and expires, where it was opened from, and when it was ended.
- **Session proof**: what the browser holds to show a session is its own. Replaced at every
  renewal; the previous one is honoured briefly, then treated as stolen.
- **Sign-in event**: a successful sign-in, for the clinic's activity log (spec 059 shows it).
- **Security event**: a failed or suspicious act at sign-in, with identifiers, the network
  address and the browser (specs 025 and 059 show them).
- **Blocked network**: an address or range from which sign-in is refused. Checked here; managed
  by spec 025.
- **Support channels**: the hours and the three contact values shown in the help window; platform
  settings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A team member with correct details completes both steps and is inside in under one
  minute, of which the wait for the code email is under 30 seconds in 95% of sign-ins.
- **SC-002**: In an automated run over every "wrong before a correct password" case (unknown
  email; wrong password; wrong password on a locked, disabled, invited or suspended-clinic
  account), 100% of answers are identical in content, and their average response times differ by
  less than 5% over 1,000 requests.
- **SC-003**: 0 sessions exist for any account that has completed only the first step.
- **SC-004**: After the fifth consecutive wrong password, 100% of further attempts within 15
  minutes fail to reach the code step, and 100% of that account's sessions are ended.
- **SC-005**: A guessed code succeeds in at most 5 attempts out of 1,000,000 possibilities per
  issued code, and a code is never accepted after 10 minutes, after use, or after a newer one was
  issued.
- **SC-006**: After a session is ended by any cause, 100% of its subsequent requests are
  refused, starting with the very next one.
- **SC-007**: 100% of reuses of a replaced session proof after the grace period end every session
  of that account and produce a security event.
- **SC-008**: Every data request made with a session returns records of that session's clinic
  only, in an automated run with at least two clinics signed in at the same moment.
- **SC-009**: The eleventh sign-in request from one address within a minute is refused, and no
  account's count of wrong attempts changes because of refused or blocked requests.
- **SC-010**: 100% of the sign-in states listed in FR-047 exist in Arabic and in English, pass
  the translation check, and show no clinic name.
- **SC-011**: 0 passwords, codes, session proofs or email addresses appear in operational logs in
  the automated leak run of spec 001 extended to these screens.
- **SC-012**: Specs 004, 005, 010 and 026 can build on this spec — reset a password, attach a
  role, sign in an operator, disable an account — without changing how sign-in or sessions work.

## Assumptions

- Addresses follow constitution v1.1.0: staff sign in at `app.scs-heta.com`, one address for all
  clinics. The BRD (SH-003, CL-SH-065, B-R1) still describes a sign-in address per clinic and is
  to be updated by the product owner.
- An email address has one staff account on the whole platform (product owner, 10 Oct 2026). The
  BRD says "unique within the clinic" (B-3-1) because it assumed an address per clinic; that line
  is to be updated with SH-003. One person working in several clinics under one email is not
  supported; it would be a new spec.
- The wording of banners follows the BRD's settled rules where the drawn text differs: the
  disabled banner says "disabled" only (B-O18), and the suspended banner omits the sentence about
  paying a renewal invoice. Final wording is fixed at the page review the BRD calls for; the
  behaviour is as written here.
- The limit of three resends per sign-in was decided by the product owner on 10 Oct 2026; the BRD
  has no limit and is to be updated. The note shown after the third is not drawn; its wording is
  fixed at page review.
- The too-many-requests banner, and the return to the form with a state banner between the two
  steps, are not drawn in the design files; they reuse the warning and danger banners already
  drawn for the sign-in form.
- The session's maximum life of 12 hours was confirmed by the product owner on 10 Oct 2026. The
  resend countdown starts at 60 seconds and the grace period for a replaced session proof at 30
  seconds. Each is platform configuration until the operators' settings screen exists (spec
  024). The BRD says only that each is a platform setting.
- Ten sign-in requests per minute per network address is the BRD's figure (SH-041). A clinic's
  whole team usually shares one address, and ten people arriving together need about twenty
  requests; the figure is a setting and is listed for the product owner to confirm.
- Ending a session after a period without activity (the idle timeout) is a clinic setting
  delivered by spec 085. Until then only the maximum life applies.
- A wrong password while the account is locked does not lengthen the lock, and a single wrong
  code never counts towards the lock; only a third spent code in a row does (product owner, 10
  Oct 2026 — the BRD has no such rule and is to be updated).
- Wrong attempts are forgotten after 15 minutes without a new one (product owner, 10 Oct 2026).
  The BRD says "five wrong attempts" without a period; this is to be added to SH-041.
- Until the notifications engine (spec 008) and the platform's email settings (spec 024) exist,
  this spec sends its one email — the code — through the platform's outgoing mail directly, in
  Arabic and English together. From spec 008 on, the engine owns templates and the clinic's
  language.
- The support hours and channel values are platform configuration with the BRD's sample values
  («من السبت إلى الخميس، من ٩:٠٠ ص إلى ٩:٠٠ م بتوقيت القاهرة») until spec 024 exposes them.
- Accounts do not yet have names, roles or permissions. After sign-in every account can reach
  the landing page only; what else it may do arrives with spec 005.
- Until accounts can be created from a screen (specs 015 and 026), made-up accounts for the two
  seeded clinics are added by this spec's seed, and the operations of FR-004, FR-016 and FR-040
  are exercised by tests.
- The sign-in and security events recorded here are stored by this spec in the form later specs
  read: spec 007 (audit trail), spec 025 (operators' security events) and spec 059 (the clinic's
  activity and security log). Alerting anyone about a lock is spec 008.
- The clinic states this spec reacts to are `suspended` and `cancelled` (nobody enters, sessions
  cannot continue; one banner for both — product owner, 10 Oct 2026) and read-only (entry
  allowed). Every other state lets the team in. The full list of states and their meaning is spec
  016; spec 002 already stores the state.
- The privacy policy and terms links lead to addresses whose pages are delivered by spec 086.
- The method used to keep passwords and session proofs, and how a session is carried by the
  browser, are fixed at planning under the constitution; they are deliberately not named here.
