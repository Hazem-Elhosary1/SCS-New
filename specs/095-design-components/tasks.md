# Tasks: Design Components

**Input**: Design documents from `/specs/095-design-components/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (components.md, conventions.md, gallery.md, icons.md), quickstart.md

**Tests**: required by the constitution (Principle VII): unit tests per component (options, keys, names, refusals), the gallery suite (inventory coverage, look checks, kept pictures), the tools' tests, the console's existing browser suites after the move, and the clinic's before-and-after pictures. None of the four test-first areas is touched, so order within a task is free; every task names its tests.

**Organization**: by user story. Paths are under `src/web/`. `ui/` means `projects/ui/src/lib/`; `ops-app/` means `projects/ops/src/app/`; `own/` means `ops-app/components/`; `gallery/` means `ops-app/gallery/`; browser tests are `e2e/`. Design nodes are in the operators' file `SetO9EhDivNcmurAMfcflY` unless "clinic" is said (`4HLWEqlna9sbn68276LHYt`); read every frame named in a task through the Figma connector (`use_figma` with `skillNames: "resource:figma-use"`, `get_screenshot`) before building it, in both twins. If the connector refuses ("no edit access"), stop and tell the owner — never build a component from memory. Every component follows `contracts/conventions.md`; its row in `contracts/components.md` gives its nodes, place, options and keys.

**Every component task includes**: build or bring to the design; unit tests in the named `.spec.ts`; the gallery section `gallery/sections/<page>.section.ts` with one specimen per drawn variant (labelled with its option values and node) and a live example; for a shared component, the specimens again in the clinic look against the clinic node, with each difference between the two files noted in `validation.md`; opening the section at 1440 and 390 in both languages (and both looks) and looking at it beside the frames.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story of spec.md the task serves

---

## Phase 1: Setup

- [X] T001 Start `specs/095-design-components/validation.md` with the baseline: spec 011's last runs as written in `specs/011-operators-roles/validation.md` (unit 68 / 1 / 27 / 164 / 107; browser 755; Edge 118 for the console), and run `npm run ci` once to confirm it still passes before any change
- [X] T002 Take the clinic's and patients' "before" pictures (research R9). Today only `access-states`, `roles-list`, `sample-list` and `shell-states` picture their states, at 1440 only: first make `pictureFor` in `e2e/staff-platform.ts` also save a 390 picture, and add a `pictureFor` call at each state of the suites that take none (`sign-in`, `sign-in-states`, `password-reset`, `password-states`, `temporary-password`, `activation`, `access`, `clinic`, `files`, `notifications`, `placeholder`, `shell`, `shell-banners`, `shell-keyboard`, `shell-sources`, `shell-timing`, `shell-widths`), changing no assertion; then run every clinic and patient suite with `SCS_SHOTS=node_modules/.tmp/before095` in both languages and record the count of pictures in `validation.md`. Only after this may a token, a component or an `index.html` change
- [X] T003 Write the design inventory `gallery/design-inventory.json` per `data-model.md` "Design inventory" from the operators' file: for each of the 33 component pages its node, each component set with its English and Arabic nodes, every variant's option values "exactly as the design writes them", the standalone components (Navbar `278:3`/`311:16`, Pagination Menu `2104:140`/`2104:160`), the drawn parts of FR-002 with their nodes, the five pattern pages, and `read: 2026-10-16`; assert in the same task that it holds 44 sets and 328 variants per language, or note what the file now says

---

## Phase 2: Foundational (blocks every story)

- [X] T004 Put each app's look on an attribute (research R3): change the selector of `projects/ops/src/styles/tokens.css`, `projects/clinic/src/styles/tokens.css` and `projects/patient/src/styles/tokens.css` from `:root` to `[data-scs-look='ops']`, `[data-scs-look='clinic']`, `[data-scs-look='patient']`, and add `data-scs-look` to the `<html>` of each `projects/*/src/index.html`; check by a unit test per app that the root carries its look, and by opening one clinic and one patient screen that nothing moved
- [X] T005 Rewrite `projects/ops/src/styles/tokens.css` from the design file per research R2: every value of the R2 table under its token (the new ones `--scs-color-text-placeholder`, `--scs-color-text-disabled`, `--scs-color-bg-hover`, `--scs-color-border-control`, `--scs-color-brand-hover`, `--scs-color-danger-hover`, `--scs-color-danger-active` included), the type scale of the file's text styles, spacing, radii 4 · 8 · 12 · 16 · full, border widths 1 · 1.5 · 2, shadows Elevation XS…Overlay; replace the header's "NOT YET FROM THE DESIGN FILE" with the file, the collections and the date read; load Cairo in `projects/ops/src/styles.css` as the clinic does and make it `--scs-font-family`; add to `projects/clinic/src/styles/tokens.css` and `projects/patient/src/styles/tokens.css` the same new token names with the clinic file's values (its "Color" collection), changing no existing value
- [X] T006 Move the operators' forms' field style to the named option of research R13: a class `.scs-form--template` in `projects/ops/src/styles.css` setting `--scs-field-label-weight: 400`, `--scs-field-label-color` and `--scs-select-empty-color`; apply it in `ops-app/operators/operator-form.page.ts` and `role-form.page.ts` in place of their `:host` variables; the component default becomes the Dropdown page's (empty text grey, `--scs-color-text-placeholder`)
- [X] T007 Copy the icon library (research R6, `contracts/icons.md`): export through the connector, in batches, every icon of the Icons page `738:7380` (1,144) and the 16-pixel menu icons of the Menu page `2200:86` as SVG text with "name", "group", "node", "svg" and one "copied" date, into `tools/icons/library/part-*.json` (shapes as exported, coordinates to hundredths); assert the count
- [X] T008 Make the icon component registry-driven: create `ui/icon/icon-registry.ts` holding the 53 icons drawn so far as entries `from: 'drawn'` with their current paths, `paint: 'stroke'`, and `points: true` for `back`, `chevron-start`, `sign-out`; make `ScsIconName` the registry's keys; render the entry in `ui/icon/icon.component.ts` (sizes 16 · 18 · 20 · 24, `currentColor`, `aria-hidden`), mirror `points` entries right to left and remove the per-name rules; test in `ui/icon/icon.component.spec.ts` that every old icon draws exactly the same paths as before and that only pointing ones mirror
- [X] T009 Create `tools/icons.mjs` with `list <words>`, `add <design name> --as <short name> [--points]` and `check` per `contracts/icons.md` (refuses a name not in the copy and prints the closest names; refuses a taken short name; `check` fails on a registered icon used by no file under `projects/`), with `tools/icons.spec.mjs`; add `icons.mjs check` and the tool's tests to `npm run ci` in `package.json`
- [X] T010 Create the gallery's frame per `contracts/gallery.md`: `gallery/gallery.routes.ts` (`/dev/components` and `/dev/components/:page`, no guard, outside the frame) and `gallery/gallery.routes.prod.ts` exporting no route; spread them into `ops-app/app.routes.ts` before the frame; add the `fileReplacements` to the `production` configuration of `ops` in `angular.json`; `gallery/gallery.page.ts` with the bar (title, language switch, look switch, the list of sections) and one section per design page in the file's order, "Other parts", the five patterns and "Icons", each empty for now; the gallery's own words as data in both languages (not keys of `projects/ops/public/i18n`); test ids of `contracts/gallery.md`
- [X] T011 Create `tools/check-production.mjs` (+ `tools/check-production.spec.mjs`): fails when any file under `dist/ops` contains `dev/components`, `scs-gallery` or `design-inventory`; run it after `build:all` in `npm run ci`; build once and confirm it passes and that serving `dist/ops` answers `/dev/components` as an unknown address
- [X] T012 Create the gallery's catalogue and specimen tools: `gallery/catalogue.ts` per `data-model.md` "Catalogue entry" (fields `page`, `name`, `kind`, `clinicNode`, `options`, `stillStates`, `live`, `replaces`); `gallery/specimen.directive.ts` (`scsSpecimen`: sets `data-scs-state` for hover, focus, pressed and makes the specimen `inert`, research R5); `gallery/specimen-grid.component.ts` laying specimens with their labels and nodes, one column at 390; the look switch wrapping the content in `data-scs-look="clinic"` and loading `projects/clinic/src/styles/tokens.css` through an unencapsulated stylesheet of `gallery/clinic-look.component.ts`, showing only `shared` entries there
- [X] T013 Create the gallery suite's harness in `e2e/gallery/gallery-platform.ts` and `e2e/gallery/coverage.spec.ts`: open each section in both languages, both widths and (for shared) both looks; fail for every variant of `design-inventory.json` without a specimen `specimen-<page>/<component>/<variant>` (SC-001); run `opsLooksWrong` on every section (FR-027a), its digit check per look (Western in the operators' look, CL-SH-064 in the clinic's) and its language check skipping `[data-scs-label]`; set `snapshotPathTemplate` for `e2e/gallery/__pictures__/` in `playwright.config.ts`. It fails until every component is built — leave it failing and say so

**Every shared component task also**: reads the clinic set's geometry beside the operators' one, and makes each size the two files draw differently a per-look custom property of the component (`contracts/conventions.md` §2) whose default is the clinic screens' current value, set to the operators' value in `projects/ops/src/styles/tokens.css`; the clinic's kept pictures and T054 prove no clinic part changed shape.

**Checkpoint**: the gallery opens empty in development, is absent from production; both looks apply; icons draw as before from the registry; the coverage suite lists every missing specimen.

---

## Phase 3: User Story 1 — See every component in every state (P1) 🎯 MVP

**Goal**: every drawn variant of the 33 pages on the gallery, both languages, both widths, the shared ones in both looks.

**Independent test**: `e2e/gallery/coverage.spec.ts` passes; any one page's section matches its frames when laid beside them.

- [X] T014 [P] [US1] Button (`31:302`/`316:470`, parts `32:3`, `32:9`, `32:14`, `32:19`; clinic `4252:2227`): bring `button[scsButton]` and `[scsLink]` in `ui/controls/controls.ts` and `ui/controls/controls.css` to six types × three sizes × six states, busy keeping its width, icon leading, trailing or alone with `label`; `ui/controls/button.spec.ts`; section `gallery/sections/button.section.ts` (108 specimens per language)
- [X] T015 [P] [US1] Checkbox (`43:36`/`297:34`; clinic `4252:58206`) and Switch (`43:102`/`297:96`; clinic `4252:58656`) in `ui/controls/form-controls.ts`: indeterminate and error for the checkbox; the busy (Loading) state for the switch; the knob at the right when on in both languages; tests in `ui/controls/form-controls.spec.ts`; sections `checkbox`, `switch`
- [X] T016 [P] [US1] Radio (`43:66`/`298:29`; clinic `4252:58465`): new `scs-radio-group` and `scs-radio` in `ui/controls/radio.component.ts` with roving focus; `ui/controls/radio.component.spec.ts`; section `radio`
- [X] T017 [P] [US1] Slider (`55:58`/`300:56`; clinic `4260:809`): new `scs-slider` in `ui/controls/slider.component.ts`, single and range (two thumbs, each `role="slider"`), arrows, Page Up/Down, Home, End; `ui/controls/slider.component.spec.ts`; section `slider`
- [X] T018 [P] [US1] Input (`54:257`/`304:257`; clinic `4252:56552`): bring `scs-text-field` in `ui/field/text-field.component.ts` to twelve states × three sizes (`size`, `warning`/`success` with message, `readonly`, `busy`, leading icon); `ui/field/text-field.component.spec.ts`; section `input`
- [X] T019 [P] [US1] Textarea (`146:95`/`304:369`; clinic `4272:885`): sizes, read only and counted limit for `scs-textarea` in `ui/controls/form-controls.ts`; tests; section `textarea`
- [X] T020 [P] [US1] File Upload (`157:72`/`304:440`; clinic `4260:938`): the six drawn states for `scs-file-upload`, `scs-file-card`, `scs-file-field` in `ui/files/`; `ui/files/files.spec.ts`; section `file-upload`
- [X] T021 [P] [US1] Select and its list (`57:43`/`303:40`; list `58:3`, multi `58:14`, search empty `58:30`, results `2224:70`; clinic `4272:1534`, `4973:2602`): add `multiple` with chips to `scs-select` in `ui/controls/select.component.ts`; specimens of the six trigger states and the four list parts; `ui/controls/select.component.spec.ts`; section `dropdown`
- [X] T022 [P] [US1] Segmented control (`385:11`/`386:19`, tracks `385:12`/`386:20`; clinic `4260:1432`, `4260:1448`): new `scs-segmented` in `ui/controls/segmented.component.ts`; spec; section `segmented-control`
- [X] T023 [US1] Dates (research R11): create `ui/date/calendar.ts` — month grid, week start as the calendar panel draws it (read `75:4` and its Arabic twin; record the first day in `validation.md`), "today" in Cairo, names in the page's language, digits by the console's rule — with `ui/date/calendar.spec.ts` (month edges, leap years, Cairo midnight, Western digits in the operators console)
- [X] T024 [US1] Date field and calendar (`74:53`/`308:52`; calendar `75:4`, range `76:3`; clinic `4256:824`, `4256:1424`, `4256:885`): new `scs-date-field` and `scs-calendar` (one day and a range) in `ui/date/`; the calendar's keys of `contracts/components.md`; `ui/date/date-field.component.spec.ts`; section `date-picker`
- [X] T025 [US1] Time field and list (`77:53`/`308:103`; list `78:4`, wheel `78:16`; clinic `4256:1542`, `4256:1480`): new `scs-time-field` in `ui/date/time-field.component.ts`, `mode` list or wheel, `step`; spec; section `time-picker`
- [X] T026 [P] [US1] Date range and date & time, the console's own (`392:53`/`393:112`, calendar `393:2`; `79:71`/`308:172`, panel `80:4`): `own/date-range/date-range-field.component.ts` and `own/date-time/date-time-field.component.ts` on `scs-calendar` and `scs-time-field`; specs beside them; sections `date-range-picker`, `date-time-picker`
- [X] T027 [P] [US1] Avatar (`294:131`/`8:111`; clinic `Avatar` on `4228:7`) and Badge (`294:147`/`8:86`; clinic `4253:53652`): new `scs-avatar` in `ui/avatar/`; `scs-badge` in `ui/badge/` replacing `scs-status-mark` (keep `scs-status-mark` as an alias selector until the clinic moves); specs; sections `avatar`, `badge`; note the two files' differences (research)
- [X] T028 [P] [US1] KPI Card (`294:315`/`8:135`; clinic `4267:1175`) and Sidebar Nav Item (`294:323`/`8:119`; clinic `4252:60310`): `scs-kpi-card` in `ui/list/page-parts.ts` with the three trends; new `scs-nav-item` in `ui/shell/nav-item.component.ts` with Default, Hover, Active, Disabled; specs; sections `kpi-card`, `sidebar-nav-item`
- [X] T029 [P] [US1] Loading (`289:27`, `289:38`, `289:48` / `314:54`, `314:65`, `314:75`; clinic `4252:59832`, `4252:59743`, `4252:59877`): new `scs-spinner` and `scs-progress` in `ui/loading/`; `scs-skeleton` in `ui/list/list.ts` with the three line widths and the composed loader and skeleton; specs; section `loading`
- [X] T030 [P] [US1] Alert (`60:46`/`257:71`; clinic `4253:54128`): `scs-banner` in `ui/controls/controls.ts` and the toast of `scs-toast-outlet` in `ui/overlay/overlay.ts` with the four intents in both styles; specs; section `alert`
- [X] T031 [P] [US1] Modal (`62:57`/`309:116`; clinic `4252:61695`): the four sizes for `scs-dialog` in `ui/dialog/dialog.component.ts`, keeping its look as the owner chose; spec; section `modal`
- [X] T032 [P] [US1] Navigation (`284:12`, `284:21`, `284:34`, `284:53` / `312:62`, `312:71`, `312:84`, `312:103`; menu `2104:140`/`2104:160`; folded `2111:195`/`2111:285`; clinic `4252:60720`, `4252:60901`, `4252:60984`, `4252:61106`): `scs-tabs`, `scs-breadcrumb` in `ui/list/page-parts.ts`, the pager of `scs-table-footer` with the folded pagination and its menu in `ui/list/list.ts`, new `scs-stepper` in `ui/stepper/`; specs; section `navigation`
- [X] T033 [P] [US1] Data display (`281:27`, `281:64`, `281:77` / `313:26`, `313:63`, `313:76`; clinic `4252:61428`, `4253:53827`, `4260:1383`): `scs-card` states, new `scs-tag` in `ui/tag/` (and `scs-applied-chips` using it), new `scs-accordion` in `ui/accordion/`; the console's own Divider (`281:88`/`313:87`) and Stat (`281:107`/`313:106`) in `own/divider/`, `own/stat/`; specs; section `data-display`
- [X] T034 [P] [US1] Table (`273:52`/`309:166`, example `273:55`; clinic `4267:903`, `4267:1004`, `4267:744`): the row states and the composed table in `ui/list/list.css` and `ui/list/list.ts`; spec; section `table`
- [X] T035 [P] [US1] Navbar (`278:3`/`311:16`, unread `2403:6546`/`2403:6562`, logo `2467:133`/`2467:161`; clinic `4252:60501`) and Notifications (`362:23`, `362:43` / `365:59`, `365:77`; panel `364:32`; clinic `4262:1056`): new `scs-navbar` in `ui/shell/navbar.component.ts`; new `scs-notification-bell`, `scs-notification-item`, `scs-notification-panel` in `ui/notifications/`; specs; sections `navbar`, `notifications`
- [X] T036 [P] [US1] Menu (`368:23`/`370:38`, panel `368:26`, row menus `2201:86`, icons `2200:86`; clinic `4252:61814`, `4252:61893`): `[scsMenu]` and `[scsMenuItem]` in `ui/overlay/overlay.ts` with Danger and Danger Hover, icons and disabled with reason; register the drawn menu icons with `tools/icons.mjs add` (`--points` where they point); the row-actions menus of every table as specimens; spec; section `menu`
- [X] T037 [P] [US1] Empty State (`374:35`/`374:68`; clinic `4252:60047`) and Tooltip (`383:23`/`384:22`; clinic `4252:59606`): `scs-empty-state` types in `ui/state/`; `[scsTooltip]` placements start and end in `ui/overlay/`; specs; sections `empty-state`, `tooltip`
- [X] T038 [P] [US1] Usage Meter (`387:24`/`388:23`; clinic `Plan Usage Meter` on `4228:7`) and Chart (Sparkline `394:15`/`395:14`; bar chart `394:16`/`395:15`; clinic `5601:4517`): new `scs-usage-meter` in `ui/usage-meter/`, `scs-bar-chart` in `ui/chart/` (SVG, research R12), `ops-sparkline` in `own/chart/`; specs; sections `usage-meter`, `chart`
- [X] T039 [P] [US1] Media (`82:20`/`314:19`; avatar group `84:3`, video `84:15`, carousel `84:26`, tiles `84:39`…`84:67`): `own/media/` — image with its three states, avatar group, video player on `<video>` with the drawn controls, carousel, gallery tile; example files in `gallery/examples/`; specs; section `media`
- [X] T040 [P] [US1] AI (`399:13`, `401:19`, `404:10` / `408:21`, `408:39`, `409:29`; Ask-AI `402:3`, `402:6`, chat `405:2` and their Arabic twins; the suggestion card's states): `scs-insight-card` and `scs-message-bubble` in `ui/ai/` (clinic `Insight Card`, `Chat Bubble`); `ops-ai-badge`, the Ask-AI button and field and the chat widget in `own/ai/`, example data only and no call (Constitution V); specs; section `ai`
- [X] T041 [US1] Fill "Other parts" (`gallery/sections/other-parts.section.ts`) with the parts kept as they are (`contracts/components.md`): code input, password rules, language toggle, environment strip, auth frame, no access, page header, list toolbar, filter panel, placeholder panel — each in its states
- [X] T042 [US1] Take the kept pictures: `e2e/gallery/pictures.spec.ts` takes one `toHaveScreenshot` per specimen (`<page>/<component>/<variant>--<ar|en>--<1440|390>[--clinic].png`, research R7); before accepting, open every section at both widths in both languages and both looks and look at it; accept with `--update-snapshots`; `e2e/gallery/coverage.spec.ts` of T013 now passes

**Checkpoint**: SC-001 holds; every section has been opened.

---

## Phase 4: User Story 2 — Build a screen from components, any state by setting it (P1)

**Goal**: each component behaves as the design and the BRD rules say, worked by keyboard and read by a screen reader.

**Independent test**: `e2e/gallery/keyboard.spec.ts` passes on every live example in both languages.

- [X] T043 [US2] Write `e2e/gallery/keyboard.spec.ts`: for every live example, reach it by Tab, work it by the keys of `contracts/components.md` alone, check its role, name, value and state as a screen reader is told them (accessible tree), and for what opens: `aria-expanded` on the opener, Escape closes, focus returns, the opened part inside the window at 390 and at 200% zoom (FR-007, FR-008)
- [X] T044 [US2] Write `e2e/gallery/settings.spec.ts` for the states that are settings (acceptance scenarios of US2): a field's error replaces its hint and is its description; a busy button keeps its width, cannot be pressed twice and has `aria-busy`; the searchable list's "no results" row; a range chosen in the calendar in both directions; a disabled control's reason shown and read (FR-009, CL-SH-061, CL-SH-062); long Arabic and English words in a field, a drop-down and its list, a badge, a tag, a menu item and a table cell wrap or end with an ellipsis as drawn and stay whole for a screen reader (spec, Edge Cases)
- [X] T045 [US2] Check every component's own words (FR-014): each comes from `projects/ui/i18n/{ar,en}.json` under `ui.*` as the design writes it; `npm run i18n:check` passes; no Arabic-Indic digit in any section of the operators' look (OP-B-386) — extend T013's checks if one is missing
- [X] T046 [US2] Reduced motion (FR-013): with `prefers-reduced-motion: reduce`, spinners, skeleton shimmer, toasts and turning chevrons are still or change at once; add the case to `e2e/gallery/settings.spec.ts`
- [X] T047 [US2] Controls at 390 (FR-012): in `e2e/gallery/settings.spec.ts`, every pressable element of every live example is at least 40 by 40 at 390

**Checkpoint**: SC-003 holds.

---

## Phase 5: User Story 3 — The operators' screens use the components (P2)

**Goal**: the screens of specs 010 and 011 are built from the gallery's components and still match their frames.

**Independent test**: every `e2e/ops-*.spec.ts` passes; their pictures beside the frames are as close or closer than before.

- [X] T048 [US3] Move the console's frame onto `scs-navbar`, `scs-nav-item` and `scs-notification-bell` in `ops-app/shell/ops-shell.component.ts` (no bell behaviour yet — 093 brings it; the bell is hidden until then as spec 011 left it); the frame's tests in `ops-app/shell/` and `e2e/ops-console.spec.ts` pass
- [X] T049 [P] [US3] Move the sign-in screens, the code step, forgot and set password (`ops-app/sign-in/`, `ops-app/password/`) onto the components (button types and sizes, text field states, alert); `e2e/ops-sign-in.spec.ts` passes
- [X] T050 [P] [US3] Move the profile and its dialogs (`ops-app/profile/`) onto the components; `e2e/ops-console.spec.ts` and `e2e/ops-look.spec.ts` pass
- [X] T051 [P] [US3] Move the operators and the roles (`ops-app/operators/`) onto the components: `scs-badge` for the status marks, the table, pagination, menus, the forms with `.scs-form--template`; `e2e/ops-operators.spec.ts` and `e2e/ops-roles.spec.ts` pass
- [X] T052 [US3] Retake every picture of the console (`SCS_SHOTS` on the five `ops-*` files), open every one, and lay each beside its frame of specs 010 and 011; list in `validation.md` every change of look, each as a correction toward the frame or a difference with its decision (FR-021)

**Checkpoint**: SC-005 holds.

---

## Phase 6: User Story 4 — The clinic keeps its look (P2)

**Goal**: no clinic or patient screen changes; shared components are right in the clinic's look.

**Independent test**: the before-and-after comparison finds nothing beyond smoothing; the gallery's clinic look matches the clinic file.

- [X] T053 [US4] Write the comparison script `tools/compare-pictures.mjs` (+ `.spec.mjs`): for two folders, each pair's size and share of differing pixels (a channel sum above 30), sorted; exit non-zero for any change of size or any share above 0.05% (research R9)
- [X] T054 [US4] Take the "after" pictures exactly as T002 into `node_modules/.tmp/after095`, compare with T053, open every pair it flags, and fix or explain each in `validation.md` (SC-004, FR-023)
- [X] T055 [US4] Lay each shared component's clinic-look specimens beside the clinic file's drawings (`4228:2`–`4228:6`), open every sheet, and list each difference between the two files in `validation.md` for the designer (FR-005, US4 scenario 3)

**Checkpoint**: SC-004 holds.

---

## Phase 7: User Story 5 — Icons (P3)

**Goal**: only the icons in use; any other added by name.

**Independent test**: quickstart §4.

- [X] T056 [US5] Fill the gallery's "Icons" section (`gallery/sections/icons.section.ts`): every registry entry at 16, 18, 20, 24 with its short name, design name and whether it points, in both directions
- [X] T057 [US5] Add every icon the components and screens of this spec name with `tools/icons.mjs add`, `--points` for arrows, chevrons, "back", "next", sign-out; run `node tools/icons.mjs check` and remove what is unused (FR-024, SC-006)
- [X] T058 [US5] Time the quickstart §4 path for one new icon — `list`, `add`, use it in a component, see it in the gallery — and record that it took under five minutes (SC-007)

---

## Phase 8: User Story 6 — The patterns (P3)

**Goal**: the five patterns composed only of the gallery's components.

**Independent test**: each pattern beside its pattern page, both languages, both widths.

- [X] T059 [P] [US6] Search & Filter (`111:2`) in `gallery/sections/pattern-search-filter.section.ts`: search at the reading start, «تصفية» at the other end, the panel opening downward, applied chips with «مسح الكل», facets only for visible columns, "nothing to filter" (CL-SH-046 … CL-SH-050)
- [X] T060 [P] [US6] Page States (`117:2`) in `gallery/sections/pattern-page-states.section.ts`: loading, empty, error, offline, read only, not in the plan as drawn; the states the page does not draw are built from the components and listed as undrawn (SH-097)
- [X] T061 [P] [US6] Confirmation (`120:2`) and Multi-step Form (`123:2`) in `gallery/sections/pattern-confirmation.section.ts` and `pattern-multi-step-form.section.ts` (CL-SH-060, CL-SH-061)
- [X] T062 [P] [US6] Onboarding (`253:2`) in `gallery/sections/pattern-onboarding.section.ts`, example data only
- [X] T063 [US6] Kept pictures and coverage for the five patterns in `e2e/gallery/pictures.spec.ts`; open each beside its page

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T064 Production weight (R15, SC-006): `npm run build:all` with no size warning for `ops`; `tools/check-production.mjs` passes; record the `ops` initial size before and after in `validation.md`
- [X] T065 [P] Update `docs/setup.md` (the gallery's address; the icon tool; accepting kept pictures with `--update-snapshots` only after looking) and `docs/adding-a-module.md` (a screen takes its parts from `ui` or the console's own components; a missing part is added there and to the gallery, never drawn on a screen)
- [X] T066 Run every gate with nothing else running: `npm run ci`, `npx playwright test` (all apps), the console's suites and the gallery suite in Edge (`-c` with `channel: 'msedge'`); fix what fails
- [X] T067 Walk the gallery in the browser as quickstart §1–§2: every section opened at 1440 and 390, both languages, both looks; every live example worked by keyboard
- [X] T068 For each design page, make the sheet laying its frames beside its specimens (both languages; both looks when shared) and open every one; write each difference and its decision in `validation.md` (FR-027, SC-002)
- [X] T069 Make `specs/095-design-components/design-vs-real.png`: one frame of each design page beside its specimens, Arabic and English, and open it
- [X] T070 Raise with the owner what reading the files found and the spec leaves to them: the week's first day in the calendar (the English frame starts on Sunday), the two files' differences (Badge, Avatar, Select), the two colour collections of the operators' file; record the answers in `validation.md`
- [X] T071 Write `specs/095-design-components/validation.md`: what was run with counts against T001; the gallery walked; pictures taken, opened and kept; the sheets opened; the clinic's comparison; the operators' screens' changes; differences with whose decision; what was built differently from the plan; what was **not** checked (Firefox, Safari, a screen reader itself)
- [X] T072 Mark row `095-design-components` of `docs/reference/brd/SCS-SpecKit-Phases.md` as built in its notes
- [X] T073 Walk the operators' screens on the real local stack after US3 (constitution VII): the "By hand" steps of `specs/011-operators-roles/quickstart.md` and spec 010's sign-in, code, forgot password and profile, with walk accounts invited through the Migrator only — never the seeded operator — in the user's own browser left signed in as it is (a separate Playwright browser); read the API's log of the walk; record it in `validation.md`

---

## Dependencies & Execution Order

- **Setup (T001–T003)**: T002 before any change to a token, a component or an `index.html`.
- **Foundational (T004–T013)**: T004 before T005 and T012; T005 before any component task; T007 before T009; T008 before any component naming a new icon; T010 before T011, T012, T013.
- **US1 (T014–T042)**: after the foundation. T023 before T024–T026; T029 before T034 (skeleton in the table); T032 before T034 (pager); T027 and T033 before T051. T041 after the components it shows; T042 last.
- **US2 (T043–T047)**: after the live examples of US1 exist (each test can be written page by page as US1 progresses).
- **US3 (T048–T052)**: after the components each screen uses; T052 last.
- **US4 (T053–T055)**: T053 any time; T054 after US3 (the last change to shared parts); T055 after US1.
- **US5 (T056–T058)**: T056 after T008; T057 after US1 and US3; T058 after T057.
- **US6 (T059–T063)**: after the components they compose.
- **Polish (T064–T073)**: last; T066 → T073 → T067 → T068 → T069 → T070 → T071.

### Parallel opportunities

- T004 beside T007; T009 beside T010 once T007 and T008 are done.
- Within US1, every task marked [P] (T014–T022, T026–T040) touches its own files and section: run them side by side, a few at a time so each is looked at.
- US3's T049–T051 together after T048. US6's T059–T062 together.

## Implementation Strategy

1. **MVP**: Setup, Foundational and US1's first pages (Button, Input, Select, Checkbox, Switch) — the gallery shows real components in both languages and looks. Stop and show the owner the gallery before going on.
2. Then the rest of US1 page by page, US2's tests beside it.
3. Then US3 (the console moves), US4 (the clinic held), US5 and US6, and the polish phase with every gate, every sheet opened and `validation.md`.

## Notes

- Requirements → tasks: FR-001 T014–T040 · FR-002 T021, T024–T026, T032, T034–T036, T038–T040 · FR-003 T005, T006, T068 · FR-004, FR-005 T012, T055 and each shared component's task · FR-006 T014–T037 (parts brought in place) · FR-007, FR-008 T043 · FR-009 T044 · FR-010 T008, T015, every section in Arabic · FR-011 T023, T045 · FR-012 T047 · FR-013 T046 · FR-014 T045 · FR-015 T010, T011 · FR-016–FR-018a T010, T012 · FR-019 T059–T062 · FR-020 T010, T039, T040 · FR-021 T048–T052, T073 · FR-022 T006, T051 · FR-023 T002, T054 · FR-024–FR-026 T007–T009, T056–T057 · FR-027 T068 · FR-027a T013 · FR-027b T042, T063 · FR-028 T043 · FR-029 T002, T054.
- Success criteria: SC-001 T013, T042 · SC-002 T068 · SC-002a T042 · SC-003 T043 · SC-004 T054 · SC-005 T052 · SC-006 T011, T057, T064 · SC-007 T058 · SC-008 checked by spec 012 onward.
- Not built by any task, and said so in T071: the clinic's own parts (page `4228:7`); moving the clinic's screens onto the components; a gallery in the clinic console; the behaviour behind AI, charts, video and onboarding.
- The implementation is not complete until T066 to T071 and T073 are done and written down (constitution VII), and every picture and sheet has been opened.

## Phase 10: Convergence

- [X] T074 Picture the clinic's and patients' screens again (`bash node_modules/.tmp/before095.sh after095c`) and compare them with `tools/compare-pictures.mjs` against `before095`, `before095b` and `before095c`, opening every picture it flags, since `scs-select` (`searchText`), `ui/src/lib/list/list.ts` (the still pages' menu) and the slider's fallbacks changed after T054; record the result in `validation.md` per FR-029 / SC-004 (partial)
- [X] T075 Run the console's five `e2e/ops-*.spec.ts` files and the gallery's suites in Microsoft Edge after T068's fixes, T070's week and T073's guard fix, and record the counts in `validation.md` per the spec's Assumptions (Chromium and Edge) / Constitution VII (partial)
- [X] T076 [P] Add unit tests for the options T068 added — `scs-select` `searchText` in `src/web/projects/ui/src/lib/controls/select.component.spec.ts` (or the controls' spec), `scs-table-footer` `menuShown` in the list's spec, `app-gallery-tile` `more`/`moreLabel` in `src/web/projects/ops/src/app/components/media/media.spec.ts` — per plan: Testing (partial)

## Phase 11: Convergence

- [X] T077 Bring the T071 summary in `specs/095-design-components/validation.md` up to date with T074–T076: unit tests ui 179 and ops 40 in its table, Edge as T075's 327 passed (the console's 120 and the gallery's 207, the kept pictures held in Chromium only), and "Edge again after T068's fixes" taken off its "Not checked" list, per T071 / Constitution VII (partial)
