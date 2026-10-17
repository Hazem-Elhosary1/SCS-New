# Feature Specification: Plans and Their Prices

**Feature Branch**: `013-plans-pricing`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: empty. Taken from row `013-plans-pricing` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1, the operators console): "a plan, its
features and limits, the monthly and annual price, enabling and disabling." Depends on 012.

**BRD source**: Ops BRD module 16 (`16-ops-clinics-plans-billing.md`) — §2-2 (the permissions
«عرض الباقات والفواتير» and «إدارة الباقات والاشتراكات»), §2-4 (the view-only holder), §3-5 (the
plan: its fields), §3-7 (a plan's feature: its row in the grid), §4-2 (its states), §5-43 (the
plans list), §5-44 (adding and editing a plan), §5-45 (disabling and enabling), §5-46 (the plan's
detail), §6-12 (the plan form), §6-13 (a row of the grid), §7-9 (the list's table and cards),
§7-10 (the subscribed clinics), §7-19 (the grid's groups and rows), rules ق-٣٥, ق-٣٨, ق-٤٢,
ق-٤٣, ق-٤٥ … ق-٤٩, requirements OP-A-187 … OP-A-208 and OP-A-262, §13-ب items 3, 4 and 7.
Shared: SH-010 (a plan is a set of features from the catalogue), SH-011 (a switch or a limit),
SH-020 (every value and price is the operator's; the BRD fixes no number), SH-021, SH-055 (a
control the operator may not use is removed, not disabled), SH-056 (every route names its
permission), SH-092 (the audit trail), SH-097 (states), SH-098 (words).

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY`, page «Template — Plans» (`1170:172`),
read on 16 Oct 2026:

- «Screen / AR Plans List» (`1180:70`) and "Screen / EN Plans List" (`1180:291`): the title
  «الباقات» and its line «إدارة باقات الاشتراك المتاحة على المنصة.»; the buttons «كتالوج الميزات»
  and «إضافة باقة +»; four cards — «إجمالي الباقات» (with «باقة واحدة معطلة»), «إجمالي العيادات
  المشتركة» (with «+8 هذا الشهر»), «الباقة الأكثر طلبًا» (with «64 عيادة»), «إيراد الباقات الشهري»
  (with «+12٪ هذا الشهر»); «تصدير إلى إكسل»; search «ابحث عن باقة بالاسم...» and «فلترة»; the
  table «اسم الباقة» (with an initials avatar) · «السعر الشهري» («249 ج.م / شهريًا») · «العيادات
  المشتركة» («18 عيادة») · «الحالة» («نشطة» · «معطلة») · «الحد الأقصى» («حتى 3 مستخدمين» ·
  «مستخدمون غير محدودين» · «مستخدم واحد») · «الإجراءات» («⋯»); the row menu «عرض تفاصيل الباقة» ·
  «تعديل الباقة» · a divider · «تعطيل الباقة…» in red; «عرض 1–4 من 4 باقات».
- «Screen / AR·EN Plans List — Empty» (`1250:70`, `1253:70`): the cards at 0 with «لا توجد
  بيانات», «لا توجد باقات بعد» / «ابدأ بإضافة أول باقة اشتراك على المنصة.» and «إضافة باقة».
- «Screen / AR·EN Plans List — Filter Open» (`1253:5666`, `1261:756`): the panel «تصفية الباقات»
  with «الحالة» (نشطة · معطلة) and «دورة الفوترة» (شهري …), «تطبيق الفلاتر» and «إعادة تعيين».
- «Screen / AR Add Plan» (`1202:70`) and "Screen / EN Add Plan" (`1202:116`): the breadcrumb
  «الباقات / إضافة باقة جديدة»; «إضافة باقة جديدة» / «املأ التفاصيل أدناه لإضافة باقة جديدة إلى
  المنصة.»; the names in Arabic and English, «العملة» («جنيه مصري (ج.م)») and «السعر الشهري (ج.م)»,
  «السعر السنوي (ج.م)» («مثال: 4990 (خصم شهرين)»), «وصف الباقة»; «ميزات الباقة» with its line and
  the seven groups of the catalogue, each row a switch, its name, a number and its unit for a
  limit, and the tag «قادمة»; «إلغاء» and «إضافة الباقة».
- «Screen / AR Edit Plan» (`1214:70`) and "Screen / EN Edit Plan" (`1214:159`): «تعديل <الاسم>» /
  «حدّث تفاصيل الباقة أدناه.», the same form filled, «حفظ التغييرات». (This frame and «Edit Plan —
  Extra-Unit Prices» (`2567:1971`, `2567:1490`) also draw the unit-price sub-rows — 021's, below.)
- «Screen / AR·EN Disable Plan Confirm» (`1214:601`, `1214:761`): the dialog «تعطيل الباقة», its
  text, the warning «باقة «الاحترافية» عليها 64 عيادة نشطة حاليًا», «سبب التعطيل (اختياري)» / «اكتب
  سبب التعطيل هنا...», «إلغاء» and «تأكيد التعطيل».
- «Screen / AR Plan Detail» (`2016:963`) and "Screen / EN Plan Detail" (`2019:1004`): the
  breadcrumb «الباقات / تفاصيل الباقة»; the name with its initials, «pro · أضيفت 24 أغسطس 2026»,
  «تعديل» and «تعطيل الباقة»; «معلومات الباقة» (السعر الشهري · الحالة · السعر السنوي · تاريخ
  الإضافة · الحد الأقصى للمستخدمين · آخر تحديث · رمز الباقة · وصف الباقة); «ميزات الباقة» / «ما
  تشمله الباقة من كتالوج الميزات، وحدودها الشهرية.» in two columns by group with «✓» or the
  limit, and «لا تشمل الباقة 19 ميزة أخرى من الكتالوج. تُعدَّل الميزات وحدودها من «تعديل».»;
  «الوحدات الإضافية» (021's); «الاشتراكات» (العيادات المشتركة · الإيراد الشهري من الباقة ·
  اشتراكات جديدة هذا الشهر · عيادات في الفترة التجريبية); «العيادات المشتركة في الباقة» with each
  clinic's state.

**Differences found** (for the owner at clarification):

- The filter panel draws «دورة الفوترة»; a plan has no billing cycle (a clinic does — §13-ب-7).
  The BRD is followed: the panel offers «الحالة» alone.
- The empty list's words differ: the frame «لا توجد باقات بعد» / «ابدأ بإضافة أول باقة اشتراك على
  المنصة.», the BRD «لا توجد باقات» / «أضف أول باقة لتتمكن العيادات من الاشتراك فيها.» (§7-9). The
  BRD is followed, as spec 012 did for its catalogue (owner, 16 Oct 2026; confirmed for this list —
  clarified). The frame also puts
  «لا توجد باقات مطابقة لبحثك حتى الآن.» as the page's line; that sentence is the no-results
  state's (§7-9), and the page's line stays «إدارة باقات الاشتراك المتاحة على المنصة.».
- «الحد الأقصى» reads «حتى 3 مستخدمين» in the frame and «حتى <ن> مستخدم» in the BRD (§7-9); the
  BRD's words are followed, with «مستخدم واحد» for one (confirmed by the owner — clarified).
- The detail draws the plan's code as `pro`; the BRD derives it from the English name
  ("Professional Plan" → `professional-plan`, §3-5). The BRD is followed.
- The form draws no USD prices, no change note and no badge field; the BRD lists all three
  (§6-12, §13-ب-7). All three are built (clarified), beyond the frames, with the console's fields.
- «إعادة المحاولة» in the BRD's error states is the console's «حاول مرة أخرى» (owner, 16 Oct 2026).

**Depends on**: 012-feature-catalog (the catalogue, its published read, «أين تُستعمل»). Built on
011 (the permissions, the console's frame and side bar), 007 (the audit trail) and 095 (the
components: table, toolbar and filter, form controls, switch, row menu, confirmation, cards,
empty and error states).

## Overview

A clinic subscribes to a plan; a plan is a set of features from the catalogue with the limits it
gives and the prices it costs (SH-010, ق-٣٥). The catalogue exists (012); no plan does yet, so no
clinic can be created on one (015) and nothing can be enforced (014).

This spec delivers the plans, before clinics exist:

- **The plans list**: four cards, a searchable and filterable table of every plan — its name,
  monthly price, subscribed clinics, state and the users it allows — and, for the holders of
  «إدارة الباقات والاشتراكات», «إضافة باقة +» and each row's menu. The side bar's «الباقات» now
  opens this list; the catalogue is reached by its button «كتالوج الميزات».
- **A plan**: names in both languages, a description, monthly and annual prices before tax in
  Egyptian pounds and in US dollars, an optional badge shown to clinics, and its grid: every
  feature the catalogue sells, each included or not, each limit with its number or none.
- **Adding and editing a plan**: the code is derived once from the English name and never
  changes; each save replaces the whole grid and is checked against the catalogue before anything
  is written.
- **Disabling and enabling a plan**: a disabled plan cannot be given to a new clinic or moved to;
  the clinics on it keep it.
- **The plan's detail**: its information, what it includes grouped as the catalogue groups it,
  its subscriptions and its clinics.
- **The catalogue sees its plans**: «في الباقات», «مُباعة في الباقات», «أين تُستعمل» and the
  warning on retiring now count real plans.

Clinics on plans, changing a clinic's plan, invoices and revenue are later specs: the clinic and
its plan (015), the subscription and its invoices (016 onward), enforcement and the eight limits'
counting (014), extra units and their per-plan prices (021), exporting to Excel (023).

## Clarifications

### Session 2026-10-16

- Q: Should 013 include each plan's extra-unit prices (a price and a step for the four
  monthly-metered features), which the Edit Plan and Plan Detail frames draw inside the plan, or
  leave them to 021 as the phases list says? → A: Leave them to 021. The plan form has no
  unit-price sub-rows and the detail no «الوحدات الإضافية» card in 013; 021 adds both, with the
  rule that turning a metered feature off deletes its price (ق-٤٤, OP-A-199).
- Q: What happens to the plan fields the BRD lists but the frames do not draw — the two USD prices
  (reserved, never invoiced) and the optional change note? → A: Build them beyond the frames: a
  monthly and an annual price in US dollars beside the Egyptian ones, and an optional change note
  at the end of the form, with the console's fields; the frames are listed for the designer.
- Q: Where does the badge field («الشارة على بطاقة الباقة») go, which the owner requires (ب-١٠) and
  the form does not draw? → A: Right after «وصف الباقة» and before «ميزات الباقة», in the BRD's
  order (§5-44 step 3), with its hint and the line naming the plan that holds the badge now.
- Q: In what order should the plans list show its plans? → A: Active plans from the lowest
  monthly price up, then disabled plans the same way, ties by the name in the console's language —
  as the frame draws them
  (249 → 499 → 999, then the disabled 99).
- Q: When the catalogue stops selling a feature that some plans already include, what happens to
  those plans? → A: They keep it, like a retired feature (ق-٤٨): with its limit, marked «لا تُباع»
  in their form's grid, saved until the operator switches it off; no other plan can take it.
- Q: How does «الحد الأقصى» write the number of users — the BRD's single form or Arabic plural
  forms as the frame draws «حتى 3 مستخدمين»? → A: The BRD's single form: «مستخدم واحد» for one,
  «حتى <ن> مستخدم» for every other number, «مستخدمون غير محدودين» for none; the detail's «الحد
  الأقصى للمستخدمين» and its «حتى <ن> <الوحدة>» read the same way.
- Q: Which words does the empty plans list use — the frame's «لا توجد باقات بعد» / «ابدأ بإضافة
  أول باقة اشتراك على المنصة.» or the BRD's? → A: The BRD's: «لا توجد باقات» / «أضف أول باقة
  لتتمكن العيادات من الاشتراك فيها.» with «إضافة باقة» (§7-9), as 012's catalogue did.
- Q: When editing a disabled plan, how does the badge field appear? → A: It is removed (SH-055),
  and one line in its place says «الباقة المعطلة لا تحمل شارة. فعّلها أولًا لتختار لها شارة.».
- Q (implementation, 17 Oct 2026): how does the feature catalogue lead back to the plans? → A: A breadcrumb «الباقات / كتالوج الميزات» above its title, as the plan form, the plan detail and the feature form have; «الباقات» opens the plans list.
- Q (implementation, after reading every frame): four looks → A: the grid row's switch sits at the
  row's far end as drawn; a limit's number field is always shown and disabled while its switch is
  off; «تأكيد التعطيل» is red (danger), as 012's «إيقاف الميزة», not the frame's orange; «الباقة
  الأكثر طلبًا» shows the plan's full name, cut with … when it does not fit (no short-name field).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An operator sees every plan on the platform (Priority: P1)

An operator who may view plans opens «الباقات» and sees every plan: its name, its monthly price,
how many clinics are on it, whether it is active, and how many users it allows. They search by
name and filter by state, and open a plan's detail or the feature catalogue from there.

**Why this priority**: every later step — giving a clinic a plan, checking what a clinic may do —
starts from the plans; nobody can manage or check them without seeing them.

**Independent Test**: as holders of «عرض الباقات والفواتير» with and without «إدارة الباقات
والاشتراكات», open the list in Arabic and English with no plans, with plans of both states, with
a search and a filter, and with more than 8 plans.

**Acceptance Scenarios**:

1. **Given** an operator holding «عرض الباقات والفواتير», **When** they open «الباقات» in the side
   bar, **Then** the plans list opens (no longer the catalogue), with «الباقات» / «إدارة باقات
   الاشتراك المتاحة على المنصة.» and the button «كتالوج الميزات», which opens the catalogue.
2. **Given** an operator without «عرض الباقات والفواتير», **When** they open the console or a plan
   address, **Then** «الباقات» is not shown and the address says the page is not in their role, as
   every refused page does (spec 011).
3. **Given** the list opens, **When** it draws, **Then** the four cards read «إجمالي الباقات» with
   «<ن> باقة معطلة», «إجمالي العيادات المشتركة» with the clinics new this month, «الباقة الأكثر
   طلبًا» (the plan with the most clinics; on a tie, the lower monthly price, then the newer plan)
   with «<ن> عيادة», and «إيراد الباقات الشهري» with its
   change; a card with nothing to count says «لا توجد بيانات».
4. **Given** each row, **When** it is read, **Then** «اسم الباقة» shows the name of the console's
   language with «أضيفت <التاريخ>» under it; «السعر الشهري» «<المبلغ> ج.م / شهريًا»; «العيادات
   المشتركة» «عيادة واحدة» or «<ن> عيادة»; «الحالة» «نشطة» or «معطلة»; «الحد الأقصى» «مستخدمون غير
   محدودين», «مستخدم واحد» or «حتى <ن> مستخدم», read from the plan's «المستخدمون» row.
5. **Given** a search, **When** the operator types part of a plan's name in either language,
   **Then** only matching rows remain, and the count says how many.
6. **Given** the filter, **When** the operator opens «فلترة» and picks a state, **Then** only those
   rows remain, the Filter button carries the number of filters applied and a chip names each one
   with «مسح الكل», as the operators' list does (spec 011, spec 095); a state with no plans is not
   offered.
7. **Given** more than 8 plans, **When** the list draws, **Then** it shows 8 a page with «عرض
   <من>–<إلى> من <المجموع> باقات», the active plans from the lowest monthly price up, then the
   disabled ones the same way (ties by the name in the console's language).
8. **Given** a holder of «إدارة الباقات والاشتراكات», **When** they open a row's «⋯», **Then** it
   offers «تعديل الباقة» and, for an active plan, a divider then «تعطيل الباقة…» in red, or for a
   disabled one «تفعيل الباقة»; clicking the row opens its detail.
9. **Given** an operator who may view but not manage, **When** the list draws, **Then** there is
   no «إضافة باقة +» and no «⋯» (its only other item would open the detail, which the row itself
   does — §13-ب-4), and clicking a row opens its detail.
10. **Given** no plan exists, **When** the list opens, **Then** it says «لا توجد باقات» / «أضف أول
    باقة لتتمكن العيادات من الاشتراك فيها.» with «إضافة باقة» for those who may add one.
11. **Given** a search or filter matches nothing, **Then** it says «لا توجد باقات مطابقة لبحثك حتى
    الآن.» with «مسح الفلاتر».
12. **Given** the list cannot be read, **When** it is opened, **Then** it says «حدث خطأ ما» /
    «تعذر تحميل قائمة الباقات. تحقق من اتصالك وحاول مرة أخرى.» with «حاول مرة أخرى»; while it
    loads, it shows it is loading.

---

### User Story 2 - An operator makes a plan out of the catalogue (Priority: P1)

An operator who manages plans adds a plan: its names, description, prices before tax, an optional
badge, and its grid — every feature the catalogue sells, grouped as the catalogue groups them,
each switched on or off, each limit with its number or left empty for no limit. The plan is
saved whole, or not at all.

**Why this priority**: without a plan no clinic can be created (015) and nothing can be enforced
(014).

**Independent Test**: add a plan with switches, limits with and without numbers, and an upcoming
feature; add one whose English name is taken; try every refusal; read the result in the list,
the detail and the audit trail.

**Acceptance Scenarios**:

1. **Given** a holder of «إدارة الباقات والاشتراكات», **When** they press «إضافة باقة +»,
   **Then** «إضافة باقة جديدة» / «املأ التفاصيل أدناه لإضافة باقة جديدة إلى المنصة.» opens with the
   breadcrumb «الباقات / إضافة باقة جديدة», and the fields of §6-12 in the frame's order, the
   badge after the description, the USD prices beside the Egyptian ones and the change note last
   (clarified).
2. **Given** the prices, **When** the form draws, **Then** above them it says «الأسعار قبل الضريبة.
   تُضاف ضريبة القيمة المضافة عند إصدار الفاتورة، وفق النسبة المحددة في الإعدادات.» (OP-A-193).
3. **Given** «ميزات الباقة», **When** it draws, **Then** it shows its line («الكتالوج كاملًا. علّم ما
   تشمله هذه الباقة واضبط حدودها؛ الحدّ الفارغ يعني بلا حدّ. «قادمة» تعني أن القدرة لم تُبنَ بعد —
   يمكن أن تشملها الباقة، لكن لا شيء يفرضها قبل بنائها.») and every feature the catalogue sells and
   has not retired, in the catalogue's groups and order; each row a switch (off by default), the
   name in the console's language, for a limit a number field with its unit and the placeholder
   «بلا حدّ», and «قادمة» for a feature not yet built (OP-A-195).
4. **Given** a limit switched on with its number empty, **When** the plan is saved, **Then** it is
   saved as «بلا حدّ» (OP-A-196); a limit's number field is always shown, as drawn,
   and can be typed in only while its switch is on (owner, 16 Oct 2026).
5. **Given** the English name "Pro Plan", **When** the plan is added, **Then** its code is
   `pro-plan`, or `pro-plan-2` (then `-3` …) if that code is taken (OP-A-194).
6. **Given** a required field left empty or a price that is not a valid amount, **When** the
   operator saves, **Then** the field says «هذا الحقل مطلوب.» or «اكتب سعرًا صحيحًا مثل 499 أو
   499.99.» and nothing is sent.
7. **Given** a save the server refuses — a feature the catalogue does not hold, a number on a
   switch, a feature that is not sold or is retired and the plan did not already include, a badge
   on a disabled plan — **Then** nothing is written (ق-٤٢, OP-A-197) and the form says «تعذر
   إضافة الباقة.» (or «تعذر حفظ الباقة.» when editing), keeping what was typed.
8. **Given** a valid plan, **When** «إضافة الباقة» is pressed, **Then** it reads «جارٍ الحفظ…»,
   the plan is added active, «تمت إضافة الباقة.» is shown and the list opens with it; the audit
   trail records who added it, with its values (OP-A-208).
9. **Given** the badge field, **When** the operator picks «الأفضل», «الأكثر انتشارًا» or «الأكثر
   مبيعًا» and another plan holds a badge, **Then** the field says «تحملها الآن «<الباقة>»؛ حفظ هذه
   الصفحة ينقلها إلى هنا.», and saving moves it: afterwards exactly one plan holds a badge
   (ق-٤٥, OP-A-200). Its hint says «تظهر هذه الشارة للعيادات على بطاقة الباقة في شاشة تغيير
   الباقة. باقة واحدة فقط تحملها في أي وقت، والباقة المعطلة لا تحمل شارة.».
   For a disabled plan the field is removed and in its place one line says «الباقة المعطلة لا تحمل
   شارة. فعّلها أولًا لتختار لها شارة.» (SH-055; clarified).
10. **Given** an operator without «إدارة الباقات والاشتراكات», **When** they open the add or edit
    address, **Then** it says the page is not in their role, and the server refuses a save sent
    anyway.

---

### User Story 3 - An operator changes a plan, never its code (Priority: P2)

An operator who manages plans corrects a plan's names, description, prices, badge or grid. The
code stays as it was made. A change to what the plan includes reaches its clinics at once; a
change of price reaches only the invoices issued after it.

**Why this priority**: plans change as the product grows (new features, new limits), but adding
plans covers the first clinics.

**Independent Test**: edit a plan's name, prices, badge and grid; check the code is unchanged,
the grid replaced, the audit trail's before and after; edit the same plan in two tabs.

**Acceptance Scenarios**:

1. **Given** a plan, **When** «تعديل الباقة» (row menu) or «تعديل» (detail) is pressed, **Then**
   «تعديل <الاسم>» / «حدّث تفاصيل الباقة أدناه.» opens filled, with the breadcrumb «الباقات /
   تعديل <الاسم>».
2. **Given** the English name is changed, **When** it is saved, **Then** the code stays the one
   made at the start (ق-٤٦).
3. **Given** a save, **When** it succeeds, **Then** the plan's grid is exactly what was sent:
   every feature not switched on is not included (ق-٤٣, OP-A-198); «تم تحديث الباقة.» is shown
   and the plan's detail opens; the audit trail records who changed what, before and after
   (OP-A-208), with the change note if one was written.
4. **Given** a plan that includes a feature the catalogue has since retired or stopped selling,
   **When** its form opens, **Then** that row is shown in its group, marked as retired (or not
   sold), with its number kept, and the save keeps it unless the operator switches it off; once
   off, it is no longer offered (ق-٤٨).
5. **Given** a plan whose grid changes, **When** it is saved, **Then** what its clinics may use
   follows at once; a changed price applies to invoices issued after the save and never to one
   already issued (OP-A-206; the clinics and invoices are 015 and 016's).
6. **Given** two operators edit the same plan, **When** the second saves after the first, **Then**
   the second is refused with «تعذر حفظ الباقة.» and told to reload, and nothing of theirs is
   written.
7. **Given** a plan that no longer exists or cannot be read, **When** its form or detail opens,
   **Then** it says «تعذر تحميل هذه الباقة» / «تحقق من اتصالك وحاول مرة أخرى.» with «حاول مرة
   أخرى».

---

### User Story 4 - An operator reads a plan's detail (Priority: P2)

Any operator who may view plans opens a plan and reads what it costs, what it includes and
allows, grouped as the catalogue groups it, how many clinics are on it, and which.

**Why this priority**: operators answer clinics' questions about plans from here; a list row
alone does not say what a plan includes.

**Independent Test**: open the detail of an active and of a disabled plan, with switches,
limits with numbers and without, and excluded features; as a viewer and as a manager.

**Acceptance Scenarios**:

1. **Given** a plan, **When** its detail opens, **Then** the head shows its name with its initials,
   «<الرمز> · أضيفت <التاريخ>», and, for a holder of «إدارة الباقات والاشتراكات», «تعديل» and
   «تعطيل الباقة» (or «تفعيل الباقة» for a disabled plan); the breadcrumb is «الباقات / تفاصيل
   الباقة».
2. **Given** «معلومات الباقة», **When** it is read, **Then** it shows «السعر الشهري» and «السعر
   السنوي» as «<المبلغ> ج.م · <المبلغ> دولار» (an annual price of 0 says that the year is twelve
   months), «الحالة», «الحد الأقصى للمستخدمين», «تاريخ الإضافة», «آخر تحديث», «رمز الباقة», «وصف
   الباقة», and for a disabled plan «سبب التعطيل».
3. **Given** «ميزات الباقة» / «ما تشمله الباقة من كتالوج الميزات، وحدودها الشهرية.», **When** it
   is read, **Then** it lists the included features in the catalogue's groups and order, each with
   «✓» for a switch, «حتى <ن> <الوحدة>» for a limit (or «<ن> <الوحدة>» when the unit is counted
   per month), or «بلا حدّ»; and closes with «لا تشمل الباقة <ن> ميزة أخرى من الكتالوج.», followed
   for a holder of «إدارة الباقات والاشتراكات» by «تُعدَّل الميزات وحدودها من «تعديل».» (a viewer has
   no «تعديل»); a plan with nothing included says «لا توجد ميزات مفعّلة».
4. **Given** «الاشتراكات», **When** it is read, **Then** it shows «العيادات المشتركة», «الإيراد
   الشهري من الباقة», «اشتراكات جديدة هذا الشهر» and «عيادات في الفترة التجريبية».
5. **Given** «العيادات المشتركة في الباقة», **When** no clinic is on the plan, **Then** it says
   «لا توجد عيادات مشتركة في هذه الباقة حتى الآن.»; otherwise each clinic with its state, the
   newest to join first (§7-10).
6. **Given** an operator who may view but not manage, **When** the detail opens, **Then** there is
   no «تعديل» and no «تعطيل الباقة» / «تفعيل الباقة».

---

### User Story 5 - An operator disables a plan, and enables it again (Priority: P2)

An operator who manages plans stops a plan from being sold to new clinics. The clinics already on
it keep it and renew on it. A disabled plan loses its badge. Enabling it again is immediate and
does not bring the badge back.

**Why this priority**: plans are retired as the offer changes; without disabling, an old plan
stays assignable or must be deleted, which would strand its clinics.

**Independent Test**: disable an active plan with and without a reason, and one holding the
badge; enable it again; read the list, the detail, the published read of assignable plans and
the audit trail.

**Acceptance Scenarios**:

1. **Given** an active plan, **When** «تعطيل الباقة…» (row menu) or «تعطيل الباقة» (detail) is
   pressed, **Then** the dialog «تعطيل الباقة» says «العيادات الحالية على هذه الباقة تحتفظ بها
   ويمكنها التجديد عليها، ولا يمكن إضافة عيادات جديدة إليها.», and, when the plan has active
   clinics, the warning «باقة «<الباقة>» عليها <ن> عيادة نشطة حاليًا»; with «سبب التعطيل
   (اختياري)» / «اكتب سبب التعطيل هنا...» (up to 500), «إلغاء» and «تأكيد التعطيل».
2. **Given** «تأكيد التعطيل», **When** it is pressed, **Then** it reads «جارٍ التعطيل…»; the plan
   becomes «معطلة» with its reason and time kept, its badge is removed, «تم تعطيل الباقة.» is
   shown, the card counts «<ن> باقة معطلة», the detail shows «سبب التعطيل», and the audit trail
   records it (OP-A-202).
3. **Given** a disabled plan, **When** another module asks which plans a clinic may be given or
   moved to, **Then** it is not among them; a clinic already on it keeps it (ق-٤٧, OP-A-203).
4. **Given** a disabled plan, **When** «تفعيل الباقة» is pressed, **Then** with no dialog it
   becomes «نشطة», its reason and time are cleared, the badge does not come back, «تم تفعيل
   الباقة.» is shown, and the audit trail records it (OP-A-201, OP-A-204).
5. **Given** a disable or enable that fails, **Then** it says «تعذر تعطيل الباقة.» or «تعذر تفعيل
   الباقة.» and nothing changes.
6. **Given** a plan already disabled by another operator, **When** a second disable arrives,
   **Then** the first reason and time stay (§4-2).

---

### User Story 6 - The catalogue counts its plans (Priority: P3)

The catalogue (012) said «لا تُباع بعد» everywhere because no plan existed. Now it counts the
plans that include each feature, lists them in «أين تُستعمل», and warns before retiring a feature
that plans include.

**Why this priority**: the catalogue works without it; it closes what 012 left for plans.

**Independent Test**: include a feature in two plans; read its catalogue row, the cards,
«أين تُستعمل», and the retire dialog.

**Acceptance Scenarios**:

1. **Given** a feature included in plans, **When** the catalogue draws, **Then** «في الباقات»
   reads «باقة واحدة», «باقتان» or «<ن> باقات», and «مُباعة في الباقات» counts the features in at
   least one plan, «عبر <ن> باقات» the plans they are in.
2. **Given** «أين تُستعمل», **When** it opens for a feature in plans, **Then** it lists each plan
   with its state and its clinics, and the clinics across them (§5-61).
3. **Given** a feature in plans, **When** «إيقاف الميزة…» opens, **Then** it warns that those plans
   keep it, as 012's dialog says when plans exist.
4. **Given** the catalogue's form, **When** its breadcrumb's «الباقات» is pressed, **Then** the
   plans list opens.

### Edge Cases

- **Two plans asking for the badge at once**: one save moves it, the other then moves it again;
  at no moment do two plans hold one.
- **Two plans added with the same English name at once**: one gets `name`, the other `name-2`.
- **An English name with no Latin letter or digit** (only symbols): the code is `plan`, then
  `plan-2` …
- **A feature retired while a plan's form is open**: a plan that did not include it is refused
  if it is switched on (the form says «تعذر حفظ الباقة.» and reloading drops the row); a plan that
  did keeps it.
- **A limit made a switch in the catalogue** after plans gave it a number: the plans keep it
  included; the number is not shown and is dropped at the plan's next save. **A switch made a
  limit**: plans that include it read it as «بلا حدّ» until a number is given.
- **A feature that follows a specialty** is offered in every plan's grid like any other; that it
  is off for a clinic of another specialty is 014's (ق-٣٧).
- **An upcoming feature included**: allowed; it carries «قادمة» in the form, nothing enforces it
  (ق-٤٩), and it is not shown in the price table a clinic sees (SH-021, later specs).
- **An annual price of 0** means the plan has no annual price: a year is twelve monthly prices
  (Q9); it reads «0 ج.م» in the form and says so in the detail.
- **Prices with more than two decimals or above 9,999,999,999.99** are refused at the field.
- **No clinics yet** (until 015): every clinic count is 0, «الباقة الأكثر طلبًا» and «إيراد
  الباقات الشهري» say «لا توجد بيانات», the subscriptions read 0, the subscribed clinics list is
  empty, and the disable dialog shows no warning.
- **Words in the other language**: every plan has both names; the console shows the one of its
  language and searching matches either; the code is Latin in both.
- **A very long name** (100 characters) wraps in its cell and on the detail's head and is never
  cut.
- **Digits**: every number and amount is in Western digits in both languages, amounts with
  thousands separators (OP-B-386).
- **A phone's width**: the table scrolls inside its card, the toolbar wraps, the grid's rows stay
  readable, and every control is at least 40 by 40 (spec 095).

## Requirements *(mandatory)*

### Functional Requirements

**Reaching the plans**

- **FR-001**: The side bar's «الباقات» · "Plans" MUST open the plans list, shown to holders of
  «عرض الباقات والفواتير» and to nobody else (OP-A-187); the list's «كتالوج الميزات» MUST open the
  catalogue, and the catalogue's breadcrumb «الباقات / كتالوج الميزات» — on the catalogue and on its
  form — MUST open the list with «الباقات» (clarified).
- **FR-002**: Reading plans MUST need «عرض الباقات والفواتير»; adding, editing, disabling and
  enabling MUST need «إدارة الباقات والاشتراكات», on the server for every request (SH-056); the
  plan form MUST be a page behind the managing permission (OP-A-192), and a page the operator's
  role does not open MUST say so as spec 011's refused pages do.
- **FR-003**: A control the operator may not use MUST be removed, not disabled (SH-055); a row
  menu left with nothing but opening the detail MUST not be shown (§13-ب-4).

**The list**

- **FR-004**: The list MUST show its title, its line and the four cards of §7-9 (OP-A-188):
  «إجمالي الباقات» with the disabled ones, «إجمالي العيادات المشتركة» with those new this month,
  «الباقة الأكثر طلبًا» (the plan with the most clinics on it; on a tie, the lower monthly price,
  then the newer plan) with its clinics, and «إيراد الباقات
  الشهري» — the monthly price of every clinic billed monthly plus the annual price ÷ 12 of every
  clinic billed annually, nothing for a clinic in trial, expired or suspended (OP-A-189) — with
  its change against last month; a card with nothing to count MUST say «لا توجد بيانات».
- **FR-005**: The table MUST list every plan — active ones by monthly price from the lowest, then
  disabled ones the same way, ties by the name in the console's language (clarified) — 8 a page, with the columns and words
  of User Story 1 scenario 4 (OP-A-191).
- **FR-006**: Search MUST match part of either name, ignoring case.
- **FR-007**: The filter MUST offer «الحالة» with only the values that have plans, and MUST show
  its count on the Filter button and a chip for each applied filter with «مسح الكل» (spec 095's
  pattern).
- **FR-008**: The row menu MUST offer what User Story 1 scenarios 8 and 9 give (OP-A-262), and
  clicking a row MUST open its detail.
- **FR-009**: The empty, no-results, loading and error states MUST be drawn and worded as §7-9
  and §5-43 give them.
- **FR-009a**: Each screen of this spec MUST define its states (SH-097): loading, empty, error,
  view-only and offline — the console's bar of spec 010, a screen keeping what was last loaded,
  and a save, disable or enable sent while offline refused with its «تعذر …» message and nothing
  changed. "Not included in the plan" and "read-only clinic" do not apply: the operators console
  has no plan and no clinic of its own.

**A plan**

- **FR-010**: A plan MUST have: a code (Latin lower-case letters, digits and hyphens), derived from
  the English name when it is added — letters lowered, every run of characters other than a–z and 0–9 one
  hyphen,
  no hyphen at either end, `plan` when nothing is left — made unique with `-2`, `-3` … and never
  changed afterwards (OP-A-194, ق-٤٦); a name in Arabic and in English (1–100 each, after
  trimming); a description (1–500); a monthly and an annual price before tax in Egyptian pounds
  and in US dollars (each ≥ 0, at most two decimals, at most 9,999,999,999.99; an annual price of
  0 means no annual price); a badge — none, «الأفضل», «الأكثر انتشارًا» or «الأكثر مبيعًا»; a
  state, «نشطة» (when added) or «معطلة»; for a disabled plan, its reason (up to 500, optional)
  and when it was disabled; when it was added and last changed.
- **FR-011**: A plan's grid MUST hold, for each feature it includes, whether it is included and,
  for a limit, its number (1–1,000,000) or none for no limit; a feature not sent is not included
  (ق-٤٣). A number on a switch MUST be refused.
- **FR-012**: The prices MUST be net of tax (OP-A-193); the USD prices are kept and shown and
  MUST NOT be used to bill a clinic (OP-A-207).
- **FR-013**: At most one plan MUST hold a badge at any time; saving a plan with a badge MUST
  take it from the plan that held it, in the same change; a disabled plan MUST NOT hold one
  (ق-٤٥, OP-A-200, OP-A-201).

**Adding and editing**

- **FR-014**: The form MUST have the fields of §6-12 as User Story 2 scenario 1 orders them, with
  the drawn labels and examples, the tax line above the prices, «العملة» drawn with its one value
  «جنيه مصري (ج.م)», the badge's hint and holder line, and an optional «ملاحظة التغيير» (up to
  500) recorded with the change in the audit trail.
- **FR-015**: The grid MUST show every feature the catalogue sells and has not retired, in its
  seven groups and their order (§7-19, OP-A-195), plus any retired or unsold feature the plan
  already includes, marked as such; each row as User Story 2 scenario 3 draws it.
- **FR-016**: The form MUST check required fields and amounts before sending, with the words of
  User Story 2 scenario 6; a limit's number outside 1–1,000,000 MUST be refused at its field.
- **FR-017**: The server MUST check the whole save against the catalogue before writing anything
  and MUST refuse, not correct (ق-٤٢, OP-A-197): a feature the catalogue does not hold, a number
  on a switch, a feature not sold or retired that the plan did not already include, a badge on a
  disabled plan, a field out of its rules; a refusal MUST write nothing and say «تعذر إضافة
  الباقة.» or «تعذر حفظ الباقة.».
- **FR-018**: A save MUST replace the whole grid with what it sends (ق-٤٣, OP-A-198).
- **FR-019**: A save based on a plan that has changed since it was opened MUST be refused, with
  «تعذر حفظ الباقة.» and a word to reload.
- **FR-020**: After adding, the list MUST open with «تمت إضافة الباقة.»; after editing, the detail
  MUST open with «تم تحديث الباقة.»; the save button reads «جارٍ الحفظ…» while sending.

**Disabling and enabling**

- **FR-021**: Disabling MUST ask as User Story 5 scenario 1 gives it, keep the optional reason and
  the time, remove the badge, and leave the plan's clinics on it (OP-A-202, OP-A-203); disabling
  a disabled plan MUST NOT change its first reason or time.
- **FR-022**: Enabling MUST be immediate, MUST clear the reason and time, and MUST NOT bring the
  badge back (OP-A-201, OP-A-204).

**The detail**

- **FR-023**: The detail MUST show, in the frame's order, the head, «معلومات الباقة», «ميزات
  الباقة» with its closing line, «الاشتراكات» and «العيادات المشتركة في الباقة» (OP-A-205), worded
  as User Story 4 gives them; «الوحدات الإضافية» is 021's.

**Records and other modules**

- **FR-024**: Adding, editing, disabling and enabling a plan MUST each be recorded in the
  platform's audit trail with who, what and when, the values before and after (the grid's
  changes included), and the change note when one was written (OP-A-208, SH-092).
- **FR-025**: The plans MUST be readable by other modules without a copy of their own: a plan and
  what it includes with each limit, and the plans a clinic may be given or moved to (the active
  ones). The clinic (015), its subscription (016) and enforcement (014) read them.
- **FR-026**: A plan MUST NOT be deleted; disabling replaces it.

**The catalogue**

- **FR-027**: The catalogue (012) MUST count the plans that include each feature («في الباقات»,
  «مُباعة في الباقات», «عبر <ن> باقات»), list them with their state and clinics in «أين تُستعمل»,
  and warn on retiring when plans include the feature.

**Words and looks**

- **FR-028**: Every screen, dialog and message MUST be in Arabic and English, with the frames'
  words where they draw them and the BRD's elsewhere, Western digits in both, and amounts with
  thousands separators and «ج.م» · "EGP" (OP-B-386, SH-098).
- **FR-029**: The list, the form, the detail and the dialog MUST look as their frames draw them,
  in Arabic and English, at 1440 and 390, built from the console's components (spec 095).

### Key Entities

- **Plan**: code, names in both languages, description, monthly and annual prices before tax in
  Egyptian pounds and in US dollars, badge, state, disable reason and time, when added and last
  changed (and by whom, through the audit trail); counted, not stored: its clinics and its users'
  limit.
- **Plan feature**: a plan's row for one feature of the catalogue — included and, for a limit,
  its number or none.
- **Badge**: the closed list of three («الأفضل» · «الأكثر انتشارًا» · «الأكثر مبيعًا») and none,
  with names in both languages.
- **Feature** (012's, read): what the grid offers, its group and order, its kind and unit, whether
  it is built, sold or retired.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator who manages plans adds a complete plan — names, prices, and a grid of
  the whole catalogue — in under five minutes, with no release, and it is in the list at once.
- **SC-002**: A save is all or nothing: for every refusal of User Stories 2 and 3, the plan and
  its grid read exactly as before, in Arabic and English.
- **SC-003**: No request without its permission reads or changes a plan: every route is refused
  without «عرض الباقات والفواتير», and every write without «إدارة الباقات والاشتراكات».
- **SC-004**: After any sequence of saves, disables and enables, at most one plan holds a badge and
  no disabled plan holds one.
- **SC-005**: A plan's code never changes: after any edit, it is the code it was added with.
- **SC-006**: A disabled plan is never offered to a new clinic, and every clinic on it keeps it
  (checked again by 015 and 016 when clinics exist).
- **SC-007**: The list, the form, the detail and the dialog look as their frames draw them, in
  Arabic and English, at 1440 and 390 — laid beside the frames and every picture opened.

## Assumptions

- **No plans ship with the product.** The BRD fixes no plan, price or limit (SH-020); the list
  starts empty, and the design's four plans are examples. Operators add the plans.
- **No clinics yet.** Every count of clinics, the most wanted plan, the revenue and its change,
  the subscriptions and the subscribed clinics read from clinics and their subscriptions, which
  015 and 016 build; until then they are 0, «لا توجد بيانات» or empty. The rules for counting them
  are this spec's (FR-004), so 015 and 016 fill them without changing these screens. The revenue's
  "price paid for the current period" (OP-A-190) is later, as the BRD marks it.
- **The revenue's change** is against the same figure at the end of last month; with no last
  month, the card shows no change line.
- **Extra units are 021's** (clarified): the unit-price sub-rows, their line under the grid, the
  detail's «الوحدات الإضافية» card, and the rule that switching a metered feature off deletes its
  price.
- **Exporting to Excel is 023's**, which builds the export once for every list (spec 011's
  decision); the list has no «تصدير إلى إكسل» until then, and `plans_exported` is 023's record.
- **«العملة»** is drawn with one value and is not stored: the invoice's currency is a platform
  setting (§3-16, 024); the field shows «جنيه مصري (ج.م)» as drawn (§13-ب).
- **The USD prices** are required like the Egyptian ones and may be 0; they are reserved (ب١٥).
- **The badge is shown to clinics** on the plan-change screen, a later spec's.
- **A feature that plans include and the catalogue then stops selling** is kept by those plans
  like a retired one (ق-٤٨ read for both; clarified): shown marked «لا تُباع» in their grid with
  its limit, kept until switched off, and offered to no other plan.
- **A clinic's state words** in «العيادات المشتركة في الباقة» («نشطة · تتجدد <التاريخ>» ·
  «تجريبية · تنتهي خلال <ن> يوم» …) are 015 and 016's to fill; the list is the plan's.
- **Dates** read «24 أغسطس 2026» and «24 أغسطس 2026 · 3:15 م» as drawn, in the platform's time
  zone (Cairo); «آخر تحديث» reads relatively («منذ يومين») as drawn.
- **The success and failure toasts** are the BRD's words; «حاول مرة أخرى» is the console's retry
  word (owner, 16 Oct 2026).
- **Out of scope**, each with the spec that owns it: giving a clinic a plan and refusing a
  disabled one there (015); changing a clinic's plan, upgrades and downgrades (016–018);
  enforcement, the eight limits' counting and the refusal codes (014); extra units and their
  prices (021); export (023); the price table and plan cards a clinic sees, with the badge
  (078–079).
