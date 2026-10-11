import { Page, expect, test } from '@playwright/test';
import { CLINIC, sampleSources, staffPlatform } from './staff-platform';

/**
 * Spec 006, FR-040 and SC-009: every control of the frame with a keyboard alone — reached by
 * Tab in reading order, opened with Enter or Space, closed with Escape with focus returned to
 * what opened it, and named for assistive technology.
 */
const BRANCHES = [
  { id: 'b-1', nameAr: 'فرع المعادي', nameEn: 'Maadi branch' },
  { id: 'b-2', nameAr: 'فرع مدينة نصر', nameEn: 'Nasr City branch' },
];

async function inside(page: Page, path = ''): Promise<void> {
  await sampleSources(page, { branches: BRANCHES });
  await staffPlatform(page, { signedIn: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/${path}`);
  await expect(page.getByTestId('shell')).toBeVisible();
}

const focused = (page: Page) =>
  page.evaluate(() => document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName);

test('Tab walks the top bar in reading order, then the side bar, then the page', async ({ page }) => {
  await inside(page);
  const seen: (string | undefined)[] = [];

  for (let step = 0; step < 15; step++) {
    await page.keyboard.press('Tab');
    seen.push(await focused(page));
  }

  expect(seen.slice(0, 5)).toEqual([
    'branch-switch',
    'language-switch',
    'bell',
    'account-chip',
    'side-bar-fold',
  ]);
  expect(seen.slice(5, 15)).toEqual([
    'nav-home',
    'nav-bookings',
    'nav-patients',
    'nav-invoices',
    'nav-inbox',
    'nav-tickets',
    'nav-reports',
    'nav-administration',
    'nav-data',
    'nav-settings',
  ]);
});

test('every control shows where focus is', async ({ page }) => {
  await inside(page);

  for (const id of ['branch-switch', 'bell', 'account-chip', 'nav-bookings', 'nav-reports', 'side-bar-fold']) {
    const control = page.getByTestId(id);
    await control.focus();
    await page.keyboard.press('Shift+Tab');
    await page.keyboard.press('Tab');
    await expect(control, id).toBeFocused();
    const outline = await control.evaluate((element) => getComputedStyle(element).outlineStyle);
    expect(outline, id).not.toBe('none');
  }
});

test('the account menu: Enter opens it, arrows move, Escape closes and returns focus, Enter chooses', async ({
  page,
}) => {
  await inside(page);
  const chip = page.getByTestId('account-chip');

  await chip.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('account-menu')).toBeVisible();
  await expect(page.getByTestId('account-menu')).toHaveAttribute('role', 'menu');
  await expect(page.getByTestId('menu-subscription')).toBeFocused();

  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId('sign-out')).toBeFocused();
  await expect(page.getByTestId('sign-out')).toHaveAttribute('role', 'menuitem');

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('account-menu')).toHaveCount(0);
  await expect(chip).toBeFocused();

  await page.keyboard.press('Space');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(`${CLINIC}/account/subscription`);
});

test('the branch menu and the bell open and close the same way', async ({ page }) => {
  await inside(page);

  const branch = page.getByTestId('branch-switch');
  await branch.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('branch-menu')).toBeVisible();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('branch-menu')).toHaveCount(0);
  await expect(branch).toHaveText('فرع المعادي');

  const bell = page.getByTestId('bell');
  await expect(bell).toHaveAttribute('aria-label', 'الإشعارات');
  await bell.focus();
  await page.keyboard.press('Enter');
  const panel = page.getByTestId('notification-panel');
  await expect(panel).toBeVisible();
  await expect(panel).toHaveAttribute('role', 'dialog');
  await expect(panel).toHaveAttribute('aria-label', 'الإشعارات');

  await page.keyboard.press('Escape');
  await expect(panel).toHaveCount(0);
  await expect(bell).toBeFocused();
});

test('the side bar: Enter follows an item, opens a parent, and folds the bar', async ({ page }) => {
  await inside(page);

  await expect(page.getByTestId('side-bar').locator('nav')).toHaveAttribute('aria-label', 'أقسام لوحة العيادة');

  await page.getByTestId('nav-data').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('nav-data')).toHaveAttribute('aria-expanded', 'true');

  await page.keyboard.press('Tab');
  await expect(page.getByTestId('nav-data.doctors')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(`${CLINIC}/data/doctors`);
  await expect(page.getByTestId('nav-data.doctors')).toHaveAttribute('aria-current', 'page');

  const fold = page.getByTestId('side-bar-fold');
  await expect(fold).toHaveAttribute('aria-label', 'طيّ القائمة');
  await fold.focus();
  await page.keyboard.press('Space');
  await expect(page.getByTestId('side-bar')).toHaveAttribute('data-folded', 'true');
  await expect(fold).toHaveAttribute('aria-label', 'بسط القائمة');

  // Folded, an item is still named: its words are hidden from sight, and a tooltip shows on focus.
  await page.getByTestId('nav-bookings').focus();
  await expect(page.locator('.scs-tooltip')).toHaveText('الحجوزات');
});

test('the language pill is a named group whose other half is a button', async ({ page }) => {
  await inside(page);
  const pill = page.getByTestId('top-bar').locator('scs-language-toggle');

  await expect(pill).toHaveAttribute('role', 'group');
  await expect(pill).toHaveAttribute('aria-label', 'تغيير اللغة');

  await pill.getByTestId('language-switch').focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
});

test('the sample list: the filter panel, the row menu and the confirmation by keyboard', async ({ page }) => {
  await inside(page, 'dev/sample-list');

  const filter = page.getByTestId('list-filter');
  await filter.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('filter-popover')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('filter-popover')).toHaveCount(0);

  const menu = page.getByTestId('sample-row').first().getByTestId('row-menu');
  await menu.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('row-menu-items')).toBeVisible();
  // «⋯» did not open the record.
  await expect(page.getByTestId('opened-record')).toHaveCount(0);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');

  await expect(page.getByTestId('dialog-title')).toHaveText('حذف السجل');
  await page.keyboard.press('Escape');
  await expect(page.locator('dialog[open]')).toHaveCount(0);
  await expect(page.getByTestId('sample-row')).toHaveCount(10);
});
