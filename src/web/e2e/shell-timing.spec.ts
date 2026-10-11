import { expect, test } from '@playwright/test';
import { CLINIC, staffPlatform } from './staff-platform';

/**
 * Spec 006, the timed outcomes: the frame is on screen within two seconds of opening the
 * console, and moving between destinations takes under a second and never reloads the page.
 * Timed alone, with retries: a loaded machine is not what is being measured.
 */
test.describe.configure({ mode: 'serial', retries: 2 });

test('the frame is on screen within two seconds of opening the console', async ({ page }) => {
  await staffPlatform(page, { signedIn: true });
  await page.goto(`${CLINIC}/sign-in`);
  await expect(page.getByTestId('shell')).toBeVisible();

  const started = Date.now();
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('side-bar')).toBeVisible();
  await expect(page.getByTestId('page-title')).toBeVisible();

  expect(Date.now() - started).toBeLessThan(2000);
});

test('moving between destinations takes under a second, with no reload and no new session call', async ({
  page,
}) => {
  const platform = await staffPlatform(page, { signedIn: true });
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('shell')).toBeVisible();
  await page.evaluate(() => ((window as unknown as { marker: string }).marker = 'still here'));
  const sessionCalls = () => platform.calls.filter((call) => call === 'GET /v1/auth/session').length;
  const before = sessionCalls();

  for (const [key, title] of [
    ['bookings', 'الحجوزات'],
    ['patients', 'المرضى'],
    ['settings', 'الإعدادات'],
    ['home', 'الرئيسية'],
  ]) {
    const started = Date.now();
    await page.getByTestId(`nav-${key}`).click();
    await expect(page.getByTestId('page-title')).toHaveText(title);
    expect(Date.now() - started, key).toBeLessThan(1000);
  }

  expect(await page.evaluate(() => (window as unknown as { marker: string }).marker)).toBe('still here');
  expect(sessionCalls()).toBe(before);
});

test('switching the language turns the whole console within a second (SC-004)', async ({ page }, info) => {
  await staffPlatform(page, { signedIn: true });
  await page.goto(`${CLINIC}/administration/roles`);
  await expect(page.getByTestId('role-row')).toHaveCount(3);

  const started = Date.now();
  await page.getByTestId('top-bar').getByTestId('language-switch').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(page.getByTestId('page-title')).toHaveText('Roles & permissions');
  await expect(page.getByTestId('nav-bookings')).toHaveText('Bookings');
  const took = Date.now() - started;

  info.annotations.push({ type: 'measured', description: `language switch: ${took} ms` });
  console.log(`SC-004 language switch: ${took} ms`);
  expect(took).toBeLessThan(1000);
});

test('the offline alert shows within three seconds and goes within three of the connection returning (SC-006)', async ({
  page,
  context,
}, info) => {
  await staffPlatform(page, { signedIn: true });
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('shell')).toBeVisible();

  let started = Date.now();
  await context.setOffline(true);
  await expect(page.getByTestId('offline-alert')).toBeVisible();
  const shown = Date.now() - started;

  started = Date.now();
  await context.setOffline(false);
  await expect(page.getByTestId('offline-alert')).toHaveCount(0);
  const gone = Date.now() - started;

  info.annotations.push({ type: 'measured', description: `offline shown: ${shown} ms, gone: ${gone} ms` });
  console.log(`SC-006 offline alert shown: ${shown} ms, gone: ${gone} ms`);
  expect(shown).toBeLessThan(3000);
  expect(gone).toBeLessThan(3000);
});
