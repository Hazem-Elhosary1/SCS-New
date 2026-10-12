import { Page, Route, expect, test } from '@playwright/test';

/**
 * SC-012: each of the three front ends, in Arabic and in English, has the right direction, no
 * untranslated text, Cairo time and Egyptian pounds, all six screen states, and shows the
 * platform's own message and reference when a request fails.
 */
const apps = [
  { name: 'clinic', url: 'http://localhost:4201/dev/platform', ar: 'لوحة العيادة', en: 'Clinic console' },
  // The patient site is served under a clinic's path.
  { name: 'patient', url: 'http://localhost:4202/noor', ar: 'واجهات المريض', en: 'Patient pages' },
  // The operators console is no longer a placeholder (spec 010): its screens, languages and
  // states are checked by ops-sign-in.spec.ts and ops-console.spec.ts.
] as const;

const languages = ['ar', 'en'] as const;
const states = ['loading', 'empty', 'error', 'offline', 'view-only', 'not-in-plan'] as const;

const release = {
  version: '0.1.0+3f9c2ab',
  commit: '3f9c2ab4d5e6f708192a3b4c5d6e7f8091a2b3c4',
  // 10:00 UTC in July is 13:00 in Cairo.
  builtAt: '2026-07-01T10:00:00Z',
  environment: 'local',
};

const TRACE = '4bf92f3577b34da6a3ce929d0e0e4736';

const cors = {
  'access-control-allow-origin': '*',
  'access-control-allow-headers': '*',
  'access-control-expose-headers': 'X-Trace-Id',
};

/**
 * The staff and operators sites call the platform with cookies (spec 003), so an answer must
 * name the calling site rather than allow any.
 */
function corsFor(route: Route): Record<string, string> {
  return {
    ...cors,
    'access-control-allow-origin': route.request().headers()['origin'] ?? '*',
    'access-control-allow-credentials': 'true',
  };
}

const signedIn = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'owner@noor.example',
  clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
};

/** Answers the platform's two status calls, including the browser's preflight. */
async function platform(page: Page, version: (route: Route) => Promise<void>): Promise<void> {
  const answer = (handler: (route: Route) => Promise<void>) => async (route: Route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: corsFor(route) });
    } else {
      await handler(route);
    }
  };

  await page.route('**/v1/meta/version', answer(version));
  // The clinic console is behind sign-in; these tests are about what is shown once inside.
  await page.route(
    '**/v1/auth/session',
    answer((route) => route.fulfill({ status: 200, headers: corsFor(route), json: signedIn })),
  );
  await page.route(
    '**/v1/clinic',
    answer(async (route) => {
      const asked = route.request().headers()['x-scs-clinic'] ?? '';
      if (asked === 'noor' || asked === 'old-noor') {
        await route.fulfill({
          status: 200,
          headers: corsFor(route),
          json: {
            shortName: 'noor',
            nameAr: 'عيادة النور',
            nameEn: 'Al Noor Clinic',
            state: 'active',
            renamed: asked !== 'noor',
          },
        });
        return;
      }

      const english = (route.request().headers()['accept-language'] ?? '') === 'en';
      await route.fulfill({
        status: 404,
        headers: { ...corsFor(route), 'content-type': 'application/problem+json' },
        body: JSON.stringify({
          type: 'urn:scs:error:clinic-not-available',
          title: english ? 'This page is not available' : 'هذه الصفحة غير متاحة',
          status: 404,
          code: 'CLINIC_NOT_AVAILABLE',
          traceId: TRACE,
        }),
      });
    }),
  );
  await page.route(
    '**/health/ready',
    answer((route) =>
      route.fulfill({ status: 200, headers: corsFor(route), json: { status: 'Healthy' } }),
    ),
  );
}

const healthy = (route: Route) =>
  route.fulfill({ status: 200, headers: corsFor(route), json: release });

async function open(page: Page, url: string, language: 'ar' | 'en'): Promise<void> {
  await page.goto(url);
  await expect(page.getByTestId('placeholder')).toBeVisible();
  if (language === 'en') {
    await page.getByTestId('placeholder').getByTestId('language-switch').click();
  }
  await expect(page.locator('html')).toHaveAttribute('lang', language);
}

for (const app of apps) {
  for (const language of languages) {
    test.describe(`${app.name} in ${language}`, () => {
      test('direction, translated text, Cairo time and Egyptian pounds', async ({ page }) => {
        await platform(page, healthy);
        await open(page, app.url, language);

        await expect(page.locator('html')).toHaveAttribute(
          'dir',
          language === 'ar' ? 'rtl' : 'ltr',
        );
        await expect(page.getByTestId('app-name')).toHaveText(app[language]);
        await expect(page.getByTestId('platform-reachable')).toHaveText(
          language === 'ar' ? 'المنصّة متاحة' : 'Platform is available',
        );

        // No text is shown as its key.
        const visible = await page.locator('body').innerText();
        expect(visible).not.toMatch(/\b(?:ui|app)\.[a-z]+\.[A-Za-z.]+/);

        const date = page.getByTestId('sample-date');
        const amount = page.getByTestId('sample-amount');
        if (language === 'ar') {
          await expect(date).toContainText('١:٠٠');
          await expect(amount).toContainText('١٬٢٥٠٫٥٠');
          await expect(amount).toContainText('ج.م');
        } else {
          await expect(date).toContainText('13:00');
          await expect(amount).toContainText('1,250.50');
          await expect(amount).toContainText('EGP');
        }
      });

      test('each of the six states renders', async ({ page }) => {
        await platform(page, healthy);
        await open(page, app.url, language);

        for (const state of states) {
          await page.getByTestId(`force-${state}`).click();
          const forced = page.getByTestId('forced-state');
          await expect(forced).toHaveAttribute('data-state', state);
          // Loading, empty and error are the shared parts of lists (spec 006): one look per state.
          if (state === 'loading') {
            await expect(forced.locator('scs-skeleton')).toBeVisible();
          } else {
            await expect(forced.locator('.title, [data-testid="empty-title"], [data-testid="offline-alert"]')).not.toBeEmpty();
          }
        }

        await page.getByTestId('force-not-in-plan').click();
        await expect(page.getByTestId('forced-state').getByTestId('empty-title')).toHaveText(
          language === 'ar' ? 'غير مشمولة في باقتك' : 'Not included in your plan',
        );
      });

      test('a failed request shows the platform message and its reference', async ({ page }) => {
        const title = language === 'ar' ? 'حدث خطأ غير متوقع' : 'An unexpected error occurred';
        let sentLanguage = '';

        await platform(page, async (route) => {
          sentLanguage = route.request().headers()['accept-language'] ?? '';
          await route.fulfill({
            status: 500,
            headers: { ...corsFor(route), 'content-type': 'application/problem+json' },
            body: JSON.stringify({
              type: 'urn:scs:error:internal-error',
              title,
              status: 500,
              code: 'INTERNAL_ERROR',
              traceId: TRACE,
            }),
          });
        });
        await open(page, app.url, language);
        if (language === 'en') {
          // The first check ran in Arabic, before the switch; ask again in English.
          await page.getByTestId('state-retry').click();
        }

        const error = page.getByTestId('platform-unreachable');
        await expect(error).toHaveAttribute('data-state', 'error');
        await expect(error.getByTestId('empty-title')).toHaveText(title);
        await expect(error.getByTestId('state-reference')).toHaveText(TRACE);
        expect(sentLanguage).toBe(language);
      });
    });
  }
}

test('the page shows its own version and calls no third party', async ({ page }) => {
  const outside: string[] = [];
  page.on('request', (request) => {
    const host = new URL(request.url()).hostname;
    if (host !== 'localhost') outside.push(request.url());
  });
  await platform(page, healthy);
  await page.route('**/version.json', (route) =>
    route.fulfill({
      json: {
        app: 'clinic',
        version: '0.2.0+abcdef0',
        commit: 'abcdef0'.padEnd(40, '0'),
        builtAt: '2026-10-10T09:30:00Z',
      },
    }),
  );

  await page.goto(apps[0].url);

  await expect(page.getByTestId('app-version')).toHaveText('0.2.0+abcdef0');
  // The typeface is served by the site itself.
  await expect(page.locator('body')).toHaveCSS('font-family', /Cairo Variable/);
  await page.evaluate(() => document.fonts.ready);
  expect(outside).toEqual([]);
});

test('switching language keeps the page: no reload', async ({ page }) => {
  await platform(page, healthy);
  await page.goto(apps[0].url);
  await expect(page.getByTestId('platform-reachable')).toBeVisible();
  await page.evaluate(() => ((window as unknown as { marker: string }).marker = 'still here'));

  await page.getByTestId('placeholder').getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await page.getByTestId('placeholder').getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

  expect(await page.evaluate(() => (window as unknown as { marker: string }).marker)).toBe(
    'still here',
  );
});

test('the chosen language is remembered on the next visit', async ({ page }) => {
  await platform(page, healthy);
  await page.goto(apps[0].url);
  await page.getByTestId('placeholder').getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');

  await page.reload();

  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByTestId('app-name')).toHaveText('Clinic console');
});

test('offline shows the offline state and recovers by itself', async ({ page, context }) => {
  await platform(page, healthy);
  await page.goto(apps[0].url);
  await expect(page.getByTestId('platform-reachable')).toBeVisible();

  await context.setOffline(true);
  await expect(page.locator('scs-state[data-state="offline"]')).toBeVisible();

  await context.setOffline(false);
  await expect(page.getByTestId('platform-reachable')).toBeVisible();
});

test('a page that cannot start says so in both languages', async ({ page }) => {
  await page.route('**/config.json', (route) => route.fulfill({ status: 404, body: 'missing' }));

  await page.goto(apps[0].url);

  const failure = page.locator('[data-scs-startup-failure]');
  await expect(failure).toBeVisible();
  await expect(failure.locator('p[lang="ar"]')).not.toBeEmpty();
  await expect(failure.locator('p[lang="en"]')).not.toBeEmpty();
});
