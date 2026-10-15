import { Page, expect, test } from '@playwright/test';
import { OPS, codeStep, opsEnglish, opsPicture, opsPlatform, opsProblem } from './ops-platform';

// The tests' browser is an Arabic one unless a test says otherwise: before sign-in the console
// follows the browser when nothing was chosen in it (FR-054).
test.use({ locale: 'ar-EG' });

/**
 * Spec 010, the screens before sign-in of the operators console: sign in, the code, a
 * forgotten password, and choosing a password from a link — each as its frame in the design
 * file writes it, in Arabic and in English.
 */
async function fillSignIn(page: Page, email = 'ahmed.sami@smartclinicos.com', password = 'Operator-Horse-9!') {
  await page.getByTestId('email').fill(email);
  await page.getByTestId('password').fill(password);
  await page.getByTestId('sign-in-submit').click();
}

async function typeCode(page: Page, code: string) {
  await page.locator('scs-code-input input').first().click();
  await page.keyboard.type(code);
}

test.describe('an operator signs in', () => {
  test('the screen in Arabic, as the design writes it', async ({ page }) => {
    await opsPlatform(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${OPS}/sign-in`);

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByTestId('sign-in-title')).toHaveText('تسجيل الدخول');
    await expect(page.getByText('ادخل ببيانات حساب التشغيل الخاص بك')).toBeVisible();
    await expect(page.getByTestId('auth-panel-title')).toHaveText('لوحة تشغيل SCS');
    await expect(page.getByTestId('auth-panel-line')).toHaveText(
      'إدارة كل عيادات المنصة، الباقات، التذاكر، والفريق من مكان واحد.',
    );
    await expect(page.getByText('تذكرني')).toBeVisible();
    await expect(page.getByTestId('forgot-password')).toHaveText('نسيت كلمة المرور؟');
    await expect(page.getByTestId('sign-in-submit')).toHaveText('تسجيل الدخول');
    await expect(page.getByTestId('privacy-link')).toBeVisible();
    await expect(page.getByTestId('help-link')).toHaveCount(0);

    // The form is on the right in Arabic, the brand panel on the left.
    const form = (await page.getByTestId('auth-form-panel').boundingBox())!;
    const panel = (await page.getByTestId('auth-intro').boundingBox())!;
    expect(form.x).toBeGreaterThan(panel.x);
    await opsPicture(page, 'ar', 'sign-in');
  });

  test('the screen in English', async ({ page }) => {
    await opsEnglish(page);
    await opsPlatform(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${OPS}/sign-in`);

    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByTestId('sign-in-title')).toHaveText('Sign In');
    await expect(page.getByText('Enter your operator account details')).toBeVisible();
    await expect(page.getByTestId('auth-panel-title')).toHaveText('SCS Ops Console');
    await expect(page.getByText('Remember me')).toBeVisible();
    await expect(page.getByTestId('forgot-password')).toHaveText('Forgot password?');
    await opsPicture(page, 'en', 'sign-in');
  });

  test('empty and malformed fields are said under them and nothing is sent', async ({ page }) => {
    const platform = await opsPlatform(page);
    await page.goto(`${OPS}/sign-in`);

    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByText('البريد مطلوب')).toBeVisible();
    await expect(page.getByText('كلمة المرور مطلوبة')).toBeVisible();

    await page.getByTestId('email').fill('not-an-address');
    await page.getByTestId('password').fill('x');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByText('البريد غير صحيح — مثل name@company.com')).toBeVisible();
    expect(platform.calls.filter((call) => call.startsWith('POST'))).toEqual([]);
  });

  test('email, password, the code, and the operator is inside the console', async ({ page }) => {
    const platform = await opsPlatform(page);
    await page.goto(`${OPS}/sign-in`);
    await page.getByTestId('remember').check();
    await fillSignIn(page);

    await expect(page).toHaveURL(`${OPS}/sign-in/code`);
    expect(platform.bodies[0]).toMatchObject({ email: 'ahmed.sami@smartclinicos.com', remember: true });
    expect(platform.lastHeaders['x-scs-request']).toBe('1');

    await expect(page.getByTestId('code-title')).toHaveText('التحقق بخطوتين');
    await expect(page.getByTestId('code-email')).toHaveText('ahmed.sami@smartclinicos.com');
    await expect(page.getByTestId('auth-panel-line')).toHaveText('طبقة حماية إضافية لحساب التشغيل الخاص بك.');
    await expect(page.getByTestId('code-wait')).toContainText('يمكنك إعادة إرسال الكود خلال');
    await expect(page.getByTestId('code-countdown')).toHaveText(/^00:[0-9]{2}$/);
    await expect(page.getByTestId('code-submit')).toHaveText('تحقق');
    await expect(page.getByTestId('code-back')).toHaveText('الرجوع لتسجيل الدخول');
    await opsPicture(page, 'ar', 'code');

    // The sixth digit sends.
    await typeCode(page, '427913');
    await expect(page.getByTestId('ops-top-bar')).toBeVisible();
    await expect(page).toHaveURL(`${OPS}/`);
    await expect(page.getByTestId('ops-operator-name')).toHaveText('أحمد سامي');
  });

  test('an address opened with no session returns to it after sign-in', async ({ page }) => {
    await opsPlatform(page);
    await page.goto(`${OPS}/profile`);
    await expect(page).toHaveURL(/\/sign-in\?next=%2Fprofile$/);

    await fillSignIn(page);
    await typeCode(page, '427913');
    await expect(page).toHaveURL(`${OPS}/profile`);
    await expect(page.getByTestId('profile-title')).toBeVisible();
  });

  test('a signed-in operator has nothing to do on the sign-in screen', async ({ page }) => {
    await opsPlatform(page, { signedIn: true });
    await page.goto(`${OPS}/sign-in`);
    await expect(page).toHaveURL(`${OPS}/`);
  });

  test('what the platform refuses is one banner, and the fields keep what was typed', async ({ page }) => {
    const platform = await opsPlatform(page, { signIn: opsProblem(401, 'INVALID_CREDENTIALS') });
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${OPS}/sign-in`);
    await fillSignIn(page);

    const invalid = page.getByTestId('banner-invalid');
    await expect(invalid).toContainText('بيانات الدخول غير صحيحة');
    await expect(invalid).toContainText('تأكد من البريد الإلكتروني وكلمة المرور وحاول مرة أخرى.');
    await expect(page.getByTestId('email')).toHaveValue('ahmed.sami@smartclinicos.com');
    await expect(page.getByTestId('password')).toHaveValue('Operator-Horse-9!');
    await opsPicture(page, 'ar', 'sign-in-error');

    platform.signIn = opsProblem(403, 'ACCOUNT_DISABLED');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('banner-suspended')).toHaveText('هذا الحساب موقوف.');

    platform.signIn = opsProblem(403, 'ACCOUNT_LOCKED', { lockedUntil: '2026-10-12T14:05:00Z' });
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('banner-locked')).toContainText('الحساب مقفل حتى');
    await expect(page.getByTestId('locked-until')).toHaveText(/^[0-9]+:[0-9]{2} [صم]$/);
    await expect(page.getByTestId('banner-locked')).toContainText('إعادة تعيين كلمة المرور تفتحه الآن');
    await opsPicture(page, 'ar', 'sign-in-locked');

    platform.signIn = opsProblem(503, 'SIGN_IN_CODE_NOT_SENT');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('banner-codeNotSent')).toBeVisible();

    platform.signIn = opsProblem(429, 'RATE_LIMITED');
    await page.getByTestId('sign-in-submit').click();
    await expect(page.getByTestId('banner-rateLimited')).toBeVisible();
  });

  test('with the platform out of reach the form says so and nothing is lost', async ({ page }) => {
    await opsPlatform(page);
    await page.goto(`${OPS}/sign-in`);
    await page.route('**/v1/ops/auth/sign-in', (route) => route.abort());
    await fillSignIn(page);
    await expect(page.getByTestId('banner-unreachable')).toHaveText('تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.');
    await expect(page.getByTestId('email')).toHaveValue('ahmed.sami@smartclinicos.com');
  });

  for (const [reason, words] of [
    ['replaced', 'تم تسجيل الدخول من جهاز آخر، فانتهت هذه الجلسة.'],
    ['idle', 'انتهت الجلسة. الدخول من جديد للمتابعة.'],
    ['password-reset', 'تمت إعادة تعيين كلمة المرور، فتم تسجيل خروجك. سجّل دخولك بكلمة المرور الجديدة.'],
    ['suspended', 'تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.'],
    ['role-changed', 'تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.'],
  ] as const) {
    test(`a session ended for "${reason}" says why on the sign-in screen, once`, async ({ page }) => {
      await opsPlatform(page, { endedFor: reason });
      await page.goto(`${OPS}/`);

      await expect(page).toHaveURL(`${OPS}/sign-in`);
      await expect(page.getByTestId('sign-in-notice')).toHaveText(words);

      await page.reload();
      await expect(page.getByTestId('sign-in-title')).toBeVisible();
      await expect(page.getByTestId('sign-in-notice')).toHaveCount(0);
    });
  }
});

test.describe('the code', () => {
  async function atCode(page: Page, start: Parameters<typeof opsPlatform>[1] = {}) {
    const platform = await opsPlatform(page, start);
    await page.goto(`${OPS}/sign-in`);
    await fillSignIn(page);
    await expect(page.getByTestId('code-title')).toBeVisible();
    return platform;
  }

  test('a wrong code is said and the boxes start empty again', async ({ page }) => {
    await atCode(page, { code: opsProblem(401, 'CODE_INVALID', { attemptsLeft: 4 }) });
    await typeCode(page, '111111');

    await expect(page.getByTestId('code-problem')).toHaveText('الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.');
    await expect(page.locator('scs-code-input input').first()).toHaveValue('');
    await opsPicture(page, 'ar', 'code-wrong');
  });

  test('a step that is over goes back to the form with its line', async ({ page }) => {
    await atCode(page, { code: opsProblem(401, 'STEP_EXPIRED') });
    await typeCode(page, '111111');

    await expect(page).toHaveURL(`${OPS}/sign-in`);
    await expect(page.getByTestId('sign-in-notice')).toHaveText('انتهت الجلسة. الدخول من جديد للمتابعة.');
  });

  test('"send again" appears when the wait is over, and sends a new code', async ({ page }) => {
    const platform = await atCode(page, { signIn: () => ({ status: 200, json: codeStep(1) }) });

    await expect(page.getByTestId('code-resend')).toBeVisible({ timeout: 5000 });
    await page.getByTestId('code-resend').click();
    await expect(page.getByTestId('code-wait')).toBeVisible();
    expect(platform.calls).toContain('POST /v1/ops/auth/code/resend');

    platform.resend = opsProblem(503, 'SIGN_IN_CODE_NOT_RESENT');
  });

  test('going back ends the step', async ({ page }) => {
    const platform = await atCode(page);
    await page.getByTestId('code-back').click();

    await expect(page).toHaveURL(`${OPS}/sign-in`);
    expect(platform.calls).toContain('POST /v1/ops/auth/code/cancel');

    // Opened with no sign-in in progress, the code screen is the form.
    await page.goto(`${OPS}/sign-in/code`);
    await expect(page).toHaveURL(`${OPS}/sign-in`);
  });

  test('a pasted code fills the boxes, and the group is named for a screen reader', async ({ page }) => {
    await atCode(page);
    await expect(page.getByRole('group', { name: 'رمز التحقق، ستة أرقام' })).toBeVisible();

    await page.locator('scs-code-input input').first().focus();
    await page.evaluate(() => {
      const data = new DataTransfer();
      data.setData('text/plain', '427913');
      document.activeElement!.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true }));
    });
    await expect(page.getByTestId('ops-top-bar')).toBeVisible();
  });
});

test.describe('a forgotten password, and choosing one from a link', () => {
  test('asking for a link, in Arabic, as the design writes it', async ({ page }) => {
    const platform = await opsPlatform(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.clock.install();
    await page.goto(`${OPS}/sign-in`);
    await page.getByTestId('forgot-password').click();

    await expect(page.getByTestId('forgot-title')).toHaveText('نسيت كلمة المرور');
    await expect(page.getByText('أدخل بريدك الإلكتروني وسنرسل لك رابط إعادة التعيين')).toBeVisible();
    await expect(page.getByTestId('forgot-submit')).toHaveText('إرسال رابط إعادة التعيين');
    await opsPicture(page, 'ar', 'forgot');

    await page.getByTestId('forgot-submit').click();
    await expect(page.getByText('البريد مطلوب')).toBeVisible();

    await page.getByTestId('email').fill('ahmed.sami@smartclinicos.com');
    await page.getByTestId('forgot-submit').click();

    await expect(page.getByTestId('forgot-sent-title')).toHaveText('تحقق من بريدك الإلكتروني');
    await expect(page.getByText('أرسلنا رابط إعادة تعيين كلمة المرور إلى')).toBeVisible();
    await expect(page.getByTestId('forgot-sent-to')).toHaveText('ahmed.sami@smartclinicos.com');
    await expect(page.getByText('لم يصلك البريد الإلكتروني؟')).toBeVisible();
    // "Send again" rests for a minute after each sending, and counts the minute down.
    const sendings = () => platform.calls.filter((call) => call === 'POST /v1/ops/password/forgot').length;
    await expect(page.getByTestId('forgot-again')).toHaveCount(0);
    await expect(page.getByTestId('forgot-wait')).toContainText('يمكنك إعادة الإرسال خلال');
    await expect(page.getByTestId('forgot-countdown')).toHaveText(/^0[01]:[0-9]{2}$/);
    // The countdown changes every second: it is in no live region, or a screen reader would
    // be made to speak sixty times a minute.
    const live = '[role="status"], [role="alert"], [aria-live]';
    expect(await page.getByTestId('forgot-countdown').evaluate((part, s) => part.closest(s) === null, live)).toBe(true);
    await page.clock.fastForward(20_000);
    await expect(page.getByTestId('forgot-countdown')).toHaveText(/^00:[34][0-9]$/);
    expect(sendings()).toBe(1);
    await opsPicture(page, 'ar', 'forgot-sent');

    // The minute over: the link is back, and pressing it says it is sending, then that it sent.
    await page.clock.fastForward(41_000);
    await expect(page.getByTestId('forgot-wait')).toHaveCount(0);
    await expect(page.getByTestId('forgot-again')).toHaveText('إعادة الإرسال');
    // Nor is the link: a control is not a status.
    expect(await page.getByTestId('forgot-again').evaluate((part, s) => part.closest(s) === null, live)).toBe(true);

    let answer = () => {};
    const held = new Promise<void>((resolve) => (answer = resolve));
    await page.route('**/v1/ops/password/forgot', async (route) => {
      if (route.request().method() === 'POST') {
        await held;
      }
      await route.fallback();
    });
    await page.getByTestId('forgot-again').click();
    await expect(page.getByTestId('forgot-sending')).toHaveText('جارٍ الإرسال…');
    // What happens is announced: "sending", then "sent", in one status that holds nothing else.
    await expect(page.getByTestId('forgot-said')).toHaveAttribute('role', 'status');
    await expect(page.getByTestId('forgot-said')).toHaveText('جارٍ الإرسال…');
    await expect(page.getByTestId('forgot-again')).toHaveCount(0);
    answer();
    await expect(page.getByTestId('forgot-resent')).toContainText('أرسلنا رابطًا جديدًا.');
    await expect(page.getByTestId('forgot-said')).toHaveText('أرسلنا رابطًا جديدًا.');
    await expect(page.getByTestId('forgot-countdown')).toBeVisible();
    expect(sendings()).toBe(2);

    // And when it cannot be sent, that is said, and the link is there to try again.
    await page.clock.fastForward(61_000);
    platform.forgot = opsProblem(429, 'RATE_LIMITED');
    await page.getByTestId('forgot-again').click();
    await expect(page.getByTestId('forgot-not-resent')).toBeVisible();
    await expect(page.getByTestId('forgot-again')).toBeVisible();
    await expect(page.getByTestId('forgot-resent')).toHaveCount(0);

    await page.getByTestId('forgot-back').click();
    await expect(page).toHaveURL(`${OPS}/sign-in`);
  });

  test('the first password: the email is shown, the rules turn, and the account is ready', async ({ page }) => {
    const platform = await opsPlatform(page, {
      link: { status: 200, json: { kind: 'first', email: 'sarah.ahmed@scs-platform.com' } },
    });
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${OPS}/password/first#token=abc123`);

    await expect(page.getByTestId('set-password-title')).toHaveText('تعيين كلمة المرور');
    await expect(page.getByTestId('link-email')).toHaveText('sarah.ahmed@scs-platform.com');
    await expect(page.getByText('10 أحرف على الأقل')).toBeVisible();
    await expect(page.getByTestId('set-password-submit')).toHaveText('تفعيل الحساب');
    expect(platform.bodies[0]).toEqual({ token: 'abc123' });
    await opsPicture(page, 'ar', 'first-password');

    // Nine characters: its rule is not met and nothing is sent.
    await page.getByTestId('new-password').fill('Short-9!x');
    await page.getByTestId('confirm-password').fill('Short-9!x');
    await page.getByTestId('set-password-submit').click();
    expect(platform.calls).not.toContain('POST /v1/ops/password/set');

    await page.getByTestId('new-password').fill('Operator-Horse-9!');
    await page.getByTestId('confirm-password').fill('Operator-Horse-8!');
    await page.getByTestId('set-password-submit').click();
    await expect(page.getByText('كلمتا المرور غير متطابقتين، يرجى المحاولة مرة أخرى.')).toBeVisible();

    await page.getByTestId('confirm-password').fill('Operator-Horse-9!');
    await page.getByTestId('set-password-submit').click();
    await expect(page).toHaveURL(`${OPS}/sign-in`);
    await expect(page.getByTestId('sign-in-notice')).toHaveText('تم تفعيل الحساب. سجّل دخولك الآن.');
    expect(platform.bodies.at(-1)).toEqual({ token: 'abc123', password: 'Operator-Horse-9!' });
  });

  test('a new password from a reset link, and the same password refused', async ({ page }) => {
    const platform = await opsPlatform(page, {
      set: { status: 400, code: 'VALIDATION_FAILED', extra: { errors: { password: ['SAME_AS_CURRENT'] } } },
    });
    await page.goto(`${OPS}/password/reset#token=abc123`);

    await expect(page.getByTestId('set-password-title')).toHaveText('كلمة مرور جديدة');
    await page.getByTestId('new-password').fill('Operator-Horse-9!');
    await page.getByTestId('confirm-password').fill('Operator-Horse-9!');
    await page.getByTestId('set-password-submit').click();
    await expect(page.getByText('يجب أن تختلف كلمة المرور الجديدة عن الحالية.')).toBeVisible();

    platform.set = { status: 204 };
    await page.getByTestId('new-password').fill('Replaced-Horse-10!');
    await page.getByTestId('confirm-password').fill('Replaced-Horse-10!');
    await page.getByTestId('set-password-submit').click();
    await expect(page.getByTestId('sign-in-notice')).toHaveText('تم تغيير كلمة المرور. سجّل دخولك بكلمة المرور الجديدة.');
  });

  test('a link that no longer works says so: a reset offers a new one, an invitation does not', async ({ page }) => {
    const platform = await opsPlatform(page, { link: opsProblem(410, 'LINK_INVALID', { kind: 'reset' }) });
    await page.goto(`${OPS}/password/reset#token=old`);

    await expect(page.getByTestId('link-gone-title')).toHaveText('هذا الرابط لم يعد صالحًا');
    await expect(page.getByTestId('link-new')).toHaveText('طلب رابط جديد');

    platform.link = opsProblem(410, 'LINK_INVALID', { kind: 'first' });
    await page.goto(`${OPS}/password/first#token=old`);
    await page.reload();
    await expect(page.getByTestId('link-gone-line')).toHaveText(
      'انتهت صلاحية الدعوة أو استُعملت من قبل. تُطلب دعوة جديدة ممّن أرسلها.',
    );
    await expect(page.getByTestId('link-new')).toHaveCount(0);
  });

  test('the privacy link leads to a page that says it is not available yet', async ({ page }) => {
    await opsPlatform(page);
    await page.goto(`${OPS}/sign-in`);
    await page.getByTestId('privacy-link').click();
    await expect(page.getByTestId('ops-not-yet')).toHaveText('غير متاحة بعد');
  });

  test('the screens work with the keyboard alone and at a narrow width', async ({ page }) => {
    await opsPlatform(page);
    await page.setViewportSize({ width: 390, height: 800 });
    await page.goto(`${OPS}/sign-in`);

    await expect(page.getByTestId('auth-intro')).toBeHidden();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    await page.getByTestId('email').focus();
    await page.keyboard.type('ahmed.sami@smartclinicos.com');
    await page.keyboard.press('Tab');
    // The show/hide control of the password sits after the field.
    await page.getByTestId('password').focus();
    await page.keyboard.type('Operator-Horse-9!');
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('code-title')).toBeVisible();
  });
});

test.describe('the language before sign-in, when none was chosen in this browser', () => {
  for (const [locale, direction, title] of [
    ['en-GB', 'ltr', 'Sign In'],
    ['ar-EG', 'rtl', 'تسجيل الدخول'],
    ['fr-FR', 'rtl', 'تسجيل الدخول'],
  ] as const) {
    test.describe(`a browser in ${locale}`, () => {
      test.use({ locale });

      test(`starts ${direction}`, async ({ page }) => {
        await opsPlatform(page);
        await page.goto(`${OPS}/sign-in`);
        await expect(page.locator('html')).toHaveAttribute('dir', direction);
        await expect(page.getByTestId('sign-in-title')).toHaveText(title);
      });
    });
  }

  test.describe('and a choice made on the screen', () => {
    test.use({ locale: 'en-GB' });

    test('is kept over the browser’s language on the next visit', async ({ page }) => {
      await opsPlatform(page);
      await page.goto(`${OPS}/sign-in`);
      await expect(page.getByTestId('sign-in-title')).toHaveText('Sign In');
      await page.getByTestId('language-switch').click();
      await expect(page.getByTestId('sign-in-title')).toHaveText('تسجيل الدخول');
      await page.reload();
      await expect(page.getByTestId('sign-in-title')).toHaveText('تسجيل الدخول');
    });
  });
});
