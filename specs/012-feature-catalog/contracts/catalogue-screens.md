# Contract: the catalogue's screens

**Feature**: 012-feature-catalog

Frames on «Template — Plans» (`1170:172`): catalogue `2503:1262` / `2503:1114`, add `2509:1305` /
`2509:1139`, where used `2783:30449` / `2783:30129`, retire `2783:31078` / `2783:30770` (Arabic /
English), read as pictures on 16 Oct 2026 (T002). The words are the frames' and the spec's (User
Stories 1–4, FR-014 – FR-021a, §5-47 … §5-49, §6-14), Arabic · English. Undrawn ones are marked
*(not drawn)*. Every word lives in `app.plans.*` of `ops/public/i18n/{ar,en}.json`.

## 1. The side bar and routes

| Route | Shows | Needs |
|---|---|---|
| side bar «الباقات» · Plans (icon `gem`, after «الرئيسية» and before «المشغلون», as drawn) | → `/plans/features` until spec 013 | `billing.view` |
| `/plans/features` | the catalogue | `billing.view` |
| `/plans/features/new` | «إضافة ميزة» | `billing.managePlans` |
| `/plans/features/:key/edit` | «تعديل ميزة» | `billing.managePlans` |

Without the permission, the console's "not in your role" page of spec 011.

## 2. The catalogue — `/plans/features`

- **Header**: «كتالوج الميزات» · Feature Catalogue; «كل قدرة يمكن أن تشملها باقة. إضافتها هنا
  تجعلها قابلة للبيع — بلا تعديل كود.» · "Every capability a plan can include. Adding one here
  makes it sellable — no code change."; «+ إضافة ميزة» · "+ Add Feature" for holders of
  `billing.managePlans` only.
- **Cards** (`feature-cards`), the KPI card with its note as a chip:

  | Card | Icon, tone | Note |
  |---|---|---|
  | «إجمالي الميزات» · Total Features | `layers`, brand | «<ن> مجموعات» · "<n> groups" |
  | «المبنية» · Built | `check-square`, success | «تُباع اليوم» · "sellable today" |
  | «قادمة» · Planned | `barrier`, warning | «لم تُبنَ بعد» · "not built yet" |
  | «مُباعة في الباقات» · Sold in Plans | `tag`, success | «عبر <ن> باقات» · "across <n> plans" |

- **Toolbar** (spec 095): search «ابحث بالاسم أو المفتاح…» · "Search features by name or key…"
  over both names and the key, ignoring case; «فلترة» · Filter with group, type («مفتاح» ·
  Switch, «حدّ» · Limit), status («مبنية» · Built, «قادمة» · Planned, «موقوفة» · Retired *(not
  drawn)*), each listing only values that have rows; the count on the button; a chip per filter
  and «مسح الكل».
- **Table** (`features-table`), by group then by order, columns as drawn:
  - «الميزة» · Feature: the name in the console's language, the key under it, small and grey
    (never wrapped); a long name wraps and is never cut.
  - «المجموعة» · Group: the group's name.
  - «النوع» · Type: «مفتاح» · Switch · «حدّ · <الوحدة>» · "Limit · <unit>" · «مفتاح · للأسنان» ·
    "Switch · dental" (and the other specialties) · «يتبع التخصص» · "Follows specialty" for
    `specialty_exam_modules`, the one feature the clinic's specialty decides (a fixed key in the
    domain, as the monthly list is).
  - «الحالة» · Status: the badge «مبنية» · Built (success, with its dot) or «قادمة» · Planned
    (neutral); a retired feature also carries «موقوفة» · Retired *(not drawn)* (FR-008).
  - «في الباقات» · In Plans: «<ن> باقات» · "<n> plans" · «باقتان» · "2 plans" · «باقة واحدة» ·
    "1 plan" · «لا تُباع بعد» · "Not sold yet" (also for a not-sold feature).
  - «إجراءات» · Actions: ⋯.
  13 rows a page, «عرض <من>–<إلى> من <المجموع> ميزة» · "Showing <from>–<to> of <total> features",
  the table footer of spec 095 with numbered pages.
- **Row menu**: «تعديل الميزة» · Edit feature (`menu-edit`), «أين تُستعمل» · Where it is used
  (`menu-view`), then after a divider «إيقاف الميزة…» · Retire feature… (`menu-disable`, red)
  for a sold, not retired feature, or «إعادة بيع الميزة» · Sell again *(not drawn)*
  (`menu-enable`) for a retired one; a not-sold feature has neither. A holder of `billing.view`
  alone sees «أين تُستعمل» only.
- **States**: loading (the cards' and the table's skeletons); failed: «تعذّر تحميل الكتالوج.» with
  the retry button «حاول مرة أخرى» · "Try again" (the console's `retry`; owner, T1); nothing matching: the list's "nothing
  matches" with «مسح الكل»; empty (never after the seed): «لا ميزات بعد» / «أضف أول قدرة يمكن أن تشملها باقة.» · "No features yet" / "Add the first capability a plan can include." (§5-47).

## 3. The form — «إضافة ميزة» · Add Feature and «تعديل ميزة» · Edit Feature

Breadcrumb «الباقات / إضافة ميزة» · "Plans / Add Feature" (or «تعديل ميزة»). The line of «إضافة
ميزة»: «قدرة يمكن أن تشملها باقة. تظهر في نموذج الباقة فورًا، وتحمل فيه وسم «قادمة» إلى أن
تُبنى.» · "A capability a plan can include. It appears on the plan form straight away; until it is
built, it carries a "Planned" tag there." One card, two columns, in the drawn order (start column
first):

| Row | Start column | End column |
|---|---|---|
| 1 | «اسم الميزة (عربي)» * · Feature Name (Arabic) — «مثال: خريطة الأسنان» | «اسم الميزة (إنجليزي)» * · Feature Name (English) — "e.g. Dental chart" |
| 2 | «المفتاح» * · Key — «مثال: dental_chart»; read-only when editing, «لا يتغيّر المفتاح بعد الإنشاء.» | «المجموعة» · Group — select of seven, the first by default |
| 3 | «النوع» · Type — select «مفتاح» / «حدّ», «مفتاح» by default | «الوحدة (عربي)» · Unit (Arabic) (the frame: «الوحدة (للحدود فقط)») — «مثال: تحليل / شهر» · "e.g. scans / month" |
| 3a *(limit only)* | «بدون هذه القدرة» · Without this capability (the baseline; empty = none) *(not drawn)* | the unit in English · Unit (English) *(not drawn: §6-14 wants both)* |
| 4 | «التخصص» · Specialty — select «كل التخصصات» · All specialties + the fifteen | «الحالة» · Status — select «مبنية» / «قادمة», «قادمة» by default |
| 5 | «تُباع في الباقات» · Sold in plans — switch, on, with «أطفئه للقدرات التي تأتي مع المنتج ولا تُسعَّر — أساس العيادة، أو ما يقرره التخصص.» · "Turn off for capabilities that come with the product and are never priced — the core clinic, or anything the specialty decides."; disabled on a retired feature, with its reason | |
| 6 | «الوصف (عربي)» · Description (Arabic), beside «الوصف (إنجليزي)» · Description (English) — «يظهر للمشغّلين في الكتالوج وفي نموذج الباقة.» · "Shown to operators in the catalogue and on the plan form."; and the other language's description *(not drawn: §6-14 wants both)* | |

The unit fields and «بدون هذه القدرة» are shown for a limit only (FR-014; the frame shows the unit
always, labelled «للحدود فقط»).

| Field | Rule and its word |
|---|---|
| both names | required, 1–120 — «هذا الحقل مطلوب.» |
| the key | 2–64, `^[a-z][a-z0-9_]*$` — «حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.» |
| both units (a limit) | required, ≤ 40 — «الحدّ يحتاج وحدة بالعربية والإنجليزية: ماذا يعدّ؟» |
| the baseline | whole number 0–1,000,000 |
| both descriptions | ≤ 500 |

Buttons «إضافة الميزة» · Add Feature or «حفظ التغييرات» · Save changes, and «إلغاء» · Cancel.
Checks run when the form is sent and are said beside the field; focus goes to the first in error.

- Saved: back to the catalogue with «أُضيفت الميزة.» or «حُفظت الميزة.».
- Any server refusal: «تعذّر حفظ الميزة.» at the top of the form, and the form keeps what was
  typed; a key in use is also marked at the key field.
- A key the catalogue does not hold (an old link): «لم تعد هذه الميزة موجودة.».
- The edit form, while the feature is read: its skeleton; when it cannot be read: the failed state
  with the retry button.

## 4. Dialogs

| Dialog | Title | Body | Buttons | After |
|---|---|---|---|---|
| where used (`where-used-dialog`) | «أين تُستعمل ميزة <الاسم>» · "Where <name> is used" | «تشملها <ن> باقات، وعليها <ن> عيادة. ولكل باقة حدّها في «تعديل الباقة».» · "<n> plans include this feature, with <n> clinics on them. Each plan sets its own limit in Edit plan."; a table «الباقة» · Plan, «الحالة» · Status («نشطة» · Active / «معطلة» · Inactive), «العيادات» · Clinics («<ن> عيادة» · "<n> clinics"), and the row «الإجمالي» · Total; with none: «لا تشملها أي باقة بعد.» · "No plan includes it yet." *(not drawn)* | «إغلاق» · Close, and × | — |
| retire (`retire-dialog`) | «إيقاف ميزة <الاسم>؟» · "Retire <name>?" | «لن تستطيع الباقات الجديدة أن تشملها: تختفي من «إضافة باقة»، ومن «تعديل الباقة» للباقات التي لا تشملها.» · "New plans can no longer include it: it disappears from Add plan, and from Edit plan for plans that don't have it."; when plans include it, the warning box naming them and their clinics (User Story 4 scenario 3) | «إيقاف الميزة» · Retire feature, red (owner: the spec over the frame's orange), «إلغاء» · Cancel, and × | «أُوقفت الميزة <الاسم>.» · «تعذّر إيقاف الميزة.» *(not drawn)* |
| sell again (`resume-dialog`) *(not drawn)* | «إعادة بيع ميزة <الاسم>؟» · "Sell <name> again?" | «تعود الميزة إلى «إضافة باقة» و«تعديل الباقة» لكل باقة.» · "The feature returns to Add plan and Edit plan for every plan." | «إعادة بيع الميزة» · Sell again, «إلغاء» · Cancel, and × | «أُعيدت الميزة <الاسم> إلى البيع.» · «تعذّر إعادة بيع الميزة.» |

## 5. Offline, and "not in the plan"

Offline: the console's bar of spec 010 («لا اتصال بالإنترنت — ما يظهر هو آخر ما حُمّل.»); the
catalogue and an open form keep what was last loaded; a save, a retire or a sell again while
offline ends in its «تعذّر …» message and changes nothing (FR-008a). "Not included in the plan"
does not apply to the operators console.

## 6. Everywhere

Right to left in Arabic; Western digits in both languages; no sideways scroll of the page at 390
(the table becomes cards, as the operators list does; the toolbar wraps); every control at least
40 by 40 at 390; reachable by keyboard; a dialog is named, holds the focus and returns it to the
row's menu.
