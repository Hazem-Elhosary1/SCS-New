# Quickstart: Design Components (spec 095)

How to see, run and check the work. Commands run from `src/web` with
`TEMP`/`TMP` set to `E:\Heta_SCS\src\web\node_modules\.tmp` and `PLAYWRIGHT_BROWSERS_PATH=0`
(see `docs/setup.md`). No API, database or sign-in is needed for the gallery.

## 1. Open the gallery

```bash
npm run start:ops
```

Open `http://localhost:4203/dev/components`. Expected: the bar with the language and look
switches; 33 component sections, "Other parts", five patterns and "Icons", in the design file's
order (`contracts/gallery.md`). Switch to English and back; switch to the clinic look: only shared
components remain, in the clinic's colours.

`http://localhost:4203/dev/components/button` shows the button alone: 108 specimens per language,
labelled `Primary · Medium · Hover` and so on, and a live button.

## 2. Work the live examples by keyboard

For each live example, from the address bar press Tab until it is reached, then use the keys of
`contracts/components.md`. Expected: every one works without the mouse; focus is drawn as the
design's focus state; Escape closes what was opened and focus returns to its opener.

## 3. Production contains no gallery

```bash
npm run build:all
```

```bash
node tools/check-production.mjs
```

Expected: the build passes with no size warning for `ops`; the check prints that no file of
`dist/ops` names the gallery. Serving the built `ops` and opening `/dev/components` shows the
console's "not found" answer.

## 4. Icons

```bash
node tools/icons.mjs list information
```

```bash
node tools/icons.mjs add Interface-Essential/information-circle --as info-circle
```

Expected: the icon appears in the gallery's "Icons" section at 16, 18, 20 and 24; using it in a
component compiles. Then:

```bash
node tools/icons.mjs check
```

Expected: it fails while `info-circle` is used nowhere, and passes once it is used or removed.

## 5. Pictures

```bash
npx playwright test e2e/gallery
```

Expected: every specimen matches its kept picture; the first run, or one after an accepted change,
is taken with `--update-snapshots` only after the pictures were looked at. The run also fails on any
fault of `opsLooksWrong` and on any inventory variant without its specimen.

## 6. The clinic is unchanged

Before the work and after it, take the clinic's and patients' pictures:

```bash
npx playwright test e2e/clinic.spec.ts e2e/shell.spec.ts e2e/sign-in.spec.ts
```

with `SCS_SHOTS` set to a folder for "before" and then for "after" (the full list of clinic and
patient files is in `tasks.md`). Compare the two folders with the comparison script (research
R9). Expected: no picture changes size; none differs in more than 0.05% of its pixels; any that
does is opened and explained.

## 7. The full gates

```bash
npm run ci
```

```bash
npx playwright test
```

Expected: lint, the translation check (the gallery's words are not in the console's files), the
tools' tests, unit tests, the icon check, the builds and the production check pass; every browser
test passes in Chromium, and the console's in Edge.

## 8. Beside the design

For each design page, the sheet laying the frames beside the specimens (both languages, both
looks) is made and opened; the differences go to `validation.md` with their decisions, and one
side-by-side picture of the whole spec goes to `specs/095-design-components/design-vs-real.png`.
