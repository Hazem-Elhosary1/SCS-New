import { expect, test } from '@playwright/test';
import {
  CLINIC,
  passwordRefused,
  staffPlatform,
  submitSignIn,
  typeCode,
  useEnglish,
} from './staff-platform';

/**
 * Spec 004, User Story 6: a person whose password was chosen by someone else is shown one
 * screen after both sign-in steps, and reaches nothing else until they choose their own.
 */
const TEMPORARY = 'Typed-By-Manager-5!';
const NEW = 'Brand-New-Pass-7!';

test('both sign-in steps lead to the temporary-password screen, and nowhere else, until it is replaced', async ({
  page,
}) => {
  const platform = await staffPlatform(page, { mustChange: true });

  await page.goto(`${CLINIC}/sign-in`);
  await submitSignIn(page, 'owner@noor.example', TEMPORARY);
  await typeCode(page, '123456');
  await page.getByTestId('code-submit').click();

  // Not the console: the one screen that is allowed.
  await expect(page).toHaveURL(`${CLINIC}/change-password`);
  await expect(page.getByTestId('temporary-title')).toHaveText('كلمة المرور مؤقتة ويلزم تغييرها');
  await expect(page.getByTestId('account-email')).toHaveText('owner@noor.example');
  await expect(page.getByTestId('signed-in')).toHaveCount(0);
  await expect(page.locator('input')).toHaveCount(1);
  await expect(page.getByTestId('new-password')).toBeFocused();
  await expect(page.getByTestId('password-rules').locator('li')).toHaveCount(4);

  // Any other address comes back here; there is nothing to press that skips it.
  for (const path of ['/', '/patients', '/sign-in']) {
    await page.goto(`${CLINIC}${path}`);
    await expect(page).toHaveURL(`${CLINIC}/change-password`);
    await expect(page.getByTestId('temporary-title')).toBeVisible();
  }

  await expect(page.locator('a[href="/"], [data-testid="skip"]')).toHaveCount(0);

  // The temporary password itself is refused, and the person stays.
  platform.change = passwordRefused('SAME_AS_CURRENT');
  await page.getByTestId('new-password').fill(TEMPORARY);
  await page.getByTestId('change-password-submit').click();
  await expect(page.getByTestId('new-password-error')).toHaveText(
    'هذه هي كلمة المرور الحالية. يلزم اختيار كلمة مرور مختلفة عنها.',
  );
  await expect(page).toHaveURL(`${CLINIC}/change-password`);

  // A rule unmet: its line, and no call.
  const before = platform.calls.length;
  await page.getByTestId('new-password').fill('short');
  await page.getByTestId('change-password-submit').click();
  await expect(page.getByTestId('rule-length')).toHaveAttribute('data-state', 'unmet');
  expect(platform.calls.length).toBe(before);

  // Their own password: straight into the console, in the same session.
  platform.change = { status: 204 };
  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('change-password-submit').click();

  await expect(page).toHaveURL(`${CLINIC}/`);
  await expect(page.getByTestId('signed-in-email')).toHaveText('owner@noor.example');
  expect(platform.bodies.at(-1)).toEqual({ password: NEW });
  expect(platform.calls).not.toContain('POST /v1/auth/sign-out');

  // The screen is for a person who holds a temporary password, and nobody else.
  await page.goto(`${CLINIC}/change-password`);
  await expect(page).toHaveURL(`${CLINIC}/`);

  // And the next sign-in asks for nothing.
  await page.getByTestId('sign-out').click();
  await submitSignIn(page, 'owner@noor.example', NEW);
  await typeCode(page, '123456');
  await page.getByTestId('code-submit').click();
  await expect(page.getByTestId('signed-in')).toBeVisible();
  await expect(page).toHaveURL(`${CLINIC}/`);
});

test('signing out stays possible from the screen', async ({ page }) => {
  const platform = await staffPlatform(page, { mustChange: true, signedIn: true });

  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('temporary-title')).toBeVisible();

  await page.getByTestId('sign-out').click();

  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  expect(platform.signedIn).toBe(false);
  await expect(page.locator('scs-banner')).toHaveCount(0);
});

test('closing the browser on the screen changes nothing: it is shown again', async ({ page }) => {
  await staffPlatform(page, { mustChange: true, signedIn: true });

  await page.goto(`${CLINIC}/change-password`);
  await expect(page.getByTestId('temporary-title')).toBeVisible();
  await page.reload();

  await expect(page.getByTestId('temporary-title')).toBeVisible();
  await expect(page.getByTestId('new-password')).toHaveValue('');
});

test('an outage is said on the form and nothing is lost', async ({ page }) => {
  await staffPlatform(page, { mustChange: true, signedIn: true });
  await page.goto(`${CLINIC}/change-password`);
  await page.route('**/v1/auth/password/change', (route) => route.abort());

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('change-password-submit').click();

  await expect(page.getByTestId('banner-unreachable')).toBeVisible();
  await expect(page.getByTestId('new-password')).toHaveValue(NEW);
  await expect(page.getByTestId('change-password-submit')).toBeEnabled();
  await expect(page).toHaveURL(`${CLINIC}/change-password`);
});

test('a person who is not signed in is shown the sign-in form instead', async ({ page }) => {
  await staffPlatform(page);

  await page.goto(`${CLINIC}/change-password`);

  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
});

test('the screen reads the same in English', async ({ page }) => {
  await useEnglish(page);
  await staffPlatform(page, { mustChange: true, signedIn: true });

  await page.goto(`${CLINIC}/`);

  await expect(page.getByTestId('temporary-title')).toHaveText(
    'The password is temporary and must be changed',
  );
  await expect(page.getByTestId('change-password-submit')).toHaveText(
    'Save the password and continue',
  );
  await expect(page.getByTestId('sign-out')).toHaveText('Sign out');
});
