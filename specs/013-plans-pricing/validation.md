# Validation: Plans and their prices (spec 013)

**Date**: 2026-10-16 · **Branch**: `013-plans-pricing` (from develop `cd751a8`, nothing committed)

## Baseline (T001)

Taken as written and **not run again** before this work: spec 012's last full run on the code
merged into develop as `cd751a8` (`specs/012-feature-catalog/validation.md`, T060) — server unit
886, architecture 68, integration 867; `npm run ci` passed (unit tests clinic 68, patient 1,
ops 67, core 164, ui 179; 81 icons, all used); browser tests, all three apps in Chromium, 1143
passed and 4 skipped. Develop has not changed since.

## Before the work: the analysis's fixes

`/speckit-analyze` found no constitution issue; two medium and six low findings, fixed in this
spec's documents before any code (owner: "apply all 12", 16 Oct 2026):

- **I1**: two saves asking for the badge at once both succeed — the holder's row is locked first
  (research R2; T019, T020).
- **I2**: equal prices are ordered by the name in the console's language, on the console (FR-005,
  research R13, `plans-api.md` §1).
- **A1**: «الباقة الأكثر طلبًا» on a tie — the lower monthly price, then the newer plan.
- **A2**: the code keeps `a–z` and `0–9` (FR-010's wording).
- **I3**: the detail's closing sentence has a viewer's form without «تعديل» (User Story 4).
- **G1**: no address deletes a plan — tested in T013.
- **G2**: "read-only clinic" does not apply to the operators console (FR-009a).
- **U1**: the icon check fails between T004 and T016 by design (tasks' dependencies).

## The frames, read (T002)

All fourteen frames of this spec were read as pictures through the Figma connector on 16 Oct 2026
and opened: list `1180:70` / `1180:291`, empty `1250:70` / `1253:70`, filter open `1253:5666` /
`1261:756`, add `1202:70` / `1202:116`, edit `1214:70` / `1214:159`, disable `1214:601` /
`1214:761`, detail `2016:963` / `2019:1004`; their texts were also read node by node, and the
icon instances of the cards named (`Interface-Essential/trophy`, `Money-Shopping/coins-stack`).
The component pages (table, toolbar and filter, KPI card, menu, modal, form controls, switch,
select, breadcrumb) were **not read again**: the console's parts were built from them and
compared with them by spec 095.

What the frames showed that the documents did not say, and what was done:

| Frame | What it draws | Decision |
|---|---|---|
| add · edit | each grid row: name at the start, the limit field and unit, the switch at the far end | as drawn (owner, 16 Oct 2026) — `plans-screens.md` §4 corrected |
| add · edit | a limit's field shown beside an off switch | shown always, disabled while off (owner) — spec User Story 2 scenario 4 corrected |
| disable | «تأكيد التعطيل» orange, the banner orange | the button red (danger), as 012's retire (owner); the banner stays the warning tone |
| list | «الباقة الأكثر طلبًا» a short name («الاحترافية» · "Professional") | the full name, cut with … (owner); there is no short name |
| empty · filter · disable | no «كتالوج الميزات» beside «إضافة باقة +» (the list frame has it) | kept on every state of the list: it is the catalogue's only way in |
| empty | the page's line «لا توجد باقات مطابقة لبحثك حتى الآن.» | the line stays «إدارة باقات الاشتراك…» (spec, Differences found) |
| filter open | the panel: checkboxes for «الحالة», «دورة الفوترة», «تطبيق الفلاتر», «إعادة تعيين» | the console's filter of spec 095 with «الحالة» only |
| detail | the code `pro`, EGP only, «الوحدات الإضافية» | the derived code, EGP · USD, no extra units (021) |
| edit | the unit-price sub-rows and their line | not built (021) |

## Built differently from the documents

- **The one badge in the database** (research R2): a unique index on `State` filtered `WHERE
  Badge IS NOT NULL` (`UX_Plan_Badge`), not a computed `BadgeSlot` with a filter on it — SQL
  Server refused that (a filter cannot name a computed column; the first run of T013's tests failed
  on the migration). Every holder is active (`CK_Plan_Badge`), so two holders would repeat
  `active`. Saves asking for the badge lock the holder first (research R2), so two at once follow
  one another and both succeed (T019).
- **No retry of a lost code** (research R3): the codes of a base are locked to the end of the
  transaction, so two adds of one English name follow one another and take two codes (T019); the
  unique index is the last guard and answers `PLAN_CODE_TAKEN`, never seen in a run.
- **An undeclared delete** answers 401 (deny by default, SH-056), not 404 or 405: T013 checks that
  it is refused and the plan is still there.
- **«أين تُستعمل»'s answer** (012's `GET /features/{key}/plans`) now carries both names and the
  plans' own state words (`active` · `disabled`), not one name and `inactive`: 012's dialogs, words
  (`app.plans.where.disabled`) and tests follow (contract §3).
- **Two additive inputs on shared parts**, by hand: the switch's `labelHidden` (the grid draws the
  name itself; as the checkbox already had), and the KPI card's opt-in to cut a value with …
  (`--scs-kpi-value-overflow`, `--scs-kpi-value-space`; nothing changes for any other card).
- **The initials** of a plan are drawn by the plans' pages, not the shared avatar, whose letters
  follow a person's name (the frame's «أس» for «الباقة الأساسية»).
- **012's pins**: the catalogue's cards test counts `soldInPlans` and `plans` from the rows and the
  database instead of 0; the side-bar test follows «الباقات» to the plans list, then «كتالوج
  الميزات»; the where-used test reads "Disabled" and the plans' English names. Nothing loosened.
- **At 390** the list's two header buttons stand one above the other, and a grid row puts the name
  and switch on one line and the limit with its unit under them.

## Beside the design (T018, T023, T028, T032, T037)

Opened and laid beside their frames: the list (AR, EN), empty (AR), the add form (AR), the edit
form (EN), the detail (AR, EN), the disable dialog (AR), and at 390 the list (AR) and the form (AR,
EN). What stays different, and whose decision it is:

| Screen | Difference | Decision |
|---|---|---|
| list | «أضيفت <التاريخ>» under each name | the BRD (§7-9) |
| list | no «تصدير إلى إكسل» | spec 023 |
| list | «فلترة» at the toolbar's far end, not beside the search | the shared toolbar (spec 095), as 012's catalogue |
| list | «الباقة الأكثر طلبًا» shows the full name | owner (16 Oct 2026) |
| empty | no search and no table header over an empty list | as 012's empty catalogue |
| form | every row «قادمة» | the data: all 35 seeded features are upcoming (012) |
| form | the tax line, the USD prices, the badge, the change note | owner (clarified), not drawn |
| form | a limit field reads «1000», not «1,000» | the typed number, kept as typed |
| edit | no unit-price sub-rows | spec 021 |
| detail | the code `professional-plan`, not `pro`; EGP · USD | the BRD; owner |
| detail | no «الوحدات الإضافية» | spec 021 |
| dialog | «تأكيد التعطيل» red | owner (16 Oct 2026) |

## For the designer and the BRD

- Draw: the badge field, the USD prices, the change note, the tax line, «تفعيل الباقة» in the row
  menu, the kept retired / unsold rows' marks, the disabled plan's badge line, the 390 layouts.
- Correct: «دورة الفوترة» in the plans' filter (a plan has no cycle); the code `pro`; the empty
  list's words (the BRD's are followed); «حتى 3 مستخدمين» (the BRD's single form is followed,
  owner); the short name in «الباقة الأكثر طلبًا».
- The BRD's §6-12 lists the USD prices and the change note; the frames do not draw them (§13-ب-7):
  built as the BRD says (owner).

## What was run (T043), against T001's baseline

Run on 16–17 Oct 2026 on the final code, one gate after another with nothing else running (the
server suite in Debug: the API preview held the Release files):

| Gate | Result | Against the baseline |
|---|---|---|
| Server unit tests | 941 passed | +55 (886) |
| Architecture tests | 68 passed | 0 (68) |
| Integration tests | 907 passed (22 m 47 s) | +40 (867) |
| `npm run ci` | passed — lint, `i18n:check`, tools' tests, the icon check (83 registered, all used), unit tests clinic 68, patient 1, ops 99, core 164, ui 179, the three builds, the production check | ops +32 (67), icons +2 (81) |
| Browser tests, all three apps, Chromium | 1195 passed, 4 skipped (the same four guards as 012's) | +52 (1143) |
| `ops-plans` in Edge | 52 passed | new |

No size warning in any build. The gallery's kept pictures passed unchanged: the two additive inputs
of shared parts (the switch's `labelHidden`, the KPI card's cut value) change no other screen.

Test-first, as constitution VII asks for permissions and plan entitlements: `PlanTests` (with the
grid's rules), `PlansReadTests`, `PlansAddTests`, `PlansEditTests`, `PlansDisableTests`,
`PlanBookContractTests` and `FeaturePlansCountTests` were each run and seen to fail before the code
they test was written (the first run of `PlansReadTests` also caught the refused index, above).

## The real stack (T044)

The API preview restarted on the final code ("Module Plans is up to date."); the walk of
`quickstart.md`, all twelve steps, by `walk13.mjs` with two walk accounts made for it (never the
seeded operator) and a feature of its own (no seeded feature was retired). Seen: «الباقات» opens
the empty list, every card 0 with «لا توجد بيانات»; «كتالوج الميزات» and back; an empty name and
`499.999` refused at their fields, the focus on the first; a plan added with its grid
(«حتى 3 مستخدم», «بلا حدّ», an upcoming feature); the same English name twice → `…` and `…-2`; the
badge asked by one plan then another — the holder named, moved, the first's version raised; the
detail in full; the second tab refused with «تغيّرت هذه البيانات منذ فتحتها.»; disabling with a
reason (no warning: no clinics), «باقة واحدة معطلة», the menu offering «تفعيل الباقة», enabling with
the badge not back and the badge field offered again; the walk's feature retired — kept by its plan,
marked «موقوفة», not offered to a new plan — and sold again; «في الباقات» «باقة واحدة» and «أين
تُستعمل» listing the plan; the trail's sentences in the operator's activity; the viewer — no add,
no menus, no «تعديل», `/plans/new` refused, a plan posted and a disable sent by hand both 403
`PERMISSION_REQUIRED`. No server error in the API's log. Pictures: `shots013/walk/`.

## Pictures (T045, T046)

Every state of `ops-plans` taken again on the final code with `SCS_SHOTS`, 32 at 1440 and 32 at
390, both languages. Opened: the list (AR, EN), empty (AR), filtered (EN), the add form (AR, and at
390 AR and EN), the edit form (EN), the detail (AR, EN), the dialog (AR, twice); the walk's
activity. Fixed from them: the badge drop-down was empty (now «بلا شارة»); a grid row at 390 broke
unevenly (now two lines); the list's two buttons overflowed at 390 in English (now stacked).
`design-vs-real.png`: the fourteen frames beside the screens as built, opened.

## Not checked

- The real clinic figures (spec 015/016): every count of clinics, the revenue and its change, the
  subscribed clinics' list were seen only through the stand-in clinics of the tests.
- The pictures not opened one by one: the remaining states at 1440 (errors, stale, menu, none,
  failed, offline, long name, the badge, the disabled detail) and most at 390 — each is checked by
  the browser tests' `opsLooksWrong` (nothing cut, no sideways scroll) and their words.
- The component pages of the design (spec 095 compared them).
- The dialog's reason label is the template's grey but the shared textarea's 15px, a little larger
  than the frame's.

## Convergence pass 1 (T048–T050)

`/speckit-converge` found three partial gaps, none critical; fixed and run:

- **T048**: the catalogue's «مُباعة في الباقات» card (15 · «عبر 4 باقات») and the retire warning
  naming «الباقة الأساسية والباقة الاحترافية» are now checked in the browser from the plans' own
  stand-in, in both languages.
- **T049**: the EGP annual price — the billed one — decides «بلا سعر سنوي»; a USD annual set beside
  an EGP 0 no longer reads «0 ج.م · 250 دولار». Unit case added.
- **T050**: a 100-character name on the detail's head broke the page at 390 (the derived code
  under it had no break point, and the head did not shrink): the head now shrinks and the code
  wraps; tested at 1440 and 390. Closing the disable dialog from the detail — Escape, «إلغاء», ×
  — gives the focus back to «تعطيل الباقة» (the browser's own return; tested, no code needed).

Run after: `npm run ci` passed (ops unit tests 100, +1); browser tests, all three apps, 1200
passed, 4 skipped, 1 flaky — `access.spec` "within a second" (SC-008 of an earlier spec), which
passed on its retry and is known to be timing-sensitive under load. The server was not changed.

## Convergence pass 2, and the owner's request (T051, T052)

- **T051**: the limit error of a grid row is now linked to its field (`aria-describedby` →
  `limit-error-<key>`), so a screen reader says why when the focus lands on the field. Unit case,
  and a browser check of the field's accessible description in both languages.
- **T052** (owner, 17 Oct 2026, «مش عارف أرجع من كتالوج الميزات للباقات»): the catalogue now
  carries the breadcrumb «الباقات / كتالوج الميزات» · "Plans / Feature Catalogue" above its
  title, as the plan pages have it; «الباقات» opens the plans list. Tested in both languages and
  looked at, 1440 and 390 — nothing overlaps or is cut. Pictures: `shots013/ops-*-catalogue-crumbs.png`
  and `shots013/narrow/*-catalogue-crumbs.png` (scratch, not committed).

Run after: ops unit tests 16 files, 100 passed; browser tests for the plans and the catalogue,
108 passed and 1 failed: a screenshot of 012's catalogue page that could not be captured with 4
workers ("Unable to capture screenshot"), not an assertion. Run again on its own, the catalogue's
47 passed. The server was not changed.
