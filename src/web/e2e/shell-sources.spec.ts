import { Page, expect, test } from '@playwright/test';
import { CLINIC, sampleSources, staffPlatform } from './staff-platform';

/**
 * Spec 006, User Story 7: the parts of the top bar whose data belongs to a later spec — the
 * bell and the branch switch — in the state they have now ("nothing yet") and in every state
 * they will have, given by sample sources.
 */
const NOW = Date.now();
const ITEMS = [
  { id: 'n-1', kind: 'invoice', title: 'فاتورة مستحقة', text: 'فاتورة المريض أحمد سالم تجاوزت موعد السداد.', at: new Date(NOW - 3_600_000).toISOString(), read: true },
  { id: 'n-2', kind: 'ticket', title: 'ردّ جديد على تذكرتك', text: 'ردّ فريق الدعم على تذكرة «تعذّر طباعة الفاتورة». '.repeat(6), at: new Date(NOW - 60_000).toISOString(), read: false },
  { id: 'n-3', kind: 'subscription', title: 'اشتراك العيادة', text: 'ينتهي الاشتراك خلال سبعة أيام.', at: new Date(NOW - 600_000).toISOString(), read: false },
];
const BRANCHES = [
  { id: 'b-1', nameAr: 'فرع المعادي', nameEn: 'Maadi branch' },
  { id: 'b-2', nameAr: 'فرع مدينة نصر', nameEn: 'Nasr City branch' },
];

async function inside(page: Page, sample?: Record<string, unknown>): Promise<void> {
  if (sample !== undefined) {
    await sampleSources(page, sample);
  }
  await staffPlatform(page, { signedIn: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('shell')).toBeVisible();
}

const calls = (page: Page) =>
  page.evaluate(() => (window as unknown as { __scsSample: { calls: string[] } }).__scsSample.calls);

test.describe('the bell', () => {
  test('with nothing to show: no dot, and a panel that says so', async ({ page }) => {
    await inside(page);
    const bell = page.getByTestId('bell');

    await expect(page.getByTestId('bell-dot')).toHaveCount(0);
    await bell.click();

    const panel = page.getByTestId('notification-panel');
    await expect(panel).toBeVisible();
    await expect(bell).toHaveAttribute('aria-expanded', 'true');
    await expect(panel.locator('h2')).toHaveText('الإشعارات');
    await expect(panel.getByTestId('empty-title')).toHaveText('لا إشعارات جديدة');
    await expect(panel.locator('scs-empty-state .mark scs-icon')).toHaveAttribute('data-icon', 'bell');
    await expect(panel.getByTestId('empty-text')).toHaveText('سيظهر هنا ما يخصّك من تذاكر وفواتير واشتراك.');
    await expect(page.getByTestId('mark-all-read')).toHaveCount(0);
    await expect(panel.locator('a')).toHaveCount(0);

    // 360 wide, under the bell, at the far edge — the left in Arabic.
    const [opener, opened] = [(await bell.boundingBox())!, (await panel.boundingBox())!];
    expect(opened.width).toBe(360);
    expect(opened.y).toBeGreaterThanOrEqual(opener.y + opener.height);
    expect(Math.abs(opened.x - opener.x)).toBeLessThanOrEqual(1);

    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    await expect(bell).toBeFocused();
    await expect(bell).toHaveAttribute('aria-expanded', 'false');

    await bell.click();
    await expect(panel).toBeVisible();
    await page.mouse.click(900, 600);
    await expect(panel).toHaveCount(0);

    // The bell itself opens and closes it.
    await bell.click();
    await expect(panel).toBeVisible();
    await bell.click();
    await expect(panel).toHaveCount(0);
  });

  test('with items: newest first, unread tinted, a dot, and «تعليم الكل كمقروء»', async ({ page }) => {
    await inside(page, { notifications: ITEMS });

    await expect(page.getByTestId('bell-dot')).toBeVisible();
    await page.getByTestId('bell').click();

    const items = page.getByTestId('notification-item');
    await expect(items.locator('.item-title')).toHaveText([
      'ردّ جديد على تذكرتك',
      'اشتراك العيادة',
      'فاتورة مستحقة',
    ]);
    await expect(items.nth(0)).toHaveAttribute('data-read', 'false');
    await expect(items.nth(2)).toHaveAttribute('data-read', 'true');
    await expect(items.nth(0).locator('scs-icon')).toHaveAttribute('data-icon', 'ticket');
    await expect(items.nth(1).locator('scs-icon')).toHaveAttribute('data-icon', 'card');
    await expect(items.nth(2).locator('scs-icon')).toHaveAttribute('data-icon', 'receipt');
    await expect(items.nth(0).locator('.item-time')).not.toBeEmpty();

    // A long text takes two lines at most.
    const text = items.nth(0).locator('.item-text');
    const lines = await text.evaluate(
      (element) => element.getBoundingClientRect().height / parseFloat(getComputedStyle(element).lineHeight),
    );
    expect(lines).toBeLessThanOrEqual(2.1);

    await page.getByTestId('mark-all-read').click();

    expect(await calls(page)).toEqual(['markAllRead']);
    await expect(page.getByTestId('bell-dot')).toHaveCount(0);
    await expect(items.nth(0)).toHaveAttribute('data-read', 'true');
  });

  test('opening an item tells the source and closes the panel', async ({ page }) => {
    await inside(page, { notifications: ITEMS });

    await page.getByTestId('bell').click();
    await page.getByTestId('notification-item').nth(1).click();

    expect(await calls(page)).toEqual(['open n-3']);
    await expect(page.getByTestId('notification-panel')).toHaveCount(0);
  });

  test('many items scroll inside the panel, which stays on screen', async ({ page }) => {
    const many = Array.from({ length: 30 }, (_, index) => ({
      ...ITEMS[0],
      id: `m-${index}`,
      at: new Date(NOW - index * 60_000).toISOString(),
    }));
    await inside(page, { notifications: many });

    await page.getByTestId('bell').click();

    const box = (await page.getByTestId('notification-panel').boundingBox())!;
    expect(box.y + box.height).toBeLessThanOrEqual(900);
    const list = page.locator('.panel-list');
    expect(await list.evaluate((element) => element.scrollHeight > element.clientHeight)).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollHeight)).toBeLessThanOrEqual(900);
  });

  test('while loading it shows skeletons, and a failure offers «إعادة المحاولة» inside the panel', async ({
    page,
  }) => {
    await inside(page, { notificationsState: 'failed' });
    await page.getByTestId('bell').click();

    const panel = page.getByTestId('notification-panel');
    await expect(panel.getByTestId('empty-title')).toHaveText('تعذّر تحميل الإشعارات');
    await expect(page.getByTestId('mark-all-read')).toHaveCount(0);

    await page.getByTestId('notifications-retry').click();

    expect(await calls(page)).toEqual(['retry']);
    await expect(panel.getByTestId('empty-title')).toHaveText('لا إشعارات جديدة');
  });

  test('loading shows skeletons', async ({ page }) => {
    await inside(page, { notificationsState: 'loading' });
    await page.getByTestId('bell').click();

    await expect(page.getByTestId('notification-panel').locator('scs-skeleton')).toBeVisible();
  });

  test('opening the bell closes the account menu, and the other way round', async ({ page }) => {
    await inside(page);

    await page.getByTestId('account-chip').click();
    await expect(page.getByTestId('account-menu')).toBeVisible();

    await page.getByTestId('bell').click({ force: true });
    await expect(page.getByTestId('account-menu')).toHaveCount(0);
  });
});

test.describe('the branch switch', () => {
  test('is absent in a clinic with one branch — as every clinic is until the branches spec', async ({
    page,
  }) => {
    await inside(page);

    await expect(page.getByTestId('branch-switch')).toHaveCount(0);
  });

  test('with two branches: «كل الفروع», a menu under it, the active one marked, and a choice', async ({
    page,
  }) => {
    await inside(page, { branches: BRANCHES });
    const button = page.getByTestId('branch-switch');

    await expect(button).toHaveText('كل الفروع');
    await button.click();

    const menu = page.getByTestId('branch-menu');
    await expect(menu.locator('[scsmenuitem]')).toHaveText(['كل الفروع', 'فرع المعادي', 'فرع مدينة نصر']);
    await expect(menu.locator('[scsmenuitem]').nth(0)).toHaveAttribute('data-active', 'true');
    await expect(button).toHaveAttribute('aria-expanded', 'true');
    const [opener, opened] = [(await button.boundingBox())!, (await menu.boundingBox())!];
    expect(opened.width).toBe(240);
    expect(opened.y).toBeGreaterThanOrEqual(opener.y + opener.height);

    await menu.locator('[scsmenuitem]').nth(2).click();

    await expect(menu).toHaveCount(0);
    await expect(button).toHaveText('فرع مدينة نصر');
    expect(await calls(page)).toEqual(['choose b-2']);

    await button.click();
    await expect(page.getByTestId('branch-menu').locator('[scsmenuitem]').nth(2)).toHaveAttribute(
      'data-active',
      'true',
    );
  });

  test('a person given one branch of several sees its name and no choice', async ({ page }) => {
    await inside(page, { branches: [BRANCHES[0]], multiBranch: true });
    const button = page.getByTestId('branch-switch');

    await expect(button).toHaveText('فرع المعادي');
    await button.click({ force: true });
    await expect(page.getByTestId('branch-menu')).toHaveCount(0);
  });
});

test.describe('the inbox count and the kinds of clinic', () => {
  test('an unread count stands beside «الصندوق الوارد», in the page’s digits', async ({ page }) => {
    await inside(page, { unread: 12 });

    await expect(page.getByTestId('nav-inbox-count')).toHaveText('١٢');
  });

  test('a dental clinic has «معمل الأسنان», and a linked account has «ملف الطبيب»', async ({ page }) => {
    await inside(page, { dental: true, linkedToDoctor: true });

    await page.getByTestId('nav-dental-lab').click();
    await expect(page.getByTestId('page-title')).toHaveText('معمل الأسنان');

    await page.getByTestId('account-chip').click();
    await expect(page.getByTestId('account-menu').locator('[scsmenuitem]')).toHaveText([
      'الملف والاشتراك',
      'ملف الطبيب',
      'تسجيل الخروج',
    ]);
    await page.getByTestId('menu-doctor-profile').click();
    await expect(page.getByTestId('page-title')).toHaveText('ملف الطبيب');
  });
});
