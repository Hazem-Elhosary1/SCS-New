# Validation: Email Template

**Feature**: 094-email-template | **Date**: 2026-10-12 | **Branch**: `094-email-template` (not committed)

What was run, what was seen, what differs from the design and by whose decision, and what was
not checked.

## 1. What was run

| Gate | Baseline (spec 010's last run; not run again before this work) | After |
|---|---|---|
| Server unit tests | 566 | **823 passed** |
| Server architecture tests | 58 | **63 passed** |
| Server integration tests | 748 | **751 passed** (15 min) |
| Web: clinic / core / ui / ops / patient unit tests | 68 / 164 / 102 / 11 / 1 | **68 / 164 / 102 / 11 / 1 passed**; lint and the three builds clean, no size warning |
| Browser tests | 467 | **691 passed, 4 skipped** (the picture-only tests of specs 005 and 006 when no folder is named) |

Of the browser tests, 228 are this spec's (`e2e/emails.spec.ts`): every email of the catalogue
(34 pages: the eleven emails and the operators' second code in two languages, and five sample
kinds in two) at 680 wide, at 320 and 360, in a dark theme, with all loading refused, without
its style block, with no styles at all, against its text-only form; where the design puts
things; and the shell and the codes in Edge itself.

The first full integration run failed 14 tests that start the platform outside "local": the
platform's names and support email are now required there and the test fixture gave none. The
fixture now does; nothing else changed for them.

## 2. What was walked on the real stack

API on the final build, clinic console 4201, operators console 4203, local mailbox 8025. Each
message was taken from the mailbox as it arrived, opened with every request refused, pictured,
and its text-only part read.

| Step of `quickstart.md` | Seen |
|---|---|
| 1. Clinic code, Arabic then English | `owner@shifa.example` (seeded, second clinic): «رمز التحقق», Arabic only; language set to English, signed in again: "Your verification code", English only; set back to Arabic. Both codes signed in. |
| 2. Clinic reset | `doctor@shifa.example`: «إعادة تعيين كلمة المرور»; the written-out link equals the button's and opened the reset screen. |
| 3. Password changed | «تم تغيير كلمة مرورك», time «12 أكتوبر 2026، 3:20 م» = Cairo time of the walk; no link, no password. The doctor's password was put back to the seed's with a second reset. |
| 4. Invitation | `new@shifa.example` through the local-only address: «دعوة إلى عيادة الشفاء»; the link opened the screen to choose a password (not used). |
| 5. Account created, access reset | **Not walked**: no screen sends them until specs 015 and 026. Held by `TemporaryPasswordTests` and the catalogue. |
| 6. Notification | The new-device notification by email, Arabic («الدخول من جهاز جديد») and English; subject = title; time in Western digits. |
| 7. Operator code; code for a change | A walk operator (`walk94-…@heta.example`): «رمز التحقق» signed in; «رمز تأكيد تغيير كلمة المرور» changed the password from the profile. |
| 8. Operator reset | «إعادة تعيين كلمة المرور»; the link opened the screen (not used). |
| 9. First operator | `operator invite`: «دعوة إلى لوحة التشغيل», 72 hours; the link set the first password. |
| 10. New browser | «تسجيل دخول من متصفح جديد» with the browser, the address and the time. |
| 11. Footer | Local has no phone or address: name, support email, reason, no gap. Then (T054) the running API with the phone and both addresses set: an operator's reset email arrived with "12 Tahrir St., Cairo" and "support@… · +20 100 000 0000" in its footer and in its text-only form; the settings were put back. |
| 12. Sender | Every message from «سمارت كلينك أو إس» (Arabic) or "Smart Clinic OS" (English) at the configured address; no subject has a tag or the platform's name. |
| 13. Nothing fetched | 0 requests from each of the 12 messages. |
| 14. Logs | The API's log of the walk: counts of emails sent only; no subject, no address, no code, no link. |

The seeded operator and the two Noor accounts the owner uses were not signed in to or changed.
An operator cannot choose a language yet; for T054 the walk account's language was set to
English in the local database, its reset email arrived in English only ("Reset your password",
from "Smart Clinic OS", left to right), and the account was set back. The operators' other
English emails are in the catalogue.

## 3. Pictures opened

- All 34 catalogue emails at 680 wide (the eleven real ones pair by pair, Arabic beside English).
- All 34 at 320 and at 360 wide, and all 34 in the dark theme (contact sheets).
- The 12 messages of the walk, from the mailbox.
- `design-vs-real.png`: 16 frames, each beside the email as built.

Found by looking and corrected: the footer's support line ran right to left in Arabic; the
written-out link was underlined; the code box was not the frame's size (now 300 × 58); the
title sat too close to the lines around it; the Arabic short line was too small; in Arabic a
fact's value touched its name; an invoice number broke over two lines inside an Arabic
sentence; the invoice's last row had a rule the frame does not draw; the preview line held
markup.

## 4. Beside the design — what differs, and whose decision

| Difference | Why |
|---|---|
| Body and note greys are darker than the frames | Owner: darken to 4.5 to 1 (clarification 5). |
| The written-out link is whole; the frames end it with "…" | FR-014: it must be copyable. |
| Digits are Western in Arabic emails | Owner (clarification 4); the frames agree. |
| The Arabic short line has no letter spacing | Mine: spacing breaks joined Arabic letters in mail programs. Size raised to 12 instead. |
| The typeface is Cairo only where it is installed (as on this machine); otherwise the system's | FR-019: nothing is loaded. |
| The three notes under a code are one size and colour; the frame draws the first darker | Mine: one kind of note. Small. |
| The code email has a third note the frame lacks ("nobody will ask you for this code") | Kept from spec 010, by the owner's answer at specify. |
| Invitations' bodies name the clinic, not who invited or a role | The frame's facts do not exist until specs 011/015 (`contracts/emails.md`). |
| At 320 wide a long name and value in one row of facts wrap to three lines | Whole and readable; crowded. Left. |

## 5. Built differently from the plan

- `EmailFacts` has `ruled` (default true): without it the total's value alone stands out, as
  the invoice frame draws; with it, the rule and bold row of the subscription frame.
- A title longer than 200 is cut at a word, not refused (a notification's title is not ours).
- `Words.Western` (shared kernel) turns the bell's Arabic-Indic digits into Western ones for
  emails; the bell itself is unchanged.
- The clinic's code email moved from `CodeEmail.cs` into `AccountEmails.SignInCode`.
- Account created / access reset go to a new account, which has kept no language: Arabic.
- The stale-build check of the browser setup compares each part of the emails' code with the
  built file it ends up in.

## 5a. After the first convergence (T050–T054)

- **T050** Both consoles' addresses are held at start to what an email's button accepts:
  `https://`, or `http://` on the machine itself when local. Before, a local `http://` address
  on another machine, or any `http://` operators console address, started and then could not
  send an email with a button. One rule, `ConsoleAddresses.MayBeLinkedTo`, read by both.
- **T051** An email that cannot be composed no longer stops a job: the notification job gives
  it up and sends the rest (it used to fail every run at that email); a password link and an
  operator's invitation treat it as "not sent". Held by a test for the notification job and
  one for password links. The operators' invitation job got its test in T055.
- **T052** A paragraph or a note given nothing is left out of both forms with no gap, as the
  contract says; the email is byte for byte the one that never had it.
- **T053** `Words.Western` turns both ranges of Arabic digits.
- **Gates**: the figures of §1 are of one run of every gate on the code as it stands after T055, with nothing else running (T056): server 823 unit, 63 architecture, 751 integration; web 68 / 164 / 102 / 11 / 1 with lint and the builds clean; browser 691 passed and 4 picture-only tests skipped. An earlier full integration run, before the test event of T051 was given its line, stood at 749 of 750.
- **T055** The operators' invitation job is now held by a test too: of two operators owed an invitation, the one whose email cannot be composed stays owed with no link kept and only its identifier in the log, the other receives theirs in the same run, and the first is sent on the next run once it can be made.
- **T054** Step 11's second half and one operator email in English, in §2.

## 6. Not checked

**Outlook for Windows, Gmail, Apple Mail, any phone's mail app, and a real screen reader were
not checked.** Everything above was seen in Chromium and Edge and in the local mailbox's own
view. The markup is the kind those programs keep (tables, styles on each element, nothing
loaded), but that is not the same as having looked. Before release the owner sends the
catalogue's emails to real mailboxes and looks; anything found is fixed in one place,
`EmailRenderer.cs`.

Also open, for the owner:

- The words marked **(owner)** in `contracts/emails.md` — every subject, and the short lines
  and titles the design does not draw — are built as drafted; read them in the catalogue
  (`npm run emails` from `src/web`).
- The real support phone and address (`SCS_PLATFORM__*`); the frames' are samples.
- Carried from `research.md`: the BRD says the operators console writes Western digits in
  both languages; spec 010 built Arabic-Indic digits in Arabic. Not changed here.
