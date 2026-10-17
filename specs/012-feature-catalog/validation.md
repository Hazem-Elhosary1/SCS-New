# Validation: The feature catalogue (spec 012)

**Date**: 2026-10-16 · **Branch**: `012-feature-catalog` (nothing committed)

## Baseline (T001)

Taken as written and **not run again** before this work: the server from spec 011's last full run
(spec 095 changed no server code) — 853 unit, 65 architecture, 827 integration; the web from spec
095's last run (T077) — unit 68 / 1 / 40 / 164 / 179 (clinic, patient, ops, core, ui); browser,
all apps in Chromium, 1096 passed and 4 skipped.

## Before the work: the analysis's fixes

`/speckit-analyze` found one constitution gap and a few inconsistencies; fixed in the documents
before any code:

- **C1** (constitution VI): the offline state is FR-008a, `catalogue-screens.md` §5 and T038;
  "not included in the plan" is stated as not applying to the operators console.
- **I1** (FR-017, constitution IV): retiring and selling again record before and after.
- **A1**: a server's refusal is said at the top of the form, a key in use also at the key field.
- **G1**: the edit form's loading and failed states are in T031 and T032.
- **U1**: there is no delete; the 404 case is an old link.
- **D1**: «تُباع في الباقات» cannot be turned off a retired feature — in the spec's assumptions.
- **I2, I3, I4**: task references, the parallel list, the icon count.
- **T1** (the retry word) was put to the owner — see below.

## The frames, read (T002)

All eight frames of this spec were read as pictures through the Figma connector on 16 Oct 2026
and opened: catalogue `2503:1262` / `2503:1114`, add `2509:1305` / `2509:1139`, where used
`2783:30449` / `2783:30129`, retire `2783:31078` / `2783:30770`. `contracts/catalogue-screens.md`
was rewritten from them: the table's six columns (الميزة · المجموعة · النوع · الحالة · في الباقات ·
إجراءات), the cards' icons and notes, the form's order (names; key and group; type and unit;
specialty and status; the sold switch with its hint; the description), the type and the status as
drop-downs (not segmented), the English words, and «يتبع التخصص» on `specialty_exam_modules`. The
English names of the seed were taken from the English frame into `data-model.md`.

Differences between the frames and the spec, and which side is built:

| Frame | Built | Whose |
|---|---|---|
| 34 features, «خريطة الأسنان وخطة العلاج» as one | 35, the two apart (OP-A-222) | clarified in the spec |
| one unit and one description | both languages of each (§6-14) | the BRD |
| the unit always shown, labelled «للحدود فقط» | the units and «بدون هذه القدرة» for a limit only (FR-014) | the spec |
| no baseline field | «بدون هذه القدرة» (not drawn) | the BRD (§3-6) |
| the retire dialog's button orange | red | owner, 16 Oct 2026 |
| the cards' numbers (26 built, 16 sold across 4 plans) | the catalogue's own (0 built, 0 sold until features and plans are built) | the spec |
| no retired mark, no sell again, no messages, no empty where-used, no filter panel | built as the spec words them, listed for the designer | the spec |

## The owner's answers (16 Oct 2026)

- **T1, the retry word**: «حاول مرة أخرى», as the console already says (not the spec's «أعد
  المحاولة», nor SH-098's verbal noun); the spec was corrected. The BRD's copy rule stays an open
  item for the BRD's owner.
- **T2, the retire button**: red, as the spec says, over the frame's orange.
- **T3, the switch row of the form**: the console's shared switch, the toggle beside its label as
  in spec 011's forms — not the frame's title at the start and toggle at the far end. Listed for
  the designer.
- **T4, the «المبنية» card's icon**: the frame's `check-square`, added from the design file.

## Each screen beside its frame (T023, T028, T032, T037)

Pictures from `SCS_SHOTS` (`src/web/node_modules/.tmp/shots012`), every one opened before being
compared:

- **The catalogue** (`ops-ar-features`, `ops-en-features`, `narrow/ar-features`) beside
  `2503:1262` / `2503:1114`: header and line, the four cards with their icons and notes, the
  search and the Filter button, the six columns, the name over the key, the badges with their
  dot, the ⋯ menu, the count and the numbered pages — as drawn. At 390 each row becomes a card, as
  the operators list does. Kept different by decision: the cards read 35 · 0 · 35 · 0 (the frame's
  34 · 26 · 8 · 16 are the replaced product's); the «المبنية» card first used the console's plain
  check — changed to the frame's `check-square` (owner, T4).
- **The form** (`ops-ar-feature-add`, `-add-limit`, `ops-en-feature-add`) beside `2509:1305` /
  `2509:1139`: the breadcrumb, title and line; two columns in the drawn order; the type and the
  status as drop-downs; the sold switch with its hint; the buttons. Kept different: both units
  and both descriptions (§6-14), the units and «بدون هذه القدرة» for a limit only, the switch beside
  its label (owner, T3). Found and fixed on looking: the specialty drop-down showed empty with
  nothing chosen (it now says «كل التخصصات»); the English description's and, on the English page,
  the Arabic fields' text ran in the page's direction (each now runs in its own language's).
- **Editing** (`ops-*-feature-edit`, `-edit-missing`) — not drawn apart: the add frame with the
  key read-only and its line, «حفظ التغييرات»; the missing feature's state.
- **The dialogs** (`ops-*-where-used`, `-where-used-none`, `ops-*-retire`, `ops-*-resume`) beside
  `2783:30449` / `2783:30129` and `2783:31078` / `2783:30770`: title, sentence, the plans' table
  with states and clinics and the total, the warning box naming the plans and their clinics, ×
  and the buttons. Kept different: the retire button red (owner, T2). Not drawn, built as the
  spec words them: the empty where-used, the sell-again dialog, every message.

Found by the browser tests and fixed: the row menu kept the row it was first opened for — after
a retire it still offered «إيقاف الميزة…»; it now reads the row as the catalogue holds it.

## Built differently from the plan

- The console's routes sit in `app.routes.ts` beside spec 011's, behind `guarded(...)`; no
  `plans.routes.ts` file (the plan named one).
- The words' tests (T019) live in `plans.spec.ts` with the page's and the form's, not in a
  `feature-words.spec.ts` of their own.
- The browser tests' stand-in for the catalogue is its own file, `src/web/e2e/ops-features-platform.ts`,
  built on `opsPlatform` and `opsTeam` (whose role decides what is held), not lines added to
  `ops-team-platform.ts` as T013 named; it refuses as `contracts/features-api.md` §2 does (T053).
- The seed is a migration of its own (`SeedFeatures`) after the table's, with the rows in code
  (`FeatureSeed`) so the unit tests and the migration read one list (research R4).
- `Version` is an edit counter the entity raises, mapped as EF's concurrency token, instead of a
  database row version: a retire or a sell again does not raise it, so an edit open across one
  still saves (spec edge case).
- The form says the platform's own field refusals beside their fields (`VALIDATION_FAILED`,
  `FEATURE_UNITS_REQUIRED`) as the operators' form does, besides «تعذّر حفظ الميزة.» at the top.
- Two packages that arrive only transitively, `System.Security.Cryptography.Pkcs` and `.Xml`,
  are pinned to 10.0.13 in `Directory.Packages.props`: their 10.0.0–10.0.12 carry advisories
  published 13 Oct 2026 and a fresh restore of any project failed on them (the first build of the
  new module). This touches every project's restore, not this spec's code alone.
- The operator's page gained sentences for the four feature actions (`activity-sentences.ts`),
  so the trail reads «أضاف الميزة «…»» instead of «نفّذ إجراءً على المنصة».
- The retry button reads «حاول مرة أخرى» (the console's word; owner, T1) where the spec wrote
  «أعد المحاولة»; the spec was corrected.

## For the designer and the BRD

- **Designer**: «إعادة بيع الميزة» (menu item, dialog, messages); the retired mark «موقوفة» in the
  status column; the filter panel's three facets; the empty «أين تُستعمل»; the form's second unit,
  second description and «بدون هذه القدرة»; the retire button in red; the sold switch beside its
  label; «خريطة الأسنان وخطة العلاج» drawn as one row where the seed has two.
- **BRD**: SH-098's verbal-noun rule for controls against the console's «حاول مرة أخرى» (owner's
  choice, T1); §5-47 draws «كتالوج الميزات» as a button on the plans page — the side bar's
  «الباقات» opens the catalogue until spec 013 moves it there.

## Not checked

- Firefox and Safari (no browser of theirs runs here); a screen reader itself (names, roles,
  `aria-*` and focus are checked by the tests, not heard); real phones (390 is a browser's width).
- Plans and clinics on the «في الباقات» column, the cards' «مُباعة في الباقات» and the retire
  warning on the real stack: there are no plans until spec 013 — the browser tests exercise them
  with intercepted plans only.
- SC-004 end to end (a retired feature keeps every plan and clinic unchanged): proved here only
  as far as the catalogue reaches; 013 and 014 check it again when plans and clinics exist.
- GitHub's own checks, locked for billing.

## On the real stack (T041, 16 Oct 2026)

`src/web/node_modules/.tmp/walk12.mjs` against the real API (restarted with the module: "Module
Plans is up to date."), database and mailbox, with the walk's own accounts (`walk12a-…`,
`walk12b-…@heta.example`; passwords made in the script and never printed). The seeded operator was
not touched. The viewer is «مشاهد فقط» (holds `billing.view` without `billing.managePlans`), in
place of the quickstart's custom role. Pictures in `shots012/walk/`. Seen:

1. The command's admin arrives: side bar «الرئيسية», «الباقات», «المشغلون»; «الباقات» opens
   `/plans/features`, «كتالوج الميزات»; cards 35 · 0 · 35 · 0 with «7 مجموعات», «تُباع اليوم»,
   «لم تُبنَ بعد», «عبر 0 باقات»; 13 rows, «عرض 1–13 من 35 ميزة»; no Arabic-Indic digit.
2. Search «أسنان» and "DENTAL" both find `dental_chart`, `dental_lab`; «السريرية» + «حدّ» leaves
   none and says «لا توجد ميزات تطابق البحث.» with «مسح الكل»; «السريرية» + «قادمة» leaves 10 with
   «فلتران مطبّقان» on the button; «مسح الكل» brings the 13 back.
3. A limit without units: the unit sentence twice, focus on the Arabic unit; `max_users` as a
   key: «تعذّر حفظ الميزة.» at the top, «هذا المفتاح لميزة أخرى.» at the key, the key kept; a valid
   switch in «القنوات»: «أُضيفت الميزة.», back at the catalogue, last in its group (after
   `message_credits`, before `allow_analytics`); the cards read 36.
4. Edit: «تعديل ميزة», the key read-only with «لا يتغيّر المفتاح بعد الإنشاء.»; renamed and moved
   to «التقارير»: «حُفظت الميزة.», now after `allow_custom_branding`; a second tab's save on the
   older reading: «تعذّر حفظ الميزة.».
5. «أين تُستعمل ميزة …»: «لا تشملها أي باقة بعد.».
6. The menu offers «إيقاف الميزة…»; the dialog with no warning (no plans); «أُوقفت الميزة ….»,
   «موقوفة» on the row, the menu now «إعادة بيع الميزة»; «أُعيدت الميزة … إلى البيع.», the mark
   gone, the menu «إيقاف الميزة…» again.
7. The admin's recent activity: `feature.resumed`, `feature.retired`, `feature.updated`,
   `feature.created` with the feature's name, read on the operator's page as «أعاد بيع الميزة
   «…»», «أوقف بيع الميزة «…»», «عدّل الميزة «…»», «أضاف الميزة «…»»; the feature as the platform
   holds it: version 2, group `reports`, order 30, not retired.
8. The viewer: the side bar shows «الباقات»; the catalogue reads; no «إضافة ميزة»; the row menu
   is «أين تُستعمل» alone; `/plans/features/new` says «لا يمكنك فتح هذه الصفحة»; a feature posted
   by hand and a retire by hand both answer 403 `PERMISSION_REQUIRED`.

## What was run (T040, run again by T060 on the final code), against T001's baseline

Run on 16 Oct 2026, 22:18–22:45 and again after the fix below, one gate after another with nothing
else running:

| Gate | Result | Against the baseline |
|---|---|---|
| Server unit tests | 886 passed | +33 (853) |
| Architecture tests | 68 passed | +3 (65) |
| Integration tests | 867 passed (20 m 26 s) | +40 (827) |
| `npm run ci` | passed — lint, `i18n:check`, tools' tests 28, the icon check (81 registered, all used), unit tests clinic 68, patient 1, ops 67, core 164, ui 179, the three builds, the production check (no file of `dist/ops` names the gallery) | ops +27 (40), ui 0 (179) |
| Browser tests, all three apps, Chromium | 1143 passed, 4 skipped (two only with `SCS_SHOTS`, two "Edge is not installed" guards) | +47 (1096) |
| `ops-features` in Edge (before the convergence passes) | 35 passed | new |

No size warning in any build.

**Found by the final run, and fixed**: the first run of T060 failed one test of 1147 — the gallery's
kept pictures of the input field in Arabic, ops look (21, 12 and 24 pixels across seven specimens,
the same on three reruns). Cause: formatting the shared `text-field.component.ts` and
`form-controls.ts` with prettier had reflowed their templates (a space before the label's
asterisk) and rewritten lines of no concern to this spec; the same formatter had reflowed
`ops-roles.spec.ts` (299 lines for a one-line change) and `ops.spec.ts`. All four were restored
from `develop` and only this spec's lines put back — the two direction inputs (`rtl` on the text
field, `dir` on the textarea: 8 lines), the side bar's «الباقات» and the sections' list; one
unrelated reformat in `app.routes.ts` was undone. The expected, actual and diff pictures were
opened first. Then the gallery's pictures, `ops-roles` and `ops-features` (201 tests), `ui` 179,
`ops` 67, lint, the whole `npm run ci` and the whole browser suite (1143) passed again. No server
file changed after the server suite passed.

## Pictures (T042, T043)

- `SCS_SHOTS` took 16 states × 2 languages × 2 widths (64 pictures) in `shots012/`, plus the walk's
  10 in `shots012/walk/`. Opened one by one: the Arabic catalogue (wide and at 390), the Arabic
  limit form (twice: before and after the specialty and direction fixes), the English add form
  (three times: the direction of the Arabic fields was fixed twice — first the box and the label
  flipped together, then the box alone through the new `rtl`/`dir` inputs), the Arabic retire
  dialog, the English where-used dialog, and the walk's trail picture. The other states were seen
  through `design-vs-real.png`'s eight bands and the tests' `opsLooksWrong` (no sideways scroll,
  nothing cut, no Arabic-Indic digit) — not each file opened.
- `design-vs-real.png` (1.44 MB, 1832 × 7040): the eight frames of this spec beside the screen as
  built, Arabic and English, opened twice (before and after the English form's fix).
- Shared controls changed for this: `scs-text-field` gains `rtl`, `scs-textarea` gains `dir`
  (additive; the operators' and the clinic's screens unchanged — the browser suite of all three
  apps passed after the change, and `ui`'s 179 tests).

## The adversarial review (16 Oct 2026, T044)

A workflow of 69 agents read the whole change set against the spec's documents and the
constitution: five finders (server rules, access and audit, the console, tests against the spec,
documents against the code), each finding then judged by two independent agents (one told to
refute it, one asked whether it matters for the spec being finished). 32 findings judged, 18
confirmed by both, 14 refuted (misreadings of the code, deliberate decisions already recorded, or
out of this spec's scope). Of the 18:

- **Fixed in the server**: the key's pattern was anchored with `$`, which in .NET lets a trailing
  newline through to the database's check (a 500 instead of a 400) — now `\z`, with the cases in
  `FeatureTests`; an edit whose body left `isSold` out was treated as "sold" — now it keeps the
  feature's value.
- **Fixed in the console**: the specialty drop-down listed «كل التخصصات» twice; the English
  where-used sentence read "1 plan include"; a 64-character key overflowed a row at 390 (now cut
  with an ellipsis and whole in its title); a reload that failed after a retire discarded the
  catalogue (now keeps it and says so); the retire dialog offered its button before the plans
  were read and dropped the warning silently when the read failed (now waits, and says «تعذّر
  قراءة الباقات التي تشملها»); a late answer for a previous feature could overwrite an open
  dialog (dropped now).
- **Fixed in the tests**: a 404 on save; the sell-again failure's message; FR-008a's offline save
  and sell again (only a retire was tried); the count after a search; an inactive plan, the
  clinics per row and «إغلاق» in where-used; the retire sentence and the absence of the warning;
  the catalogue checked on the screen after an add and an edit, not only in the stand-in's memory.
- **Fixed in the documents**: the contract named a `403 OPS_FORBIDDEN` the gate never answers
  (it is `403 PERMISSION_REQUIRED`, and a clinic's session is 401); the quickstart quoted a
  where-used sentence the console does not say; T026's text claimed a question before leaving
  with unsaved changes, which the operators' forms do not ask (spec 011's do not either) — the
  task's text was corrected and the question is listed for a later spec; the where-used `total`
  is now defined; the published contract's shape names both names; `FeatureSeed`'s comment named
  the wrong migration.
- **Kept, recorded**: the trail names a feature by its Arabic name (`TargetName` is one column,
  as spec 011's roles are named as written); an English reader of the operator's page sees the
  Arabic name. For the BRD and the designer with the trail's own screen (spec 023).
- Rerun after every fix: Plans unit 33, Plans integration 40, ops unit 57, lint, the catalogue's
  35 browser tests (and the whole browser suite and `ui` before the last four — the shared
  controls were not touched by them).

Unverified by the pair (listed by the finders beyond the eight each dimension verified): eleven
low items of test breadth (a sweep of all 35 names in the search, the built filter value, the
loading picture's timing, FeaturesReadTests leaving 265 rows in the shared catalogue, the
stand-in's refusals) — read, and left for the next converge pass.

## Convergence (T045–T055, 16 Oct 2026)

`/speckit-converge` read the code against every requirement, acceptance scenario, edge case and
plan decision and found no functional gap, and eleven places where a requirement was built but not
held by a test, or a deviation not written down. Done:

- **T045** the empty catalogue (FR-007): unit and browser tests in both languages; its picture
  (`ops-ar-features-empty`, `ops-en-features-empty`) opened — four zero cards, «0 مجموعات», the
  mark, «لا توجد ميزات بعد.» and its line, no search and no filter.
- **T046** the four cards' numbers: 35 · 0 · 35 · 0, then with one feature built and two plans
  intercepted 35 · 1 · 34 · 1 «عبر 2 باقات»; the «مبنية» badge, «باقتان» and the built filter value.
- **T047** the group and type facets offer only what exists (no limit → no «حدّ»; no report → no
  «التقارير»).
- **T048** a viewer at `/plans/features/max_users/edit` gets the "not in your role" page.
- **T049** `feature.resumed`'s actor, target and `isRetired` true → false.
- **T050** after the two shape refusals of an edit, the row is unchanged and nothing recorded.
- **T051** the form's own checks: key of 1 and 65, baseline −1, 1,000,001 and "abc", a 121-letter
  name, a 41-letter unit, a 501-letter description — each said, nothing sent.
- **T052** SC-005 as a sweep: each of the 35 seeded features found by part of its Arabic name, of
  its English name in capitals and of its key in capitals, in both languages: none missed.
- **T053** the stand-in refuses descriptions over 500, a baseline outside its range and values
  outside the four closed lists.
- **T054** the 300-feature read timed as the best of three after a warm-up.

Counts after convergence: Plans integration 40 of 40; ops unit 66 (+9); `ops-features` browser
tests 45 (+10; 2 more for the empty picture); lint clean.

## Convergence, second pass (T056–T057)

- **T056**: closing a dialog by its own button left the focus on a button of the closed dialog
  (Escape alone gave it back). Now every dialog of the catalogue gives the focus back to the menu
  of the row it was opened from, however it closes — Escape, ×, «إغلاق», «إلغاء», or after a
  retire or a sell again once the catalogue is read again. Held by a browser test in both
  languages for each way of closing.
- **T057**: «بدون هذه القدرة» now says BRD §6-14's placeholder «بلا حدّ» · "No limit" and its
  hint «ما تحصل عليه العيادة حين لا تشمل باقتها هذا الحدّ. اتركه فارغًا لبلا حدّ، أو 1 لواحد، أو 0
  لِلا شيء.»; held by a unit test.

After: ops unit 67 (+1), `ops-features` browser tests 47 (+2), lint clean, translation check passed.

## Convergence, third pass (T058)

The empty catalogue now says BRD §5-47's words — «لا ميزات بعد» / «أضف أول قدرة يمكن أن تشملها
باقة.» · "No features yet" / "Add the first capability a plan can include." — where it said words
of the screens contract's own (corrected too). Both pictures re-taken and opened. After: ops unit
67, `ops-features` 47, lint and the translation check clean.

## Convergence, fourth pass (T059)

`design-vs-real.png` rebuilt at 22:14 (1.44 MB, 1832 × 7040) from the pictures of the 22:11 run,
taken after every screen change of the review and of the convergence passes; opened, and its
English where-used source opened too — "Included in 2 plans, with 76 clinics on them.", an
active and an inactive plan, «إغلاق» · Close. It replaces the picture of 18:2x named in
"Pictures (T042, T043)" above.
