import { Page, expect, test } from '@playwright/test';
import { OPS, opsEnglish, opsOperator, opsPicture, opsPlatform, opsProblem, opsProfile } from './ops-platform';

// The tests' browser is an Arabic one unless a test says otherwise: before sign-in the console
// follows the browser when nothing was chosen in it (FR-054).
test.use({ locale: 'ar-EG' });

/**
 * Spec 010, inside the operators console: the frame around every page, and the operator's own
 * profile — account details, recent sessions, signing out everywhere, and changing the
 * password with a code.
 */
async function inside(page: Page, start: Parameters<typeof opsPlatform>[1] = {}, path = '/') {
  const platform = await opsPlatform(page, { signedIn: true, ...start });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${OPS}${path}`);
  await expect(page.getByTestId('ops-top-bar')).toBeVisible();
  return platform;
}

const english = {
  operator: { ...opsOperator, fullName: 'Ahmed Sami', language: 'en' as const, roleName: 'Super Admin' },
};

test.describe('the frame of the console', () => {
  test('in Arabic: the name, the operator, and only the sections that exist', async ({ page }) => {
    await inside(page);

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByTestId('ops-brand')).toContainText('SCS — لوحة التشغيل');
    await expect(page.getByTestId('ops-person')).toHaveText('أحمد سامي');
    await expect(page.getByTestId('ops-role')).toHaveText('مسؤول عام');
    await expect(page.getByTestId('ops-initials')).toHaveText('أس');

    const items = page.getByTestId('ops-side-bar').locator('a');
    await expect(items).toHaveCount(1);
    await expect(items.first()).toHaveText('الرئيسية');
    await expect(items.first()).toHaveClass(/\bon\b/);
    // Marked for a screen reader as well as for the eye, and only while it is the page shown.
    await expect(items.first()).toHaveAttribute('aria-current', 'page');
    await page.goto(`${OPS}/profile`);
    await expect(page.getByTestId('profile-title')).toBeVisible();
    await expect(page.getByTestId('ops-nav-home')).not.toHaveAttribute('aria-current', 'page');
    await page.getByTestId('ops-nav-home').click();
    await expect(page.getByTestId('ops-nav-home')).toHaveAttribute('aria-current', 'page');

    // The side bar is on the right in Arabic.
    const bar = (await page.getByTestId('ops-side-bar').boundingBox())!;
    expect(bar.x).toBeGreaterThan(700);

    await expect(page.getByTestId('ops-home-title')).toContainText('مرحبًا،');
    await expect(page.getByTestId('ops-home-title')).toContainText('أحمد سامي');
    await opsPicture(page, 'ar', 'home');
  });

  test('in English, after sign-in the console speaks the account’s language', async ({ page }) => {
    await inside(page, english);

    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByTestId('ops-brand')).toContainText('SCS — Ops Console');
    await expect(page.getByTestId('ops-role')).toHaveText('Super Admin');
    await expect(page.getByTestId('ops-initials')).toHaveText('AS');
    expect((await page.getByTestId('ops-side-bar').boundingBox())!.x).toBeLessThan(10);
    await opsPicture(page, 'en', 'home');
  });

  test('the account menu, by pointer and by keyboard', async ({ page }) => {
    await inside(page);

    await page.getByTestId('ops-account').click();
    const menu = page.getByTestId('ops-account-menu');
    await expect(menu.getByRole('menuitem')).toHaveText(['الملف الشخصي', 'تسجيل الخروج']);
    await opsPicture(page, 'ar', 'account-menu');

    // A press elsewhere closes it; so does Escape, which gives the focus back.
    await page.getByTestId('ops-home-title').click();
    await expect(menu).toHaveCount(0);

    await page.getByTestId('ops-account').focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('ops-menu-profile')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('ops-menu-sign-out')).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(menu).toHaveCount(0);
    await expect(page.getByTestId('ops-account')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(`${OPS}/profile`);
  });

  test('signing out shows the sign-in screen, and going back shows nothing of the console', async ({ page }) => {
    const platform = await inside(page);

    await page.getByTestId('ops-account').click();
    await page.getByTestId('ops-menu-sign-out').click();

    await expect(page).toHaveURL(`${OPS}/sign-in`);
    await expect(page.getByTestId('sign-in-notice')).toHaveCount(0);
    expect(platform.calls).toContain('POST /v1/ops/auth/sign-out');

    await page.goBack();
    await expect(page.getByTestId('sign-in-title')).toBeVisible();
    await expect(page.getByTestId('ops-top-bar')).toHaveCount(0);
  });

  test('a second tab follows a sign-out in the first', async ({ page, context }) => {
    await inside(page);
    const second = await context.newPage();
    await opsPlatform(second, { signedIn: true });
    await second.goto(`${OPS}/`);
    await expect(second.getByTestId('ops-top-bar')).toBeVisible();

    await page.getByTestId('ops-account').click();
    await page.getByTestId('ops-menu-sign-out').click();
    await expect(second.getByTestId('sign-in-title')).toBeVisible();
  });

  test('a session ended elsewhere is shown with its reason on a console nobody touches', async ({ page }) => {
    await page.clock.install();
    const platform = await inside(page);

    // Somebody signs in on another device.
    platform.signedIn = false;
    platform.endedFor = 'replaced';
    await page.clock.fastForward(21_000);

    await expect(page.getByTestId('sign-in-notice')).toHaveText('تم تسجيل الدخول من جهاز آخر، فانتهت هذه الجلسة.');
    // What the console asked by itself was marked as not the operator's activity.
    expect(platform.background).toContain('GET /v1/ops/auth/session');
  });

  test('a console left untouched for the idle period signs itself out', async ({ page }) => {
    await page.clock.install();
    const platform = await inside(page, { idleMinutes: 1 });

    await page.clock.fastForward(61_000);

    await expect(page.getByTestId('sign-in-notice')).toHaveText('انتهت الجلسة. الدخول من جديد للمتابعة.');
    expect(platform.calls).toContain('POST /v1/ops/auth/sign-out');
  });

  test('an address that does not exist stays in the frame, with a way home', async ({ page }) => {
    // Spec 015 made «/clinics» a page; this address is still nowhere.
    await inside(page, {}, '/no-such-page');

    await expect(page.getByTestId('ops-not-found')).toHaveText('الصفحة غير موجودة');
    await page.getByTestId('ops-not-found-home').click();
    await expect(page.getByTestId('ops-home-title')).toBeVisible();
  });

  // Found on the real stack (spec 095, T073): the guard asked for the router after waiting, and
  // the screen failed instead of going home.
  for (const path of ['/sign-in', '/forgot-password']) {
    test(`a signed-in operator who opens ${path} is taken home, with no failure`, async ({ page }) => {
      const failures: string[] = [];
      page.on('pageerror', (error) => failures.push(error.message));
      page.on('request', (request) => {
        if (request.url().includes('client-errors')) failures.push('reported: ' + request.postData());
      });
      await inside(page);
      await page.goto(`${OPS}${path}`);

      await expect(page.getByTestId('ops-home-title')).toBeVisible();
      await expect(page).toHaveURL(`${OPS}/`);
      expect(failures).toEqual([]);
    });
  }

  test('at a narrow width the side bar is behind a control and nothing scrolls sideways', async ({ page }) => {
    await inside(page);
    await page.setViewportSize({ width: 390, height: 800 });

    await expect(page.getByTestId('ops-side-bar')).toBeHidden();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    await page.getByTestId('ops-nav-opener').click();
    await expect(page.getByTestId('ops-nav-home')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('ops-side-bar')).toBeHidden();
  });

  test('a long name is shortened in the top bar and never wraps it', async ({ page }) => {
    await inside(page, { operator: { ...opsOperator, fullName: 'عبد الرحمن '.repeat(12) } });
    expect((await page.getByTestId('ops-top-bar').boundingBox())!.height).toBe(64);
  });
});

test.describe('my profile', () => {
  test('in Arabic, as the design writes it', async ({ page }) => {
    await inside(page, {}, '/profile');

    await expect(page.getByTestId('profile-title')).toHaveText('الملف الشخصي');
    await expect(page.getByText('إدارة معلومات حسابك الشخصي وإعدادات الأمان.')).toBeVisible();
    await expect(page.getByTestId('profile-name')).toHaveText('أحمد سامي');
    await expect(page.getByTestId('profile-role')).toHaveText('مسؤول عام');
    await expect(page.getByTestId('profile-email')).toHaveText('ahmed.sami@smartclinicos.com');
    await expect(page.getByTestId('profile-mobile')).toHaveText('+20 100 000 0000');
    await expect(page.getByTestId('change-password')).toHaveText('تغيير كلمة المرور');

    await expect(page.getByTestId('two-factor')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('two-factor')).toBeDisabled();
    await expect(page.getByTestId('two-factor')).toHaveAccessibleDescription(/لا يمكن إيقافه من هنا/);
    await expect(page.getByTestId('two-factor-reason')).toHaveText(
      'مطلوب لكل المشغلين من الإعدادات ← الأمان، لذا لا يمكن إيقافه من هنا.',
    );
    await expect(page.getByTestId('one-session')).toHaveText(
      'جلسة واحدة فقط في كل مرة — تسجيل الدخول من جهاز آخر يُنهي هذه الجلسة.',
    );

    const rows = page.getByTestId('session-row');
    await expect(rows).toHaveCount(3);
    await expect(rows.nth(0)).toContainText('Chrome على Windows');
    await expect(rows.nth(0)).toContainText('نشط الآن');
    await expect(rows.nth(0)).toContainText('الجلسة الحالية');
    await expect(rows.nth(1)).toContainText('انتهت منذ ساعتين');
    await expect(rows.nth(2)).toContainText('انتهت منذ يومين');
    await expect(page.getByTestId('sign-out-everywhere')).toHaveText('تسجيل الخروج من كل الأجهزة');
    await opsPicture(page, 'ar', 'profile');
  });

  test('in English', async ({ page }) => {
    await inside(
      page,
      {
        ...english,
        profile: () => ({
          status: 200,
          json: {
            ...opsProfile(),
            fullName: 'Ahmed Sami',
            roleName: 'Super Admin',
            sessions: opsProfile().sessions.map((s) => ({ ...s, device: s.device.replace(' على ', ' on ') })),
          },
        }),
      },
      '/profile',
    );

    await expect(page.getByTestId('profile-title')).toHaveText('My Profile');
    await expect(page.getByTestId('two-factor-reason')).toHaveText(
      'Required for every operator in Settings → Security, so it can’t be turned off here.',
    );
    await expect(page.getByTestId('session-row').nth(0)).toContainText('Active now');
    await expect(page.getByTestId('session-row').nth(1)).toContainText('Ended 2 hours ago');
    await opsPicture(page, 'en', 'profile');
  });

  test('a profile that cannot be read says so and can be tried again', async ({ page }) => {
    const platform = await inside(page, { profile: opsProblem(503, 'SERVICE_UNAVAILABLE') }, '/profile');
    await expect(page.locator('scs-state')).toBeVisible();

    platform.profile = () => ({ status: 200, json: opsProfile() });
    await page.locator('scs-state').getByRole('button').click();
    await expect(page.getByTestId('profile-name')).toBeVisible();
  });

  test('changing the password: each wrong input, then the code, then done', async ({ page }) => {
    const platform = await inside(page, {}, '/profile');
    await page.getByTestId('change-password').click();
    const dialog = page.getByTestId('change-dialog');

    await expect(dialog).toContainText('تأكد من استخدام كلمة مرور قوية لحماية حسابك.');
    await expect(page.getByTestId('change-hint')).toHaveText(
      '10 أحرف على الأقل، تتضمن حروفًا كبيرة وصغيرة ورقمًا ورمزًا.',
    );
    await opsPicture(page, 'ar', 'change-open');

    // Nothing in the dialog is cut off or scrolls sideways, and its buttons are whole.
    const fits = () =>
      page.getByTestId('dialog').filter({ has: dialog }).evaluate((element) => ({
        sideways: element.scrollWidth > element.clientWidth,
        box: element.getBoundingClientRect().toJSON() as { left: number; right: number },
        inside: [...element.querySelectorAll('input, button')].map((control) => control.getBoundingClientRect().toJSON() as { left: number; right: number }),
      }));
    const open = await fits();
    expect(open.sideways).toBe(false);
    for (const control of open.inside) {
      expect(control.left).toBeGreaterThanOrEqual(open.box.left);
      expect(control.right).toBeLessThanOrEqual(open.box.right);
    }

    // The two new passwords differ; and the rules are not met: nothing is sent.
    await page.getByTestId('current-password').fill('Operator-Horse-9!');
    await page.getByTestId('new-password').fill('short');
    await page.getByTestId('confirm-password').fill('other');
    await page.getByTestId('change-save').click();
    await expect(dialog).toContainText('كلمتا المرور غير متطابقتين، يرجى المحاولة مرة أخرى.');
    await expect(page.getByTestId('change-hint')).toHaveClass(/unmet/);
    expect(platform.calls).not.toContain('POST /v1/ops/profile/password');
    await opsPicture(page, 'ar', 'change-error');

    await page.getByTestId('new-password').fill('Changed-Horse-11!');
    await page.getByTestId('confirm-password').fill('Changed-Horse-11!');

    platform.change = { status: 400, code: 'VALIDATION_FAILED', extra: { errors: { current: ['WRONG'] } } };
    await page.getByTestId('change-save').click();
    await expect(dialog).toContainText('كلمة المرور الحالية غير صحيحة.');

    platform.change = { status: 400, code: 'VALIDATION_FAILED', extra: { errors: { password: ['SAME_AS_CURRENT'] } } };
    await page.getByTestId('change-save').click();
    await expect(dialog).toContainText('يجب أن تختلف كلمة المرور الجديدة عن الحالية.');

    // Right: nothing changes yet — the code is asked for.
    platform.change = () => ({ status: 202, json: { email: 'ahmed.sami@smartclinicos.com', expiresAt: new Date(Date.now() + 600_000).toISOString(), resendAt: new Date(Date.now() + 60_000).toISOString(), resendsLeft: 3, now: new Date().toISOString() } });
    await page.getByTestId('change-save').click();
    await expect(page.getByTestId('change-code-email')).toHaveText('ahmed.sami@smartclinicos.com');
    expect(platform.bodies.at(-1)).toEqual({ current: 'Operator-Horse-9!', password: 'Changed-Horse-11!' });
    await opsPicture(page, 'ar', 'change-code');
    expect((await fits()).sideways).toBe(false);

    platform.changeConfirm = opsProblem(401, 'CODE_INVALID', { attemptsLeft: 4 });
    await dialog.locator('scs-code-input input').first().click();
    await page.keyboard.type('111111');
    await expect(page.getByTestId('code-problem')).toBeVisible();

    platform.changeConfirm = { status: 204 };
    await dialog.locator('scs-code-input input').first().click();
    await page.keyboard.type('427913');
    await expect(page.getByTestId('change-done')).toHaveText('تم تغيير كلمة المرور بنجاح');
    await expect(dialog).toContainText('يمكنك الآن استخدام كلمة المرور الجديدة لتسجيل الدخول.');
    await opsPicture(page, 'ar', 'change-success');

    await page.getByTestId('change-done-close').click();
    await expect(dialog).toBeHidden();
    await expect(page.getByTestId('ops-top-bar')).toBeVisible();
  });

  test('closing the dialog while the code is awaited abandons the change', async ({ page }) => {
    const platform = await inside(page, {}, '/profile');
    await page.getByTestId('change-password').click();
    await page.getByTestId('current-password').fill('Operator-Horse-9!');
    await page.getByTestId('new-password').fill('Changed-Horse-11!');
    await page.getByTestId('confirm-password').fill('Changed-Horse-11!');
    await page.getByTestId('change-save').click();
    await expect(page.getByTestId('change-code-email')).toBeVisible();

    await page.getByTestId('change-cancel').click();
    await expect(page.getByTestId('change-dialog')).toBeHidden();
    expect(platform.calls).toContain('POST /v1/ops/profile/password/cancel');
  });

  for (const step of ['form', 'code'] as const) {
    test(`a session that ends while the change-password dialog is at its ${step} closes it with the sign-in screen`, async ({ page }) => {
      await page.clock.install();
      const platform = await inside(page, {}, '/profile');
      await page.getByTestId('change-password').click();
      await page.getByTestId('current-password').fill('Operator-Horse-9!');
      await page.getByTestId('new-password').fill('Changed-Horse-11!');
      await page.getByTestId('confirm-password').fill('Changed-Horse-11!');
      if (step === 'code') {
        await page.getByTestId('change-save').click();
        await expect(page.getByTestId('change-code-email')).toBeVisible();
      }

      // Somebody signs in on another device while the dialog is open.
      platform.signedIn = false;
      platform.endedFor = 'replaced';
      await page.clock.fastForward(21_000);

      await expect(page).toHaveURL(`${OPS}/sign-in`);
      await expect(page.getByTestId('sign-in-notice')).toHaveText('تم تسجيل الدخول من جهاز آخر، فانتهت هذه الجلسة.');
      // The dialog went with the page: none is open, nothing dims the sign-in form, and the
      // form can be typed in.
      await expect(page.getByTestId('change-dialog')).toHaveCount(0);
      await expect(page.locator('dialog[open]')).toHaveCount(0);
      await page.getByTestId('email').fill('ahmed.sami@smartclinicos.com');
      await expect(page.getByTestId('email')).toHaveValue('ahmed.sami@smartclinicos.com');
      // And nothing was changed.
      expect(platform.calls).not.toContain('POST /v1/ops/profile/password/confirm');
    });
  }

  test('the fifth wrong current password ends the session, and the sign-in screen says why', async ({ page }) => {
    const platform = await inside(page, {}, '/profile');
    await page.getByTestId('change-password').click();
    await page.getByTestId('current-password').fill('Wrong-Horse-9!');
    await page.getByTestId('new-password').fill('Changed-Horse-11!');
    await page.getByTestId('confirm-password').fill('Changed-Horse-11!');

    platform.change = () => {
      platform.signedIn = false;
      return opsProblem(401, 'SESSION_ENDED', { reason: 'locked' });
    };
    await page.getByTestId('change-save').click();

    await expect(page).toHaveURL(`${OPS}/sign-in`);
    await expect(page.getByTestId('sign-in-notice')).toContainText('تم قفل الحساب بعد محاولات خاطئة');
  });

  test('signing out of all devices asks first, then shows the sign-in screen', async ({ page }) => {
    const platform = await inside(page, {}, '/profile');

    await page.getByTestId('sign-out-everywhere').click();
    await expect(page.getByTestId('everywhere-dialog')).toContainText('تنتهي كل جلسات حسابك');
    expect(
      await page
        .getByTestId('dialog')
        .filter({ has: page.getByTestId('everywhere-dialog') })
        .evaluate((element) => element.scrollWidth > element.clientWidth),
    ).toBe(false);
    expect(platform.calls).not.toContain('POST /v1/ops/auth/sign-out-everywhere');

    await page.getByTestId('everywhere-confirm').click();
    await expect(page).toHaveURL(`${OPS}/sign-in`);
    expect(platform.calls).toContain('POST /v1/ops/auth/sign-out-everywhere');
  });
});
