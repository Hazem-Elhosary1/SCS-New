# Feature Specification: Notifications Engine

**Feature Branch**: `008-notifications-engine`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: empty. Taken from row `008-notifications-engine` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "the bell and email, the table of
events, who is told and who can switch it off."

**BRD source**: `BRD-0-shared.md` — SH-072 (every notification event has: who is told, by which
channel, and who may switch it off), SH-070 (nothing clinical leaves on a messaging channel),
SH-094 (no personal data in operational logs), SH-098 (wording). The unified table of events:
Chapter 13 §9 (ن-٠١ … ن-٣٨), its data in §3-4 and §3-5, its rules ق-٣٠ … ق-٣٨ and ق-٥٧. The
bell: Chapter 01 §أ-٥-٤, rules أ-ق١٣ … أ-ق١٧, أ-ق٢٢ and أ-ق٢٧, requirements CL-SH-026 …
CL-SH-032, CL-SH-186 and CL-SH-187.

**Design source**: Figma file `4HLWEqlna9sbn68276LHYt`. The bell and its panel were built as a
frame by spec 006 from these frames, and this spec fills them with real notifications, so the
same frames are its design: page "Home" (`4160:3`), "Shell / Notifications Open" (`5999:831798`,
LTR `5999:834947`) and "Shell / Notifications Empty" (`5999:832473`, LTR `5999:835622`); page
"Components — Feedback" (`4228:4`), component "Notification Panel" (`4262:1056`).

What the frames fix about an item, read on 11 Oct 2026: an icon in a round tinted mark for its
kind; a title of a few words («ردّ فريق الدعم على تذكرتك» · "Support replied to your ticket",
«صدرت فاتورة التجديد» · "Renewal invoice issued", «ينتهي اشتراكك خلال ٧ أيام» · "Your
subscription ends in 7 days", «تم استلام الدفعة» · "Payment received"); one line under it that
names the record by its identifier and one fact («التذكرة T-1046 · تأخّر وصول رسائل واتساب»,
«الفاتورة INV-2026-0012 · تستحق في ٣ أكتوبر», «الفاتورة INV-2026-0011 · ٤٬٥٠٠ ج.م»); a time that is
relative when recent and a date when not («منذ ساعتين» · "2h ago", «اليوم، ٩:٠٠ ص» · "Today,
9:00 AM", «أمس» · "Yesterday", «١٧ سبتمبر» · "17 Sep"); an unread item tinted with a dot at the
far end; the heading «الإشعارات» · "Notifications" with «تعليم الكل كمقروء» · "Mark all read".

**Undrawn** (raised with the owner): the icon and tint for the events that are none of the four
drawn kinds — a channel outage, a used-up allowance, a stopped booking page, a refund, an
expired offer, a follow-up that is due, a result out of range, a sign-in from a new device, an
operator's action on a user, a licence about to expire; every email this spec sends (no email
frame exists; they follow the emails of specs 003 and 004); the error state of the panel
(CL-SH-187, built by spec 006 from the shared error state). The component's empty state reads
«لا شيء هنا بعد» while the BRD and the page frame say «لا إشعارات جديدة» (CL-SH-030): spec 006
followed the BRD, and this spec does not reopen it.

**Depends on**: 006-app-shell (the bell, its panel and the source they read from). Built on 002
(isolation), 003 (accounts, sessions, the shared email sender), 005 (permissions, by which
recipients are found) and 007 (the audit trail).

## Overview

Things happen in a clinic that somebody needs to know about: the support team answered a ticket,
an invoice was issued, a licence is about to expire, somebody signed in to an account from a
device it never used. Until now the platform can tell nobody. The bell is in the top bar and
always says "nothing new".

This spec delivers the one mechanism every later spec uses to tell people:

- **One table of events**, kept with the product: for each event its name in both languages,
  its group, who is told, whether it is also emailed — always, while the clinic keeps its
  switch on, or never — and when it repeats before a date.
- **One way to raise an event.** A later spec says "this happened, about this record"; the
  engine works out who is told, puts a notification in each of their bells, and sends the email
  when the event calls for one.
- **The bell with real notifications**: newest first, unread ones marked, each opening the
  place it is about, read state kept for each person.
- **Email that follows the rules**: urgent events always, billing and licence events while the
  clinic's switch is on, clinical events never.
- **The clinic's email switches**, kept and obeyed here; the page that shows them is spec 084.
- **The first real event**: a sign-in from a device the account never used tells its owner, in
  the bell and by email.

**No new screen is built.** The bell and its panel exist (spec 006). The settings page
«التنبيهات» is spec 084; the support team's notices, their dialog and the incident banner are
spec 083; the weekly summary email is spec 060; what a patient receives is the channels'
business.

## Clarifications

### Session 2026-10-11

- Q: What should count as a "new device" for the «الدخول من جهاز جديد» notification? → A: A browser the account has never completed a sign-in from; the network is not considered. The BRD's ق-٥٧ ("a device or a network") is to be reworded by the owner.
- Q: How should a notification already in someone's bell read after the record it is about changes? → A: It keeps the facts as they were when the event was raised; only the language follows the reader.
- Q: When many events of the same kind reach one person in a short time, are they gathered? → A: No. One notification, and where the event is emailed one email, for each event.
- Q: Are notifications older than the ninety days the bell shows removed or kept? → A: Removed: a daily clean-up deletes notifications older than ninety days, and the records of their emails with them.
- Q: Is a waiting email still sent when its recipient's account was disabled or removed after the event? → A: No. At the moment of sending, an email to an account that can no longer sign in is dropped.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - What concerns a person reaches their bell (Priority: P1)

Something happens that a person should know about. Without reloading anything, their bell shows
a dot; opening it shows the notification at the top — its mark, its title, one line saying which
record and one fact about it, and when.

**Why this priority**: it is the reason the engine exists; every later event depends on it.

**Independent Test**: raise an event for a clinic and read the bell of a person who should be
told.

**Acceptance Scenarios**:

1. **Given** an event is raised about a record, **When** a person who should be told opens the
   bell, **Then** one notification for it is at the top, with the event's title, a line naming
   the record by its identifier and one fact, and its time.
2. **Given** a person has the console open, **When** an event that concerns them is raised,
   **Then** the bell shows its dot within a minute, without the page being reloaded.
3. **Given** several notifications, **When** the panel is opened, **Then** they are newest
   first, the unread ones tinted with a dot, and the panel scrolls; there is no "all
   notifications" page.
4. **Given** a person with no notifications, **When** they open the bell, **Then** the panel
   says «لا إشعارات جديدة», with no «تعليم الكل كمقروء» and no dot on the bell.
5. **Given** the notifications cannot be loaded, **When** the panel is opened, **Then** it shows
   the error state with «إعادة المحاولة», and the rest of the console works.
6. **Given** the change that raised an event did not take effect, **When** anyone opens their
   bell, **Then** no notification of it exists.

---

### User Story 2 - Only the people the event names are told (Priority: P1)

Each event says who is told: the holders of a permission, the owner of the record, a named
person, or everyone in the clinic. Nobody else is told — not a colleague without the
permission, not anyone in another clinic.

**Why this priority**: a notification shown to the wrong person leaks what they may not see.
Isolation and permissions are non-negotiable.

**Independent Test**: raise events of each recipient rule in a clinic with people of different
roles, beside a second clinic; list who received what.

**Acceptance Scenarios**:

1. **Given** an event told to the holders of a permission, **When** it is raised, **Then**
   every person of the clinic whose role holds that permission at that moment receives it, and
   nobody else.
2. **Given** an event told to the owner of a record, **When** it is raised, **Then** that
   person receives it; where the table adds the holders of a permission, they receive it too,
   and a person who is both receives it once.
3. **Given** an event told to everyone, **When** it is raised, **Then** every person of the
   clinic who can sign in receives it.
4. **Given** two clinics, **When** an event is raised in one, **Then** nobody of the other
   receives anything, and no way of reading returns it to them.
5. **Given** a person whose account is disabled or was never activated, **When** an event that
   would name them is raised, **Then** they receive nothing.
6. **Given** a person who lost the permission after a notification reached them, **When** they
   open the bell, **Then** the notification is still theirs to read, but the place it leads to
   answers as it does for anyone without the permission.
7. **Given** a person who gains the permission later, **When** they open the bell, **Then**
   they do not find notifications of events raised before.

---

### User Story 3 - Reading a notification is the person's own business (Priority: P1)

Pressing a notification marks it read and takes the person to what it is about. «تعليم الكل
كمقروء» marks them all. What one person has read says nothing about a colleague's bell.

**Why this priority**: a bell that cannot be cleared is noise, and people stop looking at it.

**Independent Test**: give two people the same notification; one reads it; check both bells.

**Acceptance Scenarios**:

1. **Given** an unread notification, **When** it is pressed, **Then** it is read, the panel
   closes, and the person is at the place it is about.
2. **Given** unread notifications, **When** «تعليم الكل كمقروء» is pressed, **Then** all are
   read and the dot is gone; nothing else changes — no passing notice, no dialog.
3. **Given** two people who received the same notification, **When** one reads it, **Then** it
   stays unread for the other.
4. **Given** a person signed in on two browsers, **When** they read a notification on one,
   **Then** the other shows it read within a minute.
5. **Given** a notification whose record no longer exists or which leads nowhere, **When** it
   is pressed, **Then** it is read and the person stays where they were, or reaches the
   record's own "not found".
6. **Given** another person's notification, **When** someone tries to read it or mark it by its
   identity, **Then** it is as if it did not exist.

---

### User Story 4 - Email follows the event, and never carries anything clinical (Priority: P2)

Some events are also emailed. Urgent ones always. Billing and licence ones while the clinic
keeps their switch on. Clinical ones never. The email says what happened in the reader's
language and leads to the same place the bell does.

**Why this priority**: people are not always in the console; but the bell alone already makes
the engine useful.

**Independent Test**: raise one event of each email kind with the switch on and off; look at
what was sent.

**Acceptance Scenarios**:

1. **Given** an event emailed always, **When** it is raised, **Then** each recipient gets one
   email, whatever the clinic's switches say.
2. **Given** an event emailed while its switch is on, **When** it is raised with the switch on,
   **Then** each recipient gets one email; with the switch off, none — and the bell
   notification arrives either way.
3. **Given** an event never emailed, **When** it is raised, **Then** no email is sent to
   anyone.
4. **Given** the mail server is unreachable, **When** an event is raised, **Then** the action
   that raised it succeeds, the bell notification is there, and the email is tried again until
   it is sent or given up after a day.
5. **Given** any email this engine sends, **When** it is read, **Then** it holds no clinical
   content, no password and no code; it names the record by its identifier and leads to the
   console.
6. **Given** a recipient whose language is English, **When** the email is sent, **Then** it is
   in English; otherwise it is in Arabic.

---

### User Story 5 - A sign-in from a new device tells the account's owner (Priority: P2)

A person signs in from a browser their account has never signed in from. They find a
notification in the bell and an email: when, and from what device. If it was not them, they
know at once. Nobody can switch this off.

**Why this priority**: it is the one event the platform can raise today, so it proves the
engine end to end; and it is a security signal the BRD requires (ق-٥٧, CL-SB-149).

**Independent Test**: sign in to a seeded account from a fresh browser, then again from the
same one.

**Acceptance Scenarios**:

1. **Given** an account that has signed in before, **When** a sign-in completes from a browser
   it never used, **Then** its owner receives «الدخول من جهاز جديد» in the bell and by email,
   saying when and from what device.
2. **Given** the same browser, **When** the account signs in from it again, **Then** nothing
   new is sent.
3. **Given** an account signing in for the very first time, **When** the sign-in completes,
   **Then** nothing is sent: there is no earlier device to be new against.
4. **Given** a sign-in that fails or is refused, **When** it ends, **Then** nothing is sent.
5. **Given** the clinic's email switches, **When** any of them is turned off, **Then** this
   email still arrives.

---

### User Story 6 - A clinic decides which billing and licence emails it gets (Priority: P2)

Four events can have their email switched off for the whole clinic: «صدرت فاتورة», «استُلمت
الدفعة», «قرب انتهاء الاشتراك» and «اقتراب انتهاء رخصة». The switches are kept here and obeyed
here; the page that shows them comes with spec 084. Turning one off never stops the bell.

**Why this priority**: the rule "who can switch it off" is part of every event (SH-072); the
page is another spec's.

**Independent Test**: change a switch through the platform as a person with and without
«إدارة التنبيهات»; raise the event.

**Acceptance Scenarios**:

1. **Given** a new clinic, **When** its switches are read, **Then** all four are on.
2. **Given** a person holding «إدارة التنبيهات», **When** they turn a switch off, **Then** it
   is off for the whole clinic from the next event on, and the change is on the audit trail
   with the value before and after.
3. **Given** a person without «إدارة التنبيهات», **When** they try to change a switch, **Then**
   they are refused as for anything their role does not open; a person holding «عرض الإعدادات»
   alone can read the switches.
4. **Given** an event whose email is "always" or "never", **When** someone tries to switch it,
   **Then** it is refused: it has no switch.
5. **Given** one clinic's switch is off, **When** the same event is raised in another clinic,
   **Then** that clinic's recipients are emailed as before.

---

### User Story 7 - A later spec adds its event without touching the engine (Priority: P3)

The spec that builds tickets, invoices or licences adds a row to the table of events and says
"this happened" where it happens. Recipients, the bell, the email, the switch and the two
languages follow from the row.

**Why this priority**: ninety specs come after this one; the engine must not be reopened by
each.

**Independent Test**: add an event in a test alone and raise it; nothing in the engine changes.

**Acceptance Scenarios**:

1. **Given** a new event in the table, **When** it is raised, **Then** it reaches its
   recipients' bells and, by its email kind, their mail — with no change to the engine.
2. **Given** an event that repeats before a date (fourteen, seven and one day before a renewal;
   sixty, thirty and seven before a licence expires), **When** the same reminder for the same
   record is raised twice, **Then** recipients are told once.
3. **Given** an event that is not in the table, **When** something tries to raise it, **Then**
   it is refused.
4. **Given** the table, **When** the checks run, **Then** every event has its name in Arabic
   and English, a group, a recipient rule and an email kind, and a key that was used is never
   renamed or reused.

---

### User Story 8 - A notification reads in the language of whoever reads it (Priority: P3)

The same notification is read in Arabic by one person and in English by another, and by the
same person in whichever language the console is in at that moment.

**Why this priority**: the product is bilingual by principle; the bell must not be the one
place that is not.

**Independent Test**: read the same notifications in Arabic, switch the language, read again.

**Acceptance Scenarios**:

1. **Given** a notification, **When** it is read in Arabic and in English, **Then** its title,
   its line and its time are in that language; identifiers and email addresses keep their
   Latin letters in both.
2. **Given** a notification read in Arabic, **When** it shows a number, **Then** the digits are
   Arabic-Indic; in English they are Western.
3. **Given** a notification whose line holds a name a person wrote, **When** it is read in
   either language, **Then** the name is as written.
4. **Given** the time of a notification, **When** it is read, **Then** it is relative when
   recent and a date when not, in Cairo time.

### Edge Cases

- **Many recipients.** An event told to everyone in a clinic of two hundred people reaches all
  of them; raising it does not make the action that raised it wait for two hundred emails.
- **Many of the same kind.** Five replies on five tickets within an hour are five
  notifications and five emails; nothing is rolled into a count.
- **Nobody to tell.** An event whose rule finds no one — no holder of the permission — tells
  no one and fails nothing.
- **The same person by two rules.** The owner of a ticket who also holds «عرض كل تذاكر
  العيادة» is told once.
- **The actor.** A person is told about what they did themselves only where the table names
  them; an event about a record does not tell the person who caused it unless they are its
  recipient by rule.
- **A suspended clinic.** Nobody of a suspended clinic has a session, so nobody reads a bell;
  notifications raised meanwhile are there when the clinic returns. Emails marked "always"
  still go — a change of subscription state is exactly such an event (ن-٠٨).
- **A read-only clinic.** Reading the bell and marking as read still work: they are a person's
  own business, not a change to the clinic's data.
- **Offline.** Notifications already loaded stay readable; marking as read waits for the
  connection like any change.
- **A very long bell.** A person with hundreds of notifications opens the panel as fast as one
  with three; older ones load as the panel is scrolled.
- **Old notifications.** A notification older than ninety days is no longer shown, no longer
  counted as unread, and is removed by the daily clean-up, read or not.
- **A changed or deleted record.** The notification outlives the record it names and keeps
  the facts it was raised with: an invoice whose due date moved still reads the date it had.
- **Disabled in between.** An account disabled after an event was raised and before its email
  left gets no email; its bell notification stays, unread, for nobody to open.
- **Two languages in one email.** An email is in one language, the recipient's.
- **An address that bounces.** A refused address is the mail server's business; the engine
  records that it handed the email over, or that it gave up.
- **The message in the inbox.** A new message in the clinic's inbox is not a bell notification
  in this release (أ-ق٢٧); its count is on the side bar.

## Requirements *(mandatory)*

### Functional Requirements

**The table of events**

- **FR-001**: Events MUST come from one table kept with the product. Each event has: a key; its
  name in Arabic and English; one of six groups — support and tickets; billing and
  subscription; channels and messages; the booking page, remote visits and AI; patients and
  appointments; accounts and licences; a recipient rule; an email kind; the kind of mark it is
  drawn with; and, where it repeats before a date, the days before (Chapter 13 §3-5).
- **FR-002**: The table MUST hold the twenty events of Chapter 13 §9-1 (ن-٠١ … ن-٢٠) with the
  recipients and email kinds written there. An event that reaches a clinic from outside that
  section (§9-2) is added by the spec that raises it.
- **FR-003**: A key, once used, MUST NOT be renamed or reused; the checks MUST fail when the
  table changes a key that exists, and when an event lacks a name in either language.
- **FR-004**: Raising an event that is not in the table MUST be refused.

**Raising an event**

- **FR-005**: There MUST be one way to raise an event: the caller names the event, the record
  it is about, the facts its line needs, and — where the rule is "the owner" or "a named
  person" — who that is. The caller MUST NOT choose the recipients, the channels or the wording
  of the title.
- **FR-006**: An event MUST be raised together with the change that causes it: if the change
  does not take effect, no notification and no email of it exists.
- **FR-007**: Raising an event MUST NOT make the action that raised it fail or wait because an
  email cannot be sent.
- **FR-008**: An event raised with a named occasion — a reminder's day-count, anything a job
  may run twice — MUST tell each recipient once for the same record and the same occasion. A
  reminder that repeats before a date is one occasion for each of its days (ق-٣٧). An event
  raised with no occasion is a happening of its own every time: two replies on one ticket are
  two notifications.
- **FR-008a**: Events are never gathered: each event raised is one notification for each
  recipient and, where it is emailed, one email — whatever number of the same kind came before
  it (clarified).
- **FR-009**: The platform itself MUST be able to raise an event, for work it does on its own
  (a scheduled reminder).

**Who is told**

- **FR-010**: Recipients MUST be found by the event's rule and never by the name of a role: the
  holders of a permission at the moment the event is raised; the owner of the record; a named
  person; or everyone in the clinic. Where the table names both an owner and the holders of a
  permission, both are told (ق-٣٤, أ-ق٢٢, CL-SH-186).
- **FR-011**: A person MUST be told once about one event, whatever number of rules name them.
- **FR-012**: Only a person of the clinic the event belongs to, whose account can sign in, MUST
  be told. A disabled account and an account never activated receive nothing.
- **FR-013**: A notification, once delivered, MUST stay its recipient's when their role changes
  later; it MUST NOT appear for someone who gained the permission afterwards.
- **FR-014**: A notification MUST belong to one clinic and one person; no way of reading may
  return it to another person or another clinic (Constitution II).

**The notification**

- **FR-015**: Every event MUST reach each recipient inside the console, always; nothing a
  clinic can set stops it (ق-٣٠, ق-٣٥).
- **FR-016**: A notification MUST hold: its recipient; its event; the record it is about, by
  identity and by the identifier a person reads; the facts for its line; where it leads; when
  it was raised; and whether its recipient has read it.
- **FR-017**: A notification MUST NOT hold clinical content: not a result, not a diagnosis, not
  a prescription. An event about something clinical says that it happened and for which
  record, never its value (ق-٣٣, SH-070).
- **FR-018**: A notification MUST NOT hold a password, a code, a link that signs in, or a
  session proof.
- **FR-019**: A notification MUST be stored as facts and read as words, in the reader's
  language at the moment of reading: its title from the table, its line from the event's
  template and the stored facts.

**The bell**

- **FR-019a**: The facts of a notification MUST be those of the moment the event was raised:
  a later change to the record, or its removal, does not change what the notification says
  (clarified).
- **FR-019b**: A notification older than ninety days, and the record of its email, MUST be
  removed by the platform once a day. Removing them is not recorded in the audit trail: a
  notification points at something that is recorded where it happened (clarified).
- **FR-020**: The bell's panel MUST show the person's notifications of the last ninety days,
  newest first, each with its mark, title, line and time; unread ones tinted with a dot (the
  frames; CL-SH-027, CL-SH-032).
- **FR-021**: The bell MUST carry a dot while the person has at least one unread notification
  of the last ninety days, and no count (CL-SH-026, أ-ق١٥).
- **FR-022**: The panel MUST be the whole list and scroll; older notifications load as it is
  scrolled; there is no "all notifications" page or link (CL-SH-028, أ-ق١٣).
- **FR-023**: Pressing a notification MUST mark it read, close the panel and open where it
  leads. A notification that leads nowhere is marked read and the person stays (CL-SH-031).
- **FR-024**: «تعليم الكل كمقروء» MUST mark every unread notification of the person read and
  put the dot out, with no passing notice and no dialog (CL-SH-029).
- **FR-025**: Read state MUST be each person's own: one person's reading never marks a
  colleague's notification (أ-ق١٧).
- **FR-026**: A new notification MUST show on the bell of a person who has the console open
  within one minute, without a reload; and what was read on one browser MUST show as read on
  the person's other browsers within one minute.
- **FR-027**: With no notifications the panel MUST show its empty state, and when loading fails
  its error state with «إعادة المحاولة»; neither stops the rest of the console (CL-SH-030,
  CL-SH-187).
- **FR-028**: Reading the bell and marking as read MUST work in a read-only clinic, and MUST
  need no permission beyond being signed in: they concern the person's own notifications only.
- **FR-029**: The time of a notification MUST read as the frames write it — «منذ ساعتين»,
  «اليوم، ٩:٠٠ ص», «أمس», «١٧ سبتمبر» — in Cairo time (أ-ق١٩).

**Email**

- **FR-030**: Each event MUST have one of three email kinds: always; while the clinic's switch
  for it is on; never. Clinical events are "never" (ق-٣٢, ق-٣٣).
- **FR-031**: An email MUST be sent to each recipient of an event whose kind is "always", and
  to each recipient of an event whose kind is "switch" while the clinic's switch is on.
- **FR-032**: No notification reaches the team on WhatsApp, Telegram or SMS (ق-٣١).
- **FR-033**: An email MUST be in its recipient's language — the language they chose in the
  console, else Arabic until a clinic has a language of its own (أ-ق٢٥) — and in one language
  only.
- **FR-034**: An email MUST say what happened in the event's words, name the record by its
  identifier, and lead to the console; it MUST NOT hold clinical content, a password or a code.
- **FR-035**: An email that cannot be sent MUST be tried again — after one, five and thirty
  minutes, then every hour — and given up after a day; the
  outcome is kept, without the address appearing in operational logs (SH-094).
- **FR-035a**: At the moment an email is sent or tried again, its recipient's account MUST
  still be able to sign in; an email to an account disabled or removed since the event is
  dropped, and that is kept as its outcome. A person who only lost the permission the event
  names still gets the email they were owed (clarified).
- **FR-036**: The emails of sign-in and passwords that specs 003 and 004 send (ن-٢٨, ن-٢٩,
  ن-٣٦) MUST stay as they are: they are not notifications and have no bell item.

**The clinic's email switches**

- **FR-037**: Each clinic MUST have one switch for each event whose email kind is "switch" —
  four today: «صدرت فاتورة», «استُلمت الدفعة», «قرب انتهاء الاشتراك», «اقتراب انتهاء رخصة» —
  on by default (Chapter 13 §3-4, CL-SB-097).
- **FR-038**: Changing a switch MUST need «إدارة التنبيهات»; reading the switches MUST need
  «عرض الإعدادات». An event whose email is "always" or "never" has no switch and cannot be
  given one (ق-١٣٧).
- **FR-039**: A switch is for the whole clinic; there is no switch for one person (ق-٣٥).
- **FR-040**: Changing a switch MUST be recorded in the audit trail with the value before and
  after, under the category of changes.
- **FR-041**: The platform MUST offer reading and changing the switches as something a
  signed-in person's browser can ask, with no screen in this spec; the page is spec 084.

**The first event: a sign-in from a new device**

- **FR-042**: When a sign-in completes from a browser the account has not completed a sign-in
  from before, its owner MUST receive the event «الدخول من جهاز جديد» in the bell and by email,
  always, saying when and from what device (ن-١٨, ق-٥٧, CL-SB-149).
- **FR-042a**: A sign-in from a network the account has not used, from a browser it has, MUST
  NOT raise the event (clarified).
- **FR-043**: A sign-in by an account that has no known browser yet MUST NOT raise it — the
  first sign-in an account ever completes, and the first one each existing account completes
  after this spec ships: there is nothing to be new against. A later sign-in from a browser
  already used MUST NOT raise it; a failed or refused sign-in MUST NOT raise it.
- **FR-044**: Nothing a clinic can set stops this event.

**Languages**

- **FR-045**: Every event MUST have its name, and every line its template, in Arabic and
  English; the checks MUST fail for one that lacks either.
- **FR-046**: Digits MUST be Arabic-Indic in Arabic and Western in English; identifiers and
  email addresses keep their Latin letters in both (أ-ق١٨). Wording follows SH-098.

### Key Entities

- **Event**: one row of the table. Key, names, group, recipient rule, email kind, the mark it
  is drawn with, the days before a date it repeats on.
- **Notification**: one event told to one person. Recipient, event, the record it is about, the
  facts of its line, where it leads, when, read or not.
- **Email switch**: a clinic's choice for one switchable event. On or off; who changed it last
  and when.
- **Outgoing email**: one email owed to one recipient for one notification. Waiting, handed
  over, given up, or dropped because the account can no longer sign in.
- **Known device**: a browser an account has completed a sign-in from, so that the next one
  from it is not new.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person with the console open sees the dot for a new notification within one
  minute of the event, without reloading; one who opens the console later sees it at once.
- **SC-002**: Across every recipient rule and two clinics, 100% of the people an event names
  receive it and 0 others do.
- **SC-003**: 0 notifications of one clinic or one person are returned to another, by any way
  of reading or marking.
- **SC-004**: In a test that makes the causing change fail, 0 notifications and 0 emails of it
  exist; in a test that makes the mail server fail, 100% of the causing changes still succeed
  and 100% of the bell notifications are there.
- **SC-005**: 100% of emails for "always" events are sent whatever the switches say; 0 emails
  are sent for "never" events; for "switch" events the number sent with the switch off is 0.
- **SC-006**: In notifications and emails produced while clinical values, passwords and codes
  were within reach, 0 of those values are found anywhere in what was stored or sent.
- **SC-007**: The panel opens in under one second for a person with a thousand notifications,
  and marking all as read takes under one second.
- **SC-008**: Raising an event told to two hundred people adds no more than a tenth of a second
  to the action that raised it.
- **SC-009**: A developer adds a new event — its row, its line, the place it is raised — with
  no change to the engine.
- **SC-010**: 100% of the table's events have a name and a line in Arabic and in English, and a
  notification read in one language contains no text of the other except what a person wrote
  and identifiers.
- **SC-011**: A sign-in from a browser never used by the account produces exactly one bell
  notification and one email for its owner; a repeat from the same browser produces none.
- **SC-012**: The panel with real notifications matches its frames in Arabic and in English.

## Assumptions

- **No new screen.** The bell and its panel are spec 006's; this spec makes the source they
  read real. The page «التنبيهات» with its six cards is spec 084; the support team's notices,
  their dialog and the incident banner are spec 083; tickets 082; the weekly summary 060.
- **Twenty events are in the table, one is raised.** The table holds ن-٠١ … ن-٢٠ now, because
  the settings page lists exactly those. Only «الدخول من جهاز جديد» has a cause that exists
  today; each other event is raised by the spec that builds its cause, which also writes the
  words of its line.
- **Ninety days in the bell.** The BRD gives no limit; a list that grows for ever cannot stay
  "the whole list". Ninety days matches the audit trail's default period. After ninety days a
  notification is removed (clarified): it points at a record kept elsewhere, so nothing is lost.
- **Within a minute.** The BRD does not say how soon a notification appears. One minute,
  without a reload, is taken as what a person expects from a bell; nothing here needs it
  instant.
- **"New device" means a browser the account has not signed in from** (clarified). The BRD's
  ق-٥٧ says "a device or a network it has not used"; a network changes many times a day on a
  phone. **Owner item**: reword ق-٥٧ and CL-SB-149 in the BRD.
- **Language of an email**: the recipient's chosen console language (spec 006), else Arabic.
- **Marks for undrawn kinds**: an event that is none of the four drawn kinds is drawn with the
  mark of a warning when something stopped or ran out, and of a notice otherwise, until the
  design draws them.
- **The person who caused an event** is told only when the rule names them.
- **Retrying email for a day**, then giving up: the BRD gives no figure. A day covers a mail
  server's outage; after it the news is stale and the bell has carried it all along.
- **The operators' console** has its own notifications when it exists; nothing here is for
  operators.
- **Changing a switch is recorded** as a new action of the audit catalogue, added the way spec
  007 lays down.
