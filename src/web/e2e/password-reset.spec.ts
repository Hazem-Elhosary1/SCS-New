import { Page, Route, expect, test } from '@playwright/test';
import {
  CLINIC,
  TOKEN,
  passwordRefused,
  problem,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 004, User Stories 1, 2, 4 and 5: asking for a reset link, choosing a new password from
 * it under the four rules, and what a link that no longer works shows. The platform is played
 * by the test, as in every browser test of this project.
 */
const NEW = 'Brand-New-Pass-7!';

const rule = (page: Page, key: string) => page.getByTestId(`rule-${key}`);

/** Stands for the platform not answering at all. */
const unreachable = (route: Route) => route.abort();

async function ruleStates(page: Page): Promise<(string | null)[]> {
  return Promise.all(
    ['length', 'cases', 'digit', 'symbol'].map((key) => rule(page, key).getAttribute('data-state')),
  );
}

async function openLink(page: Page, token = TOKEN): Promise<void> {
  await page.goto(`${CLINIC}/set-password#t=${token}`);
}

test('a forgotten password is replaced from an emailed link, and the new one is what signs in', async ({
  page,
}) => {
  const platform = await staffPlatform(page);

  // From the sign-in form to the request, and the one result every address gets.
  await page.goto(`${CLINIC}/sign-in`);
  await page.getByTestId('forgot-password').click();
  await expect(page.getByTestId('forgot-title')).toHaveText('إعادة تعيين كلمة المرور');
  await expect(page.getByTestId('email')).toBeFocused();
  await expect(page.locator('input')).toHaveCount(1);

  await page.getByTestId('email').fill('  owner@noor.example ');
  await page.getByTestId('forgot-submit').click();

  await expect(page.getByTestId('forgot-sent-title')).toHaveText('أُرسل الطلب');
  await expect(page.getByTestId('forgot-sent-title')).toBeFocused();
  await expect(page.getByTestId('forgot-sent-text')).toHaveText(
    /إن كان\s+owner@noor\.example\s+مسجَّلًا في العيادة فسيصله خلال دقائق رابطٌ لإعادة تعيين كلمة المرور\./,
  );
  expect(platform.bodies.at(-1)).toEqual({ email: 'owner@noor.example' });
  await expect(page.locator('form')).toHaveCount(0);

  // The emailed link. The address stops carrying its secret as soon as the page has read it.
  await openLink(page);
  await expect(page.getByTestId('reset-title')).toHaveText('تعيين كلمة مرور جديدة');
  await expect(page.getByTestId('link-email')).toHaveText('owner@noor.example');
  expect(page.url()).toBe(`${CLINIC}/set-password`);
  expect(await page.evaluate(() => location.href + JSON.stringify(history.state))).not.toContain(
    TOKEN,
  );
  expect(
    await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage })),
  ).not.toContain(TOKEN);
  expect(platform.calls.filter((call) => call.includes('/v1/auth/password/link'))).toEqual([
    'POST /v1/auth/password/link',
  ]);
  expect(platform.bodies.at(-1)).toEqual({ token: TOKEN });

  // One field, with the eye, and the four rules before anything is typed.
  await expect(page.locator('input')).toHaveCount(1);
  await expect(page.getByTestId('new-password')).toBeFocused();
  await expect(page.getByTestId('password-eye')).toBeVisible();
  await expect(page.getByTestId('password-rules').locator('li')).toHaveText([
    /١٠ أحرف على الأقل/,
    /حرف كبير وحرف صغير \(A-z\)/,
    /رقم واحد على الأقل \(0-9\)/,
    /رمز خاص واحد على الأقل \(!@#\$%\)/,
  ]);
  await expect.poll(() => ruleStates(page)).toEqual(['pending', 'pending', 'pending', 'pending']);

  await page.getByTestId('new-password').fill(NEW);
  await expect.poll(() => ruleStates(page)).toEqual(['met', 'met', 'met', 'met']);
  await page.getByTestId('set-password-submit').click();

  // Back on the sign-in form, with the good news and the email filled in — and nobody signed in.
  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  await expect(page.getByTestId('banner-passwordChanged')).toContainText('غُيّرت كلمة المرور');
  await expect(page.getByTestId('banner-passwordChanged')).toContainText(
    'الدخول الآن بكلمة المرور الجديدة.',
  );
  await expect(page.getByTestId('banner-passwordChanged')).toHaveAttribute('role', 'status');
  await expect(page.getByTestId('email')).toHaveValue('owner@noor.example');
  expect(platform.bodies.at(-1)).toEqual({ token: TOKEN, password: NEW });
  expect(platform.signedIn).toBe(false);
  expect(platform.calls).not.toContain('POST /v1/auth/sign-in/code');

  // The news is said once: a reload shows the plain form.
  await page.reload();
  await expect(page.getByTestId('sign-in-title')).toBeVisible();
  await expect(page.locator('scs-banner')).toHaveCount(0);
});

test('the request form checks the address before asking, with no banner and no call', async ({
  page,
}) => {
  const platform = await staffPlatform(page);
  await page.goto(`${CLINIC}/forgot-password`);

  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('email-error')).toHaveText('البريد مطلوب');

  await page.getByTestId('email').fill('not-an-email');
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('email-error')).toHaveText('البريد غير صحيح — مثل name@clinic.com');

  await expect(page.locator('scs-banner')).toHaveCount(0);
  expect(platform.calls.filter((call) => call.includes('/password/'))).toEqual([]);

  await page.getByTestId('back-to-sign-in').click();
  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
});

test('every address ends on the same result screen', async ({ page }) => {
  await staffPlatform(page);
  const seen: string[] = [];

  for (const email of ['owner@noor.example', 'nobody@nowhere.example']) {
    await page.goto(`${CLINIC}/forgot-password`);
    await page.getByTestId('email').fill(email);
    await page.getByTestId('forgot-submit').click();
    await expect(page.getByTestId('forgot-sent-title')).toBeVisible();
    seen.push((await page.getByTestId('auth-form-panel').innerText()).replace(email, '‹email›'));
    await page.getByTestId('back-to-sign-in').click();
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  }

  expect(seen[0]).toBe(seen[1]);
});

test('too many requests and an outage are said on the form, which stays as typed', async ({
  page,
}) => {
  const platform = await staffPlatform(page, {
    forgot: { ...problem(429, 'RATE_LIMITED'), headers: { 'Retry-After': '37' } },
  });
  await page.goto(`${CLINIC}/forgot-password`);
  await page.getByTestId('email').fill('owner@noor.example');
  await page.getByTestId('forgot-submit').click();

  await expect(page.getByTestId('banner-rateLimited')).toContainText('طلبات كثيرة من هذه الشبكة');
  await expect(page.getByTestId('retry-after')).toHaveText('37');
  await expect(page.getByTestId('email')).toHaveValue('owner@noor.example');
  await expect(page.getByTestId('forgot-submit')).toBeEnabled();

  await page.route('**/v1/auth/password/forgot', unreachable);
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('banner-unreachable')).toBeVisible();
  await expect(page.locator('scs-banner')).toHaveCount(1);

  await page.unroute('**/v1/auth/password/forgot', unreachable);
  platform.forgot = { status: 202 };
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('forgot-sent-title')).toBeVisible();
});

test('a rule turns green as it is met, red on submit when it is not, and nothing is sent', async ({
  page,
}) => {
  const platform = await staffPlatform(page);
  await openLink(page);
  const field = page.getByTestId('new-password');

  await field.pressSequentially('abc');
  await expect.poll(() => ruleStates(page)).toEqual(['pending', 'pending', 'pending', 'pending']);
  await field.pressSequentially('DEF');
  await expect.poll(() => ruleStates(page)).toEqual(['pending', 'met', 'pending', 'pending']);
  await field.pressSequentially('12');
  await expect.poll(() => ruleStates(page)).toEqual(['pending', 'met', 'met', 'pending']);
  await field.pressSequentially('!');
  await expect.poll(() => ruleStates(page)).toEqual(['pending', 'met', 'met', 'met']);

  // Nine characters: the first line turns red, alone; no message under the field; no call.
  const before = platform.calls.length;
  await page.getByTestId('set-password-submit').click();
  await expect.poll(() => ruleStates(page)).toEqual(['unmet', 'met', 'met', 'met']);
  await expect(page.getByTestId('new-password-error')).toHaveCount(0);
  await expect(page.locator('scs-banner')).toHaveCount(0);
  await expect(page.getByTestId('set-password-submit')).toBeEnabled();
  expect(platform.calls.length).toBe(before);
  await expect(rule(page, 'length')).toContainText('غير متحقق');

  // The tenth character turns it green without another submit.
  await field.pressSequentially('x');
  await expect.poll(() => ruleStates(page)).toEqual(['met', 'met', 'met', 'met']);

  // A space is not a symbol, and an Arabic letter has no case: the lines say so as typed.
  await field.fill('Space only 123');
  await expect.poll(() => ruleStates(page)).toEqual(['met', 'met', 'met', 'unmet']);
  await field.fill('كلمةسرطويلةجدا١٢٣');
  await expect.poll(() => ruleStates(page)).toEqual(['met', 'unmet', 'unmet', 'unmet']);

  // The eye shows what was typed and sends nothing.
  await page.getByTestId('password-eye').click();
  await expect(field).toHaveAttribute('type', 'text');
  expect(platform.calls.length).toBe(before);
});

test('the current password is refused with its own message, and the link can still be used', async ({
  page,
}) => {
  const platform = await staffPlatform(page, { linkSet: passwordRefused('SAME_AS_CURRENT') });
  await openLink(page);

  await page.getByTestId('new-password').fill('Correct-Horse-9!');
  await page.getByTestId('set-password-submit').click();

  await expect(page.getByTestId('new-password-error')).toHaveText(
    'هذه هي كلمة المرور الحالية. يلزم اختيار كلمة مرور مختلفة عنها.',
  );
  await expect.poll(() => ruleStates(page)).toEqual(['met', 'met', 'met', 'met']);
  await expect(page.locator('scs-banner')).toHaveCount(0);
  await expect(page.getByTestId('set-password-submit')).toBeEnabled();

  // Typing again clears the message; a different password goes through with the same link.
  await page.getByTestId('new-password').fill(NEW);
  await expect(page.getByTestId('new-password-error')).toHaveCount(0);
  platform.linkSet = { status: 200, json: { kind: 'reset' } };
  await page.getByTestId('set-password-submit').click();
  await expect(page.getByTestId('banner-passwordChanged')).toBeVisible();
  expect(platform.bodies.at(-1)).toEqual({ token: TOKEN, password: NEW });
});

test('a rule the platform refuses is shown on its line even if the browser thought it met', async ({
  page,
}) => {
  await staffPlatform(page, { linkSet: passwordRefused('NO_SYMBOL', 'TOO_SHORT') });
  await openLink(page);

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();

  await expect.poll(() => ruleStates(page)).toEqual(['unmet', 'met', 'met', 'unmet']);
  await expect(page.getByTestId('new-password-error')).toHaveCount(0);
});

for (const dead of [
  { name: 'used, expired or replaced', reach: (page: Page) => openLink(page), calls: 1 },
  { name: 'made up', reach: (page: Page) => openLink(page, 'nonsense'), calls: 1 },
  {
    name: 'not shaped like a token',
    reach: (page: Page) => page.goto(`${CLINIC}/set-password#t=has spaces!`).then(() => undefined),
    calls: 0,
  },
  {
    name: 'missing',
    reach: (page: Page) => page.goto(`${CLINIC}/set-password`).then(() => undefined),
    calls: 0,
  },
]) {
  test(`a reset link that is ${dead.name} says so and offers a new one`, async ({ page }) => {
    const platform = await staffPlatform(page, {
      link: problem(410, 'LINK_INVALID', { kind: 'reset' }),
    });

    await dead.reach(page);

    await expect(page.getByTestId('dead-link-title')).toHaveText('انتهت صلاحية الرابط أو استُعمل');
    await expect(page.getByTestId('dead-link-title')).toBeFocused();
    await expect(page.getByTestId('dead-link-text')).toHaveText(
      'رابط إعادة تعيين كلمة المرور يعمل مرة واحدة ولمدة محدودة.',
    );
    await expect(page.locator('input')).toHaveCount(0);
    expect(platform.calls.filter((call) => call.includes('/password/')).length).toBe(dead.calls);

    await expect(page.getByTestId('back-to-sign-in')).toHaveAttribute('href', '/sign-in');
    await page.getByTestId('ask-again').click();
    await expect(page).toHaveURL(`${CLINIC}/forgot-password`);
    await expect(page.getByTestId('forgot-title')).toBeVisible();
  });
}

test('a link that runs out between opening and submitting shows the same screen, and nothing changed', async ({
  page,
}) => {
  await staffPlatform(page, { linkSet: problem(410, 'LINK_INVALID', { kind: 'reset' }) });
  await openLink(page);
  await expect(page.getByTestId('reset-title')).toBeVisible();

  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();

  await expect(page.getByTestId('dead-link-title')).toBeVisible();
  await expect(page.getByTestId('ask-again')).toBeVisible();
  await expect(page.locator('input')).toHaveCount(0);
});

test('a reload after opening holds nothing that opens the link again', async ({ page }) => {
  const platform = await staffPlatform(page);
  await openLink(page);
  await expect(page.getByTestId('reset-title')).toBeVisible();

  await page.reload();

  await expect(page.getByTestId('dead-link-title')).toBeVisible();
  expect(platform.calls.filter((call) => call === 'POST /v1/auth/password/link').length).toBe(1);
});

test('when the platform cannot be asked about a link, the link is kept and can be tried again', async ({
  page,
}) => {
  const platform = await staffPlatform(page);
  await page.route('**/v1/auth/password/link', unreachable);
  await openLink(page);

  await expect(page.getByTestId('banner-unreachable')).toBeVisible();
  await expect(page.getByTestId('dead-link-title')).toHaveCount(0);
  await expect(page.locator('input')).toHaveCount(0);

  await page.unroute('**/v1/auth/password/link', unreachable);
  await page.getByTestId('link-retry').click();
  await expect(page.getByTestId('reset-title')).toBeVisible();
  expect(platform.calls).not.toContain('POST /v1/auth/password/link/set');
});

test('the reset screens read the same in English, mirrored', async ({ page }) => {
  await useEnglish(page);
  await staffPlatform(page);

  await page.goto(`${CLINIC}/forgot-password`);
  await expect(page.getByTestId('forgot-title')).toHaveText('Reset the password');
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('email-error')).toHaveText('Email is required');
  await page.getByTestId('email').fill('owner@noor.example');
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('forgot-sent-title')).toHaveText('The request was sent');

  await openLink(page);
  await expect(page.getByTestId('reset-title')).toHaveText('Set a new password');
  await expect(page.getByTestId('password-rules').locator('li')).toHaveText([
    /At least 10 characters/,
    /An upper-case and a lower-case letter \(A-z\)/,
    /At least one digit \(0-9\)/,
    /At least one special character \(!@#\$%\)/,
  ]);
  await page.getByTestId('new-password').fill(NEW);
  await page.getByTestId('set-password-submit').click();
  await expect(page.getByTestId('banner-passwordChanged')).toContainText(
    'The password was changed',
  );
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
});
