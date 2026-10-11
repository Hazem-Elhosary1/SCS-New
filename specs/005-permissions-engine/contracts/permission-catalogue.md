# Contract: The Permission Catalogue

**Spec**: [../spec.md](../spec.md) | **Source**: BRD Volume 1, Chapter 10 §2-3

The clinic console's catalogue: 83 permissions in 10 groups that hold permissions, plus `home`
(«الرئيسية»), which holds none on purpose. This table was produced from the BRD table by a
script, not typed; the code table of `Scs.SharedKernel/Access` must equal it row by row, and
tests hold it there.

Rows 80–83 have their codes and English names by the owner's decision of 11 Oct 2026; the BRD
does not carry them yet.

| Count | Value |
|---|---|
| Permissions | 83 |
| By group | bookings 7 · patients 20 · money 7 · inbox 4 · tickets 4 · reports 7 · admin 6 · data 8 · settings 13 · subscription 7 |
| By kind | view 20 · read 5 · write 56 · scope 2 |
| With a requirement | 61 (each points to a view or a read) |
| «مدير العيادة» | 59 |
| «طبيب» | 40 |
| «مالك العيادة» | all, always |

**Groups, in the order of the side bar**: `home` الرئيسية · `bookings` الحجوزات · `patients`
المرضى · `money` الفواتير والتحصيل · `inbox` الصندوق الوارد · `tickets` التذاكر · `reports`
التقارير · `admin` الإدارة · `data` إدارة البيانات · `settings` الإعدادات · `subscription` الملف
والاشتراك.

**Worth noticing**: `reports.export` (47) and `settings.myHours` (62) require nothing.
`subscription.pay` (75) requires `subscription.invoices.view` (74), a read that itself requires
`subscription.view` (73). The two scope permissions are `bookings.viewAll` and
`tickets.viewAll`.

## The table

| # | Code | Arabic name | English name | Group | Kind | Requires | Manager | Doctor |
|---|---|---|---|---|---|---|---|---|
| 1 | `bookings.view` | عرض الحجوزات | View bookings | bookings | view | — | ✓ | ✓ |
| 2 | `bookings.viewAll` | عرض مواعيد كل الأطباء | View all doctors' appointments | bookings | scope | `bookings.view` | ✓ | — |
| 3 | `bookings.manage` | إدارة الحجوزات | Manage bookings | bookings | write | `bookings.view` | ✓ | ✓ |
| 4 | `bookings.cancel` | إلغاء المواعيد | Cancel appointments | bookings | write | `bookings.view` | ✓ | — |
| 5 | `bookings.attendance` | تسجيل الحضور والغياب | Check patients in and record no-shows | bookings | write | `bookings.view` | ✓ | ✓ |
| 6 | `bookings.queue` | إدارة دور اليوم | Manage today's queue | bookings | write | `bookings.view` | ✓ | ✓ |
| 7 | `bookings.waitlist` | إدارة قائمة الاستعداد | Manage the waiting list | bookings | write | `bookings.view` | ✓ | — |
| 8 | `patients.view` | عرض المرضى | View patients | patients | view | — | ✓ | ✓ |
| 9 | `patients.manage` | إدارة ملفات المرضى | Manage patient files | patients | write | `patients.view` | ✓ | ✓ |
| 10 | `patients.delete` | حذف ملفات المرضى | Delete patient files | patients | write | `patients.view` | — | — |
| 11 | `patients.merge` | دمج ملفات المرضى | Merge patient files | patients | write | `patients.view` | ✓ | — |
| 12 | `patients.records.view` | عرض السجل الطبي | View medical records | patients | read | `patients.view` | — | ✓ |
| 13 | `patients.exam.edit` | تحرير الكشف | Edit exams | patients | write | `patients.view` | — | ✓ |
| 14 | `patients.exam.sign` | توقيع ملاحظات الكشف | Sign exam notes | patients | write | `patients.view` | — | ✓ |
| 15 | `patients.documents` | إصدار المستندات الطبية | Issue clinical documents | patients | write | `patients.view` | — | ✓ |
| 16 | `patients.orders` | طلب التحاليل والأشعة | Order tests and imaging | patients | write | `patients.view` | — | ✓ |
| 17 | `patients.results` | تسجيل النتائج | Record results | patients | write | `patients.view` | ✓ | ✓ |
| 18 | `patients.consents.collect` | جمع الموافقات | Collect consents | patients | write | `patients.view` | ✓ | ✓ |
| 19 | `patients.consents.withdraw` | سحب الموافقات | Withdraw consents | patients | write | `patients.view` | — | ✓ |
| 20 | `patients.plans.create` | إنشاء خطط العلاج | Create treatment plans | patients | write | `patients.view` | — | ✓ |
| 21 | `patients.plans.present` | تقديم عروض الأسعار | Present quotes | patients | write | `patients.view` | — | ✓ |
| 22 | `patients.plans.accept` | تسجيل قبول المرضى | Record patient acceptance | patients | write | `patients.view` | — | ✓ |
| 23 | `patients.plans.execute` | تسجيل إنجاز البنود | Record items as done | patients | write | `patients.view` | — | ✓ |
| 24 | `patients.recall.view` | عرض قائمة المتابعة | View the recall list | patients | read | `patients.view` | ✓ | ✓ |
| 25 | `patients.recall.log` | تسجيل التواصل مع المرضى | Log patient contacts | patients | write | `patients.view` | ✓ | ✓ |
| 26 | `money.view` | عرض الفواتير | View invoices | money | view | — | ✓ | ✓ |
| 27 | `money.invoice` | إصدار الفواتير | Issue invoices | money | write | `money.view` | ✓ | — |
| 28 | `money.collect` | تحصيل الدفعات | Collect payments | money | write | `money.view` | ✓ | — |
| 29 | `money.refund` | إصدار المرتجعات | Issue refunds | money | write | `money.view` | ✓ | — |
| 30 | `money.shift` | تقفيل الورديات | Close shifts | money | write | `money.view` | ✓ | — |
| 31 | `money.statements.view` | عرض كشوف الأطباء | View doctor statements | money | read | `money.view` | ✓ | ✓ |
| 32 | `money.statements.approve` | اعتماد كشوف الأطباء | Approve doctor statements | money | write | `money.view` | ✓ | — |
| 33 | `inbox.view` | عرض الصندوق الوارد | View inbox | inbox | view | — | ✓ | ✓ |
| 34 | `inbox.reply` | الرد على المحادثات | Reply to conversations | inbox | write | `inbox.view` | ✓ | ✓ |
| 35 | `inbox.link` | ربط المحادثات بالمرضى | Link conversations to patients | inbox | write | `inbox.view` | ✓ | — |
| 36 | `inbox.block` | حظر جهات الاتصال | Block contacts | inbox | write | `inbox.view` | ✓ | — |
| 37 | `tickets.view` | عرض تذاكري | View my tickets | tickets | view | — | ✓ | ✓ |
| 38 | `tickets.viewAll` | عرض كل تذاكر العيادة | View all clinic tickets | tickets | scope | `tickets.view` | ✓ | — |
| 39 | `tickets.create` | فتح التذاكر | Open tickets | tickets | write | `tickets.view` | ✓ | ✓ |
| 40 | `tickets.reply` | الرد على التذاكر | Reply to tickets | tickets | write | `tickets.view` | ✓ | ✓ |
| 41 | `reports.financial` | عرض التقارير المالية | View financial reports | reports | view | — | ✓ | — |
| 42 | `reports.clinical` | عرض التقارير الطبية | View clinical reports | reports | view | — | ✓ | ✓ |
| 43 | `reports.activity` | عرض سجل الأنشطة | View the activity log | reports | view | — | ✓ | — |
| 44 | `reports.weekly` | عرض الملخص الأسبوعي | View the weekly summary | reports | view | — | ✓ | ✓ |
| 45 | `reports.feedback` | عرض تقييمات المرضى | View patient feedback | reports | view | — | ✓ | ✓ |
| 46 | `reports.feedback.followup` | متابعة تقييمات المرضى | Follow up on patient feedback | reports | write | `reports.feedback` | ✓ | — |
| 47 | `reports.export` | تصدير التقارير | Export reports | reports | write | — | ✓ | — |
| 48 | `admin.users.view` | عرض المستخدمين | View users | admin | view | — | ✓ | ✓ |
| 49 | `admin.users.manage` | إدارة المستخدمين | Manage users | admin | write | `admin.users.view` | ✓ | — |
| 50 | `admin.roles.view` | عرض الأدوار والصلاحيات | View roles & permissions | admin | view | — | ✓ | ✓ |
| 51 | `admin.roles.manage` | إدارة الأدوار والصلاحيات | Manage roles & permissions | admin | write | `admin.roles.view` | ✓ | — |
| 52 | `admin.licences.view` | عرض الرخص | View licences | admin | view | — | ✓ | — |
| 53 | `admin.licences.manage` | إدارة الرخص | Manage licences | admin | write | `admin.licences.view` | ✓ | — |
| 54 | `data.doctors.view` | عرض الأطباء | View doctors | data | view | — | ✓ | ✓ |
| 55 | `data.doctors.manage` | إدارة الأطباء | Manage doctors | data | write | `data.doctors.view` | ✓ | — |
| 56 | `data.branches.view` | عرض الفروع | View branches | data | view | — | ✓ | ✓ |
| 57 | `data.branches.manage` | إدارة الفروع | Manage branches | data | write | `data.branches.view` | ✓ | — |
| 58 | `data.services.view` | عرض الخدمات | View services | data | view | — | ✓ | ✓ |
| 59 | `data.services.manage` | إدارة الخدمات والأسعار | Manage services and prices | data | write | `data.services.view` | ✓ | — |
| 60 | `settings.view` | عرض الإعدادات | View settings | settings | view | — | ✓ | ✓ |
| 61 | `settings.hours` | إدارة ساعات العمل | Manage working hours | settings | write | `settings.view` | ✓ | — |
| 62 | `settings.myHours` | عرض ساعاتي | View my hours | settings | read | — | — | ✓ |
| 63 | `settings.channels` | إدارة القنوات ومساعد الحجز | Manage channels & booking assistant | settings | write | `settings.view` | — | — |
| 64 | `settings.policies` | إدارة سياسات الحجز | Manage booking policies | settings | write | `settings.view` | ✓ | — |
| 65 | `settings.insurance` | إدارة سياسات التأمين والمرتجعات | Manage insurance & refund policies | settings | write | `settings.view` | — | — |
| 66 | `settings.documents` | إدارة المطبوعات | Manage documents | settings | write | `settings.view` | ✓ | — |
| 67 | `settings.payments` | إدارة طرق الدفع | Manage payment methods | settings | write | `settings.view` | ✓ | — |
| 68 | `settings.teleconsult` | إدارة الكشف عن بُعد | Manage teleconsult | settings | write | `settings.view` | — | — |
| 69 | `settings.bookingPage` | إدارة صفحة الحجز العامة | Manage the public booking page | settings | write | `settings.view` | ✓ | — |
| 70 | `settings.notifications` | إدارة التنبيهات | Manage notifications | settings | write | `settings.view` | — | — |
| 71 | `settings.security` | إدارة إجراءات الأمان | Manage security actions | settings | write | `settings.view` | — | — |
| 72 | `settings.ai` | إدارة ميزات الذكاء الاصطناعي | Manage AI features | settings | write | `settings.view` | — | — |
| 73 | `subscription.view` | عرض الاشتراك | View the subscription | subscription | view | — | ✓ | — |
| 74 | `subscription.invoices.view` | عرض فواتير الاشتراك | View subscription invoices | subscription | read | `subscription.view` | — | — |
| 75 | `subscription.pay` | دفع فواتير الاشتراك | Pay subscription invoices | subscription | write | `subscription.invoices.view` | — | — |
| 76 | `subscription.plan` | تغيير الباقة | Change the plan | subscription | write | `subscription.view` | — | — |
| 77 | `subscription.units` | شراء الوحدات الإضافية | Buy extra units | subscription | write | `subscription.view` | — | — |
| 78 | `subscription.clinic` | تعديل بيانات العيادة | Edit clinic details | subscription | write | `subscription.view` | — | — |
| 79 | `subscription.tax` | تعديل البيانات الضريبية | Edit tax details | subscription | write | `subscription.view` | — | — |
| 80 | `data.consents.view` | عرض نصوص الموافقة | View consent texts | data | view | — | ✓ | ✓ |
| 81 | `data.consents.manage` | إدارة نصوص الموافقة | Manage consent texts | data | write | `data.consents.view` | ✓ | — |
| 82 | `patients.lab.view` | عرض حالات المعمل | View lab cases | patients | view | — | ✓ | ✓ |
| 83 | `patients.lab.manage` | إدارة حالات المعمل | Manage lab cases | patients | write | `patients.lab.view` | ✓ | ✓ |

## Changing it

A permission is added by a release: a new row with the next number, a constant, and — if a ready
role should hold it in clinics created from then on — a mark in its column. No stored role
changes except «مالك العيادة», which holds it at once. Renaming or removing a code is a data
change and must ship with its migration; a test fails for any stored code that is not here.