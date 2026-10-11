# Validation: Notifications Engine (spec 008)

**Date**: 11 Oct 2026 · **Branch**: `008-notifications-engine`  · **Constitution**: v1.2.0, Principle VII

What was run, what was walked on the real stack, what was measured, what was laid beside the
design, what was built differently from the plan, and what was not done. Second writing, after the
convergence tasks T039–T040 (section 8).

## 1. What was run

| Gate | Result |
|---|---|
| `dotnet test src/server/Scs.sln` (Debug), with nothing else running | unit **499** · architecture **44** · integration **640** — all passed |
| `npm --prefix src/web run ci` (lint, translation check, tools, unit tests, build of the three apps) | passed — clinic **68**, core **144**, ui **72**, patient 1, ops 1; clinic's first download **449.97 kB**, no size warning |
| `npx playwright test` | **378 passed**, 4 skipped (the picture-only tests) |

Before starting (T001) the three suites were run and matched spec 007's record: 473 · 38 · 557;
51 · 128 · 68. New since: 26 unit, 6 architecture and 83 integration tests on the server; 17 unit
tests in clinic, 16 in core and 4 in ui; 15 browser tests.

New test files: `Notifications/NotificationIsolationTests`, `RaisingTests`, `RecipientsTests`,
`BellTests`, `EmailTests`, `EmailSwitchTests`, `NewDeviceTests`, `CleanupAndVolumeTests`
(`CleanupTests` and `NotificationVolumeTests`, one file), `Isolation/NotificationSamples`,
`Fixtures/NotificationFixtures` (integration); `Notifications/NotificationEventsTests` with
`NotificationWordsTests`, `Wording/DeviceTextTests` moved from Audit (unit);
`NotificationRulesTests` (architecture); `platform-notifications.spec.ts` (core),
`ago.spec.ts` (clinic), `overlay-direction.spec.ts` (ui); `e2e/notifications.spec.ts`.

Every figure above is from the last run, after the convergence tasks, with nothing else
running. The browser tests were run before two unit tests of `core` were brought in line with
T039; no source the browser loads changed after that run.

**Existing tests that had to change**, and why:

- `DeclaredCrossingsTests`: seven crossings, not six — the sweep that lists clinics with email
  waiting.
- `RouteDeclarationTests` and `specs/005-permissions-engine/contracts/access-engine.md` §1: the
  own-account list gains the bell's four addresses. `IdentityRulesTests` allowed "own account"
  only inside Identity; it now allows the one file of the bell's routes as well. This widens a
  rule of spec 005 and is worth a look in review.
- `RouteAuditDeclarationTests`: the two read marks record nothing; the switch records.
- `AuditActionsTests`, `SentencesTests`, `AuditFactsTests`: a thirteenth action,
  `settings.notification-email-changed`.
- `RolesRollOutTests`, `SignInEntriesTests`: the scratch databases migrate `notify` before
  Identity, since a sign-in may now raise an event.
- `sources.spec.ts` (core): the bell's default source is the platform's; the class with nothing
  is tested as itself.

**Failures on the way, all fixed**: an email's address was the app's address and the path with
no "/" between them (found by `EmailTests`); a device recorded as nothing read as an empty line
rather than «جهاز غير معروف»; the console fetched the list twice when the bell was opened while
the first question was still on its way (found by the unit tests).

## 2. What was measured

| What | Figure | Limit |
|---|---|---|
| A person with a thousand notifications: the state and the first page | **37 ms** | 1 s (SC-007) |
| the next page | **28 ms** | 1 s |
| marking the thousand read | **44 ms** | 1 s |
| A save that tells two hundred people, median of 15 | **30.2 ms**, against 0.8 ms for one that tells nobody: **29.4 ms added** | under 100 ms (SC-008) |
| On the real stack: the dot in a browser already open, after the same account signed in elsewhere | **29 s**, no reload | within a minute (SC-003) |
| On the real stack: the email in Mailpit after the sign-in began | **3 s** | within a minute |

One unmeasured call comes first in each volume test: the first of a process pays for building
its statements. The save figure includes writing two hundred notifications and two hundred
emails owed, in the caller's transaction.

## 3. What was walked on the real stack

Compose stack `heta-scs`, the API with its worker (restarted on this branch: both migrations
applied), the clinic app, Mailpit; two browsers of the walk's own and a third for reception
(`walk8.mjs`). The seven steps of `quickstart.md`:

1. `owner@shifa.example` signs in: no dot, «لا إشعارات جديدة». The account had no known browser
   before this spec, so its first sign-in told nobody (FR-043).
2. The same account signs in from a second browser that calls itself Safari on macOS.
3. Without a reload the first browser's bell has its dot 29 s later. The panel: «الدخول من جهاز
   جديد», «متصفح Safari على macOS», «الآن», tinted. Mailpit holds one email with that subject, in
   Arabic, right-to-left, with no password and no code.
4. Pressed: the panel closes, the page stays, the dot goes, the item is no longer tinted; the
   second browser shows it read too.
5. The second browser signs out and in again: nothing new in the bell, no second email.
6. In English: "Sign-in from a new device", "Safari on macOS", "1 min ago"; the panel is whole
   inside the window after the switch (section 6).
7. The switches as the owner: 200, four switches, all on — «صدرت فاتورة», «استُلمت الدفعة»,
   «قرب انتهاء الاشتراك», «اقتراب انتهاء رخصة». As `reception@shifa.example`: 403
   `PERMISSION_REQUIRED`; reception's own bell is empty and does not hold the owner's
   notification.

**The walk found one defect the tests could not**: the email's last line read «فتح في Smart
Clinic OS: /». Locally the app's address is not configured and has a fallback, which the email
did not use; the tests configure the address. Fixed (`StaffAppBase()`), and seen again on the
real stack from a fresh browser: "Open in Smart Clinic OS: http://localhost:4201/".

Left behind by the walk: `owner@shifa.example` now reads the console in English (step 6 saved
the choice), and holds two notifications and three known browsers. Nothing of `noor` was
touched.

## 4. Beside the design

`SCS_SHOTS=<folder> npx playwright test notifications` at 1440×900, laid beside frames "Shell /
Notifications Open" `5999:831798` (LTR `5999:834947`), "Notifications Empty" `5999:832473` and
component "Notification Panel" `4262:1056`.

Same as the frames: the panel 360 wide under the bell at its far edge, in both directions; the
title and «تعليم الكل كمقروء» · "Mark all read"; the unread tint and the dot at the far edge of
an unread item; the mark in a circle, title, a two-line text, the time beneath; a hairline
between items; the red dot on the bell.

**Found and fixed — the time's wording.** The panel wrote «قبل ساعتين» and "2 hours ago". The
design writes «منذ ساعتين» · "2 h ago", «اليوم، ٩:٠٠ ص» · "Today, 9:00 AM", «أمس» · "Yesterday"
and «١٧ سبتمبر» · "17 Sep". The panel now writes these (`shell/ago.ts`, days in Cairo time, the
exact time on hover as before). Two things the design does not say and were chosen: a time
reads as "ago" for six hours and as "Today, …" after; a date of another year carries its year.

Differs, and left:

- **Marks.** The frames tint each mark by its kind — blue for an invoice, amber for a
  subscription ending, green for a payment. The panel draws every mark in the brand colour, as
  spec 006 left it. The frames show four kinds; the catalogue has seven marks.
- **A new device has no drawn mark.** It uses the warning triangle.
- **The empty panel** reads «لا إشعارات جديدة» with a bell, and offers no «تعليم الكل كمقروء»,
  as the spec asks (FR-027). The frame shows the library's generic empty state («لا شيء هنا
  بعد») with the link still present. Unchanged from spec 006.
- **No email is drawn.** The email's layout is this spec's own: title, line, one link, a line
  saying replies are not read.

## 5. Built differently from the plan

- **No `INotificationSwitches`** (T008). Whether an email is owed is decided inside the one
  statement that writes the notifications, by reading `notify.EmailSwitch` there. One round
  trip instead of two, and the check cannot be forgotten by a caller.
- **One audit sentence, with before and after**, not two sentences: «غُيِّر بريد «…» من «يعمل»
  إلى «متوقف»». It is how every action that keeps a value before and after reads;
  `data-model.md` §7 now says so.
- **`NotificationRecipients` lives in Identity's `ClinicRoles.cs`**: an architecture rule of
  spec 005 lets only that file ask whether a role holds everything.
- **`DeviceText` moved** from Audit to `Scs.SharedKernel/Wording`, so the trail and the bell
  describe a device with the same words.
- **The clean-up and volume tests share one file**, `CleanupAndVolumeTests.cs`.
- **The console's source** has `setOpen(open)` besides what the plan lists: the list is fetched
  when the bell opens, not before.
- **The made-up platform of the browser tests** keeps the bell's calls apart from `calls`, since
  the bell asks by itself and existing tests compare `calls` exactly.

## 6. A defect reported by the owner during this work, outside the spec

"The notifications panel is sometimes cut off after changing the language without a reload."
The overlay toolkit read the page's direction once, at start; after a switch — or when a
remembered language was applied just after start — panels were still placed for the old
direction (the bell's panel reached x = 1505 of 1440). `provideOverlayDirection()` in `ui` now
follows the page's direction, in the three apps. Four unit tests and three browser tests
(`shell-sources.spec.ts`); two of the three fail without the fix. It touches every panel and
menu, not only the bell.

## 7. Not done, and for the owner

- **Only one event has a cause**: `account.new-device`. The other nineteen are in the catalogue
  with their recipients and email kind and no line; each gets its line and destination from the
  spec that raises it. The engine's rules for them are tested with events made for the tests.
- **FR-028, the read-only clinic**: there is no read-only state until the subscription specs,
  so "the bell still works there" is not tested.
- **FR-032** (nothing on WhatsApp, Telegram or SMS): nothing was built, and nothing sends there.
- **No page for the switches** (the settings spec) and **no «التنبيهات» page** (spec 084).
- **For the owner — the BRD**: ق-٥٧ and CL-SB-149 speak of a new device "or network"; the
  clarified rule is the browser alone. The wording is the owner's to change.
- **For the owner — the design**: marks for the kinds the frames do not draw, the tint of each
  mark, and the emails.
- **For the owner — language**: an email is written in the language its reader chose in the
  console, Arabic when they chose none. A clinic has no language of its own yet.
- **Not checked**: the bell on a phone-width screen with real notifications; a mail server that
  is really down on the real stack (the retry waits are tested with a made-up one); two API
  instances sending at once.

## 8. Convergence (T039–T040)

`/speckit-converge` found two partial gaps; both are closed.

- **T039 — FR-026.** A panel left open showed the dot going out but kept its items tinted when
  they were read on the person's other browser, until it was reopened. An open panel that shows
  anything unread now fetches its list at each turn; a turn that fails leaves the list as it is.
  One unit test and one browser test.
- **T040 — FR-042.** The email of a new device did not say when. Every notification email now
  carries the moment its event was raised, in Cairo time and the reader's language — «١١ أكتوبر
  ٢٠٢٦، ٣:٠٤ م» · "11 October 2026, 3:04 PM" — between the line and the way in. Four unit tests
  and a line in `NewDeviceTests`.

Neither was walked again on the real stack: the walk of section 3 is from before them.
