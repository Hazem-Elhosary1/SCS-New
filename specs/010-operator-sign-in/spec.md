# Feature Specification: Operator Sign-in

**Feature Branch**: `010-operator-sign-in`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: empty. Taken from row `010-operator-sign-in` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1, the operators console): "the operator
signs in with an email code, the lock and lifting it, sessions."

**BRD source** (`docs/reference/brd/source/`): `BRD-0-shared.md` — SH-040 (email and password,
then a code by email at every sign-in, valid ten minutes, five tries), SH-041 (locked fifteen
minutes after five wrong tries; ten requests a minute for an address), SH-043 (nothing is
revealed before a correct password), SH-045 (disabling an account or changing its role ends its
sessions), SH-047 (the privacy-policy link on every sign-in screen), SH-048 (a reset link lives
thirty minutes), SH-049 (what lifts a lock), SH-091 and SH-094 (no secret and no personal data
in logs). `modules/17-ops-support-platform.md` — §1 (what is out of that chapter and lives
here), §3-9 (the operator), §4-3 (the states of an operator's account), §9-3 (what reaches the
owner of the account), rules ق-١, ق-٥٨, ق-٨٨ and ق-١٢٥.

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY` (the operators console), read on
12 Oct 2026. Page "Login" (`8:32`): Login (`128:85` Arabic, `128:54` English), Login Error
(`134:54`, `134:15`), 2FA (`135:61`, `135:38`), Forgot Password (`141:53`, `141:38`), Forgot
Password Sent (`158:52`, `158:38`), and Invite – Set Password (`167:66`, `167:38`), whose
layout is the model for choosing a new password. Page "Template — Profile" (`1844:70`): the
account menu (`1844:2699`), My Profile (`1849:2791`), Profile with two-factor required by
settings (`2185:567`), Change Password open, error and success (`1851:2003`, `1852:2042`,
`1852:2169`) and the note on its errors (`2185:674`). Page "Navbar" (`278:2`) and "Sidebar Nav
Item" (`8:40`) for the frame. Every frame has an Arabic and an English twin.

## Overview

The people who run the platform — operators — have their own console at its own address, and
nothing of it exists yet but an empty page. This spec gives them the way in and the room they
arrive in: an operator signs in with email and password and then a code sent by email; wrong
tries lock the account; nothing is revealed to someone who does not know the password; a
forgotten password is replaced through a link; and a session ends when it should — at once
when the operator signs in elsewhere, signs out, changes their password, or is suspended.

Once in, the operator is inside the console's frame — a top bar with their name and an account
menu, and a side bar that lists only what has been built — and has a page of their own, "My
profile": their account details, their recent sessions, a control that signs them out
everywhere, and a dialog to change their password.

An operator's account is not a clinic's: it belongs to the platform, opens no clinic, and a
clinic team member's session opens nothing here. Operators hold the widest reach in the
product, so two rules are stricter than for a clinic's team: the email code cannot be switched
off, and an operator has one session at a time.

**In scope**: the operator's account as far as signing in needs it, and the operation that
makes the first operator and the screen where an invited operator chooses their first
password; the sign-in form, the code step, the lock, the single answer; forgot password and
choosing a new one from the link; the email about a sign-in from a new browser; sessions — one at a time, their end, the
detection of a stolen one; signing out; the console's frame; the profile page with recent
sessions, signing out of all devices and changing the password.

**Out of scope**: inviting an operator from the console and sending an invitation again,
editing, suspending and reactivating operators, lifting a lock for someone else, operators' roles and
their 28 permissions (011); the bell and its notifications in this console (the spec that
builds the operators' bell); the dashboard that will be the console's home (022); the security
settings — lock threshold and length, session timeout, requiring two-factor, the allowed-address
list (024); the security events screen and blocking addresses (025); the operators' audit log
and its reports (023); the uploaded logo on the sign-in screen (024); a "not a robot" check
(SH-042, later).

## Clarifications

### Session 2026-10-12

- Q: Is the email code required at every operator sign-in, or can an operator switch it off for their own account? → A: Always required. The switch on the profile is shown on and locked; whether a platform setting may ever relax it is spec 024's question.
- Q: The console has no frame and no spec that builds it; what does this spec deliver after sign-in? → A: The frame and the profile page: top bar, account menu, a side bar with only what exists, and My Profile with account details, recent sessions, sign out of all devices and change password.
- Q: May an operator be signed in on several devices? → A: No. One session at a time: a new sign-in ends the earlier session, and that device is told why.
- Q: How does the very first operator get into the console, given that invitations do not exist until spec 011? → A: The deployment operation creates the account as invited and emails a link, valid 72 hours, to choose a password. No password is shown to or typed by whoever deploys.
- Q: Should an operator's session end after a period with no activity, in addition to the 12-hour maximum? → A: Yes, after 30 minutes with no activity; the maximum life stays 12 hours.
- Q: What does ticking «تذكرني» do? → A: It keeps the session after the browser is closed, within the session's idle and maximum time; unticked, closing the browser signs out.
- Q: Is an operator told by email when their account is signed in to from a device it has not used before? → A: Yes, every time, and it cannot be switched off.
- Q: If the first operator's email is mistyped, how is the mistake undone before a stranger uses the link? → A: The deployment tool can withdraw an invited account that has not chosen a password: its link stops working at once and the account is removed (security checklist CHK002).
- Q: Anyone who knows an operator's email can lock that account with five wrong passwords; how does the spec treat this? → A: Accepted, as for a clinic's team (SH-041): the real operator gets in at once through "forgot password", which lifts the lock (CHK010).
- Q: What does changing the password from the profile ask for besides the current password? → A: A code sent by email, entered before the change is saved (CHK017).
- Q: How long are the records of sign-in events kept until specs 023 and 025 decide? → A: Twelve months (CHK027).
- Decided without a question, each having one sensible answer: every address that changes something refuses a request forged from another site (CHK014); and the reset link, the code and the notices all going to one mailbox is accepted, because SH-040 defines the second step as a code by email (CHK016).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An operator signs in with email, password and an email code (Priority: P1)

An operator opens the console's address, types their email and password and presses «تسجيل
الدخول». A six-digit code arrives by email; they type it and are inside the console, in its
frame, with their name and role in the top bar.

**Why this priority**: nothing else in the operators console can be reached without it.

**Independent Test**: with an active operator account and a mailbox, sign in end to end and
arrive in the console's frame; confirm no session exists after the password alone.

**Acceptance Scenarios**:

1. **Given** the sign-in screen, **When** it is shown in Arabic, **Then** it reads «تسجيل
   الدخول», «ادخل ببيانات حساب التشغيل الخاص بك», the fields «البريد الإلكتروني» and «كلمة
   المرور», the box «تذكرني», the link «نسيت كلمة المرور؟» and the button «تسجيل الدخول»,
   beside the panel «لوحة تشغيل SCS» / «إدارة كل عيادات المنصة، الباقات، التذاكر، والفريق من
   مكان واحد.»; in English it reads as its twin frame does.
2. **Given** an active account, **When** the correct email and password are sent, **Then** no
   session is opened, a code is sent to that email, and the code step is shown: «التحقق
   بخطوتين», «أدخل الكود المكوّن من 6 أرقام المُرسل إلى ‹البريد›», six boxes, «تحقق» and
   «الرجوع لتسجيل الدخول».
3. **Given** the code step, **When** the right code is entered, **Then** the session opens and
   the operator is in the console's frame, on its home.
4. **Given** the form, **When** it is submitted with an empty or malformed email or an empty
   password, **Then** the field says so under it and nothing is sent.
5. **Given** a request in flight, **When** the operator presses the button again, **Then**
   nothing more is sent: the button is locked and says it is working.
6. **Given** an operator who is signed in, **When** they open the sign-in address, **Then**
   they are taken into the console.
7. **Given** someone who is not signed in, **When** they open any address of the console,
   **Then** they see the sign-in screen, and after signing in arrive where they were going.

---

### User Story 2 - Nothing is revealed before a correct password (Priority: P1)

Someone who does not know a password learns nothing from this screen: not whether an email
belongs to an operator, not whether the account is locked, suspended or still invited.

**Why this priority**: the operators console reaches every clinic; its sign-in is the first
thing an attacker probes.

**Independent Test**: send a wrong password for an existing account, any password for an
unknown email, and a wrong password for a locked, a suspended and an invited account; the five
answers are the same in words, form and time.

**Acceptance Scenarios**:

1. **Given** an unknown email, or a known email with a wrong password, **When** the form is
   sent, **Then** the banner reads «بيانات الدخول غير صحيحة» / «تأكد من البريد الإلكتروني وكلمة
   المرور وحاول مرة أخرى.», between the title and the fields, and both fields keep what was
   typed.
2. **Given** a locked, suspended or invited account, **When** a wrong password is sent, **Then**
   the answer is that same banner and nothing else.
3. **Given** a suspended account, **When** the correct password is sent, **Then** the screen
   says «هذا الحساب موقوف.» and no code is sent.
4. **Given** an invited account that has not chosen a password, **When** anything is sent,
   **Then** the answer is the single banner: there is no password to be correct.
5. **Given** the forgot-password form, **When** any email is sent, **Then** the next screen is
   the same whether or not an operator has it.
6. **Given** the answers above, **When** their timing is measured, **Then** an unknown email
   cannot be told from a wrong password by how long the answer took.

---

### User Story 3 - Wrong tries lock the account, and the operator knows what to do (Priority: P1)

After five wrong passwords the account is locked for fifteen minutes. The operator who then
types the right password is told the account is locked and until when, and that choosing a new
password opens it at once.

**Why this priority**: it is what stops guessing; and a locked operator with no explanation
cannot work.

**Independent Test**: send five wrong passwords, then the right one, and read the lock message
with its time; wait out the lock or reset the password, and sign in.

**Acceptance Scenarios**:

1. **Given** four wrong passwords, **When** a fifth is sent, **Then** the account is locked for
   fifteen minutes and the answer is still the single banner.
2. **Given** a locked account, **When** the correct password is sent, **Then** a warning says
   the account is locked, the time it opens, and that resetting the password opens it now; no
   code is sent.
3. **Given** a locked account, **When** more wrong passwords are sent, **Then** the lock is not
   made longer.
4. **Given** the fifteen minutes have passed, **When** the operator signs in correctly,
   **Then** they are let in and the count of wrong tries starts again from nothing.
5. **Given** a locked account, **When** its owner completes a password reset, **Then** the lock
   is lifted.
6. **Given** a signed-in operator changing their password, **When** they give a wrong current
   password, **Then** it counts as a wrong try; on the fifth they are signed out and the account
   is locked.
7. **Given** a correct sign-in, **When** it completes, **Then** the count of wrong tries is
   cleared.

---

### User Story 4 - The code is short-lived, limited and can be sent again (Priority: P2)

The code lives ten minutes and allows five tries. A new one can be asked for after a short
wait, shown as a countdown; asking again makes the earlier code useless.

**Why this priority**: the code is the second factor; its limits are what make it one.

**Independent Test**: enter a wrong code five times, an expired one, and a resent one; each
behaves as below.

**Acceptance Scenarios**:

1. **Given** the code step, **When** a wrong code is entered, **Then** the screen says the code
   is wrong or expired and that a new one can be asked for, and the boxes are emptied.
2. **Given** five wrong codes, **When** a sixth try is made with any code, **Then** it is
   refused: the operator starts again from the password.
3. **Given** a code older than ten minutes, **When** it is entered, **Then** it is refused with
   the same words as a wrong one.
4. **Given** the code step has just opened, **When** the operator looks for "send again",
   **Then** they see «يمكنك إعادة إرسال الكود خلال ‹د:ث›» counting down from one minute, and
   the control to send again when it reaches zero.
5. **Given** a new code was sent, **When** the earlier code is entered, **Then** it is refused.
6. **Given** the correct password but the code cannot be sent, **When** the form is sent,
   **Then** the operator is told the code could not be sent and to try again; no session and no
   code step.
7. **Given** a code, **When** it has been used once, **Then** it cannot be used again.
8. **Given** the code step, **When** «الرجوع لتسجيل الدخول» is pressed, **Then** the sign-in
   form is shown and the code is useless.
9. **Given** a code is pasted into the first box, **When** it has six digits, **Then** it fills
   the six boxes.
10. **Given** three codes in a row have each been spent by five wrong tries, **When** the third
    is spent, **Then** the account is locked for fifteen minutes, as by five wrong passwords.
11. **Given** three new codes were already sent in one sign-in, **When** another is asked for,
    **Then** it is refused and the operator starts again from the password.

---

### User Story 5 - An operator replaces a forgotten password (Priority: P2)

From «نسيت كلمة المرور؟» the operator gives their email and is told to check it. The link in
the email, good for thirty minutes, opens a screen where they choose a new password. Doing so
ends their sessions and lifts a lock.

**Why this priority**: without it a forgotten password needs another operator, and the first
operator has nobody.

**Independent Test**: ask for a link, open it, choose a password, and sign in with it; try the
link again and an expired one.

**Acceptance Scenarios**:

1. **Given** the forgot-password screen, **When** shown, **Then** it reads «نسيت كلمة المرور»,
   «أدخل بريدك الإلكتروني وسنرسل لك رابط إعادة التعيين», the field «البريد الإلكتروني», the
   button «إرسال رابط إعادة التعيين» and «الرجوع لتسجيل الدخول».
2. **Given** any email is sent, **When** the answer comes, **Then** the screen reads «تحقق من
   بريدك الإلكتروني», «أرسلنا رابط إعادة تعيين كلمة المرور إلى ‹البريد›», «لم يصلك البريد
   الإلكتروني؟ إعادة الإرسال» and the way back; an email is sent only when an active or locked
   operator has that address.
3. **Given** a link was sent less than a minute ago, **When** another is asked for, **Then**
   none is sent and the screen is the same.
4. **Given** a good link, **When** it is opened, **Then** a screen asks for the new password
   and its confirmation, with the password's rules listed under the field and turning as they
   are met.
5. **Given** a new password that meets the rules, **When** it is saved, **Then** every session
   of the account ends, a lock is lifted, the link is useless, and the operator is taken to the
   sign-in screen with a line saying the password was changed.
6. **Given** a link older than thirty minutes, used already, or replaced by a newer one,
   **When** it is opened, **Then** the screen says the link no longer works and offers to send
   a new one.
7. **Given** a suspended account, **When** a link is asked for, **Then** none is sent.
8. **Given** an operator signed in on a device, **When** their password is reset from another,
   **Then** that device's next action shows the sign-in screen with «تمت إعادة تعيين كلمة
   المرور، فتم تسجيل خروجك. سجّل دخولك بكلمة المرور الجديدة.»

---

### User Story 5a - The first operator chooses their password from an emailed link (Priority: P1)

Whoever deploys the platform runs one operation with the first operator's name and email. The
operator receives a link, good for 72 hours, opens it and chooses a password; the account
becomes active and they sign in. Nobody else ever sees or types that password.

**Why this priority**: without a first operator nobody can sign in at all, and inviting is a
later spec.

**Independent Test**: run the operation, open the link from the mailbox, choose a password,
and sign in; open the link again, and an expired one.

**Acceptance Scenarios**:

1. **Given** no operator exists, **When** the operation is run with a name and an email,
   **Then** an account is created as invited and one email with the link is sent, in Arabic.
2. **Given** a good link, **When** it is opened, **Then** the screen reads «تعيين كلمة المرور»
   with a line saying the operator was invited to the console, their email shown and not
   editable, «كلمة المرور الجديدة», «تأكيد كلمة المرور», the password's rules under the field
   turning as each is met, and «تفعيل الحساب».
3. **Given** a password that meets the rules and its matching confirmation, **When** saved,
   **Then** the account becomes active, the link is useless, and the operator is taken to the
   sign-in screen with a line saying the account is ready; they sign in with the password and
   the email code like anyone.
4. **Given** a link older than 72 hours, used already, or replaced by a newer one, **When**
   opened, **Then** the screen says the link no longer works and to ask whoever sent it for a
   new one; it offers no way to send one itself.
5. **Given** an invited account, **When** the operation is run again for the same email,
   **Then** a new link is sent and the earlier one stops working; no second account is made.
6. **Given** an active account, **When** the operation is run again for its email, **Then**
   nothing is changed and the operation says the account is already active: a forgotten
   password goes through "forgot password".
7. **Given** the operation, **When** it is looked for in the console or among its addresses,
   **Then** it is not there: it runs only where the platform is deployed, and each run is
   recorded.
8. **Given** an invited account that has not chosen a password, **When** the withdraw
   operation is run for its email, **Then** its link stops working at once and the account is
   removed; opening the link shows "no longer works". For an active account the operation
   refuses and changes nothing.

---

### User Story 6 - One session at a time, ending when it should (Priority: P2)

An operator has one session. Signing in elsewhere ends the earlier one, and that device says
why. A session also ends when the operator signs out, leaves it idle too long, has lived its
longest life, changes or resets their password, or is suspended.

**Why this priority**: an operator's session left open on another machine is the widest door
in the product.

**Independent Test**: sign in on two browsers in turn and see the first one shown the sign-in
screen with its reason; sign out; leave a session idle; each ends as below.

**Acceptance Scenarios**:

1. **Given** an operator signed in on one browser, **When** they complete a sign-in on
   another, **Then** the first session is ended, and the first browser shows the sign-in
   screen with a line saying they signed in on another device — on its next action, or by
   itself within thirty seconds if it is left untouched.
2. **Given** a signed-in operator, **When** they choose «تسجيل الخروج» from the account menu,
   **Then** the session is ended on the platform and the sign-in screen is shown; going back
   in the browser shows nothing of the console.
3. **Given** «تذكرني» was not ticked, **When** the browser is closed and opened again, **Then**
   the operator must sign in again.
4. **Given** «تذكرني» was ticked, **When** the browser is closed and opened again within the
   session's life, **Then** the operator is still signed in.
5. **Given** a session with no activity for thirty minutes, **When** the operator acts,
   **Then** the sign-in screen is shown with «انتهت الجلسة. الدخول من جديد للمتابعة.»; an open
   page that is only being looked at is not activity.
6. **Given** a session twelve hours old, **When** the operator acts, **Then** it has ended the
   same way, however active they were.
7. **Given** a suspended account, **When** the operator's open console next acts, **Then** it
   shows the sign-in screen with «تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.»
8. **Given** two tabs of one browser, **When** the operator signs out in one, **Then** the
   other shows the sign-in screen without being asked.
9. **Given** an ended session, **When** anything of the console is asked for with it, **Then**
   it is refused, whatever the browser still holds.
10. **Given** a sign-in completes from a browser this account has not used before, **When** the
    session opens, **Then** an email goes to the operator naming the browser and system, the
    address and the time, and what to do if it was not them; a sign-in from a known browser
    sends none.

---

### User Story 7 - A stolen session is detected and shut down (Priority: P2)

If what keeps a session alive is copied and used by someone else, the platform notices the
second use of an old value, ends the session, and records it.

**Why this priority**: a session is as good as a password and a code together.

**Independent Test**: replay an earlier value of a session after it was renewed; the session is
ended for both holders and a security record is written.

**Acceptance Scenarios**:

1. **Given** a session whose value was renewed, **When** the old value is presented, **Then**
   the operator's session is ended, both holders are signed out, and a record of a reused
   session value is written.
2. **Given** that happened, **When** the operator signs in again, **Then** they are let in
   normally.

---

### User Story 8 - The operator sees their account and their sessions, and changes their password (Priority: P2)

From the account menu the operator opens «الملف الشخصي»: their name, role, email and phone;
the two-factor switch, on and locked; their recent sessions; «تسجيل الخروج من كل الأجهزة»; and
«تغيير كلمة المرور».

**Why this priority**: it is where an operator checks that nobody else has been in, and where
they change a password they no longer trust.

**Independent Test**: open the profile, read the sessions, change the password with each wrong
input and then correctly, and sign out of all devices.

**Acceptance Scenarios**:

1. **Given** the profile, **When** shown, **Then** it reads «الملف الشخصي» / «إدارة معلومات
   حسابك الشخصي وإعدادات الأمان.», a card «معلومات الحساب» with the name, the role's name, the
   email under «البريد الإلكتروني», the phone under «رقم الهاتف» and the button «تغيير كلمة
   المرور», and a card «الأمان والجلسات».
2. **Given** the security card, **When** shown, **Then** the switch «تفعيل التحقق بخطوتين (2FA)
   لحسابي» is on and cannot be changed, with «مطلوب لكل المشغلين من الإعدادات ← الأمان، لذا لا
   يمكن إيقافه من هنا.» under it.
3. **Given** the list «الجلسات الأخيرة», **When** shown, **Then** each row has the browser and
   system («Chrome على Windows»), the address it came from, and its state — «نشط الآن» with
   «الجلسة الحالية» for this one, «انتهت منذ …» for the others — newest first, with the line
   «جلسة واحدة فقط في كل مرة — تسجيل الدخول من جهاز آخر يُنهي هذه الجلسة.»
4. **Given** the profile, **When** «تسجيل الخروج من كل الأجهزة» is pressed and confirmed,
   **Then** every session of the account ends, this one too, and the sign-in screen is shown.
5. **Given** the change-password dialog, **When** shown, **Then** it has «كلمة المرور الحالية»,
   «كلمة المرور الجديدة» with «10 أحرف على الأقل، تتضمن حروفًا كبيرة وصغيرة ورقمًا ورمزًا.»
   under it, «تأكيد كلمة المرور الجديدة», «إلغاء» and «حفظ كلمة المرور».
6. **Given** the dialog, **When** the two new passwords differ, **Then** «كلمتا المرور غير
   متطابقتين، يرجى المحاولة مرة أخرى.» appears under the confirmation; a wrong current password
   gives «كلمة المرور الحالية غير صحيحة.» under its field; a new password equal to the current
   gives «يجب أن تختلف كلمة المرور الجديدة عن الحالية.»; a new password that breaks the rules
   turns the hint red.
7. **Given** a correct current password and a valid new one, **When** «حفظ كلمة المرور» is
   pressed, **Then** nothing is changed yet: a code is sent to the operator's email and the
   dialog asks for it, with the six boxes and the countdown of the sign-in's code step.
7a. **Given** the dialog asks for the code, **When** the right code is entered, **Then** the
   password is changed and the dialog reads «تم تغيير كلمة المرور بنجاح» / «يمكنك الآن استخدام
   كلمة المرور الجديدة لتسجيل الدخول.» with «تم», and this session stays open.
7b. **Given** the dialog asks for the code, **When** five wrong codes are entered, the code's
   ten minutes pass, or the dialog is closed, **Then** the password is not changed and the
   operator starts the change again.
8. **Given** the profile's data cannot be loaded, **When** the page opens, **Then** it says so
   and offers to try again; while loading it shows its skeleton.

---

### User Story 9 - The console has a frame, and shows only what exists (Priority: P3)

Inside, every page sits in one frame: a top bar with the console's name, the operator's name
and role and the account menu; a side bar with the console's sections. Until their specs build
them, sections that do not exist are not listed.

**Why this priority**: later specs of this phase put their pages in it; without it each would
invent its own.

**Independent Test**: sign in and see the frame in Arabic and in English, at desktop and
narrow widths; open the account menu; follow a link to a page that does not exist.

**Acceptance Scenarios**:

1. **Given** a signed-in operator, **When** any page is shown, **Then** the top bar has the
   mark and «SCS — لوحة التشغيل», the operator's name with their role under it, and their
   initials in a round mark that opens the account menu.
2. **Given** the account menu, **When** opened, **Then** it lists «الملف الشخصي» and «تسجيل
   الخروج», closes on Escape or a press elsewhere, and can be worked with the keyboard alone.
3. **Given** the side bar, **When** shown, **Then** it lists «الرئيسية» and nothing that has
   not been built; the page being shown is marked.
4. **Given** the home before the dashboard exists, **When** opened, **Then** it greets the
   operator by name and says the console's sections will appear here as they are built.
5. **Given** an address of the console that does not exist, **When** opened by a signed-in
   operator, **Then** the frame stays and the page says it was not found, with a way home.
6. **Given** the frame in Arabic, **When** shown, **Then** it reads right to left with the side
   bar on the right; in English, left to right with it on the left.
7. **Given** a narrow window, **When** the frame is shown, **Then** the side bar is behind a
   control that opens it, and nothing is cut off or needs sideways scrolling.

---

### User Story 10 - Outer defences and complete screens (Priority: P3)

An address that asks too often is slowed down before any account is looked at. Every screen
before sign-in is whole: both languages, a state for no connection, the privacy link, and
usable with the keyboard and a screen reader.

**Why this priority**: the lock and the single answer already protect an account; these
protect the platform and finish the screens.

**Independent Test**: send eleven requests in a minute from one address; open each screen in
both languages with no connection, with the keyboard only.

**Acceptance Scenarios**:

1. **Given** ten sign-in requests from one address within a minute, **When** an eleventh is
   sent, **Then** it is refused with a message to try again shortly, and no account's count of
   wrong tries changes.
2. **Given** the platform cannot be reached, **When** a form is sent, **Then** it says «تعذّر
   الاتصال بالخادم. حاول مرة أخرى بعد قليل.» and what was typed stays.
3. **Given** any screen before sign-in, **When** shown, **Then** «سياسة الخصوصية» is in its
   footer.
4. **Given** any screen before sign-in, **When** the language is changed, **Then** every word,
   the direction and the digits follow, and the choice is kept for the next visit.
5. **Given** a keyboard only, **When** the operator moves through a screen, **Then** the order
   follows the reading order, every control can be reached and worked, and an error is
   announced when it appears.
6. **Given** the code boxes, **When** read by a screen reader, **Then** they are announced as
   one code of six digits.

### Edge Cases

- **The same email is a clinic team member's and an operator's.** The two accounts are
  separate: each has its own password, lock and sessions, and signing in to one does nothing
  to the other.
- **A clinic team member's session is presented to the operators console**, or an operator's
  to a clinic's console: refused as no session at all.
- **Email typed with capitals or spaces around it.** Compared in lower case and trimmed.
- **A password longer than 200 characters.** Refused as a wrong password.
- **The operator signs in on a second device while the first is at the code step.** The first
  may still finish; whichever completes last holds the session.
- **Two sign-ins complete at the same moment.** One session remains; the other device is shown
  the sign-in screen with the "another device" line.
- **The operator is suspended between the password and the code.** The code is refused and no
  session opens.
- **A reset link is asked for while a code is waiting.** Both stand until one is used; a
  completed reset makes the waiting code useless.
- **The new password is the same as the old one in a reset.** Refused, as in the dialog.
- **The clock of the operator's machine is wrong.** The countdown and the lock's time are
  computed from the platform's time.
- **An operator forgets their password and their mailbox is gone.** Nothing in this spec gets
  them in: another operator replaces the account's access in spec 011. For the only operator
  it is a matter for whoever deploys the platform, outside the product, and is recorded in the
  runbook.
- **The first operator's link expires before it is used.** The operation is run again and
  sends a new one.
- **The new-browser email cannot be sent.** The sign-in still completes; the failure is
  recorded.
- **The session ends while the change-password dialog is open.** The dialog closes with the
  sign-in screen; nothing was changed.
- **A very long name or role.** Shortened in the top bar with the full text on hover; never
  wraps the bar.
- **An address the platform has blocked** (025): refused before anything else, once that spec
  exists; this spec leaves the place for it.

## Requirements *(mandatory)*

### Functional Requirements

**The operator's account**

- **FR-001**: The platform MUST hold, for each operator, an account with: full name (1–200),
  email (unique among operators, kept in lower case, never changed after creation), mobile
  number (optional), interface language (Arabic by default), state — invited · active ·
  suspended — the count of wrong tries and "locked until", and the last sign-in (§3-9, §4-3).
- **FR-002**: An operator's account MUST belong to the platform and to no clinic. It MUST be
  separate from clinic team accounts: the same email may exist in both, with nothing shared.
- **FR-003**: There MUST be an operation, run at deployment and never reachable from a screen
  or an address of the console, that creates an operator as invited from a name and an email
  and emails a link to choose a password. Run again for an invited account it sends a new
  link and ends the earlier one; for an active account it changes nothing. It never shows,
  takes or sets a password. Every use of it MUST be recorded (clarified).
- **FR-003a**: The same tool MUST be able to withdraw an invited account that has not chosen a
  password: its links stop working at once and the account is removed, leaving the record of
  the invitation and of the withdrawal. It MUST refuse for an active or suspended account
  (clarified).
- **FR-004**: An account in the state invited MUST NOT be able to sign in or to ask for a reset
  link. Its link MUST live 72 hours, be usable once and be replaced by a newer one (SH-048).
- **FR-004a**: Opening a good first-password link MUST show the design's "Invite – Set
  Password" screen (`167:66`, `167:38`): the email shown and not editable, the new password
  and its confirmation, the rules of FR-029 listed and turning as each is met, and «تفعيل
  الحساب». Saving MUST make the account active and take the operator to the sign-in screen;
  it opens no session. A link that no longer works MUST say so and point to whoever sent it.
  Spec 011 sends its invitations to this same screen.

**Signing in**

- **FR-005**: The sign-in form MUST have email, password, «تذكرني», «نسيت كلمة المرور؟» and one
  primary button, with the wording of frames `128:85` and `128:54`.
- **FR-006**: The form MUST check before sending: email present and well-formed, password
  present; each failure is said under its field.
- **FR-007**: A correct password MUST NOT open a session. It MUST issue a six-digit code, send
  it to the account's email in the account's language, and show the code step (SH-040).
- **FR-008**: The email code MUST be required at every sign-in of every operator; nothing an
  operator can set removes it (clarified).
- **FR-009**: The platform MUST check in this order: blocked address → request limit → the
  account and its password → the account's state. What comes later is not evaluated when an
  earlier check refuses.
- **FR-010**: An unknown email, a wrong password, and any password for an invited account MUST
  get one identical answer — «بيانات الدخول غير صحيحة» with its line — and the same for a
  locked or suspended account when the password is wrong (SH-043, ق-٨٨).
- **FR-011**: The answers of FR-010 MUST be indistinguishable in content, form and time.
- **FR-012**: «هذا الحساب موقوف.» for a suspended account, and the lock warning for a locked
  one, MUST be shown only after a correct password.
- **FR-013**: When the code cannot be sent, the form MUST say so and nothing MUST be opened.

**The lock**

- **FR-015**: Each wrong password for an existing active account MUST count as one wrong try;
  the fifth MUST lock the account for fifteen minutes (SH-041). A wrong current password in
  the change-password dialog counts the same, and on the fifth the operator is signed out.
  Only wrong tries inside the attempt window count: a try made more than fifteen minutes
  before is forgotten, as in the clinic's sign-in (spec 003), and the window is platform
  configuration with the threshold (FR-019) (owner's decision, 12 Oct 2026, convergence T067).
- **FR-016**: A lock MUST end when its time has passed, when the account's owner completes a
  password reset, or when lifted by an operator allowed to (011) (SH-049, ق-١٢٥). Wrong tries
  during a lock MUST NOT lengthen it. That a stranger who knows an email can cause a lock is
  accepted: the account's owner opens it at once through "forgot password" (clarified).
- **FR-017**: The lock warning MUST give the time the lock ends, in the reader's language and
  digits, and say that resetting the password opens the account at once.
- **FR-018**: A completed sign-in MUST clear the count of wrong tries.
- **FR-019**: The threshold of five and the fifteen minutes MUST be platform configuration,
  not written into screens, so that spec 024 can make them settings.

**The code**

- **FR-020**: A code MUST be six digits, live ten minutes, allow five tries, and be usable
  once. After five wrong tries the code is spent and the sign-in starts again from the
  password (SH-040).
- **FR-020a**: Three codes spent in a row for one account — fifteen wrong tries with no
  completed sign-in between them — MUST lock the account as five wrong passwords do, so that
  knowing the password does not allow guessing codes without end. A completed sign-in clears
  the count (the rule of the clinic's sign-in, spec 003).
- **FR-021**: A new code MUST be available one minute after the last was sent, at most three
  times in one sign-in; sending one MUST make the earlier code useless. The wait is shown as a
  countdown, and after the third the operator starts again from the password.
- **FR-022**: A wrong, expired, used or replaced code MUST get one answer, which says a new
  code can be asked for.
- **FR-023**: The code MUST be bound to the sign-in that asked for it: it opens no other
  account and cannot be used from another browser.
- **FR-024**: The code's email MUST say what it is for, how long it lives, and that nobody
  from the platform will ask for it; it MUST NOT contain a link that signs in.

**Forgetting the password**

- **FR-025**: The forgot-password screen and its "sent" screen MUST read as frames `141:53`
  and `158:52` and their twins. The answer MUST be the same for every email.
- **FR-026**: A reset link MUST be sent only to an active account, locked or not; it MUST live
  thirty minutes, be usable once, and be replaced by a newer one (SH-048). At most one MUST be
  sent for an account in a minute.
- **FR-027**: Opening a good link MUST show a screen to choose the new password, laid out as
  the design's "set password" frame, with the rules of FR-029 listed and turning as each is
  met. A link that no longer works MUST say so and offer to send a new one.
- **FR-028**: Completing a reset MUST end every session of the account, lift a lock, clear the
  count of wrong tries, and make every waiting code and link useless (ق-٥٨).
- **FR-029**: A password MUST have at least 10 characters with an upper-case and a lower-case
  letter, a digit and a symbol, and at most 200 characters — the rule of the clinic's accounts
  (spec 004). The platform enforces it whatever the screen did. The design's invitation frame
  says eight characters; ten governs.

**Sessions**

- **FR-030**: An operator MUST have at most one session. Completing a sign-in MUST end any
  earlier session of the account, and the earlier device MUST be shown the sign-in screen with
  a line that says they signed in on another device (clarified). An open console MUST learn
  that its session has ended without being used, within thirty seconds; the question it asks
  to learn this is not activity (FR-032).
- **FR-031**: A session MUST end: on sign-out; after thirty minutes with no activity; twelve
  hours after sign-in, however active; when
  the password is changed by reset; when the account is suspended; on "sign out of all
  devices". An ended session MUST be refused at once, whatever the browser holds.
- **FR-032**: The thirty minutes and the twelve hours MUST be platform configuration (spec 024
  makes the timeout a setting, 15 to 10,080 minutes). Activity is something the operator
  does — opening a page, saving, searching; what a page asks for by itself while it is only
  open is not (clarified).
- **FR-033**: With «تذكرني» unticked the session MUST NOT outlive the browser being closed;
  ticked, it lasts until one of the ends of FR-031. The box never lengthens the thirty minutes
  or the twelve hours: a browser closed for more than thirty minutes finds the session ended
  whether or not the box was ticked, which is intended (clarified).
- **FR-033a**: When a sign-in completes from a browser the account has not signed in from
  before, an email MUST be sent to the operator, in their language, with the browser and
  system, the address and the time, and what to do if it was not them. No setting stops it. A
  failure to send it MUST NOT fail the sign-in (clarified).
- **FR-034**: What keeps a session alive MUST be renewed in use, and the presentation of a
  value already replaced MUST end the session and write a record of a reused session value
  (the event `session_token_reuse` of §4-5).
- **FR-035**: A session ended by someone or something other than the operator MUST show its
  reason on the sign-in screen: another device; «تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة
  أخرى.» (suspended); «تمت إعادة تعيين كلمة المرور، فتم تسجيل خروجك. سجّل دخولك بكلمة المرور
  الجديدة.» (reset); «انتهت الجلسة. الدخول من جديد للمتابعة.» (idle or longest life) (§9-3);
  and the lock warning when the fifth wrong current password signed them out (FR-015).
  The place for «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.» is kept for 011.
- **FR-036**: Signing out in one tab MUST sign out the browser's other tabs without a reload.
- **FR-037**: A session of the clinic's console MUST open nothing in the operators console,
  and the reverse.
- **FR-038**: The platform MUST keep, for each session: when it began, the browser and system
  it came from, its address, when it ended and why.

**Access**

- **FR-039**: Every address of the operators console MUST be refused without an operator's
  session, except those that sign-in itself needs. Inside, an address with no rule that names
  who may use it MUST be refused; the three things an operator does to their own account —
  reading their profile, changing their password, signing out everywhere — are allowed to
  every signed-in operator (ق-١, Constitution III).
- **FR-039a**: Every address of the operators console that changes something MUST refuse a
  request that did not come from the console itself — one forged from another site — whether
  or not a session accompanies it (clarified).
- **FR-040**: Until operators' roles exist (011) every signed-in operator reaches the frame,
  the home and their own profile, and nothing else.

**The profile**

- **FR-041**: The profile MUST show the operator's name, the name of their role, email and
  phone, as frames `1849:2791` and `1850:2824`; none is edited here.
- **FR-042**: The two-factor switch MUST be shown on and locked, with the line of frame
  `2185:567`.
- **FR-043**: «الجلسات الأخيرة» MUST list the account's last sessions, newest first, at most
  ten: browser and system, address, and state — «نشط الآن» and «الجلسة الحالية» for the open
  one, «انتهت منذ …» for the rest — with the "one session at a time" line.
- **FR-044**: «تسجيل الخروج من كل الأجهزة» MUST ask for confirmation, then end every session
  of the account and show the sign-in screen.
- **FR-045**: The change-password dialog MUST ask for the current password, the new one and
  its confirmation, with the errors of the design's note under their fields.
- **FR-045a**: A correct current password and a valid new one MUST NOT change the password by
  themselves: a six-digit code MUST be sent to the account's email and asked for in the
  dialog, with the rules of the sign-in's code — ten minutes, five tries, once, a new one
  after a minute. Only the right code changes the password; the session stays open and the
  success state of frame `1852:2169` is shown. Five wrong codes, the code's expiry or closing
  the dialog abandon the change. Wrong codes here do not count towards the lock of FR-020a
  (clarified).
- **FR-046**: A new password MUST differ from the current one.

**The frame**

- **FR-047**: Every page after sign-in MUST sit in one frame: the top bar and side bar of the
  design's "Navbar" and "Sidebar Nav Item", in Arabic right to left and English left to right.
- **FR-048**: The top bar MUST show the console's mark and name, the operator's name and
  role, and the account menu with «الملف الشخصي» and «تسجيل الخروج». The bell is added by the
  spec that builds it and is absent until then.
- **FR-049**: The side bar MUST list only sections that exist. In this spec that is the home.
  A later spec adds its section by declaring it, without changing the frame.
- **FR-050**: The home MUST be a placeholder until spec 022: a greeting by name and one line.
- **FR-051**: Loading, empty, not-found, error and no-connection MUST each have a state in the
  frame (SH-097).

**Records, logs and language**

- **FR-052**: Each of these MUST be recorded with who, when, from which address and browser: a
  completed sign-in; an invitation withdrawn by the deployment tool; a wrong password; a wrong code; a lock; a sign-out; a session ended and
  why; a reused session value; a reset asked for and completed; a password changed; a use of
  the operation of FR-003. They are kept so that the operators' audit log (023) and security
  events (025) can read them; this spec builds no screen for them.
- **FR-052a**: Records of FR-052 MUST be kept twelve months and then removed, until specs 023
  and 025 set the period for good (clarified).
- **FR-053**: A password, a code, a reset link and a session's value MUST NOT appear in a log
  or a record, and an email address MUST NOT appear in operational logs (SH-091, SH-094).
- **FR-054**: Every screen MUST exist in Arabic and in English. Before sign-in the language is
  the one last chosen in that browser, else the browser's, else Arabic, and can be changed on
  the screen; after sign-in it is the account's. Digits follow the language (SH-098).
- **FR-055**: Emails of this spec — the code, the reset link, the first-password link and the
  new-browser notice — MUST be written in the account's language and carry the platform's name.
- **FR-056**: «سياسة الخصوصية» MUST be in the footer of every screen before sign-in (SH-047).
  It leads to the address the policy will have; until the page exists (spec 086) that address
  shows the console's "not available yet" state, in the reader's language.

### Key Entities

- **Operator account**: a person who runs the platform — name, email, mobile, language, state
  (invited · active · suspended), wrong tries and "locked until", last sign-in. Belongs to no
  clinic. Its role is added by spec 011.
- **Sign-in code**: six digits for one sign-in in progress — when it was sent, when it
  expires, how many tries remain, whether it was used.
- **Password link**: what lets the owner of an account choose a password — for one account,
  once: a reset link lives thirty minutes, a first-password link 72 hours.
- **Known browser**: a browser an account has completed a sign-in from, kept so that a new one
  can be told apart.
- **Operator session**: one signed-in browser — when it began, where from, whether it should
  outlive the browser, when it ended and why. At most one open for an account.
- **Sign-in record**: one line for each event of FR-052, for the audit log and the security
  events to come.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator with their mailbox open signs in, from opening the address to
  seeing the console, in under one minute.
- **SC-002**: The code's email arrives within thirty seconds of the password being accepted,
  in 95 of 100 sign-ins. Before production this is measured on the local stack and recorded;
  the figure in production belongs to the platform's monitoring.
- **SC-003**: Across an unknown email, a wrong password, and a wrong password for a locked, a
  suspended and an invited account, the answers are identical in content and form, and their
  times cannot be told apart over a hundred tries each.
- **SC-004**: Of a thousand guessed passwords against one account, no more than five are
  evaluated in any fifteen minutes.
- **SC-005**: After a sign-in on a second device, the first device's session is refused on its
  very next action, and that device shows its reason within thirty seconds without a reload.
- **SC-005a**: A console left untouched is signed out between thirty and thirty-one minutes
  after the operator's last action.
- **SC-005b**: Every sign-in from a browser the account has not used before produces exactly
  one email to the operator, and a sign-in from a known browser none.
- **SC-006**: After sign-out, a password reset, a suspension or "sign out of all devices", no
  request with the ended session is accepted — not one.
- **SC-007**: A forgotten password is replaced, from pressing the link on the sign-in screen
  to being signed in again, in under three minutes.
- **SC-008**: No password, code, link or session value is found in any log or record after the
  full suite of scenarios has run.
- **SC-009**: Each screen of this spec, in Arabic and in English, matches its frame in the
  design file in layout, wording and states when laid beside it.
- **SC-010**: Every screen before sign-in and the profile can be completed with the keyboard
  alone, and their errors are announced by a screen reader.
- **SC-011**: A clinic team member's session opens nothing in the operators console, and an
  operator's nothing in a clinic's, across every address tried.

## Assumptions

- The operators console lives at its own address (constitution v1.1.0); this spec replaces the
  placeholder page of the `ops` application.
- The first operator's email is written in Arabic, the default language of an account.
- Operators' roles and permissions are spec 011. Here the account holds no role; the profile
  and the top bar show the role's name as soon as 011 gives one, and «مسؤول عام» for the first
  operator until then.
- The longest life of a session is the twelve hours spec 003 settled for the clinic's team;
  the thirty idle minutes are the operators' alone. Both are configuration until spec 024.
- The wait before a code can be sent again is one minute, as in spec 003; the design's
  countdown shows 00:47 of it.
- States the design does not draw take their words from the BRD or from spec 003, and are the
  owner's to confirm: the lock warning; a wrong or expired code; the code that could not be
  sent; too many requests; the link that no longer works; the line "you signed in on another
  device"; the confirmation of "sign out of all devices"; the language control before sign-in;
  the home's greeting; the not-found page.
- The screen that chooses a new password from a reset link is not drawn; it follows the
  design's "Invite – Set Password" frame with its title and line reworded for a reset, and the
  ten-character rule. That frame's line names a role («كمشرف»); until roles exist (011) the
  line is written without one.
- The operators' side bar in the design lists eight sections; each appears when its spec
  builds it.
- The emails are sent at once through the platform's mail sender, not through the
  notifications engine (whose rows belong to a clinic), with their own templates from the
  design's "Email Template" page; the bell of this console is not part of this spec.
- Records of FR-052 are kept apart from a clinic's audit trail (spec 007), which belongs to a
  clinic; how the operators' audit log presents them is spec 023's.
- The second step is a code by email (SH-040), and the reset link goes to the same mailbox:
  whoever controls an operator's mailbox controls the account. This is accepted for this
  phase; an authenticator app as a second factor would be a change to the BRD.
- The code step of the change-password dialog is not drawn; it reuses the code boxes and the
  countdown of the sign-in's code step, and its words are the owner's to confirm.
- Blocking an address (025) and the allowed-address list (024) do not exist yet; the order of
  checks keeps their place.
- A "not a robot" check is not shown on sign-in (SH-042).
