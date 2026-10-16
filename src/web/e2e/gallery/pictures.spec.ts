import { expect, test } from '@playwright/test';
import { CATALOGUE } from '../../projects/ops/src/app/gallery/catalogue';
import { GALLERY_SECTIONS } from '../../projects/ops/src/app/gallery/gallery-sections';
import { INVENTORY, Language, Look, NARROW, WIDE, kebab, openSection } from './gallery-platform';

/**
 * The kept pictures (spec 095, FR-027b, research R7): one picture of every specimen of every built
 * section, in both languages, wide and on a phone, and in the clinic's look where the section has
 * parts the clinic shares — `<page>/<component>/<variant>--<ar|en>--<1440|390>[--clinic].png` under
 * `e2e/gallery/__pictures__/`. A change that alters a specimen fails with its name; after looking at
 * it, the picture is accepted anew with `--update-snapshots`.
 *
 * Left out: the working examples (they move and keep what is typed) and the placeholder panel, which
 * draws whether the platform answers — a fact of the machine, not of the part.
 */
const shared = new Set(Object.entries(CATALOGUE).filter(([, entry]) => entry.kind === 'shared').map(([key]) => key));
const pagesWithShared = new Set(
  INVENTORY.pages.filter((page) => page.sets.some((set) => shared.has(`${page.page}/${set.name}`))).map((page) => kebab(page.page)),
);
const LEFT_OUT = new Set(['specimen-other-parts/parts/placeholder-panel']);

/**
 * The gallery's bar follows the page down: a specimen scrolled under it, or taller than the window,
 * would be drawn with the bar across it. While pictures are taken it stays at the top of the page
 * (the screenshot's own `style` option comes too late for it).
 */
const STILL_BAR = '[data-testid="gallery"] { position: static !important; }';

const built = GALLERY_SECTIONS.filter((section) => section.load && (section.kind === 'component' || section.kind === 'other' || section.kind === 'pattern'));

test.describe.configure({ mode: 'parallel' });

for (const section of built) {
  const looks: Look[] = pagesWithShared.has(section.id) || section.kind === 'other' ? ['ops', 'clinic'] : ['ops'];
  for (const language of ['ar', 'en'] as Language[]) {
    for (const look of looks) {
      test(`${section.title} — ${language}, ${look} look: the kept pictures`, async ({ page }) => {
        test.setTimeout(180_000);
        await openSection(page, section.id, language, look);
        await page.addStyleTag({ content: STILL_BAR });
        for (const size of [WIDE, NARROW]) {
          await page.setViewportSize(size);
          await page.evaluate(() => document.fonts.ready);
          const ids = await page
            .locator(`[data-testid="section-${section.id}"] [data-testid^="specimen-"]`)
            .evaluateAll((elements) =>
              elements
                .filter((element) => (element as HTMLElement).offsetParent !== null)
                .map((element) => element.getAttribute('data-testid') ?? ''),
            );
          for (const id of ids) {
            if (LEFT_OUT.has(id)) continue;
            const path = id.replace(/^specimen-/, '').split('/');
            const file = `${path.pop()}--${language}--${size.width}${look === 'clinic' ? '--clinic' : ''}.png`;
            await expect.soft(page.getByTestId(id), id).toHaveScreenshot([...path, file], { animations: 'disabled', caret: 'hide' });
          }
        }
      });
    }
  }
}
