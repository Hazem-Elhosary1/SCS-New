# Validation: Operators and their roles (spec 011)

**Date**: 2026-10-13 · **Branch**: `011-operators-roles` (nothing committed)

## Baseline (T001)

Spec 094's last full run, taken as written in its validation and **not run again** before this
work: server 823 unit, 63 architecture, 751 integration; web unit 68 / 164 / 102 / 11 / 1;
browser 691 passed, 4 skipped.

## What was run

| Gate | Result | Against the baseline |
|---|---|---|
| Server unit tests | 853 passed | +30 |
| Architecture tests | 65 passed | +2 |
| Integration tests | 827 passed (second full run, alone; see below) | +76 |
| Web: lint, i18n check, tools tests | pass | — |
| Web unit tests (clinic 68, patient 1, ops 27, core 164, ui 107) | 367 passed | ops +16, ui +5 |
| Builds of the three apps | pass; no size warning in `ops` | — |
| Browser tests, all three apps (Chromium) | 755 passed | +64 |
| This spec's browser tests in Edge (`ops-operators`, `ops-roles`) | 46 passed | new |
| The console's browser tests after the new drop-down (`ops-*`, five files), Chromium and Edge | 118 passed in each | — |

### The integration suite

The first full run of the solution (while the browser suite was running beside it) ended with
824 of 827 passing. The three failures:

- `AuditFactsTests.A_fact_is_a_short_text…` — it pinned the fact-length limit at 200; this spec
  raises it to 500 (a reason or a note is "up to 500"). **A change to spec 007's rule**; the
  test now pins 500.
- `AuditFactsTests.Nothing_secret_that_passed_through_sign_in…` — it expected every action of
  the catalogue in a clinic's trail; the ten operator actions are written to the platform's own
  trail. The test now leaves them to `PlatformEntryTests` and `NobodyGainsTests`.
- `AuditVolumeTests.A_million_entries…` — a timing test; it passed when run again without the
  browser suite beside it.

After the fixes, a second full run of the integration suite — alone, with the local API still
running beside it — passed whole: **827 of 827**, in 31 minutes.

## What was walked on the real stack (T060)

`src/web/node_modules/.tmp/walk11.mjs`, against the real API, database and mailbox, with the
walk's own accounts (`walk11a-…`, `walk11b-…`, `walk11c-…@heta.example`; passwords made in the
script and never printed). The seeded operator was not touched. Seen:

1. The command's invitation names no inviter; the admin's side bar is «الرئيسية», «المشغلون»;
   the top bar says «مسؤول عام».
2. The list's cards, rows and count in Western digits; search by part of an email in capitals
   and by part of a name; the filter offered only «نشط» (nobody was invited then) and the roles held.
3. Inviting a viewer: the new operator's page, «تم إرسال الدعوة.», added by the admin, no link
   or token on the screen or in its address; the email names the admin and «مشاهد فقط» and says
   72 hours; the viewer signs in, has no add button and a row menu of one item.
4. Inviting with the switch off: «تم إنشاء الحساب.», no email after 70 seconds; «إرسال الرابط»
   sends the invitation in the account's language (English, naming the inviter and "Viewer
   Only"); pressed again within the minute: the "less than a minute" sentence and no second email.
5. As a custom role that manages operators: «مسؤول عام» disabled with «يمنحه مسؤول عام فقط»,
   the two other system roles with «به صلاحيات ليست لديك»; own edit page locked with its sentence.
6. A changed role: the other browser, untouched, was on the sign-in screen after 8 seconds with
   «تم تغيير دورك…».
7. A permission added to the role: its holder stayed signed in and the next page showed
   «+ إضافة دور».
8. Suspending: the other browser out after 20 seconds with «تم قفل حسابك…»; the right password
   answered «هذا الحساب موقوف.»; reactivated.
9. No suspend button on one's own page; own role locked.
10. Five wrong passwords: the account's page showed the lock and «فك قفل الحساب»; after it the
    right password signed in.
11. A role made, edited, refused deletion while held (count named, button disabled), deleted
    once free; copying «مشاهد فقط» as someone who holds less: «تُركت 8 صلاحيات لا تحملها.»
12. System roles: no menu on their rows.
13. The admin's own page: 28 permissions, one active session, and nine lines of activity as
    sentences with their times.
14. As the viewer: `POST /v1/ops/operators` by hand → 403 `PERMISSION_REQUIRED`;
    `/operators/new` by its address → «لا يمكنك فتح هذه الصفحة».
15. The code step's countdown `01:00` and the profile: no Arabic-Indic digit.
16. The API's log of the walk: route templates and identifiers; nothing matching the walk's
    emails, names or `token=`; no error lines.

**Not walked on the real stack**: "the last active «مسؤول عام»" (step 9's refusals). The
local database holds other active super admins — the seeded operator and earlier walks' — and
none of them may be touched. It is held by `LastSuperAdminTests` (two simultaneous requests)
and by the browser tests. The walk left its second account suspended, its third invited, and
its two admin accounts (one from a first attempt that stopped on the script's own mistake)
active.

## Pictures

With `SCS_SHOTS`, every state of this spec was pictured in Arabic and English at 1440 and at
390. Opened, as contact sheets (`src/web/node_modules/.tmp/sheets11/`):

- **Arabic, 1440 — all 36 states** (sheets `ar-1` … `ar-9`), and seven of them at full size
  (list, filter, operator, add with errors, suspend refused, roles list, add role with needed boxes).
- **English, 1440 — 12 of 36** (sheets `en-1`, `en-3`, `en-5`: the list and its states, the
  operator's page and its states, the add and edit forms). **The other 24 English pictures were
  taken and not opened**; they passed the same automatic checks (nothing outside its frame,
  nothing cut off, no sideways scroll).
- **Arabic, 390 — 16 of 24** (sheets `nar-1`, `nar-2`) and the row menu at full size.
  **English at 390 was not opened.**
- **The walk's 16 pictures** of the real stack (sheets `walk-1` … `walk-3`).
- Spec 010's screens were re-pictured by the same run and **not opened again**; their tests
  pass with Western digits.

What the pictures showed and was fixed: the ops console had never imported the shared table
and overlay styles (unstyled table and panel); a filter panel taller than the window (now two
drop-downs); a refusal banner drawn as a tall block inside a dialog; the phone number's
placeholder written right to left; «منذ ساعة واحدة» where the frame says «منذ ساعة»; a table
too wide for a phone (rows now stack, each cell with its column's name); pager buttons, row
menus, name links and switches under 40 by 40 at 390.

### Once more as a whole (T061, 2026-10-16)

After the drop-down was rebuilt, every screen and state of the console — this spec's and spec
010's — was pictured again with `SCS_SHOTS`: **158 at 1440 and 158 at 390**, Arabic beside
English (`src/web/node_modules/.tmp/sheets11b/`, sheets `w-1` … `w-40` and `n-1` … `n-20`).
**All 316 were opened**, and the new drop-down, open and closed, the filter panel and the
add-role form at full size. Found and fixed: the filter panel's two drop-downs were 32 apart
where the panel spaces its fields 16 (the operators list added its own margin); re-pictured and
opened. Seen and not a fault: the shade of a dialog ends at the window's height in a full-page
picture; the drop-down's arrow is caught half-turned in the narrow picture taken just after
reopening it (a 0.15 s turn, none with reduced motion).

**Edge**: the same five files ran in Microsoft Edge (118 passed) and pictured the same 316
states. Each was measured against its Chromium twin: none changed size, the most different
differs in 0.3% of its pixels; the most different pairs were opened side by side and differ in
nothing visible (smoothing and the text cursor). Edge's own "reveal" eye in a password box was
checked during spec 010.

**Left for the owner**: at 390 the filter panel opens only downward, so from the Filter button
near the bottom of the window it starts at the window's edge and is seen by scrolling. The panel
is the shared list toolbar's (the clinic uses it too).

## Beside the design

`design-vs-real.png` lays nine frames beside the screens as built (Arabic, 1440), and was
opened. The design file refused every read during implementation ("you don't have edit
access"); when it answered again (2026-10-16) the remaining frames were read — see "T002" below.

Differences, and whose decision each is:

| Difference | Decision |
|---|---|
| No «تصدير إلى إكسيل» | owner (spec 023) |
| One performance card, not four | owner (specs 015, 082) |
| Side bar lists two sections; no bell | owner (each section with its spec; 093) |
| «رقم الموبايل», not «رقم الهاتف» | owner, after the BRD |
| The row shows the email under the name; the frame shows the name alone | contract §1 (mine to raise) |
| A reason field in the suspend dialog, a note in the edit form, reset and unlock buttons | BRD requires; not drawn |
| The roles tab has no filter, pager or export | spec, Assumptions |
| A table's card has padding around the table; the frame's is flush | **not matched** — the shared card |
| A passing notice sits over the header's button for five seconds | **not matched** |
| English names of the nine groups ("Plans & Subscriptions", …) | **mine, unverified** — the English role-form frame was not read |

### Design review (2026-10-13, after implementation)

The design file still refused every read, so the review was of the nine saved frames, each at
full size beside the build. Corrected: the forms' field labels (small, grey and regular, as
drawn — they were bold and dark); the divider and the grey label over «صلاحيات هذا المشغل»;
the forms' titles (medium, not bold); the pencil for "edit" in the two row menus (it was a
clipboard); and the line under the fixed email, which the frame does not draw (the reason is
now read out and shown on hover). After them: this spec's 46 browser tests, the sign-in and
console tests (119), every web unit test (367), lint and the words' check pass.

Seen and **left as they are**:

| Difference | Why |
|---|---|
| Dialogs have no rule under the title and over the buttons | owner, 2026-10-14: the shared dialog stays |
| The table sits inside the card's padding | owner, 2026-10-14: the shared card stays |
| The list shows the email under the name; the roles tab a description under each role | owner, 2026-10-14: keep |
| The two-step switch is dimmed, with «مطلوب لكل المشغلين» | it cannot be changed (FR-040); the frame draws it as an ordinary switch |
| The fixed email is drawn as a grey box | it cannot be edited (ق-59); the frame draws an ordinary field |
| A fourth row in «معلومات المشغل» for the job title | the form collects it; the frame's page does not show it |

### RTL and the shared controls (2026-10-14)

Looked at the Arabic screens part by part, enlarged beside the frames. Corrected: the KPI cards'
number and note, and the performance card's number, stood at the left — they now stand at the
right under the label (a number keeps its own order inside an isolate, the block keeps the
page's direction); the same for the email under a name and the fixed email in the edit form;
the shared switch put an "on" knob at the left in Arabic — it is at the right in both languages,
as the frame and spec 010's profile draw it; «اختر الدور» is dark, as drawn. A browser test now
holds the cards to the reading start and the switch's knob to its side. Text fields and
drop-downs were already right in Arabic: labels at the right, a left-to-right value aligned
right, the drop-down's chevron at the left, the error mark at the inline end.

**Owner's choices (asked 2026-10-14)**: the switch's knob at the right when on, in both
languages; the frames' field style (grey regular labels, dark empty drop-down) in the operators'
forms only — the clinic and the sign-in screens keep theirs; the email under each name and a
description under each role stay; the shared dialog and card stay as they are.

### T002 — the frames, once Figma answered again (2026-10-16)

Read, words and pictures: edit role (`1751:813`, `1752:884`), delete blocked (`2788:16350`,
`2788:16128`), reset password (`1720:112`, `1720:304`), edit operator (`1703:70`, `1705:70`),
last super admin (`2260:1198`, `2259:1108`), and the English twins of the list, the operator's
page, add operator, the roles list, add role, the two refusals, suspend and delete. Corrected:

- Edit role draws a «الفئة» field — a box holding the «مخصص» mark, with «الأدوار المخصصة قابلة
  للتعديل الكامل، بما في ذلك الحذف.» beside it. Added (a system role: «نظام» and a sentence of mine).
- Delete while held: the second sentence sits in an amber note. Added.
- English: "You can't **give** a role with permissions you don't have."; the edit-role note "Update
  the role name, description, and the permissions it grants." The English group names I had
  guessed ("Plans & Subscriptions", "Security", "Dashboard") are the frame's.
- Confirmed as built: the reset dialog, the last-super-admin sentence, the counts in both languages.

**The drop-down**, against the component page "Dropdown" (`57:2`; Select Trigger in six states and
the Menu Panel), after the owner said it did not look like Figma: it was the browser's own
`<select>`, whose open list Windows draws. It is now the library's own (`ui/controls/select.component.ts`):
a 44-high trigger, 14 text, a grey hairline, a 1.5 purple border and a ring 4 outside it when focused
or open, a red border in error, a thin chevron that turns when open; the list beneath it at its width,
white, rounded 10, a hairline and a soft shadow, 40-high rows, the hovered row shaded, the chosen one
tinted purple with a check, a refused one grey with its reason and not choosable; worked with the
arrows, Enter, Space, Escape, Home, End and a typed letter; an optional search with "no results".
Every page that used it is unchanged. The "nothing chosen" text stays dark in the operators' forms
(the owner's choice of 2026-10-14, after the template); the component page draws it grey, which is
now the library's default.

The other component pages (Checkbox, Switch, Textarea, Badge, KPI Card, Navigation, Table, Modal,
Menu and the rest) are the next spec's: every component of the design file, in every state, in a
gallery (owner, 2026-10-16).

## Built differently from the plan

- Role actions are `opsrole.created/edited/deleted` (the clinic owns `role.*`).
- A system role is named in the trail by a fact `systemRole` / a change's `afterKey`, so a
  reader names it in their own language.
- `GET /roles/{id}` carries `copy { codes, left }`; the detail read carries `firstOperator`.
- The directive is `*appCan` (the linter's prefix), not `*opsCan`; the guard is `canMatch`, so
  a refused page keeps its address.
- The suspend and reset dialogs live in `operator.page.ts`, not in files of their own; the
  access browser tests are in `ops-roles.spec.ts`, not `ops-access.spec.ts`; the logs test is
  in `NobodyGainsTests.cs`, not in spec 010's file.
- The filter panel uses two drop-downs, not lists of choices.
- A role that may not be granted shows its reason after its name in the list ("— …"), not
  under it: a native list cannot draw a second line.
- Sending a link to an invited account sends the invitation at once, rather than marking it owed.
- The console's catalogue is a generated file (`Scs.Migrator ops-catalogue`), held by a test.
- The Operators module now references `Scs.Audit.Contracts` (the activity read); the
  architecture rule was narrowed to forbid the Audit module's insides only.
- The fact-length limit of the audit writer is 500, for every module.

## For the owner

- Every word marked **(owner)** in `contracts/operator-screens.md` — the unlock button, the two
  refusals the BRD has no text for, the activity sentences, the English of the toasts — and the
  three system roles' descriptions on the roles tab, which I wrote.
- "Hours ago" is used up to three hours back, then «اليوم · …» (the frame shows both; the
  boundary is mine).
- **A rule that is not written anywhere**: an operator with «إضافة وتعديل المشغلين» who is not
  a «مسؤول عام» may move a «مسؤول عام» to a role they can grant, or suspend one, as long as
  another stays active. The BRD speaks only of what may be *granted*. Say if this should be
  refused.
- Everyone who was an operator before this spec is now «مسؤول عام», the earlier walks'
  accounts included (`walk10…`, `walk94…`, `walk11…`): they can be suspended from the console.
- «يحمل هذا الدور 1 مشغل. انقلهم…» reads oddly for one holder.

## For the designer

The suspend dialog's reason field; the edit form's note; reset and unlock buttons on the
operator's page; the invitation email's "30 minutes"; a phone-width layout for the two tables.

## Not checked

- A screen reader itself: roles, names and descriptions are asserted by tests, not listened to.
- Firefox and Safari.
- 200 operators in the console (the platform's read is timed; the page was seen with 12).
- GitHub's own checks, which are locked for billing.
- `security.md` stays unchecked: it is the reviewer's.
