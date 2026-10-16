# Data Model: The Feature Catalogue

## Feature — `plans.Feature` (platform-wide)

| Field | Type | Required | Rule |
|---|---|---|---|
| `Key` | text, primary key | yes | 2–64; `^[a-z][a-z0-9_]*$`; unique; never changed (ق-٤٦) |
| `NameAr`, `NameEn` | text | yes | 1–120 each |
| `DescriptionAr`, `DescriptionEn` | text | no | ≤ 500 each |
| `Group` | code | yes | `core` · `channels` · `reports` · `money` · `clinical` · `ai` · `growth` |
| `Kind` | code | yes | `switch` · `limit` |
| `UnitAr`, `UnitEn` | text | for a limit | ≤ 40 each; both required on a limit, both empty on a switch |
| `Baseline` | integer | no | limit only; 0–1,000,000; null = no limit; empty on a switch |
| `Specialty` | code | no | one of the fifteen; null = every specialty |
| `Status` | code | yes | `built` · `upcoming`; default `upcoming` |
| `IsSold` | bit | yes | default 1 |
| `IsRetired` | bit | yes | default 0; only a sold feature is retired |
| `SortOrder` | integer | yes | computed: the group's highest + 10 on add or group change |
| `Version` | integer | yes | starts at 1; raised by every edit, not by retire or sell again (an edit made while the feature was retired saves its fields and it stays retired); an edit with an older one is refused |

Database checks: the key's pattern and length; `Kind = 'limit'` ⇒ both units present; `Kind =
'switch'` ⇒ no unit and no baseline; baseline in range; `IsRetired = 1` ⇒ `IsSold = 1`.

Listed in `PlatformWideTables`: "The catalogue of capabilities plans are made of (spec 012):
names and rules of features; no clinic's data."

Who changed it and when is the audit trail's (`audit.PlatformEntry`), not columns.

## Closed lists (in code, with their words in the console's i18n)

**Groups, in order**: الأساس `core` · القنوات `channels` · التقارير `reports` · المال `money` ·
السريرية `clinical` · الذكاء الاصطناعي `ai` · النمو `growth`.

**Specialties, in order** (SH-013): أسنان `dental` · عظام `orthopedics` · مخ وأعصاب `neurology` ·
أطفال `pediatrics` · باطنة `internal_medicine` · جراحة عامة `general_surgery` · جلدية
`dermatology` · نساء وتوليد `obgyn` · عيون `ophthalmology` · أنف وأذن وحنجرة `ent` · مسالك
بولية `urology` · قلب `cardiology` · طب نفسي `psychiatry` · تغذية `nutrition` · علاج طبيعي
`physiotherapy`.

**Decided by the specialty** (not stored; its type reads «يتبع التخصص»): `specialty_exam_modules`.

**Monthly-counted limits** (not stored): `message_credits`, `ai_xray_analysis`,
`ai_voice_notes`, `teleconsult_minutes`.

## States

| From | To | By | Guard |
|---|---|---|---|
| — | upcoming, sold (default) | «+ إضافة ميزة» | `billing.managePlans`; key unused |
| upcoming ↔ built | — | «تعديل الميزة» | key unchanged |
| sold | retired | «إيقاف الميزة…» + confirm | sold and not retired |
| retired | sold | «إعادة بيع الميزة» + confirm | retired |
| sold ↔ not sold | — | «تعديل الميزة» (the switch «تُباع في الباقات») | a retired feature is sold by definition; turning "sold" off a retired one is refused |

## The seed — 35 rows, every one `upcoming`

| Group | Key · name (ar / en) · kind · unit · specialty · baseline · sold |
|---|---|
| core | `core_clinic` الحجوزات والمرضى والكشف والروشتة / Bookings, patients, exams & prescriptions · switch · — · not sold |
| | `max_users` المستخدمون / Users · limit · مستخدم / users · baseline none |
| | `max_doctors` الأطباء / Doctors · limit · طبيب / doctors · baseline 1 |
| | `max_branches` الفروع / Branches · limit · فرع / branches · baseline 1 |
| channels | `allow_whatsapp` قناة واتساب / WhatsApp channel · switch |
| | `allow_telegram` قناة تيليجرام / Telegram channel · switch |
| | `bot_assistant` مساعد الحجز / Booking assistant · switch |
| | `message_credits` رصيد الرسائل / Message credits · limit · رسالة/شهر / msgs / mo · baseline 0 |
| reports | `allow_analytics` التقارير المالية والطبية / Financial & medical reports · switch |
| | `allow_custom_branding` العلامة التجارية المخصصة / Custom branding · switch |
| money | `allow_insurance` التأمين / Insurance · `allow_refunds` الاسترداد / Refunds · `itemized_invoice` فاتورة المريض بالبنود / Itemised patient invoice · `doctor_statement` كشف حساب الطبيب / Doctor statement · `deposit_policy` العربون ورسوم عدم الحضور / Deposit & no-show fee — all switches |
| clinical | `specialty_exam_modules` وحدات الكشف بحسب التخصص / Specialty exam modules · switch · not sold |
| | `dental_chart` خريطة الأسنان / Dental chart · switch · dental |
| | `treatment_plans` خطط العلاج / Treatment plans · switch (clarified) |
| | `dental_lab` معمل الأسنان / Dental lab · switch · dental |
| | `drug_directory` دليل الأدوية / Drug directory · `informed_consent` الموافقة المستنيرة / Informed consent · `licences_registry` ملف الرخص والتغطية / Licences & coverage · `inventory` المخزون والمستهلكات / Inventory & consumables · `sick_leave_referral` الإجازة المرضية وخطاب التحويل / Sick leave & referral letter · `lab_orders` طلبات التحاليل والأشعة / Lab & imaging orders — all switches |
| ai | `ai_xray_analysis` تحليل الأشعة بالذكاء الاصطناعي / X-ray AI analysis · limit · تحليل/شهر / scans / mo · baseline 0 |
| | `ai_voice_notes` كتابة الملاحظة بالصوت / Voice notes · limit · دقيقة/شهر / min / mo · baseline 0 |
| growth | `reminders_confirm` التذكيرات و«أكّد أو ألغِ» / Reminders & confirm-or-release · switch |
| | `queue_screen` شاشة الدور ورابط «دورك» / Queue screen & "your turn" link · limit · شاشة / screens · baseline 0 |
| | `public_booking` صفحة الحجز العامة / Public booking page · `recall_list` قائمة المتابعة / Recall list · `weekly_summary` الملخص الأسبوعي للمالك / Owner weekly summary · `teleconsult` الكشف عن بُعد / Teleconsult — switches |
| | `teleconsult_minutes` دقائق الكشف عن بُعد / Teleconsult minutes · limit · دقيقة/شهر / min / mo · baseline 0 |
| | `patient_feedback` تقييم المريض بعد الزيارة / Patient feedback · switch |

Order within each group: the order listed, 10, 20, 30, …. English names follow the design's
English frame where it shows them (read 16 Oct 2026, T002): Users, Doctors, WhatsApp channel, Dental
chart (the frame's "Dental chart & treatment plan", split), X-ray AI analysis, Specialty exam modules,
Teleconsult, Teleconsult minutes, Deposit & no-show fee, Inventory & consumables, Sick leave & referral
letter, Patient feedback, Lab & imaging orders; the rest are the behaviour reference's.

## Published contract (for 013 and 014)

`IFeatureCatalogue` in `Scs.Plans.Contracts`: for a key — whether it exists, both names, kind, unit, baseline,
specialty, built or upcoming, sold, retired; and the list of sellable, not-retired features for a
plan's grid. An unknown key answers "unknown", never a feature (ق-٣٩ is 014's).
