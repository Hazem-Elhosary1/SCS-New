# Feature Specification: The Feature Catalogue

**Feature Branch**: `012-feature-catalog`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: empty. Taken from row `012-feature-catalog` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1, the operators console): "a feature as a
switch or a limit, its group, whether it follows the clinic's specialty, retiring it." (093, the
next row, was deferred by the owner on 16 Oct 2026 until the first spec that raises an operator
event.)

**BRD source**: Ops BRD module 16 (`16-ops-clinics-plans-billing.md`) — §3-6 (the feature: its
fields), §4-3 (its states), §5-47 (the catalogue), §5-48 (adding and editing a feature), §5-49
(retiring a feature), §5-61 («أين تُستعمل»), §6-14 (the feature form), §7-12 (the catalogue's
table), §7-19 (the drawn rows — the catalogue's seed), rules ق-٣٥ … ق-٣٩, ق-٤٢, ق-٤٦, ق-٤٨,
ق-٤٩, requirements OP-A-209 … OP-A-223 and OP-A-263; the fifteen specialties (§6, «قيم التخصّص
الخمس عشرة»). Shared: SH-010 (a plan is a set of features from a catalogue the operator runs; no
feature is added by changing code), SH-011 (a switch or a limit), SH-012 (a feature may follow
the clinic's specialty), SH-013 (fifteen specialties), SH-021 (an unbuilt feature is not shown
in the clinic's price table), SH-056 (every route names its permission), SH-092 (the audit
trail), SH-098 (words).

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY`, page «Template — Plans» (`1170:172`),
read on 16 Oct 2026:

- «Screen / AR Feature Catalogue» (`2503:1262`) and "Screen / EN Feature Catalogue" (`2503:1114`):
  the title «كتالوج الميزات» and its line «كل قدرة يمكن أن تشملها باقة. إضافتها هنا تجعلها قابلة
  للبيع — بلا تعديل كود.»; «+ إضافة ميزة»; four cards («إجمالي الميزات» 34 · «7 مجموعات»,
  «المبنية» 26 · «تُباع اليوم», «قادمة» 8 · «لم تُبنَ بعد» (the design's numbers come from the product
  being replaced; this rebuild starts every seeded feature «قادمة» — clarified), «مُباعة في الباقات» 16 · «عبر 4
  باقات»); search «ابحث بالاسم أو المفتاح...» and «فلترة»; the table «الميزة» (name, key under
  it) · «المجموعة» · «النوع» · «الحالة» · «في الباقات» · «إجراءات», 13 rows a page, «عرض 1–13 من
  34 ميزة»; the row menu «تعديل الميزة» · «أين تُستعمل» · a divider · «إيقاف الميزة…» in red.
- «Screen / AR Add Feature» (`2509:1305`) and "Screen / EN Add Feature" (`2509:1139`): the
  breadcrumb «الباقات / إضافة ميزة»; the form in two columns — the Arabic and English names, the
  key and the group, the type and «الوحدة (للحدود فقط)», the specialty and the status, the switch
  «تُباع في الباقات» with its hint, the description; «إضافة الميزة» and «إلغاء».
- «Screen / AR·EN Feature Catalogue — Where It Is Used» (`2783:30449`, `2783:30129`) and «— Retire
  Feature Confirm» (`2783:31078`, `2783:30770`).

**Differences found** (for the owner at clarification):

- The form draws one «الوحدة (للحدود فقط)» field and one «الوصف» field; the BRD requires both in
  Arabic and English (§6-14, §13-ب). The BRD is followed: two units and two descriptions.
- «بدون هذه القدرة» (what a clinic gets when its plan leaves a limit out) is in the BRD's form and
  not in the frame. The BRD is followed; the field sits under the unit, for a limit only.
- The seed row «خريطة الأسنان وخطة العلاج» (`dental_chart`) joins two capabilities that OP-A-222
  requires apart («خطط العلاج» a feature separate from «خريطة الأسنان»). OP-A-222 is followed:
  «خريطة الأسنان» (`dental_chart`) and «خطط العلاج» (`treatment_plans`) are two rows.
- The filter panel is drawn as a button only; its panel and choices are not drawn (§7-12). The
  catalogue's filter follows the search-and-filter pattern of spec 095 with the table's own facets
  (group, type, status), each offering only values that have rows.

**Depends on**: 011-operators-roles (the permissions «عرض الباقات والفواتير» and «إدارة الباقات
والاشتراكات», the console's frame and side bar). Built on 007 (the audit trail) and 095 (the
components: table, toolbar and filter, form controls, row menu, confirmation, empty and error
states).

## Overview

A plan is a set of features from a catalogue the operators run; no feature should need a release
to exist (SH-010). Today the platform has no catalogue at all.

This spec delivers the catalogue, before plans exist:

- **The catalogue page**: four cards, a searchable and filterable table of every feature —
  its name and key, group, type, status and how many plans sell it — and, for the holders of
  «إدارة الباقات والاشتراكات», «+ إضافة ميزة» and each row's menu.
- **A feature**: a switch (included or not) or a limit (a number and its unit, in both
  languages), in one of seven groups, open to every specialty or to one, "built" or "upcoming",
  sold in plans or not, with what a clinic gets when its plan leaves the limit out.
- **Adding and editing a feature**, with the key fixed once made.
- **Retiring a feature**: it stays in the catalogue, and no plan that does not have it can take
  it; plans that have it keep it, and their clinics are untouched. A retired feature can be sold
  again.
- **«أين تُستعمل»**: the plans that include a feature, their state, and their clinics.
- **The seed**: the catalogue starts with the features the design draws (§7-19), so plans (013)
  have something to be made of.

What a plan includes, prices, and whether a clinic may use a feature are later specs: plans and
their grid (013), enforcement and the eight limits' counting (014), extra units' prices (021).

## Clarifications

### Session 2026-10-16

- Q: With what status do the seeded features start — «قادمة» for all until each is built, or as the design draws them (26 «مبنية»)? → A: Every seeded feature starts «قادمة»; the spec that builds a feature marks it «مبنية», so the catalogue never says a feature is built before it is, and an unbuilt feature never reaches the clinic's price table (SH-021).
- Q: Once a feature is retired with «إيقاف الميزة…», can an operator make it sellable again? → A: Yes: «إعادة بيع الميزة» in the retired feature's row menu (in place of «إيقاف الميزة…»), behind a confirmation, recorded in the audit trail; the feature is offered again to every plan. Not drawn — built in the manner of «إيقاف الميزة…» and listed for the designer.
- Q: The design draws «خريطة الأسنان وخطة العلاج» as one feature; OP-A-222 wants «خطط العلاج» apart. Which does the seed hold? → A: Two features, as OP-A-222 says: «خريطة الأسنان» · "Dental chart" (`dental_chart`, dental only) and «خطط العلاج» · "Treatment plans" (`treatment_plans`, every specialty, a switch in «السريرية» right after it) — 35 seeded features; the frame is listed for the designer.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An operator sees every feature a plan can be made of (Priority: P1)

An operator who may view plans opens the catalogue and sees every feature: what it is called in
both languages, its key, its group, whether it is a switch or a limit (and what the limit counts),
whether it is built yet, and how many plans sell it. They search by name or key and filter by
group, type or status.

**Why this priority**: plans are made of these features; nobody can build or check a plan without
seeing them.

**Independent Test**: as holders of «عرض الباقات والفواتير» with and without «إدارة الباقات
والاشتراكات», open the catalogue in Arabic and English; search and filter; page through it.

**Acceptance Scenarios**:

1. **Given** an operator holding «عرض الباقات والفواتير», **When** they open the console, **Then**
   the side bar shows «الباقات», and it opens the catalogue.
2. **Given** an operator without «عرض الباقات والفواتير», **When** they open the console or the
   catalogue's address, **Then** «الباقات» is not shown and the address says the page is not in
   their role, as every refused page does (spec 011).
3. **Given** the catalogue opens, **When** it draws, **Then** the four cards read the catalogue's
   own numbers — «إجمالي الميزات» with «<ن> مجموعات», «المبنية» with «تُباع اليوم», «قادمة» with
   «لم تُبنَ بعد», «مُباعة في الباقات» with «عبر <ن> باقات» — and the table lists features by group,
   then by their order in it, 13 a page, with «عرض <من>–<إلى> من <المجموع> ميزة».
4. **Given** each row, **When** it is read, **Then** «الميزة» shows the name with the key under it;
   «النوع» says «مفتاح», «حدّ · <الوحدة>», «مفتاح · <للتخصّص>» (as «مفتاح · للأسنان») or «يتبع
   التخصص»; «الحالة» is «مبنية» or «قادمة»; «في الباقات» is «<ن> باقات», «باقتان», «باقة واحدة»
   or «لا تُباع بعد».
5. **Given** a search, **When** the operator types part of a name (either language) or of a key,
   **Then** only matching rows remain, and the count says how many.
6. **Given** the filter, **When** the operator picks a group, a type or a status, **Then** only
   those rows remain, the Filter button carries the number of filters applied and a chip names
   each one with «مسح الكل», as the operators' list does (spec 011, spec 095).
7. **Given** an operator who may view but not manage, **When** the catalogue draws, **Then**
   there is no «+ إضافة ميزة»; each row's menu offers «أين تُستعمل» alone.
8. **Given** the catalogue cannot be read, **When** it is opened, **Then** it says «تعذّر تحميل
   الكتالوج.» with «حاول مرة أخرى» (the console's word, owner 16 Oct 2026); while it loads, it shows it is loading.

---

### User Story 2 - An operator adds a feature without a release (Priority: P1)

An operator who manages plans adds a capability to the catalogue: its names, its key, its group,
switch or limit, and the rest. It is in the catalogue at once, ready for a plan to include.

**Why this priority**: SH-010 — no feature is added by changing code.

**Independent Test**: add a switch, then a limit with its unit, then try each refusal.

**Acceptance Scenarios**:

1. **Given** an operator holding «إدارة الباقات والاشتراكات», **When** they press «+ إضافة ميزة»,
   **Then** the form «إضافة ميزة» opens with its line «قدرة يمكن أن تشملها باقة. تظهر في نموذج
   الباقة فورًا، وتحمل فيه وسم «قادمة» إلى أن تُبنى.», the group set to its first, the type to
   «مفتاح», the status to «قادمة», and «تُباع في الباقات» on.
2. **Given** the type is «مفتاح», **When** the form draws, **Then** the unit and «بدون هذه
   القدرة» are not shown; **and given** it is «حدّ», **Then** both units and «بدون هذه القدرة»
   appear.
3. **Given** a valid feature, **When** the operator presses «إضافة الميزة», **Then** it is saved,
   «أُضيفت الميزة.» is shown, the catalogue is open again with it in its group, last in that group's
   order, and the audit trail records it.
4. **Given** an empty name, **When** they press «إضافة الميزة», **Then** that field says «هذا
   الحقل مطلوب.» and nothing is saved.
5. **Given** a key that is not lower-case English letters, digits and underscores starting with a
   letter, or is shorter than 2 or longer than 64, **When** they press «إضافة الميزة», **Then** it
   says «حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.».
6. **Given** a limit without both units, **When** they press «إضافة الميزة», **Then** it says
   «الحدّ يحتاج وحدة بالعربية والإنجليزية: ماذا يعدّ؟».
7. **Given** a key already in the catalogue, **When** they press «إضافة الميزة», **Then** it is
   refused with «تعذّر حفظ الميزة.» and the form keeps what was typed.
8. **Given** an operator who may view only, **When** they open the add page by its address,
   **Then** it says the page is not in their role, and a direct request to add is refused.

---

### User Story 3 - An operator corrects a feature, never its key (Priority: P2)

An operator who manages plans edits a feature's names, group, type, unit, specialty, status,
sale or baseline. Its key cannot change: plans, the server and the audit trail name a feature by
its key.

**Why this priority**: names and statuses change as features are built; a changed key would
break every plan that uses it.

**Independent Test**: edit a feature's names and status; try to change its key by the form and by
a direct request.

**Acceptance Scenarios**:

1. **Given** a feature, **When** the operator picks «تعديل الميزة» in its row's menu, **Then**
   «تعديل ميزة» opens with its values and its key shown but not editable.
2. **Given** a change, **When** they press «حفظ التغييرات», **Then** it is saved, «حُفظت الميزة.»
   is shown, the catalogue is open again, and the audit trail records the values before and after.
3. **Given** a feature moved to another group, **When** it is saved, **Then** it goes last in that
   group's order.
4. **Given** a direct request that changes the key, puts a unit or a baseline on a switch, or makes
   a limit without both units, **When** it is sent, **Then** it is refused and nothing changes.
5. **Given** an address for a key the catalogue does not hold (an old or mistyped link), **When**
   the form opens or is saved, **Then** it says «لم تعد هذه الميزة موجودة.».

---

### User Story 4 - An operator sees where a feature is used, and retires it (Priority: P2)

An operator checks which plans include a feature, and how many clinics are on them, before
changing it. A feature that should no longer be sold is retired: it stays in the catalogue, new
plans cannot take it, and the plans and clinics that have it keep it.

**Why this priority**: retiring is the catalogue's only way out; it must never take a feature
away from a clinic that pays for it.

**Independent Test**: open «أين تُستعمل» for a feature; retire a sold feature, confirm, and check
the catalogue and the rows that remain.

**Acceptance Scenarios**:

1. **Given** a feature, **When** an operator who may view plans picks «أين تُستعمل», **Then** the
   dialog «أين تُستعمل ميزة <الاسم>» says «تشملها <ن> باقات، وعليها <ن> عيادة. ولكل باقة حدّها في
   «تعديل الباقة».» and lists each plan with its state («نشطة» / «معطلة») and «<ن> عيادة», a total
   row, and «إغلاق».
2. **Given** a feature that no plan includes, **When** «أين تُستعمل» opens, **Then** it says so
   plainly instead of an empty table.
3. **Given** a feature that is sold, **When** an operator who manages plans picks «إيقاف الميزة…»,
   **Then** the dialog «إيقاف ميزة <الاسم>؟» says «لن تستطيع الباقات الجديدة أن تشملها: تختفي من
   «إضافة باقة»، ومن «تعديل الباقة» للباقات التي لا تشملها.» and, when plans include it, a warning
   naming them and their clinics: «تحتفظ بها الباقتان <الباقة> و<الباقة> حتى تُعدَّلا وتُزال منهما،
   ولا تتأثر العيادات المشتركة فيهما (<ن> عيادة).»; the buttons «إيقاف الميزة» (red) and «إلغاء».
4. **Given** the operator confirms, **When** it is done, **Then** the feature stays in the catalogue,
   is marked retired, is no longer offered to a plan that does not include it, keeps its place
   (with its limit) in every plan that does, and the audit trail records it; a success message
   names the feature.
5. **Given** a feature that is not sold, **When** its row's menu opens, **Then** it offers no
   «إيقاف الميزة…».
6. **Given** retiring fails, **When** the server refuses, **Then** an error names the action and
   nothing changes.
7. **Given** a retired feature, **When** an operator who manages plans opens its row's menu,
   **Then** it offers «إعادة بيع الميزة» in place of «إيقاف الميزة…»; **and when** they confirm the
   dialog «إعادة بيع ميزة <الاسم>؟» («تعود الميزة إلى «إضافة باقة» و«تعديل الباقة» لكل باقة.»,
   buttons «إعادة بيع الميزة» and «إلغاء»), **Then** it is sold again, offered to every plan,
   unmarked as retired, «أُعيدت الميزة <الاسم> إلى البيع.» is shown, and the audit trail records it
   (clarified).

---

### User Story 5 - The catalogue starts with the features the design draws (Priority: P1)

The first time the platform runs, the catalogue holds the features the product is designed
around, in their groups, with their keys, types, units and specialties, so plans have something
to be made of.

**Why this priority**: an empty catalogue makes the plans spec (013) impossible to build or test.

**Independent Test**: start an empty platform and open the catalogue.

**Acceptance Scenarios**:

1. **Given** a platform started for the first time, **When** the catalogue is opened, **Then** it
   holds 35 features: the seed of §7-19 with «الحجوزات والمرضى والكشف والروشتة» and «وحدات الكشف بحسب
   التخصص», in the seven groups and their order, with «خريطة الأسنان» and «خطط العلاج» apart
   (OP-A-222).
2. **Given** the seed, **When** it is read, **Then** «الحجوزات والمرضى والكشف والروشتة» and «وحدات
   الكشف بحسب التخصص» are not sold in plans; «خريطة الأسنان» and «معمل الأسنان» follow «أسنان»;
   the four monthly-counted limits are «رصيد الرسائل», «تحليل الأشعة بالذكاء الاصطناعي», «كتابة
   الملاحظة بالصوت» and «دقائق الكشف عن بُعد»; the baselines are none for «المستخدمون», 1 for
   «الأطباء» and «الفروع», and 0 for the quotas (ق-٣٨).
3. **Given** the platform restarts, or a later release runs again, **When** the seed is applied,
   **Then** it adds nothing twice and changes no feature an operator has edited.
4. **Given** the seed's statuses, **When** the catalogue is read, **Then** every seeded feature is «قادمة», «المبنية» reads 0, and
   «قادمة» counts them all; the spec that later builds a feature marks it «مبنية» (clarified).

---

### Edge Cases

- **Two operators add the same key at once**: one is saved; the other is refused as a key already
  in the catalogue.
- **A feature retired while another operator edits it**: the edit saves its fields; it stays
  retired.
- **Retired, then sold again, then retired**: each step is allowed and recorded; plans that kept
  the feature while it was retired are unchanged throughout.
- **A limit edited into a switch** (or back): its unit and baseline are cleared on a switch and
  required on a limit; plans that include it are 013's to correct (ق-٤٢ refuses a number on a
  switch when a plan is saved).
- **A feature that follows a specialty, edited to "every specialty"**: allowed; it then follows
  the plan alone (enforcement is 014's).
- **Words in the other language**: every feature has both names; the console shows the one of its
  language, and searching matches either.
- **A very long name** (120 characters) wraps in its cell and is never cut; the key under it does
  not wrap.
- **No plans yet** (until 013): «مُباعة في الباقات» is 0 «عبر 0 باقات», every row says «لا تُباع
  بعد», «أين تُستعمل» says no plan includes it, and retiring shows no warning.
- **Digits**: every number is in Western digits in both languages (OP-B-386).
- **A phone's width**: the table scrolls inside its card, the toolbar wraps, and every control is
  at least 40 by 40 (spec 095).

## Requirements *(mandatory)*

### Functional Requirements

**Reaching the catalogue**

- **FR-001**: The side bar MUST show «الباقات» · "Plans" to operators holding «عرض الباقات
  والفواتير», and to nobody else; until the plans page exists (013), it MUST open the catalogue.
- **FR-002**: Reading the catalogue MUST need «عرض الباقات والفواتير»; adding, editing and retiring
  MUST need «إدارة الباقات والاشتراكات» (OP-A-210), on the server for every request (SH-056); a
  page the operator's role does not open MUST say so as spec 011's refused pages do.

**The catalogue**

- **FR-003**: The catalogue MUST show its title, its line, and the four cards with their numbers
  and lines (OP-A-211), counted from the catalogue as it is.
- **FR-004**: The table MUST list every feature — retired ones too — by group, then by order within
  the group, 13 a page, with the columns of §7-12 and the words of User Story 1 scenario 4
  (OP-A-211, OP-A-212).
- **FR-005**: Search MUST match part of either name or of the key, ignoring case.
- **FR-006**: The filter MUST offer the group, the type and the status, each with only the values
  that have rows, and MUST show its count on the Filter button and a chip for each applied filter
  with «مسح الكل» (spec 095's pattern).
- **FR-007**: The empty, loading and error states MUST be drawn and worded as §5-47 gives them.
- **FR-008**: A retired feature MUST be marked as retired in its row.
- **FR-008a**: Each screen of this spec MUST define its states (SH-097): loading, empty, error,
  view-only (User Story 1 scenario 7, User Story 2 scenario 8) and offline — the console's bar of
  spec 010, the catalogue keeping what was last loaded, and a save, a retire or a sell again sent
  while offline refused with its «تعذّر …» message and nothing changed. "Not included in the plan"
  does not apply: the operators console has no plan.

**A feature**

- **FR-009**: A feature MUST have: a key (2–64; lower-case English letters, digits and
  underscores, starting with a letter; unique; never changed once made — OP-A-214, ق-٤٦); a name
  in Arabic and in English (1–120 each), from which every screen names it; an optional description
  in each language (up to 500); a group of the seven (الأساس · القنوات · التقارير · المال ·
  السريرية · الذكاء الاصطناعي · النمو); a type, switch or limit; for a limit only, its unit in each
  language (up to 40) and its baseline (0–1,000,000, or none); a specialty — one of the fifteen,
  or every specialty; a status, «مبنية» or «قادمة» (default «قادمة»); whether it is sold in plans
  (default yes); its order within the group, computed (§3-6).
- **FR-010**: A limit MUST have both units; a switch MUST have neither unit nor baseline
  (OP-A-215); the server MUST refuse otherwise.
- **FR-011**: The order within a group MUST be computed — the group's last plus 10 — when a feature
  is added or moved to another group, and MUST NOT be asked of the operator (OP-A-216).
- **FR-012**: Whether a limit is counted monthly MUST follow from the fixed list of four
  (رصيد الرسائل · تحليل الأشعة · الكتابة الصوتيّة · دقائق الكشف عن بُعد) and MUST NOT be a field.
- **FR-013**: The fifteen specialties MUST be the fixed list, in its order: أسنان · عظام · مخ
  وأعصاب · أطفال · باطنة · جراحة عامة · جلدية · نساء وتوليد · عيون · أنف وأذن وحنجرة · مسالك بولية
  · قلب · طب نفسي · تغذية · علاج طبيعي (SH-013), with their English names.

**Adding and editing**

- **FR-014**: The form MUST follow §6-14 and the frames: two columns, the fields in the drawn
  order, the units and the baseline shown for a limit only, the key read-only when editing, with
  the hints and placeholders of §6-14 in both languages.
- **FR-015**: Checks MUST run when the form is sent and say what is wrong beside the field, with the
  words of §5-48; the server's refusals (a key in use, a changed key, a unit or baseline on a switch)
  MUST say «تعذّر حفظ الميزة.» at the top of the form, and a key in use MUST also be marked at the
  key field; a key that no longer exists (an old link) MUST say «لم تعد هذه الميزة موجودة.».
- **FR-016**: Saving MUST return to the catalogue with «أُضيفت الميزة.» or «حُفظت الميزة.».
- **FR-017**: Adding, editing, retiring and selling again MUST each write the audit trail with the
  values before and after (`feature_created`, `feature_updated`, `feature_retired`,
  `feature_resumed`; SH-092).

**Where it is used, and retiring**

- **FR-018**: «أين تُستعمل» MUST list the plans that include the feature, with their state and
  their number of clinics, and a total (OP-A-263); with none, it MUST say so.
- **FR-019**: «إيقاف الميزة…» MUST be offered for a sold feature only, last in the row's menu after
  a divider, in red, to holders of «إدارة الباقات والاشتراكات».
- **FR-020**: Retiring MUST pass a confirmation naming the feature and, when plans include it, the
  plans and their number of clinics (OP-A-220), with a button that names the action.
- **FR-021a**: A retired feature MUST be made sellable again by «إعادة بيع الميزة» in its row's menu,
  in place of «إيقاف الميزة…», to holders of «إدارة الباقات والاشتراكات», behind a confirmation
  that names the feature; it is then offered to every plan again and written to the audit trail
  (`feature_resumed`). Retiring and selling again MAY repeat (clarified).
- **FR-021**: A retired feature MUST stay in the catalogue; MUST NOT be offered to a new plan or to
  a plan that does not include it; MUST stay, with its limit and its unit price, in every plan that
  includes it until an operator removes it; and MUST NOT change anything for any clinic
  (OP-A-219, ق-٤٨).

**The seed**

- **FR-022**: The first run MUST fill the catalogue with the seed: the rows of §7-19 in their groups
  and drawn order, «الحجوزات والمرضى والكشف والروشتة» and «وحدات الكشف بحسب التخصص» (not sold),
  and «خطط العلاج» apart from «خريطة الأسنان» (OP-A-222) — 35 in all — with the keys the design
  shows and the units, specialties and baselines of §3-6, §7-15 and ق-٣٨. «خريطة الأسنان» follows
  «أسنان»; «خطط العلاج» is open to every specialty, a switch in «السريرية» right after it
  (clarified).
- **FR-023**: Applying the seed again MUST add nothing twice and MUST NOT change a feature an
  operator has added or edited.
- **FR-024**: Every seeded feature MUST start «قادمة»; a later spec that builds a feature MUST mark it
  «مبنية» as part of that spec (clarified 16 Oct 2026). An operator may still change a status by
  editing the feature.

**For later specs**

- **FR-025**: The catalogue MUST be the one place that says, for any key, whether the feature
  exists, what it is, its specialty and its baseline, so the plans (013) and their enforcement
  (014) read it and never repeat it (ق-٣٦ is 014's; this spec provides what it reads).
- **FR-026**: A key the catalogue does not hold MUST be reported as unknown, never as a feature
  (ق-٣٩ is enforced by 014).

### Key Entities

- **Feature**: a row of the catalogue — key, names and descriptions in both languages, group, type
  (switch or limit), units and baseline (limit only), specialty (one or every), status (built or
  upcoming), sold in plans, retired, order within its group; when it was made and changed, and by
  whom (through the audit trail).
- **Group**: the closed list of seven, with their order and names in both languages.
- **Specialty**: the closed list of fifteen, with their order and names in both languages.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator who manages plans adds a new feature to the catalogue in under two
  minutes, with no release, and it is in the catalogue at once.
- **SC-002**: Every refusal of User Stories 2 and 3 is said in the operator's language beside its
  field (a server's refusal at the top of the form, a key in use also at the key field), and none
  of them saves anything — checked for each refusal in Arabic and English.
- **SC-003**: No request without its permission reads or changes the catalogue: every route is
  refused without «عرض الباقات والفواتير», and every write without «إدارة الباقات والاشتراكات».
- **SC-004**: A retired feature keeps every plan and every clinic that has it unchanged: after
  retiring, what each plan includes and each clinic may use is the same as before (checked again
  by 013 and 014 when plans and clinics exist).
- **SC-005**: Searching the seeded catalogue for part of any feature's name or key finds it, in
  either language, on the first try.
- **SC-006**: Starting the platform twice leaves exactly one copy of each seeded feature, and an
  operator's edits to a seeded feature survive a new release.
- **SC-007**: The catalogue, the form and both dialogs look as their frames draw them, in Arabic
  and English, at 1440 and 390 — laid beside the frames and every picture opened.

## Assumptions

- **Where the catalogue lives until plans exist.** The BRD puts «كتالوج الميزات» as a button beside
  «إضافة باقة +» on the plans page (§5-47). The plans page is 013's; until then the side bar's
  «الباقات» opens the catalogue, and 013 moves the entry to its button. The breadcrumb of the form
  is «الباقات / إضافة ميزة» as drawn.
- **No plans yet.** Every count of plans and clinics is 0 until 013: «لا تُباع بعد» in every row,
  0 «عبر 0 باقات», no warning when retiring.
- **The seed's keys** are the design's where it shows them (`max_users`, `max_doctors`,
  `allow_whatsapp`, `dental_chart`, `ai_xray_analysis`, `specialty_exam_modules`, `teleconsult`,
  `teleconsult_minutes`, `deposit_policy`, `inventory`, `sick_leave_referral`, `patient_feedback`,
  `lab_orders`) and the behaviour reference's for the rest (`core_clinic`, `max_branches`,
  `allow_telegram`, `bot_assistant`, `message_credits`, `allow_analytics`, `allow_custom_branding`,
  `allow_insurance`, `allow_refunds`, `itemized_invoice`, `doctor_statement`, `dental_lab`,
  `drug_directory`, `informed_consent`, `licences_registry`, `ai_voice_notes`, `reminders_confirm`,
  `queue_screen`, `public_booking`, `recall_list`, `weekly_summary`), plus `treatment_plans` for
  OP-A-222. The English names follow the design's English frame.
- **The specialties' English names** follow the behaviour reference: Dental, Orthopedics,
  Neurology, Pediatrics, Internal medicine, General surgery, Dermatology, Obstetrics & gynaecology,
  Ophthalmology, ENT, Urology, Cardiology, Psychiatry, Nutrition, Physiotherapy.
- **A retired feature is sold by definition**: «تُباع في الباقات» cannot be turned off on it — sell
  it again first, or leave it retired (the plan's decision, research R6).
- **Filtering** uses the facets the table has (group, type, status); the frame draws only the
  button (§13-ب).
- **The success message after retiring and its error** are not drawn; they follow the console's
  toasts: «أُوقفت الميزة <الاسم>.» and «تعذّر إيقاف الميزة.». «إعادة بيع الميزة», its dialog and its
  messages («أُعيدت الميزة <الاسم> إلى البيع.» · «تعذّر إعادة بيع الميزة.») are not drawn either
  (clarified) and are listed for the designer.
- **Out of scope**, each with the spec that owns it: plans, their grid and what they include
  (013); unit prices of the monthly limits (021); whether a clinic may use a feature, the counting
  of the eight limits and the refusal codes (014, OP-A-221); the clinic's specialty (015); the price
  table a clinic sees (SH-021, 079 and onward); the «⋯» visibility for view-only holders beyond
  «أين تُستعمل» (§13-ب, settled here as above).
