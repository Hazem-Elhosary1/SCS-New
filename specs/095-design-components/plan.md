# Implementation Plan: Design Components

**Branch**: `095-design-components` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/095-design-components/spec.md`

## Summary

Every component the operators' design file draws is built in every drawn option and state, in
Arabic and English: 44 component sets with 328 variants per language, the open parts beside them
(lists, calendars, time lists, menus, the notification panel), and five patterns. What the clinic
file also draws goes in the shared `ui` library and is checked in both looks; the rest stays in the
operators console. The console's tokens are taken from the design file for the first time (Cairo,
the drawn greys and purples, the elevations). A gallery at `/dev/components` shows everything,
in development only, with switches for language and look; a production build does not contain it.
The operators' screens of specs 010 and 011 move onto the components; the clinic's screens are held
unchanged by before-and-after pictures. Icons come from a copy of the design file's 1,144 kept with
the source, through a small tool, into a typed registry.

## Technical Context

**Language/Version**: TypeScript on Angular 22 (zoneless, signals), as pinned by spec 001. No
server change.

**Primary Dependencies**: none new. Angular CDK (overlay, a11y), Transloco, Tailwind 4, the
`@fontsource-variable/cairo` font the clinic already loads, Playwright and Vitest.

**Storage**: none. Source files only: the design inventory, the gallery's catalogue, the icon copy
and registry, kept pictures.

**Testing**: Vitest unit tests per component (options, keys, names, refusals) and for the date
functions; Playwright: the gallery suite (inventory coverage, `opsLooksWrong` per section, a kept
picture per specimen in both languages, both widths, both looks), the console's existing suites
after the move, Edge for the console; the tools' tests (`node --test`) for the icon tool and the
production check; before-and-after pictures of the clinic and patients.

**Target Platform**: the three front ends of `src/web`; the gallery in `ops` under `ng serve`.

**Project Type**: web front end — the shared component library, one app's components and a
development page, two small tools.

**Performance Goals**: `ops` initial bundle under its 500 kB warning; nothing of the gallery and no
unused icon in production (SC-006).

**Constraints**: no third-party UI library (constitution); clinic and patient pictures unchanged
(SC-004), shared geometry per look so no clinic part changes shape (conventions 2); the component page wins over a template, the owner's named options aside (research R13);
tokens only inside shared components; Western digits in the operators console (OP-B-386).

**Scale/Scope**: 32 functional requirements, 6 user stories; about 50 components (33 design
pages): 22 new to `ui`, 11 groups of the console's own, the rest existing parts brought to the
design; 5 patterns; 328 variants per language → about 1,310 specimens in the operators' look (two
languages, two widths) and about 1,000 more for the shared ones in the clinic's look, so about
2,300 kept pictures of a few kilobytes each (some 30–60 MB in the repository); 1 development page;
3 tools; the 10 screens and 9 dialogs of specs 010 and 011 moved.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How |
|---|---|---|
| I. The BRD and the design file are the source of truth | Pass, differences recorded | The spec cites SH-002, SH-097, SH-098, SH-101, CL-SH-046…062 and OP-B-386 for the rules the components carry, and names every page and node built from (`contracts/components.md`). No behaviour is added to the product. Differences between the two files and between pages and templates are listed in `research.md` and go to `validation.md` with decisions; the week's first day in the calendar is raised with the owner |
| II. Tenant isolation in the database (non-negotiable) | Not applicable | No server, table or query |
| III. The server decides | Not applicable, held | No permission or plan is touched; components that show a refused action do so only as screens tell them (a disabled control with its reason), never deciding access. The gallery calls nothing |
| IV. Financial records | Not applicable | No money |
| V. Patient data, secrets, AI | Pass | The AI parts are visual with invented example data and call no model; the gallery's examples contain no real person, clinic or address; nothing of the gallery is delivered |
| VI. Bilingual and complete | Pass | Every component in Arabic and English, right to left as its twin, Western digits in the console; every state drawn, and the states SH-097 requires for screens in the Page States pattern; words of components from `ui/i18n` in both languages |
| VII. Tested and verified | Pass | Unit tests per component, the gallery suite with kept pictures, Edge, the console's suites after the move, the clinic's before-and-after pictures; every sheet opened, `design-vs-real.png` and `validation.md` close the work |

**Result**: gate passed.

**Post-design re-check** (after Phase 1): unchanged. Stated plainly: the operators' screens change
look (typeface, greys, shadows — the owner's choice "ops moves"); every app's `index.html` and
token selector change to carry the look attribute, with no visible change for the clinic or the
patients, proven by pictures; `scs-status-mark` becomes `scs-badge` with the old name kept for the
clinic until it moves.

## Project Structure

### Documentation (this feature)

```text
specs/095-design-components/
├── plan.md
├── research.md                 # R1–R16 and the differences read
├── data-model.md               # inventory, catalogue, specimen, looks, icons, kept pictures
├── quickstart.md
├── contracts/
│   ├── components.md           # each component: nodes, place, what it becomes, options, keys
│   ├── conventions.md          # what every component follows
│   ├── gallery.md              # the development page
│   └── icons.md                # the copy, the tool, the registry
├── checklists/requirements.md
└── tasks.md                    # /speckit-tasks
```

### Source Code (repository root)

```text
src/web/
├── angular.json                         # ops production: fileReplacements for the gallery's routes
├── tools/
│   ├── icons/library/part-*.json        # NEW: the copy of the Icons page (+ menu icons)
│   ├── icons.mjs, icons.spec.mjs        # NEW: list, add, check
│   └── check-production.mjs (+ .spec)   # NEW: no gallery in dist/ops
├── package.json                         # ci: + icons check, + production check
├── projects/ui/
│   ├── i18n/{ar,en}.json                # + ui.* words the components draw
│   └── src/lib/
│       ├── controls/                    # button, checkbox, switch, textarea, select brought to the design;
│       │                                #   NEW radio, slider, segmented
│       ├── field/                       # text field: sizes, warning, success, busy
│       ├── files/                       # the six drawn states
│       ├── date/                        # NEW: calendar functions, scs-calendar, scs-date-field, scs-time-field
│       ├── avatar/, badge/, tag/, accordion/, stepper/, usage-meter/, loading/,
│       │   notifications/, chart/, ai/, shell/        # NEW
│       ├── list/                        # table, pagination, KPI card, tabs, breadcrumb, card, skeleton
│       ├── overlay/                     # menu, tooltip, toast
│       ├── dialog/, state/              # modal sizes; empty state types
│       └── icon/icon.component.ts, icon-registry.ts   # registry-driven; `points`
├── projects/ops/
│   ├── src/index.html                   # data-scs-look="ops"
│   ├── src/styles/tokens.css            # from the design file (research R2), on [data-scs-look='ops']
│   ├── src/styles.css                   # + Cairo; + .scs-form--template
│   └── src/app/
│       ├── components/                  # NEW: the console's own (divider, stat, date-range, date-time,
│       │                                #   media, sparkline, ai)
│       ├── gallery/                     # NEW (development only): routes, routes.prod, catalogue,
│       │                                #   design-inventory.json, sections per page, patterns,
│       │                                #   specimen directive, examples
│       ├── shell/                       # onto scs-navbar, scs-nav-item, the notification bell
│       ├── sign-in/, password/, profile/, operators/    # onto the components
│       └── app.routes.ts                # + gallery routes
├── projects/clinic/src/{index.html, styles/tokens.css}  # look attribute; + new token names only
├── projects/patient/src/{index.html, styles/tokens.css} # look attribute
└── e2e/
    ├── gallery/*.spec.ts, gallery/__pictures__/        # NEW: coverage, looks, kept pictures, keyboard
    └── ops-*.spec.ts                    # updated where screens moved
```

**Structure Decision**: shared parts in `ui`, one folder per design page, existing folders brought
to the design in place; the console's own parts and the gallery in `ops`; the gallery is cut from
production at build time; the clinic's and patients' code changes only in its look attribute and
new token names.

**Terms**: "the gallery" is `/dev/components`; "a specimen" is one drawn variant shown still; "the
live example" is a working one; "the look" is a console's tokens; "the copy" is
`tools/icons/library/`; "the registry" is `icon-registry.ts`; "kept pictures" are the gallery
suite's screenshots.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| A page with no BRD requirement (the gallery) | The owner asked to see and use every component in every state; it is a development tool, not part of the product, and is not delivered | A separate tool (Storybook) was declined by the owner and would add a third-party dependency |
| Changing the look attribute and token selector of the clinic and patient apps | The clinic look must be shown in the gallery from the clinic's own file (clarify Q1) | A copy of the clinic's values drifts from the source |
| A 1 MB copy of the icon library in the repository | Adding icons must work offline and while the design file refuses (clarify Q2) | Reading the design file from the tool fails when access drops |
