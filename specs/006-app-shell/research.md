# Research: Console Frame (App Shell)

**Date**: 2026-10-11 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Decisions taken while planning. Nothing is left as "needs clarification"; what the owner still
has to confirm is at the end.

What exists before this spec, read from the code: after sign-in every address falls to one bare
landing page with a sign-out button; `/administration/roles` (spec 005) is a page on its own
canvas with no bars. The shared library has a banner, a dialog, a state component, an icon set
of 15 and the no-access card. `LanguageService` keeps the language in the browser only.
`OnlineService` knows whether there is a connection. The runtime configuration carries the
environment's name. `@angular/cdk` is **not** installed.

What was read from the design file (page "✓ Home" `4160:3` and the components pages), by
structure and by text in both languages:

| Part | Measured |
|---|---|
| Environment strip | 1440×32, above everything; «بيئة تجريبية — البيانات ليست حقيقية» / "Test environment — data is not real" |
| Top bar | 1440×64. Identity side: logo 36, clinic name, divider, branch button (123×32). Actions side: language pill 86×32, bell 40×40, divider, user chip (name, role, round mark with initials) |
| Subscription banner | 1440×48, between the top bar and the body |
| Side bar | 240 wide (72 folded), items 215×40 with a 4 px gap, group labels, a 32×32 fold button at the foot |
| Menus | open 4 px under the top bar: branch menu 240 wide, account menu 240 wide; notifications panel 360 wide |
| Content | 32 px side padding, 24 px top; page header, then cards |
| Offline | an alert 50 high inside the content, under the page header — not a strip across the frame |
| Toast | the Alert component's `Style=Toast`, 360×78, four intents |

## R1 — One frame component, with the pages as its children

**Decision**: a `ShellComponent` in the clinic app holds the environment strip, the top bar, the
subscription banner, the side bar and a router outlet. Every address after sign-in is a child
route of it; the pages before sign-in stay outside (FR-001). The session guard moves to the
frame's route.

**Rationale**: the bars are created once and never re-created while moving between pages, so
an open menu, the folded state and focus survive navigation.

## R2 — One table of destinations drives the side bar, the routes and the tests

**Decision**: `destinations.ts` lists every destination once: key, parent, address, icon, the
two names' translation key, the view permission (or none), and what kind of clinic it needs.
The side bar is drawn from it; the routes are generated from it; the tests walk it.

For each destination the generated routes are: its page (when a later spec registers one) or
the "not available yet" page, guarded by `holds(view)` from spec 005; then the no-access page on
the same path. A parent's address redirects to the first child the person may open.

Addresses (FR-003), fixed here:

| Destination | Address | View permission |
|---|---|---|
| الرئيسية | `/` | none |
| الحجوزات | `/bookings` | `bookings.view` |
| المرضى | `/patients` | `patients.view` |
| الفواتير والتحصيل | `/invoices` | `money.view` |
| الصندوق الوارد | `/inbox` | `inbox.view` |
| التذاكر | `/tickets` | `tickets.view` |
| التقارير › المالية · الطبية · سجل الأنشطة والأمان · الملخص الأسبوعي · تقييمات المرضى | `/reports/financial` · `clinical` · `activity` · `weekly` · `feedback` | `reports.financial` · `.clinical` · `.activity` · `.weekly` · `.feedback` |
| الإدارة › المستخدمون · الأدوار والصلاحيات · الرخص والتغطية | `/administration/users` · `roles` · `licences` | `admin.users.view` · `admin.roles.view` · `admin.licences.view` |
| إدارة البيانات › الأطباء · الفروع · الخدمات والأسعار · نصوص الموافقة | `/data/doctors` · `branches` · `services` · `consents` | `data.doctors.view` · `data.branches.view` · `data.services.view` · `data.consents.view` |
| الإعدادات | `/settings` | `settings.view` |
| معمل الأسنان | `/dental-lab` | `patients.lab.view`, dental clinics only |
| الملف والاشتراك (account menu) | `/account/subscription` | none to open; its sections are guarded later |
| ملف الطبيب (account menu) | `/account/doctor-profile` | none; linked accounts only |

20 destinations in the side bar under 3 parents, 2 in the account menu.

**Rationale**: one list cannot disagree with itself. A later spec adds a page by registering a
component for a key — it does not touch the frame (SC-008).

## R3 — What a person keeps: one row per account

**Decision**: a clinic-owned table `identity.StaffPreference` (one row per account): language,
side bar folded, and the subscription banner closed and on which day. Read with the session
(`GET /v1/auth/session` gains `preferences`), written by `PUT /v1/account/preferences` — the
third own-account route, added to the written list of spec 005.

The day a banner was closed is taken by the server, in Cairo time (FR-024).

**Rationale**: it rides on the answer the client already asks for at sign-in and every ten
minutes; no extra request. It belongs to Identity because it is a fact about an account.

**Alternatives considered**: browser storage — does not follow the person to another browser
(SC-005). A row per setting — three settings do not need a key-value table.

## R4 — Language at sign-in

**Decision**: when the session answer arrives: a saved language is applied and written to the
browser; with none saved, the browser's current language is sent as the first saved choice
(FR-019). Switching applies at once, then saves; a failed save is retried with the next change
and never shown (spec, Edge Cases). The sign-in pages keep using the browser's value.

## R5 — Parts that wait for data: sources with a "nothing yet" default

**Decision**: each part of the frame that a later spec fills reads from a small source in the
core library, registered with a default that says "nothing":

| Source | Default now | Filled by |
|---|---|---|
| `NotificationsSource` — items, unread count, loading/failed, mark all read, open an item | no items | 008 |
| `BranchesSource` — the person's branches, the choice, choose | one branch → the switch is absent | 028 |
| `SubscriptionSource` — banner state and days, or none | none | 012–016 |
| `PlanSource` — is a destination outside the plan | never | 012 |
| `InboxCountSource` — unread count | none | inbox |
| `ClinicTraitsSource` — dental clinic; account linked to a doctor | no, no | 015, 029 |

A later spec provides its own implementation of the source; the frame's components do not
change. Tests of this spec provide fake sources to show every state (US7).

**Rationale**: the owner's "frame now, data later". It also keeps the frame from knowing where
any of this data comes from.

**How a test gives a source its data** (added during implementation): outside production the
clinic app reads `window.__scsSample` once, before it starts, and answers the six sources from
it (`projects/clinic/src/app/dev/sample-sources.ts`; `sampleSources(page, {…})` in the browser
tests). In production the name is never read — `shell-banners.spec.ts` checks that a sample set
there changes nothing. It is a tool for tests and for people building the product, not a way
for a later spec to supply data: that is still done by providing the source.

## R6 — Overlays: add the component kit the constitution names

**Decision**: add `@angular/cdk` (the version matching Angular 22) and build the menus, the
notifications panel, the filter popover, tooltips and focus handling on its overlay, menu and
a11y parts. The existing dialog keeps working and is moved onto the same base only if needed.

**Rationale**: the constitution's technology line is "Tailwind + Angular CDK, own components".
Positioning under a button in both directions, closing on outside press and Escape, returning
focus and trapping it are exactly what it provides; hand-written versions of these are where
accessibility bugs live (FR-040).

## R7 — Offline: one directive marks what changes data

**Decision**: a directive `scsWrite` on any control that changes data. While `OnlineService`
says offline, the control is disabled and named as disabled for the reason of the offline
alert. The frame shows the alert inside the content, under the page header, as drawn. When the
connection returns the current route's data is read again (the page re-runs its load).

The same directive is the place where the read-only clinic (spec 016) will disable writes.

**Rationale**: "every control that changes data" has to be a property of the control, said once
where it is written — not a list kept somewhere else (CL-SH-042).

## R8 — Lists and tables: parts in the shared library, rules in a contract

**Decision**: the shared library gains `scs-page-header`, `scs-card`, `scs-list-toolbar`,
`scs-filter-popover`, `scs-applied-chips`, `scs-tabs`, table styles (header row, row, cell),
`scs-table-footer` with pagination, `scs-row-menu`, `scs-skeleton`, `scs-empty-state` (no data,
no results, error), `scs-toast` with a `ToastService`, and `scs-confirm`. The rules a page must
follow when using them are written in [contracts/list-contract.md](contracts/list-contract.md).

Design reference: the parts themselves are on the components pages (Table Row `4267:903`,
Table Header Row `4267:1004`, Filter Popover `4274:54643`, Pagination `4252:61024`, Tab Bar
`4252:60835`, Empty State `4252:60047`, Skeleton `4252:59877`, Alert/Toast `4253:54128`, Modal
`4252:61695`, Menu `4252:61893`). The Home frames show them in use, but the Home card puts its
search and filter in the card's header — a compact form of its own. The full-width toolbar row
of BRD §A-7 is read from the list frames of "✓ Administration › Users" (`4160:7`) when that
part is built.

## R9 — The roles list, for reading

**Decision**: `/administration/roles` shows two cards as drawn in "Roles / Roles List"
(`6674:1004380`, `6674:1004929`): «أدوار جاهزة» and «أدوار مخصّصة», each a short table — role
(name and description), holders, permissions ("N of 83"). Loading and failure as "Roles /
Loading" (`6675:58005`) and "Roles / Load Failed" (`6675:58859`); no custom roles as "Roles /
Empty" (`6675:65061`). The line under the title counts roles and permissions. `GET /v1/roles`
gains `holders` per role.

Not built (spec 027): the «إنشاء دور» button in the header, the row menu, opening a role.

**Rationale**: the owner's choice of the first real list (11 Oct 2026). It needs one new number
from the server and nothing else.

## R10 — The sample list

**Decision**: `/dev/sample-list`, registered only when the environment is not production: 52
made-up rows held in the page, with search, two filters, chips, tabs, paging, a row menu, every
state, a toast and a confirmation (FR-042). Nothing on the server.

## R11 — Screen width

**Decision**: the side bar's folded state is: the saved choice when there is one; otherwise
folded under 1280 wide, expanded at 1280 and above (FR-043). Under 1024 the frame keeps a
minimum width and the page scrolls sideways.

## R12 — Icons

**Decision**: the icon component gains the icons the frame draws (home, calendar, people,
receipt, inbox, ticket, chart, shield, user-group, key, database, settings, building, layers,
bell, chevrons, search, filter, more, sign-out, user, tooth), taken from the file's "Icons"
page (`44:2`) at the size and stroke of the existing set.

## R13 — Wording where the design and the BRD differ

The BRD's order of authority is owner decisions, then the design, then code. Three places:

| Place | Design | BRD | Built |
|---|---|---|---|
| Empty notifications panel | «لا شيء هنا بعد» / «أضف أول سجل وسيظهر هنا.», with «تعليم الكل كمقروء» | «لا إشعارات جديدة», a line saying what will appear, no «تعليم الكل كمقروء» (CL-SH-030, ruling 27) | the BRD's — it is a recorded ruling, and the design's text is the component's placeholder |
| Subscription banner action | «تجديد الآن» twice, «تجديد الاشتراك» once | one word wanted (أ-م7) | «تجديد الاشتراك» for the three, per the spec's assumption |
| Environment strip | «بيئة تجريبية — البيانات ليست حقيقية» | the wording clashes with the trial period (أ-م15) | «بيئة اختبار — البيانات ليست حقيقية», per the spec's assumption; English as drawn |

Each is recorded for the owner; the design frames are compared with these differences named.

## R14 — Tests

Failing tests first for: the side bar by permission over all 20 destinations and 3 parents
(SC-002); every address inside the frame (SC-001); preferences saved and isolated; the
own-account list (now three).

Browser tests picture every state of the frame in both languages at 1440×900 for the design
comparison, and check 1280, 1024 and 1920 for overlap (SC-010). Keyboard use is tested for
every menu and panel (SC-009).

## Still to confirm with the owner

1. The three wordings of R13.
2. The addresses of R2.
3. The "not in your plan" card: not found in the components pages; built from the Empty State
   card with the plan named, unless a module page draws it.
4. The inbox count has no substitute on the folded icon (BRD أ-م14) — built as the BRD says.
5. The side bar's sub-items read «التقارير المالية», «التقارير الطبية», «سجل الأنشطة والأمان»,
   «الملخص الأسبوعي» in the design, without the BRD's diacritics — built as drawn.
