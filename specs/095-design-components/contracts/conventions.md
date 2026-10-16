# Contract: What Every Component Follows (spec 095)

## Look

1. A component reads its colours, type, spacing, radii, borders and shadows from tokens only
   (`--scs-*`); nothing is written in the component (research R2, R3). A token a component needs
   and no look has is added to every look that shows the component.
2. Sizes that are the component's own geometry (a 44-pixel trigger, a 40-pixel row, a 1.5-pixel
   focus border) are written as the component page draws them — but a shared component's geometry
   that the two design files draw differently (a height, a radius, a padding, a border width) is a
   per-look custom property of that component (`--scs-button-height-md`, …), whose default is
   what the clinic's screens draw today, and the operators' look sets the operators' value. So a
   shared part never changes shape on a clinic screen (FR-023).
3. The component page is the default. A template's difference the owner chose is a named option
   applied by a class on a container (research R13); a component documents the custom properties
   such an option may set, and nothing else may be overridden from outside.
4. In Arabic the component is drawn as its Arabic twin: start and end, not left and right;
   pointing icons mirrored (registry `points`); an "on" switch's knob at the right in both
   languages.
5. Digits follow the look's rule: Western in the operators' look in both languages (OP-B-386);
   in the clinic's look, Eastern Arabic in Arabic and Western in English (CL-SH-064). The gallery
   checks digits per look.
6. With `prefers-reduced-motion: reduce`, nothing moves except to show a change of state.

## Options and states

7. An option the design names is an input with the design's values in lower case (`size`:
   `sm` · `md` · `lg`; `intent`: `success` · `warning` · …). A state that is a setting (disabled,
   error, read only, busy, chosen, open, expanded) is an input. A state of the pointer or keyboard
   (hover, pressed, focus) is never an input.
8. Each interactive component styles hover, pressed and focus both by the pseudo-class and by an
   ancestor's `data-scs-state` (`:host-context([data-scs-state~='hover'])`, or
   `[data-scs-state~='hover'] .scs-button` for a part styled by the global sheet); only the gallery's
   specimen wrapper sets that attribute, and makes the specimen `inert` (research R5).
9. A disabled control with a reason shows it as drawn and points to it with
   `aria-describedby` (CL-SH-062). A field's error replaces its hint and is read with it
   (CL-SH-061). A busy control keeps its size, cannot be pressed again and says it is busy
   (`aria-busy`).
10. A component that opens something marks its opener (`aria-expanded`, CL-SH-057), closes on
    Escape and on a click outside where the design closes it, returns focus to the opener, and
    keeps what it opened inside the window at 390 and at 200% zoom. The filter panel opens
    downward (CL-SH-049).
11. Every control a person presses is at least 40 by 40 at 390 (spec 011).

## Words and names

12. A component shows no word of its own except those the design draws for it ("Search…",
    "No results found", a close button's name, "Previous", "Next"); they come from the shared
    translation keys `ui.*` in both languages and follow SH-098. Everything else is given by the
    screen.
13. Every interactive component has an accessible name, its kind (role), its value and its
    state; an icon-only button takes `label`.
14. Every component takes `testId`, put on its main element; the parts it opens use
    `<testId>-panel`, `<testId>-option`, and so on.

## Tests

15. Each component has unit tests for its options, its keys (research R10), its names and its
    refusals (a disabled option cannot be chosen; a busy button cannot be pressed twice).
16. Each component has its specimens and a live example in the gallery (`gallery.md`), and its
    kept pictures (research R7).
