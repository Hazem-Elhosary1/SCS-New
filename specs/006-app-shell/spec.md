# Feature Specification: Console Frame (App Shell)

**Feature Branch**: `006-app-shell`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "for new spec". Taken from row `006-app-shell` of
`docs/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "the two bars, the branch and
language switches, the account menu, the banners, the rules of lists and tables."

**BRD source**: Volume 1 (Clinic Console), Chapter 01, Part A "the shared frame" — §A-2 (side bar
items and what opens each), §A-3 (user preference), §A-4-1 (subscription banner), §A-5-1 to
A-5-10 (flows), §A-6, §A-7 (the contract every list keeps; dialogs, drawers, toasts), §A-8 rules
أ-ق1 to أ-ق27, §A-10; requirements CL-SH-001 to CL-SH-064, CL-SH-187. Volume 0: SH-001, SH-002,
SH-014, SH-054, SH-055, SH-097, SH-098.

**Design source**: Figma file "Smart Clinic OS — Design System & Screens"
(`4HLWEqlna9sbn68276LHYt`). The owner's direction (11 Oct 2026): the frame is drawn around every
console screen of that file. Read so far, on page "✓ Administration › Roles & permissions"
(`5803:4`): the `navbar` instance (`6675:19377`, 1440×64) and the `sidebar` instance
(`6675:19416`, 240 wide) of frame "Roles / Roles List Read Only / RTL" (`6675:19376`), its LTR
twin (`6675:20097`), and the side bar without the administration group in "Roles / No Access"
(`6675:20818`, `6675:21209`).

**Where each state is drawn** (found on 11 Oct 2026 by listing the file's pages; every frame has
an RTL and an LTR twin). Page "✓ Home" (`4160:3`):

| Section | Frames (RTL · LTR) |
|---|---|
| «الإطار ولوحة التحكم — الحالات الأساسية ١» `4343:15038` | Shell / Default `4343:15040` · `4343:16756`; Sidebar Collapsed `4343:16343` · `4343:18059`; Sidebar Group Opened `6538:914228` · `6538:914892` |
| «… الحالات الأساسية ٢» `4343:18543` | Shell / Loading `4343:18545` · `4343:19520`; Load Failed `4343:18839` · `4343:19814`; Offline `4343:19040` · `4343:20015` |
| «لوحة التحكم — بحث جدول اليوم وتصفيته» `4481:15660` | Table Search Active `4481:15662` · `4481:17816`; Table Filter Open `4481:16287` · `4481:18441`; Table Filtered `4481:16929` · `4481:19083`; Table No Results `4481:17410` · `4481:19564` |
| «الإشعارات ومحدّد الفرع وقائمة الحساب» `5999:831796` | Notifications Open `5999:831798` · `5999:834947`; Notifications Empty `5999:832473` · `5999:835622`; Branch Selector `5999:833094` · `5999:836243`; Branch Selector Open `5999:833681` · `5999:836830`; Account Menu Open `5999:834312` · `5999:837461` |
| «لافتات الاشتراك والرئيسية بحسب الصلاحيات» `4384:13884` | Trial Banner `4384:13886` · `4384:16709`; Subscription Expiring `4384:14621` · `4384:17444`; Grace Period `4384:15163` · `4384:17986`; Read Only `4384:15705` · `4384:18528`; Limited Permissions (side bar) `6538:915556` · `6538:916216` |

Components: page "Components — Navigation" (`4228:5`) — Navbar `4252:60501`, Sidebar `4262:651`,
Sidebar Nav Item `4252:60310`, Language Toggle `4262:1111`, Page Header `4252:61267`, Breadcrumb
`4252:60932`, Tab Bar `4252:60835`, Pagination `4252:61024`. Page "Components — Feedback"
(`4228:4`) — Notification Panel `4262:1056`, Subscription Banner `4262:1214`, Dev Environment
Strip `4274:54936`, Empty State `4252:60047`, Skeleton `4252:59877`, Alert `4253:54128`, Tooltip
`4252:59606`. Page "Components — Containers" (`4228:6`) — Menu `4252:61893`, Modal `4252:61695`,
Drawer `4267:617`, Table Row `4267:903`, Table Header Row `4267:1004`, Filter Popover
`4274:54643`.

The Home frames also draw the home page's own content (today's cards and table); that content
is the home page's spec. The support-notice and incident frames of section `5295:16892` belong
to spec 008.

Found at planning: the passing notice is the Alert component's `Style=Toast` variants (360×78,
four intents). The "not in your plan" card has no variant in the components pages; it is
looked for on the module pages when it is built, and otherwise is undrawn.

**Undrawn** (BRD §A-13; built from the drawn components and raised with the owner): the error
state of the notifications panel (CL-SH-187); the pages of «معمل الأسنان» and «نصوص الموافقة»;
the "not available yet" page inside the frame; the "page not found" page; the "not in your plan"
card (no variant of it is in the components pages); and the whole sample list of FR-042, which
is a test page built from the drawn list parts.

**Undrawn, found while building** (11 Oct 2026): the icons of «نصوص الموافقة» (a document was
chosen), «معمل الأسنان» and «ملف الطبيب» — no frame shows these items; and two icons the file
draws differently from the set the product uses — the inbox (a tray with an arrow) and the
ticket (tilted).

**Owner decisions that differ from the frames** (11 Oct 2026): the fold button is at the top of
the side bar, beside the first group's name, with a hairline around it — the frames draw it at
the foot, where the owner found it was not seen; and only one parent of the side bar is open at
a time — opening one closes the other. The design should be brought into line.

**Depends on**: 005-permissions-engine.

## Overview

After signing in, a person today lands on a bare page. This spec gives the console its frame:
one top bar, one side bar, and a content area that every later page is placed in — and the
rules every list, table, dialog and passing notice in the console keeps, so that ninety later
pages look and behave as one product.

Scope follows the owner's choice of 11 Oct 2026, **"frame now, data later"**: every part of the
frame is built as drawn with all its states. A part whose data belongs to a later spec shows
its honest "nothing yet" state — the bell opens an empty panel, a one-branch clinic has no
branch switch, no subscription banner is shown — and that later spec only supplies the data.

**In scope**: the top bar (clinic name, branch switch, language switch, bell, account chip and
menu); the side bar by permission, its groups, order, collapsing and active item; the address
of every destination of the side bar, each opening inside the frame; the environment bar; the
offline banner; the subscription banner as a part with four states; the "not in your plan"
state; what a person keeps between sessions (language, collapsed side bar); the shared parts
and rules of lists, tables, dialogs and toasts; the roles list, for reading, as their first use.

**Out of scope** (each has its own spec): who is told about which event and the content of the
bell (008); branches and which branches a person is given (028); subscription states, plans and
the read-only clinic (012 to 016); a person's name and picture (026); linking an account to a
doctor's file (029); the home page's content; the content of every module; the idle timeout
(settings); the suspended clinic.

## Clarifications

### Session 2026-10-11

- Q: The frame has controls whose data comes from later specs (the bell, the branch switch, the subscription banner, "not in your plan"). How much of them belongs here? → A: Frame now, data later: every part is built as drawn with all its states; a part without data shows its real "nothing yet" state, and the later spec only supplies data.
- Q: Where is the frame drawn? → A: Around every console screen of the main design file; there is no separate link.
- Q: What are the states of the frame built from, given their boards had not been found? → A: From the design: the owner confirmed every screen is in the main file, and the boards were then found by listing its pages — the frame's states are on page "✓ Home" and its parts on the three components pages (table under "Design source"). Each is built from and compared with its frame; nothing is built from BRD wording alone.
- Q: What does the account chip show where the name goes, while accounts have no name (until spec 026)? → A: The account's email address, and its first letter in the round mark. Recorded as a temporary difference from the design; no name field is added here.
- Q: Where are the rules of lists and tables built and verified, when no real list exists yet? → A: On a real screen and a sample one. The roles list at `/administration/roles` is built for reading in place of "not available yet" — its two tables (ready and custom roles) with the role, its holders and "N of 83" — from the shared parts and against its frames; creating, editing, deleting and the role's detail stay with spec 027. Because that list is short and has no toolbar, search, filtering, chips and paging are verified on a sample list that exists outside production only.
- Q: What is the narrowest screen the frame must work on? → A: 1280 wide and up, complete. Between 1024 and 1280 the side bar is folded on its own for a person with no saved choice. Under 1024 the content scrolls sideways and nothing overlaps. There is no design for phones or tablets, and none is built.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every page after sign-in sits in one frame (Priority: P1)

A receptionist signs in. Above is a bar with her clinic's name and her own account; beside the
reading edge is the side bar; the rest is the page. Whatever she opens next — a page that
exists, one that is not built yet, one her role does not open — appears in the same place, and
the bars do not move.

**Why this priority**: every later screen is placed inside this. Without it there is nowhere to
put a page and no way to move between pages.

**Independent Test**: sign in and open the home address, a built destination, a destination not
built yet and one the role does not open; the bars are the same in all four.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** any console address is opened, **Then** the page is
   shown inside the frame: top bar, side bar on the reading side, content.
2. **Given** a destination whose page is not built yet, **When** a person who holds its view
   opens it, **Then** the frame shows its title and a "not available yet" state — not a blank
   page and not the home page.
3. **Given** a destination the person's role does not open, **When** they type its address,
   **Then** the frame shows the "not in your role" state of spec 005 for that destination.
4. **Given** an address that is no destination at all, **When** it is opened, **Then** the frame
   shows a "page not found" state with a way back to «الرئيسية».
5. **Given** nobody is signed in, or the account holds a temporary password, **When** a console
   address is opened, **Then** the answers of specs 003 and 004 apply and no frame is shown.
6. **Given** the pages before sign-in (sign-in, code, forgot, set and change password), **When**
   they are shown, **Then** they keep their own frame from spec 003 and show no console bar.

---

### User Story 2 - The side bar shows what the role opens, in one fixed order (Priority: P1)

A doctor sees «الرئيسية», «الحجوزات», «المرضى» and the rest his role views; he does not see
«الإدارة › المستخدمون». The order of what he sees is the order everyone sees.

**Why this priority**: the side bar is how people reach everything, and SH-055 says a person
does not see what they may not open.

**Independent Test**: sign in with roles holding different view permissions and compare the
side bar with the table of BRD §A-2.

**Acceptance Scenarios**:

1. **Given** the side bar, **When** it is shown to an owner, **Then** it has the label «القائمة»
   with «الرئيسية», «الحجوزات», «المرضى», «الفواتير والتحصيل», «الصندوق الوارد», «التذاكر»,
   «التقارير»; then «إدارة العيادة» with «الإدارة», «إدارة البيانات», «الإعدادات» — in that
   order.
2. **Given** a person without the view permission of a destination, **When** the side bar is
   shown, **Then** that item is absent — not disabled — and the others keep their order.
3. **Given** a person who holds none of a group's items, **When** the side bar is shown,
   **Then** the group and its label are absent.
4. **Given** a person who holds only «عرض التقارير الطبية» of the five report views, **When**
   they open «التقارير», **Then** «التقارير الطبّيّة» is its only item.
5. **Given** any signed-in person, including one with no role, **When** the side bar is shown,
   **Then** «الرئيسية» is there.
6. **Given** the page that is open, **When** the side bar is shown, **Then** its item is marked
   as the active one, and when it is a child its parent is open.
7. **Given** a parent («التقارير», «الإدارة», «إدارة البيانات»), **When** it is pressed in the
   expanded bar, **Then** its items open, and close when pressed again.
8. **Given** «معمل الأسنان», **When** the clinic is not a dental clinic, **Then** the item is
   absent whatever the role holds.
9. **Given** a role's permissions changed (spec 005), **When** the person's screen next learns
   what they hold, **Then** the side bar follows without signing in again.

---

### User Story 3 - The account chip says who is signed in, and its menu signs out (Priority: P1)

At the end of the top bar is the person's chip: a mark, a name and the name of their role.
Pressing it opens a small menu: «الملف والاشتراك», a separator, «تسجيل الخروج». Signing out ends
this session and shows the sign-in form.

**Why this priority**: today sign-out lives on a temporary page that this spec removes.

**Independent Test**: sign in, read the chip, open the menu, sign out, press the browser's back.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** the top bar is shown, **Then** the chip carries their
   name and the name of their role, as spec 005 names it; with no role, the name alone.
2. **Given** the chip, **When** it is pressed, **Then** the menu opens under it, aligned to its
   far edge, the chip is drawn pressed, and pressing outside closes it with nothing changed.
3. **Given** the menu, **When** it is read, **Then** it holds «الملف والاشتراك», then «ملف
   الطبيب» only for an account linked to a doctor's file, then a separator, then «تسجيل الخروج»
   as an ordinary item — not red, and with no confirmation.
4. **Given** «تسجيل الخروج», **When** it is pressed, **Then** this session alone ends, the plain
   sign-in form is shown with no notice, and the browser's back shows no data.
5. **Given** the clinic's name, **When** the top bar is shown, **Then** it is at the start of
   the bar in the language of the page.

---

### User Story 4 - The language switch turns the whole console, and is remembered (Priority: P2)

The bar has a pill «عربي · EN». Pressing the other half turns every word and the direction of
the page at once, on the same page in the same state. The next time she signs in, on any
device, the console is in the language she left it in.

**Why this priority**: SH-002 — two languages, complete; and the choice belongs to the person,
not to the browser.

**Independent Test**: switch, reload, sign out and in again in another browser.

**Acceptance Scenarios**:

1. **Given** the console in Arabic, **When** «EN» is pressed, **Then** the page is in English
   from left to right at once, without a reload, on the same page.
2. **Given** a person who switched to English, **When** they sign in again in another browser,
   **Then** the console is in English.
3. **Given** a person with no saved choice who chose a language on the sign-in form, **When**
   they sign in, **Then** that language becomes their saved choice.
4. **Given** a person with a saved choice, **When** they sign in from a browser set to the
   other language, **Then** their saved choice wins.
5. **Given** the switch, **When** it is shown, **Then** the active half names the current
   language, and there are no flags.

---

### User Story 5 - The side bar folds to icons (Priority: P2)

A button at the top of the side bar, beside the first group's name, folds it to a narrow column of icons, giving the page more
room; each icon then has a tooltip with its name. Pressing the icon of a group unfolds the bar
with that group open. The choice is kept between sessions.

**Why this priority**: tables are wide; a clinic's reception screen is not.

**Independent Test**: fold, hover, press a group icon, press an ordinary icon, reload, sign in
again.

**Acceptance Scenarios**:

1. **Given** the expanded bar, **When** the fold button is pressed, **Then** the bar narrows
   from 240 to 72, shows icons alone, and the content widens.
2. **Given** the folded bar, **When** an icon is hovered or focused, **Then** a tooltip names
   its item.
3. **Given** the folded bar, **When** the icon of «التقارير», «الإدارة» or «إدارة البيانات» is
   pressed, **Then** the bar unfolds with that group open.
4. **Given** the folded bar, **When** any other icon is pressed, **Then** its page opens and
   the bar stays folded.
5. **Given** either state, **When** the bar is shown, **Then** the active item is marked.
6. **Given** a person who folded the bar, **When** they sign in again, **Then** it is folded.

---

### User Story 6 - The frame says what is going on: environment, connection, plan, subscription (Priority: P2)

On a test environment a thin bar across the top says so, on every screen. When the connection
drops a warning appears above the content and every control that changes data is disabled until
it returns. A destination the clinic's plan does not include opens to a card that names the plan
as the reason. A banner under the top bar tells of a trial ending, a renewal coming, a grace
period, or a read-only account.

**Why this priority**: each of these stops people from doing the wrong thing or wondering why
they cannot do the right one.

**Independent Test**: run outside production; cut the connection; give each part its state.

**Acceptance Scenarios**:

1. **Given** an environment that is not production, **When** any screen is shown — before or
   after sign-in — **Then** the environment bar is there; in production it never is.
2. **Given** a loaded page, **When** the connection is lost, **Then** a warning above the
   content reads «انقطع الاتصال بالإنترنت — ما تراه آخر نسخة محمَّلة، وما يغيّر البيانات معطَّل
   حتى يعود الاتصال.», every control that changes data is drawn disabled, and reading, search
   and filtering go on working on what was loaded.
3. **Given** the offline warning, **When** the connection returns, **Then** the warning goes,
   the controls return, and the current page is read again. Nothing done offline was queued.
4. **Given** a destination marked "not in the plan", **When** a person who holds its view opens
   it, **Then** its item is still in the side bar, and the page shows its title and a card that
   names the plan — not a permission — with a link to «الملف والاشتراك».
5. **Given** the subscription banner is given one of its four states, **When** any page is
   shown, **Then** one banner spans the width under the top bar, above any other notice, with
   that state's tone, wording and action.
6. **Given** a banner that can be closed (trial, expiring, grace), **When** a person closes it,
   **Then** it is gone on every page for that person until the next day; the read-only banner
   has no close button.
7. **Given** a person without «عرض الاشتراك», **When** a banner is shown, **Then** its action is
   absent and the banner stays.
8. **Given** no subscription state is supplied — as now — **When** any page is shown, **Then**
   there is no banner.
9. **Given** a session that has ended, **When** the person does anything, **Then** the screen is
   cleared and the sign-in form shows «انتهت الجلسة. الدخول من جديد للمتابعة.» (spec 003).

---

### User Story 7 - The bell and the branch switch are in their places, saying "nothing yet" (Priority: P2)

The bell is in the top bar. Pressing it opens the panel under it: «لا إشعارات جديدة». The branch
switch is not shown, because the clinic has one branch. When specs 008 and 028 arrive they fill
these parts; nothing in the frame is rebuilt.

**Why this priority**: the owner chose to build the frame whole, so later specs add data, not
layout.

**Independent Test**: press the bell with no notifications; give the parts sample data in a
test and see each state.

**Acceptance Scenarios**:

1. **Given** no notifications — as now — **When** the bell is pressed, **Then** it is drawn
   pressed, a panel 360 wide opens under it at the far reading edge with the heading
   «الإشعارات», the bell mark, the title «لا إشعارات جديدة» and a line saying what will appear;
   there is no «تعليم الكل كمقروء», no "view all" link, and no dot on the bell.
2. **Given** the panel, **When** a press lands outside it, **Then** it closes.
3. **Given** the panel is supplied with items, **When** it opens, **Then** the newest is on top,
   unread items are tinted, each has its kind's icon, title, text and relative time, the list
   scrolls inside the panel, «تعليم الكل كمقروء» is in the heading, and the bell has a dot while
   one is unread.
4. **Given** the panel cannot load, **When** it opens, **Then** it shows an error state with
   «إعادة المحاولة» inside the panel.
5. **Given** a clinic with one branch — as every clinic now — **When** the top bar is shown,
   **Then** there is no branch switch.
6. **Given** the branch switch is supplied with more than one branch for the person, **When**
   it is pressed, **Then** it is drawn pressed and a menu opens under it: «كل الفروع», then the
   person's branches, the active one marked; choosing one closes the menu and the button carries
   its name. With exactly one branch the button carries that name and offers no other.
7. **Given** «الصندوق الوارد» in the side bar, **When** an unread count is supplied, **Then**
   the expanded bar shows it beside the item; with none supplied, nothing is shown.

---

### User Story 8 - Every list and table keeps one contract (Priority: P2)

Every list in the console is built from the same parts in the same order: heading, toolbar,
applied filters, rows, footer. Search is at the reading start, «تصفية» with its word at the
other end. The footer counts at the start and pages at the end. Empty, no results, loading and
error each look one way everywhere. A change is followed by a passing notice beside the page's
buttons.

**Why this priority**: there are dozens of lists. Decided once here, none is decided again.

**Independent Test**: open the roles list as an owner and as a person with the view alone and
lay it beside its frames; on the sample list, go through every state and rule of BRD §A-7.

**Acceptance Scenarios**:

1. **Given** a list with a toolbar, **When** it is shown, **Then** the order is heading, toolbar,
   applied chips (only when something is applied), rows, footer; search is at the reading start
   with a fixed width, «تصفية» with its word and icon at the other end, and nothing else is in
   that row.
2. **Given** «تصفية», **When** it is pressed, **Then** it is drawn pressed and its panel opens
   downward over the rows it filters, covering no notice and no page numbers.
3. **Given** filters are applied, **When** the list is shown, **Then** each appears once, as a
   chip «‹الوجه›: ‹القيمة› ×» in a row under the toolbar starting under the search, with «مسح
   الكل» at the far end; the row is absent when nothing is applied.
4. **Given** a table that fits one page, **When** it is shown, **Then** the footer has the count
   alone; with more pages the count is at the start and the paging at the end, «السابق» disabled
   on the first page and «التالي» on the last.
5. **Given** a list with nothing in it, **When** it is shown, **Then** there is no toolbar and
   no «تصدير»: a title, a message, and the create action for a person who holds it.
6. **Given** a search or filter that matches nothing, **When** the list is shown, **Then** the
   toolbar stays and the way out is the × of the search or «مسح الكل».
7. **Given** loading fails, **When** the list is shown, **Then** the page and toolbar stay, the
   table area alone shows the error with «إعادة المحاولة», and numbers that came from the failed
   request show a dash — never the last loaded values.
8. **Given** a list is loading, **When** it is shown, **Then** grey skeletons stand in place of
   cards and rows.
9. **Given** a row, **When** it is pressed, **Then** its record opens; "open" is never repeated
   inside the row's «⋯» menu, which exists only when there are other actions, follows the
   record's state and the person's permissions, and draws a dangerous action red.
10. **Given** an action that changed something, **When** it succeeds, **Then** a passing notice
    appears in the corner at the reading end, beside the page's header buttons and not over
    them, and the change is visible on the page.
11. **Given** an action that cannot be undone or touches someone else, **When** it is asked for,
    **Then** a confirmation names the action on its button, which is disabled until what it
    needs is chosen; whatever opened a dialog, drawer or menu stays marked while it is open.
12. **Given** a person with the view alone, **When** a list is shown, **Then** rows open their
    records and there is no «⋯» and no control that writes.
13. **Given** a holder of «عرض الأدوار والصلاحيات», **When** they open the roles destination,
    **Then** the ready roles are listed first — «مالك العيادة», «مدير العيادة», «طبيب» — then
    the clinic's own roles, each with its description, the number of accounts that hold it and
    "N of 83"; with no custom role, that list shows its empty state.
14. **Given** the roles list, **When** it is loading or fails to load, **Then** it shows the
    shared skeleton or the shared error with «إعادة المحاولة», and the counts in the line under
    the title show a dash.

---

### User Story 9 - The frame is complete in two languages and as drawn (Priority: P3)

Every part of the frame exists in Arabic and English, mirrored, with numbers in Arabic-Indic
digits in Arabic and Western digits in English, and matches its frames in the design file.

**Why this priority**: constitution I and VI.

**Independent Test**: lay each part and state, in each language, beside its frame.

**Acceptance Scenarios**:

1. **Given** the frame in Arabic, **When** it is laid beside the RTL frame, **Then** the top bar
   (height, clinic mark and name, switches, bell, chip) and the side bar (width, labels, items,
   icons, active mark, fold button) match.
2. **Given** the same in English, **When** it is laid beside the LTR frame, **Then** it matches,
   mirrored, with the English names of every item.
3. **Given** any number the frame shows, **When** the page is Arabic, **Then** its digits are
   Arabic-Indic; identifiers and email addresses keep their Latin letters in both languages.
4. **Given** every control of the frame, **When** it is used with a keyboard alone, **Then** it
   can be reached, opened, used and closed, and assistive technology is told what is open.

---

### Edge Cases

- A person whose role opens nothing but «الرئيسية»: the side bar shows that one item under
  «القائمة»; «إدارة العيادة» is absent.
- A person with no role: the same, and the chip shows no role name.
- A person is on a page when their role loses its view: the next refusal by the server shows
  the "not in your role" state (spec 005) and the side bar drops the item.
- The side bar is folded and the open page is a child of a group: the group's icon is marked.
- A very long clinic name, account name or role name: it is cut with an ellipsis in the bar,
  and read whole by assistive technology and on hover.
- The window is between 1024 and 1280 wide and the person never chose: the side bar is folded.
  A person who chose "expanded" keeps it, and the content scrolls sideways if it must.
- The window is under 1024 wide: the content scrolls sideways; the bars do not overlap it and
  nothing is cut off. Phones and tablets are not designed and not built.
- Two menus of the top bar: opening one closes the other. Escape closes whichever is open and
  returns focus to what opened it.
- The language is switched while a menu, the bell panel or a dialog is open: it stays open, in
  the other language.
- Saving the language or the folded state fails (no connection): the change still applies on
  this screen, and is saved by the next successful change; no error is shown for it.
- The connection drops while a form is half filled: what was typed stays; the submit control is
  disabled with the offline warning as its reason.
- The browser reports "online" while the platform cannot be reached: a failed request shows
  its own error; the offline warning is for a lost connection only.
- The subscription banner and the offline warning at once: the subscription banner is above.
- A banner closed just before midnight in Cairo: it returns after midnight, Cairo time.
- A destination is both "not in the plan" and not in the person's role: the role's answer is
  shown; a person who may not open a page is not told about the plan.
- An address under a parent that is not itself a page (for example the reports parent): it
  opens the first of its items the person may open; with none, the "not in your role" state.
- The environment bar and the pages before sign-in: the bar is shown there too.

## Requirements *(mandatory)*

### Functional Requirements

**The frame**

- **FR-001**: Every page of the console after sign-in MUST be shown inside one frame: a top
  bar, a side bar on the reading side, and the content (CL-SH-001). The pages before sign-in
  keep the frame of spec 003.
- **FR-002**: The top bar MUST carry, from the reading start: the clinic's mark and name, the
  branch switch (when it applies), then at the far end the language switch, the bell, and the
  account chip (CL-SH-002).
- **FR-003**: Every destination of the side bar and of the account menu MUST have an address,
  fixed by this spec, that opens inside the frame: its page when built; a "not available yet"
  state for a person who may open it; the "not in your role" state of spec 005 for a person who
  may not. An address that is no destination shows "page not found" inside the frame.
- **FR-004**: The temporary landing page of spec 003 and its sign-out button are replaced by
  the frame; «الرئيسية» shows a "not available yet" state inside it until the home page's spec.
  The platform-status panel of spec 001 that the landing page carried moves to a page that
  exists outside production only, beside the sample list.

**The side bar**

- **FR-005**: The side bar MUST have two labelled groups, «القائمة» and «إدارة العيادة», with
  the items, parents and children of BRD §A-2 in that order (CL-SH-003): «الرئيسية», «الحجوزات»,
  «المرضى», «الفواتير والتحصيل», «الصندوق الوارد», «التذاكر», «التقارير» (five children);
  «الإدارة» (three), «إدارة البيانات» (four), «الإعدادات»; and «معمل الأسنان».
- **FR-006**: An item MUST be shown only to a person who holds the view permission of its
  destination, using the catalogue of spec 005; an item not shown is absent, never disabled
  (CL-SH-004, أ-ق1). «الرئيسية» is shown to everyone signed in (CL-SH-007).
- **FR-007**: A parent MUST be shown when the person may open at least one of its children and
  MUST list only those; a group with no item left is absent with its label (أ-ق2, أ-ق3,
  CL-SH-005).
- **FR-008**: The order of items MUST be the same for everyone; an absent item does not reorder
  the rest (CL-SH-006).
- **FR-009**: «معمل الأسنان» MUST be shown only in a dental clinic and to a holder of «عرض حالات
  المعمل» (CL-SH-008); «نصوص الموافقة» under «إدارة البيانات» to a holder of «عرض نصوص الموافقة»
  (CL-SH-009).
- **FR-010**: The active item MUST be marked, expanded and folded; a parent opens and closes
  on press; the parent of the active item is open (CL-SH-014). One parent is open at a time:
  opening one closes the other, and a parent closed over the open page carries the mark itself
  (owner decisions, 11 Oct 2026).
- **FR-011**: The side bar MUST fold from 240 to 72 with a button at its top, beside the first
  group's name and drawn as a button (owner decision, 11 Oct 2026; the frames draw it at the
  foot), showing icons
  alone, each with a tooltip of its name; pressing a group's icon unfolds the bar with that
  group open; other icons open their pages and leave it folded (CL-SH-011 to CL-SH-013).
- **FR-012**: A destination marked as not included in the clinic's plan MUST stay in the side
  bar (CL-SH-010, أ-ق5).
- **FR-013**: «الصندوق الوارد» MUST be able to carry an unread count in the expanded bar; the
  count is supplied by the inbox's spec (CL-SH-015).

**The account chip and menu**

- **FR-014**: The chip MUST show a mark, the person's name — until spec 026 gives accounts a
  name, their email address, with its first letter in the mark — and the name of their role as
  spec 005 gives it — a ready role in the language of the page until a clinic renames it
  (CL-SH-021). It opens a menu under it, aligned to its far edge (CL-SH-022).
- **FR-015**: The menu MUST hold, in order: «الملف والاشتراك»; «ملف الطبيب» only for an account
  linked to a doctor's file (CL-SH-023); a separator; «تسجيل الخروج».
- **FR-016**: «تسجيل الخروج» MUST end the current session alone and show the plain sign-in form
  with no notice; it is an ordinary item with no confirmation, and it is always available
  (CL-SH-024, CL-SH-025).

**What a person keeps**

- **FR-017**: The platform MUST keep for each person, between sessions and across browsers:
  the language of the console, and whether the side bar is folded (أ-ق25).
- **FR-018**: The language switch MUST turn every text and the direction of the page at once,
  on the same page in the same state, and save the choice (CL-SH-016). Its active half names
  the current language. It is every person's own; no permission grants or forbids it.
- **FR-019**: A person with no saved language MUST get the language chosen on the sign-in form
  as their saved choice at sign-in; a saved choice wins over the browser's.

**Banners and states of the frame**

- **FR-020**: The environment bar MUST be shown on every screen of every environment that is
  not production — before and after sign-in — and never in production (CL-SH-045, أ-ق20).
- **FR-021**: When the connection is lost the frame MUST show the warning of BRD §A-5-8 above
  the content and every control that changes data MUST be disabled; reading, search and
  filtering continue on what was loaded; nothing is queued. When it returns the warning goes,
  the controls return and the current page is read again (CL-SH-042, CL-SH-043).
- **FR-022**: The frame MUST provide the "not in your plan" state: the page's title and a card
  that names the plan as the reason, with a link to «الملف والاشتراك» (CL-SH-010). Which
  destinations a plan excludes is supplied by the plans' spec; none is excluded now.
- **FR-023**: The frame MUST provide the subscription banner with its four states — trial,
  expiring, grace, read-only — each with its tone, wording with the number of days, and action
  that opens «الملف والاشتراك» (BRD §A-4-1, CL-SH-036, CL-SH-037). The state is supplied by the
  subscription's spec; with none supplied there is no banner.
- **FR-024**: Only one subscription banner is shown at a time, above any other notice. The
  read-only banner cannot be closed; the others are closed by a person for themselves until the
  next day, Cairo time, on every page (CL-SH-038, CL-SH-039). Its action is absent for a person
  without «عرض الاشتراك» (CL-SH-040).
- **FR-025**: Every page MUST be able to show the six states of SH-097 — loading, empty, error,
  offline, view only, not in the plan — from shared parts; a page never draws its own
  (CL-SH-063). There is one shared part per state: the state component that specs 001 and 005
  already ship is rebuilt on the drawn parts of this spec, not kept beside them.

**The bell and the branch switch**

- **FR-026**: The bell MUST be in the top bar and open a panel 360 wide under it at the far
  reading edge, drawn pressed while open (CL-SH-027). The panel is the whole list and scrolls;
  there is no "all notifications" page or link (CL-SH-028).
- **FR-027**: With no notifications the panel MUST show the bell mark, «لا إشعارات جديدة» and a
  line saying what will appear, with no «تعليم الكل كمقروء» and no dot on the bell (CL-SH-030).
  When loading fails it shows an error with «إعادة المحاولة» inside the panel (CL-SH-187).
- **FR-028**: The panel MUST be able to show items — newest first, unread tinted, each with the
  icon of its kind (seven kinds), title, text of at most two lines and relative time — with
  «تعليم الكل كمقروء», and the bell a dot while one is unread (CL-SH-026, CL-SH-029, CL-SH-032).
  Items, their destinations and marking as read are supplied by spec 008; none exists now.
- **FR-029**: The branch switch MUST be absent for a clinic with one branch. For a clinic with
  more it is on every screen after sign-in and lists «كل الفروع» then the person's own
  branches with the active one marked (CL-SH-017, CL-SH-018). Branches, the saved choice and
  what the choice narrows are supplied by spec 028; every clinic has one branch now.

**Lists, tables, dialogs and passing notices**

- **FR-030**: The shared library MUST provide the parts of a list in one anatomy — heading,
  toolbar, applied chips, rows, footer — and every list of the console MUST be built from them
  (CL-SH-046).
- **FR-031**: The toolbar MUST hold the search at the reading start, with a fixed width and a
  placeholder that names what is searched without a verb or dots, and «تصفية» with its word and
  icon at the other end, with no count badge — and nothing else (CL-SH-047).
- **FR-032**: The filter panel MUST open downward over the rows it filters, covering no notice
  and no page numbers, with its button drawn pressed (CL-SH-049). What is applied appears in one
  place: a row of chips «‹الوجه›: ‹القيمة› ×» under the toolbar with «مسح الكل» at the far end
  (CL-SH-048). No filter exists without a visible column for it (CL-SH-050). The branch is never
  a filter or a chip (CL-SH-020). An exclusive split with counts is tabs above the toolbar.
- **FR-033**: The footer of every table MUST be one row: the count at the start, the paging at
  the end; «السابق» disabled on the first page and «التالي» on the last; one page shows the
  count alone (CL-SH-051, CL-SH-052).
- **FR-034**: A row MUST open its record on press; the «⋯» menu exists only for other actions,
  follows the record's state and the person's permissions, and draws a dangerous action red
  (CL-SH-053). With the view alone there is no «⋯» and no writing control.
- **FR-035**: The states of a list MUST be the same everywhere: empty — no toolbar, no export,
  a title, a message and the create action for its holder; no results — the toolbar stays;
  error — the table area alone, with «إعادة المحاولة», and a dash for numbers of the failed
  request; loading — grey skeletons (CL-SH-054 to CL-SH-056). A control with nothing to act on
  is absent.
- **FR-036**: Whatever opens a dialog, drawer or menu MUST stay marked while it is open
  (CL-SH-057). Every action that changes something MUST be followed by a passing notice and a
  visible change (CL-SH-058); the notice sits in the corner at the reading end, beside the
  page's header buttons and never over the control that caused it (CL-SH-059).
- **FR-037**: A confirmation MUST be used for what cannot be undone or touches someone else;
  its button names the action and is disabled until what it needs is chosen (CL-SH-060). A
  form's main button is enabled and checks on submit; a field's error replaces its hint
  (CL-SH-061). What is known before submitting is drawn on the control, disabled with its
  reason (CL-SH-062).
- **FR-038**: The roles destination MUST show the clinic's roles for reading, built from these
  parts as drawn in "Roles / Roles List Read Only" (`6675:19376`, `6675:20097`) and the roles
  list frames of the same page: two short lists — ready roles and custom roles — each row with
  the role's name and one-line description, how many accounts hold it, and "N of 83" counted
  from the catalogue; the line under the page title counts the roles and the permissions in
  digits of the page's language, with the Arabic noun following the number's rule (٣ أدوار، ١١
  دورًا) — the frame writes the numbers as words, which a count that changes cannot do. A
  person without «إدارة الأدوار والصلاحيات» sees it under the «للعرض فقط» notice of spec 005. It
  has no toolbar (a short list that is read, not browsed), rows do not open anything yet, and
  there is no control that creates, edits or deletes: those and the role's detail are spec 027.
- **FR-042**: Search, filtering, applied chips, tabs, paging, the row menu, every list state,
  the passing notice and the confirmation MUST be shown working on a sample list with made-up
  rows that exists outside production only, so each rule can be verified before the first long
  list arrives. It is never reachable in production.

**Screen width**

- **FR-043**: The frame MUST work complete at 1280 wide and wider. Between 1024 and 1280 the
  side bar MUST start folded for a person who has no saved choice; a saved choice is kept.
  Under 1024 the content scrolls sideways and no part of the frame overlaps another.

**Language, numbers and reach**

- **FR-039**: Every part of the frame MUST exist in Arabic and English, mirrored. Numbers are
  Arabic-Indic in Arabic and Western in English; identifiers and email addresses keep Latin
  letters in both (CL-SH-064, أ-ق18). Times are Cairo time and never break from their mark
  across two lines (أ-ق19).
- **FR-040**: Every control of the frame MUST be usable with a keyboard alone, close on Escape
  with focus returned to what opened it, and tell assistive technology what is open and which
  item is active.
- **FR-041**: Wording follows SH-098: formal Arabic, a control named by a verbal noun, no
  third-person pronoun for a person.

### Key Entities

- **Destination**: a place the side bar or the account menu leads to — its name in two
  languages, its address, its icon, its place in the order, its parent if any, and the view
  permission that opens it (or none). Fixed by the product, the same for every clinic.
- **User preference**: what one person keeps between sessions — the language of the console and
  whether the side bar is folded. One per account.
- **Subscription banner state**: one of trial, expiring, grace, read-only, with the number of
  days; supplied from outside this spec.
- **Banner dismissal**: that a person closed a given banner state on a given day.
- **Notification item** (shape only): kind, title, text, time, read or not, destination;
  supplied by spec 008.
- **Branch choice** (shape only): "all branches" or one of the person's branches; supplied by
  spec 028.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of console addresses open inside the frame; 0 pages after sign-in are shown
  without it, and 0 pages before sign-in are shown with it.
- **SC-002**: For every one of the 20 destinations of the side bar and its 3 parents, a person who may open it sees
  it and a person who does not sees no trace of it, in 100% of automated checks; the order of
  visible items is the same for every role tested.
- **SC-003**: A person reaches any destination they may open in at most 2 presses from any
  page, and signs out in 2.
- **SC-004**: Switching language changes every visible text and the direction in under one
  second without a reload, and 100% of the frame's texts exist in both languages.
- **SC-005**: A person's language and folded side bar are the same after signing out and in
  again in a different browser, in 100% of checks.
- **SC-006**: Within 3 seconds of losing the connection the warning is shown and 100% of
  controls that change data on the sample page are disabled; within 3 seconds of its return
  they are enabled again.
- **SC-007**: Each part and state of the frame matches its design frame in Arabic and in
  English when laid side by side at the frame's size: bar heights, side bar widths (240 and
  72), panel width (360), wording, icons and tones.
- **SC-008**: A later spec adds a destination's page, supplies notifications, branches or a
  subscription state, or builds a list, without changing the frame.
- **SC-009**: Every control of the frame can be operated with the keyboard alone.
- **SC-010**: At 1280, 1440 and 1920 wide, 0 parts of the frame overlap, are cut off, or need
  sideways scrolling; at 1024 the side bar is folded for a person with no saved choice.

## Assumptions

- **The person's name** (owner decision, 11 Oct 2026). Accounts have no name until spec 026.
  Until then the chip shows the account's email address where the name goes and its first
  letter in the mark; the design draws a name, so this is recorded as a temporary difference.
- **The clinic's language.** A clinic has no language of its own yet, so a new account's first
  language is Arabic unless the person chose one on the sign-in form.
- **A dental clinic.** A clinic has no speciality yet (SH-012 arrives with clinic creation), so
  «معمل الأسنان» is defined and never shown until then.
- **«ملف الطبيب».** No account is linked to a doctor's file until spec 029, so the item is
  defined and never shown until then.
- **«الملف والاشتراك» is shown to everyone** in the account menu; its sections are guarded by
  their own permissions in the subscription's spec. Until then it opens "not available yet".
- **Addresses.** This spec fixes the console's addresses; `/administration/roles` of spec 005
  stays. The others are chosen at planning, in English, one per destination.
- **The roles list** (owner decision, 11 Oct 2026) is the first real list. The design's counts
  still say 79; the product counts from the catalogue (83). The number of holders is new
  information the platform must supply for each role.
- **The sample list of FR-042** covers what the roles list cannot show. It is removed or
  replaced when a long list exists. It holds made-up rows and no clinic data.
- **Read-only clinic, suspended clinic, plan entitlements, idle timeout** are other specs; this
  spec gives them the places where their effects are shown.
- **Where the bell's wording for the empty panel's second line is open** (BRD أ-م6), the
  library's version is used: «سيظهر هنا ما يخصّك من تذاكر وفواتير واشتراك.»
- **One word for the banner's action** (BRD أ-م7): the three renewing states use «تجديد
  الاشتراك»; the trial uses «اختيار باقة».
- **The environment bar's wording** (BRD أ-م15) avoids «تجريبية», which also names the trial:
  it names the environment — for example «بيئة اختبار — البيانات ليست حقيقية». To be confirmed.
- **The folded side bar drops the inbox count** with no substitute on the icon (BRD أ-م14).
- **Phones and tablets** are not designed and not built (owner decision, 11 Oct 2026); the
  design draws 1440, and 1280 is the narrowest width the frame is complete at.
- How preferences are stored, how a later spec supplies data to a part, and the addresses
  themselves are fixed at planning under the constitution; they are deliberately not named here.
