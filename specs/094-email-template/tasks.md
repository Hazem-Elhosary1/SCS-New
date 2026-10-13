# Tasks: Email Template

**Input**: Design documents from `/specs/094-email-template/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (email-composition.md, emails.md), quickstart.md

**Tests**: required by the constitution (Principle VII). The renderer's tests are written before the renderer and must fail first. Tests of specs 003, 004, 008 and 010 that hold what an email *does* — its recipient, its event, its code, its link, its life — are not loosened; only what they read of an email's *wording* changes (research R10).

**Organization**: by user story. Server paths are under `src/server/`, browser-test paths under `src/web/`. `Mail/` means `src/server/shared/Scs.BuildingBlocks/Mail/`; `Identity/`, `Notifications/`, `Operators/` mean that module's `Infrastructure/` folder under `src/server/modules/`; test folders are `src/server/tests/Scs.UnitTests/`, `Scs.IntegrationTests/` and `Scs.ArchitectureTests/`. Frames are in the operators' design file `SetO9EhDivNcmurAMfcflY`, page `447:2`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Record the baseline before any change: with the local API stopped, run `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci` and `npx playwright test` (in `src/web`), and note the counts for `specs/094-email-template/validation.md`. If they are not run again, say so and take the figures of spec 010's last run
- [X] T002 Add `PlatformOptions` to `src/server/shared/Scs.BuildingBlocks/Configuration/ScsOptions.cs` as section `SCS_PLATFORM__`: `NAME_AR` (local default `سمارت كلينك أو إس`, "1–80"), `NAME_EN` (local default `Smart Clinic OS`, "1–80"), `SUPPORT_EMAIL` (local default `support@scs.localhost`, "a well-formed address"), `SUPPORT_PHONE` (empty, "up to 40"), `ADDRESS_AR` and `ADDRESS_EN` (empty, "up to 200", each falling back to the other); in `ScsOptionsValidator.cs` refuse outside local when either name or the support email is missing, and any value over its limit anywhere; cover in `tests/Scs.UnitTests/Configuration/ScsOptionsValidatorTests.cs`; add the keys with comments to `deploy/compose/.env.example`, the address and phone as commented samples (research R4)
- [X] T003 [P] Add `.emails/` to the ignore list that covers `src/web/e2e/` and add the script `"emails"` to `src/web/package.json` (export the catalogue, then open `e2e/.emails/index.html`); the export itself is T010

---

## Phase 2: Foundational (blocks every story)

- [X] T004 Write failing `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` for the skeleton of `contracts/email-composition.md` §2 and §3: an Arabic and an English content of short line, title, one paragraph and reason render with `lang` and `dir` on the root (`rtl` / `ltr`), the mark «S» as text, the title as the only `h1`, the footer's name in the email's language, the default reason line when none is given; and the refusals — empty subject, short line, title or reason → refused when built
- [X] T005 [P] Create `Mail/EmailContent.cs` and `Mail/EmailParts.cs` per `data-model.md` §1–§2: `EmailContent` (`To`; `Language` "`ar` or `en`; anything else is treated as `ar`"; `Subject` "1–120 characters … a longer one is cut at a word with '…'"; `Eyebrow` "1–60"; `Title` "1–200"; `Parts`; `Reason`), and the closed set of parts — `Paragraph`, `Code` ("six digits", "one at most"), `Button` (label, address; "one at most"), `Facts` (rows; optional `Total`; optional `Status` with a tone), `Attention`, `Note` — tones `neutral`, `good`, `warning`, `bad`, and a way to mark a value "left to right"
- [X] T006 [P] Create `Mail/EmailPalette.cs` with the colours and measures of research R3 — title `#1a2024`, purple `#7c3aed`, body `#5c6975`, notes and footer lines `#68757f`, rules `#d9dce0`, ground `#f0f2f4`, code box `#efeafc`, card 600, radius 8, the sizes of frames `447:3`/`451:2` (mark 18, short line 11, title 24, body 14, notes 11–12) — and failing `tests/Scs.UnitTests/Mail/EmailPaletteTests.cs` that computes the contrast of every pair of text and ground the renderer uses and fails under 4.5 (FR-006)
- [X] T007 Change `Mail/EmailSender.cs`: `EmailMessage` gains `SenderName`, and `SmtpEmailSender` writes it before the configured `SCS_MAIL__FROM` address; keep `ToString()` printing nothing of the message; cover in `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` that a rendered message's sender name is the platform's Arabic name for `ar` and English name for `en` (FR-009a, research R11)
- [X] T008 Create `Mail/EmailRenderer.cs` (after T005–T007): content + `PlatformOptions` → `EmailMessage` with the laid-out form (table layout, every style on its element, fluid card of at most 600, `role="presentation"` on layout tables, header with the mark, rule, body with short line, title, `Paragraph`, `Button` (drawn; its rules are US1's) and `Note`, rule, footer with name, address, support email as a link that starts a message, phone, reason; a part or line with nothing is not drawn) and the text-only form from the same content in the same order; the line a mail program shows beside the subject is the email's first paragraph, or its title when it has none, hidden from the page and from a screen reader (FR-010); every text encoded; register it in `Mail/EmailSender.cs`'s `AddScsMail`; T004 passes (research R1, R2)
- [X] T009 [P] Create `Mail/EmailCatalogue.cs`: `EmailSample` (`Kind`, `Audience` — `clinic`, `operators`, `sample` —, `Language`, `Content`) and the interface a module implements to provide its samples, per `data-model.md` §5; a shell-only sample (short line, title, paragraph, button, reason — the words of frames `447:3`/`451:2`) in `tests/Scs.UnitTests/Mail/SampleKinds.cs`
- [X] T010 Create `tests/Scs.UnitTests/Mail/EmailCatalogueExport.cs`: when `SCS_EMAILS_OUT` names a folder, write `index.html` (every sample by audience with links to its Arabic, English and text-only pages) and one page for each sample, language and form, exactly as the message would be sent; a no-op otherwise. Create `src/web/e2e/emails-setup.ts` that runs it into `src/web/e2e/.emails/` from the unit tests as they are already built, without building (a running local API holds the modules' files), and fails with one clear line — "build the server first: dotnet build src/server/Scs.sln" — when the built tests are missing or older than any file under `Mail/`, a module's email files or `tests/Scs.UnitTests/Mail/`; wire it as the global setup in `src/web/playwright.config.ts`; make the `emails` script of T003 use it (research R8)
- [X] T011 Create `src/web/e2e/emails.spec.ts` with its helpers: open an exported page; `emailLooksWrong(page)` after `opsLooksWrong` of `e2e/ops-platform.ts` (no sideways scroll, nothing outside the card, no words cut off); save a picture when `SCS_SHOTS` is set; and the first test — the shell sample in Arabic and English at 600 wide

**Checkpoint**: an email of a short line, a title and a paragraph renders in both languages, is exported and is opened by a browser test.

---

## Phase 3: User Story 1 - Every email of the platform looks like one product (P1) 🎯 MVP

**Goal**: the shell of the design, in both directions, with one button and its written-out link.

**Independent test**: open the shell sample from the catalogue beside frames `447:3` and `451:2`.

- [X] T012 [P] [US1] Extend `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` (failing first): a `Button` renders once with its label, and under it «أو انسخ هذا الرابط في متصفحك:» / "Or copy this link into your browser:" followed by the whole address, never shortened; an email with no button has neither the button nor the line; two buttons, a button with no address, or an address that is not `https://` or the local `http://localhost` are refused; the order of parts given is the order rendered, in both forms
- [X] T013 [US1] Implement `Button` in `Mail/EmailRenderer.cs`: a table cell with the purple ground and padding around a link (a button with styles, a link without), the written-out address in the note colour, breaking inside the card; in the text-only form the label, then the address on its own line; T012 passes (FR-014, research R2)
- [X] T014 [US1] In `src/web/e2e/emails.spec.ts` hold where the design puts things for the shell sample, in Arabic and English at 600 wide: the card 600 wide and centred on the ground; the mark above the first rule, centred; the short line, the title, the paragraph and the button centred, in that order; the button 200 by 48; the written-out link under it; the footer under the second rule with its four lines in order; the whole page `rtl` in Arabic and `ltr` in English; no element of one language's words in the other's page
- [X] T015 [US1] Take the shell's pictures (`SCS_SHOTS=<folder> npx playwright test emails`), **open both**, lay them beside frames `447:3` and `451:2`, and correct what differs in `Mail/EmailRenderer.cs` or `Mail/EmailPalette.cs`; record what stays different by decision (the darker greys, the typeface when Cairo is not installed)

**Checkpoint**: the shell matches its frames in both languages.

---

## Phase 4: User Story 2 - The emails already sent move onto the template (P1)

**Goal**: all eleven on the template, with the words of `contracts/emails.md`, in one language each.

**Independent test**: trigger each on the local stack and open it in the mailbox, in both languages.

- [X] T016 [P] [US2] Extend `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` (failing first) for `Code` and `Facts` rows: the code is one run of six digits with no space between them in both forms, in an element that reads left to right in both languages, with a label that reads the digits one at a time; a code that is not six digits is refused; each fact is a name and a value, the value at the far edge, and "name: value" in the text-only form; a facts part with no row is refused
- [X] T017 [US2] Implement `Code` and the rows of `Facts` in `Mail/EmailRenderer.cs`: the tinted box of frames `458:2`/`459:2` with large digits spaced by letter spacing; the bordered box of frame `2227:237`; the message asks mail programs not to turn digits into links; T016 passes (FR-013, FR-015, research R6)
- [X] T018 [P] [US2] Rewrite `tests/Scs.UnitTests/Operators/OperatorPiecesTests.cs`'s email cases for `contracts/emails.md` §C (failing first): `Code` for a sign-in and for a change of password — subject, short line, title, body, the three notes, no button, no link, the code once; `ResetLink` and `FirstPassword` — their words, one button with the link, the life in minutes or hours; `NewBrowser` — the three facts with Cairo time, no button; each in one language only, the subject with no platform name and no tag
- [X] T019 [US2] Rewrite `Operators/OperatorEmails.cs` to return `EmailContent` for the four emails of `contracts/emails.md` §C and render through the renderer at its callers (`OperatorSignIn.cs`, `OperatorPasswords.cs`, `OperatorInvite.cs`, `OperatorSessions.cs`); create `Operators/OperatorEmailSamples.cs` with both languages of `Code` (sign-in), `Code` (change), `ResetLink`, `FirstPassword` and `NewBrowser`; T018 passes
- [X] T020 [P] [US2] Write `tests/Scs.UnitTests/Identity/AccountEmailsTests.cs` for `contracts/emails.md` §A (failing first): `SignInCode`, `PasswordReset`, `PasswordChanged`, `Invitation`, `AccountCreated`, `AccessReset` — each one's subject, short line, title, body, parts, notes and reason in Arabic and in English; one language only; the clinic's Arabic name in an Arabic email and its English name in an English one; the time of `PasswordChanged` in Cairo time; `AccountCreated` and `AccessReset` with a "sign in" button and no password anywhere
- [X] T021 [US2] Rewrite `Identity/AccountEmails.cs` and `Identity/CodeEmail.cs` to return `EmailContent` per `contracts/emails.md` §A; make each caller (`SignInService.cs`, `ForgotPassword.cs`, `PasswordLinks.cs`, `AccountHandover.cs`) pass the recipient's kept language — none kept, or not `ar`/`en` → Arabic (research R5) — and send what the renderer returns; create `Identity/IdentityEmailSamples.cs` with both languages of the six; T020 passes
- [X] T022 [P] [US2] Update the notification email's tests (`tests/Scs.UnitTests/Notifications/NotificationEventsTests.cs` and what it shares) for `contracts/emails.md` §B (failing first): the subject is the notification's title, cut at a word at 120 with "…"; short line «إشعار» / "NOTIFICATION"; the title and the line; the time as a fact when known; the button "open in ‹platform›" with the address; the note that replies are not read; the reason line
- [X] T023 [US2] Change `Notifications/NotificationEmails.cs` to build `EmailContent` per §B and render through the renderer; create `Notifications/NotificationEmailSamples.cs` with both languages; T022 passes
- [X] T024 [US2] Run the integration tests of the four specs (`tests/Scs.IntegrationTests/Identity/`, `Notifications/`, `Operators/` and their fixtures) and change **only** what reads an email's wording, as research R10 lists: where a test checked that both languages were present it now checks that exactly one is, and which; subjects; the "open in" line. Keep every assertion about who received an email, on what event, its code, its link and its life; if one of those fails, the email is wrong, not the test
- [X] T025 [US2] Extend `src/web/e2e/emails.spec.ts`: every sample of the three modules opens in Arabic and English at 600 wide and passes `emailLooksWrong`; for the four drawn emails hold the frame's order — code: short line, title, body, box, notes, no button; reset and the two invitations: short line, title, body, button, link, note
- [X] T026 [US2] Take the pictures of all eleven in both languages, **open every one**, lay the four drawn emails beside frames `2227:91`/`2227:71`, `2227:132`/`2227:111`, `2227:174`/`2227:153` and `2227:216`/`2227:195`, and correct what differs

**Checkpoint**: nothing the platform sends is in its earlier layout; the earlier specs' tests pass.

---

## Phase 5: User Story 3 - An email reads wherever it is opened (P2)

**Goal**: whole without pictures, styles or the design's typeface; at a phone's width; dark; as text; by a screen reader.

**Independent test**: open the catalogue at 320 and 360 wide, in a dark theme, with loading blocked, and as text only.

- [X] T027 [P] [US3] Write `tests/Scs.UnitTests/Mail/EmailCatalogueTests.cs` over every sample of every module and of `SampleKinds.cs` (failing first where the renderer falls short): the laid-out form contains no `<img`, no `url(`, no `@import`, no `<link`, no `src=` and no address other than the button's, its written-out copy and the support email; the text-only form contains every word of the laid-out form in the same order and no `<`, `>` or `&…;`; the root has `lang` and `dir`; there is exactly one `h1`; every layout table has `role="presentation"`; the hidden preview line is the first paragraph's words and is marked hidden from a screen reader
- [X] T028 [US3] Extend `src/web/e2e/emails.spec.ts`: every sample in both languages at 320 and 360 wide passes `emailLooksWrong`; with every request but the page itself refused, the page makes none and looks the same; with the page's `<style>` block removed it still passes `emailLooksWrong` and the button is still a link with its ground; with every `style` attribute removed as well, the words are all there in order and the button's link still works; and one test opens the shell and the code email in Edge itself at 600 wide (skipped where Edge is not installed)
- [X] T029 [US3] Add the dark theme to `Mail/EmailRenderer.cs` per research R7 — the message declares that it is designed light; one `<style>` block adds a dark card, light text and the same button for programs that restyle — and hold in `src/web/e2e/emails.spec.ts`, with the browser's dark scheme on, that every text of every sample is at 4.5 to 1 or better with the ground behind it
- [X] T030 [US3] Add the long-content cases to `tests/Scs.UnitTests/Mail/SampleKinds.cs` and hold them in `src/web/e2e/emails.spec.ts` at 320 wide in both languages: a clinic's name of 120 characters in a title; a link of 400 characters; an email address and an invoice number inside an Arabic sentence keeping their left-to-right order; a notification title of 200 characters — nothing outside the card, the link whole in the page's text
- [X] T031 [US3] Take the pictures at 320 and 360 wide and in the dark theme, **open every one** (contact sheets of eight are enough for the narrow ones), and correct what is cut off, crowded or unreadable

**Checkpoint**: every catalogue email is whole in each of these conditions.

---

## Phase 6: User Story 4 - Later specs compose their emails from the same parts (P2)

**Goal**: the remaining parts, the three sample kinds beside their frames, and the rule that keeps the catalogue complete.

**Independent test**: the three samples, built from parts alone, laid beside frames `449:2`, `2227:237` and `2593:183`.

- [X] T032 [P] [US4] Extend `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` (failing first): `Facts` with a `Total` row draws a rule above it and makes it stand out; `Facts` with a `Status` draws the mark in its tone; `Attention` draws the tinted box with an optional mark, a line and a quieter second line; each tone has its own colours; all appear in the text-only form as words
- [X] T033 [US4] Implement `Total`, `Status` and `Attention` in `Mail/EmailRenderer.cs`, reading the tones' colours from frames `449:2`, `2227:237` and `2593:183` into `Mail/EmailPalette.cs` and darkening any pair under 4.5 (`EmailPaletteTests` covers them); T032 passes (FR-012, FR-013)
- [X] T034 [US4] Add to `tests/Scs.UnitTests/Mail/SampleKinds.cs` the three samples of `contracts/emails.md` §D with the frames' own sample words, in Arabic and English: `TicketNotification` (`449:2`/`455:2`), `Invoice` (`2227:237`/`2227:268`), `SubscriptionNotice` (`2593:71`/`2593:183`) — each from the parts alone, with no markup of its own
- [X] T035 [US4] Write `tests/Scs.ArchitectureTests/EmailRulesTests.cs`: every method in a module that returns an `EmailContent` has a catalogue sample of its kind in Arabic and in English (FR-039); no file outside `Mail/EmailRenderer.cs` contains email markup (`<html`, `<table`, `<td`); no module's code writes "Smart Clinic OS" or «سمارت كلينك أو إس» as fixed words in an email (FR-025); and nothing in the API, the worker or the deployment tool refers to the catalogue's types beyond providing samples (FR-037)
- [X] T036 [US4] Extend `src/web/e2e/emails.spec.ts` for the three samples at 600 wide in both languages: the invoice's three rows with values at the far edge and the last standing out; the subscription notice's rows, rule, total and status mark; the ticket notification's tinted box with its mark and two lines; then take their pictures, **open every one**, lay each beside its frame, and correct what differs
- [X] T037 [P] [US4] Write `docs/adding-an-email.md`: how a later spec adds an email — fill an `EmailContent` from the parts, pass the recipient's language, render and send, add both languages to the module's samples, add its words to that spec's contract; what the template does for it (direction, footer, text-only form) and what it refuses

**Checkpoint**: a new email of a drawn kind needs words and a choice of parts, nothing else.

---

## Phase 7: User Story 5 - The footer says who is writing, from one place (P3)

**Goal**: name, address and support contacts from the configuration; what is not set is not shown.

**Independent test**: set all, send; unset phone and address, send again.

- [X] T038 [P] [US5] Extend `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` (failing first): with all set, the footer shows the name of the email's language, the address of that language, and the support email and phone on one line with a dot between; with the phone unset, the email alone and no dot; with the address unset, no address line and no gap; with only `ADDRESS_EN` set, an Arabic email shows it left to right; the support email is a link that starts a message; changing a name or the support email changes every sample and nothing else (SC-008)
- [X] T039 [US5] Complete the footer in `Mail/EmailRenderer.cs` for those cases, in both forms; T038 passes (FR-004, FR-023, FR-024)
- [X] T040 [US5] In `docs/setup.md` add "The platform's identity": the six keys, which are required outside local, that the frames' address and phone are samples and the real ones are the owner's to supply, and that spec 024 will make them settings

**Checkpoint**: the footer is whole with everything set and tidy with only the required set.

---

## Phase 8: User Story 6 - An email carries nothing it should not (P3)

**Goal**: what a person wrote cannot change an email; only the platform's links; nothing in a log.

**Independent test**: run the suites, then read every email produced and every log line.

- [X] T041 [P] [US6] Extend `tests/Scs.UnitTests/Mail/EmailRendererTests.cs` (failing first): a title, a paragraph, a fact's value and a note that contain `<b>`, `<a href>`, `"` and `&` appear in the laid-out form as those characters and add no element and no link; in the text-only form they appear as written
- [X] T042 [US6] Extend `tests/Scs.UnitTests/Mail/EmailCatalogueTests.cs`: every sample has at most one button and a code's email has none; every link is under the clinic console's, the operators console's or the patients' address from configuration, or is the support email (FR-034); every subject is 1–120 characters, has no "[" and contains neither name of the platform (FR-009); no sample of an always-sent email contains "unsubscribe", «إلغاء الاشتراك» or "stop receiving" (FR-035); no sample in either language contains an Arabic-Indic digit (FR-011)
- [X] T043 [US6] Extend the log tests of specs 003, 008 and 010 (the notifications' email test among them; `tests/Scs.IntegrationTests/Identity/` and `Operators/OperatorPasswordAndSessionTests.cs`, "no password, code, link or address reaches the logs") to hold that after their emails are sent on the template the captured logs contain no subject, no title, no body phrase and no sender's name of an email either (FR-036)

**Checkpoint**: the care the eleven emails had is held by tests of the template.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T044 [P] Update `docs/setup.md` ("Looking at the emails": `npm --prefix src/web run emails`) and `ops/runbooks/operators.md` where it describes the operator's emails by their old subjects
- [X] T045 Run every gate with nothing else running: `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test`; fix what fails; no size warning
- [X] T046 Walk the real stack as `quickstart.md` "By hand": the fourteen steps, each of the eleven emails opened in the local mailbox in its laid-out and its text-only view, in Arabic and English; the footer with and without phone and address; the sender's name and subject in the mailbox's list; nothing fetched; nothing of an email in the API's log. Use accounts made for the walk; do not reset the owner's seeded accounts
- [X] T047 Look at everything once more as a whole: export the catalogue, open **every** page — each email in both languages laid out and as text only, at 600 and at 320, light and dark — and the same emails in the local mailbox from T046; fix what is cut off, misaligned or different from its frame, and re-take the pictures after any fix
- [X] T048 Make `specs/094-email-template/design-vs-real.png`: every drawn frame used here (the shell, the code, the reset, the two invitations, and the three sample kinds; Arabic and English) beside the email as built, and open it
- [X] T049 Write `specs/094-email-template/validation.md`: what was run with its counts against the baseline of T001; what was walked and seen; which pictures were opened; beside the design, with every difference and whose decision it is; what was built differently from the plan; and what was **not** checked — Outlook for Windows, Gmail, Apple Mail, phone mail apps, a real screen reader — in so many words, with the owner's check before release

---

## Dependencies & Execution Order

- **Setup (T001–T003)** then **Foundational (T004–T011)**: block everything. T004 before T008; T005, T006, T007 before T008; T009 before T010; T010 before T011.
- **US1 (T012–T015)**: needs the foundation. **US2 (T016–T026)**: needs US1's button; T017 before T019, T021 and T023; T019, T021 and T023 before T024; T024 before T025.
- **US3 (T027–T031)**: after US2, so that it checks real emails; T029 before T031.
- **US4 (T032–T037)**: T032–T034 need only the foundation and may run beside US2; T035 after US2 (it reads the modules); T036 after T034.
- **US5 (T038–T040)**: after the foundation; independent of US2–US4.
- **US6 (T041–T043)**: T041 after the foundation; T042 and T043 after US2.
- **Polish (T044–T049)**: last; T045 → T046 → T047 → T048 → T049 in order.

### Parallel opportunities

- T003, T005, T006 and T009 once T002 is done.
- The three modules' email tests — T018, T020, T022 — together, then their rewrites T019, T021, T023 (different folders).
- T032, T037, T038 and T041 beside the US2 work: they touch the renderer's tests and docs, not the modules.
- T027 beside T030.

## Implementation Strategy

1. **MVP**: Setup, Foundational and US1 — the shell in both languages, exported and laid beside its frames. Stop and look.
2. Then US2 — the eleven moved, the earlier specs' tests passing. This is the point at which the platform's real emails change; walk a few on the real stack before going on.
3. Then US3 and US4 (reading everywhere; the remaining parts and the catalogue rule), US5 and US6 (the footer's cases; care), and the polish phase with the gates, the walk, every picture opened and `validation.md`.

## Notes

- Requirements → tasks: FR-001–FR-003 T004, T008, T014 · FR-004 T008, T038, T039 · FR-005 T006, T014, T015 · FR-006 T006, T029, T033 · FR-007 T004, T021 · FR-008 T030 · FR-009 T018, T020, T022, T042 · FR-009a T007 · FR-010 T008, T027 · FR-011 T018, T020 · FR-012 T013, T017, T033 · FR-013 T017, T032, T033 · FR-014 T012, T013 · FR-015 T016, T017 · FR-016 T008, T038 · FR-017 T004, T012, T016 · FR-018 T008, T027 · FR-019 T027, T028 · FR-020 T028, T030 · FR-021 T028, T029 · FR-022 T016, T027 · FR-023–FR-025 T002, T035, T038, T039 · FR-026–FR-030 T018–T023 · FR-031 T024 · FR-032 T020, T021, T024 · FR-033 T041 · FR-034, FR-035 T042 · FR-036 T043 · FR-037 T009, T010 · FR-038 T034, T036 · FR-039 T035.
- Success criteria: SC-001 T024, T026 · SC-002 T015, T026, T036, T048 · SC-003 T028, T030 · SC-004 T027, T028 · SC-005 T006, T029 · SC-006 T028 (the code is the largest text and inside the first 360 by 640) · SC-007 T024 · SC-008 T038 · SC-009 T042 · SC-010 T043 · SC-011 T034, T036.
- Not done by any task, and said so in T049: a check in Outlook for Windows, Gmail, Apple Mail or a phone's mail app; a real screen reader.
- The words marked **(owner)** in `contracts/emails.md` are built as drafted; the owner reads them in the catalogue.
- The implementation is not complete until T045 to T049 are done and written down (constitution VII), and every picture has been opened.

---

## Phase 10: Convergence

- [X] T050 Make a configuration that starts always able to send its emails: `ScsOptionsValidator.cs` accepts an `http://` clinic console address on any host when local, and checks nothing of `SCS_OPS__APP_URL` but that it is set, while `EmailButton` in `Mail/EmailParts.cs` refuses every address that is not `https://` or `http://` on the machine itself — so a developer's `http://192.168.x.x:4201`, or an operators console address given as `http://…` outside local, starts and then throws `EmailCompositionException` when a reset, an invitation or a notification is sent. Decide the one rule (the validator refuses at start what a button would refuse, for both consoles' addresses, with the message naming the variable), cover it in `ScsOptionsValidatorTests.cs` and with a renderer test that every address the validator accepts makes a button, and say it in `docs/setup.md` per FR-031, FR-034 (contradicts)
- [X] T051 Keep one email that cannot be composed from holding up the others: in `Notifications/NotificationEmails.cs` (`NotificationEmailJob`) the content is built outside the `try`, so an `EmailCompositionException` fails the job before the email is settled and the same email fails every run, with every email after it in the batch never sent; settle it as given up, count it in the job's log line (a count only — no title, no address), and go on; the same for `Operators/OperatorInvite.cs` (`OperatorInviteJob`) and `Identity/ForgotPassword.cs` (`AccountEmailJob`), each of which handles `EmailNotSentException` only; cover each with a test that a refused email leaves the next one sent per FR-031, FR-036 (partial)
- [X] T052 Do not draw an optional part that has nothing in it: `EmailNote` and `EmailParagraph` in `Mail/EmailParts.cs` throw on empty text, where `contracts/email-composition.md` §3 says "a part with nothing in it that is optional (a note with empty text) is simply not drawn" and FR-016 says it leaves no gap — today each module must guard by hand, as `NotificationEmails.Notification` does for its line; let an empty note or paragraph be accepted and skipped by `Mail/EmailRenderer.cs` in both forms (required things — short line, title, a button's address, a code, a fact's name and value — stay refused), cover it in `EmailRendererTests.cs` with the spacing unchanged around the missing part, and remove the guard from the notification per FR-016, FR-017 (partial)
- [X] T053 Make `Words.Western` in `src/server/shared/Scs.SharedKernel/Wording/Words.cs` turn the Eastern Arabic-Indic digits ۰–۹ (U+06F0–U+06F9) into Western ones as it does ٠–٩: the catalogue and browser tests refuse both ranges in an email, but only one is converted, so a notification line holding the other would be sent as written; cover both ranges in a unit test per FR-011 (partial)
- [X] T054 Finish what T046 left of the real-stack walk and record it in `validation.md` §2: step 11's second half — the running API with `SCS_PLATFORM__SUPPORT_PHONE` and both addresses set, one email sent and opened from the mailbox with the phone and the address in its footer, then the settings put back — and, where an account's language can be set without a screen that does not exist yet, one operator email in English from the mailbox; keep "account created" and "access reset" stated as not walked until specs 015 and 026 give them a screen per T046, US5/AC1 (partial)

---

## Phase 11: Convergence

- [X] T055 Cover the operators' invitation job with the test T051 asked of each job and left out for it: `Operators/OperatorInvite.cs` (`OperatorInviteJob`) now treats an email that cannot be composed like one that was not sent, but nothing holds it — `validation.md` §5a says so. Let the test mail sender in `tests/Scs.IntegrationTests/Fixtures/SignInFixtures.cs` refuse one address as uncomposable (it throws `EmailCompositionException`, which the job catches in the same place), and in `tests/Scs.IntegrationTests/Operators/` hold that with two operators owed an invitation, the first refused that way stays owed with no link kept and nothing of it in the log but its account's identifier, and the second receives theirs in the same run per T051, FR-031 (partial)
- [X] T056 Run every gate once on the code as it now stands and replace the partial figures of `validation.md` §1 and §5a with them: after T050–T054 the full integration suite ran once at 749 of 750 and only the notifications tests and one other were run again after the fix, and the web's unit tests, lint, builds and the whole browser suite were not run at all; with nothing else running — `dotnet test src/server/Scs.sln`, `npm --prefix src/web run ci`, `npx playwright test` — fix what fails and write the counts per T045, Constitution VII (partial)
