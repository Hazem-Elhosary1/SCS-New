import { Page } from '@playwright/test';
import { opsLooksWrong } from '../ops-platform';
import inventory from '../../projects/ops/src/app/gallery/design-inventory.json';

/**
 * The design components' gallery for the browser tests (spec 095, `contracts/gallery.md`). It needs
 * no platform: the gallery calls nothing.
 */
export type Language = 'ar' | 'en';
export type Look = 'ops' | 'clinic';

export interface InventorySet {
  name: string;
  en: string;
  ar: string;
  props: string[];
  variants: [string, string, string][];
}
export interface InventoryPage {
  page: string;
  node: string;
  sets: InventorySet[];
}
export const INVENTORY = inventory as unknown as { pages: InventoryPage[] };

export const kebab = (text: string): string =>
  text
    .toLowerCase()
    .replace(/&/g, 'and')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

/** The operators console under `ng serve` (playwright.config.ts). */
export const OPS_APP = 'http://localhost:4203';

export const WIDE = { width: 1440, height: 900 };
export const NARROW = { width: 390, height: 844 };

/** Opens one section of the gallery in a language and a look. */
export async function openSection(page: Page, section: string, language: Language, look: Look = 'ops'): Promise<void> {
  await page.addInitScript((chosen) => localStorage.setItem('scs.language', chosen), language);
  await page.goto(`${OPS_APP}/dev/components/${section}`);
  await page.getByTestId(`section-${section}`).waitFor();
  // The section's content is loaded when it is shown; an unbuilt one says so and is left as it is.
  await page
    .waitForFunction((id) => !document.querySelector(`[data-testid="section-${id}"] p.empty`), section, { timeout: 5000 })
    .catch(() => undefined);
  if (look === 'clinic') {
    await page.getByTestId('gallery-look').getByRole('button').nth(1).click();
  }
  await page.evaluate(() => document.fonts.ready);
}

/**
 * What is certainly wrong (FR-027a): the console's layout checks, a digit that breaks the look's
 * rule (Western in the operators' look, OP-B-386; in the clinic's, Eastern in Arabic and Western in
 * English, CL-SH-064), and Arabic words on an English page. The design's own names
 * (`[data-scs-label]`) and a language's own name (`[lang]`) are skipped.
 */
export async function galleryLooksWrong(page: Page, language: Language, look: Look): Promise<string[]> {
  const wrong = await opsLooksWrong(page);
  const text = await page.evaluate(() => {
    const parts: string[] = [];
    const walker = document.createTreeWalker(document.querySelector('main') ?? document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const parent = node.parentElement;
      if (!parent || parent.closest('[data-scs-label], [lang], .cdk-visually-hidden, [hidden]')) continue;
      const value = (node.textContent ?? '').trim();
      if (value) parts.push(value);
    }
    return parts;
  });
  const eastern = /[٠-٩۰-۹]/;
  const western = /[0-9]/;
  const arabicLetters = /[؀-ۿ]/;
  for (const value of text) {
    if ((look === 'ops' || language === 'en') && eastern.test(value)) {
      wrong.push(`an Arabic-Indic digit in "${value.slice(0, 40)}"`);
    }
    if (look === 'clinic' && language === 'ar' && western.test(value) && arabicLetters.test(value)) {
      wrong.push(`a Western digit in Arabic text in the clinic look: "${value.slice(0, 40)}"`);
    }
    if (language === 'en' && arabicLetters.test(value)) {
      wrong.push(`Arabic on the English page: "${value.slice(0, 40)}"`);
    }
  }
  return wrong;
}
