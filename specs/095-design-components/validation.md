# Validation: Design Components (spec 095)

**Started**: 2026-10-16 · **Branch**: `095-design-components`, cut from `develop` after spec 011
was committed (`e73be71`) and `develop` fast-forwarded to it locally (nothing pushed).

## Baseline (T001)

Spec 011's last runs, as written in `specs/011-operators-roles/validation.md`: web unit 68 / 1 /
27 / 164 / 107 (clinic, patient, ops, core, ui); browser 755 (all apps, Chromium); the console's
five files 118 in Chromium and 118 in Edge.

Run again before any change of this spec (16 Oct 2026): `npm run ci` passed — lint, the
translation check, the tools' tests, unit tests 68 / 1 / 27 / 164 / 107, the three builds. The
`ops` initial bundle: **335.15 kB** raw (91.11 kB transferred).

## The clinic's and patients' "before" pictures (T002, T053)

Built differently from the task's words, with the same aim: instead of threading a picture call
through the 13 suites that took none, every test now leaves a picture of its last screen when
`SCS_SHOTS` is set (`playwright.config.ts`, `screenshot: 'on'`), and `pictureFor` also takes each
state at 390. No assertion changed. Pictures for comparing are taken still: with `SCS_SHOTS`, the
browser asks for reduced motion and the helper finishes finite animations — before that, the
clinic's side bar was caught in its 0.2-second widening after the resize to 1440, at a different
width each run (it did so before this spec too).

Every clinic and patient suite (21 files, 395 tests, all passing) was run three times into
`src/web/node_modules/.tmp/before095`, `before095b`, `before095c`: each time 189 states at 1440,
189 at 390 and 395 last screens — **773 pictures**. Two runs of the same code still differ in 15
pictures; held against the closest of two other runs, a third differs in **6**, all last screens
caught at a different moment (opened side by side: the roles loaded in one and still loading in
the other; the bell's panel open in one and closed in the other, twice; the branch and account
menus a few pixels apart; the error's reference number, which is random). So the "after" pictures are held against all three runs with
`tools/compare-pictures.mjs` (`--also`), and every picture it flags is opened.

`tools/compare-pictures.mjs` (T053) decodes the pictures itself (no package, no browser); its
tests in `tools/compare-pictures.spec.mjs` (4) run with the tools' tests, which now take every
`tools/*.spec.mjs`.

## The components, as built (US1)

Each was read from its page in both twins before building, and its section was opened at 1440 and
390 in both languages and both looks. Differences found, with whose decision each is:

- **Button** (`31:302`/`316:470`; clinic `4252:2227`). Six types, three sizes (32 · 40 · 48; corners
  8 · 10 · 10 in the operators' file, 6 · 10 · 10 in the clinic's), the focus ring two pixels wide one
  pixel outside (red on the destructive type). Busy: the operators' page draws the ring alone in a
  square button; the button keeps its words unseen, so it keeps its width and its name (spec US2).
  The clinic's screens keep the button they had: no `size` keeps the earlier geometry (48 high,
  padding 16), and the clinic look keeps its fade when disabled or busy and its ring beside the
  words. **For the designer:** the clinic file draws a grey disabled button and a lighter hover
  (`#146f8b`) that the clinic's screens do not show yet — they follow when the clinic's screens move.
  The trailing icon of the page's "Next" example is a downward chevron.
- **Found while building:** a border shorthand that holds a variable hides a later colour of one side
  in the built styles (the busy ring's arc); each side is set by itself.
- **Checkbox** (`43:36`/`297:34`) and **Switch** (`43:102`/`297:96`): every state as drawn — the box
  20 with corner 5 and a 1.5 border, its tick and dash, the error below in 11; the track 44 × 24 with
  an 18 knob, the knob at the right when on in both languages (owner, 14 Oct 2026), a ring in the
  knob while busy. Neither was on a clinic screen, so both follow the page in both looks; the clinic
  look takes the clinic's colours. The pressable area stays at least 40 high.

## The icon copy (T007)

All 1,144 icons of the Icons page (`738:7380`) and the 18 menu icons of the Menu page (`2200:86`)
are in `src/web/tools/icons/library/` (55 part files, 712 KB), taken through the design connector:
the connector answers at most 20 KB at a time, so the parts hold 5 to 45 icons each, written by this
session and by three helper sessions in parallel. **Every part was checked against the design file**:
the file rebuilt each part's text and its length and checksum (FNV-1a) were compared with the file on
disk — all 55 equal. (The helpers had made five copying slips — an extra digit in a number — and
caught them with the same check before handing back.) The 14-pixel Streamline icons keep their
stroke codes; the 19 "Rounded" icons are 24-pixel filled drawings in `#374957`, which the tool turns
into the text's colour.
- **Radio** (`43:66`/`298:29`), **Slider** (`55:58`/`300:56`), **Segmented control** (`385:11`,
  `385:12`/`386:19`, `386:20`): new in `ui`. Real radios (the browser moves among them with the
  arrows); the slider's thumbs are `role="slider"` with steps, Page Up/Down, Home and End, and a
  range's thumbs never cross; the segmented control is a radio group of pills. In Arabic the slider
  fills from the right as its twin draws it; its value ("30 – 75") is written left to right, as the
  frame writes it.
- **Input** (`54:257`/`304:257`): twelve states in three sizes (32 · 44 · 60), the required asterisk,
  hover, focus (1.5 border and a ring two pixels out), typing ("Active", a 2 border), disabled, read
  only, error, success, warning and busy, each with its mark at the end and its message in 11. The
  clinic's sign-in and password screens use this field: with no `size` it keeps its earlier geometry,
  and the clinic look keeps its shadow, its 1-pixel focus border and its placeholder colour. **For the
  designer:** the Arabic twin writes its messages in English ("Please enter a valid email address",
  "Looks good!"); the gallery shows Arabic words there.
- **Textarea** (`146:95`/`304:369`): six states in three sizes (72 · 96 · 120), hover darker than the
  input's, focus a 2 border with no ring, the count at the end turning red with an error. Not on a
  clinic screen; follows the page in both looks.
- **File Upload** (`157:72`/`304:440`): the zone (default, hover, a file held over it with the page's
  own words "Drop your file here · Release to upload") and under it the card of spec 009 for
  uploading, uploaded and failed. The operators' look draws the zone 132 high with a 1.5 dashed line,
  an outlined mark and a purple hover, and the card grey, green or red with corner 10; the clinic's
  development page keeps spec 009's drawing. **For the owner (T070):** the operators' page draws the
  uploading card with a 6-pixel bar and "62% · Uploading…" under the name, and the failed card with
  "Try again" as words, where the shared card (spec 009, from the clinic file) has a spinner and icon
  buttons; and the zone's words differ ("Click to upload or drag and drop" against spec 009's "Drag a
  file here or click to browse"). The card and the words are kept as spec 009 built them until the
  owner decides.
- **Select and its list** (`57:43`/`303:40`; lists `58:3`, `58:14`, `58:30`, `2224:70`): the trigger's
  six states (hover and focus now also as still specimens), the disabled label grey, the error in 11
  red; the list built in spec 011 gains **several choices** (`multiple`: a box on each row, the list
  stays open, `aria-multiselectable`) and can be drawn in place for the gallery. **Undrawn:** the
  trigger holding several choices — the gallery writes their names one after another.
- **Days under the calendar** (`ui/date/calendar.ts`, research R11): a day is `YYYY-MM-DD`, a whole
  day no time zone moves; "today" is Cairo's (past Cairo's midnight it is already tomorrow, summer
  and winter time both tested); six weeks a month so the panel keeps its height, as `75:4` draws.
  **The week's first day: Sunday in both languages** — `75:4` heads its columns "Su Mo Tu We Th Fr
  Sa"; the components page has no Arabic calendar panel, and the only Arabic one in the file (the
  archived "AR Dashboard — Date Filter Open", `1923:9835`) mirrors it, «ح» (Sunday) on the right and
  «س» (Saturday) on the left, single letters as `weekday: narrow` gives them. **Digits:** the Arabic
  date trigger (`308:52`, Filled) writes «٢٨ أغسطس ٢٠٢٦» in Arabic-Indic digits, and the archived
  panel uses Western ones; the operators console keeps Western digits (OP-B-386), the clinic its own
  rule (Arabic-Indic, core's `SCS_ARABIC_DIGITS`). For the owner (T070).
- **Date field and calendar** (`74:53`/`308:52`; calendar `75:4`, range `76:3` · clinic `4256:824`,
  `4256:1424`, `4256:885`): new `scs-date-field` and `scs-calendar` (one day or a range) in `ui/date/`.
  The trigger is drawn from the text field's `--scs-field-*` settings; the calendar from new
  `--scs-calendar-*` settings — the clinic's 40-pixel teal days by default, the operators' 36-pixel
  purple days, bordered arrows and 32 under the last week in `ops/src/styles/tokens.css`. The
  operators console now provides Western digits (`SCS_ARABIC_DIGITS = latn`, OP-B-386) and the week
  from Sunday (`SCS_CALENDAR_WEEK_START`); the clinic's default is Saturday, as `4256:887` draws, and
  its own digits. Keys as `contracts/components.md` (arrows follow the page's direction; Page Up/Down
  a month, with Shift a year; Home/End the week's edges; Escape closes with the focus back on the
  trigger; Tab goes round inside the open calendar). Looked at in English and Arabic, operators' and
  clinic looks, beside `74:53`, `75:4`, `76:3` and `4256:887`: same. **Differences:** the range band
  is drawn from the middle of its first day to the middle of its last, and from the row's edge when it
  continues — `76:3` starts the second row's band in the middle of the 16th and runs the first row's
  past the panel's edge; the clinic's calendar ends with a link to today («اليوم»), kept as the option
  `todayLink` (the gallery shows it in the clinic look); the clinic's date input (`4256:824`) is a field
  typed into ("يوم / شهر / سنة") where the operators' is a button that opens the calendar — the
  operators' page wins (spec), the typed field is not built. For the owner (T070): **the week starts on
  Sunday in the operators' file and on Saturday in the clinic's.**
- **Time field** (`77:53`/`308:103`; list `78:4`, wheels `78:16` · clinic `4256:1542`, `4256:1480`):
  new `scs-time-field` in `ui/date/` with `mode` list or wheel and `step`; times are `HH:mm` on the
  wall clock, written "2:30 PM" · «2:30 م». The list: the arrows, Home, End, Enter, Escape; a refused
  time is grey and cannot be chosen. The wheels: three spin buttons (hour, minute by `step`, the half
  of the day) turned with the arrows, Page Up/Down or the mouse wheel, kept with "Done" («تم»).
  New `--scs-time-*` settings: the clinic's two columns of 36-pixel teal slots by default (`4256:1525`),
  the operators' 200-pixel single column with a purple chosen row and its tick in
  `ops/src/styles/tokens.css`. Looked at beside `77:53`, `78:4`, `78:16` and `4256:1525`, both
  languages and looks: same. **Differences:** `78:16` draws the minutes 15, 25, 30, 35, 45 — not
  evenly spaced; the wheel turns by `step` (5 in the gallery: 20, 25, 30, 35, 40); the clinic writes
  its slots with two-digit hours («٠٩:٠٠ ص») where the operators' list writes "9:00 AM" — both use
  the one-or-two-digit hour; the wheels are operators' only (the clinic file draws none).
- **Date range and date & time, the operators' own** (`392:53`/`393:112`, calendar `393:2`;
  `79:71`/`308:172`, panel `80:4`): `app-date-range-field` in `ops/src/app/components/date-range/`
  and `app-date-time-field` in `ops/src/app/components/date-time/` (the plan's `components/` folder;
  the `app-` prefix the console's lint asks for, where `contracts/components.md` wrote `ops-`). Both
  draw their trigger from the shared trigger styles (`SCS_DATE_TRIGGER_STYLES`, now a constant in
  `ui/date/`). The range: a start and then an end, closing on the end — "Aug 1 – Aug 28, 2026" ·
  «1 أغسطس – 28 أغسطس 2026», the year once (`formatRange`). The date & time: the calendar and the
  times in one panel (the time list is now its own `scs-time-list`, which the time field uses too);
  a day moves the focus to the times, a time with a day keeps both, `YYYY-MM-DDTHH:mm`; Tab goes
  round both sides. Looked at beside `392:53`, `393:2`, `79:71` and `80:4`, both languages: same.
  On a phone the times go under the calendar (the page draws only the wide panel). The gallery's
  section for "Date & Time Picker" is `date-and-time-picker`, as the inventory's name gives it.
  **Undrawn:** the presets the contract mentions — `392:2` draws none, so none are built.
- **Avatar and Badge** (`294:131`/`8:111`, clinic Avatar `4252:62734`; `294:147`/`8:86`, clinic
  `4253:53652`): new `scs-avatar` (`ui/avatar/`: letters of the first two words or a picture that
  gives way to them, `role=img` named; `size` sm/md/lg; `tone` brand or neutral) and `scs-badge`
  (`ui/badge/`: `intent` success/warning/danger/info/neutral/brand, `solid`, `size` sm/md, `dot`,
  `icon`). `scs-status-mark` is now the badge under its old name (its `tone` read as the intent), so
  the operators' screens of spec 011 keep working and take the badge's look. New `--scs-avatar-*`
  and `--scs-badge-*` settings: the clinic's 32/40/56 avatars and round 24-pixel badges by default,
  the operators' 28/44/64 and 28-pixel badges with corners 8. Looked at beside both files, both
  languages and looks: same. **Differences between the files (for the owner, T070):** sizes as
  above; the clinic's avatar has an "Admin" purple track that no token names — not built (brand and
  neutral are); the clinic's badge colours are a shade off its own tokens (success `#f0fbf6`/`#116932`
  against `#ecfdf3`/`#085d3a`) — the tokens are used; the operators' badge is drawn with its dot
  start-aligned in both languages and its word clipped tight — drawn here with 10 on both sides.
- **KPI Card and Sidebar Nav Item** (`294:315`/`8:135`, clinic `4267:1175`; `294:323`/`8:119`, clinic
  `4252:60310`): `scs-kpi-card` gains `trend` (positive green, negative red, neutral grey, none — the
  clinic's card without a change line) and the clinic's `caption`; the icon and its `tone` of the
  operators' list template (spec 011) stay as an option. New `scs-nav-item` (`ui/shell/`): a link
  marked as the current page (`aria-current`, tint, a bar at its start), disabled, and the clinic's
  count, group that opens (`expandable`) and sub-item (`level` sub). New `--scs-kpi-*` and
  `--scs-nav-*` settings: the clinic's 40-pixel row and card by default, the operators' 48-pixel row,
  corners 14 and 28-pixel number. Looked at beside both files, both languages and looks: same.
  **Differences:** the operators' note mark is drawn 100 wide whatever it says — here it fits its
  words; the clinic's change mark carries an arrow no icon of the copy draws yet — not shown; the
  operators' Arabic KPI twin repeats the first card three times — the gallery writes each card's
  own words in Arabic.
- **Loading** (`289:27`, `289:38`, `289:48` / `314:54`, `314:65`, `314:75`; composed `290:4`, `290:11`,
  `290:22`, `290:31` · clinic `4252:59832`, `4252:59743`, `4252:59877`): new `scs-spinner` (a faint
  ring and three eighths of it in colour, its stroke 12% of its side; `size` sm/md/lg/xl; `tone`
  brand/neutral/inverse; a `label` makes it a status, without one it is a quiet picture beside words)
  and `scs-progress` (`ui/loading/`: a share 0–100 as a `progressbar`, or a running segment when not
  known; `intent`); `scs-skeleton` gains `widths` and the new `scs-skeleton-block` (a block or a
  circle) builds the page's composed card and table rows. New `--scs-spinner-*`, `--scs-progress-*`
  and `--scs-skeleton-*` settings; the skeleton's defaults are its look before spec 095 (FR-023), the
  operators' 14-pixel `#eceef1` lines in `ops/src/styles/tokens.css`. Looked at in both languages and
  looks beside the page: same. **Differences:** the clinic's spinner (`4252:59832`) is a drawn arc of
  16 to 32 with a 1.5 stroke, the operators' a ring of 16 to 48 — one ring is built, sized per look
  (clinic 16/20/24/32); the Arabic twin writes «٦٢٪» — Western digits in the operators' console.
  Moving still pictures: with reduced motion the spinner turns slowly and the unknown progress stands
  still a little in.
- **Alert** (`60:46`/`257:71`; clinic `4253:54128`): `scs-banner` and the new `scs-toast` (the toast
  outlet now draws each notice with it, so a toast can stand still in the gallery). The clinic's
  banner and toast are their looks before spec 095 and stay the defaults (FR-023: the banner's icon
  now sits in a 20-pixel mark of the same place; its colours go through one variable, unchanged).
  The operators' banner — a 4-pixel bar of the intent's colour at the start, the icon in a 28-pixel
  tinted circle, title and words in the intent's colour, no border, corners 12, a grey close — and
  toast — no border, corners 12, a deeper shadow, a short bar in place of the icon — come from new
  `--scs-banner-*` and `--scs-toast-*` settings (two of them switches, 0 or 1, mixed into colours,
  since a look's setting cannot name the intent's colour itself). Looked at beside `60:46`, both
  languages and looks: same. **Differences:** the operators' toast draws no close — the close stays
  (so a passing notice can be closed from the keyboard); the page's error banner draws the
  warning's triangle — the console's danger mark (a circle) is kept; the gallery has its own toast
  outlet, being outside the console's frame.
- **Modal** (`62:57`/`309:116`; clinic `4252:61695`): `scs-dialog` gains the page's four sizes (`size`
  sm 360, md 480, lg 640, full 900, never wider than the window) and `still` (open in place, without
  dimming the page, for the gallery). Its look is kept as the owner chose — no lines under the title
  or over the buttons, its own corners and shadow — so the page's dividers and 42-pixel buttons are not
  drawn. Looked at in both languages, wide and on a phone. In the gallery the cells are 660 wide, so
  the large and full-screen specimens show the large width; the full width is seen in the working
  window.
- **Navigation** (`284:12`, `284:21`, `284:34`, `284:53` / `312:62` … `312:103`; menu `2104:140`;
  folded `2111:195`/`2111:285` · clinic `4252:60720`, `4252:60901`, `4252:60984`, `4252:61106`):
  `scs-tabs` gains the keys of a tab list (arrows in the page's direction, Home, End; only the chosen
  tab in the Tab order); `scs-breadcrumb` its per-look words; the paging of `scs-table-footer` folds
  past six pages — the three pages around the current one, "…", the last three — and "…" opens
  upwards a menu of every page not shown (`moreLabel` names it), exactly as `2111:195` draws on pages
  2 and 5; on a phone the paging wraps under the count. Each page button is `.scs-page-item`
  (controls.css), so the gallery shows one alone. New `scs-stepper` (`ui/stepper/`): done steps ticked
  (and said to be done), the current one ringed and `aria-current="step"`, the rest grey. New
  `--scs-tabs-*`, `--scs-crumb-*`, `--scs-pager-*` and `--scs-stepper-*` settings: the defaults are the
  parts before spec 095 and, for the stepper, the clinic's (name beside the circle, a line to the
  next); the operators' 36-pixel white pages with corners 8, "…" in a hairline with its chevron, and
  steps with their name under the circle. Looked at in both languages and looks beside the page:
  same. **Differences:** the clinic's upcoming step ring is `#cbd3d9`, which no clinic token names —
  its control border is used; the clinic's tabs draw a count pill in the brand's tint when chosen —
  the tabs keep their count as before.
- **Data display** (`281:27`, `281:64`, `281:77`, `281:88`, `281:107` / `313:26` … `313:106` · clinic
  `4252:61428`, `4253:53827`, `4260:1383`): `scs-card` keeps its look (the owner's) and gains
  `interactive` (a tint under the pointer) and `selected` (the brand's tint and border); new `scs-tag`
  (`ui/tag/`: `intent` default/success/warning/neutral, `size`, `icon`, `dismissible` with its named
  remove button, the clinic's `selected` and `disabled`), and `scs-applied-chips` now draws each chip
  as a tag, keeping the chip's look of before through `--scs-chip-*` (the operators' chips take the
  tag's look); new `scs-accordion` / `scs-accordion-item` (`ui/accordion/`: the heading is a button
  that says whether its region is open). The console's own `app-divider` (plain, a picture; or with
  a word, a named separator) and `app-stat` in `ops/src/app/components/`. New `--scs-tag-*`,
  `--scs-chip-*`, `--scs-accordion-*` settings: the clinic's grey 32-pixel pill and divided rows by
  default, the operators' 28-pixel purple pill and separate cards with corners 12. Looked at in both
  languages and looks beside the page: same. **Differences:** the clinic's tag has no intents, only
  states — its plain grey is the default; the operators' tag has no selected or disabled state — the
  clinic's are used in both; the Arabic stat twin writes «١٤٨» — Western digits in the operators'
  console.
- **Table** (`273:52`/`309:166`, example `273:55`; clinic `4267:903`, `4267:1004`, `4267:744`): the
  table's rows gain their states in `ui/list/list.css` — under the pointer when it opens its record
  (and still in the gallery), chosen with `aria-selected="true"` (the brand's tint) — and every part
  of the table reads `--scs-table-*` settings whose defaults are the table before spec 095; the
  operators' look draws the page's table: a grey header with a line under it, 12 semibold grey heads,
  rounded rows without lines between them, corners 10. `scs-checkbox` gains `labelHidden`, so a row's
  box is named without showing its name. The composed table scrolls sideways inside its frame on a
  phone; the gallery's check of "outside its frame" now judges what is inside a box that scrolls
  sideways against that box (`e2e/ops-platform.ts`), the box itself still against the window.
  Looked at beside `273:55`, both languages and looks: same. **Differences:** the page draws the plain
  cells ("Professional", "8 users") in grey — the table keeps them in the text colour, as the
  operators' screens of spec 011 have them; its header leaves the box column blank — the gallery
  names it.
- **Navbar and Notifications** (`278:3`/`311:16`, unread `2403:6546`, logo `2467:133` · clinic
  `4252:60501`; bell `362:23`, item `362:43`, panel `364:32` · clinic `4262:1056`): new `scs-navbar`
  (`ui/shell/`: the mark or an uploaded logo and the console's name as a link home; a start slot for
  the side bar's button; an end slot for the bell and its panel; the person's name, role and initials
  as the button of their account menu — `aria-haspopup="menu"`, Down opens it — with a slot for the
  menu itself; on a phone the names give way to the initials and the bar never widens the page). New
  `scs-notification-bell` (a round button named with its count; a red number, or a dot), `scs-
  notification-item` (a menu item: an unread one is tinted, says "Unread" before its title and carries
  a dot) and `scs-notification-panel` (title, "Mark all as read", the notices as a menu — the arrows
  move, Escape closes — "View all notifications") in `ui/notifications/`. New `--scs-navbar-*` and
  `--scs-notice-*` settings: the clinic's corners 16, tinted notices and grey footer by default, the
  operators' corners 12, purple-tinted notices with a dot, white footer. Looked at in both languages
  and looks beside the page: same. The operators' frame moves onto `scs-navbar` in T048; the clinic's
  shell keeps its own bar and bell panel (contract).
- **Menu** (`368:23`/`370:38`, panel `368:26`, row menus `2201:86`, icons `2200:86`; clinic
  `4252:61814`, `4252:61893`): `[scsMenuItem]` gains `disabled` (grey, cannot be chosen, its reason
  in a `.reason` line) and the destructive choice's pale red under the pointer (Danger Hover); the
  menu, its items, its separator and the row's kebab button read `--scs-menu-*` and
  `--scs-row-menu-*` settings whose defaults are the menu before spec 095 — the operators' 200-wide
  panel with corners 10, 13-pixel items with corners 6 and a 10 gap, and a round kebab that tints
  under the pointer. The 18 drawn 16-pixel menu icons are in the registry as `menu-*` (`tools/icons.mjs
  add`; `menu-send` and `menu-refund` point and turn in Arabic). The gallery shows the item's four
  states, the panel, the icons, the kebab's three states, and all ten row-actions menus of 2201:86 in
  both languages, and a working row menu (Enter, Space or Down open it on the first item, Up on the
  last; the arrows wrap; Escape and Tab close). Looked at beside `2201:86`: same. The menus opened
  over the page take the page's look (the operators'), also when the gallery shows the clinic look.
- **Empty State and Tooltip** (`374:35`/`374:68`, clinic `4252:60047`; `383:23`/`384:22`, clinic
  `4252:59606`): `scs-empty-state` gains the page's `loading` (a turning ring and its words, said
  politely) and `success` (a green tick, said politely) beside empty, no results, error, not in plan
  and not found; its mark, title and words read `--scs-empty-*` settings (the operators' 72 grey
  mark, 17 semibold title, 14 grey words). `[scsTooltip]` gains `scsTooltipPlacement` — top, bottom,
  start, end (the page's left and right are start and end, mirrored in Arabic) — trying the other
  sides when there is no room, and an arrow at the side of its control; its bubble reads
  `--scs-tooltip-*`. Looked at in both languages and looks beside the pages: same. **Differences
  (for the owner, T070):** the clinic's file draws the tooltip with an arrow too, but the clinic's
  tooltip before spec 095 had none and keeps none (FR-023) — the arrow is the operators' look's
  setting; the operators' empty page draws a magnifier for "no clinics yet" and a round "!" for the
  error — the console's own marks (a clipboard, a triangle) are kept; its loading ring is 56 wide —
  the spinner's largest size, 48, is used.
- **Usage Meter and Chart** (`387:24`/`388:23`, clinic Plan Usage Meter `4252:63959`; Sparkline
  `394:15`/`395:14`, bar chart `394:16`/`395:15`, clinic Bar Chart `5601:4517`): new
  `scs-usage-meter` (`ui/usage-meter/`: what is counted and how much over a track filled to the share,
  a line under it; `level` normal/warning/critical/exceeded, worked out from the share when not given;
  a `meter` for assistive technology; the amount is written in its own direction, so «85 / 100 جيجا»
  reads right in Arabic) and `scs-bar-chart` (`ui/chart/`: bars to their share, the highest — or the
  chosen — in the brand's colour with its number over it, a bar's number under the pointer, the numbers
  also in a table for assistive technology; the clinic's value lines behind the bars by default). The
  console's own `app-sparkline` (`ops/src/app/components/chart/`, SVG drawn from its numbers, green up,
  red down, grey level, a dot at its end, left to right in both languages). New `--scs-meter-*` and
  `--scs-chart-*` settings. Looked at in both languages and looks beside the pages: same. **Departure
  from research R12:** the bar chart's bars are drawn as boxes, not SVG, so their words and the page's
  direction come for free; the sparkline is SVG. **Differences:** the clinic's bar chart (1104 wide,
  with a hover state) is drawn at the gallery's width with four value lines; its numbers are worked out
  from the highest bar.
- **Media, the operators' own** (`82:20`/`314:19`; avatar group `84:3`, video `84:15`, carousel
  `84:26`, tiles `84:39` … `84:67`): `ops/src/app/components/media/` — `app-image` (empty with a
  picture mark, a shimmer while loading — still with reduced motion — and a pale red box with "Failed
  to load image" when it cannot be shown), `app-avatar-group` (four avatars ringed in white and "+N",
  the group named with everyone), `app-video-player` (the browser's `<video>` with the drawn round
  play button, named and turning to pause, the length in its chip and a thin bar of what has played),
  `app-carousel` (round previous and next, a dot for each picture saying which is shown, the arrows of
  the keyboard in the page's direction) and `app-gallery-tile`. New icons `play` and `pause` from the
  copy. Example pictures are drawn in `gallery/examples/media-examples.ts` (nothing fetched, no file
  kept); there is no example video — a video file would have to be downloaded, so the player shows its
  poster. Looked at in both languages beside the page: same. **Difference:** the avatars overlap by 10,
  not 14, so the initials are whole in Cairo.
- **AI** (`399:13`, `401:19`, `404:10` / `408:21`, `408:39`, `409:29`; Ask-AI `402:3`, `402:6`, chat
  `405:2` and their Arabic twins; suggestion card states `2421:88`/`2421:112` · clinic Insight Card
  `4309:491`, Chat Bubble `4252:63119`): new `scs-insight-card` (`ui/ai/`: heading with the AI mark
  when the finding is the AI's and plain when AI is off, the clinic's big number and impact line, words,
  projected actions; loading shapes, "nothing to suggest", and the failed state with "Retry") and
  `scs-message-bubble` (bot, the person's own, incoming, outgoing, a note; sender, assistant's tag,
  time, an attached picture's name, a failure said at once). The console's own `app-ai-badge`,
  `app-ask-ai-button`, `app-ask-ai-field` and `app-chat-widget` (`ops/src/app/components/ai/`) hand
  what is typed to the page and send nothing (Constitution V); the gallery answers with a fixed line
  saying it is an example. New `--scs-insight-*`, `--scs-bubble-*` and `--scs-ai-*` settings; new icons
  `sparkle` and `send` from the copy. Looked at in both languages and looks beside the page: same.
  **Differences:** the copy has no four-pointed sparkle — the magic wand of the copy stands for it
  (for the owner, T070); the clinic's insight card is a big-number card where the operators' is a
  sentence with actions — one component carries both.
- **Other parts** (`gallery/sections/other-parts.section.ts`): the parts kept as they are, each in
  its states — the code boxes (empty, filled, refused, accepted), the password rules (while typing and
  after submitting), the language toggle (each language current), the environment strip, the sign-in
  frame (in a 640-high box), "no access", the page header with its action, the list's toolbar with
  its filter panel, and the placeholder panel (which shows the platform as unreachable while the
  api is stopped). On a phone the wider ones scroll inside their own box; the page never scrolls
  sideways (checked at 390 in both languages). Looked at in both languages.

## The kept pictures (T042)

- `e2e/gallery/pictures.spec.ts`: one picture of every specimen of every built section, in both
  languages, at 1440 and 390, and in the clinic's look where the page has shared parts — 126 tests.
  Run twice in a row without accepting, 2026-10-16: 126 passed both times, so the pictures are stable.
- **A fault in the pictures, found and fixed.** The gallery's bar (title, language, look) is sticky.
  A specimen scrolled under it, or taller than the window, was drawn with the bar across it, and such
  pictures had been accepted: at 390, the input's "empty" sizes, the textarea, the tab item, the
  card and the image (clinic and ops), the menu item and the row menus. The bar is now held at the
  top of the page while pictures are taken (a style added to the page; the screenshot's own
  `style` option comes too late for a sticky part). The 38 pictures it had covered were taken again and
  looked at: each now shows its part.
- Before accepting, every changed picture (168) was laid on contact sheets and looked at. What
  remains cut is the crop, not the part: a focus ring reaching 3 pixels past a 40-high button, the
  shadow of a menu item, and table cells that scroll sideways on a phone.
- One picture (the multi-select, Arabic, clinic look, 390) once failed to settle within five
  seconds under the full parallel run; alone it passed four times in four, and both full runs after it
  passed.

## The states and the keyboard (T043–T047)

- **Keyboard (T043)**: `e2e/gallery/keyboard.spec.ts`, 16 tests, pass.
- **Settings (T044)**: `e2e/gallery/settings.spec.ts`: a field's error in place of its hint; a busy
  button; "No results found"; a range in the calendar in both languages; a refused menu choice read
  with its reason; long words in English and Arabic in a field, a drop-down's trigger and its list,
  a tag, a table cell, the badge in it and a menu item — each wraps or ends with an ellipsis inside
  its part, and the words are whole for a screen reader. **Fixed:** a badge did not end a long word
  with an ellipsis (its words ran out of it); they are now in their own span, clipped along the line
  only, so Arabic letters above and below the line are not cut.
- **Words (T045)**: `npm run i18n:check` passes. It found the console's AI badge writing "AI"
  in its template; the word now comes from `app.ai.badge` ("AI" in both files, as the design draws
  it). The gallery's checks (T013) refuse an Arabic-Indic digit anywhere in the operators' look.
- **Reduced motion (T046)**: in `settings.spec.ts`; spinners still or slow (2 s or more), the shimmer,
  the unknown progress and the chevrons still.
- **40 by 40 at 390 (FR-012, T047)**: every pressable control of every working example, checked
  per section, 33 sections. It found, all in the operators' look: small buttons and icon buttons
  (32), the row menu (32), the calendar's days (36) and its month arrows (28), the tag's remove (14),
  the banner's close (24) and action, the breadcrumb's links (20 high), the segmented control (28),
  the table's choice boxes (38 wide), the insight card's Retry and Dismiss, and three of the
  gallery's own controls. **Fixed** as spec 011 did: on a phone the operators' look sets
  `--scs-touch: 40px` (with the calendar's cell 40, gap 2 and arrows 40, so the month keeps its width),
  and each part is at least that. Where the drawing is smaller than 40 (the tag's ×, the banner's
  close, links in a line) the room is made around it with negative margins, so the part keeps its
  size and the line its height; buttons, row menus, the segmented control and the calendar are
  drawn 40 high. The clinic declares no `--scs-touch` and the gallery's clinic look clears it, so
  no clinic part changes: every clinic picture passed unchanged apart from those the bar had covered.
  The page checks now measure a control by the room it holds in the line (its box less its
  negative margins) when checking its container's padding.
- Suites after the changes: unit tests ui 177, ops 39; `settings`, `keyboard` and `coverage` 180
  passed; `test:tools` 28 passed; `icons.mjs check` — every registered icon used (75). Lint is clean
  for spec 095's files; five errors remain in e2e files of earlier specs (`emails`, `ops-console`,
  `ops-look`, `ops-sign-in`) that this branch has not touched.

## The console's screens on the components (US3, T048–T052)

Questions put to the owner on 2026-10-16, and their answers: the top bar keeps the design's order, the
name then the avatar at the bar's far end ("choose the best"); the name and role sit beside the avatar
in both languages (the Arabic file draws them at the far side of a 160-wide box, which reads as a
mirroring slip); a filtered list shows both a count on the Filter button and a chip for each filter
with «مسح الكل», as Search & Filter (`111:2`) and the clinic app do.

- **Frame (T048)**: `ops-shell` now draws `scs-navbar` (the console's name, the operator's name, role and
  initials as the account menu's button) and an `scs-nav-item` for each section. The account menu is
  the shared `.scs-menu`. `scs-navbar` gains `testId` (its parts are `<testId>-brand`, `-account`,
  `-person`, `-role`, `-initials`), `compact` (the console folds its side bar below 1024, wider than a
  phone), an `account` event that says whether it was a press or Down, and `focusAccount()` for Escape.
  The bell stays out until spec 093 brings notices, as spec 011 left it. **Corrections toward the
  frames** (`278:3`/`311:16`, Getting Started `4:2`): the name in bold, the 44 avatar 24 from the edge
  and 8 from the names (new `--scs-navbar-account-gap`, `--scs-navbar-account-inset`), the side bar's
  items 216 wide with 12 at the sides, 16 above and 8 between, 48 high with the bar at their start. The
  tests now read `ops-person` and `ops-role` (were `ops-operator-name`, `ops-operator-role`) and the nav
  item's `on` mark (was `active`).
- **Sign-in and password (T049)**: «تذكرني» is `scs-checkbox` (its 40-high row; the frame `128:85`
  leaves 12 under the password and 20 above the button). The other screens were already on the button,
  text field, banner and link; their success tick and the inline "not sent" line stay as drawn.
- **Profile (T050)**: two-factor is `scs-switch`, on and locked, with its reason read as its
  description; "active now" is `scs-badge` (success, no dot); the red buttons are `scsButton="destructive"`
  (the page's `.ops-danger` is gone). **Corrections**: every button of the page and its dialogs is
  the design's 40 high with 14px words (`1851:2003`: 204×40) — at 48 and 16 the red one's words broke
  onto two lines in the dialog; the locked switch is drawn as `2185:611` draws it — the brand at half
  strength, a white knob, its words dark — through new per-look settings `--scs-switch-locked`,
  `-locked-knob`, `-locked-text` (the clinic keeps its grey). **Differences kept** (spec 010's decisions,
  held by its tests): the dialog's heading starts the line where the frame centres it; fields are 48
  high where the frame draws 44.
- **Operators and roles (T051)**: every status mark is `scs-badge` with `intent`; `scs-status-mark`,
  its `tone` and its type are removed from `ui`. The rows' and the page's initials are `scs-avatar`
  (28 in rows; the operator's page 64 as `1658:70` draws it, was 48); the service's own `initials()`
  is gone for the shared `initialsOf`. The list shows what is filtered: `scs-list-toolbar` gains
  `filterCount`, `filterCountText` and `filterCountLabel` (an 18-pixel round count between the icon
  and the word, read as the button's description: "1 filter applied" · «فلتر واحد مطبّق»), and the list
  draws `scs-applied-chips` («الحالة: معطل»), whose × takes that filter away alone. **Corrections**: the
  chips in the brand's pale tint with brand words and «مسح الكل» right after them (new `--scs-chip-bg`,
  `--scs-chip-ink`, `--scs-chips-clear-push`; the clinic keeps its grey chips and its «مسح الكل» at
  the far end). **Differences**: the frame's rows draw a 36 round mark, between the avatar's 28 and
  44 — the rows keep 28; the English chip in `111:2` has a dot and no ×, the Arabic one an × and no dot —
  both languages have the ×, which is what removes a filter; the add-operator frame (`1690:70`) draws the
  required 2FA switch at full strength where the profile draws it at half — one rule, the profile's.
- **Pictures (T052)**: every picture of the five `ops-*` files taken before (159) and after (161 —
  two new: the filtered list in each language) with `SCS_SHOTS`; 246 of 320 differ. Every pair was
  laid side by side and looked at: the differences are the ones listed above, and the phone pages are
  taller only by the 64 avatar and the 40-high rows. The 118 tests of the five files pass.
- Suites after US3: unit tests ui 177, ops 39; gallery `settings`, `keyboard`, `coverage` 180; the
  kept pictures 126 of 126 (the twelve navbar pictures accepted anew after looking — the gap and the
  avatar's place); `i18n:check`, `test:tools` (28) and the icon check pass; lint clean.

## The clinic and patients after spec 095 (T054)

The "after" pictures were taken exactly as T002 (`before095.sh after095`: 21 suites, 395 tests passing,
189 states at 1440, 189 at 390, 395 last screens) and held against the three "before" runs with
`tools/compare-pictures.mjs --also`.

- **First run: 62 differed** — the clinic's sign-in and password screens at 390 (about 1%) and their
  last screens. Opened side by side, the words under the sign-in button and inside the green and grey
  notices were drawn with another kind of anti-aliasing (coloured sub-pixel fringes in place of grey
  ones); nothing moved. To find why, the clinic of the last commit (before spec 095) was built in a
  throw-away worktree and served beside today's: every element's box, colour and font on the sign-in
  page matched to the hundredth of a pixel (the links' `display` became `flex` from `block`, which
  moves nothing). A bisection — the old clinic built with today's `ui`, then with today's
  `controls.css` alone — and a measure of the coloured fringes (old 0.14, new 0.60) found it: spec
  095 had given `.scs-button` and `scs-banner` `position: relative` (and the banner `overflow:
  hidden`), which the operators' busy ring and coloured bar need. A positioned element changes how
  Chromium lays the text under it in layers, and with it the anti-aliasing. **Fixed**: the button is
  placed only while busy and only where a look asks (`--scs-button-busy-anchor`, the operators'
  `relative`); the banner is placed and clipped only where a look asks (`--scs-banner-anchor`,
  `--scs-banner-clip`, the operators' `relative`/`hidden`). The fringes measure 0.14 again.
- **After the fix: 0 of 189 at 390 differ**, and 6 last screens and 1 state at 1440, each opened
  and each a moment of capture already seen between the three "before" runs: the roles loaded in one
  and loading in the other; the bell's panel open in one and closed in the other; the account menu a
  few pixels apart; the error's random reference; two password screens 0.1% apart in a field being
  typed into; and one clinic test whose last screen is the **operators'** sign-in, which changed on
  purpose (T049's «تذكرني»). No clinic or patient screen changed (SC-004, FR-023).
- Afterwards the console's 118 tests, the gallery's 180 and its 126 kept pictures passed again with
  nothing to accept (the operators' busy ring and banner bar are drawn as before); unit tests ui 177.

## The shared parts beside the clinic's file (T055)

Every shared part (37, `kind: 'shared'` in the catalogue) was pictured in the gallery's clinic look,
in Arabic at 1440 (`node_modules/.tmp/t055/ours`, on ten sheets), and laid beside its drawing in the
clinic file (`4228:2`–`4228:7`), each opened. The gallery draws each set as the **operators'** file
names its variants, in the clinic's colours and sizes; what follows is what the clinic's file draws
that the shared part does not, or draws otherwise — for the designer (FR-005, US4 scenario 3). None of
it changes a clinic screen today (T054).

| Part | The clinic's file | The shared part in the clinic look |
|---|---|---|
| Avatar | four sizes; teal, purple and grey; a person icon when there is no name | three sizes, the brand's tint; grey only in the working example |
| Badge | no dot; soft and solid; two sizes; six intents with teal | a dot unless a screen sets `[dot]="false"` |
| Button | busy: the ring **before** the words | the gallery's busy specimens show the ring alone |
| Checkbox, Radio | two box sizes (20, 16) | one size |
| Tag | grey only; hover, selected (teal outline), disabled; two sizes; no ×, no intents | intents and × from the operators' set |
| Date field | placeholder «يوم / شهر / سنة»; three sizes | «اختر تاريخاً»; one size |
| Time field | its list in two columns of times; placeholder «الوقت» | one column or the wheel; «اختر وقتاً» |
| Select | three sizes, an "open" state | one size |
| Textarea | no character count | a count at the end |
| Empty state | four kinds: empty, no results, error, **offline**; the button outlined | no "offline"; the button filled |
| File upload | a dashed zone; uploading shows the progress inside the zone | a solid zone; uploading shows the file's card with a ring |
| KPI card | the trend chip with an arrow icon; «عن الفترة السابقة» | no arrow |
| Progress | four intents (brand, success, warning, danger), two thicknesses | brand and success |
| Skeleton | lines, circles and blocks in three sizes | lines (blocks exist as `scs-skeleton-block`) |
| Spinner | an open arc with no track; brand, grey, white on dark | an arc on a pale track |
| Menu item | no icons; "selected" and "disabled" states | icons; danger and its hover |
| Modal | the footer a grey strip | no strip |
| Breadcrumb | a chevron (›) between items | «/» |
| Tab | a disabled state; two sizes | default and active |
| Notification item | an unread item carries a dot at its start corner; nine kinds of icon | the tint, no dot in the clinic look |
| Slider | the value above the thumb, moving with it | the value at the end above the track |
| Switch | "on" with the knob mirrored in Arabic (at the left); "off" a darker grey | the knob at the right in both languages (spec 011's answer, given for the console) |
| Tooltip | an arrow in every placement | an arrow only in the operators' look |
| Alert | three styles: soft, toast with a bar at its start, plain without a background | soft and toast; the toast's bar only in the operators' look |
| Message bubble | four kinds: mine, incoming, the assistant's with its label, a small note; time and tick | bot and user |
| Insight card | a big number, an impact line in red and a link | the operators' sentence with actions (one component carries both) |

Matching, apart from sizes the clinic draws more of: accordion, card, input, pagination, segment,
sidebar nav item, stepper, usage meter, navbar and bell (the clinic's bar adds the branch picker and
the language toggle, and stays its own by contract). The clinic's table page is 21,750 pixels high —
too tall to read as one picture; its rows were compared when the table was built (T034).

## Icons (US5, T056–T058)

- **The gallery's Icons section (T056)**: every registry entry (74) at 16, 18, 20 and 24, with its short
  name, its name in the design file (or "drawn by hand before spec 095") and whether it points; a
  pointing icon is shown in both directions. Looking at it found two faults, both fixed:
  - **Mirroring by the nearest direction.** `scs-icon` mirrored with `:host-context([dir])`, which
    matches *any* ancestor: on an English page an icon inside a right-to-left area was not mirrored
    (`back` pointed left in both), and on an Arabic page an arrow inside a left-to-right island (an
    address, a code) would have been mirrored wrongly. It now uses `:dir()`, the nearest direction;
    `back`, `chevron-start`, `sign-out`, `menu-send` and `menu-refund` were opened in both directions
    on both pages.
  - **The send icon.** `send` (copied from `Mail/send`) was marked as drawn left to right but points
    to the upper left. The design's Ask-AI field draws its own upward arrowhead (`402:6`, "send-icon");
    it was registered, and the owner then asked for the recommended icon instead (2026-10-16): the
    AI field and the chat now use the console's paper plane `menu-send` (up and to the right in
    English, mirrored in Arabic), and `send` is gone. `tools/icons.mjs add` also takes `--points rtl`
    for a copied icon drawn pointing the other way.
- **Every icon in use is from the registry (T057)**: the icons of this spec were added with the tool
  as each part was built; `node tools/icons.mjs check` — every registered icon is used (74).
- **The quickstart path for one new icon (T058)**: `list wifi`, `add "Computer-Devices/wifi-disabled"
  --as wifi-off`, used in a component, seen in the gallery — **69 seconds** (SC-007: under five
  minutes). Nothing needed that icon, so it was taken out again; the check passes with 74.
- After: unit tests ui 177, ops 39; gallery suites 306 (the eight AI pictures accepted anew after
  looking — the paper plane); `test:tools` 28.
- **Later the same day, at the owner's word**: the send button uses `Mail/send` after all (the plane
  with speed lines), registered with `--points rtl` because the copy draws it pointing to the upper
  left — so it points up and to the right in English and up and to the left in Arabic. And the owner
  saw two × in a search field in Edge: the browser's own clear button beside the toolbar's. The list
  toolbar now hides the browser's (`::-webkit-search-cancel-button`); the drop-down's search box,
  which has no clear button of its own, keeps it. Gallery, operators and the clinic's list: 368 passed.

## The patterns (US6, T059–T063)

Each pattern is a gallery section composed of the shared parts only, with still specimens as its page
draws them and a working example; each was laid beside its page in both languages and opened.

- **Search & Filter (`111:2`, T059)**: the toolbar — search at the reading start, «فلترة» with its count
  and the sort at the other end, on one line; the applied filters as chips with «مسح الكل»; the panel
  open, while its values load, and with nothing to filter yet. The working example filters four
  clinics: the panel offers only the statuses some clinic has (no "trial"), a chip's × takes that
  filter away alone, and a search with no match says so. The sort's name is read but not shown, as
  drawn.
- **Page States (`117:2`, T060)**: empty, error, loading and success as drawn; offline (with the new
  `wifi-off` icon), read only (the page's fields locked under an info banner giving the reason) and
  not in the plan built from the parts and marked **not drawn on the page** (SH-097). The working
  example walks a card through all seven with the segmented control. **Differences**: the page's error
  mark is a bare «!» where the empty state draws a triangle; its loading mark is a purple quarter where
  the shared spinner turns an arc.
- **Confirmation (`120:2`, T061)**: the confirmation and the destructive (delete) one, over the dimmed
  page, their buttons naming the action; the working examples are `scs-confirm` (Escape cancels and
  gives the focus back). **Difference**: the page draws a line above the buttons; the shared dialog
  has none.
- **Multi-step Form (`123:2`, T061)**: the stepper over a card with one step's fields, «رجوع» and
  «التالي»; "next" checks only the step shown, the last step reviews what was typed. The phone number
  is written left to right in Arabic too (it had read "5678 1234 10 20+"). **Difference**: the page
  draws a full-width stepper with green lines and green finished steps; the shared stepper in the
  operators' look (the Navigation page's) draws no lines and finishes in the brand's colour.
- **Onboarding (`253:2`, T062)**: three cards — «تخطي», the round mark, title, line, the dots
  following the reading direction, «التالي» then «ابدأ الآن»; example data only (new `dashboard` icon
  from the copy). **Difference**: the page's marks are filled; the registry's are outlined.
- **Pictures and checks (T063)**: `pictures.spec` keeps the patterns too (72 new pictures, looked at
  on sheets: wide and at 390, both languages); `coverage.spec` checks each pattern and the Icons
  section at 1440 and 390 (they had widened the page on a phone — every block now keeps to the
  window); the phone check takes the patterns' working examples (it found «تخطي» at 21×18 — now 40
  to press — and now measures a field by the box drawn around it, as spec 011 does);
  `e2e/gallery/patterns.spec.ts` works each example in both languages. Gallery suites: 343 passed;
  kept pictures 136 of 136.

## Weight, docs, gates and the walk (T064–T067)

- **Production weight (T064)**: the first `npm run ci` of this pass **failed** — the `ops` production
  build stopped on the gallery's `clinic-reset.css` (10.27 kB, over the 8 kB error budget for a
  component's styles): production swaps the gallery's routes for an empty list, but every component
  of the program was still compiled. **Fixed**: `ops` builds for production from
  `projects/ops/tsconfig.app.prod.json`, which leaves `src/app/gallery/**` out of the program. The
  same run also showed the **clinic's first download at 533.40 kB**, over its 500 kB warning
  (spec 007 had brought it to 445.95 kB): the clinic's sign-in path imported the language toggle,
  the empty state and the skeleton from `ui/list/list.ts`, which this spec grew with the table
  footer's menu, the filter panel and the chips, and pulled them — and `@angular/cdk` — in. **Fixed**:
  those three parts live in their own files (`list/language-toggle.component.ts`,
  `list/empty-state.component.ts`, `list/skeleton.component.ts`, still exported from `list.ts`);
  the clinic's first download is **418.61 kB** (111.10 kB transferred). Four shared parts' styles
  are over 4 kB (select 4.53, calendar 4.33, text field 5.10, file card 4.17 — each now draws every
  state its design does, and each is loaded only by the pages using it): the component-style
  warning budget is **6 kB** in the three apps (the error stays 8 kB). After: no size warning in any
  app; `ops` initial **384.74 kB** raw, 101.44 kB transferred (was 335.15 kB / 91.11 kB at T001;
  under the 500 kB of R15); patient 343.70 kB; `node tools/check-production.mjs` — no file of
  `dist/ops` names the gallery.
- **Docs (T065)**: `docs/setup.md` gains "The design components" (the gallery's address and bar,
  the icon tool with `--points [rtl]` and `check`, kept pictures accepted only after looking, the
  gallery's suites); `docs/adding-a-module.md` gains "Where a screen's parts come from" (`ui` or the
  console's components, a part's look in its look's settings, a missing part added there with its
  gallery entry and pictures, icons through the tool, 40 by 40 on a phone).
- **Gates (T066)**, with nothing else running: `npm run ci` **passed** — lint, `i18n:check`,
  `test:tools` 28, icons 77 used, unit tests 68 / 1 / 39 / 164 / 177 (clinic, patient, ops, core, ui;
  T001: 68 / 1 / 27 / 164 / 107), the three builds, the production check. `npx playwright test` (all
  apps, Chromium): **1094 passed, 4 skipped** (two run only with `SCS_SHOTS`; two are "Edge is not
  installed" guards, which ran). In **Edge**, the console's five files and the gallery's four
  suites: 323 passed and 2 failed by timeout — "a second tab follows a sign-out" and "a password box
  shows one eye"; both passed alone in Edge (3 of 3, 2 of 2); in the console's five files alone, 117
  passed and the second only failed again. That test opens a second Edge of its own (it ran and
  passed in the Chromium run); under the Edge configuration it starts one more Edge while the run's
  are busy, and runs out of time. Recorded, not hidden: Edge 117 + 1 of 118 for the console's files,
  207 of 207 for the gallery.
- **The walk (T067)**: every one of the gallery's 40 sections in Arabic and English, in both looks,
  at 1440 and 390 — 320 visits: none scrolls sideways, every page has its language's direction. The
  console logged one kind of error, a 404 on each page: `version.json`, which only the production
  build writes (the version check misses it on any development server, before this spec too); and
  `missing-picture.png`, the avatar example's picture that fails on purpose. Every live example's
  keys are worked by `keyboard.spec` (16) and `patterns.spec` (10); every specimen was looked at
  through the kept pictures (136 tests) as listed above.

## Each design page beside the gallery (T068, T069)

Every page of the operators' file the gallery draws from — the 33 component pages, the five
patterns and Icons — was pictured through the design connector (38 pages and the Navbar's six
frames one by one, 16 Oct 2026), and every gallery section was pictured at 1440 in Arabic and
English in the operators' look, and in the clinic look where it shows shared parts (160 pictures,
the gallery's bar held still). Each page's sheet (`node_modules/.tmp/t068/sheets/`, 40: the 39
pages and "Other parts", which no page draws) lays the page above its sections; the tall ones were
cut into bands and **every sheet and band was opened**. What they showed:

**Fixed here** (each looked at again; the kept pictures of the changed sections were laid expected
beside actual — 164, on 12 sheets plus the File Upload rerun — opened, and accepted section by
section; one surprise, a pixel's shift of Navigation's stepper and folded pagination at 390 under
the new part above them, was looked at and is the same drawing):

- **Slider, clinic look** — disabled was drawn dark (the fallback was the tertiary text colour);
  the clinic file (`4260:809`) draws the fill `#e3e8ec`, the knob ring `#cbd3d9`, the value
  `#9facb5`. The disabled value is light grey in both looks now.
- **Dropdown** — "Searchable / Results" (`2224:70`) showed an empty search; it now holds the typed
  «النور» · "noor" and New Al Noor chosen, as drawn (`scs-select` gains `searchText`, what the search
  box holds at first). The Arabic error repeated the placeholder; it is «يرجى اختيار فرع» (the
  Arabic frame writes the English error — a slip of the file).
- **Navigation** — the Pagination Menu (`2104:140`/`2104:160`), inventoried as a part of its own,
  had no still specimen: `scs-table-footer` gains `menuShown` (the pages' list drawn open in place
  above "…") and the section a "Pagination Menu" part.
- **Notifications** — the bell's "Unread" specimen drew the dot; the page draws a count (3).
- **Media** — the grid's last tile says "+12" over a dark veil, as drawn (`app-gallery-tile` gains
  `more`/`moreLabel`, said to a screen reader); the example poster's own picture mark showed as a
  box behind the play button — the poster is a plain dark frame now.
- **Table** — the composed table chooses its second row, as drawn.
- **Usage Meter** — the working example's control was the browser's own slider; it is `scs-slider`.
- **Textarea, File Upload** — the specimens were narrower than the page draws them, so the filled
  Medium text ran to the box's edge and a failed upload's reason took three lines: the cells are
  300 and 340 (the upload part stops at 320, the clinic's width, which the clinic's screens use).
- **Labels** — parts were named by their layer names (`table-example`, `avatar-group`,
  `menu-panel`, `bar-chart-ar`, `ask-ai-fab`, …): each is named as the page writes it ("Table —
  Composed Example", "Avatar Group", "Composed Example — Ask AI · button", …).

**Kept, with the reason** (the design's slip or a rule above it — no owner decision needed):

- Western digits where the Arabic frames write Eastern ones (KPI Card, Stat, Date Picker's filled
  value) — OP-B-386 / FR-011.
- The open accordion's chevron points up; the frame draws both states pointing down.
- Avatar initials from the Arabic name in Arabic («عا»), where the Arabic card keeps "AN".
- The Navbar's Arabic "unread" frame puts the count on the brand tile, and the uploaded-logo frames
  leave the console's name under the logo — the count is on the bell; the logo stands alone.
- The Menu's composed example lists Edit/Duplicate/Archive, each with a pencil: the page's own
  Rules give every item the icon that says what it does from Menu Icons (`2200:86`), which has no
  "archive" — the example uses View Details/Edit/Copy/Delete.
- The video's play triangle is filled; the icon set has only the outlined `button-play`.
- The spinner's coloured part runs to the centre in the frame (a wedge); built as an arc of the
  ring (US1). The textarea keeps the browser's resize grip. A placeholder stays grey in a focused
  or refused drop-down, where the frames draw it dark (it is not a value).
- Recorded before, seen again: the toast's close and the circle of the danger mark (Alert), the
  console's own empty and error marks (Empty State, Page States), the stepper's lines (Multi-step
  Form), outlined marks (Onboarding), the clinic look's differences of T055.

**For the owner (T070):** the Modal page also draws "Confirmation Dialogs" (`63:3`, `63:15`: a 56
icon disc, centred title and words, two equal buttons) and "Bottom Sheet / Drawer" (`63:27` a phone
sheet with a grab handle, `63:33` a side drawer) — English frames only, outside the spec's table
(Modal: Size) and the inventory, and no shared part draws a sheet or a drawer.

**The one picture (T069)**: `specs/095-design-components/design-vs-real.png` (2,701 kB) — each of
the 39 pages beside its section in English and Arabic, two pages to a row; opened in five bands.

## The owner's answers (T070, 16 Oct 2026)

- **The week's first day in the operators console: Saturday, like the clinic's** (the owner's
  choice over the frame's Sunday, `75:4`). The console no longer provides
  `SCS_CALENDAR_WEEK_START`, so both consoles take the default, Saturday; the gallery's date-picker
  section uses Saturday in both looks. The 16 calendar pictures that changed were laid expected
  beside actual and opened — Sa first on the left in English, «س» first on the right in Arabic,
  days and ranges unmoved — and accepted. After: unit tests ops 39, ui 177; the gallery's suites and
  the console's files 461 passed. **For the designer:** `75:4` and the date-range and date-and-time
  panels draw Sunday first.
- **The two colour collections: "Ops Console Colors" is the source** (what every component page
  draws, primary `#7c3aed`). "Semantic" (primary `#6d2fd8`) is stale — for the designer to tidy
  or remove. Nothing changes in the code.
- **The clinic file's differences (Badge, Avatar, Select, and the T055 table): later, with the
  clinic's screens.** The shared parts stay as built; the differences go to the designer now; the
  clinic look takes them when its screens are rebuilt from the clinic file.
- **The Modal page's confirmation dialogs, bottom sheet and side drawer (T068): built with the
  first screen that needs them.** The Confirmation pattern covers the confirming dialog until then.

## The BRD row (T072)

Left to the owner (16 Oct 2026): `docs/reference/brd/SCS-SpecKit-Phases.md` is the owner's to edit,
and no earlier row (001–011, 094) carries a "built" mark. Its row 095 and the phase 1 count (19)
are as the owner added them; nothing was changed.

## On the real local stack (T073, 16 Oct 2026)

The real API (preview "api", port 5080), database and mailbox (`heta-scs-sqlserver-1`,
`heta-scs-mailpit-1`) and the console on 4203. The walk is spec 011's `walk11` moved to spec 095's
screens (`node_modules/.tmp/walk95.mjs`): its own accounts — a walk super admin invited through the
Migrator, a second and a third invited from the console — with passwords made by the script and
never printed; the seeded operator was not touched; a separate Playwright browser, so the owner's
own browser stayed signed in as it was. The first run stopped at the filter (the walk pressed
Escape, which closes the whole panel); the second ran to the end, 14:51–14:59 local time.

What it showed, by spec 011's "By hand" steps:

1. The Migrator's invitation «دعوة إلى لوحة التشغيل» names no inviter; the side bar «الرئيسية»,
   «المشغلون»; the top bar «مسؤول عام».
2. Cards, rows, «عرض 1–8 من 16 مشغلًا»; search by part of an email in capitals (1 row) and of a
   name; the filter to "active" (13); **spec 095's count "1" on Filter and the chip «الحالة: نشط»**,
   whose × took the filter away (no chip, no count, 16 rows). No Arabic-Indic digit.
3. Inviting a viewer: the new operator's page, «تم إرسال الدعوة.», no link on the screen or in the
   address; the email names the inviter and the role, 72 hours; the viewer has no add button and a
   row menu of «عرض التفاصيل» only.
4. With the switch off, «تم إنشاء الحساب.» and no email in 70 s; the link sent from the page, in
   the account's language; again within the minute: the "less than a minute" sentence, one email.
5. As a custom role: «مسؤول عام», «مسؤول الفوترة» and «مشاهد فقط» refused, each with its reason
   (the picture `05-role-picker`); own role locked «لا يمكنك تغيير دورك.».
6. Changing the second account's role: its untouched browser was signed out in 8 s with
   «تم تغيير دورك، فتم تسجيل خروجك…».
7. Adding "manage roles" to that role: still signed in; the add-role button on the next page.
8. Suspend: out in 20 s with «تم قفل حسابك لأسباب أمنية…»; the right password «هذا الحساب
   موقوف.»; reactivated.
9. No suspend on one's own page; own role locked.
10. Five wrong passwords: the account's page shows the lock and «فك قفل الحساب»; after it, in.
11. A role made — ticking «عرض كل التذاكر» draws «عرض التذاكر المُسندة إليّ» ticked and grey with
    «مطلوبة لـعرض كل التذاكر» (`11-role-add`); copying «مشاهد فقط» as one who holds less:
    «تُركت 8 صلاحيات لا تحملها.»; deletion refused while held (count named, button disabled), then
    deleted once free — the database marks it deleted.
12. One menu among four rows (system roles have none).
13. The admin's page: 28 permissions, one session, nine lines of activity as sentences.
14. The viewer's `POST /v1/ops/operators`: 403 `PERMISSION_REQUIRED`; `/operators/new` by its
    address: «لا يمكنك فتح هذه الصفحة».
15. Spec 010's screens: the code step's countdown «01:00»; the profile's sessions; no Arabic-Indic
    digit; **spec 095's two-factor switch on and locked with «مطلوب لكل المشغلين من الإعدادات ←
    الأمان، لذا لا يمكن إيقافه من هنا.», and the session's badge «نشط الآن»**. Forgotten password:
    the "check your email" step, the reset email, the new password, «تم تغيير كلمة المرور…», and
    the new password signs in; the first browser was signed out by the reset.
16. The API's log of the walk: no email, name, link or token (searched for the walk's address
    domain, its Arabic names and `token=`); no error.

Three of the script's readings were wrong, not the screens: the lists of the filter and of the role
picker were read before they drew (both were then used, and `05-role-picker` shows the three
refusals), the permissions under the role field were counted by spec 011's id (they show as chips,
`03-add`), and "still listed: 1" after deleting the role was read before the list reloaded (the
database has it deleted). The English pass signed the walk admin in on a browser set to English;
the console then follows the account's language, Arabic, as spec 010 decides — the English screens
are held by the browser tests.

**A defect found and fixed:** the log held one front-end failure from the first run (14:45:41):
`NG0203` in `signedOutOnly` — the guard of the screens before sign-in asked for the router after
waiting, outside the injection context, so a signed-in operator opening `/sign-in` or
`/forgot-password` got a failure instead of home (spec 010's code; the walk's `signIn` reached it).
Test first: `ops-console.spec` "a signed-in operator who opens /sign-in · /forgot-password is taken
home, with no failure" (2) failed, then passed once the router is taken before waiting. No other
function injects after an await.

The walk's accounts stay as spec 011 left its own: the walk admins active, the second suspended,
the third invited.

## Summary (T071)

**What was run, against T001** (the last runs, 16 Oct 2026, after every fix above; brought up to date
with the convergence's T074–T076 by T077):

| | T001 | Now |
|---|---|---|
| `npm run ci` | passed | passed — lint, `i18n:check`, tools' tests 28, the icon check (77 registered, all used), the builds, the production check |
| Unit tests (clinic / patient / ops / core / ui) | 68 / 1 / 27 / 164 / 107 | 68 / 1 / 40 / 164 / 179 (T076 added one to ops, two to ui) |
| Browser tests, all apps, Chromium | 755 | 1096 passed, 4 skipped (two only with `SCS_SHOTS`, two "Edge is not installed" guards) |
| The console's five files in Edge | 118 | 120 of 120 with the gallery's coverage, keyboard, settings and patterns suites 207 of 207 — 327 passed (T075, after T068's fixes, T070's week and T073's guard fix; the kept pictures are held in Chromium only) |
| `ops` first download | 335.15 kB (91.11) | 359.47 kB (95.70) — 384.75 at T064; the calendar left it when the console stopped providing its week (T070) |
| clinic / patient first download | — | 418.61 kB / 343.70 kB, no size warning |

**The gallery**: 40 sections (33 component pages, five patterns, Icons, "Other parts"), each walked in
both languages, both looks, at 1440 and 390 — 320 visits, none scrolls sideways (T067). Its suites:
coverage, keyboard, settings, patterns and the kept pictures — 343 passed.

**Pictures**: 2,904 kept pictures in `e2e/gallery/__pictures__/` (136 tests). Every change to them
was laid expected beside actual and opened before it was accepted (T042: 168; T068: 164 and the
File Upload rerun; T070: 16). The 40 sheets of each design page beside its sections, and their bands,
were opened (T068); `design-vs-real.png` was opened in five bands (T069).

**The clinic's comparison (T054)**: 773 "before" pictures in three runs; after the fix of the
anchoring that changed the clinic's text rendering, 0 of 189 differ at 390, and the 7 left were each
opened and are moments of capture or the operators' sign-in, changed on purpose. Pictured again
after every later change to shared parts (T074): 8 of 773 flagged, each opened — the same moments of
capture and the same sign-in screen; no clinic or patient screen changed.

**The operators' screens' changes (US3)**: the frame on `scs-navbar` and `scs-nav-item`; «تذكرني»
`scs-checkbox`; the profile's switch, badge and red buttons; badges and avatars on operators and
roles; the filter's count and chips; then, on the real stack (T073), every step of spec 011's walk
and spec 010's screens, and one defect of spec 010 fixed (`signedOutOnly`).

**Differences and whose decision**: listed where found — US1 and US2 per part; T055 for the clinic
file (later, with the clinic's screens — owner); T068's "kept, with the reason"; T070's answers
(week from Saturday, "Ops Console Colors", clinic differences later, drawer and sheet when needed —
owner); T072 (the BRD row — owner's).

**Built differently from the plan**: every test leaves its last screen with `SCS_SHOTS` instead of
a picture call per suite (T002); the button's busy ring and the banner are anchored only in the
operators' look (`--scs-button-busy-anchor`, `--scs-banner-anchor`, `--scs-banner-clip`) so the
clinic's text renders as before (T054); the operators' production build leaves the gallery out of
its program (`tsconfig.app.prod.json`), the list's first-screen parts live in their own files, and
the component-style warning is 6 kB (T064); the console's side bar folds below 1024 (T048);
`scs-select` gained `searchText` and `scs-table-footer` `menuShown` for still specimens (T068).

**Not checked**: Firefox and Safari (no browser of theirs runs here); a screen reader itself (names,
roles, `aria-*` and focus are checked by the tests, not heard); the kept pictures in Edge (they
are Chromium's); real phones (390 is a browser's width, with touch emulated only where the tests ask).

## Convergence (T074–T076)

- **The clinic and patients again (T074)**: every clinic and patient suite run with pictures into
  `node_modules/.tmp/after095d` (395 passed; 189 states at 1440, 189 at 390, 395 last screens) and
  held against `before095`, `before095b` and `before095c` with `tools/compare-pictures.mjs`: of 773
  pictures, **8 flagged** — all last screens or one state at 390, the same kinds T054 found: the
  operators' sign-in screen a clinic test ends on (changed on purpose, T049), the bell's panel open in
  one run and closed in the other (two), the branch and account menus a few pixels apart, the error's
  random reference, two password screens 0.1% apart in a field being typed into. All 20 pairs the pair
  tool laid out (the 8 and the 12 under the limit it also lays) were opened: the same drawings. No
  clinic or patient screen changed (SC-004, FR-029).
- **Unit tests for T068's options (T076)**: `scs-select` starts with `searchText` in its search box and
  shows only what it finds (`form-controls.spec`); `scs-table-footer menuShown` draws every page not
  shown, still, with nothing to press and no working menu (`navigation.spec`); `app-gallery-tile` says
  "+12" and names it to a screen reader (`media.spec`). Unit tests ui 179 (was 177), ops 40 (was 39).
- **Edge again (T075)**, after T068's fixes, T070's week and T073's guard fix: the console's five
  files and the gallery's coverage, keyboard, settings and patterns suites in Microsoft Edge —
  **327 passed**: the console's 120 (118 and the two new "taken home" tests) and the gallery's 207;
  "a password box shows one eye", which timed out under T066's Edge run, passed. The kept pictures
  are Chromium's and are not held in Edge (as at T066): the run was given them too, and their 136
  tests failed only for want of Edge pictures, writing a folder of Edge "actuals"
  (`e2e/gallery/pictures.spec.ts-snapshots/`), which was removed — none was accepted.
