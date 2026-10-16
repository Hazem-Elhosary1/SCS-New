# Feature Specification: Design Components

**Feature Branch**: `095-design-components`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: empty. Taken from the owner's request of 16 Oct 2026, once the
design file answered again: «الديزاين رجع ابني كل الكمبونانت في الفرونت علشان اقدر استخدما بكل
حالاتها» — build every component of the design in the front end, usable in all its states — and
the owner's answers that day: what is common to both consoles is built once and shared, what is
particular to one console stays with it; the components are seen on a gallery page inside the
operators console, in development only; spec 011 is finished first, then this spec; only the
icons in use are taken, with a way to add any other. Added to the phase list as row
`095-design-components` (Phase 1, after 011), like 093 and 094.

**BRD source** (`docs/reference/brd/source/`): `BRD-0-shared.md` — SH-002 (every surface in
Arabic and English, each person in their own language, Arabic right to left), SH-097 (every
screen has its states: loading, empty, error, offline, read only, not in the plan), SH-098 (the
wording: plain standard Arabic, no gendered address, controls named by their noun), SH-101 (the
three front ends share one technology). `modules/01-shell-signin-home.md` — the rules every list,
dialog and form already follows and that the components carry: CL-SH-046 to CL-SH-062 (one
anatomy for a list; search at the reading start and «تصفية» at the other end; chips of what is
applied with «مسح الكل»; the filter panel opens downward over its rows; the table footer; a row
opens its record by a click; a dialog, drawer or menu marks its opener; a change is followed by a
toast in the reading-start corner; a confirmation button names its action and stays disabled
until what it needs is chosen; the primary button is enabled and checks on submit, a field's
error takes its hint's place; what is known before sending is drawn on the control as disabled
with its reason). `modules/17-ops-support-platform.md` — OP-B-386 (digits in the operators
console are Western in Arabic too).

**Design source**: Figma file `SetO9EhDivNcmurAMfcflY` (the operators console, "SCS Ops Console —
Admin Design System"), read on 16 Oct 2026: Foundations (`8:33`), Getting Started (`8:34`), the
33 component pages — Avatar `8:35`, Badge `8:36`, Button `8:37`, KPI Card `8:39`, Sidebar Nav
Item `8:40`, Checkbox `35:2`, Radio `35:3`, Switch `35:4`, Slider `35:5`, Input `41:2`, Textarea
`41:3`, File Upload `41:4`, Dropdown `57:2`, Loading `59:2`, Alert `60:2`, Modal `62:2`,
Navigation `65:2`, Data Display `68:2`, Date Picker `74:2`, Table `273:2`, Time Picker `77:2`,
Date & Time Picker `79:2`, Navbar `278:2`, Media `82:2`, Notifications `362:2`, Menu `368:2`,
Empty State `374:2`, Tooltip `383:2`, Segmented Control `385:2`, Usage Meter `387:2`, Date Range
Picker `392:2`, Chart `394:2`, AI `399:2` — the Icons page `738:7380` (1,144 icons), and the five
patterns: Search & Filter `111:2`, Page States `117:2`, Confirmation `120:2`, Multi-step Form
`123:2`, Onboarding `253:2`. Every component set has an Arabic and an English twin. For what is
shared, the clinic file `4HLWEqlna9sbn68276LHYt` is read too: Components — Actions `4228:2`,
Inputs `4228:3`, Feedback `4228:4`, Navigation `4228:5`, Containers `4228:6`. Its page
"Components — Clinic" (`4228:7`) holds the clinic's own parts and is not built here.

## Overview

Each screen built so far took the controls it needed, and only in the states that screen showed.
The operators' design file draws far more: 44 component sets with 328 drawn variants in each
language (a button alone has six types, three sizes and six states), the open panels they lead
to (the list of a drop-down, a calendar, a time list, a menu, the notification panel), and five
patterns that compose them. A screen to come will need a state no screen has needed yet: a
pressed button, a range slider, a field in warning, a calendar range, a toast.

This spec builds every component the operators' design file draws, in every drawn state and
option, in Arabic and English, at the design's width and at a phone's, and shows them all on one
gallery page of the operators console that exists only in development. What the clinic file also
draws is built once and shared by both consoles; what only the operators console draws stays
with it. The operators' screens already built move onto these components; the clinic's screens
keep exactly the look they have today. The icons are taken from the design file as they are
needed, with a way to add any other of its 1,144.

It adds no screen a person uses at work and no behaviour to any feature. What it changes for the
operators is the look of the screens already built, wherever a component page differs from what
was built.

Out of this spec: the clinic's own parts (page `4228:7`: appointments, the dental chart, calls,
prescriptions, the queue and the rest), each built by the spec that needs it; a gallery for the
clinic console; moving the clinic's screens onto the new components (each moves with its own
spec, or a later spec says otherwise); any feature behind a component — the AI widgets, charts,
the video player and the onboarding pattern are drawn and wired to example data only; anything
the design file does not draw.

## Clarifications

### Session 2026-10-16

- Q: Which components does this spec build? → A: Every component and pattern the operators'
  design file draws, in all its states. Where the clinic file draws the same component, it is
  built once, shared, and checked against both files. The clinic's own parts are built by their
  own specs.
- Q: When a shared component is brought to the design, what happens to screens already built? →
  A: The operators' screens (sign-in and its steps, the shell, the profile, the operators and the
  roles) move onto the new components in this spec. The clinic's screens keep their current look,
  held by pictures, and move when their own specs come.
- Q: When a component page and a screen template draw the same component differently, which
  wins? → A: The component page is the default everywhere. A template's difference becomes a
  named option, used only where the owner has chosen it — today the operators' forms' field style
  (grey regular labels, a dark empty drop-down), chosen on 14 Oct 2026.
- Earlier the same day (owner): shared what is common, separate what is particular; a gallery
  inside the operators console, in development only, every component in every state, Arabic and
  English, 1440 and 390, no separate tool; finish 011 first; only the icons in use, plus a way to
  add others.
- Carried from 14 Oct 2026 (owner, spec 011): an "on" switch has its knob at the right in both
  languages; the shared dialog and card keep their look; the email under a name and the
  description under a role stay.
- From the BRD (CL-SH-049): the filter panel opens downward over its rows, also at a phone's
  width. The question put to the owner on 16 Oct about letting it open upward is answered by
  this rule.
- Q: How is the clinic's look of a shared component checked, when the clinic's screens stay as
  they are and the gallery is in the operators console? → A: The gallery has a look switch
  ("operators" / "clinic"); with "clinic", every shared component is drawn, pictured and compared
  in the clinic's look against the clinic file's drawings. The clinic's screens are not touched.
- Q: Where does the add-an-icon tool take an icon from? → A: From a copy of all 1,144 icons of
  the design file's Icons page, taken once into the repository and never delivered to users; the
  tool works offline and while the design file refuses access. The copy is taken again only when
  the designer changes the Icons page.
- Q: Does opening the gallery need an operator to sign in? → A: No. In development the gallery
  opens without signing in, as a page of its own outside the console's frame, uses only example
  data and never calls the platform; a production build does not contain it.
- Q: How is "matches its frame" judged? → A: By eye, on side-by-side sheets: for each design page,
  each frame beside its built specimen, every sheet opened. Automatic checks fail only on what is
  certain (cut off, overlapping, sideways scroll, wrong language, non-Western digits in the
  operators console). Once accepted, a picture of each specimen is kept, and a later change that
  alters one is flagged for looking again.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See every component in every state (Priority: P1)

A developer, the designer or the owner opens the gallery in the operators console while it runs
in development. It lists the components by the design file's pages. Under each, every drawn
variant appears as the design names it — for the button, each of the six types in each of the
three sizes in each of the six states — beside a live one that can be pressed, typed into and
opened. A switch at the top shows it all in Arabic or in English, and the page can be looked at
at 1440 and at 390. Each entry names the design node it was built from, so the two can be laid
side by side.

**Why this priority**: The owner asked to use the components "in all their states". Nothing else
in this spec can be checked until each state can be seen.

**Independent Test**: Open the gallery in development; for one page of the design file (for
example Input: twelve states in three sizes), find every drawn variant in both languages and at
both widths, and compare each with its frame.

**Acceptance Scenarios**:

1. **Given** the operators console running in development, **When** the gallery's address is
   opened, without signing in and with no platform running, **Then** it lists the 33 component pages,
   "Other parts", the five patterns and the icons in the design file's order, each with its design
   node.
2. **Given** the gallery, **When** the Button section is shown, **Then** all 108 drawn variants
   (six types × three sizes × six states) appear, each labelled with its type, size and state, in
   the design's words.
3. **Given** the gallery in English, **When** Arabic is chosen, **Then** every component is drawn
   right to left as its Arabic twin draws it, its words are Arabic and its digits are Western.
4. **Given** the gallery at 390, **When** it is scrolled from top to bottom, **Then** nothing is
   cut off, overlaps, or scrolls sideways except where a component is drawn to scroll (a wide
   table).
5. **Given** a production build of the operators console, **When** the gallery's address is
   opened, **Then** the console answers as for any address it does not have, and the gallery is
   not in what is delivered.

---

### User Story 2 - Build a screen from components, any state by setting it (Priority: P1)

A developer building a later screen (plans, clinics, tickets, settings) takes a component and
puts it in the state the screen needs by setting it — a field in warning with its message, a
button busy, a drop-down with a search box, a calendar choosing a range, a toast of success —
without drawing anything new. The component behaves as the design and the BRD's rules say: it
can be worked by keyboard alone, it says what it is to a screen reader, a disabled one says why,
an error takes the hint's place.

**Why this priority**: This is what the owner asked for — components that can be used — and what
every later operators screen depends on.

**Independent Test**: For each interactive component, in the gallery's live example, set each
state that is a setting (disabled, error, busy, read only, chosen, open), work it by mouse and by
keyboard alone, and check what a screen reader is told.

**Acceptance Scenarios**:

1. **Given** a field, **When** it is given an error, **Then** it shows the error's border and the
   message in place of its hint, and the message is read with the field.
2. **Given** a button, **When** it is busy, **Then** it shows the spinner as drawn, keeps its
   width, cannot be pressed twice, and is announced as busy.
3. **Given** a drop-down with search, **When** a word that matches nothing is typed, **Then** the
   list shows the drawn "no results" row.
4. **Given** a date field, **When** a range is chosen, **Then** the calendar shows the start, the
   days between and the end as drawn, in the reading direction of the language.
5. **Given** any disabled control with a reason, **When** it is reached, **Then** the reason is
   shown as the design draws it and is the control's description.
6. **Given** any menu, dialog, popover or list that opens, **When** it opens, **Then** its
   opener is marked (CL-SH-057), Escape closes it and focus returns to the opener.

---

### User Story 3 - The operators' screens built so far use the components (Priority: P2)

The operators' screens already in the console — sign-in and its steps, the shell, the profile,
the operators and the roles — are built from these components, so a later change to a component
reaches them, and each still looks like its own frame.

**Why this priority**: Components that the existing screens do not use would leave two looks for
the same thing in one console.

**Independent Test**: Take every screen and state of specs 010 and 011 in both languages at both
widths before and after; the screens are composed of the gallery's components, and every
difference between before and after is either a correction toward the frame or listed for the
owner.

**Acceptance Scenarios**:

1. **Given** the operators' screens after this spec, **When** each is compared with its frame,
   **Then** it matches as well as or better than before, and every place where the component page
   and the screen's frame disagree is listed with the decision taken.
2. **Given** the operators' forms, **When** they are drawn, **Then** they keep the field style the
   owner chose for them (grey regular labels, a dark empty drop-down) as a named option.

---

### User Story 4 - The clinic keeps its look (Priority: P2)

A member of a clinic's team sees the clinic console exactly as before: a component shared with
the operators console takes the clinic's own colours, type and shapes, and no clinic screen
changes.

**Why this priority**: Sharing must not change a console nobody asked to change.

**Independent Test**: Take every picture of the clinic and patient screens before and after this
spec; they are the same.

**Acceptance Scenarios**:

1. **Given** the clinic and patient screens of specs 001 to 009, **When** they are pictured
   before and after this spec at both widths in both languages, **Then** no picture differs
   beyond the smoothing of edges.
2. **Given** a component shared by both consoles, **When** it is shown in the clinic console,
   **Then** it uses the clinic's look, and its shape agrees with the clinic file's drawing of it;
   where the two files draw it differently, the difference is listed for the designer.
3. **Given** the gallery, **When** the clinic's look is chosen, **Then** every shared component
   appears with the options the clinic file draws, in the clinic's look, and each specimen is
   compared with the clinic file's drawing (page `4228:2`–`4228:6`) in both languages.

---

### User Story 5 - Icons: the ones in use, and any other when needed (Priority: P3)

The console carries only the icons its components and screens use. When a later screen needs
another of the design file's 1,144 icons, a developer adds it by its name in the design file, and
it is drawn like the others at every size the design uses.

**Why this priority**: A screen cannot be built to its frame without the frame's icons; carrying
all 1,144 would weigh on every visit.

**Independent Test**: Count the icons the console carries against the icons its components and
screens name; add one icon by its design name and see it in the gallery's icon section.

**Acceptance Scenarios**:

1. **Given** the console, **When** its icons are listed, **Then** every icon is used by a
   component or a screen, and every icon a component or screen names is there.
2. **Given** an icon name from the design file's Icons page, **When** a developer asks for it to
   be added, **Then** it is taken from the design file as drawn, appears in the gallery's icon
   section, and a component or screen can use it.
3. **Given** Arabic, **When** an icon that points (an arrow, a chevron, "back", "next") is drawn,
   **Then** it points the other way; an icon that does not point (a check, a bell, a clock) is
   drawn the same.

---

### User Story 6 - The patterns as worked examples (Priority: P3)

The gallery shows the five patterns of the design file — search and filter, the page states,
confirmation, a multi-step form and onboarding — composed only of the gallery's components, with
example data, so a screen that follows a pattern starts from it.

**Why this priority**: Patterns are compositions; they prove the components fit together, and
later specs follow them.

**Independent Test**: Compare each pattern in the gallery with its pattern page, in both
languages and at both widths.

**Acceptance Scenarios**:

1. **Given** the Search & Filter pattern, **When** it is shown, **Then** it follows CL-SH-046 to
   CL-SH-050: search at the reading start, «تصفية» at the other end, the panel opening downward
   over the rows, the applied chips with «مسح الكل», and a facet only for a visible column.
2. **Given** the Page States pattern, **When** it is shown, **Then** it has every state of
   SH-097: loading, empty, error, offline, read only and not in the plan, as drawn.

### Edge Cases

- A word longer than its control (a long role name in Arabic, a long email): it wraps or ends with
  an ellipsis as the design draws, and the whole text is still available to read and to a screen
  reader.
- A state the design draws in one language only, or a component drawn on the operators' page but
  missing from the clinic's: built from the drawn twin, mirrored, and listed for the designer.
- A state the design does not draw but the BRD requires (offline, read only, not in the plan, a
  disabled control's reason): built from the drawn parts and listed as undrawn.
- The two design files draw a shared component differently (the Badge has five intents in one
  file and six with two styles in the other; the Avatar's sizes are named in one and measured in
  the other): one component carries what both draw; each console shows only what its file draws;
  the differences are listed for the designer.
- Hover, pressed and focus are states of the pointer and the keyboard: the gallery shows each as a
  still specimen beside the live example, so all can be seen at once.
- Motion (a spinner, a toast arriving, a chevron turning, a skeleton's shimmer): with the system's
  "reduce motion", it is still or reduced, and the state is still clear.
- At 200% zoom, at 390 and in either language, a dialog, menu or panel stays inside the window
  and can be scrolled; the filter panel still opens downward (CL-SH-049).
- An "on" switch has its knob at the right in both languages (owner, 14 Oct 2026).
- Digits are Western in the operators console in both languages (OP-B-386); the clinic console
  keeps its own rule (CL-SH-064).
- An icon named by a component but missing from the console fails the checks before anything is
  delivered, not at run time.
- An icon name that is not in the copy of the Icons page: the tool refuses it and names the
  closest names; the designer's later icons need the copy to be taken again.
- The gallery is opened in production: it does not exist there (story 1, scenario 5).

## Requirements *(mandatory)*

### Functional Requirements

**The components**

- **FR-001**: Every component set on the 33 component pages of the operators' design file MUST be
  built with every drawn option and state, in Arabic and English, as listed in "What is built"
  below. A drawn option is a setting of the component; a drawn state of the pointer or keyboard
  (hover, pressed, focus) is how it answers them.
- **FR-002**: The open parts each page draws beside its sets MUST be built with them: the list of
  a drop-down (default, hover, chosen, multi-select, searchable with results and with none); the
  calendar for one day and for a range; the time list and the time wheel; the date-and-time
  panel; the menu panel and the row-actions ("⋯") menus of every table drawn on the Menu page;
  the folded pagination with its menu; the notification panel; the segmented track; the table
  with its header, rows and footer; the composed loading, skeleton, chart, chat and Ask-AI
  examples; the avatar group, image states, video player, carousel and gallery tiles; and the AI
  suggestion card's loading, none, AI-off and error states.
- **FR-003**: A component MUST look as its component page draws it: sizes, spacing, radii,
  borders, colours, type and shadows as the page and the Foundations page give them. Where a
  screen's template draws the same component differently, the component page is the default and
  the template's difference MUST be a named option, used only where the owner has chosen it.
  Each such difference found MUST be listed in this spec's validation with the decision.
- **FR-004**: A component MUST be shared by both consoles when the clinic's design file draws a
  component of the same name and purpose on its component pages (`4228:2`–`4228:6`), and MUST
  stay with the operators console when only the operators' file draws it. The first reading is
  the "Shared" column below; the plan confirms each by laying the two files' drawings side by
  side.
- **FR-005**: A shared component MUST take each console's own look from that console's design
  file, and MUST carry the options either file draws, each console using only those its file
  draws. Differences between the two files' drawings MUST be listed for the designer.
- **FR-006**: The parts the console already has (the button, the text field, the drop-down, the
  checkbox, the switch, the text area, the status mark, the KPI card, the breadcrumb, the tabs,
  the dialog, the confirmation, the menu, the tooltip, the toast, the banner, the empty state, the
  skeleton, the file upload and file card, the list toolbar, the filter panel, the applied chips,
  the table footer, the code input, the password rules, the language toggle) MUST become the
  components of this spec, not a second set beside them.

**Behaviour every component keeps**

- **FR-007**: Every interactive component MUST be workable by keyboard alone in the way its kind
  is expected to work (a button by Enter and Space; a check box and a switch by Space; a radio
  group, tabs, a segmented control and a list by the arrows; a menu, a list, a calendar and a
  time list by the arrows, Home, End, Enter and Escape; a slider by the arrows, Page Up and Down,
  Home and End), MUST show focus as the design's focus state draws it, and MUST tell a screen
  reader its name, its kind, its value and its state.
- **FR-008**: A component that opens something (a menu, a list, a calendar, a dialog, a popover,
  the notification panel) MUST mark its opener while open (CL-SH-057), close on Escape
  and on a click outside where the design closes it, return focus to the opener, and keep the
  opened part inside the window at every width.
- **FR-009**: A disabled control with a known reason MUST show the reason as the design draws it
  and make it the control's description (CL-SH-062); a field's error MUST take its hint's place
  and be read with it (CL-SH-061).
- **FR-010**: Every component MUST be drawn right to left in Arabic as its Arabic twin draws it;
  an icon that points MUST point the other way in Arabic; an "on" switch MUST keep its knob at the
  right in both languages.
- **FR-011**: In the operators console, digits MUST be Western in both languages (OP-B-386);
  identifiers and email addresses are Latin in both.
- **FR-012**: Every control a person presses MUST be at least 40 by 40 at a phone's width, as in
  spec 011.
- **FR-013**: With the system's "reduce motion", no component MUST move more than to show a state
  change; spinners and shimmers stay still or change slowly.
- **FR-014**: Every word a component shows by itself (a "no results" row, "Search…", a close
  button's name, a pagination's "previous" and "next", "Load more") MUST come from the design in
  both languages and follow SH-098.

**The gallery**

- **FR-015**: The operators console MUST have a gallery page, reachable only when the console
  runs in development, that a production build does not contain and answers as an address it
  does not have. In development it MUST open without signing in, as a page of its own outside the
  console's frame (the navbar and side bar appear in it as components), and MUST work with no
  platform running.
- **FR-016**: The gallery MUST list the 33 component pages, "Other parts" (the shared parts no
  component page draws), the five patterns and the icons, in the design file's order, each with its name, its design node, and whether it is shared or the
  operators' own.
- **FR-017**: For each component, the gallery MUST show every drawn option and state as a still,
  labelled specimen, and at least one live example a person can work, including the open parts
  of FR-002.
- **FR-018**: The gallery MUST switch between Arabic and English for everything on it at once,
  and MUST read correctly at 1440 and at 390.
- **FR-018a**: The gallery MUST switch between the operators' look and the clinic's look. In the
  clinic's look it MUST show only the shared components, each with the options the clinic file
  draws, in the clinic's colours, type, spacing, radii and shadows; the operators' own components
  are hidden there.
- **FR-019**: The gallery MUST show the five patterns composed only of the gallery's components
  with example data, following the BRD rules they illustrate (CL-SH-046 to CL-SH-050 for search
  and filter; SH-097 for page states; CL-SH-060 for confirmation).
- **FR-020**: Everything shown in the gallery's examples MUST be example data in the code; the
  gallery MUST NOT read or write anything of the platform.

**The screens already built**

- **FR-021**: The operators' screens of specs 010 and 011 (sign-in and its steps, the shell, the
  profile and its dialogs, the operators and the roles with their forms and dialogs) MUST use the
  components of this spec, and MUST each still match their frames; every change of look on them
  MUST be a correction toward a frame or listed for the owner.
- **FR-022**: The operators' forms MUST keep the field style the owner chose on 14 Oct 2026 (grey
  regular labels, a dark empty drop-down), as a named option of the shared field and drop-down.
- **FR-023**: No screen of the clinic console or the patients' pages MUST change its look; a
  shared component MUST show there exactly as before.

**The icons**

- **FR-024**: The consoles MUST carry only the icons their components and screens use; every icon
  a component or screen names MUST be present, or the checks fail before delivery.
- **FR-025**: A developer MUST be able to add any icon of the design file's Icons page by its name
  there, taken from a copy of the whole Icons page kept with the source and never delivered to
  users, so that adding one needs neither the design file nor a network; the icon is taken as drawn, sized as the design uses icons (16, 18, 20, 24), drawn in the
  colour of its surroundings, and appears in the gallery's icon section.
- **FR-026**: Each icon MUST say whether it points, so that it is mirrored in Arabic only if it
  does.

**Checks**

- **FR-027**: Every drawn variant MUST be pictured in both languages at 1440 and 390 from the
  gallery — the shared ones in both looks — and compared by eye with its frame in the matching
  design file, on a sheet per design page that lays each frame beside its specimen; every picture MUST be opened before it is
  said to be compared.
- **FR-027a**: Automatic checks MUST fail a specimen only for what is certain: something cut off,
  two things overlapping, a sideways scroll the design does not draw, a word in the other
  language, or a non-Western digit in the operators console.
- **FR-027b**: Once a specimen's picture is accepted it MUST be kept, and a later change that
  alters the picture of any specimen MUST be flagged, naming the specimen, so it is looked at
  again and either accepted anew or corrected.
- **FR-028**: The keyboard and screen-reader behaviour of FR-007 to FR-009 MUST be checked for
  every interactive component, in both languages.
- **FR-029**: The clinic's and patients' screens MUST be pictured before and after, and the
  pictures compared (FR-023).

### What is built

From the operators' file's component sets (English twins; each also in Arabic). "Shared" is the
first reading of FR-004: the clinic file draws a component of the same name and purpose.

| Page | Component | Drawn options and states | Shared |
|---|---|---|---|
| Avatar | Avatar | Size: Small, Medium, Large | yes |
| Badge | Badge | Intent: Success, Warning, Danger, Info, Neutral | yes |
| Button | Button | Type: Primary, Secondary, Tertiary, Ghost, Link, Destructive · Size: Small, Medium, Large · State: Default, Hover, Pressed, Focus, Disabled, Loading · with a leading icon, a trailing icon, text only, icon only | yes |
| KPI Card | KPI Card | Trend: Positive, Negative, Neutral | yes |
| Sidebar Nav Item | Sidebar Nav Item | State: Default, Hover, Active, Disabled | yes |
| Checkbox | Checkbox | State: Unchecked, Checked, Indeterminate, Disabled, Hover, Focus, Error | yes |
| Radio | Radio | State: Unselected, Selected, Disabled, Hover, Focus, Error | yes |
| Switch | Switch | State: Off, On, Disabled, Hover, Focus, Loading | yes |
| Slider | Slider | Type: Single, Range · State: Default, Hover, Focus, Disabled | yes |
| Input | Input | State: Empty, Default, Hover, Focus, Active, Filled, Disabled, ReadOnly, Error, Success, Warning, Loading · Size: Small, Medium, Large | yes |
| Textarea | Textarea | State: Default, Hover, Focus, Filled, Disabled, Error · Size: Small, Medium, Large | yes |
| File Upload | File Upload | State: Default, Hover, Dragover, Uploading, Success, Error | yes |
| Dropdown | Select Trigger and its list | State: Default, Hover, Focus, Filled, Disabled, Error · list: default, hover, chosen, multi-select, searchable with results and empty | yes |
| Loading | Spinner · Progress Bar · Skeleton Line | Spinner Size: Small, Medium, Large, XLarge · Progress: Determinate, Indeterminate · Skeleton width 280, 220, 140 · composed loader and skeleton | yes |
| Alert | Alert | Style: Banner, Toast · Intent: Info, Success, Warning, Error | yes |
| Modal | Modal | Size: Small, Medium, Large, Full-screen | yes |
| Navigation | Tab Item · Breadcrumb Item · Pagination Item · Stepper Step · Pagination Menu | Tab: Default, Active · Breadcrumb: Default, Current · Pagination: Default, Active, Disabled, Ellipsis, Ellipsis Open · Stepper: Complete, Active, Upcoming · folded pagination | yes |
| Data Display | Card · Tag · Accordion Item · Divider · Stat | Card: Default, Hover, Selected · Tag: Default, Success, Warning, Neutral × dismissible or not · Accordion: Expanded, Collapsed · Divider: Plain, With Label · Stat: Positive, Negative, Neutral | Card, Tag, Accordion yes; Divider, Stat operators' own |
| Date Picker | Date Field Trigger and calendar | State: Default, Hover, Focus, Filled, Disabled, Error · calendar for a day and for a range | yes |
| Table | Table Row and table | State: Default, Hover, Selected · the composed table | yes |
| Time Picker | Time Field Trigger, time list, time wheel | State: Default, Hover, Focus, Filled, Disabled, Error | yes |
| Date & Time Picker | Date & Time Field Trigger and panel | State: Default, Hover, Focus, Filled, Disabled, Error | operators' own |
| Navbar | Navbar | plain, with an unread count on the bell, with an uploaded logo | yes |
| Media | Image · avatar group · video player · carousel · gallery tile | Image: Default, Loading, Error | operators' own |
| Notifications | Notification Bell · Notification Item · panel | Bell: Default, Hover, Unread · Item: Unread, Read | Item and panel yes; Bell with the Navbar |
| Menu | Menu Item · menu panel · row-actions menus · menu icons | Item: Default, Hover, Danger, Danger Hover · the "⋯" button's states | yes |
| Empty State | Empty State | Type: Empty, Error, Loading, Success | yes |
| Tooltip | Tooltip | Placement: Top, Bottom, Left, Right (start and end in Arabic) | yes |
| Segmented Control | Segment and track | State: Default, Selected | yes |
| Usage Meter | Usage Meter | Level: Normal, Warning, Critical | yes |
| Date Range Picker | Date Range Trigger and range calendar | State: Default, Hover, Focus, Filled, Disabled, Error | trigger operators' own; calendar shared |
| Chart | Sparkline · bar chart | Sparkline Trend: Up, Down, Flat · bar chart | bar chart yes; Sparkline operators' own |
| AI | AI Badge · AI Insight Card · Message Bubble · Ask-AI field and button · chat widget | Badge Size: Small, Medium · Insight: Insight, Suggestion · Bubble: Bot, User, with media · suggestion card: loading, none, AI off, error | Insight Card and Message Bubble yes; the rest operators' own |

In all: 44 component sets with 328 drawn variants in each language, the two standalone ones
(Navbar, Pagination Menu), the open parts of FR-002, the five patterns, and the icons in use.

### Key Entities

- **Component**: a part drawn on a component page; has a name, the page and node it comes from,
  whether it is shared or one console's own, its options and its states.
- **Option**: a setting of a component that a screen chooses (type, size, intent, placement, a
  state such as disabled, error, busy or read only).
- **State of interaction**: how a component answers the pointer and the keyboard (hover, pressed,
  focus); shown in the gallery as a still specimen.
- **Console look**: the colours, type, spacing, radii and shadows of one console, from its own
  design file's Foundations; a shared component is drawn in the look of the console it is in.
- **Named option of a template**: a template's difference from a component page, used only where
  the owner chose it (FR-003, FR-022).
- **Gallery entry**: one component or pattern on the gallery page, with its design node, its
  specimens and its live example.
- **Icon**: a drawn symbol taken from the Icons page by its name; says whether it points.
- **Icon source copy**: all 1,144 icons of the Icons page as drawn on the day they were copied,
  with their names and groups; kept with the source, never delivered; the only place the tool
  takes icons from.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 328 drawn variants, in each of the two languages, appear in the gallery; a
  count against the design file at the time of checking finds none missing.
- **SC-002**: Every specimen, pictured at 1440 and at 390 in both languages (the shared ones in
  both looks), has been seen beside its frame on an opened sheet; every difference found is
  either corrected or listed with whose decision it is, and none is left unlisted.
- **SC-002a**: After acceptance, a change to any component that alters any specimen's picture is
  flagged by the checks in every case, with the specimens it altered named.
- **SC-003**: Every interactive component can be worked from start to end with the keyboard
  alone, in both languages, and tells a screen reader its name, kind, value and state.
- **SC-004**: Every screen and state of the clinic console and the patients' pages that their
  browser suites reach pictures the same before and after this spec, at 1440 and 390, in both
  languages.
- **SC-005**: Every screen and state of the operators console built by specs 010 and 011 still
  passes its checks, and is at least as close to its frame as before.
- **SC-006**: A production build of the operators console contains no part of the gallery, and
  carries no icon that no component or screen uses.
- **SC-007**: A developer can add an icon from the design file by its name and use it in a screen
  in under five minutes, without drawing anything.
- **SC-008**: A later operators screen needs no new look for a control the design file draws:
  checked at the analysis of the next spec that builds a screen (012 onward) — it adds no control
  that this spec's gallery already shows. Not verifiable within this spec.

## Assumptions

- The operators' design file as read on 16 Oct 2026 is the reference; if the designer changes a
  component page later, that change is a new piece of work.
- "Shared" is judged by name and purpose on the clinic file's component pages; a same-named part
  whose purpose differs (the operators' Stat and the clinic's KPI card) is not merged.
- The gallery is for the people who build and review the product; it needs no sign-in and no
  permission, exists only in development, and is not a feature of the console.
- Example data in the gallery is invented and contains no real person, clinic or address.
- The clinic console's look is held, not rebuilt: shared components keep drawing the clinic's
  screens as they do now. The clinic's look of every shared component is seen through the
  gallery's look switch; a gallery inside the clinic console is not needed.
- The icon set's 1,144 icons are drawn in the same family and size grid, so any of them can be
  taken the same way.
- Features behind some components (AI suggestions, charts of real figures, video, onboarding) are
  built by their own specs; here they are drawn with example data only.
- The components are checked in Chromium and in Microsoft Edge (the owner works in Edge), as in
  spec 011; Firefox and Safari are not checked by this spec.
- The rules of spec 011 that are about looks (digits, the switch's knob, control sizes at 390, the
  operators' forms' field style) stand and are carried by the components.

## For the owner and the designer

To be filled while building: each difference between a component page and a template frame, and
each difference between the two files' drawings of a shared component, with the decision taken.
Known at the start:

- The Badge: five intents in the operators' file; six intents in two styles and two sizes in the
  clinic's.
- The Avatar: sizes Small, Medium, Large in the operators' file; 24, 32, 40, 56 with initials or
  an icon in the clinic's.
- The Dropdown page draws the empty drop-down grey; the Operators template draws it dark — the
  operators' forms keep it dark by the owner's choice; the filter panel follows the component
  page (grey).
