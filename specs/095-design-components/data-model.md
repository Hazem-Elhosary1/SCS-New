# Data Model: Design Components (spec 095)

No server, no database, no stored record. The "data" of this spec is what describes the
components: the inventory read from the design file, the gallery's catalogue built against it,
the two looks, and the icons. All of it is source files in `src/web`.

## Design inventory

What the operators' design file draws, written once from the file and kept with the source
(`src/web/projects/ops/src/app/gallery/design-inventory.json`). It is the list the gallery is
checked against (SC-001).

| Field | Meaning | Rule |
|---|---|---|
| `page` | The design page's name and node (`Button`, `8:37`) | One of the 33 component pages |
| `set` | The component set's name and its English and Arabic nodes (`Button` `31:302`, `Button (عربي)` `316:470`) | Both twins present, or the missing one listed as a difference |
| `variant` | The drawn option values (`Type=Primary, Size=Medium, State=Hover`) | Exactly as the design writes them |
| `parts` | The drawn parts beside the sets (list panel `58:3`, calendar `75:4`, …) with their nodes | FR-002 |
| `read` | The date the file was read | `2026-10-16` |

Counts at reading: 44 sets, 328 variants in each language, 2 standalone components, the parts of
FR-002, 5 pattern pages.

## Catalogue entry (one per component)

The gallery's own list (`gallery/catalogue.ts`), one entry per component, in the design file's
page order.

| Field | Meaning | Rule |
|---|---|---|
| `page` | Design page name and node | Matches the inventory |
| `name` | The component as the design names it | Matches a `set` or a standalone component |
| `kind` | `shared` or `ops` | FR-004; `shared` only if the clinic file draws it (`clinicNode` set) |
| `clinicNode` | The clinic set it is checked against (`Button` `4252:2227`) | Required when `shared` |
| `options` | The drawn options and their values | Every inventory variant is a combination of these |
| `stillStates` | The states shown by `scsSpecimen` (hover, focus, pressed) | Only pointer and keyboard states (research R5) |
| `live` | Whether a working example is shown | Always for interactive components (FR-017) |
| `replaces` | The existing `ui` part it is (FR-006), if any | See `contracts/components.md` |

## Specimen

One drawn variant on the gallery, in one language and one look.

| Field | Meaning |
|---|---|
| `key` | `<page>/<component>/<variant>`; the variant written as the design writes it, for example `button/button/primary-medium-hover` |
| `language` | `ar` or `en` (the page's language at that moment) |
| `look` | `ops` or `clinic`; `clinic` only for `shared` components |
| `testId` | `specimen-<key>` on the specimen's wrapper |
| `designNode` | The node of the drawn variant in the file of the look |

**Rules**: every inventory variant has exactly one specimen per language in the ops look, and one
per language in the clinic look when `shared` (SC-001); each specimen carries its label (the
option values in the design's words) and its design node, visible beside it.

## Look

| Look | Where its values are | Applies to |
|---|---|---|
| `ops` | `ops/src/styles/tokens.css`, declared on `[data-scs-look='ops']` | The console; the gallery's default |
| `clinic` | `clinic/src/styles/tokens.css`, declared on `[data-scs-look='clinic']` | The clinic console; the gallery's "clinic" switch, shared components only |
| `patient` | `patient/src/styles/tokens.css`, on `[data-scs-look='patient']` | The patients' pages (not shown in the gallery) |

**Rules**: a shared component reads tokens only (no colour, size or shadow written in it); a token
a component uses exists in every look that shows it; the clinic's and patients' values of existing
tokens do not change.

## Named option of a template

| Field | Meaning |
|---|---|
| `name` | The class that applies it (`scs-form--template`) |
| `sets` | The component custom properties it sets (`--scs-field-label-weight: 400`, `--scs-field-label-color`, `--scs-select-empty-color`) |
| `chosen` | Who chose it and when (owner, 14 Oct 2026) |
| `used by` | The screens that apply it (the operators' add and edit forms, the role form) |

## Icon library copy

`src/web/tools/icons/library/part-*.json` — never imported by an app.

| Field | Meaning | Rule |
|---|---|---|
| `name` | The icon's name in the design file (`Interface-Essential/information-circle`, `fi-rr-search`, `Menu/menu-icon/edit`) | Unique |
| `group` | Its group on the Icons page (`Interface-Essential`, `Rounded`, `Straight`, `Menu`, …) | One of the page's groups |
| `node` | Its node in the design file | — |
| `svg` | The drawing exported as SVG text | As exported; not edited |
| `copied` | The date of the copy | One date for the whole file |

Counts: 1,144 from the Icons page plus the 16-pixel menu icons of the Menu page.

## Icon registry entry

`ui/src/lib/icon/icon-registry.ts` — what the apps can show.

| Field | Meaning | Rule |
|---|---|---|
| key | The short name a component uses (`search`, `chevron-down`) | Unique; the type `ScsIconName` is the set of keys |
| `from` | The design name it was copied from, or `drawn` for the 53 drawn by hand before this spec | Traceable to the copy when not `drawn` |
| `viewBox` | The drawing's box | — |
| `paint` | `stroke` or `fill` | As the source draws it |
| `body` | The paths | As copied |
| `points` | Whether it points (an arrow, a chevron, "back", "next", sign-out), and the direction it is drawn for | `ltr` or `rtl`; mirrored on a page of the other direction (FR-026) |

**Rules**: every key is used somewhere in `src/web/projects` (checked by `tools/icons.mjs check`,
FR-024); every name used is a key (compile error otherwise).

## Kept picture

| Field | Meaning |
|---|---|
| path | `e2e/gallery/__pictures__/<page>/<component>/<variant>--<ar|en>--<1440|390>[--clinic].png` |
| accepted | When a person looked at it and accepted it (`--update-snapshots`) |

**Rule**: a run whose picture of a specimen differs from its kept picture fails, naming the path
(FR-027b, SC-002a).
