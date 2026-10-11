# Validation: The Console Frame (spec 006)

**Date**: 11 Oct 2026 · **Branch**: `006-app-shell` (not committed) · **Constitution**: v1.2.0, Principle VII

What was run, what was walked, what was laid beside the design, and what was not. Fourth
writing, after the convergence tasks T056–T070.

## 1. What was run

| Gate | Result |
|---|---|
| `dotnet test src/server/Scs.sln` (Debug) | unit **401** · architecture **30** · integration **445** — all passed, run after the last server change (the seeded account of T065) |
| `npm --prefix src/web run ci` (lint, translation check, tools, unit tests, build of the three apps) | passed — clinic **46**, core **125**, ui **68**, patient 1, ops 1 |
| `npx playwright test` | **363 passed** — one of them, the timed test of spec 005, on its first retry (6.2) — and 4 skipped: the picture-only tests, which run when `SCS_SHOTS` is set |

New in this spec: 15 integration tests, 26 unit tests in clinic, 8 in core, 20 in ui, and 159
browser tests in nine files (`shell`, `shell-banners`, `shell-sources`, `shell-states`,
`shell-keyboard`, `shell-widths`, `shell-timing`, `roles-list`, `sample-list`).

**Measured** (last full run): language switch **255 ms** (SC-004, limit 1 s); offline alert shown
in **185 ms** and gone in **71 ms** (SC-006, limit 3 s each); the frame on screen and each move
between destinations inside their limits of 2 s and 1 s.

**Build warning, not an error**: the clinic app's first download is about 580 kB against a
warning budget of 500 kB (error at 1 MB). The growth is the overlay toolkit (`@angular/cdk`).
The pages for builders load on demand. The budget was left as it is.

## 2. What was walked on the real stack

Compose stack `heta-scs` (SQL Server, Mailpit), the API and the clinic app running locally.

**As an owner**, by hand in the browser pane: the frame, the roles list from the real platform
(three ready roles, their holders, "83 of 83 / 59 of 83 / 40 of 83", custom roles and their
empty state), "not available yet", "page not found", the sample list. The owner also walked it
and reported faults; all are fixed and tested (section 4).

**As `doctor@shifa.example`** — a real sign-in with the seed password and the code from Mailpit,
in a browser of its own so no one's session was touched (`walk.mjs`, kept out of the repo):

| Step | Seen |
|---|---|
| after sign-in | chip `doctor@shifa.example`, role «طبيب», clinic «عيادة الشفاء», the strip, Arabic |
| side bar | الرئيسية … الإعدادات with the three parents, as «طبيب» holds |
| `/administration/roles` | «٣ أدوار · ٨٣ صلاحية», the «للعرض فقط» notice, three rows, no button in the page |
| addresses the role does not open | `/administration/licences` and `/reports/financial`: "not in your role" inside the frame, naming the permission and «طبيب» |
| switch to English, fold | `lang=en`, `dir=ltr`, "Home", role "Doctor", folded |
| sign out, press back | the plain sign-in form, no notice; back does not return to the frame |
| sign in again in a fresh browser | English and folded — kept by the platform, not the browser |
| roles list in English | "3 roles · 83 permissions", "View only — Changing roles needs “Manage roles & permissions”." |

The account was left as found (Arabic, unfolded). `identity.StaffPreference` holds a row per
account that chose.

**As `reception@shifa.example`** — a person without the roles view, seeded for this (T065), signed
in for real with the seed password and the code from Mailpit, in Arabic and then in English:

| Step | Seen (Arabic · English) |
|---|---|
| after sign-in | chip `reception@shifa.example`, role «فريق العيادة» as the clinic wrote it in both languages, clinic «عيادة الشفاء» · "Al Shifa Clinic" |
| side bar | الرئيسية, الحجوزات, المرضى, الفواتير والتحصيل — no parent, no «الأدوار والصلاحيات» |
| the frame | top bar, strip, bell and fold button all there |
| `/administration/roles` | title «الأدوار والصلاحيات» · "Roles & permissions"; «هذه الصفحة ليست في دورك» · "This page is not in your role", naming the permission and the role; the side bar still there; no row of roles loaded |
| `/bookings` | "not available yet" under the destination's name |
| `/reports` | "page not found": no child of it is theirs |

The account was left as found. Nothing on the real stack is now unwalked that this spec names.

## 3. Each screen beside its frame

Pictures: 70 of this spec at 1440×900, Arabic and English (`SCS_SHOTS=<folder> npx playwright
test shell-states roles-list sample-list`). Frames read through the Figma connector, file
`4HLWEqlna9sbn68276LHYt`; sizes and icons read from the file's own nodes where a picture could
not settle them.

| State | Frame | How compared | Result |
|---|---|---|---|
| Default, owner | `4343:15040`, LTR `4343:16756` | pictures beside both frames | match: bars, order, icons of the top items, pill, chip; 44 from item to item; the open page's mark at the item's edge |
| Group opened | `6538:914228` | picture beside frame; `sub-item` measured (36 high, set in 44, 13 Medium, icon 20, text 127 wide) | match after fixes: every child has its icon; no English name is cut |
| Sub-items of الإدارة, إدارة البيانات | pages `5803:4`, `4160:6` | icon of each read from the file | users, key, shield-check; stethoscope, building, clipboard |
| Folded | `4343:16343` | picture beside frame; active item measured (white, hairline `#e3e8ec`, radius 10, 40×40) | match after fix. Differs on purpose: the fold button (5.11) |
| Limited permissions | `6538:915556` | picture beside frame | match: short bar, parents with nothing left are gone, the role under the name |
| Notifications open | `5999:831798` | picture beside frame; variables read | match after fixes: 4 under the top bar, icon in a 36 circle, unread dot and tint, "منذ …" |
| Notifications empty | `5999:832473` | picture beside frame | the mark is now the frame's (64 circle, the brand's pale tint). Wording differs on purpose (5.1) |
| Branch menu | `5999:833681` | picture beside frame | match after fix: 240 under the button at its start edge, the chosen one tinted with its tick |
| Account menu | `5999:834312` | picture beside frame | match: 240, far-aligned, person icon |
| Four banners | `4384:13886`, `14621`, `15163`, `15705` | trial as a picture; all four measured in the file (48 high, 24 at the sides, icon 18, 13 Medium, action 13 SemiBold, × 16, colours) | **rebuilt**: the state at the reading start with its icon (info, clock, alert, lock), the action and × at the far end; it was centred before. Action wording differs on purpose (5.1) |
| Offline | `4343:19040` | picture beside frame; alert measured (1136×50, `#fffbeb`, 13 Regular) | **moved**: under the page's header, above the page — it was above the title |
| Environment strip | in `5999:831798` | picture beside frame | match: solid `#b45309`, white words, info mark. Wording differs on purpose (5.1) |
| Roles list, view only | `6675:19376`, LTR `6675:20097` | pictures beside both frames | match after fixes: last number column 204 wide; English descriptions of ready roles as the frame writes them |
| Roles list, owner | `6674:1004380` | picture beside frame | the two cards match. The frame's «إنشاء دور» and «⋯» are spec 027 |
| Roles list, loading | `6675:58005` | picture beside frame | fixed: both cards with their names and grey rows. The frame's grey rows are three pills each; built as three lines each |
| Roles list, failed | `6675:58859` | picture beside frame | match after fixes: one card «الأدوار», warning triangle in a 64 circle, the frame's sentence, the button |
| Roles list, no custom roles | `6675:65061` | picture beside frame; wording read from the file in both languages | match after fixes: people mark, «لا أدوار مخصّصة بعد» and its line |
| Table, toolbar, chips, paging | table in `4343:15040`, `4481:16929` | pictures beside frames | match: search 320 at the start, «تصفية» at the end, chips under the search with «مسح الكل» at the far end, count at the start, paging as arrows and numbers |

**The English pictures beside the LTR frames** (T064). The LTR twins were found by listing the
pages: on "✓ Home" every shell state has one; on "Roles" only the list and its view-only state
do — loading, failed and "no custom roles" are drawn in Arabic only.

| State | LTR frame | Result |
|---|---|---|
| Default | `4343:16756` | match |
| Folded | `4343:18059` | match: 72 at the left, the open page a white square |
| Group opened | `6538:914892` | match: icons and names of the five reports |
| Notifications open | `5999:834947` | match: the panel's far edge under the bell's |
| Branch menu | `5999:836830` | match: under the button at its start edge, the chosen one tinted |
| Account menu | `5999:837461` | match |
| Grace banner | `4384:17986` | match: icon and words at the left, "Renew" and × at the right |
| Offline | `4343:20015` | match: under the header, full width of the content |
| Filter open | `4481:18441` | **rebuilt**: the panel now has its name and × at the top and a tinted foot with "Clear all" at the start and "Apply" at the end; the button's icon is the frame's funnel |
| Filtered | `4481:19083` | match: the chip under the search, "Clear all" at the far end, the count alone |
| No results | `4481:19564` | match: the toolbar stays, the search mark in the brand's tint |
| Roles, view only | `6675:20097` | match (second writing) |
| Notifications empty | `5999:835622` | match; the mark is the bell (FR-027) where the frame keeps the component's clipboard, and the wording is the BRD's (5.1) |
| Limited permissions | `6538:916216` | match: the short bar, «Clinic management» with what is left of it |
| Trial, expiring, read-only banners | `4384:16709`, `4384:17444`, `4384:18528` | match: icon and words at the left, the action at the right, no × on read-only. Action wording differs on purpose (5.1) |
| Roles list, owner | `6674:1004929` | match; "Create a role" and «⋯» are spec 027 |

Found on the way and fixed: buttons inside the console's pages, panels and windows were the
sign-in form's 48 high; they are the frames' 40.

**Not drawn, built from the shared parts**: "not available yet", "page not found", "not in your
plan", the bell's failed state, the whole sample list, and the icons of «نصوص الموافقة», «معمل
الأسنان» and «ملف الطبيب» — now listed in the spec.

## 4. Faults found after the first build, and their tests

| Fault | Found by | Test |
|---|---|---|
| The app did not start: a parent's address mixed a redirect with a match check | owner, on screen | `destinations.spec` "a configuration the router accepts" |
| Notifications in the source's order; the branch button empty for «كل الفروع» | `shell-sources` | same |
| The passing notice covered the header buttons | `sample-list` | rule 19 |
| The bell's panel and the filter panel did not close on a press outside | `shell-sources` | `shell-sources`, `sample-list` rule 3 |
| Sideways scroll and cut group names in the folded bar | owner | `shell.spec` fold test |
| Arabic descriptions and right-aligned text on the English roles list | owner | `roles-list` English test |
| A closed parent did not show the open page is inside it | owner | `shell.spec` |
| Folding jumped | owner | by eye only |
| Sub-items had no icons | owner | `destinations.spec`; picture beside `6538:914228` |
| English sub-item names cut ("Roles & permissio…") | owner | `shell-states`: no name in the bar is cut, both languages |
| Two parents could be open at once | owner | `side-bar.spec`, `shell.spec`: one at a time |
| The fold button at the foot was not seen | owner | `shell.spec`: at the top beside the first group's name, with a hairline |
| The banner centred; the offline alert above the title; folded active item; empty-state mark; loading as one card | second comparison with the frames | `shell-banners`, `list.spec`, `roles-list` |
| The filter panel had no name, no ×, and both buttons at one end; console buttons 48 high | comparison with the LTR frames | `sample-list` rule 3, `overlay.spec` |
| The side bar's rule for people who ask for less motion was broken by a misplaced block of styles | third convergence | by reading; no test |

## 5. Owner items

1. **Three wordings follow the BRD's rulings, not the design's text** (research R13): the strip
   («بيئة اختبار …»; design «بيئة تجريبية …»); the empty bell («لا إشعارات جديدة», no «تعليم الكل
   كمقروء»; design «لا شيء هنا بعد»); the banner's action («تجديد الاشتراك»; design «تجديد الآن»).
2. **The addresses** of the twenty-two destinations are this spec's choice.
3. **"Not in your plan"** has no drawn card.
4. **The chip shows the email** until accounts have names (spec 026).
5. **79 → 83** permissions; the line under the roles title is counts only (the frame adds the
   date and writes numbers as words), and shows a dash while loading or failed where the frame
   writes «تُحمَّل الأدوار» / «تعذّر تحميل الأدوار».
6. **The inbox count on the folded bar** is not shown; the folded frame draws none.
7. **Ready-role descriptions** are translation text while a ready role keeps its own name.
8. **Two icons differ in drawing** from the file: the inbox and the ticket.
9. **The loading rows** of the roles list are lines, not the frame's three pills.
10. **`ui.state.error.retry`** in English is now "Try again", as the roles frame writes it.
11. **A seeded account was added** for the walk: `reception@shifa.example`, holding the custom
    role «فريق العيادة» (now seeded in both clinics). Local and test environments only.
12. **Two owner decisions differ from the frames** and are recorded in the spec: the fold button
    is at the top of the side bar beside «القائمة» with a hairline (frames: at the foot); only
    one parent of the side bar is open at a time. The design should be brought into line.

## 6. Not done, and known weak points

1. `SignInLogTests.Nothing_typed_or_issued_at_sign_in_reaches_the_logs` (spec 003) failed once in
   the full server run: the random six-digit code equalled six digits of a timestamp in a log
   line. It passes alone. The test, not the product, is at fault; it was left unchanged.
2. `access.spec` "within a second" (spec 005) is timed: it failed twice and passed on its
   second retry in the last full run, and passes three times out of three alone. With the frame
   around the page the figure is closer to its limit on a busy machine.
3. `access.spec` "within a second" fails when the server suite runs at the same time on this
   machine (three tries out of three in one such run); alone, and in a full browser run by
   itself, it passes.
4. The baseline run of T001 was not repeated before the first change.
5. The files are fewer than `tasks.md` lists: list parts in `ui/src/lib/list/list.ts`; menu,
   tooltip and toast in `ui/src/lib/overlay/overlay.ts`; sources and the preferences service in
   `core/src/lib/shell/sources.ts`; the frame's pages in `clinic/src/app/shell/pages.ts`; the
   top bar, account menu, branch switch and banner in `shell.component.ts`; the bell in
   `bell-panel.component.ts`.
7. The fold animation was judged by eye; no test measures it.
