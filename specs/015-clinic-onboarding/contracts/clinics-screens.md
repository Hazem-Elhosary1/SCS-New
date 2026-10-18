# Contract: the operators console's «العيادات» screens

**Feature**: [../spec.md](../spec.md) · **API**: [ops-clinics-api.md](ops-clinics-api.md)

- **Design**: Figma file `SetO9EhDivNcmurAMfcflY`, page «Template — Clinics» (`591:2`). Every
  screen is built from its AR frame and checked against its EN twin.
- **Words**: Arabic words come from the BRD (§5, §7) and the frames. English words come from the
  EN frames, read when the screen is built and recorded in `validation.md`.
- **Keys**: under `app.clinics.*` in `ops/public/i18n/{ar,en}.json`.
- **Formats**:
  - digits are always Western;
  - dates are in Cairo (`opsCairoDate`, `opsCairoDateTime`, research R15);
  - Latin values (the email, the address `app.scs-heta.com`, the slug, the mobile) are in
    `<bdi dir="ltr">`.

## Side bar and routes

| Route | Guard | Page | Frames (AR / EN) |
|---|---|---|---|
| `/clinics` | `clinics.view` | `ClinicsListPage` | `606:776` / `599:680` |
| `/clinics/new` | `clinics.edit` | `ClinicFormPage` (add) | `655:1140` / `649:1040` |
| `/clinics/:id` → `overview` | `clinics.view` | `ClinicDetailPage` + `ClinicOverviewTab` | `632:956` / `613:872` |
| `/clinics/:id/settings` | `clinics.view` | `ClinicDetailPage` + `ClinicSettingsTab` | `2589:4185` / `2589:4007` |
| `/clinics/:id/edit` | `clinics.edit` | `ClinicFormPage` (edit) | `655:1230` / `649:1124` |

- **The side bar entry** `ops-nav-clinics` («العيادات» / "Clinics", icon `building`) comes after
  «الرئيسية», shown only with `clinics.view`.
- **Refused routes**: a route the operator may not open shows `ops-no-access` at the same address,
  as `guarded()` does today.

## The list (`ClinicsListPage`)

### Header

- The title «العيادات» and the line «إدارة كل العيادات على المنصة.».
- «إضافة عيادة +» (`clinics-add`) only with `clinics.edit`.
- «إرسال إشعار» and «تصدير إلى إكسل» are not built (FR-012).

### Cards (`scs-kpi-card` ×4, `clinics-card-<key>`)

| Key | Label | Value | Note |
|---|---|---|---|
| `total` | «إجمالي العيادات» | total | «+<n> هذا الشهر» |
| `active` | «العيادات النشطة» | active | «<n>% من الإجمالي» |
| `trial` | «عيادات التجربة» | trial | «قاربت على الانتهاء» when one ends within 7 days, else none |
| `follow` | «تحتاج متابعة» | 0 | «0 متأخرة · 0 قراءة فقط · 0 موقوفة» |

With no clinic at all, every value is 0 and every note is «لا توجد بيانات».

### Toolbar (`scs-list-toolbar`)

- **The search** «ابحث عن عيادة بالاسم أو جهة الاتصال...» waits 300 ms after typing, then sends
  and goes back to page 1.
- **«فلترة»** opens `scs-filter-popover`, which holds four facets:
  - «الحالة»: check boxes for the statuses held;
  - «الباقة»: a single choice, with «كل الباقات»;
  - «مدة الاشتراك»: the lengths held, with «كل المدد»;
  - «ينتهي خلال»: 7 / 14 / 30 days.
- **The panel's buttons** are «إعادة تعيين» and «تطبيق الفلاتر». Choices only take effect on
  «تطبيق الفلاتر».
- **Applied filters** show as `scs-applied-chips` under the toolbar, with «مسح الكل».

### Table (`table[scsTable]`, `clinics-row-<id>`)

| Column | Content |
|---|---|
| «العيادة» | initial mark, name in the screen's language, «أضيفت <date> · <time>» |
| «أضيف بواسطة» | the operator's name, or «—» |
| «الباقة» | the plan's name, or «—» |
| «الحالة» | `scs-badge`: «تجربة» info, «نشطة» success |
| «مدة الاشتراك» | «تجربة <n> يوم» (count forms), or «—» |
| «ينتهي في ▾» | «خلال <n> أيام» (count forms) for the trial, or empty; `button[scsSortHeader]` (research R15), `aria-sort` none → ascending → descending |
| «الإجراءات» | «⋯» with «تعديل العيادة» only with `clinics.edit`. Without it, there is no «⋯» at all |

- **Opening a row**: a click, or Enter on a focused row, opens the detail.
- **No check box** and no selection bar (the notices module).
- **The footer**: `scs-table-footer`, «عرض <from>–<to> من <total> عيادة», 8 a page.

### States

| State | testid | Content |
|---|---|---|
| loading | `clinics-loading` | skeleton cards and rows. Frames `607:120` / `602:23` |
| empty | `clinics-empty` | «لا توجد عيادات» / «جرب تعديل البحث أو الفلاتر، أو أضف عيادة جديدة للبدء.» + «إضافة عيادة» with `clinics.edit`. Frames `712:611` / `706:602` |
| no results | `clinics-no-results` | «لا توجد عيادات مطابقة للبحث أو الفلاتر المستخدمة.» + «مسح الفلاتر» |
| error | `clinics-error` | «حدث خطأ ما» / «تعذر تحميل قائمة العيادات. تحقق من اتصالك وحاول مرة أخرى.» + «إعادة المحاولة»; cards show «—». Frames `608:168` / `603:226` |
| offline | the shell's banner; writes disabled through `scsWrite` | — |
| view only | no «إضافة عيادة +», no «⋯» | — |

## The add and edit form (`ClinicFormPage`)

### Header

- **The breadcrumb**: «العيادات / إضافة عيادة جديدة», or «العيادات / <name> / تعديل».
- **The title**: «إضافة عيادة جديدة» with «املأ التفاصيل أدناه لإضافة عيادة جديدة إلى المنصة.».
  In editing: «تعديل العيادة» with «تحديث بيانات <name>.».

### Fields

Two to a row on wide screens, one on narrow:

| Field | testid | Control | Notes |
|---|---|---|---|
| «اسم العيادة (عربي) *» | `clinic-name-ar` | `scs-text-field` rtl | — |
| «اسم العيادة (إنجليزي)» | `clinic-name-en` | `scs-text-field` ltr | hint: the Arabic is used when left empty (beyond the frame) |
| «جهة الاتصال *» | `clinic-owner-name` | text | — |
| «البريد الإلكتروني *» | `clinic-email` | text ltr | — |
| «رقم الموبايل *» | `clinic-mobile` | text ltr | frame says «رقم الهاتف» (designer item) |
| «العنوان *» | `clinic-address` | text, full width | — |
| «الباقة *» | `clinic-plan` | `scs-select`, «اختر باقة» | add only; in editing a read-only line with the plan's name |
| «رقم التسجيل الضريبي (اختياري)» | `clinic-tax` | text ltr | hint «9 أرقام، مثال 123-456-789» |
| «التخصص *» | `clinic-specialty` | `scs-select`, «اختر التخصص» | hint «يحدد وحدات الكشف والشاشات السريرية التي تراها العيادة. لا تستطيع العيادة تغييره.»; editing adds «غير مسجّل» |
| «اللغة الافتراضية» | `clinic-language` | `scs-select` «العربية» / «الإنجليزية» | edit only |
| «المنطقة الزمنية» | `clinic-time-zone` | read-only «القاهرة (GMT+2)» | edit only |
| «ملاحظة على التعديل» | `clinic-note` | `scs-textarea`, ≤ 500, «مثال: بناءً على طلب العيادة لتحديث العنوان» | edit only |

### Buttons

- Offline: the shell's banner shows, and `scsWrite` disables the primary button.
- The primary button is «إضافة العيادة» («جارٍ الإضافة…»), or «حفظ التغييرات» («جارٍ الحفظ…»).
- «إلغاء» goes back without saving.

### Messages

- **Under fields**: «هذا الحقل مطلوب.», «أدخل رقم التسجيل الضريبي المكوّن من 9 أرقام.», «رقم
  الموبايل غير صحيح.», «هذا البريد مسجَّل لحساب آخر على المنصة.».
  - Frames for the invalid tax number: `2263:2467` / `2263:2373` (add), `2263:2661` / `2263:2561`
    (edit).
  - After a refused save, focus moves to the first `[aria-invalid="true"]`.
- **Top of the form** (`scs-banner tone="danger"`):
  - «تعذر إنشاء العيادة.» (`PLAN_DISABLED`, a fault);
  - «تعذر تحديث بيانات العيادة.»;
  - the busy words of `LIMIT_BUSY`;
  - `PLAN_LIMIT_REACHED`'s words;
  - `CLINIC_CHANGED_ELSEWHERE` with a reload button.
- **Toasts on success**: «تم إنشاء العيادة!», then the detail opens. Or «تم تحديث بيانات
  العيادة.», then the detail opens.

## The detail (`ClinicDetailPage`)

### Header

- The breadcrumb «العيادات / <name>».
- The initials mark, the `h1` name and the status badge.
- The line «جهة الاتصال: <owner> · تاريخ الانضمام <date> · باقة <plan>».
- «تعديل العيادة» (`clinic-edit`), only with `clinics.edit`.

### Tabs

- `scs-tabs` with «نظرة عامة» (`overview`) and «الإعدادات» (`settings`). Each tab has its own
  route.
- «الدفعات» and «التكاملات» are not built.

### «معلومات العيادة» card (`scs-card`, `<dl>`, `clinic-info-<row>`)

| Row | Value |
|---|---|
| «البريد الإلكتروني» | owner's email |
| «الهاتف» | owner's mobile, or «—» |
| «العنوان» | address, or «—» |
| «رقم التسجيل الضريبي» | tax number, or «—» |
| «الباقة» | plan's name, or «—» |
| «دورة الفوترة» | «—» |
| «التجديد القادم» | trial's end date, or «—» |
| «عنوان دخول العيادة» | `app.scs-heta.com` + `scs-copy-button` «نسخ عنوان دخول العيادة», then the invitation line and «إعادة إرسال الدعوة». Frames `2330:3095` / `2330:3050` |
| «المسؤول من SCS» | name, or «غير معيّن», + «تعيين» (`clinic-manager-assign`) with `clinics.edit` |
| «عدد الأطباء / الفروع» | «0 أطباء · <n> فرع» (count forms) |
| «آخر تسجيل دخول» | date and time, or «لم يسجّل الدخول بعد» |

### The invitation line (only while the owner is invited)

The line is one of three:

- live: «تنتهي صلاحية الدعوة في <date> — <relative>.»;
- lapsed: «انتهت صلاحيتها في <date>. أرسل دعوة جديدة صالحة 72 ساعة.»;
- none: «لا توجد دعوة سارية. أرسل واحدة.».

«إعادة إرسال الدعوة» (`clinic-invite-resend`, «جارٍ الإرسال…») appears with `clinics.edit`. Its
outcomes:

- success: the toast «تم إرسال الدعوة مرة أخرى — ينتهي الرابط الجديد في <date>.», and the line
  is read again;
- refusal: the toast in danger tone, with the BRD's words for `RESET_EMAIL_COOLDOWN`,
  `ACCOUNT_LINK_NOT_SENT` or `INVITATION_ALREADY_ACCEPTED`;
- any other failure: «تعذر إعادة إرسال الدعوة.».

### Copy outcomes

- success: the toast «تم نسخ عنوان دخول العيادة.»;
- refused by the browser: «تعذر النسخ. حدّد الرابط وانسخه يدويًا.».

### Assigning the account manager (inline, in place of the row's value)

- A `scs-select` listing the active operators by name, with «غير معيّن» first. Its label is
  visually hidden: «المسؤول من SCS».
- «حفظ» («جاري الحفظ...») and «إلغاء».
- Success: the toast «تم تحديث مسؤول الحساب.», and the row shows the name.
- `OPERATOR_NOT_ACTIVE` or any failure: «تعذر تحديث مسؤول الحساب. تُعاد المحاولة بعد قليل.». The row keeps
  the old value.

### States

| State | Content |
|---|---|
| loading | frames `636:992` / `616:345`: skeleton header and card |
| error, or not found | frames `637:1012` / `623:252`: «حدث خطأ ما» / «تعذر تحميل بيانات العيادة. تحقق من اتصالك وحاول مرة أخرى.» + «إعادة المحاولة». A clinic that does not exist shows the same (FR-019) |
| view only | frames `2786:26469` / `2786:26117`: no «تعديل العيادة», no «تعيين», no «إعادة إرسال الدعوة»; the copy button stays |
| offline | the shell's banner; `scsWrite` disables «تعديل العيادة», «تعيين», «حفظ» and «إعادة إرسال الدعوة». The copy button still works (it needs no server) |

## The settings tab (`ClinicSettingsTab`)

### The strip (`scs-banner tone="info"`)

There are two strips, depending on `clinics.edit`. The editor's strip leaves out "the time zone"
(the BRD's own correction):

- **With `clinics.edit`**: «للقراءة فقط: هذه إعدادات تضبطها العيادة من تطبيقها. يستطيع من يملك
  صلاحية «تعديل بيانات العيادات» تغيير اللغة (من «تعديل العيادة») وعنوان صفحة الحجز وتذكيرات
  التجديد بالبريد، وإيقاف صفحة الحجز أو ميزة ذكاء اصطناعي لهذه العيادة.»
- **Without it**: «للقراءة فقط: هذه إعدادات تضبطها العيادة من تطبيقها. تغيير عنوان صفحة الحجز أو
  تذكيرات التجديد بالبريد، وإيقاف صفحة الحجز أو ميزة ذكاء اصطناعي، يحتاج إلى صلاحية «تعديل
  بيانات العيادات».»

### The «الإعدادات العامة» card

- Rows: «اللغة الافتراضية», «المنطقة الزمنية» («القاهرة (GMT+2)») and «عنوان دخول الموظفين»
  (`app.scs-heta.com`).
- With `clinics.edit`, the link «تعديل العيادة ←» opens the edit form.
- Frames: `2589:4185` / `2589:4007`; view only `2790:8210` / `2790:7655`.
- The nine other cards are not built (FR-024).

### Offline

The shell's banner. The tab only reads, and «تعديل العيادة ←» still opens the form, whose save is
disabled while offline.

### Error

«تعذر تحميل إعدادات العيادة. تحقق من اتصالك وحاول مرة أخرى.» + «إعادة المحاولة».

## New shared pieces (`ui`, with gallery entries)

- **`button[scsSortHeader]`**:
  - inputs: `direction: 'none' | 'ascending' | 'descending'`; output `sorted`;
  - it sets `aria-sort` on its `th`;
  - it shows the ▾ the frame draws, turned for ascending.
- **`scs-copy-button`**:
  - inputs: `value`, `label` (the accessible name), `copiedText`, `failedText`;
  - it shows the frame's copy icon and raises a toast through `ToastService`.

## Tests (Playwright, `e2e/ops-clinics.spec.ts`, with a fake in `e2e/ops-clinics-platform.ts`)

- **Each test runs in Arabic and English**, at 1440 and 390, with `opsLooksWrong` and no Eastern
  digit.
- **Pictures**: with `SCS_SHOTS` set, they are written for the side-by-side picture.
- **The list**:
  - the cards and their sum;
  - the search on each field;
  - each facet, the chips and «مسح الكل»;
  - the sort both ways with missing dates last;
  - the pages;
  - empty, no results, error and loading;
  - the row menu with and without `clinics.edit`;
  - no section without `clinics.view`.
- **The form**:
  - each field error;
  - focus on the first invalid field;
  - the plan disabled meanwhile;
  - the email taken;
  - success and the toast;
  - the edit's tax number kept, removed and unchanged;
  - «غير مسجّل»;
  - the changed-elsewhere state;
  - refused without `clinics.edit`.
- **The detail**:
  - every row;
  - the three invitation lines and the active owner;
  - resend success and its three refusals;
  - copy success and refusal;
  - assign, «غير معيّن», cancel and refusal;
  - view only;
  - not found equal to error.
- **The settings tab**: the strip in both forms, the card, the link, and the error.
- **Vitest**:
  - `clinic-words` (count forms, the status words);
  - `ClinicsService`;
  - the sort header and the copy button;
  - `ops.spec.ts`'s pinned section list.
