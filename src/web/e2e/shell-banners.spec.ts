import { Page, expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  problem,
  roles,
  sampleSources,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 006, User Story 6: what the frame says above the page — the environment strip, the
 * offline alert, the subscription banner in its four states, and "not in your plan". The
 * states that belong to later specs are given by sample sources (dev/sample-sources.ts).
 */
async function inside(page: Page, path = ''): Promise<void> {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/${path}`);
  await expect(page.getByTestId('shell')).toBeVisible();
}

async function asProduction(page: Page): Promise<void> {
  await page.route('**/config.json', (route) =>
    route.fulfill({ json: { environment: 'production', apiBaseUrl: '/api' } }),
  );
}

test.describe('the environment strip', () => {
  test('is on every screen outside production, before sign-in and after it', async ({ page }) => {
    const platform = await staffPlatform(page);

    await page.goto(`${CLINIC}/sign-in`);
    const strip = page.getByTestId('env-strip');
    await expect(strip).toHaveText('بيئة اختبار — البيانات ليست حقيقية');
    expect((await strip.boundingBox())!.height).toBe(32);
    expect((await strip.boundingBox())!.y).toBe(0);
    // The form under it still fits the window: nothing scrolls because of the strip.
    expect(await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight)).toBe(true);

    for (const path of ['forgot-password', 'privacy']) {
      await page.goto(`${CLINIC}/${path}`);
      await expect(strip).toBeVisible();
    }

    platform.signedIn = true;
    for (const path of ['', 'administration/roles', 'nowhere']) {
      await inside(page, path);
      await expect(strip).toBeVisible();
      expect((await page.getByTestId('top-bar').boundingBox())!.y).toBe(32);
    }
  });

  test('reads in English too', async ({ page }) => {
    await useEnglish(page);
    await staffPlatform(page);
    await page.goto(`${CLINIC}/sign-in`);

    await expect(page.getByTestId('env-strip')).toHaveText('Test environment — data is not real');
  });

  test('is never shown in production, where the pages for builders do not exist either', async ({
    page,
  }) => {
    await asProduction(page);
    await sampleSources(page, { subscription: { state: 'trial', days: 5 } });
    const platform = await staffPlatform(page);

    await page.goto(`${CLINIC}/sign-in`);
    await expect(page.getByTestId('sign-in-submit')).toBeVisible();
    await expect(page.getByTestId('env-strip')).toHaveCount(0);

    platform.signedIn = true;
    await inside(page);
    await expect(page.getByTestId('env-strip')).toHaveCount(0);
    expect((await page.getByTestId('top-bar').boundingBox())!.y).toBe(0);
    // Sample data is not read in production.
    await expect(page.getByTestId('subscription-banner')).toHaveCount(0);

    for (const path of ['dev/sample-list', 'dev/platform']) {
      await inside(page, path);
      await expect(page.getByTestId('page-not-found')).toBeVisible();
    }
  });
});

test.describe('offline', () => {
  test('the alert is inside the content, goes by itself, and the page is read again', async ({
    page,
    context,
  }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await inside(page, 'administration/roles');
    await expect(page.getByTestId('role-row')).toHaveCount(3);
    await expect(page.getByTestId('offline-alert')).toHaveCount(0);
    const asked = () => platform.calls.filter((call) => call === 'GET /v1/roles').length;
    expect(asked()).toBe(1);

    await context.setOffline(true);

    const alert = page.getByTestId('offline-alert');
    await expect(alert).toBeVisible({ timeout: 3000 });
    await expect(alert).toContainText(
      'انقطع الاتصال بالإنترنت — ما تراه آخر نسخة محمَّلة، وما يغيّر البيانات معطَّل حتى يعود الاتصال.',
    );
    // Inside the content: beside the side bar, under the page's header, above the page itself.
    const [box, bar, title] = await Promise.all([
      alert.boundingBox(),
      page.getByTestId('side-bar').boundingBox(),
      page.getByTestId('page-title').boundingBox(),
    ]);
    expect(box!.x + box!.width).toBeLessThanOrEqual(bar!.x);
    expect(box!.y).toBeGreaterThanOrEqual(32 + 64);
    expect(box!.y).toBeGreaterThanOrEqual(title!.y + title!.height);
    expect(box!.y + box!.height).toBeLessThanOrEqual(
      (await page.getByTestId('roles-ready').boundingBox())!.y,
    );
    await expect(alert).toHaveAttribute('data-tone', 'warning');
    // What was loaded stays readable.
    await expect(page.getByTestId('role-row')).toHaveCount(3);

    await context.setOffline(false);

    await expect(alert).toHaveCount(0);
    await expect.poll(asked).toBe(2);
    await expect(page.getByTestId('role-row')).toHaveCount(3);
  });

  test('a request that fails while the browser is online shows its own error, not the offline alert', async ({
    page,
  }) => {
    await staffPlatform(page, {
      signedIn: true,
      rolesAnswer: problem(503, 'SERVICE_UNAVAILABLE'),
    });
    await inside(page, 'administration/roles');

    await expect(page.getByTestId('roles-failed')).toBeVisible();
    await expect(page.getByTestId('offline-alert')).toHaveCount(0);
  });
});

const BANNERS = [
  {
    state: 'trial',
    tone: 'info',
    icon: 'info',
    ar: 'تبقّى ٥ أيام على انتهاء الفترة التجريبية',
    en: '5 days left in your trial',
    action: { ar: 'اختيار باقة', en: 'Choose a plan' },
  },
  {
    state: 'expiring',
    tone: 'warning',
    icon: 'clock',
    ar: 'ينتهي اشتراك العيادة خلال ٥ أيام',
    en: 'Your clinic subscription ends in 5 days',
    action: { ar: 'تجديد الاشتراك', en: 'Renew subscription' },
  },
  {
    state: 'grace',
    tone: 'danger',
    icon: 'alert-circle',
    ar: 'انتهى اشتراكك — أمامك ٥ أيام قبل إقفال التعديل',
    en: 'Your subscription has ended — 5 days before editing locks',
    action: { ar: 'تجديد الاشتراك', en: 'Renew subscription' },
  },
  {
    state: 'readonly',
    tone: 'neutral',
    icon: 'lock',
    ar: 'الحساب للقراءة فقط حتى يُجدَّد الاشتراك',
    en: 'The account is read-only until the subscription is renewed',
    action: { ar: 'تجديد الاشتراك', en: 'Renew subscription' },
  },
] as const;

test.describe('the subscription banner', () => {
  test('is absent while no state is given — as it is until the subscription specs', async ({ page }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    await expect(page.getByTestId('subscription-banner')).toHaveCount(0);
  });

  for (const language of ['ar', 'en'] as const) {
    for (const banner of BANNERS) {
      test(`${banner.state} in ${language}: one banner under the top bar, its tone, wording and action`, async ({
        page,
      }) => {
        if (language === 'en') {
          await useEnglish(page);
        }
        await sampleSources(page, { subscription: { state: banner.state, days: 5 } });
        await staffPlatform(page, { signedIn: true });
        await inside(page);

        const shown = page.getByTestId('subscription-banner');
        await expect(shown).toHaveCount(1);
        await expect(shown).toHaveAttribute('data-tone', banner.tone);
        await expect(shown).toContainText(banner[language]);
        await expect(page.getByTestId('subscription-action')).toHaveText(banner.action[language]);
        await expect(page.getByTestId('subscription-close')).toHaveCount(banner.state === 'readonly' ? 0 : 1);
        await expect(shown.locator('scs-icon').first()).toHaveAttribute('data-icon', banner.icon);

        // The state at the reading start, its action at the far end.
        const [words, action] = await Promise.all([
          shown.locator('.words').boundingBox(),
          page.getByTestId('subscription-action').boundingBox(),
        ]);
        if (language === 'ar') {
          expect(action!.x).toBeLessThan(200);
          expect(words!.x + words!.width).toBeGreaterThan(1300);
        } else {
          expect(action!.x + action!.width).toBeGreaterThan(1240);
          expect(words!.x).toBeLessThan(100);
        }

        const box = (await shown.boundingBox())!;
        expect(box.y).toBe(32 + 64);
        expect(box.height).toBe(48);
        expect(box.width).toBe(1440);

        await page.getByTestId('subscription-action').click();
        await expect(page).toHaveURL(`${CLINIC}/account/subscription`);
        await expect(shown).toBeVisible();
      });
    }
  }

  test('closing it is for the person, until tomorrow, on every page', async ({ page }) => {
    await sampleSources(page, { subscription: { state: 'expiring', days: 3 } });
    const platform = await staffPlatform(page, {
      signedIn: true,
      preferences: { language: 'ar', sidebarFolded: null, dismissedBanner: null },
    });
    await inside(page);

    await page.getByTestId('subscription-close').click();

    await expect(page.getByTestId('subscription-banner')).toHaveCount(0);
    await expect.poll(() => platform.preferencesSent).toEqual([{ dismissedBanner: 'expiring' }]);

    await page.getByTestId('nav-bookings').click();
    await expect(page.getByTestId('subscription-banner')).toHaveCount(0);

    // The platform remembers it for today: the next visit starts without it.
    await page.reload();
    await expect(page.getByTestId('shell')).toBeVisible();
    await expect(page.getByTestId('subscription-banner')).toHaveCount(0);
  });

  test('a banner closed for one state does not hide another state', async ({ page }) => {
    await sampleSources(page, { subscription: { state: 'grace', days: 2 } });
    await staffPlatform(page, {
      signedIn: true,
      preferences: { language: 'ar', sidebarFolded: null, dismissedBanner: 'expiring' },
    });
    await inside(page);

    await expect(page.getByTestId('subscription-banner')).toHaveAttribute('data-state', 'grace');
  });

  test('a person without the subscription permission sees the banner with no action', async ({ page }) => {
    await sampleSources(page, { subscription: { state: 'grace', days: 2 } });
    await staffPlatform(page, {
      signedIn: true,
      role: roles.team,
      permissions: ALL_PERMISSIONS.filter((code) => code !== 'subscription.view'),
    });
    await inside(page);

    await expect(page.getByTestId('subscription-banner')).toContainText('انتهى اشتراكك');
    await expect(page.getByTestId('subscription-action')).toHaveCount(0);
  });

  test('sits above the offline alert when both are shown', async ({ page, context }) => {
    await sampleSources(page, { subscription: { state: 'readonly', days: 0 } });
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    await context.setOffline(true);
    await expect(page.getByTestId('offline-alert')).toBeVisible();

    const [banner, alert] = await Promise.all([
      page.getByTestId('subscription-banner').boundingBox(),
      page.getByTestId('offline-alert').boundingBox(),
    ]);
    expect(banner!.y + banner!.height).toBeLessThanOrEqual(alert!.y);
    await context.setOffline(false);
  });
});

test.describe('not in your plan', () => {
  test('the destination stays in the side bar and opens to a card that names the plan', async ({
    page,
  }) => {
    await sampleSources(page, { outsidePlan: ['reports.financial', 'tickets'] });
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    await expect(page.getByTestId('nav-tickets')).toBeVisible();
    await page.getByTestId('nav-tickets').click();

    await expect(page.getByTestId('page-title')).toHaveText('التذاكر');
    const card = page.getByTestId('page-not-in-plan');
    await expect(card).toContainText('غير مشمولة في باقتك');
    await expect(card).toContainText('باقة العيادة الحالية لا تشمل هذه الصفحة');

    await page.getByTestId('plan-link').click();
    await expect(page).toHaveURL(`${CLINIC}/account/subscription`);
  });

  test('a destination outside the plan and outside the role says "not in your role"', async ({ page }) => {
    await sampleSources(page, { outsidePlan: ['tickets'] });
    await staffPlatform(page, {
      signedIn: true,
      role: roles.team,
      permissions: ALL_PERMISSIONS.filter((code) => code !== 'tickets.view'),
    });
    await inside(page, 'tickets');

    await expect(page.getByTestId('no-access')).toBeVisible();
    await expect(page.getByTestId('page-not-in-plan')).toHaveCount(0);
  });
});
