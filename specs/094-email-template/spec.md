# Feature Specification: Email Template

**Feature Branch**: `094-email-template`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: empty. Taken from row `094-email-template` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1, the operators console; added to the list
on 12 Oct 2026): "one template for every email of the platform, from the page 'Email Template'
of the operators' design file, in Arabic and English, and the emails already built moved onto
it (003 · 004 · 008 · 010)."

**BRD source** (`docs/reference/brd/source/`): `BRD-0-shared.md` — SH-002 (every surface in
Arabic and English, each person in their own language, Arabic right to left), SH-091 and SH-094
(no secret and no personal data in logs), SH-098 (the wording: plain standard Arabic, no
gendered address, controls named by their noun). `modules/17-ops-support-platform.md` — §9-4
(rules of sending: every email carries the platform's name, its text mark and the support email
in its footer; a mandatory email has no "unsubscribe"), §9-5 (the structure every drawn
template shares: the mark, a short line above the title, a title, the body, one button with its
link as text under it, a box of facts, the identity footer and the line "you are receiving this
email…"; sent in the recipient's language), `OP-B-447` (the drawn templates, in the recipient's
language), `OP-B-286` (a logo image in emails is for later: the mark is text) and §6-ح-١ (the
general settings that will hold the platform's two names, the support email and the support
phone — spec 024).

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY` (the operators console), page "Email
Template" (`447:2`), read on 12 Oct 2026. The shell (`447:3` English, `451:2` Arabic); the
drawn kinds Welcome (`448:21`, `454:2`), Notification (`449:2`, `455:2`), Invoice (`450:2`,
`456:2`) and OTP (`458:2`, `459:2`); and the emails "as sent": sign-in code (`2227:71`,
`2227:91`), password reset (`2227:111`, `2227:132`), clinic invitation (`2227:153`,
`2227:174`), operator invitation (`2227:195`, `2227:216`), subscription invoice (`2227:237`,
`2227:268`) and test email (`2227:299`, `2227:315`). The page also draws emails that later
specs send — urgent alert, renewal reminder, ticket update, subscription lifecycle, notices and
incidents, add-on invoice; they are read here for the blocks they need and built by their own
specs. Every email has an Arabic and an English twin.

## Overview

The platform already sends eleven emails, each built by the spec that needed it and each with
its own plain layout: the clinic team's sign-in code, reset link, "password changed",
invitation, "account created" and "access reset" (specs 003 and 004); the notification email
(spec 008); and the operator's code, reset link, first-password link and new-browser notice
(spec 010). The design file draws one look for every email of the platform. This spec builds
that look once, moves the eleven onto it, and gives later specs the blocks to compose theirs
without designing an email again.

It adds no new email and changes when no email is sent. It changes how every email looks, the
words of those the design draws, and — for the clinic team's account emails — that they are
written in one language, the recipient's, instead of two.

Out of this spec: the emails of later specs (invoices, reminders, tickets, notices, lifecycle,
urgent alerts, the test email of settings) — each is built by its own spec on this template;
the settings screen that holds the platform's name and contacts (024); the mail provider, its
delivery events and its statistics (024); a logo image (`OP-B-286`, later); and any email to a
patient (specs 067 and later, which will say whether they use this template).

## Clarifications

### Session 2026-10-12

- Q: Four emails are already built with drafted words and the design draws them with its own.
  Which words do they carry? → A: The design's. A safety line a spec requires and the design
  leaves out is kept as the small note under the body.
- Q: The clinic team's account emails are sent with Arabic and English in one message; the
  design and the BRD say one language. Which? → A: One language, the recipient's; Arabic when
  the account has not chosen one.
- Q: The footer shows the platform's name, an address, a support email and a phone, and the
  settings that will hold them do not exist. Where do they come from? → A: From the
  deployment's configuration; a line that is not set is left out. Spec 024 makes them settings.
- Q: In an Arabic email, are numbers written in Western digits (10) or Arabic-Indic (١٠)? → A:
  Western digits in every email, in both languages, as the design draws them.
- Q: The design's grey for body text and small notes is too faint for readers with low vision;
  are those greys darkened? → A: Yes: body text, notes and footer lines use the nearest darker
  grey that reaches a contrast of 4.5 to 1 with their ground.
- Q: Is a subject plain words saying what the message is, or does it carry a tag such as
  "[SCS]"? → A: Plain. The platform's name is the sender's name shown beside the subject, so the
  subject does not repeat it.
- Q: In Arabic emails, is the platform called «سمارت كلينك أو إس», as the design writes it, or
  "Smart Clinic OS" in Latin letters? → A: The design's Arabic name in Arabic emails, "Smart
  Clinic OS" in English ones; both are configured values.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every email of the platform looks like one product (Priority: P1)

Whoever receives an email from the platform — a doctor asked for a sign-in code, a receptionist
told of a booking, an operator invited to the console — opens a message that is plainly from
Smart Clinic OS and plainly one family with every other: the purple mark at the top, a short
line saying what kind of message it is, a title, a few lines of body, one clear thing to do
when there is something to do, and a footer that says who sent it, how to reach support and
why they received it.

**Why this priority**: it is the reason the spec exists. Until it is true the platform's
emails are plain text in a box, and each new spec would draw its own.

**Independent Test**: send any one email on the local stack, open it in the local mailbox
beside the shell's frame, in Arabic and in English.

**Acceptance Scenarios**:

1. **Given** any email of the platform, **When** it is opened, **Then** it is a white card on a
   light grey ground with, from the top: the mark «S» in a purple rounded square; a rule; the
   short line above the title, in purple; the title; the body; a rule; and the footer — the
   platform's name, its address, the support email and phone, and the line that says why the
   message was sent — as frames `447:3` and `451:2`.
2. **Given** a recipient whose language is Arabic, **When** the email is opened, **Then** every
   word is Arabic and the whole message reads right to left; for English, left to right. No
   email mixes the two languages.
3. **Given** an email with something to do, **When** it is opened, **Then** it has one button,
   and under it the same link written out in full, so that it can be copied when the button
   cannot be pressed.
4. **Given** an email with nothing to do, **When** it is opened, **Then** it has no button and
   no empty space where one would be.
5. **Given** two different emails, **When** they are laid side by side, **Then** the mark, the
   widths, the type, the colours and the footer are the same; only what they say differs.

---

### User Story 2 - The emails already sent move onto the template (Priority: P1)

The eleven emails the platform sends today arrive in the new look with nothing lost: the code
is still the code, the link still opens what it opened, each is still sent when it was sent and
to whom it was sent. Where the design draws an email, it carries the design's words; where the
design draws none, it carries the words it has, set in the template's parts.

**Why this priority**: a template nothing uses is not a product. Moving them is also the proof
that the blocks are enough.

**Independent Test**: on the local stack trigger each of the eleven — sign in as a clinic team
member and as an operator, ask for a reset on each console, change a password, add a team
member each way, reset one's access, cause one notification, sign in from a new browser — and
open each in the mailbox, in both languages.

**Acceptance Scenarios**:

1. **Given** a clinic team member or an operator who gave a correct password, **When** the code
   is sent, **Then** the email reads «رمز التحقق» / «تأكيد هويتك» / «استخدم هذا الرمز لإكمال
   تسجيل الدخول إلى ‹المنصّة›.», the six digits in a tinted box, «ينتهي هذا الرمز خلال ‹المدّة›.»
   and «لم تحاول تسجيل الدخول؟ قد يكون شخص ما يعرف كلمة مرورك — غيّرها الآن.», as frames
   `2227:91` and `2227:71`; and it has no button and no link that signs in.
2. **Given** an operator changing their password, **When** the code is sent, **Then** it is the
   same email with a body that says the code confirms a change of password, so that nobody
   takes it for a sign-in.
3. **Given** a reset was asked for on either console, **When** the email is sent, **Then** it
   reads «إعادة تعيين كلمة المرور» / «اختر كلمة مرور جديدة» / «وصلنا طلب لإعادة تعيين كلمة مرور
   حسابك في ‹المنصّة›. يعمل الرابط مرة واحدة وينتهي خلال ‹المدّة›.», the button «إعادة تعيين كلمة
   المرور», the link under it, and «لم تطلب ذلك؟ تجاهل هذه الرسالة — ستبقى كلمة مرورك كما هي.»,
   as frames `2227:132` and `2227:111`, with the time the link really lives.
4. **Given** a team member is added to a clinic with a link, or an operator is invited,
   **When** the email is sent, **Then** it is laid out as the invitation frames (`2227:174`,
   `2227:216` and their twins): «دعوة», a title, a body naming the clinic or the console and
   how long the link lives, the button «تعيين كلمة المرور», the link, and «لم تكن تتوقع هذه
   الرسالة؟ يمكنك تجاهلها.»
5. **Given** the four emails the design does not draw — "password changed", "account created"
   (the password handed over in person), "access reset" and the new-browser notice — **When**
   each is sent, **Then** it carries what it says today, as a short line, a title, a body and,
   where it lists facts, the box of facts. The two that give the address to sign in at — "account
   created" and "access reset" — give it as the button; the other two have no button.
6. **Given** a notification that is also sent by email (spec 008), **When** it is sent, **Then**
   its title is the email's title, its line the body, when it happened a fact in the box, and
   its address the button; and it still says that replies are not read.
7. **Given** any of the eleven, **When** it is sent before and after this spec, **Then** it
   goes to the same person, on the same event, with the same code or link and the same life.
8. **Given** a clinic team member whose language is English, **When** an account email is sent
   to them, **Then** it is in English alone; for one who has chosen no language, Arabic alone.

---

### User Story 3 - An email reads wherever it is opened (Priority: P2)

People read email in programs the platform does not choose: one that blocks pictures, one that
drops a page's styles, a phone held upright, a dark theme, a program that shows text only, a
screen reader. In each the message is whole and its one action reachable.

**Why this priority**: a sign-in code that cannot be read locks its owner out. But it follows
the template existing.

**Independent Test**: open the catalogue's emails in the mail programs named in the
assumptions, at a phone's width, in a dark theme, with pictures blocked and as text only.

**Acceptance Scenarios**:

1. **Given** a mail program that loads nothing from the internet, **When** an email is opened,
   **Then** nothing is missing: the mark is text on a coloured square, there is no picture, no
   font to fetch and nothing that reports the message was opened.
2. **Given** a screen 360 wide, **When** an email is opened, **Then** the card fills the width
   with its margins, nothing is cut off and nothing scrolls sideways; a long link or address
   breaks rather than widens the message.
3. **Given** a program that shows text only, **When** an email is opened, **Then** the same
   words are there in the same order — the short line, the title, the body, the code or the
   link written out, the facts, the footer — with nothing that reads as leftover markup.
4. **Given** a dark theme, **When** an email is opened, **Then** every word can be read and the
   button can be told from its ground.
5. **Given** a screen reader, **When** an email is read, **Then** it is announced in its
   language and direction, the title is a heading, the layout's scaffolding is not read out,
   and the code is read as six separate digits.
6. **Given** a program that does not have the design's typeface, **When** an email is opened,
   **Then** it is set in that program's own Arabic or Latin face and keeps its sizes and
   weights.

---

### User Story 4 - Later specs compose their emails from the same parts (Priority: P2)

A spec that needs a new email — an invoice, a renewal reminder, a ticket update — writes what
it says and chooses the parts: a short line, a title, paragraphs, a box of facts with a status
mark, a button. It designs nothing and cannot get the footer or the direction wrong.

**Why this priority**: the page draws nine more families of email, and every one is these
parts. Giving them now is what keeps the look from drifting.

**Independent Test**: from the parts alone, with no new layout, build samples of the three
drawn kinds not sent yet — a ticket notification, an invoice, a subscription notice — and lay
each beside its frame.

**Acceptance Scenarios**:

1. **Given** the parts of the template, **When** an email is composed, **Then** these are
   available and can be put in the order the frames use: the short line; the title; one or more
   paragraphs; the code box; the button with its written-out link; a box of facts — rows of a
   name and a value, a row that stands out as the total, and a status mark in one of the
   design's tones; a tinted box for one thing that needs attention; a small note; the footer's
   reason line.
2. **Given** a sample invoice built from the parts, **When** laid beside frame `2227:237`,
   **Then** it matches: the rows «Invoice», «Due date» and «Amount due» with their values at
   the far edge, the button, the link, and a line under them.
3. **Given** a sample subscription notice built from the parts, **When** laid beside frame
   `2593:183`, **Then** it matches: the rows, the rule, the total line and the status mark.
4. **Given** a sample ticket notification, **When** laid beside frame `449:2`, **Then** it
   matches: the tinted box with its mark, the ticket's line and the line under it.
5. **Given** an email composed from the parts, **When** it is produced, **Then** its text-only
   form is produced with it from the same content; nobody writes an email twice.
6. **Given** a catalogue of every email the platform can send, **When** a person opens it on
   the local stack, **Then** they see each email in Arabic and English with sample values,
   without triggering the event that sends it.

---

### User Story 5 - The footer says who is writing, from one place (Priority: P3)

Every email ends with the platform's name, where it is, and how to reach support. These are set
once where the platform is deployed, and every email follows; a detail that is not set is not
shown.

**Why this priority**: needed for a complete footer, but an email without a postal address is
still a good email.

**Independent Test**: set the name, the support email, the phone and the address, send an
email; unset the phone and the address, send it again.

**Acceptance Scenarios**:

1. **Given** the platform's Arabic and English names, support email, support phone and address
   are set, **When** any email is sent, **Then** its footer shows the name in the email's
   language, the address, and the support email and phone on one line, as the shell's frames.
2. **Given** the phone or the address is not set, **When** an email is sent, **Then** that line
   or that part is left out, with no gap and no placeholder.
3. **Given** the support email in the footer, **When** it is pressed, **Then** it starts a
   message to support.
4. **Given** the platform is started outside local use without its name or its support email,
   **When** it starts, **Then** it refuses and says which is missing.

---

### User Story 6 - An email carries nothing it should not (Priority: P3)

An email is a letter that crosses machines the platform does not own. It holds what its reader
needs to act and nothing else, points only where the platform means it to, and what was written
by a person cannot change its shape.

**Why this priority**: the emails are already careful; this keeps the care when their words
and layout change.

**Independent Test**: run the suite of scenarios, then read every email produced and every log
line written.

**Acceptance Scenarios**:

1. **Given** a name, a clinic's name or a notification's text that contains markup or a link,
   **When** it is placed in an email, **Then** it is shown as written and can neither change
   the layout nor become a link.
2. **Given** any email, **When** its links are read, **Then** each goes to an address of the
   platform or starts a message to support; nothing else.
3. **Given** an email that is always sent — a code, a reset, an invitation, a security notice
   — **When** it is opened, **Then** it offers no way to stop receiving it.
4. **Given** an email is built and sent, **When** the logs are read, **Then** they hold neither
   its words, its code, its link nor the address it went to.

### Edge Cases

- **A very long name** — a clinic called by a whole sentence, an operator with five names: it
  wraps inside the card; it never widens it or runs under the edge.
- **A link several hundred characters long**: the button carries it, and the written-out link
  breaks over as many lines as it needs; it is never shortened with "…", since a shortened link
  cannot be copied.
- **A number in an Arabic email** — «ينتهي هذا الرمز خلال 10 دقائق»: Western digits, and the
  sentence still reads in order.
- **English or digits inside an Arabic sentence** — an email address, an invoice number, a
  clinic's Latin name: each keeps its own order inside the right-to-left line.
- **The recipient's language is not known** — an invitation to someone who has never signed
  in: Arabic.
- **A language the platform does not have**: Arabic.
- **A mail program that strips every style**: the message is the words in order, one under the
  other, still readable.
- **A program that turns the code into a link** (some take six digits for a phone number): the
  code is written so that it is not, as far as a message can ask for that.
- **A subject longer than a mail program shows**: what matters comes first; nothing in front of
  it is spent on a tag or the platform's name.
- **A notification whose title is very long** (spec 008 allows it): the title wraps; the
  subject is cut at a word with "…".
- **The same email sent twice** (a resend): the two are the same but for their code or link.
- **The footer's address in an Arabic email when only an English address is set**: the address
  is shown as set, in its own direction.
- **Something that cannot be composed** — a part given no content, a button with no address:
  refused when the email is built, not sent half made.

## Requirements *(mandatory)*

### Functional Requirements

**The template**

- **FR-001**: Every email the platform sends to a clinic's team or to an operator MUST be laid
  out by one template, as frames `447:3` and `451:2`: a card on a light ground holding, in this
  order, the header with the mark, the body, and the footer, with a rule between each.
- **FR-002**: The mark MUST be the letter «S» in white on a purple rounded square, as text; the
  template MUST use no picture (`OP-B-286`).
- **FR-003**: The body MUST begin with a short line above the title that says what kind of
  message it is, then the title; both are required for every email.
- **FR-004**: The footer MUST show the platform's name in the email's language, its address,
  the support email and the support phone, and a line that says why the recipient received the
  message. Each email supplies its own reason line; the default is «وصلتك هذه الرسالة بسبب نشاط
  على حسابك في ‹المنصّة›.» / "You're receiving this email because of activity on your ‹platform›
  account."
- **FR-005**: The template MUST use the design's measures and colours: the card 600 wide at
  most, the purple of the mark, the short line and the button, the title's dark, the body's and
  the notes' greys, the rules' light grey, and the sizes of frames `447:3` and `451:2`.
- **FR-006**: Every word of an email MUST have a contrast of at least 4.5 to 1 with its ground.
  Where a grey of the design does not reach it — the body's, the notes' and the footer lines' —
  the nearest darker grey that does MUST be used in its place, the same one wherever that grey
  is drawn, and the difference from the frames recorded (clarified). The purple of the short
  line and of the support line is held to the same measure.

**Language and direction**

- **FR-007**: An email MUST be written in one language: the recipient's account language;
  Arabic when the account has chosen none or the language is not one the platform has (SH-002,
  clarified).
- **FR-008**: An Arabic email MUST read right to left as a whole and an English one left to
  right; an email address, a link, a code, a number or a Latin name inside an Arabic line MUST
  keep its own left-to-right order.
- **FR-009**: The subject MUST be in the email's language and say only what the message is, in
  plain words with what matters first — «رمز التحقق», "Reset your password". It MUST carry no
  tag in front and MUST NOT repeat the platform's name: the name is the sender's name, which a
  mail program shows beside the subject (clarified). The earlier emails' two-language subjects
  and their "— Smart Clinic OS" endings go.
- **FR-009a**: Every email MUST be sent under the platform's name as the sender's name — its
  Arabic name for an Arabic email, its English name for an English one — from the platform
  identity (FR-023), so that an email is known for the platform's before it is
  opened. The platform's name is still written in the footer of every email (FR-004), which is
  how the emails of spec 010 keep "carrying the platform's name".
- **FR-010**: The line a mail program shows beside the subject MUST be the email's own first
  words of substance, not its scaffolding.
- **FR-011**: Dates, times and amounts in an email MUST be in Cairo time and the Egyptian pound
  (SH-001), with the words of the email's language and Western digits (0–9) in both languages,
  as the design's frames write them (clarified). This holds for every recipient: an email to a
  clinic's team does not follow the clinic console's Arabic-Indic digits (`CL-SH-064`), since
  one template serves both audiences.

**The parts**

- **FR-012**: The template MUST offer these parts, each as the design draws it, to be used in
  the order an email needs: paragraphs of body; the code box (frames `458:2`, `459:2`); one
  button with the same link written out under it after «أو انسخ هذا الرابط في متصفحك:» / "Or
  copy this link into your browser:"; a box of facts; a tinted box for one thing needing
  attention; a small note; and the reason line of the footer.
- **FR-013**: The box of facts MUST hold rows of a name and a value with the value at the far
  edge, MAY hold a rule and a row that stands out as the total, and MAY hold a status mark in
  one of the design's tones — neutral, good, warning, bad (frames `2227:237`, `2593:183`).
- **FR-014**: An email MUST have at most one button. The written-out link MUST be the whole
  link, never shortened.
- **FR-015**: The code box MUST show the code in large, spaced digits, left to right in both
  languages, in Western digits.
- **FR-016**: A part given no content MUST NOT be drawn, and MUST leave no gap.
- **FR-017**: Composing an email with a missing short line, title or reason, or a button
  without an address, MUST be refused when the email is built.
- **FR-018**: The text-only form of an email MUST be produced from the same content as its
  laid-out form, in the same order, with the link and the code written out.

**Reading everywhere**

- **FR-019**: An email MUST be complete with nothing loaded from the internet: no picture, no
  fetched font, no stylesheet from elsewhere, and nothing that reports that it was opened.
- **FR-020**: An email MUST be readable from 320 wide upward without scrolling sideways; long
  words, links and addresses MUST break inside the card.
- **FR-021**: An email MUST remain readable when the mail program applies a dark theme, drops
  the message's styles, or lacks the design's typeface.
- **FR-022**: An email MUST declare its language and direction, mark its title as a heading,
  keep its layout's scaffolding out of what a screen reader reads, and present the code so that
  it is read digit by digit.

**The platform's identity**

- **FR-023**: The platform's Arabic name, English name, support email, support phone and
  address MUST come from one place in the deployment's configuration, read by every email, so
  that spec 024 can make them settings without touching an email (clarified).
- **FR-024**: The name and the support email MUST be required outside local use; the phone and
  the address are optional, and a part that is not set MUST be left out of the footer.
- **FR-025**: No email MUST write the platform's name, the support email or a console's address
  as fixed words of its own. Wherever an email names the platform it MUST use the name of its
  own language: «سمارت كلينك أو إس» in an Arabic email and "Smart Clinic OS" in an English one
  are the values the design's frames show and the local defaults (clarified).

**The emails already sent**

- **FR-026**: These eleven emails MUST be built on the template and by its parts alone: for the
  clinic's team — the sign-in code, the reset link, "password changed", the invitation with a
  link, "account created" and "access reset"; the notification email; for operators — the code
  (for a sign-in and for a change of password), the reset link, the first-password link and the
  new-browser notice.
- **FR-027**: The sign-in code, the password reset and the two invitations MUST carry the words
  of their frames (`2227:71`–`2227:216`), with the name of the clinic or console, the inviter
  and role when there is one, and the time the code or link really lives in place of the
  frames' sample values (clarified).
- **FR-028**: Where a spec requires a line the frame does not have, it MUST be kept as the small
  note: for a code, that nobody from the platform will ask for it (spec 010, FR-024). A code's
  email MUST have no button and no link that signs in.
- **FR-029**: The four emails the design does not draw MUST keep what they say today, set in
  the template's parts, with a short line and a title added; their added words are the owner's
  to confirm.
- **FR-030**: The notification email MUST take its title, its line, its time and its address
  from the notification, as today, and keep the line that replies are not read.
- **FR-031**: Each of the eleven MUST be sent on the same event, to the same recipient, with
  the same code or link and the same life as before this spec. No test of specs 003, 004, 008
  or 010 that holds what an email does may be weakened to pass.
- **FR-032**: The clinic's account emails MUST stop being written in two languages (clarified).

**Care**

- **FR-033**: Whatever a person wrote that is placed in an email — a name, a clinic's name, a
  notification's text — MUST be shown as written and MUST NOT be able to change the layout or
  become a link.
- **FR-034**: Every link in an email MUST go to an address of the platform's own — the clinic
  console's, the operators console's or the patients' pages — or start a message to the support
  email. A spec that needs another destination — a payment page
  — names it in that spec.
- **FR-035**: An email that is always sent MUST NOT offer a way to stop receiving it (§9-4).
- **FR-036**: The words of an email, its code, its link and its recipient's address MUST NOT
  appear in a log (SH-091, SH-094).

**Seeing them**

- **FR-037**: There MUST be a catalogue of every email the platform can send, each in Arabic
  and English with sample values, that a person can open on the local stack without causing the
  event that sends it. It MUST NOT exist where the platform runs for real.
- **FR-038**: The catalogue MUST also hold a sample of each drawn kind not sent yet — a ticket
  notification, an invoice, a subscription notice — built from the parts alone, as the proof
  that later specs need no new layout.
- **FR-039**: A spec that adds an email MUST add it to the catalogue; an email that is not in
  the catalogue MUST be caught before merge.

### Key Entities

- **Email template**: the one layout — header with the mark, body, footer — in two directions.
- **Part**: a piece of an email's body that the design draws once: paragraph, code box, button
  with its link, box of facts, attention box, note.
- **Email**: what one message says, made of a language, a subject, a short line, a title, parts
  and a reason line; it yields a laid-out form and a text-only form.
- **Platform identity**: the Arabic and English names, the support email and phone and the
  address, set once for the deployment.
- **Catalogue**: every email the platform can send, with sample values, for looking at.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All eleven emails the platform sends today arrive on the template; not one is
  left in its earlier layout.
- **SC-002**: Each of the four emails the design draws matches its frame in layout and wording
  when laid beside it, in Arabic and in English; each of the three sample kinds matches its
  frame in layout.
- **SC-003**: Every email in the catalogue, in both languages, is whole at 320, 360 and 600
  wide: nothing cut off, nothing outside the card, no sideways scroll.
- **SC-004**: Every email in the catalogue is complete with all loading from the internet
  blocked, and its text-only form holds every word of its laid-out form in the same order.
- **SC-005**: Every email in the catalogue can be read in a dark theme, and every word of it has
  a contrast of at least 4.5 to 1 with its ground in the light theme it is designed in.
- **SC-006**: In a sign-in code's email the code is the largest text of the message and is whole
  inside the first screen of a phone 360 wide by 640 high, so that it is found without
  scrolling.
- **SC-007**: After this spec the same events send the same emails to the same people with the
  same codes and links: every earlier test of what an email does still passes unchanged in what
  it holds.
- **SC-008**: Changing the platform's name or support email in one place changes it in every
  email, with no other change.
- **SC-009**: No email holds a link to anywhere but the platform's consoles and its support
  address, and none holds more than one button.
- **SC-010**: No email's words, code, link or recipient is found in any log after the full
  suite of scenarios has run.
- **SC-011**: A new email of a drawn kind is composed from the parts without a new layout: the
  three samples are built that way.

## Assumptions

- The template is the platform's, not a clinic's: an email to a clinic's team carries the
  platform's mark and purple, as the design's "clinic invitation" does. A clinic's own name and
  mark on emails to its patients belong to the specs that send those.
- "Reads wherever it is opened" is checked in the local mailbox, in Chromium at the three
  widths and in Edge at the design's width, in a dark theme, with loading blocked and as text only. It is **not** checked
  in Outlook for Windows, Gmail or Apple Mail by this spec's tests: there is no way to do that
  from this machine. The layout is built by the conservative means those programs are known to
  keep, and a check in the real programs is the owner's before release.
- The design's typeface is Cairo. A mail program rarely has it and it is not fetched (FR-019):
  recipients see their program's own face. The pictures laid beside the frames are taken where
  Cairo is installed, or the difference of face is recorded.
- The frames' sample values — «12 شارع التحرير، القاهرة», `support@smartclinicos.com`,
  `+20 100 000 0000`, "30 minutes" for an invitation — are samples. The real name, address and
  contacts are the owner's to supply; lifetimes are the system's (an invitation lives 72
  hours).
- The Arabic name of the platform in the frames is «سمارت كلينك أو إس»; the emails built so far
  write "Smart Clinic OS" in both languages. By the owner's decision the frames govern in
  emails. The consoles' screens, which also write the Latin name in Arabic, are not changed by
  this spec; whether they follow is the owner's to say.
- The design's "clinic invitation" is the invitation of a clinic's owner when the clinic is
  created (spec 015). Its layout and words are the model here for the invitation of a team
  member added inside a clinic (spec 004); the owner's invitation itself is 015's.
- The design's "operator invitation" names who invited and in what role (spec 011). The first
  operator, invited by the deployment command (spec 010), has neither: its body says they were
  added to the console, and 011 fills in the rest.
- A person's language is the one kept with their account (specs 006 and 010). A clinic team
  member's is known once they have chosen it in the console.
- The design's greys for body text and notes do not reach the contrast FR-006 asks for. The
  greys used in their place are named in the plan; the emails therefore read slightly darker
  than the frames, by the owner's decision.
- Emails to patients, and messages on WhatsApp and SMS, are not this spec's.
- Nothing is stored by this spec: no table, no setting screen.
