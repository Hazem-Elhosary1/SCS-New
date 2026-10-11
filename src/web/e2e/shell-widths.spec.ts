import { Page, expect, test } from '@playwright/test';
import { CLINIC, sampleSources, staffPlatform, useEnglish } from './staff-platform';

/**
 * Spec 006, FR-043 and SC-010: the frame is complete from 1280 wide; between 1024 and 1280 a
 * person who never chose gets the folded bar; nothing overlaps or is cut at 1024, 1280, 1440
 * and 1920; under 1024 the page scrolls sideways rather than breaking.
 */
const LONG = { nameAr: 'عيادة ' + 'النور التخصصية '.repeat(8), nameEn: 'Al Noor Specialised Clinic '.repeat(6), state: 'active' };

async function open(page: Page, width: number, path = '', start: Parameters<typeof staffPlatform>[1] = {}) {
  await sampleSources(page, {
    subscription: { state: 'expiring', days: 5 },
    branches: [
      { id: 'b-1', nameAr: 'فرع المعادي', nameEn: 'Maadi branch' },
      { id: 'b-2', nameAr: 'فرع مدينة نصر', nameEn: 'Nasr City branch' },
    ],
  });
  const platform = await staffPlatform(page, { signedIn: true, ...start });
  await page.setViewportSize({ width, height: 800 });
  await page.goto(`${CLINIC}/${path}`);
  await expect(page.getByTestId('shell')).toBeVisible();
  return platform;
}

/** The parts of the top bar, none over another and all inside the window. */
async function topBarIsWhole(page: Page, width: number): Promise<void> {
  const boxes = await Promise.all(
    ['signed-in-clinic', 'branch-switch', 'bell', 'account-chip'].map(async (id) => ({
      id,
      box: (await page.getByTestId(id).boundingBox())!,
    })),
  );
  boxes.push({ id: 'language', box: (await page.getByTestId('top-bar').locator('scs-language-toggle').boundingBox())! });
  for (const { id, box } of boxes) {
    expect(box.x, id).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width, id).toBeLessThanOrEqual(width);
  }
  const sorted = [...boxes].sort((a, b) => a.box.x - b.box.x);
  for (let index = 1; index < sorted.length; index++) {
    expect(sorted[index].box.x, `${sorted[index - 1].id} | ${sorted[index].id}`).toBeGreaterThanOrEqual(
      sorted[index - 1].box.x + sorted[index - 1].box.width - 0.5,
    );
  }
}

for (const language of ['ar', 'en'] as const) {
  for (const width of [1024, 1280, 1440, 1920]) {
    test(`${width} wide in ${language}: nothing overlaps, nothing is cut, nothing scrolls sideways`, async ({
      page,
    }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
      await open(page, width, 'dev/sample-list', { preferences: { language: null, sidebarFolded: null, dismissedBanner: null } });

      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
      await topBarIsWhole(page, width);

      // The side bar and the content share the width exactly.
      const [bar, main] = await Promise.all([
        page.getByTestId('side-bar').boundingBox(),
        page.locator('main').boundingBox(),
      ]);
      expect(Math.round(bar!.width + main!.width)).toBe(width);
      expect(bar!.width).toBe(width < 1280 ? 72 : 240);

      // The table and its footer are inside the content.
      for (const selector of ['table', 'scs-table-footer', 'scs-list-toolbar', '[data-testid="subscription-banner"]']) {
        const box = (await page.locator(selector).first().boundingBox())!;
        expect(box.x, selector).toBeGreaterThanOrEqual(0);
        expect(box.x + box.width, selector).toBeLessThanOrEqual(width);
      }
      expect(await page.locator('main').evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    });
  }
}

test('between 1024 and 1280 a person who never chose gets the folded bar, and a saved choice wins', async ({
  page,
}) => {
  const platform = await open(page, 1100);
  const bar = page.getByTestId('side-bar');
  await expect(bar).toHaveAttribute('data-folded', 'true');

  // Widening unfolds it; nothing was saved for them.
  await page.setViewportSize({ width: 1400, height: 800 });
  await expect(bar).toHaveAttribute('data-folded', 'false');
  expect(platform.preferencesSent.some((sent) => 'sidebarFolded' in sent)).toBe(false);

  // Their own choice holds at any width.
  await page.setViewportSize({ width: 1100, height: 800 });
  await page.getByTestId('side-bar-fold').click();
  await expect(bar).toHaveAttribute('data-folded', 'false');
  await page.setViewportSize({ width: 1400, height: 800 });
  await page.setViewportSize({ width: 1100, height: 800 });
  await expect(bar).toHaveAttribute('data-folded', 'false');
});

test('under 1024 the frame keeps its shape and the page scrolls sideways', async ({ page }) => {
  await open(page, 800);

  expect((await page.getByTestId('shell').boundingBox())!.width).toBe(1024);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(1024);
});

test('a very long clinic name is cut with an ellipsis and read in full on hover', async ({ page }) => {
  const platform = await staffPlatform(page, { signedIn: true });
  platform.sessionAnswer = {
    status: 200,
    json: {
      accountId: 'a-1',
      email: 'a-very-long-address-of-a-person-at-the-clinic@noor-specialised-clinic.example',
      clinic: LONG,
      expiresAt: '2030-01-01T12:00:00Z',
      mustChangePassword: false,
      role: { id: 'r', name: 'دور باسم طويل جدًّا '.repeat(4), readyKey: null },
      permissions: [],
      preferences: { language: 'ar', sidebarFolded: null, dismissedBanner: null },
    },
  };
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('shell')).toBeVisible();

  const name = page.getByTestId('signed-in-clinic');
  await expect(name).toHaveAttribute('title', LONG.nameAr);
  expect(await name.evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(true);
  expect((await name.boundingBox())!.width).toBeLessThanOrEqual(320);
  expect(await page.getByTestId('signed-in-email').evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(true);
  expect((await page.getByTestId('top-bar').boundingBox())!.height).toBe(64);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(1280);
});
