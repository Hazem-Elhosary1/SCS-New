# Contract: the screens of operators and roles

**Feature**: 011-operators-roles

Each screen, its frame in the operators' design file (`SetO9EhDivNcmurAMfcflY`, page "Template —
Operators" `1615:70`), its words and its states. Arabic first, English after "·". Words are the
frames' unless marked: **(BRD)** from chapter 17 where the frame has none or differs,
**(owner)** drafted here for the owner to confirm. Every screen is whole at 1440 and at 390
wide, in both directions, and writes Western digits in both languages.

## 0. Common

- **Addresses in the console**: `/operators` (tab «المشغلون»), `/operators/roles` (tab «الأدوار
  والصلاحيات»), `/operators/new`, `/operators/{id}`, `/operators/{id}/edit`,
  `/operators/roles/new`, `/operators/roles/{id}/edit`.
- **Side bar**: «المشغلون» · "Operators", icon as frame `1615:71`, shown with `operators.view`.
- **Top bar**: the operator's name and, under it, their role's name (system roles: «مسؤول عام» ·
  "Super Admin", «مسؤول الفوترة» · "Billing Manager", «مشاهد فقط» · "Viewer Only"; a custom
  role as written).
- **States of an account**: «نشط» · "Active" (green mark), «مدعو» · "Invited" **(BRD)**
  (amber), «معطل» · "Suspended" (grey). Two-step: «مفعّل» · "Enabled", «غير مفعّل» ·
  "Disabled".
- **Times**: relative as the frames write them — «منذ 5 دقائق» · "5 minutes ago", «أمس · 4:30
  م» · "Yesterday, 4:30 PM", «اليوم · 9:05 ص» · "Today, 9:05 AM", «منذ 3 أيام» · "3 days ago";
  a date as «12 يناير 2026» · "January 12, 2026"; never signed in: «لم يسجل الدخول بعد» ·
  "Hasn't signed in yet" **(BRD; English owner)**.
- **A page the operator may not open**: the console's own state, «لا يمكنك فتح هذه الصفحة» ·
  "You can't open this page" **(owner)**, with a way back.
- **Toasts** are the BRD's sentences; their English is drafted **(owner)**.

## 1. The operators list — `1615:71` · `1615:225`

| Part | Words |
|---|---|
| Header | «المشغلون» · "Operators" — «إدارة فريق تشغيل المنصة وأدوارهم وصلاحياتهم.» · "Manage the platform's operations team, roles, and permissions." |
| Button (with `operators.manage`) | «+ إضافة مشغل» · "+ Add Operator" |
| Tabs | «المشغلون» · "Operators" — «الأدوار والصلاحيات» · "Roles & Permissions" |
| Cards | «إجمالي المشغلين» · "Total Operators" («جميع الأدوار» · "All roles"); «المشغلون النشطون» · "Active Operators" («+{n} هذا الشهر» · "+{n} this month"); «دعوات معلّقة» · "Pending Invitations" («بانتظار قبول الدعوة» · "Awaiting acceptance" **(BRD; English owner)**, or «لا دعوات بانتظار القبول» · "No invites waiting" at zero); «مفعّلون بالتحقق بخطوتين» · "2FA Enabled" — `{on} / {total}` and «{n}٪ من الفريق» · "{n}% of team", the percent sign and digits Western |
| Toolbar | search «ابحث عن مشغل بالاسم أو البريد...» · "Search operators by name or email..."; «فلترة» · "Filter". No export (clarified) |
| Columns | «الاسم» · "Name" (initials, name, email under it) — «الدور» · "Role" — «آخر تسجيل دخول» · "Last Login" — «الحالة» · "Status" — «التحقق بخطوتين» · "2FA" — «الإجراءات» · "Actions" |
| Row menu | «عرض التفاصيل» · "View details"; with `operators.manage`: «تعديل المشغل» · "Edit operator" |
| Footer | «عرض {من}–{إلى} من {الإجمالي} مشغلًا» · "Showing {from}–{to} of {total} operators"; pager when more than one page |
| Filter panel **(BRD)** | «فلترة النتائج» · "Filter results"; «الحالة» · "Status"; «الدور» · "Role"; «تطبيق الفلاتر» · "Apply filters"; «إعادة تعيين» · "Reset" |

States: loading (skeleton rows and cards); empty «لا يوجد مشغلون» · "No operators" / «جرّب
تعديل البحث أو الفلاتر، أو أضف مشغل جديد للبدء.» with the add button for whoever may; nothing
matching «لا يوجد مشغلون مطابقون لبحثك أو فلاترك.» with «مسح الفلاتر» · "Clear filters"; failed
«حدث خطأ ما» / «تعذّر جلب قائمة المشغلين. تأكد من الاتصال وحاول مرة أخرى.» / «حاول مرة أخرى» ·
"Try again". A row is a link to the operator's page.

## 2. One operator — `1658:70` · `1663:70`

| Part | Words |
|---|---|
| Breadcrumb | «المشغلون / تفاصيل المشغل» · "Operators / Operator Details" |
| Head | initials, name, «{الدور} · {البريد}» |
| Buttons (with `operators.manage`, each only when it applies) | «تعديل» · "Edit"; «تعليق الحساب» · "Suspend Account" (danger outline; active, not one's own); «إعادة التفعيل» · "Reactivate" **(BRD)** (suspended; while working «جاري إعادة التفعيل…»); «إعادة تعيين كلمة المرور» · "Reset Password" **(BRD; not drawn on this frame)** (active or invited); «فك قفل الحساب» · "Unlock Account" **(owner; not drawn)** (locked) |
| «معلومات المشغل» · "Operator Information" | «الدور» · "Role"; «تاريخ الانضمام» · "Join Date"; «آخر تسجيل دخول» · "Last Login"; «التحقق بخطوتين (2FA)» · "Two-Factor Authentication (2FA)"; «رقم الموبايل» · "Mobile Number" **(BRD; frame: رقم الهاتف)**; «القسم» · "Department"; «أضيف بواسطة» · "Added By" — «النظام (أول مشغل)» · "System (first operator)", «النظام» · "System", or the inviter's name. A value that is not set shows «—» |
| «الصلاحيات» · "Permissions" | the role's permissions as marks, in catalogue order; a role with none that writes: «بدون صلاحيات تعديل» · "No edit permissions" **(BRD)** |
| «الأداء (آخر 30 يومًا)» · "Performance (last 30 days)" | one card today: «الجلسات النشطة» · "Active Sessions" (clarified) |
| «سجل الأنشطة الأخيرة» · "Recent Activity Log" | up to ten lines, each a sentence and its time; none: «لا نشاط بعد» · "No activity yet" **(owner)** |

Activity sentences **(owner)**: «سجّل الدخول من متصفح {المتصفح} على {النظام}» · "Logged in from
{browser} on {system}"; «دعا {الاسم} بدور {الدور}» · "Invited {name} as {role}"; «عدّل بيانات
{الاسم}» · "Edited {name}'s details"; «غيّر دور {الاسم} إلى {الدور}» · "Changed {name}'s role to
{role}"; «علّق حساب {الاسم}» · "Suspended {name}'s account"; «أعاد تفعيل حساب {الاسم}» ·
"Reactivated {name}'s account"; «أرسل رابط كلمة المرور إلى {الاسم}» · "Sent {name} a password
link"; «فكّ قفل حساب {الاسم}» · "Unlocked {name}'s account"; «أنشأ دور {الدور}» · "Created the
role {role}"; «عدّل دور {الدور}» · "Edited the role {role}"; «حذف دور {الدور}» · "Deleted the role
{role}".

States: loading; not found (the console's own); failed «تعذّر جلب بيانات المشغل.» with «حاول
مرة أخرى».

## 3. Add operator — `1690:70` · `1692:70`

| Part | Words |
|---|---|
| Head | «إضافة مشغل جديد» · "Add New Operator" — «أضف عضو جديد للفريق وحدد دوره وصلاحياته داخل المنصة.» · "Add a new team member and set their role and permissions on the platform." |
| «الاسم الكامل» · "Full Name" | placeholder «مثال: نور خالد» · "e.g. Nour Khaled" |
| «البريد الإلكتروني» · "Email Address" | placeholder `name@smartclinicos.com`; left to right |
| «الدور» · "Role" | placeholder «اختر الدور» · "Select a role"; a role that may not be granted is disabled in the list with its reason under its name: «به صلاحيات ليست لديك» · "Holds permissions you don't have", «يمنحه مسؤول عام فقط» · "Only a Super Admin can grant it" **(owner)** |
| «رقم الموبايل (اختياري)» · "Mobile Number (optional)" **(BRD)** | placeholder `+20 1XX XXX XXXX`; left to right |
| «القسم» · "Department" | placeholder «اختر القسم» · "Select a department"; «الدعم الفني» · "Technical Support", «الفوترة» · "Billing", «نجاح العملاء» · "Customer Success", «المبيعات» · "Sales", «الإدارة العليا» · "Senior Management" |
| «المسمى الوظيفي» · "Job Title" | placeholder «مثال: أخصائي دعم فني» · "e.g. Technical Support Specialist" |
| «لغة الواجهة الافتراضية» · "Default Interface Language" | «العربية» · "Arabic", «الإنجليزية» · "English" |
| «صلاحيات هذا المشغل» · "This operator's permissions" | appears once a role is chosen: the role's permissions as marks |
| Switch | «إلزام المشغل بتفعيل التحقق بخطوتين (2FA) عند أول دخول» · "Require the operator to set up two-factor authentication (2FA) on first login" — on and locked, with «مطلوب لكل المشغلين» · "Required for all operators" **(owner)** |
| Switch | «إرسال دعوة عبر البريد الإلكتروني للانضمام الآن» · "Send an email invitation to join now" |
| Buttons | «إلغاء» · "Cancel"; «إرسال الدعوة» · "Send Invite" (with the switch off: «حفظ» · "Save" **(owner)**) |

Errors: required «هذا الحقل مطلوب.» · "This field is required."; malformed email «أدخل بريدًا
إلكترونيًّا صحيحًا.» · "Enter a valid email address." **(owner)**; email used «هذا البريد
مستعمل لمشغل آخر.» · "This email already belongs to an operator." **(owner; the BRD has no
text)**; role refused by the server «لا يمكنك منح دور به صلاحيات ليست لديك.» · "You can't grant a
role with permissions you don't have."; other «تعذّر إضافة المشغل.» After success the new
operator's page opens with «تم إرسال الدعوة.» · "Invitation sent." or «تم إنشاء الحساب.» ·
"Account created." **(owner)**. No link is shown.

## 4. Edit operator — `1703:70` · `1705:70`; refusals `2260:1012`, `2260:1105`, `2260:1198` and their English twins

As §3 with: head «تعديل بيانات المشغل» · "Edit Operator" — «حدّث بيانات {الاسم} ودوره وصلاحياته
داخل المنصة.» · "Update {name}'s information, role, and permissions." **(BRD names the person;
frame: «حدّث بيانات المشغل…»)**; the email shown and not editable; no invitation switch; the
two-step switch reads «تفعيل التحقق بخطوتين (2FA) لهذا المشغل» · "Require two-factor
authentication (2FA) for this operator"; «ملاحظة على التعديل (اختياري)» · "Change note
(optional)" **(BRD; not drawn)** with placeholder «مثال: انتقل إلى فريق الفوترة» **(owner)** and
hint «تظهر في سجل نشاط العنصر وفي سجل نشاطك.»; button «حفظ التغييرات» · "Save Changes".

| Refusal | How it is drawn |
|---|---|
| Own account | the role field disabled, under it «لا يمكنك تغيير دورك.» · "You can't change your own role." |
| Role not allowed | disabled in the list; if the server refuses, the field in its error state with «لا يمكنك منح دور به صلاحيات ليست لديك.» |
| Last super admin | the field in its error state with «يجب أن يبقى مسؤول عام واحد نشط على الأقل.» · "At least one active Super Admin must remain." |
| Changed elsewhere | a banner «تغيّرت هذه البيانات منذ فتحتها.» · "This record changed since you opened it." with «إعادة التحميل» · "Reload" **(owner)** |

After saving: back to the operator's page with «تم تحديث بيانات المشغل.». Failures: «تعذّر جلب
بيانات المشغل.» / «تعذّر حفظ التغييرات.».

## 5. Suspend — `1708:70` · `1713:70`

Dialog «تعليق حساب المشغل» · "Suspend Operator Account": «سيتم تعليق حساب المشغل {الاسم} ولن
يتمكن من تسجيل الدخول إلى لوحة التشغيل حتى تتم إعادة تفعيله.» · "{name}'s account will be
suspended and they won't be able to sign in to the Ops Console until reactivated."; a tinted
note «ستُحفظ بياناته ويمكن إعادة تفعيل الحساب لاحقًا في أي وقت.» · "Their data will be preserved
and the account can be reactivated at any time."; «السبب (اختياري)» · "Reason (optional)"
**(BRD; not drawn)** with placeholder «لماذا يُعلَّق هذا الحساب؟»; «إلغاء» and the danger button
«تعليق الحساب» · "Suspend Account" («جاري التعليق…»). Result «تم تعليق حساب المشغل.»; refused
for the last super admin with its sentence inside the dialog; other «تعذّر تعليق حساب هذا
المشغل.». Reactivating has no dialog: «تم إعادة تفعيل حساب المشغل.» / «تعذّر إعادة تفعيل حساب هذا
المشغل.».

## 6. Send a password link — `1720:112` · `1720:304`

Dialog «إعادة تعيين كلمة المرور» · "Reset Password": «سيتم إرسال رابط إعادة تعيين كلمة المرور
إلى بريد {الاسم} الإلكتروني ({البريد}).» · "A password reset link will be sent to {name}'s email
({email})."; «إلغاء»; «إرسال الرابط» · "Send Link" («جاري الإرسال…»). Result «تم إرسال رابط
إعادة التعيين.»; too soon «أُرسل رابط لهذا الحساب منذ أقل من دقيقة. انتظر قليلًا ثم حاول مرة
أخرى.»; other «تعذّر إرسال رابط إعادة التعيين.». Unlocking **(not drawn)**: the button alone,
then «تم فك قفل الحساب.» · "Account unlocked." / «تعذّر فك قفل هذا الحساب.» **(owner)**.

## 7. The roles tab — `1735:557` · `1735:772`

| Part | Words |
|---|---|
| Header | «الأدوار والصلاحيات» · "Roles & Permissions" — «إدارة أدوار المشغلين وصلاحياتهم داخل المنصة.» · "Manage operator roles and their permissions across the platform." |
| Button (with `operators.manageRoles`) | «+ إضافة دور» · "+ Add Role" |
| Cards | «إجمالي الأدوار» · "Total Roles" («جميع الأدوار المتاحة» · "All available roles"); «أدوار مُفعّلة» · "Active Roles" («قيد الاستخدام حالياً» · "Currently in use"); «أدوار مخصصة» · "Custom Roles" («قابلة للتعديل الكامل» · "Fully editable"); «أدوار النظام» · "System Roles" («غير قابلة للحذف» · "Cannot be deleted") |
| Toolbar | search «ابحث عن دور…» · "Search roles…". No filter and no export (spec, Assumptions) |
| Columns | «اسم الدور» · "Role Name" (description under it) — «الفئة» · "Category" («نظام» · "System", «مخصص» · "Custom") — «عدد المشغلين» · "Operators" («{n} مشغل» · "{n} operators") — «الحالة» · "Status" («قيد الاستخدام» · "In use", «غير مستعمل» · "Not in use") — «الصلاحيات» · "Permissions" («{n} صلاحية» · "{n} permissions") — «الإجراءات» · "Actions" |
| Row menu (custom roles, with `operators.manageRoles`) | «تعديل الدور» · "Edit role"; «حذف الدور…» · "Delete role…" (danger, last) |
| Footer | «عرض 1–{n} من {n} أدوار» · "Showing 1–{n} of {n} roles"; no pager |

Arabic counts take their grammatical form (مشغل · مشغلان · مشغلين; صلاحية · صلاحيات). States:
loading; nothing matching the search «لا توجد أدوار مطابقة لبحثك.» · "No roles match your
search." **(owner)**; failed «تعذّر جلب الأدوار.» / «تعذر تحميل قائمة الأدوار. تحقق من اتصالك
وحاول مرة أخرى.» / «حاول مرة أخرى».

## 8. Add and edit a role — `1744:676` · `1749:744`; `1751:813` · `1752:884`

| Part | Words |
|---|---|
| Breadcrumb | «المشغلون / الأدوار والصلاحيات / إضافة دور جديد» · "Operators / Roles & Permissions / Add New Role" |
| Head (add) | «إضافة دور جديد» · "Add New Role" — «أنشئ دورًا جديدًا وحدد الصلاحيات التي يمنحها داخل المنصة.» · "Create a new role and define the permissions it grants across the platform." |
| Head (edit) | «تعديل الدور: {الدور}» · "Edit Role: {role}" — «عدّل اسم الدور ووصفه والصلاحيات الممنوحة له.» · "Update the role name, description, and the permissions it grants." |
| «الفئة» · "Category" (edit only) | a box holding the mark «مخصص» · "Custom", and beside it «الأدوار المخصصة قابلة للتعديل الكامل، بما في ذلك الحذف.» · "Custom roles are fully editable, including deletion."; a system role: «نظام» · "System" and «أدوار النظام لا تُعدَّل ولا تُحذف.» · "System roles can't be edited or deleted." **(owner; not drawn)** |
| «اسم الدور» · "Role Name" | placeholder «مثال: مشرف الفرع» · "e.g. Branch Supervisor" |
| «إنشاء بناءً على دور موجود (اختياري)» · "Base on existing role (optional)" | add only; «بدون — دور جديد بالكامل» · "None — start from scratch"; after choosing, a hint when something was left out: «تُركت {n} صلاحيات لا تحملها.» · "{n} permissions you don't hold were left out." **(owner)** |
| «الوصف» · "Description" | placeholder «اشرح باختصار الغرض من هذا الدور والمسؤوليات المرتبطة به...» · "Briefly describe the purpose of this role and its responsibilities..." |
| «الصلاحيات» · "Permissions" | «حدد الكل» · "Select All" (becomes «إلغاء التحديد» · "Clear" **(BRD)** when everything the caller may tick is ticked); nine groups, each a heading and its boxes in two columns, in the order and with the names of data-model §1 |
| A needed permission | ticked and disabled, under it «مطلوبة لـ{الصلاحية}» · "Required by {permission}" **(owner)** |
| A permission the caller does not hold | disabled, under it «ليست من صلاحياتك» · "You don't hold this permission" **(owner)** |
| «ملاحظة على التعديل (اختياري)» · "Change note (optional)" | edit only **(BRD)** |
| Buttons | «إلغاء»; «حفظ الدور» · "Save Role" (add); «حفظ التغييرات» · "Save Changes" (edit) |

Errors: empty name «هذا الحقل مطلوب.»; name used «يوجد دور بهذا الاسم.» · "A role with this name
already exists." **(owner)**; nothing ticked «حدد صلاحية واحدة على الأقل.» · "Select at least
one permission." **(owner)**; refused codes «لا يمكنك إضافة صلاحيات ليست لديك: {الصلاحيات}.»;
other «تعذّر إنشاء الدور.» / «تعذّر حفظ التغييرات.». The role one holds (not a super admin): the
form read-only, no save, a banner «لا يمكنك تعديل الدور الذي تحمله.» · "You can't edit the role
you hold." Results «تم إنشاء الدور.» / «تم تحديث الدور.», back on the roles tab.

## 9. Delete a role — `2788:15904` · `2788:15681`; blocked `2788:16350` · `2788:16128`

Dialog «حذف الدور» · "Delete role". Nobody holds it: «حذف دور «{الدور}»؟ لا يحمله أي مشغل، فلن
يفقد أحدٌ صلاحياته. يختفي من قائمة الأدوار ومن اختيار الدور، ويبقى تاريخه في سجل التدقيق.» ·
"Delete the role “{role}”? Nobody holds it, so no operator loses access. It leaves the roles
list and the role picker, and its history stays in the audit log."; «ملاحظة على التعديل
(اختياري)» · "Change note (optional)" with placeholder «مثال: دُمج في دور وكيل دعم فني» · "e.g.
Merged into Support Agent"; «إلغاء»; danger «حذف الدور» · "Delete role" («جاري الحذف…»).
Held: «لا يمكن حذف دور «{الدور}» وهو مُسند إلى مشغلين.» · "The role “{role}” can’t be deleted
while operators hold it." and, in an amber note, «يحمل هذا الدور {n} مشغلين. انقلهم إلى دور آخر قبل حذفه.» ·
"Reassign the {n} operators on this role before deleting it."; no note field; the delete button
disabled. Result «تم حذف الدور.».

## 10. What the account's holder sees (spec 010's screens, caused here)

Role changed: «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.» Suspended: «تم قفل
حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.», then on a correct password «هذا الحساب موقوف.» An
ended invitation link refers the person to whoever invited them; an ended reset link offers
«طلب رابط جديد». All built by spec 010; this spec checks them end to end.

## 11. Spec 010's screens — digits

Sign-in, the code step, forgot password, choosing a password, the profile and its sessions, the
change-password dialog: every number, countdown, date and time in Western digits in Arabic
(clarified). Nothing else of them changes.

## 12. Keyboard and screen reader

Tabs are a tab list; the table has a caption and header cells; the row menu opens with Enter
and closes with Escape, returning focus to its button; dialogs trap focus, name themselves by
their title and return focus to what opened them; a disabled box or role names its reason
through its description; a toast is announced politely; an error in a form moves focus to the
first field in error; every control is at least 40 by 40 at 390 wide.
