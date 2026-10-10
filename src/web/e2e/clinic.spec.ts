import { Page, Route, expect, test } from '@playwright/test';

/**
 * Spec 002, User Story 3: a patient address leads to its clinic, and every address that names
 * no clinic shows the same "not available" page. The platform's answers are given by the test.
 */
const SITE = 'http://localhost:4202';

const cors = {
  'access-control-allow-origin': '*',
  'access-control-allow-headers': '*',
};

interface Asked {
  clinic: string[];
}

async function platform(page: Page): Promise<Asked> {
  const asked: Asked = { clinic: [] };

  const answer = (handler: (route: Route) => Promise<void>) => async (route: Route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: cors });
    } else {
      await handler(route);
    }
  };

  await page.route(
    '**/v1/meta/version',
    answer((route) =>
      route.fulfill({
        status: 200,
        headers: cors,
        json: {
          version: '0.2.0',
          commit: 'a'.repeat(40),
          builtAt: '2026-07-01T10:00:00Z',
          environment: 'local',
        },
      }),
    ),
  );
  await page.route(
    '**/health/ready',
    answer((route) => route.fulfill({ status: 200, headers: cors, json: { status: 'Healthy' } })),
  );
  await page.route(
    '**/v1/clinic',
    answer(async (route) => {
      const name = route.request().headers()['x-scs-clinic'] ?? '';
      asked.clinic.push(name);

      if (name === 'noor' || name === 'old-noor') {
        await route.fulfill({
          status: 200,
          headers: cors,
          json: {
            shortName: 'noor',
            nameAr: 'عيادة النور',
            nameEn: 'Al Noor Clinic',
            state: 'active',
            renamed: name !== 'noor',
          },
        });
        return;
      }

      await route.fulfill({
        status: 404,
        headers: { ...cors, 'content-type': 'application/problem+json' },
        body: JSON.stringify({
          type: 'urn:scs:error:clinic-not-available',
          title: 'هذه الصفحة غير متاحة',
          status: 404,
          code: 'CLINIC_NOT_AVAILABLE',
          traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
        }),
      });
    }),
  );

  return asked;
}

test('a clinic address shows the clinic, in Arabic and in English', async ({ page }) => {
  const asked = await platform(page);

  await page.goto(`${SITE}/noor`);

  await expect(page.getByTestId('clinic-name')).toHaveText('عيادة النور');
  await expect(page.getByTestId('platform-reachable')).toBeVisible();
  expect(asked.clinic).toContain('noor');

  await page.getByTestId('language-switch').click();
  await expect(page.getByTestId('clinic-name')).toHaveText('Al Noor Clinic');
});

test('letter case and a trailing slash lead to the same clinic and the address is tidied', async ({
  page,
}) => {
  await platform(page);

  await page.goto(`${SITE}/NOOR/`);

  await expect(page.getByTestId('clinic-name')).toHaveText('عيادة النور');
  await expect(page).toHaveURL(`${SITE}/noor`);
});

test('an earlier name moves to the current one and keeps the rest of the address', async ({
  page,
}) => {
  await platform(page);

  await page.goto(`${SITE}/old-noor/booking/step-2?from=sms`);

  await expect(page.getByTestId('clinic-name')).toHaveText('عيادة النور');
  await expect(page).toHaveURL(`${SITE}/noor/booking/step-2?from=sms`);
});

test('every address that names no clinic shows the same page, in both languages', async ({
  page,
}) => {
  const asked = await platform(page);
  const pages: string[] = [];

  for (const path of ['/nosuch', '/another-missing', '/', '/ab', '/api']) {
    await page.goto(`${SITE}${path}`);
    const notAvailable = page.getByTestId('clinic-not-available');
    await expect(notAvailable).toHaveText('هذه الصفحة غير متاحة');
    await expect(page.getByTestId('clinic-name')).toHaveCount(0);
    pages.push(await notAvailable.innerHTML());
  }

  // Identical: nothing tells an unknown clinic from a malformed or empty address.
  expect(new Set(pages).size).toBe(1);

  // An address that could not be a name is never sent to the platform.
  expect(asked.clinic).not.toContain('ab');
  expect(asked.clinic.filter((name) => name === '')).toEqual([]);

  await page.goto(`${SITE}/nosuch`);
  await page.evaluate(() => localStorage.setItem('scs.language', 'en'));
  await page.reload();
  await expect(page.getByTestId('clinic-not-available')).toHaveText('This page is not available');
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
});

test('from asking the platform to the final page takes under half a second, known or unknown', async ({
  page,
}) => {
  await platform(page);

  // SC-006. The platform's own share is measured by ClinicEndpointTests; this is the page's.
  for (const [path, shown] of [
    ['/noor', 'clinic-name'],
    ['/nosuch', 'clinic-not-available'],
  ]) {
    await page.goto('about:blank');
    let asked = 0;
    const onRequest = (request: { url(): string; method(): string }) => {
      if (asked === 0 && request.url().endsWith('/v1/clinic') && request.method() === 'GET') {
        asked = Date.now();
      }
    };
    page.on('request', onRequest);

    await page.goto(`${SITE}${path}`);
    await page.getByTestId(shown).waitFor();
    const took = Date.now() - asked;
    page.off('request', onRequest);

    expect(asked, `${path} asked the platform`).toBeGreaterThan(0);
    expect(took, `${path} took ${took} ms`).toBeLessThan(500);
  }
});

test('when the platform cannot be asked the page says so and does not claim the clinic is missing', async ({
  page,
}) => {
  await page.route('**/v1/clinic', (route) => route.abort());

  await page.goto(`${SITE}/noor`);

  await expect(page.getByTestId('clinic-failed')).toBeVisible();
  await expect(page.getByTestId('clinic-not-available')).toHaveCount(0);
  await expect(page.getByTestId('state-retry')).toBeVisible();
});

test('the staff and operators sites never send a clinic name', async ({ page }) => {
  const sent: string[] = [];
  page.on('request', (request) => {
    const header = request.headers()['x-scs-clinic'];
    if (header !== undefined) {
      sent.push(`${request.url()} -> ${header}`);
    }
  });
  await platform(page);

  await page.goto('http://localhost:4201/noor');
  await expect(page.getByTestId('placeholder')).toBeVisible();
  await page.goto('http://localhost:4203/noor');
  await expect(page.getByTestId('placeholder')).toBeVisible();

  expect(sent).toEqual([]);
});
