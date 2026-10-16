import { Locator, Page, chromium, expect, test } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { EMAILS } from './emails-setup';

/**
 * Spec 094, how every email of the platform looks. The pages are the emails exactly as they
 * would be sent, exported from the server's catalogue with sample values before these tests
 * run. Each is opened in Arabic and in English and checked for what an eye would catch —
 * nothing outside the card, nothing cut off, nothing scrolling sideways — at the design's
 * width and at a phone's, in a dark theme, with all loading refused and with its styles taken
 * away; and for where the design puts things. With `SCS_SHOTS` set, each is also pictured, to
 * be laid beside its frame in the design file.
 */
interface Entry {
  slug: string;
  kind: string;
  audience: string;
  variant: string;
  language: 'ar' | 'en';
  subject: string;
  sender: string;
  hasButton: boolean;
  hasCode: boolean;
}

const catalogue: Entry[] = existsSync(join(EMAILS, 'catalogue.json'))
  ? (JSON.parse(readFileSync(join(EMAILS, 'catalogue.json'), 'utf8').replace(/^﻿/, '')) as Entry[])
  : [];

const WIDE = { width: 680, height: 900 };
const WIDTHS = [320, 360] as const;

const pageOf = (slug: string, form: 'html' | 'text.html' = 'html') => pathToFileURL(join(EMAILS, `${slug}.${form}`)).href;
const part = (page: Page, name: string): Locator => page.locator(`[data-part="${name}"]`);
const box = async (locator: Locator) => (await locator.boundingBox())!;
const middle = async (locator: Locator) => {
  const { x, width } = await box(locator);
  return x + width / 2;
};

/**
 * What a look at the email would catch: the page does not scroll sideways; every piece of text
 * is whole inside the card; nothing is cut off by its own box. Returns what is wrong.
 */
async function emailLooksWrong(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const wrong: string[] = [];
    const root = document.documentElement;
    if (root.scrollWidth > root.clientWidth + 1) {
      wrong.push(`the page scrolls sideways (${root.scrollWidth} > ${root.clientWidth})`);
    }

    const card = document.querySelector('[data-part="card"]');
    if (!card) {
      return ['there is no card'];
    }
    const frame = card.getBoundingClientRect();
    if (frame.left < -1 || frame.right > root.clientWidth + 1) {
      wrong.push(`the card is outside the window (${Math.round(frame.left)}..${Math.round(frame.right)} in 0..${root.clientWidth})`);
    }

    for (const element of card.querySelectorAll('p, h1, a, span, td[data-part], td.e-title, td.e-body, td.e-note')) {
      const rect = element.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) {
        continue;
      }
      const name = `${element.getAttribute('data-part') ?? element.tagName.toLowerCase()} "${(element.textContent ?? '').trim().slice(0, 28)}"`;
      if (rect.left < frame.left - 1 || rect.right > frame.right + 1) {
        wrong.push(`${name} is outside the card (${Math.round(rect.left)}..${Math.round(rect.right)} in ${Math.round(frame.left)}..${Math.round(frame.right)})`);
      }
      if (element.scrollWidth > element.clientWidth + 1 && getComputedStyle(element).display !== 'inline') {
        wrong.push(`${name} is wider than its box (${element.scrollWidth} > ${element.clientWidth})`);
      }
    }
    return wrong;
  });
}

async function picture(page: Page, name: string): Promise<void> {
  const folder = process.env['SCS_SHOTS'];
  if (folder) {
    await page.screenshot({ path: `${folder}/email-${name}.png`, fullPage: true });
  }
}

/** The contrast of each piece of text with the ground actually drawn behind it. */
async function faintWords(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const channel = (value: number) => {
      const v = value / 255;
      return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
    };
    const parse = (colour: string) => (colour.match(/[\d.]+/g) ?? []).map(Number);
    const luminance = ([r, g, b]: number[]) => 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
    const groundOf = (element: Element): number[] => {
      for (let at: Element | null = element; at; at = at.parentElement) {
        const [r, g, b, a = 1] = parse(getComputedStyle(at).backgroundColor);
        if (a > 0.99) {
          return [r, g, b];
        }
      }
      return [255, 255, 255];
    };

    const faint: string[] = [];
    for (const element of document.querySelectorAll('[data-part="card"] *')) {
      const own = [...element.childNodes].some((node) => node.nodeType === Node.TEXT_NODE && (node.textContent ?? '').trim().length > 1);
      if (!own || element.getBoundingClientRect().width === 0) {
        continue;
      }
      const text = luminance(parse(getComputedStyle(element).color));
      const ground = luminance(groundOf(element));
      const contrast = (Math.max(text, ground) + 0.05) / (Math.min(text, ground) + 0.05);
      if (contrast < 4.5) {
        faint.push(`"${(element.textContent ?? '').trim().slice(0, 30)}" is ${contrast.toFixed(2)} to 1`);
      }
    }
    return faint;
  });
}

test('the catalogue was exported and holds every email in both languages', () => {
  expect(catalogue.length, 'the export ran in the setup').toBeGreaterThan(0);
  const kinds = new Set(catalogue.map((entry) => `${entry.audience}/${entry.kind}/${entry.variant}`));
  for (const kind of kinds) {
    const languages = catalogue.filter((entry) => `${entry.audience}/${entry.kind}/${entry.variant}` === kind).map((entry) => entry.language);
    expect(languages.sort(), kind).toEqual(['ar', 'en']);
  }
});

for (const entry of catalogue) {
  test.describe(`${entry.slug}`, () => {
    test('is whole at the design\'s width, in its language and direction', async ({ page }) => {
      await page.setViewportSize(WIDE);
      await page.goto(pageOf(entry.slug));

      await expect(page.locator('html')).toHaveAttribute('lang', entry.language);
      await expect(page.locator('html')).toHaveAttribute('dir', entry.language === 'ar' ? 'rtl' : 'ltr');
      expect(await emailLooksWrong(page), 'at 680').toEqual([]);
      expect(await faintWords(page), 'light theme').toEqual([]);

      // One family: the card of the design, the mark above the first rule, a title, a footer.
      const card = await box(part(page, 'card'));
      expect(Math.abs(card.width - 600)).toBeLessThanOrEqual(1);
      expect(Math.abs(card.x + card.width / 2 - WIDE.width / 2)).toBeLessThanOrEqual(1);
      const centre = card.x + card.width / 2;
      for (const name of ['mark', 'eyebrow', 'title', 'platform', 'reason']) {
        expect(Math.abs((await middle(part(page, name))) - centre), name).toBeLessThanOrEqual(1.5);
      }
      const mark = await box(part(page, 'mark'));
      expect([Math.round(mark.width), Math.round(mark.height)]).toEqual([36, 36]);
      await expect(page.locator('h1')).toHaveCount(1);
      await expect(part(page, 'rule')).toHaveCount(2);

      // At most one button; a code's email has none; and the button is the design's.
      await expect(part(page, 'button')).toHaveCount(entry.hasButton ? 1 : 0);
      await expect(part(page, 'link')).toHaveCount(entry.hasButton ? 1 : 0);
      if (entry.hasCode) {
        expect(entry.hasButton).toBe(false);
      }

      // The other language's letters are not in this one's words (addresses and names apart).
      const words = await part(page, 'title').innerText();
      if (entry.language === 'en') {
        expect(words).not.toMatch(/[؀-ۿ]/);
      }
      // Western digits in both languages.
      expect(await page.locator('body').innerText()).not.toMatch(/[٠-٩۰-۹]/);

      await picture(page, entry.slug);
    });

    for (const width of WIDTHS) {
      test(`is whole on a phone ${width} wide`, async ({ page }) => {
        await page.setViewportSize({ width, height: 640 });
        await page.goto(pageOf(entry.slug));
        expect(await emailLooksWrong(page), `at ${width}`).toEqual([]);

        // The card fills the width with its margin.
        const card = await box(part(page, 'card'));
        expect(card.x).toBeGreaterThanOrEqual(8);
        expect(card.x + card.width).toBeLessThanOrEqual(width - 8);

        if (entry.hasCode && width === 360) {
          // The code is the largest text of the message and is inside the first screen (SC-006).
          const code = await box(part(page, 'code'));
          expect(code.y + code.height).toBeLessThanOrEqual(640);
          const sizes = await page.evaluate(() =>
            [...document.querySelectorAll('[data-part="card"] *')]
              .filter((element) => (element.textContent ?? '').trim().length > 0 && element.children.length === 0)
              .map((element) => parseFloat(getComputedStyle(element).fontSize)),
          );
          const codeSize = await part(page, 'code').evaluate((element) => parseFloat(getComputedStyle(element).fontSize));
          expect(codeSize).toBe(Math.max(...sizes));
        }
        await picture(page, `${entry.slug}-${width}`);
      });
    }

    test('reads in a dark theme', async ({ page }) => {
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.setViewportSize(WIDE);
      await page.goto(pageOf(entry.slug));

      expect(await faintWords(page), 'dark theme').toEqual([]);
      // The card did turn dark: this is the email's own dark rule, not a light page.
      const ground = await part(page, 'card').evaluate((card) => getComputedStyle(card).backgroundColor);
      expect(ground).not.toBe('rgb(255, 255, 255)');
      expect(await emailLooksWrong(page)).toEqual([]);
      await picture(page, `${entry.slug}-dark`);
    });

    test('is complete with nothing loaded, without its style block, and with no styles at all', async ({ page }) => {
      const asked: string[] = [];
      await page.route('**/*', async (route) => {
        const url = route.request().url();
        if (url.startsWith('file:') && url.endsWith(`${entry.slug}.html`)) {
          await route.continue();
        } else {
          asked.push(url);
          await route.abort();
        }
      });
      await page.setViewportSize(WIDE);
      await page.goto(pageOf(entry.slug));
      await page.waitForLoadState('networkidle');
      expect(asked, 'the email asks for nothing').toEqual([]);

      // No picture, no fetched font, no stylesheet from elsewhere.
      expect(await page.locator('img, link, script, iframe, video, object').count()).toBe(0);
      const wordsBefore = await part(page, 'card').innerText();

      // A program that drops the style block: the email is still right.
      await page.evaluate(() => document.querySelectorAll('style').forEach((style) => style.remove()));
      expect(await emailLooksWrong(page), 'without the style block').toEqual([]);
      expect(await faintWords(page), 'without the style block').toEqual([]);
      if (entry.hasButton) {
        const ground = await part(page, 'button').evaluate((cell) => getComputedStyle(cell).backgroundColor);
        expect(ground).toBe('rgb(124, 58, 237)');
      }

      // A program that drops every style: the words are all there, in order, and the link works.
      await page.evaluate(() => document.querySelectorAll('[style]').forEach((element) => element.removeAttribute('style')));
      const squeeze = (words: string) => words.replace(/\s+/g, ' ').trim();
      expect(squeeze(await part(page, 'card').innerText())).toBe(squeeze(wordsBefore));
      if (entry.hasButton) {
        await expect(part(page, 'button').locator('a')).toHaveAttribute('href', /^https?:\/\//);
      }
    });

    test('its text-only form holds the same words in the same order', async ({ page }) => {
      await page.setViewportSize(WIDE);
      await page.goto(pageOf(entry.slug));
      // The words that only introduce the written-out link belong to the laid-out form: in the
      // text-only form the button's label is followed by the address on its own line.
      await part(page, 'link').evaluateAll((lines) =>
        lines.forEach((line) => line.childNodes.forEach((node) => node.nodeType === Node.TEXT_NODE && node.remove())),
      );
      const words = (all: string) => all.split(/[\s·:—]+/).filter((word) => word.length > 0);
      const laidOut = words(await part(page, 'card').innerText());

      await page.goto(pageOf(entry.slug, 'text.html'));
      const text = await part(page, 'text').innerText();
      expect(text).not.toMatch(/<\/?[a-z][^>]*>|&[a-z]+;/i);

      // Every word of the laid-out email is in the text-only form, in the same order. The mark
      // is the one thing left out; the button's address is written once there, not twice.
      const inText = words(text);
      let from = 0;
      const missing: string[] = [];
      for (const [index, word] of laidOut.entries()) {
        if (index === 0 && word === 'S') {
          continue;
        }
        const at = inText.indexOf(word, from);
        if (at < 0) {
          // The address under the button repeats the one the text form has already given.
          if (!/^(https?|\/\/)/.test(word) && !inText.slice(0, from).includes(word)) {
            missing.push(word);
          }
        } else {
          from = at + 1;
        }
      }
      expect(missing, 'words of the laid-out form that the text-only form lacks or misorders').toEqual([]);
    });
  });
}

/** The tops of the named parts, in the order they are on the page. */
async function order(page: Page, names: string[]): Promise<string[]> {
  const found: { name: string; y: number }[] = [];
  for (const name of names) {
    const all = part(page, name);
    for (let index = 0; index < (await all.count()); index++) {
      found.push({ name, y: (await box(all.nth(index))).y });
    }
  }
  return found.sort((a, b) => a.y - b.y).map((entry) => entry.name);
}

const BODY = ['eyebrow', 'title', 'paragraph', 'code', 'facts', 'attention', 'button', 'link', 'note'];
const drawn = (kind: string, audience: string) => catalogue.filter((entry) => entry.kind === kind && entry.audience === audience);

test.describe('where the design puts things', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize(WIDE);
  });

  for (const entry of drawn('Shell', 'sample')) {
    test(`the shell, ${entry.language} (frames 447:3, 451:2)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));

      expect(await order(page, [...BODY, 'mark', 'rule', 'platform', 'address', 'support', 'reason'])).toEqual([
        'mark', 'rule', 'eyebrow', 'title', 'paragraph', 'button', 'link', 'rule', 'platform', 'address', 'support', 'reason',
      ]);

      const button = await box(part(page, 'button'));
      expect([Math.round(button.width), Math.round(button.height)]).toEqual([200, 48]);
      const centre = WIDE.width / 2;
      for (const name of ['paragraph', 'button', 'link', 'address', 'support']) {
        expect(Math.abs((await middle(part(page, name))) - centre), name).toBeLessThanOrEqual(1.5);
      }

      // The support line reads email then phone, left to right, in both languages.
      const email = await box(part(page, 'support').locator('a'));
      const phone = await box(part(page, 'phone'));
      expect(email.x + email.width).toBeLessThan(phone.x);

      // The written-out address is the button's, whole.
      const address = await part(page, 'button').locator('a').getAttribute('href');
      await expect(part(page, 'link').locator('a')).toHaveText(address!);
      await expect(part(page, 'link').locator('a')).toHaveAttribute('href', address!);
    });
  }

  for (const entry of catalogue.filter((each) => each.hasCode)) {
    test(`a code, ${entry.slug} (frames 2227:71, 2227:91)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));
      expect(await order(page, BODY)).toEqual(['eyebrow', 'title', 'paragraph', 'code', 'note', 'note', 'note']);

      // Six digits in one run, left to right, in the tinted box, in the middle.
      const code = part(page, 'code');
      await expect(code).toHaveText(/^\d{6}$/);
      expect(await code.evaluate((cell) => getComputedStyle(cell).direction)).toBe('ltr');
      expect(await code.evaluate((cell) => getComputedStyle(cell).backgroundColor)).toBe('rgb(241, 237, 254)');
      expect(Math.abs((await middle(code)) - WIDE.width / 2)).toBeLessThanOrEqual(1.5);
      const size = await box(code);
      expect([Math.round(size.width), Math.round(size.height)]).toEqual([300, 58]);
    });
  }

  for (const entry of [...drawn('PasswordReset', 'clinic'), ...drawn('Invitation', 'clinic'), ...drawn('ResetLink', 'operators'), ...drawn('FirstPassword', 'operators')]) {
    test(`a link, ${entry.slug} (frames 2227:111 to 2227:216)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));
      expect(await order(page, BODY)).toEqual(['eyebrow', 'title', 'paragraph', 'button', 'link', 'note']);
      const button = await box(part(page, 'button'));
      expect(button.height).toBe(48);
      expect(button.width).toBeGreaterThanOrEqual(200);
    });
  }

  for (const entry of drawn('Invoice', 'sample')) {
    test(`the invoice, ${entry.language} (frames 2227:237, 2227:268)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));
      expect(await order(page, BODY)).toEqual(['eyebrow', 'title', 'paragraph', 'facts', 'button', 'link', 'note']);

      const rows = part(page, 'fact');
      await expect(rows).toHaveCount(3);
      await expect(rows.nth(2)).toHaveAttribute('data-total', 'true');
      expect(await rows.nth(2).locator('td').last().evaluate((cell) => Number(getComputedStyle(cell).fontWeight))).toBe(700);

      // The name at the start and the value at the far edge, in the email's direction.
      const facts = await box(part(page, 'facts'));
      const name = await box(rows.nth(0).locator('td').first());
      const value = await box(rows.nth(0).locator('td').last());
      if (entry.language === 'en') {
        expect(name.x).toBeLessThan(value.x);
        expect(facts.x + facts.width - (value.x + value.width)).toBeLessThanOrEqual(24);
      } else {
        expect(name.x).toBeGreaterThan(value.x);
        expect(value.x - facts.x).toBeLessThanOrEqual(24);
      }
    });
  }

  for (const entry of drawn('SubscriptionNotice', 'sample')) {
    test(`the subscription notice, ${entry.language} (frames 2593:71, 2593:183)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));
      expect(await order(page, BODY)).toEqual(['eyebrow', 'title', 'paragraph', 'facts', 'note']);
      await expect(part(page, 'fact')).toHaveCount(3);
      await expect(page.locator('[data-part="fact"][data-total="true"]')).toHaveCount(1);

      const mark = part(page, 'mark-bad');
      await expect(mark).toHaveCount(1);
      expect(await mark.evaluate((pill) => getComputedStyle(pill).backgroundColor)).toBe('rgb(251, 233, 233)');
      const total = await box(page.locator('[data-part="fact"][data-total="true"]'));
      expect((await box(mark)).y).toBeGreaterThan(total.y);
    });
  }

  for (const entry of drawn('TicketNotification', 'sample')) {
    test(`the ticket notification, ${entry.language} (frames 449:2, 455:2)`, async ({ page }) => {
      await page.goto(pageOf(entry.slug));
      expect(await order(page, BODY)).toEqual(['eyebrow', 'title', 'paragraph', 'attention', 'button', 'link']);

      const attention = part(page, 'attention');
      expect(await attention.evaluate((boxed) => getComputedStyle(boxed).backgroundColor)).toBe('rgb(254, 241, 225)');
      await expect(attention.locator('[data-part="mark-warning"]')).toHaveCount(1);
      await expect(attention.locator('p')).toHaveCount(3);
      expect(Math.round((await box(attention)).width)).toBe(400);
    });
  }
});

test('in Edge, the shell and a code are whole at the design\'s width', async () => {
  // The owner reads in Edge; the other tests run in Chromium. Skipped where Edge is not installed.
  const edge = await chromium.launch({ channel: 'msedge' }).catch(() => null);
  test.skip(edge === null, 'Edge is not installed here');
  const page = await edge!.newPage({ viewport: WIDE });
  try {
    const wanted = catalogue.filter((entry) => entry.kind === 'Shell' || entry.hasCode).slice(0, 6);
    expect(wanted.length).toBeGreaterThan(0);
    for (const entry of wanted) {
      await page.goto(pageOf(entry.slug));
      expect(await emailLooksWrong(page), entry.slug).toEqual([]);
      const card = await box(part(page, 'card'));
      expect(Math.abs(card.width - 600), entry.slug).toBeLessThanOrEqual(1);
    }
  } finally {
    await edge!.close();
  }
});
