# Feature Specification: Clinic Onboarding

**Feature Branch**: `015-clinic-onboarding`

**Created**: 2026-10-17

**Status**: Draft

**Input**: User description: empty. Taken from row `015-clinic-onboarding` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1): "the clinic's data, specialty and plan,
inviting its owner, the main branch, assigning the account manager, the «الإعدادات» tab as a
read-only mirror of the clinic's settings." Ops module 16. Depends on 013 and 004.

**BRD source**: Ops volume, module 16 (`16-ops-clinics-plans-billing.md`):

- **Scope and permissions**: §1 (routes `/clinics`, `/clinics/new`, `/clinics/:id`,
  `/clinics/:id/edit`; the detail's four tabs), §2-2 («عرض العيادات» `clinics.view`, «تعديل بيانات
  العيادات» `clinics.edit`), §2-3 (a control the operator may not use is removed), §2-4 (view only).
- **Data**:
  - §3-1, the clinic: name, slug, sign-in address, specialty, address, tax number, plan,
    trial end, account manager, default language, time zone;
  - §3-2, the owner: name, email, mobile, role «مالك العيادة»;
  - §3-3, the invitation, 72 hours;
  - §3-4, the main branch.
- **States**: §4-1 (only «تجربة» and «نشطة» here; the rest are 016's), §4-7 (the owner's
  invitation row).
- **Flows**:
  - §5-1, the list: open, search, filter (export is 023's);
  - §5-2, adding a clinic;
  - §5-3, the detail;
  - §5-4, editing without changing the plan;
  - §5-17, the account manager;
  - §5-18, the sign-in address and re-sending the owner's invitation;
  - §5-42, the «الإعدادات» tab.
- **Forms and screens**: §6-1 (the add and edit form), §6-15 (the account manager), §7-1 (the
  list), §7-2 (the header; only «تعديل العيادة» here), §7-3 (the «معلومات العيادة» card).
- **Requirements**: OP-A-001 … OP-A-046 where they concern the above. Lifecycle (OP-A-060 …
  OP-A-094), users (OP-A-095 …), plan change (OP-A-050 … OP-A-059) and billing are later specs'.
- **Shared**: SH-003 (replaced by the constitution's addresses), SH-013 (the fifteen
  specialties), SH-044 (handing an account over by emailed link), SH-052 (the owner's role),
  SH-055, SH-056, SH-092 (the audit trail), SH-097, SH-098.

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY`, page «Template — Clinics» (`591:2`), read
on 17 Oct 2026:

- **«Screen / AR Clinics List»** (`606:776`) / EN (`599:680`):
  - the title «العيادات» and the line «إدارة كل العيادات على المنصة.»;
  - the buttons «إرسال إشعار» and «إضافة عيادة +»;
  - four cards: «إجمالي العيادات» (+8 هذا الشهر), «العيادات النشطة» (83% من الإجمالي), «عيادات
    التجربة» (قاربت على الانتهاء), «تحتاج متابعة» (4 متأخرة · 3 قراءة فقط · 3 موقوفة);
  - search «ابحث عن عيادة بالاسم أو جهة الاتصال...», «فلترة» and «تصدير إلى إكسل»;
  - the columns: a check box, «العيادة» (initial, name, «أضيفت <date> · <time>»), «أضيف
    بواسطة», «الباقة», «الحالة», «مدة الاشتراك», «ينتهي في ▾», «الإجراءات»;
  - the row menu «عرض التفاصيل» · «تعديل العيادة» · «تجديد الاشتراك» · «إرسال إشعار» · «إيقاف
    مؤقت» (red);
  - «عرض 1–8 من 94 عيادة».
- **List states**: «— Loading» (`607:120` / `602:23`), «— Error» (`608:168` / `603:226`),
  «— Empty» (`712:611` / `706:602`), and the filter's status options (`2313:2714` / `2313:2673`).
- **«Screen / AR Add Clinic»** (`655:1140`) / EN (`649:1040`):
  - the breadcrumb «العيادات / إضافة عيادة جديدة»;
  - the title «إضافة عيادة جديدة» and the line «املأ التفاصيل أدناه لإضافة عيادة جديدة إلى
    المنصة.»;
  - the fields, two to a row: «اسم العيادة *» · «جهة الاتصال *», «البريد الإلكتروني *» · «رقم
    الهاتف *», «العنوان *» (full width), «الباقة» («اختر باقة») · «رقم التسجيل الضريبي
    (اختياري)» (hint «9 أرقام، مثال 123-456-789»), «التخصص *» («أسنان») with its hint «يحدد وحدات
    الكشف والشاشات السريرية التي تراها العيادة. لا تستطيع العيادة تغييره.»;
  - «إضافة العيادة» and «إلغاء».
- **«Screen / AR Edit Clinic»** (`655:1230`) / EN (`649:1124`), and the invalid-tax-number states
  (`2263:2467`, `2263:2661` and their EN twins).
- **«Screen / AR Clinic Detail»** (`632:956`) / EN (`613:872`):
  - the breadcrumb «العيادات / <name>»;
  - the initials, the name, the state badge, and the line «جهة الاتصال: <name> · تاريخ الانضمام
    <date> · باقة <plan>»;
  - «تعديل العيادة»;
  - the tabs «نظرة عامة» · «الدفعات» · «التكاملات» · «الإعدادات»;
  - the «معلومات العيادة» card: «البريد الإلكتروني», «الهاتف», «العنوان», «رقم التسجيل الضريبي»,
    «الباقة», «دورة الفوترة», «التجديد القادم», «المسؤول من SCS» with «تعيين», «عدد الأطباء /
    الفروع», «آخر تسجيل دخول».
- **Detail states**: «— Loading» (`636:992`), «— Error» (`637:1012`), «— View Only»
  (`2786:26469`), and the sign-in address row in its three states (`2330:3095` / `2330:3050`).
- **«Screen / AR Clinic Detail — Settings (read-only mirror)»** (`2589:4185`) / EN (`2589:4007`):
  - the read-only strip;
  - the «الإعدادات العامة» card with «تعديل العيادة ←»: «اللغة الافتراضية», «المنطقة الزمنية
    (القاهرة (GMT+2))», «عنوان دخول الموظفين»;
  - the nine cards of what the clinic set.
  - Its view-only twin: `2790:8210` / `2790:7655`.
- **Drawn but later specs'**: payments, integrations, users, extra quota, renew, suspend and
  expire, plan change and downgrade, the notification, the other overview cards («أحدث التذاكر»,
  «النشاط الأخير», «ميزات الباقة», «التخصصات», the five figures), and the settings tab's writes.

## Overview

Specs 012–014 give the platform a catalogue, plans, and an enforcement point that reads a
clinic's plan. No operator can yet create a clinic: clinics exist only as seed data, with no
plan, no owner and no specialty.

This spec gives the operators console its «العيادات» section:

- **The list**: cards, search, a filter, 8 a page.
- **«إضافة عيادة»**: one save creates all of it.
  - The clinic, with its slug, specialty, address, tax number and language.
  - Its plan (through 014's link) and its trial.
  - Its main branch.
  - Its owner, invited by email with the owner's role.
- **The detail**: its header and its «معلومات العيادة» card. The account manager is assigned
  there, and the owner's invitation is re-sent from the sign-in address row.
- **«تعديل العيادة»**: changes the clinic's data and its owner's details. Changing the plan is
  spec 020's.
- **The «الإعدادات» tab**: read only. Its strip and the «الإعدادات العامة» card come now; the
  other cards come with the specs that make those settings.

It also replaces 013's "no clinics" answer, so the plans' screens count real clinics.

## Clarifications

### Session 2026-10-17

- Q: Which screens does 015 build? → A: The list, «إضافة عيادة», the detail's header and
  «معلومات العيادة» card (account manager, sign-in address with «إعادة إرسال الدعوة»), «تعديل
  العيادة» without changing the plan, and the «الإعدادات» tab. The other tabs and cards come with
  their specs: 016 lifecycle, 017 users, 018–021 billing and extra units, 061 integrations, 078
  and 014's read for the plan's features.
- Q: What does «عنوان دخول العيادة» show? → A: The one staff address for every clinic,
  `app.scs-heta.com` (constitution, Technology & Platform Constraints), with its copy button and
  the invitation row as drawn. The clinic's slug is still derived and kept, for its public
  booking page `scs-heta.com/<slug>`. The BRD's «<slug>.app.<domain>» and the frames'
  «noor.app.smartclinicos.com» are recorded for the owner and the designer.
- Q: What does 015 store about the trial and the status? → A: The trial's end, at creation: now
  plus a trial length read from deployment configuration until spec 024 makes it a platform
  setting. The badge shows «تجربة» until the trial ends and «نشطة» after. Spec 016 adds the
  other statuses and their changes.
- Q: Where does the main branch go? → A: Into a minimal clinic-owned branch table, the main
  branch only: name and address copied from the clinic, one main branch per clinic. Spec 028 adds
  the rest.
- Q: Once the owner has signed in, what happens when an operator changes the owner's email in
  «تعديل العيادة»? → A: It changes at once and the new email signs in. The owner's open sessions
  end, a notice goes to the old address, and the trail keeps both addresses.
- Q: Until 018 adds billing cycles, how much does a clinic past its trial add to a plan's monthly
  revenue? → A: Nothing. A clinic with no billing cycle counts 0, like one in trial, so the
  plans' revenue reads 0 rather than "no clinics" until 018.
- Q: Should «إضافة عيادة» ask for the clinic's name once, as drawn, or in Arabic and English? →
  A: Two fields: «اسم العيادة (عربي) *» and «اسم العيادة (إنجليزي)». The English one is optional
  and takes the Arabic when empty. The slug comes from the English name, else the Arabic. Beyond
  the frame; the designer is told.
- Q: What default trial length does a new clinic get until 024 makes it a platform setting? →
  A: 14 days. A deployment may set another number; a clinic keeps the end it was created with.
- Q: Which message shows when the owner's mobile is not an Egyptian mobile of 11 digits starting
  01? → A: No Egyptian format is required, because an owner may not be Egyptian. Any phone number
  is accepted (an optional «+», then 8 to 15 digits), and a malformed one shows «رقم الموبايل غير
  صحيح.». This departs from the BRD's Egyptian-mobile rule (§3-2) and is recorded for the owner.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An operator adds a clinic and its owner is invited (Priority: P1)

An operator who may edit clinics fills «إضافة عيادة جديدة»: the name, the contact (the owner),
email, mobile, address, plan, tax number and specialty. They press «إضافة العيادة». In one save
the platform does all of this:

- it creates the clinic with a unique slug, the specialty, the plan and a trial;
- it creates the clinic's main branch;
- it creates the owner's account, invited, with the role «مالك العيادة»;
- it emails the owner an invitation that works for 72 hours.

The operator lands on the clinic's detail with «تم إنشاء العيادة!».

**Why this priority**: Every later spec of the operators console, and every clinic the product
serves, starts here. Today a clinic can only be made by seeding the database.

**Independent Test**: Add a clinic with every field, then check:

- the clinic, its slug, plan, specialty and trial end;
- its main branch;
- its owner, invited and holding «مالك العيادة»;
- the email in the mailbox;
- the trail entries;
- the detail it opens on.
Then try each refusal.

**Acceptance Scenarios**:

1. **Given** an operator holding «تعديل بيانات العيادات» and at least one active plan, **When**
   they fill the form and press «إضافة العيادة» («جارٍ الإضافة…»), **Then**:
   - the clinic exists with its name, address, tax number (kept as `NNN-NNN-NNN`), specialty, the
     chosen plan, default language Arabic, and a trial that ends after the configured trial
     length;
   - its main branch carries the clinic's name and address;
   - the owner's account exists, invited, with no usable password and holding «مالك العيادة»;
   - an invitation reaches the owner's email in the clinic's language. It works for 72 hours and
     names the staff sign-in address `app.scs-heta.com`;
   - «تم إنشاء العيادة!» shows and the detail opens. Its badge reads «تجربة» and its sign-in
     address row «دعوة معلّقة».
2. **Given** names whose Latin letters and digits make a slug already taken, **When** the clinic
   is added, **Then** its slug takes `-2`, then `-3`, and so on. The slug comes from the English
   name, or from the Arabic one when the English is left empty. A name with no Latin letter or
   digit takes `clinic-<six characters>`. An English name left empty takes the Arabic one, which
   English screens then show.
3. **Given** a required field left empty, **When** «إضافة العيادة» is pressed, **Then**:
   - «هذا الحقل مطلوب.» shows under each such field;
   - a tax number that is not nine digits shows «أدخل رقم التسجيل الضريبي المكوّن من 9 أرقام.»;
   - a mobile that is not a phone number (an optional leading «+», then 8 to 15 digits; spaces
     and dashes allowed) shows «رقم الموبايل غير صحيح.». A number from any country is accepted;
   - nothing is created.
4. **Given** the email already holds an account on the platform, **When** the clinic is added,
   **Then** the email field says so, and nothing is created: no clinic, branch or account.
5. **Given** the chosen plan was disabled between opening the form and saving, **When** the
   clinic is added, **Then** «تعذر إنشاء العيادة.» shows and nothing is created.
6. **Given** the invitation email cannot be sent, **When** the clinic is added, **Then** the
   clinic is still created, and the detail's sign-in address row offers «إعادة إرسال الدعوة»
   (OP-A-026).
7. **Given** no active plan exists, **When** the operator opens «إضافة عيادة», **Then** the
   «الباقة» list is empty and the clinic cannot be saved without one.
8. **Given** an operator who holds «عرض العيادات» only, **When** they look at the list, **Then**
   there is no «إضافة عيادة +». `/clinics/new` typed by hand does not open the form, and the
   server refuses the save.
9. **Given** the clinic is created, **When** the trails are read, **Then** `clinic.created` is in
   the platform's trail and the clinic's, naming the operator. The platform's names the clinic
   and its slug. The plan's own `subscription.plan-set` is there too.

---

### User Story 2 - An operator finds a clinic in the list (Priority: P1)

The «العيادات» section lists every clinic, newest first, 8 a page. Above the list are four cards.
The list can be searched by the clinic's name, the owner's name, email or mobile, and filtered by
status, plan, subscription length and "ending within". A row opens the clinic's detail.

**Why this priority**: The list is how an operator reaches a clinic. Every later action starts
from a row.

**Independent Test**: With clinics on different plans, some in trial and some past it, open the
list. Read the cards, search each way, filter by each facet, page through, and open a row.

**Acceptance Scenarios**:

1. **Given** clinics exist, **When** an operator with «عرض العيادات» opens «العيادات», **Then**:
   - the cards read «إجمالي العيادات» (with «+<n> هذا الشهر»), «العيادات النشطة» (with «<n>% من
     الإجمالي»), «عيادات التجربة», and «تحتاج متابعة» with 0 for each part until 016 adds those
     statuses;
   - the five counts always add up to the total;
   - the table shows each clinic's initial, name, «أضيفت <date> · <time>», «أضيف بواسطة», the plan,
     the badge, «مدة الاشتراك» («تجربة <n> يوم» in trial, «—» otherwise until 018), and «ينتهي في»
     (the trial's remaining days, «خلال <n> أيام»);
   - newest first, 8 a page, «عرض 1–8 من <n> عيادة».
2. **Given** the search «ابحث عن عيادة بالاسم أو جهة الاتصال...», **When** part of a clinic's
   name, the owner's name, email or mobile is typed, in any case, **Then** after a short pause the
   list narrows to the matches and returns to page 1.
3. **Given** «فلترة», **When** statuses, a plan, a length or "ending within" (7 / 14 / 30 days) are
   chosen and «تطبيق الفلاتر» is pressed, **Then**:
   - the list narrows;
   - the applied chips show in a row under the toolbar with «مسح الكل»;
   - «إعادة تعيين» clears the panel;
   - the panel offers only what the clinics hold after the search, and keeps a chosen plan shown
     so it can be cleared.
4. **Given** «ينتهي في ▾», **When** it is pressed, **Then** the list sorts by it ascending, then
   descending. A clinic with no date comes last both ways, and ties stay newest first.
5. **Given** no clinic at all, **When** the list opens, **Then**:
   - «لا توجد عيادات» / «جرب تعديل البحث أو الفلاتر، أو أضف عيادة جديدة للبدء.» shows, with
     «إضافة عيادة» for an operator who may add;
   - the cards read 0 with «لا توجد بيانات».
   A search or filter that matches nothing shows «لا توجد عيادات مطابقة للبحث أو الفلاتر
   المستخدمة.» with «مسح الفلاتر».
6. **Given** the list cannot be read, **When** it opens, **Then** «حدث خطأ ما» / «تعذر تحميل
   قائمة العيادات. تحقق من اتصالك وحاول مرة أخرى.» shows with «إعادة المحاولة», and the cards show
   dashes. While it loads, skeletons stand in.
7. **Given** a row, **When** it is clicked, **Then** the clinic's detail opens. Its «⋯» offers
   «تعديل العيادة» to a holder of «تعديل بيانات العيادات». A row with nothing but opening has no
   «⋯» (the BRD's rule 37).
8. **Given** an operator without «عرض العيادات», **When** the console is open, **Then** there is
   no «العيادات» in the side bar, and `/clinics` typed by hand does not open.

---

### User Story 3 - An operator reads a clinic and keeps its account manager (Priority: P2)

A clinic's detail has a header and the «معلومات العيادة» card. The header shows the initials,
name, badge, contact, join date, plan and «تعديل العيادة». The card shows the email, mobile,
address, tax number, plan, billing cycle, next renewal, sign-in address, account manager, doctors
and branches, and last sign-in. An operator who may edit clinics assigns the account manager
there, and re-sends the owner's invitation from the sign-in address row.

**Why this priority**: The detail is where the clinic is read. The account manager decides who
is told about the clinic later (093, 016), and the invitation row is how an owner who missed the
email gets in.

**Independent Test**: Open a new clinic's detail and read each row. Assign an account manager,
then «غير معيّن». Copy the address. Re-send the invitation, try again within a minute, and
re-send after the owner has accepted.

**Acceptance Scenarios**:

1. **Given** a clinic, **When** its detail opens, **Then** the header and the card show the rows
   above:
   - «دورة الفوترة» «—»;
   - «التجديد القادم» the trial's end;
   - «عدد الأطباء / الفروع» «0 أطباء · 1 فرع»;
   - «آخر تسجيل دخول» «لم يسجّل الدخول بعد» until the owner signs in.
   The breadcrumb reads «العيادات / <name>».
2. **Given** the tabs, **When** the detail is open, **Then** «نظرة عامة» and «الإعدادات» are
   offered. «الدفعات» and «التكاملات» come with their specs.
3. **Given** «تعيين» beside «المسؤول من SCS», **When** a holder of «تعديل بيانات العيادات» presses
   it, **Then**:
   - the row becomes a list of the active operators by name, with «غير معيّن»;
   - «حفظ» («جاري الحفظ...») saves, shows «تم تحديث مسؤول الحساب.» and names the operator;
   - `clinic.account-manager-assigned` is written to both trails;
   - «إلغاء» leaves it as it was.
4. **Given** an operator suspended between opening the list and saving, **When** they are chosen,
   **Then** «تعذر تحديث مسؤول الحساب. تُعاد المحاولة بعد قليل.» shows and nothing changes.
5. **Given** the sign-in address row, **When** «نسخ عنوان دخول العيادة» is pressed, **Then**
   `app.scs-heta.com` is copied with «تم نسخ عنوان دخول العيادة.». A browser that refuses the copy
   shows «تعذر النسخ. حدّد الرابط وانسخه يدويًا.».
6. **Given** the owner is still invited, **When** the row is read, **Then** it shows one of three
   states (§4-7), each with «إعادة إرسال الدعوة» for a holder of «تعديل بيانات العيادات»:
   - «تنتهي صلاحية الدعوة في <date> — <relative>.» while the invitation lives;
   - «انتهت صلاحيتها في <date>. أرسل دعوة جديدة صالحة 72 ساعة.» once it has lapsed;
   - «لا توجد دعوة سارية. أرسل واحدة.» when none was ever sent.
7. **Given** «إعادة إرسال الدعوة», **When** it is pressed («جارٍ الإرسال…»), **Then**:
   - the earlier link stops working and a new 72-hour link is emailed;
   - «تم إرسال الدعوة مرة أخرى — ينتهي الرابط الجديد في <date>.» shows;
   - `clinic.invitation-resent` is written.
   It is refused in three cases:
   - within a minute of the last: «أُرسلت دعوة لهذا البريد قبل أقل من دقيقة. يمكن إعادة الإرسال بعد قليل.»;
   - an email that did not leave: «تعذر إرسال البريد، فلم يصل شيء إلى المالك. تُراجع إعدادات البريد ثم تُعاد المحاولة.», never shown as success;
   - an owner who has accepted: «مالك هذه العيادة قَبِل دعوته بالفعل.».
8. **Given** the owner has signed in, **When** the row is read, **Then** it shows the address and
   its copy button only.
9. **Given** an operator who holds «عرض العيادات» only, **When** the detail opens, **Then**:
   - there is no «تعديل العيادة», no «تعيين» and no «إعادة إرسال الدعوة»;
   - everything else reads the same.
10. **Given** a clinic id that does not exist, **When** `/clinics/<id>` is opened, **Then** the
    error state shows, and nothing says whether the clinic ever existed. «حدث خطأ ما» / «تعذر
    تحميل بيانات العيادة. تحقق من اتصالك وحاول مرة أخرى.» shows with «إعادة المحاولة» when it
    cannot be read.
11. **Given** an operator who is the account manager of some clinics, **When** that operator's
    page (spec 011) is opened, **Then** its «الأداء (آخر 30 يومًا)» card shows «عيادات تم إدارتها»
    with the number of visible clinics whose account manager the operator is now (spec 011's
    clarification: the figure comes with 015).

---

### User Story 4 - An operator corrects a clinic's data (Priority: P2)

«تعديل العيادة» opens the clinic's data and its owner's details. The operator changes them, adds
an optional note and presses «حفظ التغييرات». The clinic and its owner's account are updated
together, and each field that changed is recorded with its value before and after.

**Why this priority**: Clinics move, change numbers and get a tax number. Without this the only
fix is the database.

**Independent Test**: Open «تعديل العيادة» for a clinic. Change its address, tax number,
specialty, language and the owner's mobile and email, save, and read the detail and the trail.
Save with nothing changed. Clear the tax number. Try an invalid value.

**Acceptance Scenarios**:

1. **Given** a holder of «تعديل بيانات العيادات», **When** they open «تعديل العيادة» («تحديث بيانات
   <name>.»), **Then**:
   - the form opens filled;
   - it adds «اللغة الافتراضية» («العربية» / «الإنجليزية») and «المنطقة الزمنية» («القاهرة
     (GMT+2)», shown, never chosen);
   - it adds the optional note («مثال: بناءً على طلب العيادة لتحديث العنوان», at most 500);
   - the plan is shown and cannot be changed here (spec 020).
2. **Given** changes, **When** «حفظ التغييرات» («جارٍ الحفظ…») is pressed, **Then**:
   - the clinic and its owner's account are updated;
   - «تم تحديث بيانات العيادة.» shows and the detail opens;
   - `clinic.updated` holds each changed field of the clinic with its value before and after, and
     the note; `clinic.owner-updated` does the same for the owner's name, email and mobile.
3. **Given** nothing changed, **When** it is saved, **Then** nothing is recorded.
4. **Given** the tax number field cleared on purpose, **When** it is saved, **Then** the stored
   tax number is removed. A tax number stored before the nine-digit rule is accepted unchanged.
5. **Given** the specialty, **When** it is changed, **Then** the change is recorded. «غير مسجّل»
   is offered in editing only (OP-A-020, OP-A-044).
6. **Given** the owner's email is changed while the owner is still invited, **When** it is saved,
   **Then** the account's email changes, and the sign-in address row offers «إعادة إرسال الدعوة».
   The earlier invitation link stops working.
   **Given** the owner has already signed in, **When** their email is changed and saved, **Then**:
   - the new email signs in at once, and the old one no longer does;
   - every open session of the owner ends;
   - a notice in the clinic's language goes to the old address, saying the sign-in email was
     changed by the platform's team, and naming the new address only in part;
   - `clinic.updated` keeps the old and new addresses.
7. **Given** an empty required field or an invalid tax number or mobile, **When** it is saved,
   **Then** the same words as in adding show under the field and nothing changes. A save that
   fails shows «تعذر تحديث بيانات العيادة.».
8. **Given** an operator without «تعديل بيانات العيادات», **When** `/clinics/<id>/edit` is opened
   by hand, **Then** the form does not open and the server refuses the save.

---

### User Story 5 - An operator reads the clinic's settings (Priority: P3)

The «الإعدادات» tab says plainly that these are the clinic's own settings, read only. It shows
the «الإعدادات العامة» card: «اللغة الافتراضية», «المنطقة الزمنية» and «عنوان دخول الموظفين», with
«تعديل العيادة ←» for a holder of «تعديل بيانات العيادات». The other cards come with the specs that
make those settings.

**Why this priority**: An operator answers a clinic's questions from it. Today most of what it
mirrors does not exist yet.

**Independent Test**: Open the tab as a holder of «تعديل بيانات العيادات» and as a viewer, and
compare the strip, the card and the link.

**Acceptance Scenarios**:

1. **Given** a holder of «تعديل بيانات العيادات», **When** «الإعدادات» opens, **Then** the strip
   reads «للقراءة فقط: هذه إعدادات تضبطها العيادة من تطبيقها. يستطيع من يملك صلاحية «تعديل بيانات
   العيادات» تغيير اللغة (من «تعديل العيادة») …» and the card shows the three rows and «تعديل
   العيادة ←». The strip does not say the time zone can be changed (the BRD's own correction).
2. **Given** an operator who holds «عرض العيادات» only, **When** the tab opens, **Then** the strip
   reads the viewer's words («… يحتاج إلى صلاحية «تعديل بيانات العيادات».») and there is no
   «تعديل العيادة ←».
3. **Given** the tab cannot be read, **When** it opens, **Then** «تعذر تحميل إعدادات العيادة.
   تحقق من اتصالك وحاول مرة أخرى.» shows with «إعادة المحاولة».

---

### User Story 6 - The plans count their clinics (Priority: P3)

013's plan screens said "no clinics" until this spec. Now they count each plan's clinics, the new
ones this month, the ones on trial and the monthly revenue, and list a plan's clinics with their
state.

**Why this priority**: 013 built those figures and left them empty. A plan cannot be judged
without them.

**Independent Test**: Put clinics on two plans, some in trial, and read the plans list and a
plan's detail.

**Acceptance Scenarios**:

1. **Given** clinics on a plan, **When** the plans list or a plan's detail is read, **Then**:
   - the plan's clinic count, «جديدة هذا الشهر» and «على التجربة» match the clinics;
   - its revenue is 0: no clinic in trial counts (OP-A-189), and no clinic past its trial counts
     until spec 018 gives it a billing cycle;
   - the detail lists the plan's clinics with their state and join date.
2. **Given** a plan with clinics, **When** an operator tries to disable it, **Then** 013's dialog
   warns with the real number.

---

### Edge Cases

- **Two clinics added at once with names giving the same slug**: one takes the slug, the other
  `-2`. Both are created.
- **The same owner email typed for two clinics at once**: one is created; the other is refused
  on the email field, and nothing of it is created.
- **A clinic whose plan is later disabled**: it keeps the plan (013's ق-٤٧). The edit form shows
  it, unchangeable here.
- **The trial length in configuration changes**: clinics already created keep their trial end.
- **An account manager who is later suspended**: the clinic keeps the name until someone assigns
  another. The list offers only active operators.
- **The seeded clinics** (noor, shifa) have no plan, specialty or address, and their seeded owner
  account has no name or mobile. The detail shows «—» or «غير مسجّل» for what they lack. The edit
  form lets an operator fill them in, and requires the owner's name and mobile on the first save.
  A clinic with no owner account at all (none today) shows no owner fields in the edit.
- **A read-only clinic**: no such state exists before spec 016, so this spec has no read-only
  refusal. 016 adds it with the state, and refuses this spec's writes for such a clinic.
- **The add fails part-way** (a database or network fault after every check has passed): the
  clinic never becomes visible: it is in no list, count or figure, it cannot be opened, and the
  owner's email is free again. The operator sees «تعذر إنشاء العيادة.».
- **The users limit of the chosen plan is 0** (an operator's choice): the owner's account takes a
  seat, so the add is refused with the plan's limit message, and nothing is created.
- **An operator acts in a clinic**: every write goes through the clinic's own isolation (its
  context is established for the request), never around it.

## Requirements *(mandatory)*

### Functional Requirements

**Access**

- **FR-001**: The «العيادات» section, the list and the detail MUST open for holders of «عرض العيادات»
  (`clinics.view`). «إضافة عيادة», «تعديل العيادة», «تعيين» and «إعادة إرسال الدعوة» MUST be for
  holders of «تعديل بيانات العيادات» (`clinics.edit`). A control a person may not use MUST be
  removed, not disabled, and the server MUST refuse it (SH-055, SH-056).

**Adding a clinic**

- **FR-002**: The add form MUST hold, in the drawn order:
  - «اسم العيادة (عربي) *» and «اسم العيادة (إنجليزي)», side by side (beyond the frame, which
    draws one field); the English one is optional and takes the Arabic when empty;
  - «جهة الاتصال *» (the owner's name);
  - «البريد الإلكتروني *»;
  - «رقم الموبايل *», drawn «رقم الهاتف» and corrected by the BRD's rule 44;
  - «العنوان *»;
  - «الباقة *», the active plans cheapest first, placeholder «اختر باقة»;
  - «رقم التسجيل الضريبي (اختياري)»;
  - «التخصص *», the fifteen specialties in the BRD's order, placeholder «اختر التخصص».
  The button is «إضافة العيادة», with «إلغاء».
- **FR-003**: The form MUST be checked when sent:
  - required fields: «هذا الحقل مطلوب.»;
  - the tax number: nine digits with or without dashes, kept as `NNN-NNN-NNN`;
  - the mobile: a phone number from any country: an optional leading «+», then 8 to 15 digits,
    spaces and dashes ignored; kept as typed without the spaces and dashes;
  - the email: its format, and not already an account anywhere on the platform (spec 003's rule:
    one email, one account);
  - the plan: active.
  A refused add MUST create nothing.
- **FR-004**: One add MUST be all or nothing as anyone sees it. Every check of FR-003, the plan's
  users limit included, MUST pass before anything is written; the clinic MUST become visible only
  once all of these are done, and never half-made:
  - create the clinic with both names, and a unique slug derived from the Latin letters and
    digits of the English name (else the Arabic), or
    `clinic-<six characters>` when none (`-2`, `-3` … on a clash);
  - set its specialty, address, tax number, default language Arabic and time zone Cairo;
  - put it on the chosen plan, through 014's link;
  - start its trial: the end is now plus the configured trial length;
  - create its main branch with the clinic's name and address;
  - create the owner's account, invited, holding «مالك العيادة».
  After the unit, the invitation email is sent. Its failure MUST NOT undo the add.
- **FR-005**: The invitation MUST be emailed in the clinic's language, work for 72 hours, and name
  the staff sign-in address `app.scs-heta.com`. No screen MUST ever show the link to an operator
  (OP-A-027).
- **FR-006**: After the add, the operator MUST land on the clinic's detail with «تم إنشاء العيادة!».
  «أضيف بواسطة» MUST be the operator's name.

**The list**

- **FR-007**: The list MUST show the four cards of FR-008, the columns of §7-1 (without the check
  box and the selection bar, which belong to the notices module), newest first, 8 a page.
- **FR-008**: The cards MUST count:
  - «إجمالي العيادات», with «+<n> هذا الشهر» (Cairo month);
  - «العيادات النشطة», with «<n>% من الإجمالي»;
  - «عيادات التجربة»;
  - «تحتاج متابعة» = overdue + read-only + suspended, 0 until 016.
  The counts MUST add up to the total.
- **FR-009**: The search MUST match part of the clinic's Arabic or English name, the owner's name,
  email or mobile,
  in any case, 300 ms after the last key, and return to page 1.
- **FR-010**: The filter MUST offer:
  - «الحالة» (the statuses that exist);
  - «الباقة» («كل الباقات»);
  - «مدة الاشتراك» (شهري · سنوي · تجربة · «غير محددة»);
  - «ينتهي خلال» (7 / 14 / 30 days; the wider includes the narrower; never what has ended).
  Choices MUST be staged until «تطبيق الفلاتر», with chips and «مسح الكل», and the panel MUST offer
  only what the clinics hold after the search.
- **FR-011**: «ينتهي في ▾» MUST sort ascending and descending, with no date last both ways and ties
  newest first.
- **FR-012**: The list MUST have its loading, empty, no-results and error states with §7-1's words.
  «تصدير إلى إكسل» and «إرسال إشعار» MUST NOT be shown until their specs (023, the notices
  module).
- **FR-013**: A row MUST open the detail. Its «⋯» MUST hold «تعديل العيادة» for a holder of
  «تعديل بيانات العيادات», and nothing else until the specs of renewing (018) and suspending (016).
  A row with no item MUST have no «⋯».

**The detail**

- **FR-014**: The detail MUST show:
  - the breadcrumb «العيادات / <name>»;
  - the header: initials, name, badge, «جهة الاتصال: <owner> · تاريخ الانضمام <date> · باقة
    <plan>», and «تعديل العيادة»;
  - the tabs «نظرة عامة» and «الإعدادات»;
  - in «نظرة عامة», the «معلومات العيادة» card with §7-3's rows and «عنوان دخول العيادة».
- **FR-015**: The badge MUST read «تجربة» while the trial has not ended and «نشطة» after. The
  other statuses are 016's. Its colours MUST be the design's (blue, green).
- **FR-016**: «المسؤول من SCS» MUST list the active operators by name with «غير معيّن». Saving MUST
  refuse an operator who is not active, record `clinic.account-manager-assigned` in both trails,
  and show the words of §5-17.
- **FR-017**: «عنوان دخول العيادة» MUST show and copy `app.scs-heta.com`, and show the owner's
  invitation in its states (§4-7).
- **FR-018**: «إعادة إرسال الدعوة» MUST:
  - replace the earlier link with a new 72-hour one, emailed;
  - be refused within a minute of the last, when the email did not leave (never shown as success),
    and when the owner has accepted, each with §5-18's words;
  - record `clinic.invitation-resent`.
- **FR-019**: A clinic that does not exist, or one the operator may not see, MUST give the same
  answer.

**Editing**

- **FR-020**: «تعديل العيادة» MUST hold the add form's fields plus «اللغة الافتراضية», «المنطقة
  الزمنية» (shown only) and the optional note (≤ 500). The plan MUST be shown and not changeable
  (spec 020). «غير مسجّل» MUST be a specialty choice in editing only.
- **FR-021**: Saving MUST update the clinic and its owner's account (name, email, mobile). The
  owner's part MUST record `clinic.owner-updated` and the clinic's part `clinic.updated`, each with
  its changed fields before and after and the note, each in the unit of its own change. A stale
  form (the clinic changed since it was opened) MUST change nothing.
  Nothing changed MUST record nothing. A new owner email MUST pass the add's checks (format, no
  other account on the platform).
- **FR-021a**: Changing the email of an owner who has signed in MUST end all the owner's open
  sessions and send a notice to the old address after the save. The email's failure MUST NOT undo
  the change. Changing the email of an invited owner MUST end the earlier invitation link.
- **FR-022**: Leaving the tax number out MUST keep the stored one; clearing it on purpose MUST
  remove it. A stored tax number that predates the rule MUST be accepted unchanged.

**Settings tab**

- **FR-023**: «الإعدادات» MUST show the strip in its two forms by permission (§5-42; without "the
  time zone" in the editor's form) and the «الإعدادات العامة» card: «اللغة الافتراضية», «المنطقة
  الزمنية» («القاهرة (GMT+2)»), «عنوان دخول الموظفين» (`app.scs-heta.com`), with «تعديل العيادة ←»
  for a holder of «تعديل بيانات العيادات».
- **FR-024**: The tab's other cards and its writes MUST NOT be shown until the specs that make
  those settings: hours (031), channels (061), messages and reminders, prescriptions and prints,
  booking and insurance policies, notifications and security, payments and teleconsult, AI, and
  the public booking page.

**Data and trail**

- **FR-025**: The clinic MUST keep:
  - its slug, unique on the platform;
  - its address and tax number;
  - its default language;
  - its specialty, from the fifteen, and only from them;
  - its trial end;
  - its account manager (an operator) or none;
  - the operator who added it.
  The owner's account MUST keep the owner's name and mobile.
- **FR-026**: The main branch MUST be the clinic's only branch at creation, with one main branch
  per clinic held by the database.
- **FR-027**: `clinic.created`, `clinic.updated`, `clinic.owner-updated`,
  `clinic.account-manager-assigned` and `clinic.invitation-resent` MUST be written to the platform's trail and the clinic's, each
  naming the operator who acted, in the same unit as the change (constitution IV, SH-092).
- **FR-028**: Every write into a clinic MUST happen under that clinic's own isolation, the clinic
  being established for the request (constitution II). No path MUST write a clinic's rows around
  it.

- **FR-030**: The operator's page (011) MUST show «عيادات تم إدارتها» in its performance card: the
  number of visible clinics whose account manager is that operator now. The figure is read through a
  published count, so the operators module reads no clinic itself.

**The plans' figures**

- **FR-029**: The plans MUST be told their clinics:
  - per plan: the count, the new this month, the ones on trial;
  - the platform's monthly revenue by OP-A-189: a clinic in trial or with no billing cycle counts
    nothing, so every clinic of this spec counts 0 until 018 records cycles;
  - each plan's clinics, with their state and join date.
  This replaces 013's "no clinics" answer.

### Key Entities

- **Clinic** (exists since 002; grows): Arabic and English names, slug, specialty, address, tax number, default
  language, state, created at, created by (operator), account manager (operator or none).
- **Clinic subscription** (014's link grows): the plan and the trial's end.
- **Main branch** (new, minimal): name, address, main. One main per clinic. 028 adds the rest.
- **Owner** (a staff account of 003/004, grows): name, email, mobile, invited, holding «مالك
  العيادة».
- **Invitation** (004): a 72-hour link, one live at a time, one email a minute.
- **Account manager**: an active operator, read through the operators' contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator adds a clinic with every field in under two minutes, and the owner's
  invitation is in the mailbox within a minute of «إضافة العيادة».
- **SC-002**: Of 10 adds at once whose names give the same slug, all 10 succeed with 10 different
  slugs. Of 2 adds at once with the same owner email, exactly one succeeds and nothing of the
  other remains.
- **SC-003**: A refused add leaves nothing behind: no clinic, branch, account, plan link or trail
  entry. In the one case another writer takes the owner's email in the same moment, the refusal
  comes after the clinic was registered: then only an unreachable `discarded` clinic record, its
  slug and its ready roles remain, in no list, count or trail an operator can read.
- **SC-004**: The list's five counts add up to the total in 100% of cases, and agree with the
  badges in the rows.
- **SC-005**: The list, the detail and the forms show in both languages at 1440 and 390, with
  nothing cut or overlapping. Beside their frames, they show no difference that is not recorded.
- **SC-006**: Every write of this spec appears in both trails with the operator's name, and the
  before-and-after of every changed field.
- **SC-007**: The list of a few hundred clinics answers a search or a filter in under a second.

## Assumptions

- **The trial length** is a deployment setting until 024 makes it a platform setting; its default
  is 14 days (the owner's answer). Clinics keep the trial end they were created with.
- **The clinic's names**: the design draws one field. The form asks for both, by the owner's
  answer; each screen shows the name in its own language. The designer is told.
- **The owner's email**: one email is one account on the whole platform (spec 003's decision). The
  BRD's "unique within the clinic" is stale.
- **The owner's name and mobile** are kept on the owner's account. 026 shows them in the users'
  screens.
- **Writing into a clinic**: an operator's request establishes the clinic and writes under its
  isolation, through the owning modules' contracts (Identity for the owner, Subscriptions for the
  plan, Tenancy for the clinic, the branch's owner for the branch). The trail's writer learns to
  name an operator in a clinic's trail.
- **The account manager** is chosen from the operators' published list of active operators. Who is
  told about the clinic because of it is 093's and 016's.
- **What later specs add**:
  - 016: the five statuses, the header's actions and the subscription log («النشاط الأخير»);
  - 017: the users card;
  - 018–019: «دورة الفوترة», renewal and payments;
  - 020: changing the plan;
  - 021: extra units;
  - 023: export;
  - 061: integrations;
  - the notices module: «إرسال إشعار»;
  - each clinic-settings spec: its card in «الإعدادات».
- **The five figures of «نظرة عامة»** (active users, tickets, WhatsApp, storage, bookings) and the
  cards «ميزات الباقة», «التخصصات», «أحدث التذاكر» come with their specs.
- **The design's «رقم الهاتف»** is built as «رقم الموبايل» (BRD rule 44). The design's per-clinic
  sign-in address is built as `app.scs-heta.com`. Both are raised with the designer.
- **The notice to the old address** when a signed-in owner's email changes is the owner's
  decision (Clarifications, 17 Oct 2026). The BRD does not carry it; it is recorded for the owner to
  bring the BRD in line. Its words are proposed (`contracts/emails.md`).
- **The owner's mobile** may be from any country (the owner's answer), not only the BRD's Egyptian
  01 mobile. Its message, «رقم الموبايل غير صحيح.», is proposed; the BRD leaves its words to the
  build.
