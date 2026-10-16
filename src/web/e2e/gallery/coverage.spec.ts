import { expect, test } from '@playwright/test';
import { INVENTORY, Language, Look, NARROW, WIDE, galleryLooksWrong, kebab, openSection } from './gallery-platform';
import { CATALOGUE } from '../../projects/ops/src/app/gallery/catalogue';
import { GALLERY_SECTIONS } from '../../projects/ops/src/app/gallery/gallery-sections';

/**
 * Every variant the operators' design file draws is on the gallery, in both languages, and the
 * shared ones in the clinic's look too (spec 095, SC-001); and nothing on any section is certainly
 * wrong at 1440 or at 390 (FR-027a).
 */
const shared = new Set(Object.entries(CATALOGUE).filter(([, entry]) => entry.kind === 'shared').map(([key]) => key));

for (const designPage of INVENTORY.pages) {
  const section = kebab(designPage.page);
  for (const language of ['ar', 'en'] as Language[]) {
    const looks: Look[] = designPage.sets.some((set) => shared.has(`${designPage.page}/${set.name}`)) ? ['ops', 'clinic'] : ['ops'];
    for (const look of looks) {
      test(`${designPage.page} — ${language}, ${look} look: every drawn variant, nothing wrong`, async ({ page }) => {
        await page.setViewportSize(WIDE);
        await openSection(page, section, language, look);
        const missing: string[] = [];
        for (const set of designPage.sets) {
          if (look === 'clinic' && !shared.has(`${designPage.page}/${set.name}`)) continue;
          for (const [variant] of set.variants) {
            const key = `${section}/${kebab(set.name)}/${kebab(variant)}`;
            if ((await page.getByTestId(`specimen-${key}`).count()) !== 1) missing.push(key);
          }
        }
        expect(missing, 'variants without their specimen').toEqual([]);
        expect(await galleryLooksWrong(page, language, look), 'at 1440').toEqual([]);
        await page.setViewportSize(NARROW);
        expect(await galleryLooksWrong(page, language, look), 'at 390').toEqual([]);
      });
    }
  }
}

/** The patterns and the icons: each composed of the parts above, drawn whole in both languages. */
for (const section of GALLERY_SECTIONS.filter((one) => one.kind === 'pattern' || one.kind === 'icons')) {
  for (const language of ['ar', 'en'] as Language[]) {
    test(`${section.title} — ${language}: drawn, nothing wrong`, async ({ page }) => {
      await page.setViewportSize(WIDE);
      await openSection(page, section.id, language, 'ops');
      const drawn = page.locator(`[data-testid="section-${section.id}"] [data-testid^="${section.kind === 'icons' ? 'icon-' : 'specimen-'}"]`);
      expect(await drawn.count(), 'what the section draws').toBeGreaterThan(0);
      expect(await galleryLooksWrong(page, language, 'ops'), 'at 1440').toEqual([]);
      await page.setViewportSize(NARROW);
      expect(await galleryLooksWrong(page, language, 'ops'), 'at 390').toEqual([]);
    });
  }
}
