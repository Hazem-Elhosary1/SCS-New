# Contract: The Gallery (spec 095)

## Address and presence

- `/dev/components` in the operators console, in development only (`ng serve ops`, 4203). A
  production build has no such route and answers as for any unknown address; no file of the
  production output names the gallery (`tools/check-production.mjs`, run after `build:all`).
- No sign-in, no guard, no call to the platform; it stands outside the console's frame. Everything
  shown is example data in `ops/src/app/gallery/`.
- The gallery's own words (its title, the switches, section and option labels) are data in its
  catalogue in both languages, not keys in the console's translation files, which are delivered
  with production; the components inside it use their own keys as on any screen.
- `/dev/components/<page>` opens one design page's section alone (for pictures and for looking at
  one thing); `<page>` is the design page in kebab case (`button`, `date-picker`, `pattern-search-filter`).

## Layout

- A bar at the top: the page's title «مكوّنات التصميم» / "Design components" (development only, not
  translated by the BRD), the language switch (العربية / English), the look switch (المشغّلين /
  العيادة — "Operators" / "Clinic"), and a list of sections to jump to.
- One section per design page, in the file's order: the 33 component pages, "Other parts", the
  five patterns, and "Icons". Each section's heading names the page and its node.
- In a section, per component: its name, `shared` or `operators' own`, its design nodes; then the
  specimens grouped as the page groups them (for the button: by size, then type, then state), each
  labelled with its option values in the design's words and its variant's node; then the live
  example. Labels and nodes are the design's own names (English even on the Arabic page); they carry
  `data-scs-label`, and the language check skips them.
- In the clinic look only shared components are shown, with the options the clinic file draws, each
  labelled with the clinic node.
- The page reads at 1440 and at 390; at 390 specimens stack in one column.

## Test identifiers

| Element | `data-testid` |
|---|---|
| The page | `gallery` |
| Language switch | `gallery-language` |
| Look switch | `gallery-look` |
| A section | `section-<page>` |
| A component | `component-<page>-<component>` |
| A specimen | `specimen-<page>/<component>/<variant>` |
| A live example | `live-<page>-<component>` |
| The icons section's grid item | `icon-<name>` |

## What is checked

- Every variant of `design-inventory.json` has its specimen in each language (and in the clinic
  look when shared) — SC-001.
- `opsLooksWrong` on every section, both languages, both widths, both looks — FR-027a; the digit
  check per look (conventions 5); `[data-scs-label]` skipped by the language check.
- A kept picture per specimen — FR-027b.
- The live examples by keyboard and by pointer — FR-028.
- A production build contains none of it — SC-006.
