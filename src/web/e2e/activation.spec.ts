import { expect, test } from '@playwright/test';
import {
  CLINIC,
  TOKEN,
  newAccountLink,
  problem,
  staffPlatform,
  submitSignIn,
  typeCode,
  useEnglish,
} from './staff-platform';

/**
 * Spec 004, User Stories 3 and 4: a new team member opens the invitation link, chooses a first
 * password, and signs in; a new-account link that no longer works says who can send another.
 */
const NEW = 'Brand-New-Pass-7!';

test('an invitation link welcomes the person to their clinic and activates the account', async ({
  page,
}) => {
  const platform = await staffPlatform(page, {
    link: { status: 200, json: newAccountLink },
    linkSet: { status: 200, json: { kind: 'new-account' } },
  });

  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);

  // The one screen before sign-in that names a clinic: the link already identifies its holder.
  await expect(page.getByTestId('new-account-title')).toHaveText(/مرحبًا بك في\s+عيادة الشفاء/);
  await expect(page.getByTestId('link-email')).toHaveText('new@shifa.example');
  await expect(page.getByTestId('link-life')).toHaveText('الرابط صالح ٧٢ ساعة من إرساله.');
  await expect(page.getByTestId('set-password-submit')).toHaveText('تفعيل الحساب');
  await expect(page.locator('input')).toHaveCount(1);
  await expect(page.getByTestId('password-rules').locator('li')).toHaveCount(4);
  expect(page.url()).toBe(`${CLINIC}/set-password`);

  // The rules hold here as everywhere.
  await page.getByTestId('new-password').fill('short');
  await page.getByTestId('set-password-submit').click();
  await expect(page.getByTestId('rule-length')).toHaveAttribute('data-state', 'unmet');
  expect(platform.calls).not.toContain('POST /v1/auth/password/link/set');

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();

  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  await expect(page.getByTestId('banner-accountActivated')).toContainText('فُعِّل الحساب');
  await expect(page.getByTestId('banner-accountActivated')).toContainText(
    'الدخول الآن بكلمة المرور الجديدة.',
  );
  await expect(page.getByTestId('email')).toHaveValue('new@shifa.example');
  expect(platform.bodies.at(-1)).toEqual({ token: TOKEN, password: NEW });
  expect(platform.signedIn).toBe(false);

  // Both steps, as for any account.
  await page.getByTestId('password').fill(NEW);
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('code-title')).toBeVisible();
  await typeCode(page, '123456');
  await page.getByTestId('code-submit').click();
  await expect(page.getByTestId('signed-in')).toBeVisible();
});

test('the clinic is named in the language of the page', async ({ page }) => {
  await useEnglish(page);
  await staffPlatform(page, { link: { status: 200, json: newAccountLink } });

  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);

  await expect(page.getByTestId('new-account-title')).toHaveText(/Welcome to\s+Al Shifa Clinic/);
  await expect(page.getByTestId('link-life')).toHaveText(
    'The link is valid for 72 hours from sending.',
  );
  await expect(page.getByTestId('set-password-submit')).toHaveText('Activate the account');
});

test('a new-account link that no longer works says who can send another, and offers no button', async ({
  page,
}) => {
  await staffPlatform(page, { link: problem(410, 'LINK_INVALID', { kind: 'new-account' }) });

  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);

  await expect(page.getByTestId('dead-link-title')).toHaveText('انتهت صلاحية الرابط أو استُعمل');
  await expect(page.getByTestId('dead-link-text')).toHaveText(
    'رابط الحساب الجديد يعمل مرة واحدة ولمدة محدودة. إرسال رابط آخر بيد من أرسل الأول.',
  );

  // "Forgot the password?" sends nothing to an account that was never activated, so there is
  // no button that would lead there.
  await expect(page.getByTestId('ask-again')).toHaveCount(0);
  await expect(page.locator('form, input')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('عيادة الشفاء');

  await page.getByTestId('back-to-sign-in').click();
  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
});

test('a new-account link that runs out while the form is open shows the same screen', async ({
  page,
}) => {
  await staffPlatform(page, {
    link: { status: 200, json: newAccountLink },
    linkSet: problem(410, 'LINK_INVALID', { kind: 'new-account' }),
  });
  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();

  await expect(page.getByTestId('dead-link-text')).toContainText('رابط الحساب الجديد');
  await expect(page.getByTestId('ask-again')).toHaveCount(0);
});

test('a link opened on a browser signed in as someone else neither uses nor ends that session', async ({
  page,
}) => {
  const platform = await staffPlatform(page, {
    link: { status: 200, json: newAccountLink },
    linkSet: { status: 200, json: { kind: 'new-account' } },
  });

  // Somebody is signed in here.
  await page.goto(`${CLINIC}/sign-in`);
  await submitSignIn(page);
  await typeCode(page, '123456');
  await page.getByTestId('code-submit').click();
  await expect(page.getByTestId('signed-in-email')).toHaveText('owner@noor.example');

  // The link is for another person's account, and that is whose screen it shows.
  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);
  await expect(page.getByTestId('link-email')).toHaveText('new@shifa.example');
  await expect(page.locator('body')).not.toContainText('owner@noor.example');

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();

  // Nobody was signed out; the browser is still the first person's.
  expect(platform.calls).not.toContain('POST /v1/auth/sign-out');
  expect(platform.signedIn).toBe(true);
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('signed-in-email')).toHaveText('owner@noor.example');
});
