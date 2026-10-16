# Research: Design Components (spec 095)

Read on 16 Oct 2026: the operators' design file `SetO9EhDivNcmurAMfcflY` (33 component pages,
Foundations, Icons, five patterns, its variables, text and effect styles), the clinic design
file `4HLWEqlna9sbn68276LHYt` (component pages `4228:2`–`4228:6`, Icons `44:2`, its variables),
and the code of `src/web` as it stands after spec 011.

## R1 — Where each component lives

**Decision**: A component the clinic file also draws (by name and purpose) goes in the shared
`ui` library, one folder per design page (`ui/src/lib/button/`, `ui/src/lib/avatar/`, …). One
only the operators' file draws goes in the console, `ops/src/app/components/<page>/`. Parts the
`ui` library already has stay where they are and are brought to the design in place
(`controls/`, `field/`, `list/`, `dialog/`, `overlay/`, `files/`, `code-input/`, …) — see the
mapping in `contracts/components.md`.

**Rationale**: the constitution puts code used by more than one app in a shared library and
forbids copies; the owner asked for what is common to be shared and the particular to stay
apart. Moving the existing files would touch every import of three apps for no change of
behaviour.

**Alternatives considered**: everything in `ui` (puts the console's own parts — Ask-AI, video
player, sparkline — where the clinic sees them as its own); a new `ops-ui` library (a third
place for the console's parts, used by one app only).

**First reading of "shared"** (the plan confirms each by laying the two drawings side by side,
FR-004): shared — Avatar, Badge, Button, KPI Card, Sidebar Nav Item, Checkbox, Radio, Switch,
Slider, Input, Textarea, File Upload, Select and its list, Spinner, Progress, Skeleton, Alert
(banner and toast), Modal, Tab, Breadcrumb, Pagination, Stepper, Card, Tag, Accordion, Date field
and calendar (one day and a range), Time field and its list, Table row and table, Navbar,
Notification bell, item and panel, Menu item and panel, Empty State, Tooltip, Segmented control,
Usage meter, bar chart, AI insight card, message bubble. The console's own — Divider, Stat, Date
& Time field and panel, Date Range trigger, Image, avatar group, video player, carousel, gallery
tile, Sparkline, AI badge, Ask-AI button and field, chat widget, the AI suggestion card's states,
the 16-pixel menu icons.

## R2 — The operators' look, from the design file

**Decision**: `ops/src/styles/tokens.css` is rewritten from the design file. The component pages
fill with plain colours, not bound variables; nearly all match the file's collection "Ops
Console Colors", and the rest are named here and added:

| Design value | Role | Token |
|---|---|---|
| `#1a2024` | text, dark | `--scs-color-text-primary` |
| `#57646d` | text, secondary | `--scs-color-text-secondary` |
| `#74838e` | text, muted; icons | `--scs-color-text-tertiary`, `--scs-color-icon-default` |
| `#9ca7b0` | a field's placeholder | `--scs-color-text-placeholder` (new) |
| `#adb4ba` | disabled text and marks | `--scs-color-text-disabled` (new) |
| `#ffffff` | surface | `--scs-color-bg-surface` |
| `#f9fafb` | page; a row's hover | `--scs-color-bg-canvas`, `--scs-color-bg-hover` (new) |
| `#f1f4f6` | hairline border; muted fill | `--scs-color-border-subtle`, `--scs-color-bg-muted` |
| `#d9dce0` | a field's border | `--scs-color-border-control` (new) |
| `#7c3aed` | primary | `--scs-color-brand-default` |
| `#6a2ed8` | primary, hover | `--scs-color-brand-hover` (new) |
| `#5b21c7` | primary, pressed | `--scs-color-brand-active` |
| `#f0ecfe` | primary, background | `--scs-color-brand-subtle` |
| `#dc2626` | danger | `--scs-color-feedback-danger-icon` |
| `#c31c1c`, `#aa1313` | danger hover, pressed | `--scs-color-danger-hover`, `--scs-color-danger-active` (new) |
| `#991b1b`, `#fbe9e9` | danger text, background | `…-danger-text`, `…-danger-bg` |
| `#c98b05`, `#b45309`, `#fef1e1` | warning mark, text, background | `…-warning-icon`, `…-warning-text`, `…-warning-bg` |
| `#1e8e5a`, `#116932`, `#e5f2ec` | success mark, text, background | `…-success-icon`, `…-success-text`, `…-success-bg` |
| `#1d66b3`, `#e6f2f6` | info text, background | `…-info-text`, `…-info-bg` |

Every value the component pages use is checked against this table while building; a value not
in it is added with its role or listed as a difference. Type: Cairo (the file's text styles —
Display 48, H1 36 … Caption 12, Label Medium 13, Button 13/14/15 SemiBold), the variable font the
clinic already loads. Spacing, radii (4, 8, 12, 16, full), borders (1, 1.5, 2) and shadows
(Elevation XS … Overlay) from the file's collections and effect styles.

**Rationale**: the token file still says "NOT YET FROM THE DESIGN FILE … neutral placeholders"
(spec 001, task T098); the file now has the values. The console's screens move with this (owner,
16 Oct 2026) — the visible changes are the typeface (system font → Cairo), a few greys and the
shadows.

**Alternatives considered**: binding to the "Semantic" collection (its values differ from what
the component pages actually draw — `purple/600 #6d2fd8` for primary against the drawn
`#7c3aed`); keeping the system font (the file draws Cairo everywhere).

## R3 — Two looks on one page: the look switch

**Decision**: each app's tokens are declared on `[data-scs-look='<app>']` instead of `:root`, and
each app's `index.html` carries `<html data-scs-look="clinic|patient|ops">`. The gallery's
"clinic" look wraps its content in `data-scs-look="clinic"` and loads the clinic's own token file
through an unencapsulated stylesheet of a gallery-only component.

**Rationale**: custom properties set on an element reach everything inside it, so the same
shared component draws in the clinic's look inside that wrapper — from the clinic's own file,
not a copy. The attribute is in the static page, so the look is there at first paint; for the
clinic and patients nothing else changes, which their before-and-after pictures prove (FR-023).

**Alternatives considered**: a copy of the clinic's values in the console (two sources that
drift); scoping by class at run time (a flash of the wrong look); building the gallery in the
clinic console too (the owner chose one gallery).

## R4 — A gallery that exists only in development

**Decision**: the gallery's routes are in `ops/src/app/gallery/gallery.routes.ts`; the
`production` build replaces that file with `gallery.routes.prod.ts`, which exports no route
(`fileReplacements`). The routes are spread into `app.routes.ts` before the frame, at
`/dev/components`, with no guard. A check after `build:all` (`tools/check-production.mjs`) fails
if any file of the production output names the gallery.

**Rationale**: a replaced file never reaches the bundler, so nothing of the gallery — its
components, its examples, the clinic's tokens — is delivered (FR-015, SC-006). `isDevMode()` would
leave the lazy chunk's import behind a run-time branch.

**Alternatives considered**: a guard that refuses in production (the code would still ship); a
separate app for the gallery (the owner chose a page inside the console).

## R5 — Hover, pressed and focus as still specimens

**Decision**: each interactive component styles its pointer and keyboard states both by the real
pseudo-class and by a host attribute: `:host(:hover), :host([data-scs-state~='hover'])`, and the
same for `focus` and `pressed`. Only the gallery sets the attribute, through its directive
`scsSpecimen`, which also makes the specimen `inert` so it cannot take focus or clicks. The live
example beside it is a normal, working component.

**Rationale**: the owner wants every drawn state visible at once; a pointer can hover one thing
at a time. An attribute keeps the state out of the component's inputs, so screens cannot misuse
it.

**Alternatives considered**: an input such as `forceState` (part of every component's API, open
to misuse on screens); pictures of the live component taken while hovering (cannot show all
states together on the page).

## R6 — Icons: one copy of the library, a tool, a registry

**Decision**:

- **The copy**: all 1,144 icons of the operators' Icons page (`738:7380`; the clinic's page `44:2`
  holds the same library, plus its 125-icon grid) are exported once through the design
  connector as SVG text, with their names and groups, into
  `src/web/tools/icons/library/` (part files of 45 icons, written as the connector answers 20 KB at
  a time). The 16-pixel menu icons of the Menu page (`2200:86`) are
  added to the same copy under the group `Menu`. The copy is never imported by an app.
- **The tool**: `node tools/icons.mjs add <design name> --as <short name> [--points]` copies one
  icon from the copy into the registry; `list <words>` searches the copy; `check` fails when
  a registered icon is used by no component or screen. `check` runs in `npm run ci`.
- **The registry**: `ui/src/lib/icon/icon-registry.ts` maps short names to their drawing
  (`viewBox`, the paths with fill or stroke, and `points`). The 53 icons drawn by hand so far (they
  follow the clinic file's Icon Grid and were compared with the frames of specs 003–011) are its
  first entries, unchanged. `ScsIconName` is the registry's keys, so an icon a component names but
  the registry lacks fails the build (FR-024).
- **Mirroring**: an entry with `points: true` is mirrored right to left; the per-name rules in the
  icon's styles go.

**Rationale**: answers clarify Q2 (offline, independent of design-file access). A typed registry
makes "missing" a compile error and "unused" a check, and only registered icons reach the
bundle.

**Alternatives considered**: the whole library in the bundle (about a megabyte of drawings, most
never shown); an icon font (no per-icon mirroring, blurry at 16 pixels); reading the design file
from the tool (fails when access drops).

## R7 — Pictures: kept per specimen, compared by eye

**Decision**:

- **Kept pictures** (FR-027b): a Playwright suite `e2e/gallery/*.spec.ts` opens the gallery and
  takes one picture per specimen element with `toHaveScreenshot`, named
  `<page>/<component>/<variant>--<language>--<width>[--clinic].png`, kept under
  `e2e/gallery/__pictures__/`. A change that alters a specimen fails with that name. After looking,
  the picture is accepted anew with `--update-snapshots`. Pictures are taken on Windows in
  Chromium with Cairo loaded from the bundle; the baselines are those of that machine.
- **Certain faults** (FR-027a): the same suite runs the console's existing look checks on every
  section (`opsLooksWrong`: nothing cut off or overlapping, no sideways scroll, no word in the other
  language, no non-Western digit).
- **By eye** (FR-027, SC-002): for each design page, a sheet lays each frame (exported through the
  connector) beside its specimens in both languages and both looks; every sheet is opened and its
  findings written to `validation.md`.

**Rationale**: answers clarify Q4. Fonts and smoothing differ between the design tool and a
browser, so a pixel limit against the frames would be either loose or noisy; against the
browser's own accepted picture it is exact.

**Alternatives considered**: a pixel limit against the frames (rejected by the owner); one picture
per section (cannot name the specimen that changed).

## R8 — The parts that exist become the components

**Decision**: the parts of `ui` named in FR-006 are brought to their component pages in place,
keeping their selectors and inputs where the design adds nothing, and gaining the design's
options where it does (the button's six types, three sizes and busy state; the field's sizes and
warning, success and busy states; the tabs as the design's Tab Item; …). Mapping in
`contracts/components.md`. A part that conflicts with its page is changed to the page and the
operators' screens that use it follow.

**Rationale**: FR-006 forbids a second set; the clinic keeps its look because these parts draw
from the clinic's tokens there, and only options the clinic does not use change shape.

## R9 — Holding the clinic's look

**Decision**: before any change, every picture of the clinic and patient browser suites is taken
(`SCS_SHOTS`, Arabic and English, 1440 and 390); after the work, again; a script compares each
pair (same size; share of differing pixels). Any pair that differs beyond smoothing (more than
0.05% of pixels, or any change of size) is opened and either fixed or explained. The clinic's
token file gains only the new token names (with the clinic file's values) that new options use.

**Rationale**: SC-004. The same method found the Chromium–Edge differences of spec 011 to be
smoothing only.

## R10 — Keyboard and screen readers

**Decision**: each interactive component follows the WAI-ARIA Authoring Practices pattern of its
kind — button; checkbox (with mixed); radio group (roving focus); switch; slider (single and two
thumbs, Page Up/Down, Home/End); combobox with listbox (single, multiple, searchable); dialog
(modal, focus trapped and returned); disclosure (accordion); tabs (automatic activation);
menu button and menu; grid of days for the calendar (arrows by day and week, Page Up/Down by
month, Home/End to the week's edges); listbox for the time list; tooltip (on focus and hover,
Escape hides); alert and status for banners and toasts; navigation landmark for breadcrumb and
pagination (`aria-current`). The contracts list each one's keys. Each is unit-tested.

## R11 — Dates and times without a library

**Decision**: the calendar, time list and their fields are built on `Intl.DateTimeFormat` and
plain date arithmetic in `ui/src/lib/date/` (pure functions, unit-tested): month grids, weeks
starting as the calendar panel draws them (Sunday in the English frame; the Arabic frame is read
while building), "today" in Cairo time (SH-001), names of months and days in the page's language,
and digits by the console's rule (Western in the operators console, OP-B-386; the clinic's own
rule there). A range is a start and an end of whole days.

**Rationale**: the constitution adds no third-party UI library; the needs are small and drawn.

## R12 — Charts, media and AI parts, drawn only

**Decision**: the sparkline and the bar chart are SVG drawn by their components from a list of
numbers; the video player is the browser's `<video>` with the drawn controls; the carousel and
gallery tiles are the console's own; the AI parts are visual with example data and call nothing
(Constitution V). Example images and the example video in the gallery are small files generated
for it under `ops/src/app/gallery/examples/`, delivered with the gallery only (never in
production, R4).

## R13 — Template differences as named options

**Decision**: where a template draws a component differently from its page and the owner chose
the template (today: the operators' forms' grey regular labels and dark empty drop-down,
14 Oct 2026), the difference is a named option expressed as a class the app applies to a
container (`.scs-form--template`) that sets the component's documented custom properties
(`--scs-field-label-weight`, `--scs-field-label-color`, `--scs-select-empty-color`). The
component page's look is the default everywhere else, including the filter panel. Each
difference found while building is listed in `validation.md` with its decision.

**Rationale**: answers the owner's precedence choice (component page wins) without losing the
choice already made for the forms.

## R14 — Browsers and sizes

**Decision**: Chromium and Microsoft Edge (as spec 011); 1440 and 390; Arabic and English; 200%
zoom checked for the opened parts (dialogs, menus, panels) at 1440. Firefox and Safari are not
checked by this spec.

## R15 — Weight

**Decision**: nothing of the gallery reaches production (R4); only registered icons reach it
(R6); components are imported by the pages that use them, so each lazy page carries only its
own. The `ops` initial bundle stays under the 500 kB warning of `angular.json`; the Cairo font is
loaded as the clinic loads it. No new package is added.

## R16 — Order of the work

**Decision**: (1) clinic pictures taken before anything changes (R9); (2) tokens and the look
switch (R2, R3); (3) the icon copy, tool and registry (R6); (4) the gallery frame and its
catalogue with the design inventory (R4, `data-model.md`); (5) the components page by page, each
with its unit tests, specimens and live examples, shared ones in both looks; (6) the patterns;
(7) the operators' screens moved onto the components; (8) kept pictures, sheets opened, clinic
pictures compared, `validation.md`.

## Differences found while reading (for the designer and the owner)

- The two files draw the Badge differently: five intents (ops) against six intents in two
  styles and two sizes (clinic).
- The Avatar: Small/Medium/Large (ops) against 24/32/40/56 with initials or an icon (clinic).
- The clinic's Select has an "Open" state and three sizes; the operators' Select Trigger has
  neither (its open list is drawn beside it).
- The operators' file has two colour collections whose values differ ("Ops Console Colors" is
  what the components draw; "Semantic" is not used by them).
- The calendar's English frame starts the week on Sunday; Egypt's week usually starts on
  Saturday — the frame is followed and the point raised.
- The Dropdown page draws the empty drop-down grey; the Operators template draws it dark (the
  forms keep it dark by the owner's choice).
