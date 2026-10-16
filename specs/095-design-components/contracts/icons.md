# Contract: Icons (spec 095)

## The copy

`src/web/tools/icons/library/part-*.json`: every icon of the operators' design file's Icons page
(`738:7380`, 1,144) and the 16-pixel menu icons of its Menu page (`2200:86`), each as `[design name, group, node, width,
height, shapes]`, the shapes as exported on the day of the copy (coordinates rounded to hundredths
of a pixel; the common paints written as codes: `S` stroked with round ends, `F` filled, `FE` filled
even-odd). Never imported by an app. Taken
again only when the designer changes the Icons page (a task with the connector, not the tool).

## The tool — `node tools/icons.mjs`

| Command | Does | Fails when |
|---|---|---|
| `list <words>` | Prints the design names in the copy that contain the words, with their groups | — |
| `add <design name> --as <short name> [--points]` | Copies the icon into `ui/src/lib/icon/icon-registry.ts` under the short name, with its `viewBox`, paths and paint as drawn; `--points` marks it as pointing (drawn left to right, mirrored on an Arabic page) | The name is not in the copy (prints the closest names); the short name is taken |
| `check` | Lists registered icons that no file under `src/web/projects` uses | Any is unused (run by `npm run ci`) |

Adding an icon needs no network and no access to the design file (clarify Q2).

## The registry and the component

- `ScsIconName` is the registry's keys: naming an absent icon is a build error.
- `<scs-icon name size>`: sizes as the design uses them (16, 18, 20, 24); drawn in `currentColor`;
  hidden from screen readers (the words beside it carry the meaning); an icon with `points` is drawn for
  one direction (`ltr` for every icon copied from the design file; `rtl` for the hand-drawn "back")
  and mirrored on a page of the other.
- The 53 icons drawn before this spec stay as entries marked `drawn`, unchanged in look.
- The gallery's "Icons" section shows every registered icon at each size, with its short name, its
  design name and whether it points.
