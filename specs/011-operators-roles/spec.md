# Feature Specification: Operators and Their Roles

**Feature Branch**: `011-operators-roles`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: empty. Taken from row `011-operators-roles` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1, the operators console): "inviting an
operator by link, suspending them, operators' roles and their 28 permissions." Depends on 010
(operator sign-in) and 005 (the permissions engine).

**BRD source** (`docs/reference/brd/source/`): `modules/17-ops-support-platform.md` — §2
(actors; the catalogue of 28 operator permissions in 9 groups; the rules of what is shown),
§3-9 and §3-10 (the operator; the role), §4-3 and §4-4 (the states of an account; system and
custom roles), §5-هـ-1 to §5-هـ-10 (the list, inviting, editing, suspending, reactivating,
resetting a password, adding, editing and deleting a role, unlocking), §6-هـ (the forms), §7-هـ
(the cards and tables), §8-هـ (rules ق-53 to ق-67, ق-125), §9-3 (what reaches the account's
holder), §11-هـ (`OP-B-148` to `OP-B-198`, `OP-B-391`, `OP-B-392`, `OP-B-399`, `OP-B-435` to
`OP-B-438`), §12-هـ (acceptance 61–80, 144–146, 151, 152, 175–177). `BRD-0-shared.md` —
SH-041, SH-043, SH-045, SH-048, SH-049 (locks, what is said after a correct password, ending
sessions, the life of links), SH-051 (the operators' catalogue), SH-054 to SH-056 (a permission
that needs another; view without write; every route refused until it names its permission).

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY` (the operators console), page "Template
— Operators" (`1615:70`), read on 13 Oct 2026; every frame has an Arabic and an English twin.
The operators list (`1615:71`, `1615:225`); one operator (`1658:70`, `1663:70`); add operator
(`1690:70`, `1692:70`); edit operator (`1703:70`, `1705:70`) and its three refusals — own
account, role locked (`2260:1012`, `2259:922`), role not allowed (`2260:1105`, `2259:1015`),
last super admin (`2260:1198`, `2259:1108`); suspend confirm (`1708:70`, `1713:70`); reset
password confirm (`1720:112`, `1720:304`); the roles list (`1735:557`, `1735:772`); add role
(`1744:676`, `1749:744`); edit role (`1751:813`, `1752:884`); delete role confirm
(`2788:15904`, `2788:15681`) and blocked (`2788:16350`, `2788:16128`). The invitation email is
frame `2227:216` / `2227:195` of page "Email Template", already built by spec 094.

## Overview

Spec 010 gave the operators console its door: one operator, invited by a deployment command,
who signs in and sees a frame. Everyone who signs in today can do everything, because nothing
says otherwise. This spec gives the console its people and its rules: an operator invites a
colleague from a screen, gives them one role, and from then on every screen and every request
answers to that role's permissions.

It builds the page «المشغلون» with its two tabs. Under «المشغلون»: the team's list with its
four cards, search and filters; one operator's page; adding an operator by an emailed
invitation; editing them; suspending and reactivating the account; sending a reset link;
unlocking a locked account. Under «الأدوار والصلاحيات»: the roles with their cards; adding,
editing and deleting a custom role from the closed catalogue of 28 permissions; three system
roles that nobody can change.

And it puts the rule under everything else in the console: a request is refused until its route
names the permission it needs, an item of the side bar shows only to whoever may open its
destination, and a person who may look but not change sees no control that changes.

Out of scope, each with the spec that owns it: the bell and who is told of what (093); the
audit log's page and exporting any list to Excel (023); the screens the other 22 permissions
open — clinics, tickets, notices, plans, reports, security, settings (012–025, 082); the
"enforce two-step for everyone" setting (024); an operator's branch scope and the welcome
screen of a new operator (`OP-B-197`, `OP-B-198`: later).

## Clarifications

### Session 2026-10-13

- Q: The operator's page draws "Performance (last 30 days)" and "Recent activity", and tickets
  and clinics are not built yet — what is built of them? → A: What exists today. Both sections
  are built: performance shows the account's active sessions, and the three other figures
  (average response time, clinics managed, tickets resolved) are added by the specs that own
  their sources (015, 082); recent activity lists the account's sign-ins and what it did to
  operators and roles.
- Q: Both lists draw "Export to Excel" — is it built here? → A: No. Spec 023 builds exporting
  once for every list and adds the button to these two; 011 draws no export button.
- Q: The role editor differs between the BRD (folding groups, "select all" per group, a side
  summary of what is selected) and the frame (nine groups open, one "select all", no summary) —
  which is built? → A: The frame as drawn. `OP-B-185` and §5-هـ-7 step 4–5 of the BRD are items
  for the owner to update.
- Q: The BRD and these frames write Western digits in both languages, and spec 010's screens
  were built with Arabic-Indic digits in Arabic — what does 011 do? → A: Western digits on
  every screen of the operators console, in both languages; 011 also corrects the screens of
  spec 010 (sign-in, the code, profile, sessions).
- Q: Which permissions do «مسؤول الفوترة» and «مشاهد فقط» hold, the BRD not listing them? (The
  roles frame shows eleven for each; the question was first asked with "5" for the viewer, a
  misreading of the frame found at analysis and asked again.) → A: «مشاهد فقط» reads everything
  and changes nothing — the ten view permissions and «عرض كل التذاكر»: عرض لوحة التحكم · عرض العيادات · عرض التذاكر المُسندة إليّ · عرض كل التذاكر · عرض الباقات
  والفواتير · عرض المشغلين · عرض التقارير المالية · عرض تقارير الأمان · عرض تقارير أداء المشغلين ·
  عرض تقارير نشاط العيادات · عرض الإعدادات. «مسؤول الفوترة»: عرض لوحة التحكم · عرض العيادات · إدارة بيانات
  الدفع والفواتير · إضافة رصيد إضافي للعيادات · عرض الباقات والفواتير · إدارة الباقات
  والاشتراكات · عرض التذاكر المُسندة إليّ · الرد على التذاكر · عرض التقارير المالية · تصدير
  التقارير · عرض الإعدادات.
- Q: What is the phone field called — the frames' «رقم الهاتف (اختياري)» or the BRD's later
  «رقم الموبايل»? → A: «رقم الموبايل (اختياري)» / "Mobile number (optional)"; the frames are an
  item for the designer.
- Q: Which role do operators that exist when this spec is installed get? → A: All become «مسؤول
  عام».
- Q: What does spec 010's deployment command `operator invite` do once operators are invited
  from the screen? → A: It is kept, for the first operator and for recovery. Whoever it invites
  is «مسؤول عام», shown as added by «النظام», and the invitation is recorded as made by the
  deployment tool.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - What an operator can do is what their role allows (Priority: P1)

Every operator holds one role, and the role is a set of permissions from one closed catalogue.
The console shows an operator the side-bar items they may open and nothing else; a page whose
purpose is to change something does not open for someone who may only look; and the platform
itself refuses any request whose permission the operator does not hold, whatever the screen
showed.

**Why this priority**: until this exists every operator is all-powerful, and none of the
screens that follow in this phase can be built safely.

**Independent Test**: sign in as operators holding «مسؤول عام», «مشاهد فقط» and a custom role
with one permission, and compare the side bar, the pages that open and the answers to direct
requests.

**Acceptance Scenarios**:

1. **Given** an operator whose role holds «عرض المشغلين» only, **When** they open the console,
   **Then** the side bar shows «المشغلون», the list opens, there is no «إضافة مشغل», and the
   row menu holds «عرض التفاصيل» alone.
2. **Given** the same operator, **When** they type the address of "add operator" or send its
   request directly, **Then** the page does not open and the platform refuses the request.
3. **Given** an operator whose role holds no permission of the operators group, **When** they
   open the console, **Then** «المشغلون» is not in the side bar and its address answers as a
   page they may not open.
4. **Given** any signed-in operator, **When** they open their profile or sign out of all
   devices, **Then** it works whatever their role holds.
5. **Given** a custom role that an operator holds while signed in, **When** a permission is
   removed from it and saved, **Then** the operator stays signed in and their next request is
   answered by the role as it now is.
6. **Given** a request to a route that names no permission, **When** it reaches the platform,
   **Then** it is refused, and a test fails on any such route.

---

### User Story 2 - An operator invites a colleague and the colleague joins by email (Priority: P1)

Someone who may manage operators opens «إضافة مشغل جديد», gives a name, an email and a role,
and sends the invitation. The colleague receives an email in their language that names who
invited them and in what role, with a link good for 72 hours where they choose their own
password. No password and no link is ever shown on a screen.

**Why this priority**: it is how the team grows beyond the first operator, and the only way an
operator account comes to exist from the console.

**Independent Test**: invite an address, open the email in the local mailbox, set a password
from its link, and sign in.

**Acceptance Scenarios**:

1. **Given** a valid form with a role the inviter may grant, **When** «إرسال الدعوة» is
   pressed, **Then** the account exists as «مدعو», an invitation email in the invited person's
   language is sent, their page opens, and no link is shown or can be copied anywhere.
2. **Given** the inviter does not hold «تعديل إعدادات المنصة», **When** they open the role
   list, **Then** a role holding it is disabled in the list with its reason under it.
3. **Given** someone who is not «مسؤول عام», **When** a request to add an operator as «مسؤول
   عام» reaches the platform, **Then** it is refused: «لا يمكنك منح دور به صلاحيات ليست لديك.»
4. **Given** the switch «إرسال دعوة عبر البريد الإلكتروني للانضمام الآن» is off, **When** the
   form is saved, **Then** the account is «مدعو» and no email is sent until someone sends the
   link from the operator's page.
5. **Given** an email already used by an operator, **When** the form is sent, **Then** it is
   refused at the email field and nothing is created.
6. **Given** the form, **When** it is read, **Then** it offers no password generated by the
   platform and none typed by the inviter: the link by email is the only way.
7. **Given** an invitation link older than 72 hours, **When** it is opened, **Then** the page
   says the link has ended and refers the person to whoever invited them.

---

### User Story 3 - The team is seen in one list, and each operator on one page (Priority: P1)

«المشغلون» shows four cards, a search by name or email, filters by state and role, and a table
of the team, newest first. A row opens the operator's page: who they are, their role and its
permissions, their active sessions and what they did lately.

**Why this priority**: every other action in this spec starts from this list or this page.

**Independent Test**: with a dozen operators in different states and roles, search, filter,
page through the list and open one.

**Acceptance Scenarios**:

1. **Given** operators exist, **When** the list opens, **Then** the cards show the total, the
   active, the pending invitations and how many have two-step on, and the table shows name with
   email, role, last sign-in (or «لم يسجل الدخول بعد»), state, two-step and the row menu.
2. **Given** a search or filter that matches nobody, **When** it is applied, **Then** the list
   says «لا يوجد مشغلون مطابقون لبحثك أو فلاترك.» with «مسح الفلاتر».
3. **Given** the filter panel, **When** it opens, **Then** «الحالة» and «الدور» offer only the
   values that exist among the operators.
4. **Given** the list cannot be loaded, **When** it fails, **Then** it says «حدث خطأ ما» /
   «تعذّر جلب قائمة المشغلين. تأكد من الاتصال وحاول مرة أخرى.» with «حاول مرة أخرى».
5. **Given** an operator's page, **When** it opens, **Then** it shows «معلومات المشغل» (role,
   date joined, last sign-in, two-step, phone, department, who added them — «النظام (أول
   مشغل)» when nobody did), «الصلاحيات» of the role as marks, «الأداء (آخر 30 يومًا)» with the
   active sessions, and «سجل الأنشطة الأخيرة» with the last ten things the account did.
6. **Given** a number, a date or a time on any of these screens, **When** it is shown in
   Arabic, **Then** its digits are Western.

---

### User Story 4 - An operator's details and role are changed, within guards (Priority: P2)

«تعديل بيانات المشغل» changes everything but the email. Changing the role is guarded: nobody
changes their own, nobody grants what they do not hold, and the last active «مسؤول عام» keeps
the role. A changed role signs the person out everywhere with the reason.

**Why this priority**: roles change as people change jobs; the guards are what keep the console
from locking itself out or being climbed.

**Independent Test**: edit an operator's department and role, then try the three refusals.

**Acceptance Scenarios**:

1. **Given** an operator editing someone else, **When** they save a new role and a note,
   **Then** «تم تحديث بيانات المشغل.» is shown, the operator's page returns, and the person is
   signed out of every device with «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى
   للمتابعة.»
2. **Given** an operator editing their own account, **When** the form opens, **Then** the role
   field is read-only with «لا يمكنك تغيير دورك.» under it.
3. **Given** the only active «مسؤول عام», **When** their role is changed, **Then** it is
   refused: «يجب أن يبقى مسؤول عام واحد نشط على الأقل.»
4. **Given** two requests at the same moment that would each remove one of the last two active
   «مسؤول عام», **When** both are handled, **Then** one is refused.
5. **Given** the form, **When** it opens, **Then** the email is shown and cannot be changed.
6. **Given** only the name or department changed, **When** it is saved, **Then** nobody is
   signed out.

---

### User Story 5 - An account is suspended, reactivated, sent a link, or unlocked (Priority: P2)

From an operator's page, someone who may manage operators suspends the account (with an
optional reason), reactivates a suspended one, sends a password link, or unlocks an account
locked by wrong passwords.

**Why this priority**: it is how a person leaves the team or gets back in; without it the only
tool is the deployment command.

**Independent Test**: suspend a signed-in operator and watch their browser; reactivate; send a
link twice within a minute; lock an account and unlock it.

**Acceptance Scenarios**:

1. **Given** an active account, **When** «تعليق الحساب» is confirmed, **Then** «تم تعليق حساب
   المشغل.» is shown, the state is «معطل», and every session of the account ends now, its
   devices showing «تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.»
2. **Given** the last active «مسؤول عام», **When** suspending is tried, **Then** it is refused:
   «يجب أن يبقى مسؤول عام واحد نشط على الأقل، لذلك لا يمكن تعليق هذا الحساب.»
3. **Given** an operator on their own page, **When** it opens, **Then** there is no «تعليق
   الحساب», and the platform refuses the request if it is sent.
4. **Given** a suspended account, **When** «إعادة التفعيل» is pressed, **Then** «تم إعادة تفعيل
   حساب المشغل.» is shown, the state is «نشط», and its count of wrong attempts and its lock are
   cleared.
5. **Given** an invited account, **When** «إعادة تعيين كلمة المرور» then «إرسال الرابط» are
   pressed, **Then** «تم إرسال رابط إعادة التعيين.» is shown and the email carries the
   invitation link anew; for an active account it carries a reset link good for 30 minutes.
6. **Given** a link was sent to the account 30 seconds ago, **When** «إرسال الرابط» is pressed
   again, **Then** «أُرسل رابط لهذا الحساب منذ أقل من دقيقة. انتظر قليلًا ثم حاول مرة أخرى.» is
   shown, nothing is sent and nothing is recorded.
7. **Given** an active account locked after wrong passwords, **When** it is unlocked from its
   page, **Then** the count and the lock are cleared, the person signs in with their password,
   and the unlocking is recorded.
8. **Given** a suspended operator who types a correct password, **When** they try to sign in,
   **Then** they are told «هذا الحساب موقوف.»; with a wrong password they are told nothing more
   than anyone is.

---

### User Story 6 - Roles are made from the catalogue, within guards (Priority: P2)

Under «الأدوار والصلاحيات», someone who may manage roles adds a custom role by naming it and
ticking permissions from the nine groups, optionally starting from an existing role; edits it;
and deletes it when nobody holds it. The three system roles can only be read.

**Why this priority**: custom roles are how the team is given exactly what each job needs; the
system roles alone are too coarse.

**Independent Test**: create a role from an existing one, edit it, try to delete it while held,
move the holder, delete it.

**Acceptance Scenarios**:

1. **Given** the roles tab, **When** it opens, **Then** four cards show the total, those in use,
   the custom and the system roles, and the table shows each role's name and description, its
   kind («نظام» · «مخصص»), how many operators hold it, whether it is in use, and its number of
   permissions; system roles first, with no row menu.
2. **Given** a new role, **When** «عرض كل التذاكر» is ticked, **Then** «عرض التذاكر المُسندة
   إليّ» becomes ticked and disabled with its reason under it; unticking the first frees it.
3. **Given** the editor does not hold «تصدير التقارير», **When** the form opens, **Then** that
   box is disabled with its reason; and if a request adds it anyway the platform answers «لا
   يمكنك إضافة صلاحيات ليست لديك: …» naming them.
4. **Given** "create from an existing role", **When** a role is chosen, **Then** the boxes are
   filled with those of its permissions the creator holds, and a hint says what was left out.
5. **Given** a valid form, **When** «حفظ الدور» is pressed, **Then** «تم إنشاء الدور.» is shown
   and the roles tab returns with the new role last among the custom ones.
6. **Given** an operator who is not «مسؤول عام», **When** they open the role they hold for
   editing, **Then** it is shown read-only with «لا يمكنك تعديل الدور الذي تحمله.»
7. **Given** a custom role held by nine operators, **When** «حذف الدور…» is chosen, **Then** the
   dialog says «لا يمكن حذف دور «…» وهو مُسند إلى مشغلين.» and «يحمل هذا الدور 9 مشغلين. انقلهم
   إلى دور آخر قبل حذفه.», and «حذف الدور» is disabled.
8. **Given** a custom role nobody holds, **When** its deletion is confirmed, **Then** «تم حذف
   الدور.» is shown, it leaves the list and every role picker, and earlier records still show
   its name.
9. **Given** the editor, **When** the group «التنبيهات» is read, **Then** it holds «إرسال
   إشعارات للعيادات» alone, and ticking it ticks «عرض العيادات»; the boxes number 28 in all.

---

### Edge Cases

- **The first operator**, invited by the deployment command before any role existed: holds
  «مسؤول عام», and their page says they were added by «النظام (أول مشغل)».
- **Operators that exist when this spec is installed** (invited by the command under spec 010):
  each becomes «مسؤول عام», since that is what they could do until now (FR-027b).
- **Every «مسؤول عام» has lost access** — a mailbox gone, a person gone: the deployment command
  invites a new one (FR-027a); nothing in the console can, since nobody can sign in to it.
- **The role an invitation names is deleted or changed before it is accepted**: a role that is
  held cannot be deleted, and an invited account holds its role; a changed role applies as it
  is when the person first signs in.
- **Two operators edit the same role, or the same operator, at once**: the second save is told
  the record changed and nothing is overwritten silently.
- **A role's name already used**: a custom role's name is unique among roles that are not
  deleted, compared without regard to case or surrounding spaces; a system role's names cannot
  be taken.
- **A role left with no permission**: it cannot be saved; a role holds at least one.
- **A permission whose requirement is unticked by hand**: it cannot be — the required box is
  disabled while anything that needs it is ticked.
- **A suspended operator's open pages**: within the time spec 010 already promises for an ended
  session, each shows the sign-in screen with its reason.
- **An invited operator is suspended**: only an active account can be suspended. An invitation
  sent by mistake has no control in the console — the BRD draws none: it is withdrawn with the
  deployment command of spec 010, or left to end after 72 hours.
- **Unlocking an account that is not locked**, or reactivating one that is not suspended: the
  control is not shown; a request that arrives anyway changes nothing and says so.
- **The inviter's account is suspended after inviting**: the invitation stands; the email
  already named them.
- **A very long name, role name or description**: wraps inside its cell or card; never widens
  the page.
- **The list has one page**: no pager is shown, only the count.

## Requirements *(mandatory)*

### Functional Requirements

**The catalogue and the rule under every request**

- **FR-001**: The operators' permissions MUST be one closed catalogue of 28, in nine groups,
  named exactly as the role editor draws them: لوحة التحكم (1) · العيادات (7) · التذاكر (6) ·
  التنبيهات (1) · الباقات والاشتراكات (2) · المشغلون (3) · التقارير (5) · الأمان (1) ·
  الإعدادات (2) (`SH-051`, `OP-B-195`, `OP-B-435`). A permission outside the catalogue MUST NOT
  be storable in a role (`OP-B-192`).
- **FR-002**: Some permissions need another, and the needed one MUST be kept with them whenever
  a role is saved: each write permission of clinics needs «عرض العيادات»; each of the other
  ticket permissions needs «عرض التذاكر المُسندة إليّ»; «إرسال إشعارات للعيادات» needs «عرض
  العيادات»; the two write permissions of operators need «عرض المشغلين»; «إدارة الباقات والاشتراكات»
  needs «عرض الباقات والفواتير» (the same view-before-write rule; the BRD's chapter 17 leaves
  that group to chapter 16, so it is confirmed when 013 is specified); «تعديل إعدادات المنصة»
  needs «عرض الإعدادات» (`SH-054`, ق-64, `OP-B-188`). «مراجعة الأحداث الأمنية وحظر عناوين IP» is
  not tied to «عرض تقارير الأمان» when a role is saved: the BRD requires the report permission
  only for actions that start from the events log and not for the audit log, which is a rule of
  those routes (spec 025) and not of the role.
- **FR-003**: Every request to the operators console's platform MUST be refused unless its
  route names the permission it needs and the operator's role holds it at that moment. Three
  things work on the operator's own account for any role: their profile, signing out of all
  devices, and — when 093 builds it — their bell (`SH-056`, §2-3 rule 1).
- **FR-004**: A check MUST fail the build when a route of the operators console names no
  permission and is not one of the three, and when a permission of this spec's group guards no
  route (`OP-B-196`). For the 22 permissions whose screens later specs build, the check is
  theirs to satisfy as each lands.
- **FR-005**: An operator's permissions MUST be those of their role at the moment of each
  request: a change to a role's permissions reaches its holders at their next request and MUST
  NOT end their sessions (ق-53, `OP-B-391`).
- **FR-006**: A side-bar item MUST show only to an operator who holds the permission to view
  its destination (§2-3 rule 2). The BRD's rule for «التقارير» — shown to whoever holds one of
  the four report permissions or «مراجعة الأحداث الأمنية وحظر عناوين IP» — is spec 023's to
  build with that section.
- **FR-007**: A page whose purpose is to change something — add or edit an operator, the role
  form — MUST stand behind the write permission itself; someone who only views does not open
  it (`OP-B-156`, `OP-B-184`).
- **FR-008**: Whoever holds view without write MUST see no control that writes: no header
  buttons, no account buttons, and a row menu with «عرض التفاصيل» alone (`SH-055`).
- **FR-009**: What the console knows in advance that a person may not do MUST be drawn on the
  control — disabled, with its reason beside it — and not left to a refusal after saving
  (`OP-B-168`). The platform MUST refuse the same thing all the same when it is asked directly.

**Roles**

- **FR-010**: There MUST be three system roles, named by the platform in both languages, that
  cannot be edited or deleted and whose rows carry no menu (ق-62, `OP-B-182`): «مسؤول عام»
  holds every permission; «مسؤول الفوترة» holds eleven — عرض لوحة التحكم · عرض العيادات · إدارة
  بيانات الدفع والفواتير · إضافة رصيد إضافي للعيادات · عرض الباقات والفواتير · إدارة الباقات
  والاشتراكات · عرض التذاكر المُسندة إليّ · الرد على التذاكر · عرض التقارير المالية · تصدير
  التقارير · عرض الإعدادات; «مشاهد فقط» holds eleven, every view and nothing
  that writes — عرض لوحة التحكم · عرض العيادات · عرض التذاكر المُسندة إليّ · عرض كل التذاكر · عرض الباقات
  والفواتير · عرض المشغلين · عرض التقارير المالية · عرض تقارير الأمان · عرض تقارير أداء المشغلين ·
  عرض تقارير نشاط العيادات · عرض الإعدادات (clarified).
- **FR-011**: «مسؤول عام» is an identity and not only a set of permissions: its holder may grant
  any role, edit any custom role including one they hold, and alone may grant «مسؤول عام»
  (ق-54, `OP-B-166`).
- **FR-012**: A custom role MUST have a name of 1–100 characters after trimming, unique among
  roles not deleted; an optional description of up to 500; and at least one permission. Its
  name is one text shown as written in both languages (§3-10).
- **FR-013**: The roles tab MUST show the cards «إجمالي الأدوار», «أدوار مُفعّلة», «أدوار
  مخصصة» and «أدوار النظام»; a search «ابحث عن دور…»; and a table of name with description,
  kind, number of operators, a mark of use («قيد الاستخدام» · «غير مستعمل») and number of
  permissions, system roles first and then custom ones oldest first (`OP-B-181`, `OP-B-437`,
  frame `1735:557`). It is one page with its count; no export button is drawn (clarified).
- **FR-014**: The role form MUST hold «اسم الدور», «إنشاء بناءً على دور موجود (اختياري)» (when
  adding; default «بدون — دور جديد بالكامل»), «الوصف», and «الصلاحيات» as the nine groups, all
  open, with one «حدد الكل» above them, as frame `1744:676` draws (clarified). When editing it
  also holds «ملاحظة على التعديل (اختياري)» of up to 500 characters.
- **FR-015**: A permission that another ticked permission needs MUST be shown ticked and
  disabled with its reason under it, and be freed when nothing ticked needs it (`OP-B-189`).
- **FR-016**: Nobody but «مسؤول عام» MUST be able to add to a role a permission they do not
  hold: its box is disabled with its reason, and a request that adds it is refused with «لا
  يمكنك إضافة صلاحيات ليست لديك: {الصلاحيات}.» What a role already held stays, even if the
  editor does not hold it (ق-63, `OP-B-190`).
- **FR-017**: Creating from an existing role MUST fill the boxes with those of its permissions
  the creator holds, and say in a hint which were left out (ق-66, `OP-B-187`).
- **FR-018**: Nobody but «مسؤول عام» MUST be able to edit the role they hold: it opens
  read-only with «لا يمكنك تعديل الدور الذي تحمله.» (`OP-B-191`).
- **FR-019**: A custom role held by any operator MUST NOT be deletable: the dialog names the
  count and its confirm button is disabled; the platform refuses if a holder appeared meanwhile
  (ق-65, `OP-B-193`, `OP-B-436`).
- **FR-020**: Deleting a role MUST ask for confirmation with the words of frame `2788:15904`
  and an optional note, and MUST hide the role rather than erase it: it leaves the list and
  every picker, and earlier records keep its name (`OP-B-194`).
- **FR-021**: Saving, editing and deleting a role MUST answer «تم إنشاء الدور.», «تم تحديث
  الدور.», «تم حذف الدور.» and return to the roles tab; failing answers «تعذّر إنشاء الدور.» or
  «تعذّر حفظ التغييرات.»; an empty name answers «هذا الحقل مطلوب.»

**The list and one operator**

- **FR-022**: «المشغلون» MUST have the header and line of §5-هـ-1, the two tabs, and for the
  operators tab the four cards of §7-هـ-1, the search «ابحث عن مشغل بالاسم أو البريد...»,
  «فلترة», the table of §7-هـ-2 newest first, and its count «عرض {من}–{إلى} من {الإجماليّ}
  مشغلين» with pages (`OP-B-148` to `OP-B-152`, frame `1615:71`).
- **FR-023**: Search MUST match the name and the email; the filter panel «فلترة النتائج» MUST
  offer «الحالة» and «الدور» with the values that exist, «تطبيق الفلاتر» and «إعادة تعيين».
- **FR-024**: The list MUST have its four states with the BRD's words: loading; empty («لا يوجد
  مشغلون» …, with «إضافة مشغل» for whoever may); nothing matching (with «مسح الفلاتر»); and
  failed (with «حاول مرة أخرى»).
- **FR-025**: A row's menu MUST hold «عرض التفاصيل» for everyone and «تعديل المشغل» for whoever
  may manage operators (`OP-B-153`).
- **FR-026**: An operator's page MUST show, as frame `1658:70`: the name, role and email; the
  account's buttons for whoever may use them; «معلومات المشغل»; «الصلاحيات» of the role, or
  «بدون صلاحيات تعديل» for a role with none that writes; «الأداء (آخر 30 يومًا)» with the
  account's active sessions; and «سجل الأنشطة الأخيرة» with the last ten of the account's
  sign-ins and of what it did to operators and roles, each with its time (`OP-B-155`,
  clarified).
- **FR-027**: An operator MUST NOT be deletable; suspending is what replaces it (`OP-B-179`).
- **FR-027a**: The deployment command of spec 010 that invites an operator MUST keep working, for
  the first operator and for recovery when no «مسؤول عام» can sign in: whoever it invites holds
  «مسؤول عام», their page says they were added by «النظام (أول مشغل)» for the earliest-created
  account that has no inviter and «النظام» for any other, and the invitation is recorded as made by the deployment tool (clarified).
- **FR-027b**: Every operator account that exists when this spec is installed MUST hold «مسؤول
  عام» from then on (clarified).

**Inviting and editing**

- **FR-028**: The operator form MUST hold the fields of §6-هـ-1 with their limits: full name
  (required, 1–200), email (required, a well-formed address, unique, kept in lower case), role
  (required), «رقم الموبايل (اختياري)» (up to 30; the name by the BRD's ruling, clarified), department (optional; one of الدعم الفني ·
  الفوترة · نجاح العملاء · المبيعات · الإدارة العليا), job title (optional, up to 150),
  interface language (العربية · الإنجليزية; default العربية), the two-step switch, and — when
  adding — the switch to send the invitation now, on by default (`OP-B-157`).
- **FR-029**: Choosing a role MUST show «صلاحيات هذا المشغل» read-only (`OP-B-158`).
- **FR-030**: In the role list, a role holding a permission the acting operator does not hold,
  and «مسؤول عام» for anyone who is not one, MUST be disabled with the reason under it; a
  request that grants one is refused with «لا يمكنك منح دور به صلاحيات ليست لديك.» (ق-54,
  `OP-B-165`, `OP-B-168`).
- **FR-031**: A new account MUST be «مدعو» with no usable password, and become «نشط» only when
  its holder sets a password from the invitation link (`OP-B-159`).
- **FR-032**: The invitation MUST be an email in the invited person's language, on the platform's
  template, naming who invited them and the role, with the link to set a password and its life
  of 72 hours (`OP-B-160`, `OP-B-438`; the frame's "30 minutes" is corrected, §8-ل). The link is
  kept only in a form that cannot be read back (`OP-B-161`).
- **FR-033**: No invitation or reset link MUST ever be shown or copyable in the console, and no
  other way of handing over an operator's account exists: no generated password, no typed one
  (`OP-B-162`, ق-60).
- **FR-034**: With the invitation switch off, the account is created «مدعو» and no email is
  sent (`OP-B-163`).
- **FR-035**: The email MUST NOT be changeable after the account exists (ق-59, `OP-B-164`).
- **FR-036**: Editing MUST also take «ملاحظة على التعديل (اختياري)» of up to 500 characters,
  answer «تم تحديث بيانات المشغل.» and return to the operator's page (`OP-B-171`).
- **FR-037**: Nobody MUST be able to change the role of their own account (ق-55, `OP-B-167`).
- **FR-038**: The role of the last active «مسؤول عام» MUST NOT be changed, nor their account
  suspended; two simultaneous requests MUST NOT remove the last two together (ق-56,
  `OP-B-169`).
- **FR-039**: A changed role MUST end every session of the account at once, each device showing
  «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.» (ق-58, `OP-B-170`).
- **FR-040**: The two-step switch MUST be shown on and locked while the platform requires the
  emailed code of every operator, as spec 010 does today; spec 024 decides whether it can ever
  be turned off.

**The account's actions**

- **FR-041**: «تعليق الحساب» MUST be offered for an active account that is not the acting
  operator's own, ask for confirmation with the words of §5-هـ-4 and an optional reason of up to
  500 characters, set the state to «معطل», and end every session of the account at once
  (`OP-B-172` to `OP-B-174`).
- **FR-042**: «إعادة التفعيل» MUST be offered for a suspended account, need no dialog, set the
  state to «نشط» and clear the count of wrong attempts and the lock (`OP-B-175`).
- **FR-043**: «إعادة تعيين كلمة المرور» MUST be offered for an active or invited account, ask
  with the words of §5-هـ-6, and send to the account's email a reset link good for 30 minutes
  for an active account, or the invitation link anew, good for 72 hours, for an invited one;
  an earlier link of the account stops working (`OP-B-176`, `OP-B-392`).
- **FR-044**: A second link MUST NOT be sent to one account within a minute; the acting operator
  is told so, and nothing is recorded since nothing was sent (ق-61, `OP-B-177`).
- **FR-045**: Completing a reset MUST end every session of the account (`OP-B-178`; built by
  spec 010 and kept).
- **FR-046**: An active account locked after wrong passwords MUST be unlockable from its page by
  whoever may manage operators, clearing the count and the lock (ق-125, `OP-B-399`). The control
  is not drawn; it is built as the account's other buttons are, named «فك قفل الحساب» / "Unlock
  account", and shown only while the account is locked.
- **FR-047**: A suspended operator MUST be told «هذا الحساب موقوف.» only after a correct
  password (`SH-043`).

**Records, language and what is not shown**

- **FR-048**: Each of these MUST be recorded with who did it, to whom or what, when, and the note
  or reason given: inviting, editing (naming what changed), a change of role, suspending,
  reactivating, sending a link, unlocking, and adding, editing and deleting a role. Nothing
  secret is recorded: no link, no password.
- **FR-049**: Every screen of this spec MUST exist in Arabic and English, right to left in
  Arabic, with the words of the BRD and the frames; every screen of the operators console —
  these and those of spec 010 — MUST write numbers, dates and times with Western digits in both
  languages (clarified).
- **FR-050**: The side bar and the top bar MUST show an operator's role by its name — a system
  role's in the reader's language, a custom role's as written.
- **FR-051**: Nothing of an operator's email, name or link MUST appear in a log; an account is
  named there by its identifier (`SH-091`, `SH-094`).

### Key Entities

- **Permission**: one entry of the closed catalogue — its group, its name in both languages,
  and what it needs.
- **Role**: a name, a description, a set of permissions, and whether it is the platform's own;
  hidden when deleted, never erased.
- **Operator**: a person of the platform's team — name, email, one role, mobile, department, job
  title, language, two-step, state (invited · active · suspended), who invited them, last
  sign-in.
- **Invitation or reset link**: one use, one life (72 hours or 30 minutes), kept unreadable;
  built by spec 010 and reused.
- **Record**: who did what to which operator or role, when, with which note.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every one of the 28 permissions' requests that exist after this spec, an
  operator without the permission is refused, and one with it is served; no route of the
  operators console answers without naming a permission, apart from the three of an operator's
  own account.
- **SC-002**: An operator can invite a colleague in under a minute, and the colleague can set a
  password from the email and sign in without anyone telling them a password or a link.
- **SC-003**: No screen, record or log of the platform ever shows an invitation or reset link.
- **SC-004**: An operator whose role was changed or whose account was suspended is out of every
  device within 30 seconds, and reads why.
- **SC-005**: No sequence of actions, including simultaneous ones, leaves the platform without
  an active «مسؤول عام».
- **SC-006**: Nobody ends up with a permission the person who granted it did not hold, by any
  path: inviting, editing an operator, adding or editing a role, copying a role.
- **SC-007**: Each drawn screen matches its frame in layout and wording when laid beside it, in
  Arabic and in English, at 1440 wide; and every screen is whole at 390 wide.
- **SC-008**: The list of operators opens and answers a search in under a second with 200
  operators.
- **SC-009**: A change to a role's permissions takes effect at each holder's next request, and
  signs nobody out.
- **SC-010**: Every action of FR-048 can be found afterwards with who, what and when.

## Assumptions

- **The codes of the permissions** are the BRD's (`dashboard.view`, `clinics.view`, …,
  `operators.manageRoles`, …); «إرسال إشعارات للعيادات», which the BRD leaves to be named when
  built, is `notices.send`.
- **Twenty-two of the 28 permissions guard nothing yet.** They are in the catalogue and can be
  ticked from the start, so that roles are whole before the screens arrive; each later spec
  puts its routes behind them.
- **The side bar lists what exists**, as spec 010 decided: after this spec, «الرئيسية» and
  «المشغلون». «المشغلون» shows by its view permission (FR-006). «الرئيسية» is still the arrival
  page with nothing of the dashboard on it, so it stays open to every operator until spec 022
  builds the dashboard and puts it behind «عرض لوحة التحكم». The other six items arrive with
  their specs, each behind its permission.
- **The audit log's page** is spec 023's. This spec records its actions through the platform's
  existing record of what was done (spec 007) and shows an operator's own last ten on their
  page.
- **Ending sessions and telling the browser why** are spec 010's, which already knows the
  reasons "role changed" and "suspended"; this spec causes them.
- **The invitation email** is spec 094's `FirstPassword`, which this spec gives the inviter's
  name and the role, as its frame draws. An operator invited by the deployment command still
  gets the wording that names no inviter.
- **Two-step is on for everyone** until spec 024 decides otherwise (FR-040), so the list's
  «التحقق بخطوتين» column reads «مفعّل» on every row and its card reads the whole team.
- **The unlock button** is not drawn and the BRD leaves its name to be chosen: «فك قفل الحساب» /
  "Unlock account", for the owner to confirm when the screen is reviewed.
- **Differences between the BRD and the frames, built as the frames draw and listed for the
  owner to update the BRD**: the role editor (clarified); the roles table has the mark of use
  as its own column «الحالة» and the number of permissions after it; the roles tab draws
  «فلترة» and a pager, which are not built since the BRD gives roles no filter and one page.
  Built as the BRD says and listed for the designer: the invitation link's life in the email
  frame; the phone field's name («رقم الموبايل», where the frames say «رقم الهاتف»); the missing «إعادة تعيين كلمة المرور» and unlock buttons on the operator's page frame.
- **"This month"** under «المشغلون النشطون» counts accounts that became active since the first
  day of the current month, Cairo time.
