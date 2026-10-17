import { Page, expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  StaffPlatform,
  pictureFor,
  planWithout,
  roles,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 014, User Story 5 (FR-026 to FR-029; contracts/not-in-plan-screen.md): a page whose feature
 * the clinic's plan leaves out keeps its header and shows the card the design draws — «Inbox /
 * Not In Plan» (`6701:1002017`), «Financial / Not In Plan» (`6614:945146`) — with the way to the
 * subscription only for whoever may open it. The plan comes from the session's answer, read again
 * when such a page opens; a person who may not open the page is told about the role, never the plan.
 */

const ANALYTICS = {
  allow_analytics: { ar: 'التقارير المالية والطبية', en: 'Financial & medical reports' },
};
const CHANNELS = {
  allow_whatsapp: { ar: 'قناة واتساب', en: 'WhatsApp channel' },
  allow_telegram: { ar: 'قناة تيليجرام', en: 'Telegram channel' },
};

const WORDS = {
  ar: {
    inboxTitle: 'الباقة الحاليّة لا تشمل قنوات المحادثة',
    inboxText:
      'قنوات المحادثة ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك».',
    reportsTitle: 'الباقة الحاليّة لا تشمل التقارير',
    reportsOwner:
      'التقارير المالية والطبية ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك»، ويُطلب ذلك من مالك العيادة.',
    link: 'فتح الملف والاشتراك',
    reportsPage: 'التقارير المالية',
  },
  en: {
    inboxTitle: 'The current plan does not include messaging channels',
    inboxText:
      'Messaging channels are not in the clinic’s current plan. This is a limit of the plan, not a missing permission, and the plan is changed in Profile & subscription.',
    reportsTitle: 'The current plan does not include reports',
    reportsOwner:
      'Financial and clinical reports are not in the clinic’s current plan. This is a limit of the plan, not a missing permission, and the plan is changed in Profile & subscription by the clinic’s owner.',
    link: 'Open Profile & subscription',
    reportsPage: 'Financial reports',
  },
} as const;

async function open(
  page: Page,
  path: string,
  start: Partial<StaffPlatform>,
): Promise<StaffPlatform> {
  const platform = await staffPlatform(page, { signedIn: true, ...start });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/${path}`);
  await expect(page.getByTestId('shell')).toBeVisible();
  return platform;
}

/**
 * Nothing wider than the window, nothing of the card cut — at 1440 and at 1024, the clinic frame's
 * narrowest: below it the frame scrolls sideways by design (spec 006, FR-043).
 */
async function whole(page: Page): Promise<void> {
  for (const width of [1440, 1024]) {
    await page.setViewportSize({ width, height: 900 });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth),
      `${width} wide`,
    ).toBeLessThanOrEqual(width);
    const cut = await page
      .getByTestId('page-not-in-plan')
      .evaluate((card) =>
        [...card.querySelectorAll('h3, p, a')].some(
          (element) => element.scrollWidth > element.clientWidth + 1,
        ),
      );
    expect(cut, `${width} wide: nothing cut`).toBe(false);
  }
  await page.setViewportSize({ width: 1440, height: 900 });
}

for (const language of ['ar', 'en'] as const) {
  const words = WORDS[language];

  test.describe(`not in the plan — ${language}`, () => {
    test.beforeEach(async ({ page }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
    });

    test('the inbox keeps its header and draws the card, with the way to the subscription', async ({
      page,
    }) => {
      await open(page, 'inbox', { plan: planWithout(CHANNELS) });

      const card = page.getByTestId('page-not-in-plan');
      await expect(card.getByTestId('empty-title')).toHaveText(words.inboxTitle);
      await expect(card.getByTestId('empty-text')).toHaveText(words.inboxText);
      await expect(page.getByTestId('nav-inbox')).toBeVisible();
      await whole(page);
      await pictureFor(page, 'plan', language, 'inbox');

      await page.getByTestId('plan-link').click();
      await expect(page).toHaveURL(`${CLINIC}/account/subscription`);
    });

    test('the financial reports say so too, and offer no link to whoever may not open the subscription', async ({
      page,
    }) => {
      await open(page, 'reports/financial', {
        plan: planWithout(ANALYTICS),
        role: roles.team,
        permissions: ['reports.financial'],
      });

      const card = page.getByTestId('page-not-in-plan');
      await expect(page.getByTestId('page-title')).toHaveText(words.reportsPage);
      await expect(card.getByTestId('empty-title')).toHaveText(words.reportsTitle);
      await expect(card.getByTestId('empty-text')).toHaveText(words.reportsOwner);
      await expect(page.getByTestId('plan-link')).toHaveCount(0);
      await whole(page);
      await pictureFor(page, 'plan', language, 'financial-no-link');
    });

    test('a holder of the subscription sees the link on the reports, as drawn', async ({
      page,
    }) => {
      await open(page, 'reports/financial', { plan: planWithout(ANALYTICS) });

      await expect(page.getByTestId('plan-link')).toHaveText(words.link);
      await pictureFor(page, 'plan', language, 'financial');
    });

    test('a person who may not open the page is told about the role, never the plan', async ({
      page,
    }) => {
      await open(page, 'reports/financial', {
        plan: planWithout(ANALYTICS),
        role: roles.team,
        permissions: ALL_PERMISSIONS.filter((code) => code !== 'reports.financial'),
      });

      await expect(page.getByTestId('no-access')).toBeVisible();
      await expect(page.getByTestId('page-not-in-plan')).toHaveCount(0);
    });

    test('a page whose feature is still upcoming is never shown as not in the plan', async ({
      page,
    }) => {
      await open(page, 'reports/financial', {});

      await expect(page.getByTestId('page-not-yet')).toBeVisible();
      await expect(page.getByTestId('page-not-in-plan')).toHaveCount(0);
    });

    test('a plan changed meanwhile shows by the next page opened', async ({ page }) => {
      const platform = await open(page, 'reports/financial', { plan: planWithout(ANALYTICS) });
      await expect(page.getByTestId('page-not-in-plan')).toBeVisible();

      platform.plan = { ...platform.plan, withheld: [], names: {} };
      await page.getByTestId('nav-home').click();
      await page.getByTestId('nav-reports').click();
      if (!page.url().endsWith('/reports/financial')) {
        await page.getByTestId('nav-reports.financial').click();
      }
      await expect(page).toHaveURL(`${CLINIC}/reports/financial`);

      await expect(page.getByTestId('page-not-yet')).toBeVisible();
      await expect(page.getByTestId('page-not-in-plan')).toHaveCount(0);
    });

    test('without a connection the card shows from the last session copy (§4)', async ({
      page,
    }) => {
      await open(page, 'reports/financial', { plan: planWithout(ANALYTICS) });
      await expect(page.getByTestId('page-not-in-plan')).toBeVisible();

      await page.getByTestId('nav-home').click();
      await page.context().setOffline(true);
      await page.getByTestId('nav-reports').click();
      if (!page.url().endsWith('/reports/financial')) {
        await page.getByTestId('nav-reports.financial').click();
      }

      await expect(page).toHaveURL(`${CLINIC}/reports/financial`);
      await expect(page.getByTestId('page-not-in-plan').getByTestId('empty-title')).toHaveText(
        words.reportsTitle,
      );
      await expect(page.getByTestId('sign-in-title')).toHaveCount(0);
      await page.context().setOffline(false);
    });

    test('a page of either channel is open while one channel is in the plan', async ({ page }) => {
      await open(page, 'inbox', { plan: planWithout({ allow_telegram: CHANNELS.allow_telegram }) });

      await expect(page.getByTestId('page-not-in-plan')).toHaveCount(0);
    });
  });
}
