import { expect, test } from '@playwright/test';
import {
  CLINIC,
  problem,
  session,
  staffPlatform,
  submitSignIn,
  typeCode,
  useEnglish,
} from './staff-platform';

/**
 * Spec 003, User Stories 1 to 7: signing in with email, password and an email code; what each
 * refusal looks like; sessions ending and renewing. The platform's answers are given by the
 * test (contracts/sign-in-screens.md says which answer shows which state).
 */

test.describe('step one', () => {
  test('without a session any page of the console shows the sign-in form', async ({ page }) => {
    await staffPlatform(page);

    await page.goto(`${CLINIC}/patients/123?tab=visits`);

    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.getByTestId('sign-in-title')).toHaveText('تسجيل دخول فريق العيادة');
    await expect(page.getByTestId('email')).toBeFocused();
    await expect(page.getByTestId('new-account-hint')).toHaveText(
      'حساب جديد؟ افتح الرابط في بريدك لتعيين كلمة المرور.',
    );
  });

  test('empty fields and a malformed email are caught before any call', async ({ page }) => {
    const platform = await staffPlatform(page);
    await page.goto(`${CLINIC}/sign-in`);

    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('email-error')).toHaveText('البريد مطلوب');
    await expect(page.getByTestId('password-error')).toHaveText('كلمة المرور مطلوبة');
    await expect(page.locator('scs-banner')).toHaveCount(0);

    await page.getByTestId('email').fill('no-at-sign');
    await page.getByTestId('password').fill('x');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('email-error')).toHaveText(
      'البريد غير صحيح — مثل name@clinic.com',
    );
    await expect(page.getByTestId('password-error')).toHaveCount(0);

    expect(platform.calls.filter((call) => call.startsWith('POST'))).toEqual([]);
  });

  test('both steps lead inside, and the form is not shown again to a signed-in person', async ({
    page,
  }) => {
    const platform = await staffPlatform(page);
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    platform.signIn = () => ({
      status: 200,
      json: { expiresInSeconds: 600, resendAvailableInSeconds: 60, resendsLeft: 3 },
    });
    await page.route('**/v1/auth/sign-in', async (route) => {
      if (route.request().method() === 'POST') {
        await held;
      }
      await route.fallback();
    });

    await page.goto(`${CLINIC}/sign-in`);
    await page.getByTestId('email').fill('owner@noor.example');
    await page.getByTestId('password').fill('Correct-Horse-9!');
    await page.getByTestId('password').press('Enter');

    // While the platform is thinking the button says so and is locked.
    await expect(page.getByTestId('sign-in-submit')).toHaveText('جارٍ تسجيل الدخول…');
    await expect(page.getByTestId('sign-in-submit')).toBeDisabled();
    release();

    await expect(page).toHaveURL(`${CLINIC}/sign-in/code`);
    await expect(page.getByTestId('code-title')).toHaveText('التحقق بخطوتين');
    await expect(page.getByTestId('code-email')).toHaveText('owner@noor.example');
    await expect(page.getByTestId('code-box-0')).toBeFocused();
    expect(platform.bodies[0]).toEqual({
      email: 'owner@noor.example',
      password: 'Correct-Horse-9!',
    });
    expect(platform.lastHeaders['x-scs-request']).toBe('1');

    await typeCode(page, '123456');
    await page.getByTestId('code-submit').click();

    await expect(page.getByTestId('signed-in-email')).toHaveText(session.email);
    await expect(page.getByTestId('signed-in-clinic')).toHaveText('عيادة النور');
    await expect(page).toHaveURL(`${CLINIC}/`);
    expect(platform.bodies[1]).toEqual({ code: '123456' });

    await page.goto(`${CLINIC}/sign-in`);
    await expect(page).toHaveURL(`${CLINIC}/`);
    await expect(page.getByTestId('signed-in')).toBeVisible();
  });

  test('the eye shows and hides the password and sends nothing', async ({ page }) => {
    const platform = await staffPlatform(page);
    await page.goto(`${CLINIC}/sign-in`);
    await page.getByTestId('password').fill('Correct-Horse-9!');

    await page.getByTestId('password-eye').click();
    await expect(page.getByTestId('password')).toHaveAttribute('type', 'text');
    await expect(page.getByTestId('eye-crossed')).toBeVisible();

    await page.getByTestId('password-eye').click();
    await expect(page.getByTestId('password')).toHaveAttribute('type', 'password');
    expect(platform.calls.filter((call) => call.startsWith('POST'))).toEqual([]);
  });

  test('a refusal is one banner above the fields, which keep what was typed', async ({ page }) => {
    const platform = await staffPlatform(page, { signIn: problem(401, 'INVALID_CREDENTIALS') });
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page, 'owner@noor.example', 'Wrong-Password-1!');

    await expect(page.getByTestId('banner-invalid')).toHaveText(
      'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
    );
    await expect(page.locator('scs-banner')).toHaveCount(1);
    await expect(page.getByTestId('email')).toHaveValue('owner@noor.example');
    await expect(page.getByTestId('password')).toHaveValue('Wrong-Password-1!');
    await expect(page.getByTestId('sign-in-submit')).toBeEnabled();
    await expect(page.getByTestId('new-account-hint')).toBeVisible();
    await expect(page.getByTestId('email-error')).toHaveCount(0);

    // A field error replaces the banner: the two are never shown together.
    await page.getByTestId('email').fill('');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('email-error')).toBeVisible();
    await expect(page.locator('scs-banner')).toHaveCount(0);
    expect(platform.calls.filter((call) => call === 'POST /v1/auth/sign-in')).toHaveLength(1);
  });

  test('a locked account is told when it opens, with the time kept on one line', async ({
    page,
  }) => {
    await staffPlatform(page, {
      signIn: problem(403, 'ACCOUNT_LOCKED', { lockedUntil: '2026-10-10T15:45:00.000Z' }),
    });
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);

    const banner = page.getByTestId('banner-locked');
    await expect(banner).toContainText('أُقفل الحساب بعد محاولات دخول فاشلة. يُفتح بعد الساعة');
    await expect(banner).toContainText('نسيت كلمة المرور؟');
    await expect(banner).toHaveAttribute('data-tone', 'warning');

    const time = page.getByTestId('locked-until');
    await expect(time).not.toBeEmpty();
    expect(await time.evaluate((element) => getComputedStyle(element).whiteSpace)).toBe('nowrap');
  });

  test('a suspended clinic is told support is the only way, and the action opens the help window', async ({
    page,
  }) => {
    await staffPlatform(page, { signIn: problem(403, 'CLINIC_SUSPENDED') });
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);

    const banner = page.getByTestId('banner-suspended');
    await expect(banner).toContainText('اشتراك العيادة موقوف');
    await expect(banner).not.toContainText('فاتورة');
    await expect(banner).toHaveAttribute('data-tone', 'danger');

    await page.getByTestId('banner-action').click();
    await expect(page.getByTestId('dialog-title')).toHaveText('تحتاج مساعدة؟');
  });

  test('too many requests says when to try again, on the same form', async ({ page }) => {
    await staffPlatform(page, {
      signIn: { ...problem(429, 'RATE_LIMITED'), headers: { 'retry-after': '40' } },
    });
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);

    await expect(page.getByTestId('banner-rateLimited')).toHaveAttribute('data-tone', 'warning');
    await expect(page.getByTestId('retry-after')).toHaveText('40');
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  });

  test('when the platform cannot be reached the form says so', async ({ page }) => {
    await staffPlatform(page);
    await page.route('**/v1/auth/sign-in', (route) =>
      route.request().method() === 'POST' ? route.abort() : route.fallback(),
    );
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);

    await expect(page.getByTestId('banner-unreachable')).toHaveText(
      'تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.',
    );
    await expect(page.getByTestId('sign-in-submit')).toBeEnabled();
  });

  test('when the code cannot be sent there is no code step', async ({ page }) => {
    await staffPlatform(page, { signIn: problem(503, 'SIGN_IN_CODE_NOT_SENT') });
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);

    await expect(page.getByTestId('banner-codeNotSent')).toHaveText(
      'لم نستطع إرسال رمز التحقق إلى بريدك. حاول مرة أخرى بعد قليل.',
    );
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  });
});

test.describe('step two', () => {
  async function atCodeStep(
    page: import('@playwright/test').Page,
    start: Parameters<typeof staffPlatform>[1] = {},
  ) {
    const platform = await staffPlatform(page, start);
    await page.goto(`${CLINIC}/sign-in`);
    await submitSignIn(page);
    await expect(page.getByTestId('code-title')).toBeVisible();
    return platform;
  }

  test('a wrong code turns the boxes red with the one message', async ({ page }) => {
    await atCodeStep(page, { code: problem(401, 'CODE_INVALID') });
    await typeCode(page, '000000');
    await page.getByTestId('code-submit').click();

    await expect(page.getByTestId('banner-invalid')).toHaveText(
      'الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.',
    );
    await expect(page.locator('scs-code-input')).toHaveAttribute('data-state', 'error');
    await expect(page.getByTestId('code-submit')).toBeEnabled();
    await expect(page.getByTestId('back-to-sign-in')).toBeVisible();
  });

  test('when the platform cannot be reached the code is not called wrong', async ({ page }) => {
    await atCodeStep(page);
    await page.route('**/v1/auth/sign-in/code', (route) =>
      route.request().method() === 'POST' ? route.abort() : route.fallback(),
    );
    await typeCode(page, '123456');
    await page.getByTestId('code-submit').click();

    await expect(page.getByTestId('banner-unreachable')).toHaveText(
      'تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.',
    );
    await expect(page.getByTestId('banner-invalid')).toHaveCount(0);
    await expect(page.locator('scs-code-input')).toHaveAttribute('data-state', 'idle');
    await expect(page.getByTestId('code-box-5')).toHaveValue('6');
    await expect(page.getByTestId('code-submit')).toBeEnabled();
  });

  test('the boxes read left to right in Western digits in Arabic, and a pasted code fills them', async ({
    page,
  }) => {
    await atCodeStep(page);

    await page.getByTestId('code-box-0').click();
    await page.keyboard.type('١٢٣');
    await expect(page.getByTestId('code-box-0')).toHaveValue('1');
    await expect(page.getByTestId('code-box-2')).toHaveValue('3');
    await expect(page.getByTestId('code-box-3')).toBeFocused();

    const first = await page.getByTestId('code-box-0').boundingBox();
    const last = await page.getByTestId('code-box-5').boundingBox();
    expect(first!.x).toBeLessThan(last!.x);
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  test('the countdown runs, then a resend restarts it without asking for the password', async ({
    page,
  }) => {
    const platform = await atCodeStep(page, {
      signIn: {
        status: 200,
        json: { expiresInSeconds: 600, resendAvailableInSeconds: 2, resendsLeft: 3 },
      },
    });

    await expect(page.getByTestId('resend-countdown')).toContainText('إعادة الإرسال خلال');
    await expect(page.getByTestId('resend-button')).toHaveCount(0);

    await expect(page.getByTestId('resend-button')).toHaveText('إعادة إرسال الرمز', {
      timeout: 5000,
    });
    await page.getByTestId('resend-button').click();

    await expect(page.getByTestId('resend-countdown')).toBeVisible();
    await expect(page).toHaveURL(`${CLINIC}/sign-in/code`);
    expect(platform.calls).toContain('POST /v1/auth/sign-in/code/resend');
    expect(platform.calls.filter((call) => call === 'POST /v1/auth/sign-in')).toHaveLength(1);
  });

  test('a resend that fails keeps the earlier code and offers the button', async ({ page }) => {
    await atCodeStep(page, {
      signIn: {
        status: 200,
        json: { expiresInSeconds: 600, resendAvailableInSeconds: 0, resendsLeft: 3 },
      },
      resend: problem(503, 'SIGN_IN_CODE_NOT_RESENT'),
    });

    await page.getByTestId('resend-button').click();

    await expect(page.getByTestId('banner-resendFailed')).toHaveText(
      'لم نستطع إرسال رمز جديد. الرمز السابق يبقى صالحًا حتى تنتهي مدّته.',
    );
    await expect(page.getByTestId('resend-button')).toHaveText('إعادة إرسال الرمز');
    await expect(page.getByTestId('code-submit')).toBeEnabled();
  });

  test('after the last resend the link gives way to a note, and the code still works', async ({
    page,
  }) => {
    await atCodeStep(page, {
      signIn: {
        status: 200,
        json: { expiresInSeconds: 600, resendAvailableInSeconds: 0, resendsLeft: 1 },
      },
      resend: {
        status: 200,
        json: { expiresInSeconds: 500, resendAvailableInSeconds: 0, resendsLeft: 0 },
      },
    });

    await page.getByTestId('resend-button').click();

    await expect(page.getByTestId('no-more-resends')).toBeVisible();
    await expect(page.getByTestId('resend-button')).toHaveCount(0);

    await typeCode(page, '123456');
    await page.getByTestId('code-submit').click();
    await expect(page.getByTestId('signed-in')).toBeVisible();
  });

  test('"back to sign-in" returns to the form, and a reload of the code page does too', async ({
    page,
  }) => {
    await atCodeStep(page);

    await page.getByTestId('back-to-sign-in').click();
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);

    await submitSignIn(page);
    await expect(page.getByTestId('code-title')).toBeVisible();
    await page.reload();
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  });

  test('an account disabled between the two steps is sent back to the form, told why', async ({
    page,
  }) => {
    await atCodeStep(page, { code: problem(403, 'ACCOUNT_DISABLED') });
    await typeCode(page, '123456');
    await page.getByTestId('code-submit').click();

    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.getByTestId('banner-disabled')).toContainText('معطَّل');
    await expect(page.getByTestId('banner-disabled')).not.toContainText('لم يُفعَّل');
  });
});

test.describe('sessions', () => {
  test('sign-out leads to the plain form', async ({ page }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await page.goto(`${CLINIC}/`);
    await expect(page.getByTestId('signed-in')).toBeVisible();

    await page.getByTestId('sign-out').click();

    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.locator('scs-banner')).toHaveCount(0);
    expect(platform.calls).toContain('POST /v1/auth/sign-out');
  });

  test('a call told the session has ended leads to the form with the neutral notice', async ({
    page,
  }) => {
    const platform = await staffPlatform(page);
    platform.sessionAnswer = problem(401, 'SESSION_ENDED');

    await page.goto(`${CLINIC}/patients`);

    await expect(page.getByTestId('banner-ended')).toHaveText(
      'انتهت الجلسة. الدخول من جديد للمتابعة.',
    );
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.getByTestId('signed-in')).toHaveCount(0);
    expect(platform.calls).not.toContain('POST /v1/auth/session/renew');
  });

  test('a page already on screen is cleared when a later call says the session has ended', async ({
    page,
  }) => {
    const platform = await staffPlatform(page, { signedIn: true });

    // The page's own status call fails at first, which leaves a "try again" button on screen:
    // a real action that makes the page ask the platform once more.
    let ended = false;
    await page.route('**/v1/meta/version', (route) => {
      if (route.request().method() === 'OPTIONS') {
        return route.fallback();
      }
      if (!ended) {
        return route.abort();
      }
      return route.fulfill({
        status: 401,
        headers: {
          'access-control-allow-origin': CLINIC,
          'access-control-allow-credentials': 'true',
          'content-type': 'application/problem+json',
        },
        body: JSON.stringify({
          type: 'urn:scs:error:session-ended',
          title: 'انتهت الجلسة. الدخول من جديد للمتابعة.',
          status: 401,
          code: 'SESSION_ENDED',
          traceId: 'a'.repeat(32),
        }),
      });
    });

    await page.goto(`${CLINIC}/`);
    await expect(page.getByTestId('signed-in-email')).toHaveText(session.email);
    await expect(page.getByTestId('platform-unreachable')).toBeVisible();

    // The session is ended elsewhere; the person's next action is refused.
    ended = true;
    platform.signedIn = false;
    await page.getByTestId('platform-unreachable').getByTestId('state-retry').click();

    await expect(page.getByTestId('banner-ended')).toHaveText(
      'انتهت الجلسة. الدخول من جديد للمتابعة.',
    );
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.getByTestId('signed-in')).toHaveCount(0);
    const text = await page.locator('body').innerText();
    expect(text).not.toContain(session.email);
    expect(text).not.toContain('عيادة النور');
    expect(text).not.toContain('Al Noor Clinic');
  });

  test('the notice is shown once and the address forgets it', async ({ page }) => {
    await staffPlatform(page);

    await page.goto(`${CLINIC}/sign-in?ended=1`);

    await expect(page.getByTestId('banner-ended')).toHaveText(
      'انتهت الجلسة. الدخول من جديد للمتابعة.',
    );
    await expect(page.getByTestId('banner-ended')).toHaveAttribute('data-tone', 'neutral');
    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  });

  test('an expired access is renewed once and the page carries on', async ({ page }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    let expired = true;
    platform.renew = () => {
      expired = false;
      return { status: 204 };
    };
    await page.route('**/v1/auth/session', async (route) => {
      if (route.request().method() === 'GET' && expired) {
        await route.fulfill({
          status: 401,
          headers: {
            'access-control-allow-origin': CLINIC,
            'access-control-allow-credentials': 'true',
            'content-type': 'application/problem+json',
          },
          body: JSON.stringify({
            type: 'urn:scs:error:access-expired',
            title: 'x',
            status: 401,
            code: 'ACCESS_EXPIRED',
            traceId: 'a'.repeat(32),
          }),
        });
      } else {
        await route.fallback();
      }
    });

    await page.goto(`${CLINIC}/`);

    await expect(page.getByTestId('signed-in-email')).toHaveText(session.email);
    expect(platform.calls.filter((call) => call === 'POST /v1/auth/session/renew')).toHaveLength(1);
  });

  test('a renewal that is refused shows the form with the notice', async ({ page }) => {
    const platform = await staffPlatform(page, {
      signedIn: true,
      renew: problem(401, 'SESSION_ENDED'),
    });
    platform.sessionAnswer = problem(401, 'ACCESS_EXPIRED');

    await page.goto(`${CLINIC}/`);

    await expect(page.getByTestId('sign-in-title')).toBeVisible();
    await expect(page.getByTestId('signed-in')).toHaveCount(0);
  });

  test('a second tab follows a sign-out in the first', async ({ context }) => {
    const first = await context.newPage();
    const second = await context.newPage();
    await staffPlatform(first, { signedIn: true });
    await staffPlatform(second, { signedIn: true });
    await first.goto(`${CLINIC}/`);
    await second.goto(`${CLINIC}/`);
    await expect(second.getByTestId('signed-in')).toBeVisible();

    await first.getByTestId('sign-out').click();

    await expect(second).toHaveURL(`${CLINIC}/sign-in`);
    await expect(second.getByTestId('signed-in')).toHaveCount(0);
  });
});

test('everything above reads the same way in English', async ({ page }) => {
  await useEnglish(page);
  await staffPlatform(page, { signIn: problem(401, 'INVALID_CREDENTIALS') });
  await page.goto(`${CLINIC}/sign-in`);

  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(page.getByTestId('sign-in-title')).toHaveText('Staff sign-in');
  await submitSignIn(page);
  await expect(page.getByTestId('banner-invalid')).toHaveText(
    'The email or password is incorrect.',
  );
});
