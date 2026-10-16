# Research: Email Template

**Feature**: 094-email-template | **Date**: 2026-10-12

Read for this: the design page `447:2` of the operators' file (shell, kinds, the emails "as
sent", their text and measures); the BRD's rules of sending (operators' chapter §9-4, §9-5);
the eleven emails as built (`AccountEmails`, `CodeEmail`, `NotificationEmails`,
`OperatorEmails`), the sender and its options; the tests that read an email.

## R1 — How an email is laid out: one renderer, no package

**Decision**: a renderer in the shared project builds the markup from content, in C#. A module
never writes markup: it fills an `EmailContent` (language, subject, short line, title, parts,
reason) and the renderer returns the message with both forms.

**Rationale**: the layout is one card with eight kinds of part. The content is already made in
C# with values that must be encoded; keeping it there means one place encodes, one place knows
the direction, and no email can be written twice (FR-018).

**Alternatives considered**: Razor views compiled at build — a second language and a
dependency for a page of markup; a mail-markup library — foreign, and its output is not ours
to hold to the design; keeping each module's own markup with a shared header and footer — what
exists today, and the reason the looks differ.

## R2 — The markup that mail programs keep

**Decision**: a table layout with every style written on its element; widths as `max-width`
on a fluid table, so the card narrows without a rule that a program might drop; `lang` and
`dir` on the root and on the outer table; `role="presentation"` on every layout table; the
title an `h1`; one `<style>` block that only *adds* (the dark theme, R7) and is not needed for
the message to be right.

The button is a table cell with the purple ground and padding around a link, so that it is a
button where styles are kept and a plain link where they are not.

**Rationale**: these are the conservative means mail programs are known to keep. Layout by
flex or grid, classes, and widths set only in a rule are the things they drop.

**Not done**: the extra markup Outlook for Windows needs for rounded buttons. It cannot be
checked from here (R9); there the button is a square purple cell, which is still a button.

## R3 — Colours, and the darker greys

**Decision**: the design's colours, with two greys replaced (FR-006):

| Use | Design | Used | Contrast on white |
|---|---|---|---|
| Title | `#1a2024` | the same | 16.5 |
| Short line, support line, button ground | `#7c3aed` | the same | 5.7 (white on it: 5.7) |
| Body | `#74838e` | `#5c6975` | 3.9 → 5.6 |
| Notes, link line, footer lines | `#9ca7b0` | `#68757f` | 2.5 → 4.7 |
| Rules | `#d9dce0` | the same | (not text) |
| Ground outside the card | `#f0f2f4` | the same | (no text on it) |
| Code box: ground / digits | `#efeafc` / `#7c3aed` | the same | 4.8 |

The tones of a status mark and of the attention box (neutral, good, warning, bad) are read
from frames `449:2`, `2227:237` and `2593:183` at implementation; any pair under 4.5 is
darkened the same way. `EmailPaletteTests` computes every pair and fails under 4.5.

**Rationale**: the owner chose the standard; the body grey keeps its place below the title and
the note grey below the body, so the frames' order of weight is kept.

## R4 — The platform's identity

**Decision**: a new section of configuration, read by the renderer alone:

`SCS_PLATFORM__NAME_AR` · `SCS_PLATFORM__NAME_EN` · `SCS_PLATFORM__SUPPORT_EMAIL` ·
`SCS_PLATFORM__SUPPORT_PHONE` · `SCS_PLATFORM__ADDRESS_AR` · `SCS_PLATFORM__ADDRESS_EN`

Local defaults: the two names of the frames («سمارت كلينك أو إس», "Smart Clinic OS") and a
local support address; no phone and no address, so the local footer shows what a deployment
with those unset would show. Outside local the validator refuses to start without both names
and the support email. An address set in one language only is used for both.

**Rationale**: FR-023 — one place, so that spec 024 swaps the source without touching an
email. The frames' address and phone are samples and are not shipped as defaults: a made-up
phone number in a real email is worse than none.

## R5 — The recipient's language

**Decision**: the caller passes it. Operators and notifications already know it. For the
clinic's account emails the module reads the staff member's kept language (spec 006); none
kept, or a value the platform does not have → Arabic.

**Rationale**: the language belongs to the account, and the module that sends already holds
the account. The renderer takes a language and trusts it; it has no way to look anyone up.

## R6 — The code

**Decision**: in the laid-out form the six digits are one run of text, spaced by letter
spacing, in a tinted box, left to right, with a label that reads them one by one for a screen
reader. In the text-only form they are the six digits alone on their own line. Neither form
puts spaces between them. The message asks mail programs not to turn digits into phone links.

**Rationale**: a code that is copied must paste as six digits — the frame shows them spaced,
which is how they should *look*, not what they should *be*. The tests of specs 003 and 010
find the code in the text-only form as six digits together, and still will.

## R7 — A dark theme

**Decision**: the message declares that it is designed light and sets every ground and colour
itself, so a program that respects that shows it as designed. For programs that restyle
anyway, the added `<style>` block gives a dark card, light text and the same purple button;
colours are chosen so that a blunt inversion still leaves text readable on its ground.

**Rationale**: no single method works in every program. The browser tests check the declared
dark rule; what a real program's own inversion does is among the things not checked (R9).

## R8 — The catalogue

**Decision**: each module provides its samples — for every email it can send, the content in
Arabic and in English with sample values — through one small interface of the shared project.
A unit-test-project exporter writes them as static pages (`index.html` and one page for each
email, language and form) when given a folder. `npm run emails` exports and opens the index;
the browser tests' setup exports to `src/web/e2e/.emails/` before they run.

An architecture test holds FR-039: every method in a module that returns an `EmailContent`
has a sample of its kind in both languages.

The three kinds not sent yet (ticket notification, invoice, subscription notice) are samples
in the unit-test project, built from the parts with the frames' sample words.

**Rationale**: FR-037 says the catalogue must not exist where the platform runs for real.
Exported files cannot. A local-only route would need its own access declaration and a test
that it is absent in production, and the browser tests would need a running API.

## R9 — What is and is not checked

**Checked here**: the local mailbox's rendering; Chromium and Edge at 320, 360 and 600 wide,
both languages; the dark rule; all loading blocked; the text-only form; structure rules by
unit test.

**Not checked**: Outlook for Windows (its own engine, no flex, limited CSS), Gmail (drops some
styles, clips long messages), Apple Mail and phone mail apps, and their own dark inversions.
There is no way to open those from this machine. The markup of R2 is what they are known to
keep; the owner's look in the real programs before release is the check. `validation.md` will
say this again.

## R10 — Earlier tests that read an email

Tests of specs 003, 004, 008 and 010 that hold what an email *does* stay as they are: who
receives it, on what event, that it holds a code or a link, that the code works, the link's
life, that nothing is sent when nothing should be. They find the code and the link in the
text-only form, which keeps both written out (R6, FR-018).

Tests that hold *wording* change with it, and only in that:

| Where | What changes |
|---|---|
| `UnitTests/Operators/OperatorPiecesTests` | subjects (no platform name, design's words); the body's phrases |
| `UnitTests/Notifications/NotificationEventsTests`, `IntegrationTests/Notifications/EmailTests` | the subject is the title; the "open in" line is the button |
| `IntegrationTests/Identity/SignInFlowTests`, `PasswordResetTests`, `TemporaryPasswordTests` | one language instead of two; subjects |
| `IntegrationTests/Operators/OperatorSignInTests` and fixtures that tell a code's email from a notice by its text | the way they tell them apart, if it read a phrase |

A new assertion replaces each one removed: where a test checked that both languages were
present, it now checks that exactly one is, and which.

## R11 — The sender's name

**Decision**: `EmailMessage` carries the sender's name; the renderer sets it to the platform's
name in the email's language; the sender puts it before the configured address. The address
itself stays `SCS_MAIL__FROM`.

**Rationale**: FR-009a. The name is identity (R4), the address is the mail server's concern.

## R12 — Typeface

**Decision**: a stack that names Cairo first, then faces every system has for Arabic and
Latin. Nothing is fetched. The pictures laid beside the frames are taken on a machine with or
without Cairo and say which.

**Rationale**: FR-019 forbids fetching; most recipients will not have Cairo; sizes and weights
are kept, so the message keeps its shape.

## Carried forward, not decided here

- The words added to the four emails the design does not draw, and all eleven subjects: the
  owner reads them in the catalogue (`contracts/emails.md`).
- The platform's real address and support phone: the owner supplies them.
- A look in Outlook, Gmail and Apple Mail before release: the owner.
- Whether the consoles' screens take the Arabic name of the platform: the owner; not this spec.
- A logo picture in emails (`OP-B-286`): later.
- The operators console writes Arabic-Indic digits in Arabic, against the operators' chapter
  of the BRD (found while clarifying this spec): a correction for spec 011 or its own change.
