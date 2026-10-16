# Implementation Plan: Email Template

**Branch**: `094-email-template` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/094-email-template/spec.md`

## Summary

The platform's emails get one look and one way of being written. A small composing layer in
the shared project turns *what an email says* — a language, a subject, a short line, a title,
parts and a reason — into the two forms a mail program needs: the laid-out message of the
design's "Email Template" page and a text-only message made from the same content. The
platform's name, support contacts and address come from one place in the configuration and
are written by the template, never by an email.

The eleven emails sent today (clinic sign-in code, reset, password changed, invitation,
account created, access reset; the notification email; operator code, reset, first password,
new browser) are rewritten as content for that layer: the four the design draws take its
words, the others keep theirs, and the clinic's stop being written in two languages. Nothing
about when, to whom, or with what code or link they are sent changes.

Every email the platform can send is listed in a catalogue with sample values. It is exported
as static pages for a person to look at and for the browser tests to open at three widths in
both languages; it is not served by the platform. A rule in the architecture tests keeps an
email from being added without its entry.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript for the browser tests (as pinned by spec 001).

**Primary Dependencies**: none new as packages. The shared mail sender (MailKit, already
there), the configuration and its validator. No templating package: the layout is one small
renderer in C#, as the emails are today (research R1).

**Storage**: none. No table, no migration. One new section of configuration, `SCS_PLATFORM__*`.

**Testing**: xUnit unit tests of the renderer and of each email's content (structure, the
text-only form, what is encoded, links, one button, contrast of every colour pair); the
earlier specs' integration tests, changed only where they read wording; an architecture test
for the catalogue rule and for "no email writes the platform's name itself"; Playwright over
the exported catalogue at 320, 360 and 600 wide in both languages, in a dark theme and with
all loading blocked, with pictures; a real-stack walk per `quickstart.md` that opens each of
the eleven in the local mailbox.

**Target Platform**: the platform API and its worker (they send); mail programs (they read).
The catalogue is a development artefact on a developer's machine.

**Project Type**: a shared server library with its users in three modules, configuration, and
a development catalogue with browser tests. No screen of a console changes.

**Performance Goals**: composing an email costs no measurable time beside sending it (string
building, no input or output); the code's email still arrives within the thirty seconds of
specs 003 and 010.

**Constraints**: nothing loaded from the internet (FR-019); at most one button and only the
platform's links (FR-014, FR-034); every word at 4.5 to 1 or better (FR-006); one language and
Western digits (FR-007, FR-011); the same events, recipients, codes and links as before
(FR-031); nothing of an email in a log (FR-036).

**Scale/Scope**: 40 functional requirements, 6 user stories; 11 emails moved, 3 sample kinds;
12 emails in two languages (the operator's code has two forms) plus 3 samples in two — 30 pages laid out and 30 as text — in the catalogue; one renderer, eight parts.

**Open items carried from research**: the words added to the four emails the design does not
draw, and the subjects of all eleven (owner, shown in the catalogue); the platform's real
address and phone (owner, configuration); how the emails look in Outlook for Windows, Gmail
and Apple Mail (owner's check before release — research R9).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, differences recorded | The layout, parts and the words of the four drawn emails are the frames of page `447:2`. Four differences are the owner's decisions and are in the spec: darker greys for contrast, the written-out link whole, plain subjects, real lifetimes in place of sample values |
| II. Tenant isolation in the database (non-negotiable) | Pass | No table is added or read differently. An email to a clinic's team is built from that clinic's own data by the module that already holds it |
| III. The server decides; deny by default | Pass | No address is added to the platform. The catalogue is exported files, not a route |
| IV. Financial records are immutable; changes are audited | Pass | No money and no record changes. Emails are not records |
| V. Secrets and patient data | Pass | Codes and links are placed in an email exactly as before and are never logged; `EmailMessage` still prints nothing of itself. No patient data is in any of the eleven. The catalogue holds sample values only |
| VI. Bilingual, complete screens | Pass | Every email in Arabic and English with its direction; one language each. Readable without styles, pictures or the design's typeface, at a phone's width, in a dark theme, as text only and by a screen reader |
| VII. Tested before merge, verified after implementation | Pass, one limit stated | Unit, integration, architecture and browser tests; every catalogue picture opened; the eleven walked on the real stack. Not verified in Outlook, Gmail or Apple Mail: stated in the spec and in `quickstart.md` |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged. Two things stated plainly. The earlier
specs' tests that read an email's wording must change with the wording; each such change is
listed in `research.md` R10 and none may loosen what a test holds about an email's code, link,
recipient or timing. And the three sample kinds are samples: their words are the frames'
sample words and are not sent by anything.

## Project Structure

### Documentation (this feature)

```text
specs/094-email-template/
├── plan.md              # This file
├── research.md          # Phase 0: decisions R1–R12
├── data-model.md        # Phase 1: the composing types and the configuration (no tables)
├── quickstart.md        # Phase 1: how to run, look and walk
├── contracts/
│   ├── email-composition.md   # what a module gives the template, and what it gets back
│   └── emails.md              # the eleven emails and three samples: every word, both languages
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/server/
├── shared/Scs.BuildingBlocks/
│   ├── Mail/
│   │   ├── EmailSender.cs            # CHANGED: EmailMessage gains the sender's name; the sender uses it
│   │   ├── EmailContent.cs           # NEW: what an email says — language, subject, short line, title, parts, reason
│   │   ├── EmailParts.cs             # NEW: paragraph, code, button, facts (rows, total, status), attention, note
│   │   ├── EmailRenderer.cs          # NEW: content + identity → laid-out and text-only forms
│   │   ├── EmailPalette.cs           # NEW: the design's colours and measures, with the darker greys
│   │   └── EmailCatalogue.cs         # NEW: a sample (kind, language, content) and the source a module provides
│   └── Configuration/
│       ├── ScsOptions.cs             # CHANGED: PlatformOptions (names, support email and phone, address)
│       └── ScsOptionsValidator.cs    # CHANGED: name and support email required outside local
├── modules/
│   ├── Identity/Scs.Identity/Infrastructure/
│   │   ├── AccountEmails.cs          # REWRITTEN: five emails as content, one language
│   │   ├── CodeEmail.cs              # REWRITTEN: the sign-in code as content
│   │   ├── IdentityEmailSamples.cs   # NEW: the six emails in the catalogue
│   │   └── (callers)                 # CHANGED: pass the recipient's language; send what the renderer returns
│   ├── Notifications/Scs.Notifications/Infrastructure/
│   │   ├── NotificationEmails.cs     # CHANGED: the notification email as content; the subject cut at a word
│   │   └── NotificationEmailSamples.cs   # NEW
│   └── Operators/Scs.Operators/Infrastructure/
│       ├── OperatorEmails.cs         # REWRITTEN: four emails as content
│       └── OperatorEmailSamples.cs   # NEW
└── tests/
    ├── Scs.UnitTests/Mail/
    │   ├── EmailRendererTests.cs         # NEW: structure, direction, parts, text-only form, encoding, refusals
    │   ├── EmailPaletteTests.cs          # NEW: every colour pair at 4.5 to 1 or better
    │   ├── EmailCatalogueTests.cs        # NEW: every sample renders; one button; only allowed links; nothing remote
    │   ├── EmailCatalogueExport.cs       # NEW: writes the catalogue as pages when asked to (SCS_EMAILS_OUT)
    │   └── SampleKinds.cs                # NEW: the ticket notification, invoice and subscription notice samples
    ├── Scs.UnitTests/{Identity,Notifications,Operators}/   # CHANGED: the emails' own tests
    ├── Scs.ArchitectureTests/EmailRulesTests.cs            # NEW: catalogue rule; no fixed platform name; no HTML outside the renderer
    └── Scs.IntegrationTests/                               # CHANGED only where a test reads wording (research R10)

src/web/
├── e2e/
│   ├── emails.spec.ts            # NEW: every catalogue email, both languages, three widths, dark, blocked; pictures
│   ├── emails-setup.ts           # NEW: exports the catalogue before the browser tests run
│   └── .emails/                  # exported pages (ignored by git)
├── playwright.config.ts          # CHANGED: the export as global setup
└── package.json                  # CHANGED: `npm run emails` exports and opens the catalogue's index

deploy/compose/.env.example       # CHANGED: SCS_PLATFORM__*
docs/
├── setup.md                      # CHANGED: the platform's identity; looking at the catalogue
└── adding-an-email.md            # NEW: how a later spec adds an email
```

**Structure Decision**: the composing layer lives in the shared project beside the sender it
feeds, because three modules use it today and every later module will. Each module keeps its
own emails — what they say is that module's knowledge — and gives the catalogue its samples.
No module gains a dependency on another. Nothing is added to the API's addresses or to the
consoles.

## Complexity Tracking

No violation of the constitution to justify.

| Choice | Why | Simpler alternative rejected because |
|---|---|---|
| A renderer in C# with no templating package | Eight parts and one layout; the content is already built in C#; a package would bring a second language for emails and a build step | Razor or a mail-template library: new dependency and runtime compilation for a page of markup |
| The catalogue as exported pages, not a route | "Must not exist where the platform runs for real" holds by construction, and the browser tests can open files without a running API | A local-only route: needs its own access declaration, its own test that it is absent in production, and a running API for the browser tests |
| The export run by the browser tests' setup | The pictures and width checks are always of the code as it is; nobody looks at a stale export | A separate command to remember: the gate would pass on old pages |
