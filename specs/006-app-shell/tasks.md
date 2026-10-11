---

description: "Task list for 006-app-shell"
---

# Tasks: Console Frame (App Shell)

**Input**: Design documents from `/specs/006-app-shell/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md. Specs 001 to 005 implemented locally and Docker running. The Figma connector is needed for every task that builds or compares a screen.

**Tests**: Included, and first. The side bar by permission, every address inside the frame, and preferences are test-first areas (constitution VII): each test task is written and seen to fail before the implementation that makes it pass. No in-memory database is used anywhere.

**Design**: every task that builds a visible part opens that part's frame first (ids in spec.md "Design source" and contracts/) with `get_screenshot` and `get_variable_defs`, and builds from it — never from the BRD's words alone (constitution I).

**Organization**: Tasks are grouped by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1 to US9)
- Paths are relative to the repository root

## Path Conventions

- Module: `src/server/modules/Identity/Scs.Identity/` (`Domain/`, `Infrastructure/`, `Api/`) and `Scs.Identity.Contracts/`
- Server tests: `src/server/tests/Scs.IntegrationTests/`
- Web: frame in `src/web/projects/clinic/src/app/shell/`, sources in `src/web/projects/core/src/lib/shell/`, shared parts in `src/web/projects/ui/src/lib/`, browser tests in `src/web/e2e/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: A known-good start, the new dependency, tokens and icons.

- [X] T001 Confirm the branch is `006-app-shell` from `develop` with spec 005 merged, and that `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci` and `npx playwright test` pass before any change (temp folder on `E:` as `docs/setup.md`)
- [X] T002 Add `@angular/cdk` at the version matching the installed Angular 22 to `src/web/package.json` and install; confirm `npm --prefix src/web run ci` still passes
- [X] T003 [P] Read the variables of frames `4343:15040` and `5999:831798` with `get_variable_defs` and add any token the frame uses that is missing to `src/web/projects/clinic/src/styles/tokens.css` (and the same names to `patient` and `ops`), keeping the design file's variable names
- [X] T004 [P] Add the frame's icons to `src/web/projects/ui/src/lib/icon/icon.component.ts` from the file's "Icons" page (`44:2`), at the stroke and box of the existing set: home, calendar, people, receipt, inbox, ticket, chart, shield, user-group, key, licence, database, building, layers, settings, tooth, bell, chevron-down, chevron-start, search, filter, more, sign-out, user; extend its spec

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: What a person keeps, on the server and in the client; the session carries it.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Write failing `src/server/tests/Scs.IntegrationTests/Identity/PreferencesTests.cs`: `PUT /v1/account/preferences` saves `language` (`ar`, `en`), `sidebarFolded`, `dismissedBanner` (`trial`, `expiring`, `grace`) each alone and together, changing only what is sent, and answers the preferences; `GET /v1/auth/session` carries `preferences` with nulls for a person who never chose; a third language, `readonly` as a banner and a non-boolean answer 400 with field code `INVALID`; an empty body answers `body`: `REQUIRED`; a banner closed today is in the answer and, with the clock moved past midnight Cairo time, is null; one account never reads or changes another's; no session 401; a temporary password 403 `PASSWORD_CHANGE_REQUIRED`
- [X] T006 Add `StaffPreference` to `src/server/modules/Identity/Scs.Identity/Domain/` (`AccountId` key and foreign key; `Language` varchar(2) null, `ar` or `en`; `SidebarFolded` bit null; `DismissedBanner` varchar(8) null, `trial`, `expiring` or `grace`; `DismissedOnDay` date null, set with `DismissedBanner` and only then; `UpdatedAtUtc`), `IClinicOwned`; map it in `Infrastructure/Persistence/IdentityDbContext.cs` with those checks; add its sample to `src/server/tests/Scs.IntegrationTests/Isolation/IdentitySamples.cs`; create the migration `StaffPreferences` with the isolation policy of spec 002
- [X] T007 Create `src/server/modules/Identity/Scs.Identity/Infrastructure/Preferences.cs` (read for an account; save the members given; the Cairo day from `TimeProvider`; a closed banner counts only today) and `Api/PreferencesEndpoints.cs` with `PUT /v1/account/preferences`, `.OwnAccountOnly()`; add `preferences` to the session answer in `Api/AuthEndpoints.cs`; register and map in `IdentityModule.cs`; add the route to the own-account list in `src/server/tests/Scs.IntegrationTests/Access/RouteDeclarationTests.cs` and in `specs/005-permissions-engine/contracts/access-engine.md` §1; T005 passes
- [X] T008 [P] Write failing `src/web/projects/core/src/lib/shell/preferences.service.spec.ts`, then create `preferences.service.ts`: reads `preferences` from the session; `setLanguage`, `setSidebarFolded`, `dismissBanner` apply at once and save; a failed save is kept and sent with the next change, and nothing is shown; at sign-in a saved language is applied and written to the browser, and with none saved the browser's language is sent as the first choice (FR-019); extend `StaffSession` in `src/web/projects/core/src/lib/auth/auth.service.ts`; export from `src/web/projects/core/src/public-api.ts`
- [X] T009 [P] Add `preferences`, `PUT /v1/account/preferences` and `holders` on roles to the stand-in platform in `src/web/e2e/staff-platform.ts`

**Checkpoint**: A person's choices are stored, isolated, and reach the client with the session.

---

## Phase 3: User Story 1 - Every page after sign-in sits in one frame (Priority: P1) 🎯 MVP

**Goal**: One frame for every console address; unbuilt destinations say so inside it.

**Independent Test**: sign in and open the home address, a built destination, one not built and one the role does not open; the bars are the same in all four.

### Tests for User Story 1 ⚠️

- [X] T010 [P] [US1] Write failing `src/web/projects/clinic/src/app/shell/destinations.spec.ts`: 20 destinations in the side bar under 3 parents and 2 in the account menu; keys and paths unique; every `permission` is a code of the catalogue (`PERMISSIONS`); the paths and permissions are exactly the table of `research.md` R2; the order is the order of `contracts/shell-frame.md` §3
- [X] T011 [US1] Write failing `src/web/e2e/shell.spec.ts`: for every destination path an owner sees it inside the frame (`shell`, `top-bar`, `side-bar` test ids present) with the "not available yet" page naming the destination; `/administration/roles` shows the roles page inside the frame; a person without a view sees "not in your role" inside the frame and no request for the page's content; `/nowhere` shows "page not found" with a link to «الرئيسية»; a parent's path (`/reports`) lands on the first child the person may open; with no session every path shows the sign-in form and no frame; with a temporary password the change-password page and no frame; `/sign-in`, `/forgot-password`, `/set-password` never show the frame

### Implementation for User Story 1

- [X] T012 [US1] Create `src/web/projects/clinic/src/app/shell/destinations.ts` (key, parent, group, path, icon, labelKey, permission, needs) with the 22 rows of `research.md` R2 and a registry by which a later spec gives a destination its page component; T010 passes
- [X] T013 [P] [US1] Create `src/web/projects/ui/src/lib/page-header/page-header.component.ts` (`scs-page-header`: breadcrumb, title, the line under it, a slot for header buttons) from Page Header `4252:61267` and Breadcrumb `4252:60932`, with its spec; move the roles page of spec 005 onto it
- [X] T014 [US1] Create `src/web/projects/clinic/src/app/shell/shell.component.ts` (environment strip slot, top bar slot, banner slot, side bar slot, `router-outlet`; the layout of `contracts/shell-frame.md` §1 from frame `4343:15040`; minimum width 1024) and `src/web/projects/clinic/src/app/pages/not-yet.page.ts` and `not-found.page.ts`; move the no-access page of spec 005 inside the frame
- [X] T015 [US1] Rebuild `src/web/projects/clinic/src/app/app.routes.ts`: the pages before sign-in as they are; one route for `ShellComponent` behind `sessionGuard` whose children are generated from `destinations.ts` — for each destination its registered page or the not-yet page behind `holds(permission)`, then the no-access page on the same path; a parent's path redirecting to the first child the person may open; `**` to not-found; remove `src/web/projects/clinic/src/app/landing/` and show the platform-status panel of spec 001 (`scs-placeholder-panel`) at `/dev/platform`, registered only when the environment is not `production`; add the wording to `src/web/projects/clinic/public/i18n/ar.json` and `en.json`; restart the "clinic" preview for the new folders
- [X] T016 [US1] Update the browser tests of specs 003 to 005 that looked for the landing page's markers (`signed-in`, `signed-in-clinic`, `signed-in-email`, `sign-out`) in `src/web/e2e/sign-in.spec.ts`, `sign-in-states.spec.ts`, `temporary-password.spec.ts`, `activation.spec.ts`, `access.spec.ts` to the frame's, and point the clinic cases of `placeholder.spec.ts` and `clinic.spec.ts` at `/dev/platform`; T011 passes

**Checkpoint**: There is a place for every page.

---

## Phase 4: User Story 2 - The side bar shows what the role opens, in one fixed order (Priority: P1)

**Goal**: Items by view permission, fixed order, empty groups gone, parents with their allowed children.

**Independent Test**: sign in with roles holding different views and compare with BRD §A-2.

### Tests for User Story 2 ⚠️

- [X] T017 [US2] Write failing `src/web/projects/clinic/src/app/shell/side-bar.spec.ts`: for each of the 20 destinations, present for a person who holds its view and absent — not disabled — for one who holds everything else; each of the 3 parents present with any one child and absent with none; a group with no item is absent with its label; the order of what is shown is always the table's; «الرئيسية» for a person with no role; «معمل الأسنان» absent unless the clinic traits say dental, whatever is held; the active item marked and its parent open; a parent toggles on press; the inbox count shown when the source gives one; the bar follows a change to the session's permissions

### Implementation for User Story 2

- [X] T018 [P] [US2] Create `src/web/projects/core/src/lib/shell/sources.ts` with `ClinicTraitsSource` (dental, linkedToDoctor — both false), `InboxCountSource` (none) and `PlanSource` (excludes nothing) as injectable defaults a later spec replaces; export from core
- [X] T019 [US2] Create `src/web/projects/clinic/src/app/shell/side-bar.component.ts` from Sidebar `4262:651`, Sidebar Nav Item `4252:60310` and frames `4343:15040`, `6538:914228`, `6538:915556`: two labelled groups, items 40 high, icon and label, count, chevron, children indented, active mark; reads `destinations.ts`, `AccessService` and the sources; names in `src/web/projects/clinic/public/i18n/ar.json` and `en.json` as drawn (Arabic and English of `contracts/shell-frame.md` §3); T017 passes

**Checkpoint**: People reach what they may open and see nothing else.

---

## Phase 5: User Story 3 - The account chip says who is signed in, and its menu signs out (Priority: P1)

**Goal**: Clinic name, the chip with name and role, the menu, sign-out.

**Independent Test**: sign in, read the chip, open the menu, sign out, press back.

### Tests for User Story 3 ⚠️

- [X] T020 [US3] Write failing cases in `src/web/e2e/shell.spec.ts`: the top bar shows the clinic's name in the page language; the chip shows the email, its first letter in the mark and the role's name (a ready role in the page language, a custom one as written, none with no role); pressing the chip opens the menu under it, the chip pressed and `aria-expanded`; items «الملف والاشتراك», separator, «تسجيل الخروج» — and «ملف الطبيب» only when the traits say linked; outside press and Escape close it and return focus; «تسجيل الخروج» ends the session, shows the plain sign-in form with no notice, and the browser's back shows no data

### Implementation for User Story 3

- [X] T021 [P] [US3] Create `src/web/projects/ui/src/lib/menu/menu.component.ts` (`scs-menu`, `scs-menu-item`, separator; a trigger directive that opens it under its button on the CDK overlay, start- or far-aligned, in both directions; closes on choose, outside press, Escape; marks the opener pressed) from Menu `4252:61893` and Menu Item `4252:61814`, with its spec
- [X] T022 [US3] Create `src/web/projects/clinic/src/app/shell/top-bar.component.ts` from Navbar `4252:60501` and frame `4343:15040` (logo, clinic name, dividers, slots for the branch button, the language pill and the bell, the chip) and `account-menu.component.ts` from frame `5999:834312`; sign-out through `AuthService.signOut()`; T020 passes

**Checkpoint**: Stories 1 to 3 are the MVP: a frame, a way around it, and a way out.

---

## Phase 6: User Story 4 - The language switch turns the whole console, and is remembered (Priority: P2)

**Goal**: The pill in the bar; the choice saved for the person.

**Independent Test**: switch, reload, sign out and in again in another browser.

- [X] T023 [US4] Write failing cases in `src/web/e2e/shell.spec.ts`: pressing «EN» turns the page to English and left-to-right without a reload, on the same page, within one second; the choice is sent to the platform; a session whose `preferences.language` is `en` opens in English in a browser set to Arabic; with none saved, the language chosen on the sign-in form is sent at sign-in; a refused save shows nothing and the page stays switched; an open menu stays open, in the other language
- [X] T024 [US4] Place the language pill (Language Toggle `4262:1111`, already in the sign-in frame of spec 003 — move it to `src/web/projects/ui/src/lib/` if it is not shared yet) in `top-bar.component.ts`, switching through `PreferencesService`; connect `LanguageService` in `src/web/projects/core/src/lib/i18n/language.service.ts` to the adopted language; T023 passes

---

## Phase 7: User Story 5 - The side bar folds to icons (Priority: P2)

**Goal**: 240 ↔ 72, tooltips, a group from the folded bar, saved.

**Independent Test**: fold, hover, press a group icon, press an ordinary icon, reload, sign in again.

- [X] T025 [US5] Write failing cases in `src/web/e2e/shell.spec.ts`: the fold button narrows the bar from 240 to 72 and the content widens; each icon has a tooltip with its name on hover and on focus; pressing a parent's icon unfolds the bar with that group open; another icon opens its page and the bar stays folded; the active item — or its parent's icon — is marked; the choice is saved and a session with `sidebarFolded: true` opens folded; with no saved choice the bar is folded at 1100 wide and expanded at 1280; a saved "expanded" is kept at 1100; the inbox count is not shown folded
- [X] T026 [P] [US5] Create `src/web/projects/ui/src/lib/tooltip/tooltip.directive.ts` (`scsTooltip`, on the CDK overlay, shown on hover and focus, placed at the reading end) from Tooltip `4252:59606`, with its spec
- [X] T027 [US5] Add folding to `side-bar.component.ts` and `shell.component.ts` from frames `4343:16343` and `6538:914228` (the 32×32 button at the foot; icons alone; the width rule of FR-043 through `PreferencesService`); T025 passes

---

## Phase 8: User Story 6 - The frame says what is going on (Priority: P2)

**Goal**: Environment strip, offline alert with writes disabled, "not in your plan", the subscription banner.

**Independent Test**: run outside production; cut the connection; give each part its state.

### Tests for User Story 6 ⚠️

- [X] T028 [P] [US6] Write failing `src/web/projects/ui/src/lib/write/write.directive.spec.ts`: a control with `scsWrite` is disabled and says why while offline, enabled again when the connection returns, and a control already disabled for its own reason stays disabled
- [X] T029 [US6] Write failing `src/web/e2e/shell-banners.spec.ts`: outside production the environment strip is on the sign-in form and on every console page, and is absent when the runtime configuration says `production`; going offline shows the alert of `contracts/shell-frame.md` §6 inside the content under the page header within 3 seconds, and coming back removes it and reads the page again (that the writing controls are disabled while search still works is checked on the sample list, T039); a failed request while the browser says "online" shows its own error and no offline alert; a destination that is outside the plan and outside the person's role shows "not in your role"; each of the four subscription states given by a fake source shows one banner under the top bar with its tone, wording with the days in the page's digits, and action; closing trial, expiring or grace sends `dismissedBanner` and the banner is gone on every page; the read-only banner has no close button; a session with `dismissedBanner` for today shows none for that state; a person without `subscription.view` sees no action; with both, the banner is above the offline alert; a destination the fake plan source excludes stays in the side bar and opens to the "not in your plan" card with a link to `/account/subscription`; with no state — the default — no banner

### Implementation for User Story 6

- [X] T030 [P] [US6] Create `src/web/projects/ui/src/lib/write/write.directive.ts` (`scsWrite`) using `OnlineService`; T028 passes
- [X] T031 [P] [US6] Create `src/web/projects/ui/src/lib/env-strip/env-strip.component.ts` from Dev Environment Strip `4274:54936` (32 high; «بيئة اختبار — البيانات ليست حقيقية» / "Test environment — data is not real") and show it from `src/web/projects/clinic/src/app/app.ts` on every screen when the runtime configuration's environment is not `production`
- [X] T032 [US6] Add `SubscriptionSource` (state and days, or none) to `src/web/projects/core/src/lib/shell/sources.ts`; create `src/web/projects/clinic/src/app/shell/subscription-banner.component.ts` from Subscription Banner `4262:1214` and frames `4384:13886`, `4384:14621`, `4384:15163`, `4384:15705` (48 high; the wording and actions of `contracts/shell-frame.md` §6; close through `PreferencesService`; action behind `*scsCan="'subscription.view'"`)
- [X] T033 [US6] Show the offline alert in `shell.component.ts` from frame `4343:19040` (the Alert banner, warning, inside the content under the page header) and re-run the current page's load when the connection returns; create the "not in your plan" page in `src/web/projects/clinic/src/app/pages/not-in-plan.page.ts` on the `not-in-plan` state of `scs-state`, redrawn from a module page's frame if one draws it, else from the Empty State card and recorded as undrawn — and have the generated routes use it when `PlanSource` excludes a destination; T029 passes

---

## Phase 9: User Story 7 - The bell and the branch switch are in their places (Priority: P2)

**Goal**: Both parts built with all their states, showing "nothing yet" by default.

**Independent Test**: press the bell with no notifications; give the parts sample data in a test and see each state.

- [X] T034 [US7] Write failing `src/web/e2e/shell-sources.spec.ts`: by default the bell has no dot and opens a panel 360 wide under it at the far edge with «الإشعارات», the bell mark, «لا إشعارات جديدة», «سيظهر هنا ما يخصّك من تذاكر وفواتير واشتراك.», no «تعليم الكل كمقروء» and no "view all"; outside press and Escape close it; with a fake source of three items (two unread) the newest is first, unread ones tinted, each with its kind's icon, title, text of at most two lines and relative time, «تعليم الكل كمقروء» in the heading, a dot on the bell, and the list scrolls inside the panel; pressing «تعليم الكل كمقروء» calls the source; a failed source shows an error with «إعادة المحاولة» inside the panel; by default there is no branch button; with a fake source of two branches the button shows «كل الفروع», opens a menu under it with «كل الفروع» and the two, the active one marked, and choosing one closes it and names the button; with one branch the button names it and offers no other; opening the bell closes the account menu
- [X] T035 [US7] Add `NotificationsSource` and `BranchesSource` with their "nothing" defaults to `src/web/projects/core/src/lib/shell/sources.ts` (shapes of `data-model.md` §3) and a spec for the defaults
- [X] T036 [US7] Create `src/web/projects/clinic/src/app/shell/bell-panel.component.ts` from Notification Panel `4262:1056` and frames `5999:831798`, `5999:832473` (empty wording per the BRD ruling, research R13; skeleton while loading; the undrawn error state from the shared empty-state part) and `branch-switch.component.ts` from frames `5999:833094`, `5999:833681`; place both in `top-bar.component.ts`; T034 passes

---

## Phase 10: User Story 8 - Every list and table keeps one contract (Priority: P2)

**Goal**: The shared parts of lists, dialogs and toasts; the roles list for reading; a sample list.

**Independent Test**: lay the roles list beside its frames; on the sample list go through every rule of `contracts/list-contract.md`.

### Tests for User Story 8 ⚠️

- [X] T037 [P] [US8] Write failing cases in `src/server/tests/Scs.IntegrationTests/Access/ReadyRolesTests.cs`: every role of `GET /v1/roles` carries `holders`, counting accounts of the clinic in any state, 0 for a role nobody holds, and never an account of another clinic
- [X] T038 [US8] Write failing `src/web/e2e/roles-list.spec.ts`: an owner sees the page header with the line counting roles and "83" permissions, the card «أدوار جاهزة» with its note and the three ready roles in order — name, description, holders, "83 من 83", "59 من 83", "40 من 83" in the page's digits — and the card «أدوار مخصّصة» with the clinic's own; a person with the view alone sees the same under «للعرض فقط»; no «إنشاء دور», no «⋯», and rows open nothing; loading shows skeletons; a failed load shows the shared error with «إعادة المحاولة» and a dash for the counts in the line; no custom role shows that card's empty state
- [X] T039 [US8] Write failing `src/web/e2e/sample-list.spec.ts` for `/dev/sample-list`, one test per rule of `contracts/list-contract.md` §3 that a page can show: the order of the anatomy; search at the start with a fixed width and «تصفية» at the end with no badge; the popover opens downward with the button pressed and covers neither a notice nor the paging; applied chips appear once under the toolbar with «مسح الكل» and the row vanishes when cleared; the footer's count and paging, «السابق» and «التالي» disabled at the ends, one page showing the count alone; a row opens its record and «⋯» has no "open" and draws delete red; with the view alone no «⋯» and no writing control; empty has no toolbar; no results keeps it; error keeps the toolbar and shows a dash; loading shows skeletons; deleting asks a confirmation whose button names the action, then a toast appears beside the header buttons and the row is gone; offline, every `scsWrite` control is disabled with its reason while the search still filters, and they return with the connection; the page does not exist when the environment is `production`

### Implementation for User Story 8

- [X] T040 [US8] Add `holders` to `RoleSummary` in `src/server/modules/Identity/Scs.Identity.Contracts/StaffContracts.cs` and to `ClinicRoles.ListAsync` in `Scs.Identity/Infrastructure/ClinicRoles.cs`; T037 passes
- [X] T041 [P] [US8] Create in `src/web/projects/ui/src/lib/list/`: `card.component.ts` (Card `4252:61428`), table styles for header row, row and cell (Table Header Row `4267:1004`, Table Row `4267:903`), `skeleton.component.ts` (Skeleton `4252:59877`) and `empty-state.component.ts` (Empty State `4252:60047`: no data, no results, error), each with its spec; rebuild `src/web/projects/ui/src/lib/state/scs-state.component.ts` on them so that its loading, empty and error states are these parts and there is one look per state (FR-025), and update its spec
- [X] T042 [P] [US8] Create in `src/web/projects/ui/src/lib/list/`: `list-toolbar.component.ts` (read the full-width toolbar from the list frames of page "✓ Administration › Users" `4160:7` first), `filter-popover.component.ts` (Filter Popover `4274:54643`), `applied-chips.component.ts` (frame `4481:16929`), `tabs.component.ts` (Tab Bar `4252:60835`), `table-footer.component.ts` (Pagination `4252:61024`) and `row-menu.component.ts` on `scs-menu`, each with its spec
- [X] T043 [P] [US8] Create `src/web/projects/ui/src/lib/toast/toast.component.ts` and `toast.service.ts` (Alert `Style=Toast` `4253:54128`; shown in the corner at the reading end beside the page header's buttons, `role="status"`, goes by itself) and `src/web/projects/ui/src/lib/confirm/confirm.component.ts` on the existing dialog (Modal `4252:61695`; the button names the action and is disabled until what it needs is chosen), each with its spec
- [X] T044 [US8] Rebuild `src/web/projects/clinic/src/app/administration/roles.page.ts` as the roles list from frames `6674:1004380`, `6675:19376`, `6675:58005`, `6675:58859`, `6675:65061` and their LTR twins, with the parts of T041 and the page header; counts from the catalogue; wording in `src/web/projects/clinic/public/i18n/ar.json` and `en.json` as drawn, with 83 where the frames say 79; T038 passes
- [X] T045 [US8] Create `src/web/projects/clinic/src/app/dev/sample-list.page.ts` with 52 made-up rows held in the page, using every part of T041 to T043, `scsWrite` and `*scsCan`; register `/dev/sample-list` in `app.routes.ts` only when the environment is not `production`; T039 passes

---

## Phase 11: User Story 9 - The frame is complete in two languages and as drawn (Priority: P3)

**Goal**: Every part and state in Arabic and English, mirrored, matching its frame; usable by keyboard; sound at four widths.

**Independent Test**: lay each part and state, in each language, beside its frame.

- [X] T046 [US9] Write `src/web/e2e/shell-states.spec.ts`: every state of `contracts/shell-frame.md` §9 in Arabic and English at 1440×900 — `lang` and `dir` right, no untranslated key, Arabic-Indic digits in Arabic and Western in English, identifiers and emails Latin in both, nothing wider than the page; a clinic name, an email and a role name of the longest allowed length are cut with an ellipsis in the bar and carried whole in a `title`; saves one picture per state and language when `SCS_SHOTS` is set
- [X] T047 [P] [US9] Write `src/web/e2e/shell-keyboard.spec.ts`: with the keyboard alone, reach and open the branch menu, the bell, the account menu, a side bar parent, the fold button and the language pill; arrow through menu items; Escape closes and returns focus to the opener; `aria-expanded`, `aria-current` on the active item and the panel's label are right
- [X] T048 [P] [US9] Write `src/web/e2e/shell-widths.spec.ts`: at 1280, 1440 and 1920 no part of the frame overlaps another, is cut off or needs sideways scrolling; at 1024 the bar is folded for a person with no saved choice and nothing overlaps; under 1024 the page scrolls sideways and the bars stay whole
- [X] T049 [US9] Run `SCS_SHOTS=<folder> npx playwright test shell-states roles-list sample-list`, open each state's frame through the Figma connector (RTL and LTR), and adjust the components until layout, sizes, icons, tones and wording match; keep a list of what was compared and what differed for `validation.md`

**Checkpoint**: All stories are complete.

---

## Phase 12: Polish & Cross-Cutting Concerns

- [X] T050 [P] Update `docs/adding-a-module.md` (registering a destination's page; providing a source; `scsWrite`; building a list from the shared parts and `contracts/list-contract.md`) and `docs/setup.md` (the frame; `/dev/sample-list`; who sees which side bar)
- [X] T051 [P] Measure SC-004 (language switch) and SC-006 (offline and back) in `src/web/e2e/shell-timing.spec.ts`, each in its own test with two retries and the figure written to the test's output
- [X] T052 Run every gate: `dotnet test src/server/Scs.sln` (Debug), `npm --prefix src/web run ci`, `npx playwright test`; fix what fails
- [X] T053 Walk the real stack as `quickstart.md` "By hand": the eleven steps with `owner@shifa.example`, `doctor@shifa.example` and a person without the roles view, in Arabic and English
- [X] T054 Write `specs/006-app-shell/validation.md`: what was run with its counts, what was walked, each state beside its frame in both languages with what matched and differed, the measured times, and what was not compared or not done
- [X] T055 Record the owner items in `validation.md`: the three wordings of research R13; the addresses; the "not in your plan" card; the chip showing the email until spec 026; 79 → 83 in the roles frames; the inbox count on the folded bar

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → stories.
- **US1 (3)** first: every other story is placed in the frame.
- **US2 (4)** and **US3 (5)** need US1; they do not need each other.
- **US4 (6)** needs US3 (the top bar) and T008.
- **US5 (7)** needs US2.
- **US6 (8)** needs US1; its sample-page checks need US8's T045, so T029's offline case is completed after T045.
- **US7 (9)** needs US3 (the top bar and `scs-menu`).
- **US8 (10)** needs US1 and T021 (`scs-menu`).
- **US9 (11)** needs all of the above.
- **Polish (12)** last.

### Within Each User Story

Tests are written and seen to fail before the implementation that makes them pass. A visible part is built with its frame open.

### Parallel Opportunities

- T003, T004 together; T008, T009 alongside T005–T007.
- T010 and T013 alongside T011.
- T018 alongside T017; T021 alongside T020; T026 alongside T025.
- T028, T030, T031 together.
- T037 alongside T038–T039; T041, T042, T043 together.
- T047, T048 alongside T046; T050, T051 together.

## Parallel Example: User Story 8

```text
T041 card, table styles, skeleton, empty state     (ui/list)
T042 toolbar, popover, chips, tabs, footer, row menu (ui/list)
T043 toast and confirmation                         (ui/toast, ui/confirm)
```

## Implementation Strategy

### MVP First

Phases 1 to 5: preferences, the frame with every address inside it, the side bar by permission,
the account menu with sign-out. Stop and validate: the console can be moved around and left.

### Incremental Delivery

1. Phases 6 and 7 — language and folding, both saved.
2. Phase 8 — environment strip, offline, banner, plan.
3. Phase 9 — bell and branch switch with their sources.
4. Phase 10 — the list parts, the roles list, the sample list.
5. Phase 11 — pictures against the design, keyboard, widths.
6. Phase 12 — gates, the real-stack walk, `validation.md`.

## Notes

- 55 tasks. Requirements to tasks: FR-001–004 T011–T016 · FR-005–013 T010, T012, T017–T019 · FR-014–016 T020–T022 · FR-017–019 T005–T008, T023–T024 · FR-011 folding T025–T027 · FR-020–025 T028–T033 · FR-026–029 T034–T036 · FR-030–037 T039, T041–T043, T045 · FR-038 T037–T038, T040, T044 · FR-042 T039, T045 · FR-043 T025, T027, T048 · FR-039–041 T046–T049.
- The implementation is not complete until T052–T054 are done and written down (constitution VII).

---

## Phase 13: Convergence

- [X] T056 CRITICAL Lay each remaining state beside its frame through the Figma connector, in Arabic and in English, adjust the components until layout, sizes, icons, tones and wording match, and record each in `specs/006-app-shell/validation.md` §3: folded (`4343:16343`), limited permissions (`6538:915556`), branch menu (`5999:833681`), notifications empty (`5999:832473`), the four banners (`4384:13886`, `4384:14621`, `4384:15163`, `4384:15705`), offline (`4343:19040`), filtered table (`4481:16929`), the roles list for an owner, loading, failed and empty (`6674:1004380`, `6675:58005`, `6675:58859`, `6675:65061`), and the LTR twin of every state already compared in Arabic per Constitution VII (partial)
- [X] T057 CRITICAL Walk the real stack as `doctor@shifa.example` and as `reception@noor.example` (no roles view), in Arabic and English, through the steps of `quickstart.md` "By hand" — the side bar each sees, the roles list under «للعرض فقط», "not in your role" inside the frame, the language and fold kept after signing in again — and record it in `validation.md` §2 per Constitution VII (partial)
- [X] T058 Add to the "Undrawn" list of `specs/006-app-shell/spec.md` the icons chosen without a frame — «نصوص الموافقة», «معمل الأسنان», «ملف الطبيب» — and the two icons drawn differently from the file's set (inbox, ticket), as owner items per Constitution I (partial)
- [X] T059 Give the `offline` and `not-in-plan` states one look each: rebuild them in `src/web/projects/ui/src/lib/state/scs-state.component.ts` on the warning banner and the "not in plan" empty-state card the frame shows, have `shell.component.ts` and `shell/pages.ts` use the same part, and update `scs-state.component.spec.ts` and the placeholder tests per FR-025 (contradicts)
- [X] T060 Create the shared `scs-filter-popover` (the panel's body with «تطبيق» and «مسح الكل», Filter Popover `4274:54643`) and `scs-confirm` (a confirmation whose button names the action and is disabled until what it needs is chosen, Modal `4252:61695`) in `src/web/projects/ui/src/lib/list/`, with specs, and use them in `src/web/projects/clinic/src/app/dev/sample-list.page.ts` in place of its own markup per contracts/list-contract.md §2 (missing)
- [X] T061 Add `src/web/projects/ui/src/lib/overlay/overlay.spec.ts`: a menu item's `chosen`, `danger` and `active`; the tooltip shown on hover and on focus and gone on leave, and absent when its text is empty per T021, T026 (partial)
- [X] T062 Read `brand-subtle`, `border-subtle` and `bg-muted` from the operators' design file (`SetO9EhDivNcmurAMfcflY`) and add them to `src/web/projects/ops/src/styles/tokens.css`, or record that the file has no such values per T003 (partial)
- [X] T063 Record `window.__scsSample` (`src/web/projects/clinic/src/app/dev/sample-sources.ts`) in `specs/006-app-shell/research.md` R5 and `quickstart.md` as the way tests give the frame's sources their data, with the rule that production never reads it — or replace it with a way the plan names per plan: research R5 (unrequested)

---

## Phase 14: Convergence

- [X] T064 CRITICAL Find the LTR twin of each of these frames on page "✓ Home" (`4160:3`) and page "Roles" (`5803:4`) with a read-only listing, lay the English picture beside it, adjust what differs, and record each in `specs/006-app-shell/validation.md` §3: folded, limited permissions, notifications open and empty, branch menu, account menu, the four banners, offline, filtered table, and the roles list for an owner, loading, failed and with no custom roles per Constitution VII (partial)
- [X] T065 CRITICAL Add to the development seed in `src/server/modules/Identity/Scs.Identity/IdentityModule.cs` an account of the Shifa clinic that holds a custom role without «عرض الأدوار والصلاحيات» and signs in with the seed password (`reception@shifa.example`), update the seed's tests and the table of accounts in `docs/setup.md`, then walk the real stack as that person in Arabic and English — the side bar without «الأدوار والصلاحيات», "not in your role" inside the frame at its address, the frame otherwise whole — and record it in `validation.md` §2 per Constitution VII (partial)
- [X] T066 Bring FR-011, the text and scenarios of User Story 5 in `specs/006-app-shell/spec.md`, and `specs/006-app-shell/contracts/shell-frame.md` §3 into line with the owner's two decisions the code already follows — the fold button at the top of the side bar beside the first group's name, and one parent open at a time — keeping the note that the frames draw the button at the foot per FR-011 (contradicts)
- [X] T067 Add to `docs/adding-a-module.md`: `scs-confirm` and `scs-filter-popover` among the shared parts of a list, and that a page must start with `scs-page-header` because it carries the offline warning per T050 (partial)

---

## Phase 15: Convergence

- [X] T068 CRITICAL Open the six LTR frames not yet opened — notifications empty (`5999:835622`), limited permissions (`6538:916216`), the trial, expiring and read-only banners (`4384:16709`, `4384:17444`, `4384:18528`) and the roles list (`6674:1004929`) — beside their English pictures, adjust what differs, and move them in `specs/006-app-shell/validation.md` §3 from "Not opened in LTR" to the table of compared states per Constitution VII (partial)
- [X] T069 Correct `docs/setup.md` where it says the start script registers "six made-up staff accounts": the seed adds eight per T065 (partial)
- [X] T070 Remove the second copy of the rule `:host([data-folded='true']) .fold scs-icon` in `src/web/projects/clinic/src/app/shell/side-bar.component.ts` per T027 (unrequested)
