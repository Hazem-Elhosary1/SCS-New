import { Page, expect, test } from '@playwright/test';
import {
  CLINIC,
  PlatformNotification,
  StaffPlatform,
  pictureFor,
  problem,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 008, User Stories 1 and 3: the bell with the person's own notifications from the
 * platform — the dot, the list, reading one and all, older ones, and what arrives while the
 * console is open.
 */
const NOW = new Date('2026-10-11T12:00:00Z');
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

const ago = (milliseconds: number) => new Date(NOW.getTime() - milliseconds).toISOString();

function notification(id: string, more: Partial<PlatformNotification> = {}): PlatformNotification {
  return {
    id,
    kind: 'ticket',
    event: 'support.ticket-opened',
    title: 'تذكرة دعم جديدة',
    text: `التذكرة ${id}`,
    at: ago(5 * MINUTE),
    read: false,
    link: null,
    ...more,
  };
}

const AR: PlatformNotification[] = [
  notification('n-4', { kind: 'warning', event: 'account.new-device', title: 'الدخول من جهاز جديد', text: 'متصفح Safari على macOS', at: ago(2 * HOUR) }),
  notification('n-3', { title: 'ردّ جديد على تذكرتك', text: 'التذكرة T-1046 · تعذّر طباعة الفاتورة', at: ago(7 * HOUR), link: 'roles' }),
  notification('n-2', { kind: 'invoice', event: 'billing.invoice-issued', title: 'فاتورة جديدة', text: 'فاتورة الاشتراك INV-0031', at: ago(26 * HOUR), read: true }),
  notification('n-1', { kind: 'subscription', event: 'subscription.renewed', title: 'تجديد الاشتراك', text: 'جُدِّد اشتراك العيادة', at: ago(35 * DAY), read: true }),
];

const EN: PlatformNotification[] = [
  { ...AR[0], title: 'Sign-in from a new device', text: 'Safari on macOS' },
  { ...AR[1], title: 'New reply to your ticket', text: 'Ticket T-1046 · Invoice would not print' },
  { ...AR[2], title: 'New invoice', text: 'Subscription invoice INV-0031' },
  { ...AR[3], title: 'Subscription renewed', text: 'The clinic’s subscription was renewed' },
];

async function inside(page: Page, notifications: PlatformNotification[]): Promise<StaffPlatform> {
  await page.clock.install({ time: NOW });
  const platform = await staffPlatform(page, { signedIn: true, notifications });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('shell')).toBeVisible();
  return platform;
}

const items = (page: Page) => page.getByTestId('notification-item');

test.describe('the bell, with notifications from the platform', () => {
  test('has its dot, and lists them newest first with the unread ones tinted', async ({ page }) => {
    const platform = await inside(page, AR);

    await expect(page.getByTestId('bell-dot')).toBeVisible();

    // The list is not asked for until the bell is opened.
    expect(platform.notificationCalls).toEqual(['GET /v1/notifications/state']);
    await page.getByTestId('bell').click();

    await expect(items(page).locator('.item-title')).toHaveText([
      'الدخول من جهاز جديد',
      'ردّ جديد على تذكرتك',
      'فاتورة جديدة',
      'تجديد الاشتراك',
    ]);
    await expect(items(page).locator('.item-text').first()).toHaveText('متصفح Safari على macOS');
    await expect(items(page).nth(0)).toHaveAttribute('data-read', 'false');
    await expect(items(page).nth(1)).toHaveAttribute('data-read', 'false');
    await expect(items(page).nth(2)).toHaveAttribute('data-read', 'true');
    await expect(items(page).nth(0).locator('scs-icon')).toHaveAttribute('data-icon', 'alert-triangle');
    await expect(items(page).nth(2).locator('scs-icon')).toHaveAttribute('data-icon', 'receipt');

    // Two hours ago, earlier today, yesterday, last month — as the frames write it.
    await expect(items(page).locator('.item-time')).toHaveText([
      'منذ ساعتين',
      'اليوم، ٨:٠٠ ص',
      'أمس',
      '٦ سبتمبر',
    ]);
    await pictureFor(page, 'notifications', 'ar', 'filled');
  });

  test('reads the same in English', async ({ page }) => {
    await useEnglish(page);
    await inside(page, EN);

    await page.getByTestId('bell').click();

    await expect(page.getByTestId('notification-panel').locator('h2')).toHaveText('Notifications');
    await expect(items(page).locator('.item-title')).toHaveText([
      'Sign-in from a new device',
      'New reply to your ticket',
      'New invoice',
      'Subscription renewed',
    ]);
    await expect(items(page).locator('.item-time')).toHaveText([
      '2 h ago',
      'Today, 8:00 AM',
      'Yesterday',
      '6 Sep',
    ]);
    await expect(page.getByTestId('mark-all-read')).toHaveText('Mark all read');
    await pictureFor(page, 'notifications', 'en', 'filled');
  });

  test('one that arrives while the console is open shows its dot without a reload', async ({ page }) => {
    const platform = await inside(page, []);
    await expect(page.getByTestId('bell-dot')).toHaveCount(0);

    platform.notifications = [notification('n-9', { at: NOW.toISOString() })];
    await page.clock.fastForward(30_000);

    await expect(page.getByTestId('bell-dot')).toBeVisible();

    // And into an open panel, in its place at the top.
    await page.getByTestId('bell').click();
    await expect(items(page)).toHaveCount(1);
    platform.notifications = [
      notification('n-10', { title: 'ردّ جديد على تذكرتك', at: new Date(NOW.getTime() + 20_000).toISOString() }),
      ...platform.notifications,
    ];
    await page.clock.fastForward(30_000);

    await expect(items(page).locator('.item-title')).toHaveText(['ردّ جديد على تذكرتك', 'تذكرة دعم جديدة']);
  });

  test('pressing one marks it read, closes the panel and opens what it is about', async ({ page }) => {
    const platform = await inside(page, AR);

    await page.getByTestId('bell').click();
    await items(page).nth(1).click();

    await expect(page.getByTestId('notification-panel')).toHaveCount(0);
    await expect(page).toHaveURL(`${CLINIC}/roles`);
    await expect.poll(() => platform.notificationCalls).toContain('POST /v1/notifications/n-3/read');

    // One is still unread: the dot stays.
    await expect(page.getByTestId('bell-dot')).toBeVisible();
    await page.getByTestId('bell').click();
    await expect(items(page).nth(1)).toHaveAttribute('data-read', 'true');
    await expect(items(page).nth(0)).toHaveAttribute('data-read', 'false');
  });

  test('one that leads nowhere is marked read and the person stays where they are', async ({ page }) => {
    const platform = await inside(page, AR);

    await page.getByTestId('bell').click();
    await items(page).nth(0).click();

    await expect(page.getByTestId('notification-panel')).toHaveCount(0);
    await expect(page).toHaveURL(`${CLINIC}/`);
    await expect.poll(() => platform.notificationCalls).toContain('POST /v1/notifications/n-4/read');
  });

  test('«تعليم الكل كمقروء» clears the dot at once, with no passing notice', async ({ page }) => {
    const platform = await inside(page, AR);

    await page.getByTestId('bell').click();
    await page.getByTestId('mark-all-read').click();

    await expect(page.getByTestId('bell-dot')).toHaveCount(0);
    await expect(items(page).nth(0)).toHaveAttribute('data-read', 'true');
    await expect(items(page).nth(1)).toHaveAttribute('data-read', 'true');
    await expect(page.getByTestId('toast')).toHaveCount(0);
    await expect.poll(() => platform.notificationCalls).toContain('POST /v1/notifications/read-all');

    // The panel stays open, and the platform agrees at its next turn.
    await expect(page.getByTestId('notification-panel')).toBeVisible();
    await page.clock.fastForward(30_000);
    await expect(page.getByTestId('bell-dot')).toHaveCount(0);
  });

  test('scrolling to the end of the list loads older ones', async ({ page }) => {
    const many = Array.from({ length: 45 }, (_, index) =>
      notification(`n-${45 - index}`, { text: `التذكرة T-${1000 + 45 - index}`, at: ago((index + 1) * MINUTE), read: index > 2 }),
    );
    const platform = await inside(page, many);

    await page.getByTestId('bell').click();
    await expect(items(page)).toHaveCount(20);

    const list = page.getByTestId('notification-list');
    await list.evaluate((element) => element.scrollTo(0, element.scrollHeight));
    await expect(items(page)).toHaveCount(40);
    await list.evaluate((element) => element.scrollTo(0, element.scrollHeight));
    await expect(items(page)).toHaveCount(45);
    await expect(items(page).last().locator('.item-text')).toHaveText('التذكرة T-1001');

    // Nothing older: the end of the list asks nothing more.
    const asked = platform.notificationCalls.filter((call) => call.includes('before=')).length;
    await list.evaluate((element) => element.scrollTo(0, 0));
    await list.evaluate((element) => element.scrollTo(0, element.scrollHeight));
    expect(asked).toBe(2);
    expect(platform.notificationCalls.filter((call) => call.includes('before=')).length).toBe(2);
  });

  for (const language of ['ar', 'en'] as const) {
    test(`with none, says so and offers nothing to mark (${language})`, async ({ page }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
      await inside(page, []);

      await expect(page.getByTestId('bell-dot')).toHaveCount(0);
      await page.getByTestId('bell').click();

      await expect(page.getByTestId('notifications-empty')).toContainText(
        language === 'ar' ? 'لا إشعارات جديدة' : 'No new notifications',
      );
      await expect(page.getByTestId('mark-all-read')).toHaveCount(0);
      await pictureFor(page, 'notifications', language, 'empty');
    });
  }

  test('a list that could not be loaded says so, and «إعادة المحاولة» loads it', async ({ page }) => {
    const platform = await inside(page, AR);
    platform.notificationsAnswer = problem(503, 'SERVICE_UNAVAILABLE');

    await page.getByTestId('bell').click();

    await expect(page.getByTestId('notifications-failed')).toBeVisible();
    await expect(page.getByTestId('mark-all-read')).toHaveCount(0);

    platform.notificationsAnswer = null;
    await page.getByTestId('notifications-retry').click();

    await expect(items(page)).toHaveCount(4);
  });

  test('after sign-out nothing more is asked', async ({ page }) => {
    const platform = await inside(page, AR);
    await expect(page.getByTestId('bell-dot')).toBeVisible();

    await page.getByTestId('account-chip').click();
    await page.getByTestId('sign-out').click();
    await expect(page.getByTestId('shell')).toHaveCount(0);
    const asked = platform.notificationCalls.length;

    await page.clock.fastForward(90_000);

    expect(platform.notificationCalls.length).toBe(asked);
  });
});
