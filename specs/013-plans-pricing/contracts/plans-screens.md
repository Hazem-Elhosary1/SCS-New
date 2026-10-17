# Contract: the plans' screens

**Feature**: 013-plans-pricing

Frames on «Template — Plans» (`1170:172`), Arabic / English: list `1180:70` / `1180:291`, empty
`1250:70` / `1253:70`, filter open `1253:5666` / `1261:756`, add `1202:70` / `1202:116`, edit
`1214:70` / `1214:159`, disable `1214:601` / `1214:761`, detail `2016:963` / `2019:1004`, read on
16 Oct 2026. The words are the frames' and the spec's (User Stories 1–6, FR-001 – FR-029), Arabic
· English. Undrawn ones are marked *(not drawn)*. Every word lives in `app.plans.*` of
`ops/public/i18n/{ar,en}.json`, beside 012's.

## 1. The side bar and routes

| Route | Shows | Needs |
|---|---|---|
| side bar «الباقات» · Plans (icon `gem`) | → `/plans` (was `/plans/features`) | `billing.view` |
| `/plans` | the list | `billing.view` |
| `/plans/new` | «إضافة باقة جديدة» | `billing.managePlans` |
| `/plans/:id` | the detail | `billing.view` |
| `/plans/:id/edit` | «تعديل <الاسم>» | `billing.managePlans` |
| `/plans/features` … | 012's catalogue, with the breadcrumb «الباقات / كتالوج الميزات» (owner, 17 Oct 2026); its form's breadcrumb too; «الباقات» → `/plans` | as 012 |

Without the permission, the console's "not in your role" page of spec 011.

## 2. The list — `/plans`

- **Header**: «الباقات» · Plans; «إدارة باقات الاشتراك المتاحة على المنصة.» · "Manage the
  subscription plans available on the platform."; «كتالوج الميزات» · Feature Catalogue (outline)
  and «إضافة باقة +» · "+ Add Plan" (brand, `billing.managePlans` only).
- **Cards** (`plan-cards`), the KPI card with its note as a chip:

  | Card | Icon, tone | Value | Note |
  |---|---|---|---|
  | «إجمالي الباقات» · Total Plans | `gem`, brand | the plans | «<ن> باقة معطلة» · "<n> plans disabled" («باقة واحدة معطلة» · "1 plan disabled"); none disabled → no note |
  | «إجمالي العيادات المشتركة» · Total Subscribed Clinics | `building`, success | clinics on any plan | «+<ن> هذا الشهر» · "+<n> this month" |
  | «الباقة الأكثر طلبًا» · Most Popular Plan | `trophy`, warning | the plan's full name, cut with … and whole on hover (owner) | «<ن> عيادة» · "<n> clinics" |
  | «إيراد الباقات الشهري» · Monthly Plan Revenue | `coins`, success | «<المبلغ> ج.م» · "EGP <amount>" | «+<ن>٪ هذا الشهر» · "+<n>% this month" (− when down; none without last month) |

  A card with nothing to count (no clinics yet) shows «0» and «لا توجد بيانات» · "No data".
- **Toolbar**: search «ابحث عن باقة بالاسم...» · "Search plans by name..." over both names,
  ignoring case; «فلترة» · Filter → «تصفية الباقات» · Filter Plans with «الحالة» · Status («نشطة»
  · Active, «معطلة» · Disabled — only the states that have plans), «تطبيق الفلاتر» · Apply
  Filters, «إعادة تعيين» · Reset; the count on the button; a chip per filter and «مسح الكل». No
  «تصدير إلى إكسل» (023).
- **Table** (`plans-table`), active plans by monthly price then disabled ones, equal prices by the
  name in the console's language (clarified):
  - «اسم الباقة» · Plan Name: the initials avatar (two letters of the name of the console's
    language), the name, «أضيفت <التاريخ>» · "Added <date>" under it *(not drawn: §7-9)*.
  - «السعر الشهري» · Monthly Price: «<المبلغ> ج.م / شهريًا» · "EGP <amount> / mo".
  - «العيادات المشتركة» · Subscribed Clinics: «عيادة واحدة» · "1 clinic" · «<ن> عيادة» · "<n>
    clinics" (0 → «0 عيادة» · "0 clinics", as drawn).
  - «الحالة» · Status: «نشطة» · Active (success, dot) · «معطلة» · Disabled (neutral, dot).
  - «الحد الأقصى» · Max Limit: «مستخدم واحد» · "1 user" · «حتى <ن> مستخدم» · "Up to <n> users" ·
    «مستخدمون غير محدودين» · "Unlimited users" (clarified).
  - «الإجراءات» · Actions: «⋯» for `billing.managePlans` only.
  8 rows a page, «عرض <من>–<إلى> من <المجموع> باقات» · "Showing <from>–<to> of <total> plans".
  A click on the row opens the detail.
- **Row menu**: «عرض تفاصيل الباقة» · View plan details (`menu-view`), «تعديل الباقة» · Edit plan
  (`menu-edit`), then for an active plan a divider and «تعطيل الباقة…» · Disable plan…
  (`menu-disable`, red), for a disabled one «تفعيل الباقة» · Enable plan (`menu-enable`) *(not
  drawn)*. View-only holders see no «⋯» (FR-003).
- **States**: loading (the table's skeleton); empty «لا توجد باقات» · "No plans yet" / «أضف أول
  باقة لتتمكن العيادات من الاشتراك فيها.» · "Add the first plan so clinics can subscribe to it." +
  «إضافة باقة» · Add Plan (managers only) (clarified); no results «لا توجد باقات مطابقة لبحثك حتى
  الآن.» · "No plans match your search yet." + «مسح الفلاتر» · Clear filters; failed «حدث خطأ ما» ·
  "Something went wrong" / «تعذر تحميل قائمة الباقات. تحقق من اتصالك وحاول مرة أخرى.» · "Couldn't
  load the plans. Check your connection and try again." + «حاول مرة أخرى» · Try again; offline,
  the console's bar and the last list kept.

## 3. The form — `/plans/new`, `/plans/:id/edit`

- **Head**: breadcrumb «الباقات / إضافة باقة جديدة» · "Plans / Add New Plan" (or «الباقات /
  تعديل <الاسم>»); title «إضافة باقة جديدة» · "Add New Plan" / «املأ التفاصيل أدناه لإضافة باقة
  جديدة إلى المنصة.» · "Fill in the details below to add a new plan to the platform." — or «تعديل
  <الاسم>» · "Edit <name>" / «حدّث تفاصيل الباقة أدناه.» · "Update the plan details below.".
- **Fields** in one card, two columns as drawn (one at 390):
  1. «اسم الباقة (عربي) *» · "Plan Name (Arabic) *" — «مثال: الباقة الاحترافية» (right to left in
     both languages) · «اسم الباقة (إنجليزي) *» · "Plan Name (English) *" — "e.g. Professional
     Plan" (left to right in both).
  2. The tax line *(not drawn)* «الأسعار قبل الضريبة. تُضاف ضريبة القيمة المضافة عند إصدار
     الفاتورة، وفق النسبة المحددة في الإعدادات.» · "Prices are before tax. VAT is added when the
     invoice is issued, at the rate set in Settings."
  3. «العملة» · Currency — a select with one value «جنيه مصري (ج.م)» · "Egyptian Pound (EGP)"
     (not stored) · «السعر الشهري (ج.م) *» · "Monthly Price (EGP) *" — «مثال: 499».
  4. «السعر السنوي (ج.م) *» · "Annual Price (EGP) *" — «مثال: 4990 (خصم شهرين)» · "e.g. 4990 (two
     months off)"; hint *(not drawn)* «اكتب 0 إن لم يكن للباقة سعر سنوي: تُحسب السنة 12 × الشهري.» ·
     "Enter 0 for no annual price: a year is 12 × monthly."
  5. *(not drawn, clarified)* «السعر الشهري (دولار) *» · "Monthly Price (USD) *" · «السعر السنوي
     (دولار) *» · "Annual Price (USD) *", hint «محجوزان لما بعد: لا تصدر بهما فاتورة اليوم.» ·
     "Reserved: no invoice is issued in dollars yet."
  6. «وصف الباقة *» · "Plan Description *" — textarea, «وصف مختصر يظهر للعيادات عند اختيار
     الباقة» · "A short description clinics see when choosing a plan".
  7. *(not drawn, clarified)* «الشارة على بطاقة الباقة» · "Badge on the plan card" — select «بلا
     شارة» · No badge · «الأفضل» · Best · «الأكثر انتشارًا» · Most popular · «الأكثر مبيعًا» · Best
     selling; hint «تظهر هذه الشارة للعيادات على بطاقة الباقة في شاشة تغيير الباقة. باقة واحدة فقط
     تحملها في أي وقت، والباقة المعطلة لا تحمل شارة.»; with another holder and a badge chosen,
     «تحملها الآن «<الباقة>»؛ حفظ هذه الصفحة ينقلها إلى هنا.» · "“<plan>” holds it now; saving this
     page moves it here."; for a disabled plan, no select and the line «الباقة المعطلة لا تحمل
     شارة. فعّلها أولًا لتختار لها شارة.» · "A disabled plan holds no badge. Enable it first to give
     it one." (clarified).
  8. «ميزات الباقة» · Plan Features with its line (spec User Story 2 scenario 3) — the grid, §4.
  9. *(not drawn, clarified)* «ملاحظة التغيير (اختياري)» · "Change note (optional)" — textarea,
     ≤ 500, «تُحفظ مع هذا التغيير في سجل التدقيق.» · "Kept with this change in the audit trail."
- **Buttons**: «إلغاء» · Cancel (back without saving) and «إضافة الباقة» · Add Plan or «حفظ
  التغييرات» · Save Changes; «جارٍ الحفظ…» · "Saving…" while sending.
- **Field errors** under their fields: «هذا الحقل مطلوب.» · "This field is required."; amounts
  «اكتب سعرًا صحيحًا مثل 499 أو 499.99.» · "Enter a valid price like 499 or 499.99."; a limit
  *(not drawn)* «أدخل حدًّا من 1 إلى 1,000,000، أو اتركه فارغًا لبلا حدّ.» · "Enter a limit from 1 to
  1,000,000, or leave it empty for no limit."; too long «النص أطول من المسموح.» (012's word).
- **Server refusals** at the top of the form, what was typed kept: «تعذر إضافة الباقة.» · "Couldn't
  add the plan." / «تعذر حفظ الباقة.» · "Couldn't save the plan."; a stale save adds «تغيّرت هذه
  البيانات منذ فتحتها.» with «إعادة التحميل» (spec 011's words).
- **Loading and failure**: «جارٍ تحميل الباقة» · "Loading the plan"; «تعذر تحميل هذه الباقة» ·
  "Couldn't load this plan" / «تحقق من اتصالك وحاول مرة أخرى.» + «حاول مرة أخرى».
- **After**: add → `/plans` with the toast «تمت إضافة الباقة.» · "Plan added."; edit →
  `/plans/:id` with «تم تحديث الباقة.» · "Plan updated.".

## 4. The grid — «ميزات الباقة»

- One block per group of the seven, in order, its name small and grey above its rows (as drawn);
  a group with nothing to offer is not shown.
- A row (`grid-row`, `data-key`), as drawn: the feature's name in the console's language at the
  start; the tag «قادمة» · Planned for a feature not built; *(not drawn)* «موقوفة» · Retired or «لا
  تُباع» · Not sold for one the plan kept (clarified), with its number kept; for a limit, a number
  field (placeholder «بلا حدّ» · "No limit") with the unit after it, small and grey — always shown,
  disabled while the switch is off (owner); the shared switch at the row's far end (on = included;
  off by default) (owner).
- Switching a limit off keeps what was typed until the save, which sends only what is on.
- No unit-price sub-rows and no unit-price line (021).

## 5. Disabling — the dialog

- «تعطيل الباقة» · Disable Plan; «العيادات الحالية على هذه الباقة تحتفظ بها ويمكنها التجديد عليها،
  ولا يمكن إضافة عيادات جديدة إليها.» · "Clinics already on this plan keep it and can renew on it.
  New clinics can't be added to it."; the warning banner, only when the plan has active clinics,
  «باقة «<الباقة>» عليها <ن> عيادة نشطة حاليًا» · "“<plan>” currently has <n> active clinics
  subscribed"; «سبب التعطيل (اختياري)» · "Reason for disabling (optional)" — «اكتب سبب التعطيل
  هنا...» · "Type the reason here..." (≤ 500); «إلغاء» · Cancel and «تأكيد التعطيل» · Confirm
  Disable (red), «جارٍ التعطيل…» · "Disabling…".
- After: «تم تعطيل الباقة.» · "Plan disabled."; failure in the dialog «تعذر تعطيل الباقة.» ·
  "Couldn't disable the plan.". The focus goes back to the control that opened it.
- Enabling has no dialog: «تم تفعيل الباقة.» · "Plan enabled." or «تعذر تفعيل الباقة.» · "Couldn't
  enable the plan.".

## 6. The detail — `/plans/:id`

- **Head**: breadcrumb «الباقات / تفاصيل الباقة» · "Plans / Plan Details"; the initials avatar,
  the name, «<الرمز> · أضيفت <التاريخ>» · "<code> · Added <date>"; for managers «تعديل» · Edit
  (outline) and «تعطيل الباقة» · Disable Plan (red outline) or «تفعيل الباقة» · Enable Plan.
- **«معلومات الباقة» · Plan Information**, two columns as drawn: «السعر الشهري» · Monthly Price
  «<المبلغ> ج.م · <المبلغ> دولار» · "EGP <a> · USD <b>"; «الحالة» · Status (badge); «السعر
  السنوي» · Annual Price (the same, or «بلا سعر سنوي — السنة 12 × الشهري» · "No annual price — a
  year is 12 × monthly"); «تاريخ الإضافة» · Added On (date · time); «الحد الأقصى للمستخدمين» · Max
  Users; «آخر تحديث» · Last Updated (relative); «رمز الباقة» · Plan Code; «وصف الباقة» · Plan
  Description; for a disabled plan «سبب التعطيل» · Reason for disabling (or «—»).
- **«ميزات الباقة» · Plan features** / «ما تشمله الباقة من كتالوج الميزات، وحدودها الشهرية.» ·
  "What the plan includes from the feature catalogue, with monthly limits.": the included features
  by group, in two columns as drawn (one at 390); each row the name and «✓» (switch), «حتى <ن>
  <الوحدة>» · "Up to <n> <unit>" (limit), «<ن> <الوحدة>» · "<n> <unit>" when the unit is per month
  (the four monthly features), or «بلا حدّ» · "No limit"; then «لا تشمل الباقة <ن> ميزة أخرى من
  الكتالوج. تُعدَّل الميزات وحدودها من «تعديل».» · "<n> other catalogue features aren’t in this
  plan. Edit features and limits from “Edit”." (the managers' sentence; for viewers it stops after
  the first one, since they have no «تعديل» — spec User Story 4 scenario 3); none included: «لا توجد ميزات مفعّلة» · "No features enabled".
- **«الاشتراكات» · Subscriptions**: four small figures «العيادات المشتركة» · Clinics on This
  Plan, «الإيراد الشهري من الباقة» · Monthly Revenue from Plan («<المبلغ> ج.م»), «اشتراكات جديدة
  هذا الشهر» · New Subscriptions This Month («+<ن>»), «عيادات في الفترة التجريبية» · Clinics on
  Trial.
- **«العيادات المشتركة في الباقة» · Clinics on This Plan**: each clinic's name and its state line
  (from 015/016), newest first; empty «لا توجد عيادات مشتركة في هذه الباقة حتى الآن.» · "No clinics
  are on this plan yet.".
- No «الوحدات الإضافية» card (021).
- **States**: loading; «تعذر تحميل هذه الباقة» / «تحقق من اتصالك وحاول مرة أخرى.» + «حاول مرة
  أخرى»; a missing plan the same.

## 7. The catalogue (012) — what changes

- «في الباقات», the «مُباعة في الباقات» card and «عبر <ن> باقات» count real plans.
- «أين تُستعمل» lists each plan (name, state badge, «<ن> عيادة») and the total, as 012 drew it.
- The retire dialog warns when plans include the feature (012's words).

## 8. Everywhere

- Arabic right to left, English left to right; Latin digits in both; amounts with thousands
  separators and up to two decimals.
- 1440 and 390: no sideways scroll of the page; tables scroll inside their cards; the form's two
  columns become one; every control at least 40 × 40.
- Test ids: `plan-cards`, `card-total` … `card-revenue`, `plans-table`, `plan-row` (`data-id`),
  `row-menu`, `row-view`, `row-edit`, `row-disable`, `row-enable`, `plans-empty`, `plan-form`,
  `field-<name>`, `grid-row` (`data-key`), `grid-switch`, `grid-limit`, `badge-holder`,
  `plan-save`, `disable-dialog`, `disable-reason`, `disable-confirm`, `plan-detail`,
  `plan-features`, `plan-subscriptions`, `plan-clinics`.
