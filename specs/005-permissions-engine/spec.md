# Feature Specification: Permissions Engine

**Feature Branch**: `005-permissions-engine`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: none typed. Taken from row `005-permissions-engine` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "the permission catalogue, enforcing
it on every route, the dependency between view and write, view-only."

**BRD source**: Volume 0 (Shared Foundation) §7 — SH-050 to SH-056; also SH-045. Volume 1 (Clinic
Console), Chapter 10 "Administration" — §2-2 display rules, §2-3 the catalogue of 83 permissions
in 11 groups, §2-4 "what is not a permission", §3-3 and §3-4 (role and permission), rules ق-28 to
ق-33, ق-35 to ق-40 and ق-60; requirements CL-AD-070 to CL-AD-072, CL-AD-082, CL-AD-083, CL-AD-090,
CL-AD-098, CL-AD-106, CL-AD-109, CL-AD-113, CL-AD-114.

**Design source**: Figma file "Smart Clinic OS — Design System & Screens", page "✓ Administration
› Roles & permissions" (node `5803:4`). Frames used by this spec: "Roles / No Access" (RTL
`6675:20818`, LTR `6675:21209`) for the no-permission state, and "Roles / Roles List Read Only"
(RTL `6675:19376`, LTR `6675:20097`) for the view-only banner. The rest of that page — the roles
list, the role detail, the editor and its guards — belongs to spec 027.

**Undrawn** (constitution I: built from the drawn components and raised with the owner):

- The no-permission sentence for an account that holds no role. The frame always names a role.
- The two states shown without the console's top bar and side bar. The frames draw them inside
  the console; the bars arrive with spec 006, so here only the content area — breadcrumb, title,
  card or notice — is built and compared.
- The "not available yet" panel under the «للعرض فقط» notice. The frame draws the roles list
  there; the list is spec 027.

**Depends on**: 003-clinic-sign-in, 004-password-account-setup.

**Open items against the two sources**:

- The design file still counts the catalogue as 79 ("٧٩ من ٧٩", "٥٥ من ٧٩", "٣٧ من ٧٩"); the BRD
  has 83 since four permissions were added (rows 80–83). This spec follows the BRD: 83, 59 and
  40. The frames are to be updated when spec 027 draws from them.
- The BRD gives rows 80–83 an Arabic name only: their English names and codes "are fixed when
  their pages are drawn". The owner fixed them on 11 Oct 2026 (Clarifications, FR-001); the BRD
  is to be updated to match.

## Overview

Spec 003 knows who is asking. This spec decides what they may do.

Every control in the clinic console is guarded by one permission from a fixed catalogue of 83. A
role is nothing but a named set of those permissions, and every member of a clinic's team holds
exactly one role. From this spec on:

1. **The server refuses by default.** Every address says who may call it — everyone, any
   signed-in person, or holders of one named permission — and the build fails for an address
   that says nothing.
2. **One catalogue.** The 83 permissions, their 11 groups, their kinds and what each requires
   live in one place that every later spec reads; nothing on any screen counts them by hand.
3. **A write needs its view.** A permission that changes something requires the view permission
   that opens its destination, and no set of permissions that breaks this can be saved.
4. **The screen follows the server.** A person does not see a control they may not use; a
   person with the view alone sees the page with a "view only" notice and no actions; a person
   who reaches a page they may not open is told which permission it needs.

It also creates the three ready roles with every clinic and gives later specs the operations to
store a role and to give an account its role.

**Out of scope** (each has its own spec): the roles pages — list, detail, editor, copying, and
the guards on who may edit which role (027); adding users and choosing their role on a screen
(026); the console's side bar and top bar (006); writing role changes to the audit trail (007);
the operators' catalogue of 28 permissions (010 and the operators' specs); plan entitlements and
"not included in your plan" (012 and after); the read-only clinic of an expired subscription
(016); the branch scope of a user (028); the link between an account and a doctor's file (029).

## Clarifications

### Session 2026-10-11

- Q: The BRD names two catalogues, 83 permissions for the clinic console and 28 for the operators console. Which does this spec deliver? → A: The clinic's only. The engine is able to hold more than one catalogue; the operators' is filled with operator sign-in (spec 010).
- Q: Where are the "no permission" state and the "view only" notice drawn? → A: On the Figma page "✓ Administration › Roles & permissions" (node `5803:4`), in the frames "Roles / No Access" and "Roles / Roles List Read Only".
- Q: Which codes and English names do permissions 80–83 take, which the BRD leaves without a code? → A: `data.consents.view` "View consent texts", `data.consents.manage` "Manage consent texts", `patients.lab.view` "View lab cases", `patients.lab.manage` "Manage lab cases" (owner decision, 11 Oct 2026; to be added to the BRD).
- Q: When a permission is taken out of a role while a holder is signed in, when does the server start refusing? → A: At the holder's very next request; the server reads the role's current permissions on every request. Only the screen may lag, until its first refusal or fifteen minutes at most. Sessions are not ended (stricter than the BRD's quarter hour, ق-35, and satisfies it).
- Q: What happens to the accounts that exist today without a role (created by specs 003 and 004)? → A: In every clinic the oldest account without a role is given «مالك العيادة» when this spec is rolled out, so no clinic is left without an owner; the local and test seeds give their other accounts explicit roles («مدير العيادة», «طبيب»); any other account stays without a role, and so without permissions, until one is given. Every role is built from the catalogue's permissions only.
- Q: When the server refuses for a missing permission, does the answer name the permission to any signed-in person? → A: Yes: the refusal carries the code of the permission needed for any signed-in person, on reading and changing addresses alike. It carries nothing about the record asked for, and a person who is not signed in gets only the "sign in" refusal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The server refuses what a person's role does not allow (Priority: P1)

A receptionist holds a role without «إدارة المستخدمين». Whatever she does — presses a control
that should not have been shown, types an address, or sends a request by hand — anything that
needs that permission is refused by the server, and nothing is read or changed.

**Why this priority**: three front ends and any direct caller share one API. A rule enforced
only by hiding a button is not a rule (constitution III).

**Independent Test**: sign in as accounts holding different roles and call addresses that need
permissions they hold and do not hold; confirm each answer, and that a refusal changed nothing.

**Acceptance Scenarios**:

1. **Given** a signed-in person whose role holds the permission an address needs, **When** they
   call it, **Then** it does its work.
2. **Given** a signed-in person whose role does not hold it, **When** they call it, **Then** it
   is refused, the refusal names the permission that was needed, and nothing was read or changed.
3. **Given** nobody is signed in, **When** an address that needs a permission is called, **Then**
   the answer is the "sign in" refusal of spec 003, not the permission refusal.
4. **Given** a person holds only the view permission of a destination, **When** they call one of
   its changing addresses, **Then** it is refused, while its reading addresses work.
5. **Given** an account that holds no role, **When** it calls any address that needs a
   permission, **Then** it is refused.
6. **Given** a refusal for a missing permission, **When** the same address is called for a
   record that exists and for one that does not, **Then** the two answers are the same: the
   permission is checked before anything is looked up.
7. **Given** a person of one clinic who holds every permission, **When** they call an address
   for another clinic's data, **Then** isolation answers as spec 002 says: a permission never
   reaches across clinics.

---

### User Story 2 - No address ships without saying who may call it (Priority: P1)

A developer adds an address for a later module and forgets to say who may call it. The address
is refused for everyone, and the build fails naming it, before the change can be merged.

**Why this priority**: SH-056 asks for "by default, not by choice". Without a check that fails
the build, the default erodes one forgotten address at a time.

**Independent Test**: add an address with no declaration and run the gates; they fail naming the
address. Declare it and they pass.

**Acceptance Scenarios**:

1. **Given** every address of the platform, **When** the gates run, **Then** each one is seen to
   declare exactly one of: open to everyone, open to any signed-in person, or needing one named
   permission of the catalogue.
2. **Given** an address that declares nothing, **When** the gates run, **Then** they fail and
   name it; and at run time it is refused for everyone.
3. **Given** an address that names a permission that is not in the catalogue, **When** the gates
   run, **Then** they fail and name it.
4. **Given** the addresses declared "open to any signed-in person", **When** they are listed,
   **Then** each is something a person does to their own account (reading who is signed in,
   changing their own temporary password) and the list is checked by the gates
   against a short written list.

---

### User Story 3 - One catalogue of 83 permissions, and three ready roles in every clinic (Priority: P1)

Every later module asks the same catalogue whether a code exists, what it is called in Arabic
and English, which group it sits in, and what it requires. Every clinic starts with three roles:
«مالك العيادة» holding all 83, «مدير العيادة» holding 59, and «طبيب» holding 40.

**Why this priority**: 92 specs name permissions. If each keeps its own list, the lists drift.

**Independent Test**: read the catalogue and compare it with the BRD table row by row; create a
clinic and read its three roles.

**Acceptance Scenarios**:

1. **Given** the catalogue, **When** it is read, **Then** it has 83 permissions numbered 1 to 83
   without gaps, each with a unique code, an Arabic name, an English name, one of 11 groups, a
   kind (view, read, write, scope) and at most one permission it requires — exactly as BRD
   Chapter 10 §2-3.
2. **Given** the 11 groups, **When** they are listed, **Then** they are in the order of the side
   bar, «الرئيسية» first with no permissions, and the others hold 7, 20, 7, 4, 4, 7, 6, 8, 13 and
   7.
3. **Given** a new clinic, **When** it is created, **Then** it has the three ready roles with
   exactly the permissions the BRD marks for each: 83, 59 and 40.
4. **Given** «مالك العيادة», **When** a permission is added to the catalogue later, **Then**
   that role holds it without anyone doing anything, and no other role changes.
5. **Given** any number shown about permissions — the size of the catalogue, of a group, of a
   role — **When** it is produced, **Then** it is counted from the catalogue, never written by
   hand.
6. **Given** a clinic, **When** it stores a role, **Then** the role can only hold codes that are
   in the catalogue; the clinic cannot add a permission of its own.

---

### User Story 4 - A write permission needs the view that opens its destination (Priority: P2)

Someone builds a role with «إدارة المستخدمين» and without «عرض المستخدمين». The role cannot be
saved: managing users is done from a page its holder could not open.

**Why this priority**: without the rule a role can hold powers nobody can reach, or — worse —
powers that work by request while the page that shows their effect stays closed.

**Independent Test**: try to store sets of permissions that respect and that break the rule, for
every write, read and scope permission that requires another.

**Acceptance Scenarios**:

1. **Given** a set that holds a permission and not the one it requires, **When** it is stored
   as a role, **Then** it is refused, naming both.
2. **Given** a set that holds a permission and the one it requires, **When** it is stored,
   **Then** it is accepted.
3. **Given** the catalogue, **When** each permission's requirement is followed, **Then** it leads
   to a view or read permission, never to a write, and never further than one step for the
   check: only the direct requirement is demanded (in «الإدارة» there are three destinations
   with three separate views).
4. **Given** «دفع فواتير الاشتراك», **When** its requirement is read, **Then** it is «عرض فواتير
   الاشتراك», which itself requires «عرض الاشتراك»; a set needs all three to hold the first.
5. **Given** «تصدير التقارير» and «عرض ساعاتي», **When** their requirements are read, **Then**
   they require nothing.
6. **Given** a set with no permission at all, **When** it is stored as a role, **Then** it is
   refused.
7. **Given** a view permission alone, **When** it is held, **Then** it grants no write; and no
   write permission grants another write.

---

### User Story 5 - The screen shows a person only what they may do (Priority: P2)

A doctor with «عرض الأدوار والصلاحيات» and without «إدارة الأدوار والصلاحيات» opens the roles
page: above it a notice reads «للعرض فقط» and «التعديل يحتاج صلاحية «إدارة الأدوار
والصلاحيات».», and there is no button that changes anything. A receptionist without the view
types the page's address: she sees «هذه الصفحة ليست في دورك», the permission the page needs, the
name of her role, and whom to ask.

**Why this priority**: a control that is shown and then refused wastes people's time; a page
that is simply blank tells them nothing.

**Independent Test**: for a destination with a view and a manage permission, open it as a person
with both, with the view alone, and with neither, and compare with the two design frames.

**Acceptance Scenarios**:

1. **Given** a person without a permission, **When** a screen is shown, **Then** the control
   that permission guards is absent — hidden, not disabled.
2. **Given** a person with the view permission of a destination and without its write
   permission, **When** they open it, **Then** the notice «للعرض فقط» is shown above the content
   with a line naming the permission that changing needs, and the page has no action controls.
3. **Given** a person without the view permission of a destination, **When** they reach it by
   its address, **Then** the no-permission state is shown as drawn: a lock mark, «هذه الصفحة
   ليست في دورك», and the text «تُفتح لمن يملك «‹الصلاحية›»، ودورك «‹الدور›» لا يحملها. وما يلزم
   منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.»
4. **Given** the no-permission state, **When** it is inspected, **Then** it shows nothing of the
   destination's content and made no request for it.
5. **Given** the client's own list of what the person may do, **When** it is compared with what
   the server allows, **Then** they agree; and when they do not (the role changed a moment ago),
   the server's refusal is shown as the no-permission state and the list is refreshed.
6. **Given** a control disabled for a reason other than a permission (the state of the record,
   the plan, the subscription), **When** it is shown, **Then** it is disabled with its reason,
   not hidden: hiding is for a missing permission only.
7. **Given** «الرئيسية», **When** any signed-in person opens it, **Then** it opens: it needs no
   permission.

---

### User Story 6 - A change of role takes effect at once (Priority: P2)

A clinic's owner removes «تحصيل الدفعات» from a role while one of its holders is signed in. Her
next action that needs it is refused; she does not have to sign out and in. Separately, when an
account is given a different role, its sessions end.

**Why this priority**: a person who was just removed from a responsibility must not keep it
until tomorrow.

**Independent Test**: change a role's permissions and, separately, an account's role, while its
holder has a live session; watch the very next request.

**Acceptance Scenarios**:

1. **Given** a signed-in holder of a role, **When** a permission is removed from that role,
   **Then** their next request that needs it is refused, without signing in again.
2. **Given** the same, **When** a permission is added to the role, **Then** their next request
   that needs it works.
3. **Given** a signed-in account, **When** it is given a different role, **Then** every one of
   its sessions ends (SH-045) and the next action shows the sign-in form.
4. **Given** a role's permissions changed, **When** its holder's screen next asks what they may
   do — at the latest within fifteen minutes — **Then** the controls shown follow the new set.
5. **Given** a role is removed while an account still holds it, **When** removal is attempted
   through this spec's operation, **Then** it is refused: a role in use is not removed.

---

### User Story 7 - Narrow by default: scope permissions and one's own record (Priority: P3)

«عرض الحجوزات» lets a doctor see his own appointments; «عرض مواعيد كل الأطباء» widens that to
everyone's. A person always sees the record that is theirs, whatever their role holds.

**Why this priority**: two permissions of the catalogue widen a scope rather than open a
control, and SH-055 gives the owner of a record their record. Later specs need one way to ask
both questions.

**Independent Test**: with a made-up destination, ask "may this person see all, or only their
own?" for holders and non-holders of the scope permission, and "is this their own record?".

**Acceptance Scenarios**:

1. **Given** a person with a view permission and without its scope permission, **When** a
   module asks how wide they may see, **Then** the answer is "their own only".
2. **Given** a person with both, **When** the same is asked, **Then** the answer is "all".
3. **Given** a scope permission, **When** it is stored in a set without the view it requires,
   **Then** the set is refused, like any other requirement.
4. **Given** a record that belongs to the person asking, **When** a module declares the address
   "holders of the permission, or the record's owner", **Then** the owner reaches it without the
   permission, and reaches no other record that way.

---

### User Story 8 - The two states are complete, bilingual and as drawn (Priority: P3)

The no-permission state and the «للعرض فقط» notice exist in Arabic and in English, mirrored, and
match their frames in the design file.

**Why this priority**: constitution VI, and since v1.2.0 constitution I: a screen is built from
its frames.

**Independent Test**: lay each state in each language beside its frame.

**Acceptance Scenarios**:

1. **Given** the no-permission state in Arabic, **When** it is laid beside "Roles / No Access /
   RTL", **Then** the mark, title, text, spacing and card match.
2. **Given** the same in English, **When** it is laid beside the LTR frame, **Then** it reads
   "This page is not in your role" and the text as drawn, mirrored.
3. **Given** the view-only notice in each language, **When** it is laid beside "Roles / Roles
   List Read Only", **Then** its tone, icon, title «للعرض فقط» / its English, and its line match.
4. **Given** either state, **When** the permission or the role it names is read out by assistive
   technology, **Then** the names are spoken, not only shown.

---

### Edge Cases

- A clinic whose accounts all predate this spec: its oldest account becomes the holder of «مالك
  العيادة» at roll-out (FR-036); running the roll-out again changes nothing.
- An account with no role (created before this spec, or by an operation that gave none): it
  holds no permission, reaches only what any signed-in person reaches, and its screens show the
  no-permission state naming no role.
- The last account that holds «مالك العيادة» is given another role, or its role is removed:
  refused by this spec's operation — a clinic is never left without an owner (SH-053).
- A person changes the role of their own account through this spec's operation: refused
  (SH-053).
- A role is stored with a code that is not in the catalogue, or with the same code twice:
  refused naming the code.
- A role is stored with the name of another role of the same clinic, or a name longer than 40
  characters, or empty: refused (ق-54).
- Two clinics each have a role named «فريق العيادة»: allowed; names are unique within a clinic
  only.
- «مالك العيادة» is edited, renamed or removed through this spec's operations: refused; it is
  fixed.
- «مدير العيادة» or «طبيب» is edited or removed: allowed like a custom role (if nobody holds it,
  for removal). A clinic may end up without them.
- A permission is added to the catalogue in a later release: every clinic's «مالك العيادة» holds
  it at once; ready roles of clinics created earlier are not changed.
- A permission's code is renamed or removed from the catalogue in a later release: not
  supported silently — the gates fail for any stored role or address that names a code no longer
  in the catalogue, so the change must come with its data fix.
- An address needs two permissions at once: not supported; an address names one. Where a screen
  needs two (a write and its view), the requirement rule already guarantees the second.
- A person holds a permission of a feature the clinic's plan does not include, or the clinic is
  read-only: the permission is still held; the other refusal (plan, subscription) is a different
  answer delivered by its own spec, and takes precedence there.
- A request is in flight when the role changes: it completes under the set that was read when
  it began; the next one sees the new set.
- A session of an account that holds a temporary password (spec 004): refused by that gate
  first; permissions are not consulted.
- The no-permission state for a permission whose name is long, or a role whose name is 40
  characters: the text wraps inside the card; nothing is cut.

## Requirements *(mandatory)*

### Functional Requirements

**The catalogue**

- **FR-001**: The platform MUST hold one catalogue of the clinic console's permissions: 83, as
  listed in BRD Chapter 10 §2-3, each with its number (1 to 83, no gaps), unique code, Arabic
  name, English name, group, kind (view, read, write, scope) and at most one permission it
  requires. Rows 80 to 83, which the BRD leaves without a code, are: 80 `data.consents.view`
  "View consent texts"; 81 `data.consents.manage` "Manage consent texts" (requires 80); 82
  `patients.lab.view` "View lab cases"; 83 `patients.lab.manage` "Manage lab cases" (requires
  82).
- **FR-002**: The catalogue MUST have 11 groups in the order of the side bar: «الرئيسية» (no
  permissions, deliberately), «الحجوزات» 7, «المرضى» 20, «الفواتير والتحصيل» 7, «الصندوق الوارد»
  4, «التذاكر» 4, «التقارير» 7, «الإدارة» 6, «إدارة البيانات» 8, «الإعدادات» 13, «الملف
  والاشتراك» 7.
- **FR-003**: The catalogue is the platform's, not a clinic's: a clinic chooses from it and
  cannot add to it (ق-33). Every count shown or returned about permissions MUST be computed from
  it (CL-AD-075).
- **FR-004**: The catalogue MUST be readable by every module and by the client in Arabic and in
  English, in group order and number order.
- **FR-005**: A permission added to the catalogue in a later release MUST NOT change any stored
  role except «مالك العيادة», which holds it automatically (CL-AD-109).
- **FR-006**: The gates MUST fail when the catalogue is inconsistent: a duplicate code or
  number, a gap in the numbers, a requirement that points to a write permission or to a code
  that does not exist, a group count that differs from FR-002, or a total other than 83.
- **FR-007**: The engine MUST be able to hold a second, separate catalogue for the operators
  console; this spec fills none.

**Roles and who holds them**

- **FR-008**: A role MUST be a named set of catalogue permissions belonging to one clinic: a
  name (required, unique within the clinic, at most 40 characters), an optional one-line
  description, whether it is ready-made or custom, whether it is fixed, and its permissions (at
  least one).
- **FR-009**: Every clinic MUST have three ready roles from the moment it exists: «مالك العيادة»
  (fixed; all permissions, always), «مدير العيادة» (59) and «طبيب» (40), with exactly the sets
  the BRD marks (CL-AD-082, ق-60).
- **FR-010**: «مالك العيادة» MUST NOT be edited, renamed or removed. «مدير العيادة» and «طبيب»
  are edited and removed like custom roles (ق-28, ق-29).
- **FR-011**: Each staff account holds at most one role. There are no per-person exceptions, no
  temporary grants and no delegation (CL-AD-106). An account with no role holds no permission.
- **FR-012**: This spec MUST provide, for specs 015, 026 and 027 to call and with no screen of
  its own, the operations to: store a new role; replace a role's name, description and
  permissions; remove a role that nobody holds; give an account its role.
- **FR-013**: Those operations MUST refuse: a set that breaks the requirement rule (FR-016); an
  empty set; a code not in the catalogue; a name that is empty, too long or already used in the
  clinic; any change to «مالك العيادة»; removing a role that an account holds; taking «مالك
  العيادة» from the last account that holds it; and a person changing the role of their own
  account (SH-053).
- **FR-014**: Giving an account a different role MUST end all of its sessions (SH-045).
- **FR-036**: When this spec is rolled out, every clinic that has accounts and none holding
  «مالك العيادة» MUST have that role given, once, to its oldest account without a role — an
  active account before any other, then by when it was created. The local and test seeds MUST
  give each of their other accounts an explicit role, so that all three ready roles have a
  holder to test with; one seeded account holds a custom role without «عرض الأدوار والصلاحيات»,
  because all three ready roles hold it and the no-permission state could not otherwise be
  seen. No other account is given a role automatically.
- **FR-015**: Roles are per clinic and are isolated like all clinic data: one clinic cannot read,
  hold or name another clinic's role.

**A write needs its view**

- **FR-016**: A set of permissions is valid only when, for every permission in it, the
  permission it directly requires is also in it (SH-054, ق-31). The rule is one function of the
  engine, used wherever a set is stored and offered to the client so a screen can show the same
  rule before saving.
- **FR-017**: The requirement is one step: holding a permission demands its direct requirement
  only (CL-AD-089). A requirement that itself requires another is satisfied because that set
  must be valid too.
- **FR-018**: A view permission MUST grant no write, and no write permission MUST imply another
  write (ق-32).

**Enforcement on the server**

- **FR-019**: Every address of the platform MUST declare exactly one of: open to everyone; open
  to any signed-in person; needing one named permission; or needing one named permission *or*
  being the owner of the record (FR-026). An address that declares nothing is refused for
  everyone (SH-056, ق-38).
- **FR-020**: The gates MUST fail, naming the address, for any address that declares nothing or
  names a code that is not in the catalogue (CL-AD-071).
- **FR-021**: "Open to any signed-in person" is for what a person does to their own account
  only. The addresses that use it MUST match a short written list, and the gates fail when an
  address is added to it without the list being changed.
- **FR-022**: A request that needs a permission the person's role does not hold MUST be refused
  with one consistent answer that carries the code of the permission needed, before any record
  is looked up and without changing anything. The answer is the same for reading and changing
  addresses, says nothing about the record asked for (not even whether it exists), and is never
  given to a caller who is not signed in: that caller gets the "sign in" refusal only (SH-043).
- **FR-023**: Refusals are ordered: no session (spec 003) → temporary password (spec 004) →
  permission → whatever the address itself decides. A permission never widens isolation
  (spec 002).
- **FR-024**: The permissions of the person asking MUST be those of their role at the moment of
  the request: a change to a role's permissions takes effect on its holders' next request, with
  no need to sign in again and with no delay allowed on the server (ق-35, CL-AD-098).
- **FR-025**: Modules MUST be able to ask, in one call each: does the person hold this
  permission; and, for a view permission that has a scope permission, how wide may they see —
  their own only, or all.
- **FR-026**: A module MUST be able to declare that a record's owner reaches their own record
  without the permission (SH-055). What "owner" means for a record is the module's to say; the
  engine provides the declaration and its enforcement, and nothing here grants access to any
  other record.

**What the screen shows**

- **FR-027**: The signed-in person's screen MUST be able to learn, in one answer: the name of
  their role, and the codes they hold. It is refreshed at sign-in, whenever the server refuses
  for a missing permission, and at least every fifteen minutes. While the account holds a
  temporary password (spec 004) the list of codes is empty.
- **FR-028**: A control guarded by a permission the person does not hold MUST be absent from the
  screen — hidden, not disabled (ق-37, CL-AD-072). Disabling is for reasons other than a
  permission (§2-4).
- **FR-029**: A destination opened by a person without its view permission MUST show the
  no-permission state as drawn in "Roles / No Access": a lock mark in the danger tone; the title
  «هذه الصفحة ليست في دورك»; the text «تُفتح لمن يملك «‹الصلاحية›»، ودورك «‹الدور›» لا يحملها.
  وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.»; nothing of the destination's
  content, and no request for it.
- **FR-030**: A destination opened by a person with its view permission and without its write
  permission MUST show the notice as drawn in "Roles / Roles List Read Only": information tone
  with its icon, the title «للعرض فقط», and the line «التعديل يحتاج صلاحية «‹الصلاحية›».» — and
  no action controls.
- **FR-031**: Both states MUST exist in Arabic and English, mirrored, with the permission's and
  the role's names in the language of the page (a role's name is as the clinic wrote it).
- **FR-032**: Until the pages of the administration group exist (026, 027), the address of the
  roles destination MUST exist and show: the no-permission state to a person without «عرض
  الأدوار والصلاحيات»; and to a person with it, the "not available yet" page of spec 003 — under
  the «للعرض فقط» notice when they lack «إدارة الأدوار والصلاحيات». For a person with the view
  the page asks the server for the clinic's roles (name, kind and number of permissions of
  each), which is the first real address guarded by a permission. This is what lets the two
  states, and a refusal by the server, be seen and verified now.
- **FR-033**: «الرئيسية» needs no permission (ق-40).

**Wording and records**

- **FR-034**: No text of the product may use a role's name where a permission or a party is
  meant (ق-30, CL-AD-107): refusals and notices name the permission.
- **FR-035**: A refusal for a missing permission MUST be recorded in the operational log with
  identifiers only: the account, the clinic, the address and the code needed.

### Key Entities

- **Permission**: one line of the platform's catalogue — number, code, Arabic and English name,
  group, kind, and the permission it requires. Fixed by the platform; the same for every clinic.
- **Permission group**: one of the 11 headings of the side bar under which permissions are
  listed, in a fixed order.
- **Role**: a clinic's named set of permissions. Ready-made (created with the clinic) or custom;
  one of the ready-made is fixed. Belongs to one clinic.
- **Role holding**: the fact that an account holds a role — at most one per account.
- **Access rule of an address**: what an address declares about who may call it — everyone, any
  signed-in person, one permission, or one permission or the record's owner.
- **Effective permissions**: what the person asking may do right now: the set of their role.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the platform's addresses declare who may call them, and a build with one
  that does not is rejected naming it.
- **SC-002**: For every one of the 83 permissions, a request by a person who holds it succeeds
  and the same request by a person who does not is refused — in 100% of automated checks, with 0
  records read or changed by a refused request.
- **SC-003**: The catalogue matches the BRD table in 83 of 83 rows on number, code, both names,
  group, kind and requirement, and the three ready roles hold exactly 83, 59 and 40.
- **SC-004**: 0 sets of permissions that break the requirement rule can be stored; every one of
  the catalogue's requirements is covered by an automated check in both directions.
- **SC-005**: After a role's permissions change, 100% of its signed-in holders' next requests
  follow the new set, with no sign-in in between; after an account's role changes, 100% of its
  sessions are refused at their next request.
- **SC-006**: Checking a permission adds no delay a person can notice: pages open and actions
  answer as fast as before this spec — the check adds under 5 milliseconds at the median,
  measured on the same machine against an address that needs no permission.
- **SC-007**: The no-permission state and the view-only notice match their design frames in
  Arabic and in English when laid side by side at the frame's size, with no difference in mark,
  wording, tone or layout.
- **SC-008**: A person without a permission sees 0 controls guarded by it across the screens
  that exist, and a person who types the address of a destination they may not open sees the
  no-permission state within one second, with 0 requests made for its content.
- **SC-009**: A later spec can guard a new address or control by naming one code, without
  changing the engine.

## Assumptions

- **Rows 80–83 keep the order, group, kind and requirement the BRD gives them**; only their codes
  and English names were missing and are now fixed (FR-001). Which row is which is taken from
  the BRD's order: consent texts 80–81, lab cases 82–83.
- **«دفع فواتير الاشتراك» requires «عرض فواتير الاشتراك»**, itself a read permission that
  requires «عرض الاشتراك», as the BRD table says; it is the one place a requirement is a read
  permission rather than a view.
- **A change to a role's permissions takes effect on the next request** (owner decision, 11 Oct
  2026). The BRD asks for "at the next session refresh, within a quarter of an hour"; immediate
  is stricter and satisfies it. The fifteen minutes remain as the longest the *screen* may lag
  before it hides or shows a control; the server never lags.
- **Changing an account's role ends its sessions (SH-045); changing what a role holds does
  not** (ق-35). These are two different events.
- **The list of what a person may do is not a secret from that person**: their screen receives
  the codes they hold and their role's name. It receives nothing about other roles.
- **An account without a role** exists only as a leftover: accounts created by specs 003 and 004
  before roles existed. FR-036 says what becomes of them (owner decision, 11 Oct 2026); specs
  015 and 026 always give a role.
- **Who may store, edit or remove a role, and who may give which role to whom** — "nobody grants
  a permission they do not hold", "nobody edits the role they hold", copying a role — are the
  rules of the roles and users pages (026, 027). This spec's operations enforce only what cannot
  depend on the caller's own set: validity, the fixed role, the role in use, the last owner, and
  one's own account.
- **The no-permission state names the person's role** because the design draws it so. The rule
  that a role's name is not used in place of a permission (ق-30) is kept: the sentence names the
  permission as what is missing.
- **The design file's counts (79, 55, 37) are stale**; the BRD's (83, 59, 40) are used. Recorded
  for the owner.
- **The view-only notice and the no-permission state are shared components**, drawn once on the
  roles page and used by every later destination with the destination's own permission names.
- **Plan entitlements, the read-only clinic, the shape of the clinic and the state of a record**
  hide or limit controls too, and are not permissions (§2-4). Each is delivered by its own spec
  and has its own answer; none is in the catalogue.
- **Writing role changes to the audit trail** is spec 007; this spec keeps what 007 will need —
  what was added and removed, by whom and when — available from its operations.
- How the catalogue is kept, how an address declares its rule and how the check is made fast are
  fixed at planning under the constitution; they are deliberately not named here.
