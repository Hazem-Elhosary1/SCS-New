import { Locator, Page, chromium, expect, test } from '@playwright/test';
import {
  OPS,
  OpsPlatform,
  opsEnglish,
  opsLooksWrong,
  opsOperator,
  opsPicture,
  opsPlatform,
  opsProblem,
  opsProfile,
} from './ops-platform';

// The tests' browser is an Arabic one unless a test says otherwise: before sign-in the console
// follows the browser when nothing was chosen in it (FR-054).
test.use({ locale: 'ar-EG' });

/**
 * Spec 010, how the operators console looks: every screen and every state of it, in Arabic and
 * in English, is opened at the design's size and at a phone's width and checked for what an
 * eye would catch — nothing cut off, nothing scrolling sideways, nothing outside its frame —
 * and for where the design puts things. With `SCS_SHOTS` set, each state is also pictured, to
 * be laid beside its frame in the design file.
 */
const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };

const words = {
  ar: { operator: opsOperator, on: ' على ' },
  en: {
    operator: { ...opsOperator, fullName: 'Ahmed Sami', language: 'en' as const, roleName: 'Super Admin' },
    on: ' on ',
  },
};

type Language = keyof typeof words;

/** The state as it is: whole at the design's size and at a phone's, and pictured. */
async function look(page: Page, language: Language, state: string): Promise<void> {
  await page.setViewportSize(NARROW);
  expect(await opsLooksWrong(page), `${language} ${state}, narrow`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/narrow/${language}-${state}.png` });
  }
  await page.setViewportSize(WIDE);
  expect(await opsLooksWrong(page), `${language} ${state}, wide`).toEqual([]);
  await opsPicture(page, language, state);
}

const box = async (locator: Locator) => (await locator.boundingBox())!;
const middle = async (locator: Locator) => {
  const { x, width } = await box(locator);
  return x + width / 2;
};

async function start(page: Page, language: Language, with_: Partial<OpsPlatform> = {}): Promise<OpsPlatform> {
  if (language === 'en') {
    await opsEnglish(page);
  }
  await page.setViewportSize(WIDE);
  return opsPlatform(page, {
    operator: words[language].operator,
    profile: () => ({
      status: 200,
      json: {
        ...opsProfile(),
        fullName: words[language].operator.fullName,
        roleName: words[language].operator.roleName,
        sessions: opsProfile().sessions.map((session) => ({
          ...session,
          device: session.device.replace(' على ', words[language].on),
        })),
      },
    }),
    ...with_,
  });
}

for (const language of ['ar', 'en'] as const) {
  test.describe(`how the console looks, ${language}`, () => {
    test('signing in: the form, what is refused, the lock, why a session ended', async ({ page }) => {
      const platform = await start(page, language, { endedFor: 'replaced' });
      await page.goto(`${OPS}/`);
      await expect(page.getByTestId('sign-in-notice')).toBeVisible();
      await look(page, language, 'sign-in-ended');

      await page.goto(`${OPS}/sign-in`);
      await expect(page.getByTestId('sign-in-title')).toBeVisible();
      await look(page, language, 'sign-in');

      // The brand panel is half the screen, and the form sits in the middle of the other half.
      const panel = await box(page.getByTestId('auth-intro'));
      expect(Math.abs(panel.width - WIDE.width / 2)).toBeLessThanOrEqual(2);
      const half = await box(page.getByTestId('auth-form-panel'));
      expect(Math.abs((await middle(page.getByTestId('sign-in-submit'))) - (half.x + half.width / 2))).toBeLessThanOrEqual(2);

      // Empty fields: each says so under itself.
      await page.getByTestId('sign-in-submit').click();
      await look(page, language, 'sign-in-empty');

      platform.signIn = opsProblem(401, 'INVALID_CREDENTIALS');
      await page.getByTestId('email').fill('ahmed.sami@smartclinicos.com');
      await page.getByTestId('password').fill('Operator-Horse-9!');
      await page.getByTestId('sign-in-submit').click();
      const banner = page.getByTestId('banner-invalid');
      await expect(banner).toBeVisible();
      await look(page, language, 'sign-in-error');

      // The banner is as wide as the fields, and its ✕ takes it away.
      const field = await box(page.getByTestId('sign-in-submit'));
      const refused = await box(banner);
      expect(Math.abs(refused.width - field.width)).toBeLessThanOrEqual(2);
      await banner.getByTestId('banner-close').click();
      await expect(banner).toHaveCount(0);

      platform.signIn = opsProblem(403, 'ACCOUNT_LOCKED', { lockedUntil: '2026-10-12T14:05:00Z' });
      await page.getByTestId('sign-in-submit').click();
      await expect(page.getByTestId('banner-locked')).toBeVisible();
      await look(page, language, 'sign-in-locked');

      platform.signIn = opsProblem(403, 'ACCOUNT_DISABLED');
      await page.getByTestId('sign-in-submit').click();
      await expect(page.getByTestId('banner-suspended')).toBeVisible();
      await look(page, language, 'sign-in-suspended');

      platform.signIn = opsProblem(429, 'RATE_LIMITED');
      await page.getByTestId('sign-in-submit').click();
      await expect(page.getByTestId('banner-rateLimited')).toBeVisible();
      await look(page, language, 'sign-in-rate-limited');
    });

    test('the code: asked, wrong, and ready to be sent again', async ({ page }) => {
      const platform = await start(page, language, {
        signIn: () => ({
          status: 200,
          json: {
            email: 'ahmed.sami@smartclinicos.com',
            expiresAt: new Date(Date.now() + 600_000).toISOString(),
            resendAt: new Date(Date.now() + 60_000).toISOString(),
            resendsLeft: 3,
            now: new Date().toISOString(),
          },
        }),
      });
      await page.goto(`${OPS}/sign-in`);
      await page.getByTestId('email').fill('ahmed.sami@smartclinicos.com');
      await page.getByTestId('password').fill('Operator-Horse-9!');
      await page.getByTestId('sign-in-submit').click();
      await expect(page.getByTestId('code-title')).toBeVisible();
      await look(page, language, 'code');

      // Six boxes in one row, together in the middle, no wider than the button under them.
      const boxes = page.locator('scs-code-input input');
      await expect(boxes).toHaveCount(6);
      const first = await box(boxes.first());
      const last = await box(boxes.last());
      expect(Math.abs(first.y - last.y)).toBeLessThanOrEqual(1);
      const submit = await box(page.getByTestId('code-submit'));
      const row = { left: Math.min(first.x, last.x), right: Math.max(first.x + first.width, last.x + last.width) };
      expect(row.left).toBeGreaterThanOrEqual(submit.x - 1);
      expect(row.right).toBeLessThanOrEqual(submit.x + submit.width + 1);
      expect(Math.abs((row.left + row.right) / 2 - (submit.x + submit.width / 2))).toBeLessThanOrEqual(1);
      expect(row.right - row.left).toBeLessThanOrEqual(380);

      platform.code = opsProblem(401, 'CODE_INVALID', { attemptsLeft: 4 });
      await boxes.first().click();
      await page.keyboard.type('111111');
      await expect(page.getByTestId('code-problem')).toBeVisible();
      await look(page, language, 'code-wrong');
    });

    test('a forgotten password: asking, and "check your email"', async ({ page }) => {
      const platform = await start(page, language);
      await page.clock.install();
      await page.goto(`${OPS}/forgot-password`);
      await expect(page.getByTestId('forgot-title')).toBeVisible();
      await look(page, language, 'forgot');

      // The way back is under the button, in its middle.
      expect(
        Math.abs((await middle(page.getByTestId('forgot-back'))) - (await middle(page.getByTestId('forgot-submit')))),
      ).toBeLessThanOrEqual(2);

      await page.getByTestId('forgot-submit').click();
      await look(page, language, 'forgot-empty');

      await page.getByTestId('email').fill('ahmed.sami@smartclinicos.com');
      await page.getByTestId('forgot-submit').click();
      await expect(page.getByTestId('forgot-sent-title')).toBeVisible();
      await look(page, language, 'forgot-sent');

      // Everything on "check your email" is in the middle, one under the other.
      const centre = await middle(page.getByTestId('forgot-sent'));
      for (const id of ['forgot-sent-title', 'forgot-wait', 'forgot-back']) {
        expect(Math.abs((await middle(page.getByTestId(id))) - centre), id).toBeLessThanOrEqual(2);
      }

      // The states of "send again": the link when the minute is over, a new link sent, and one
      // that could not be sent.
      await page.clock.fastForward(61_000);
      await expect(page.getByTestId('forgot-again')).toBeVisible();
      await look(page, language, 'forgot-sent-again');
      expect(Math.abs((await middle(page.getByTestId('forgot-again'))) - centre)).toBeLessThanOrEqual(2);

      await page.getByTestId('forgot-again').click();
      await expect(page.getByTestId('forgot-resent')).toBeVisible();
      await look(page, language, 'forgot-resent');

      await page.clock.fastForward(61_000);
      platform.forgot = opsProblem(429, 'RATE_LIMITED');
      await page.getByTestId('forgot-again').click();
      await expect(page.getByTestId('forgot-not-resent')).toBeVisible();
      await look(page, language, 'forgot-not-resent');
    });

    for (const kind of ['first', 'reset'] as const) {
      test(`choosing a password from a link (${kind}): the form, what is wrong, a dead link`, async ({ page }) => {
        const platform = await start(page, language, {
          link: { status: 200, json: { kind, email: 'sarah.ahmed@scs-platform.com' } },
        });
        await page.goto(`${OPS}/password/${kind}#token=abc123`);
        await expect(page.getByTestId('set-password-title')).toBeVisible();
        await look(page, language, `${kind}-password`);

        // The design's order: the email, the password, its confirmation, the rules, the button.
        const order = [
          page.getByTestId('link-email'),
          page.getByTestId('new-password'),
          page.getByTestId('confirm-password'),
          page.locator('scs-password-rules'),
          page.getByTestId('set-password-submit'),
        ];
        const tops = await Promise.all(order.map(async (part) => (await box(part)).y));
        expect(tops).toEqual([...tops].sort((a, b) => a - b));

        // The email reads from the start of the line, as the fields under it.
        const email = await box(page.getByTestId('link-email'));
        const field = await box(page.getByTestId('set-password-submit'));
        if (language === 'ar') {
          expect(field.x + field.width - (email.x + email.width)).toBeLessThan(40);
        } else {
          expect(email.x - field.x).toBeLessThan(40);
        }

        await page.getByTestId('new-password').fill('Operator-Horse-9!');
        await page.getByTestId('confirm-password').fill('Operator-Horse-8!');
        await page.getByTestId('set-password-submit').click();
        await look(page, language, `${kind}-password-mismatch`);

        platform.link = opsProblem(410, 'LINK_INVALID', { kind });
        await page.reload();
        await expect(page.getByTestId('link-gone-title')).toBeVisible();
        await look(page, language, `${kind}-link-gone`);

        // Its ways out begin at the same edge as its words.
        const title = await box(page.getByTestId('link-gone-title'));
        const out = await box(page.getByTestId('forgot-back'));
        if (language === 'ar') {
          expect(Math.abs(title.x + title.width - (out.x + out.width))).toBeLessThanOrEqual(2);
        } else {
          expect(Math.abs(title.x - out.x)).toBeLessThanOrEqual(2);
        }
      });
    }

    test('before sign-in: the page that is not available yet', async ({ page }) => {
      await start(page, language);
      await page.goto(`${OPS}/sign-in`);
      await page.getByTestId('privacy-link').click();
      await expect(page.getByTestId('ops-not-yet')).toBeVisible();
      await look(page, language, 'not-yet');
    });

    test('inside: home, the account menu, an address that does not exist', async ({ page }) => {
      await start(page, language, { signedIn: true });
      await page.goto(`${OPS}/`);
      await expect(page.getByTestId('ops-home-title')).toBeVisible();
      await look(page, language, 'home');

      await page.getByTestId('ops-account').click();
      const menu = page.getByTestId('ops-account-menu');
      await expect(menu).toBeVisible();
      await look(page, language, 'account-menu');

      // The menu hangs under the account, inside the window, with a line between its two items.
      const account = await box(page.getByTestId('ops-account'));
      const hung = await box(menu);
      expect(hung.y).toBeGreaterThanOrEqual(account.y + account.height);
      const line = await box(menu.locator('hr'));
      expect(line.y).toBeGreaterThan((await box(page.getByTestId('ops-menu-profile'))).y);
      expect(line.y).toBeLessThan((await box(page.getByTestId('ops-menu-sign-out'))).y);
      await page.keyboard.press('Escape');

      await page.goto(`${OPS}/nothing-here`);
      await expect(page.getByTestId('ops-not-found')).toBeVisible();
      await look(page, language, 'not-found');
    });

    test('my profile: the page, and each state of changing the password', async ({ page }) => {
      const platform = await start(page, language, { signedIn: true });
      await page.goto(`${OPS}/profile`);
      await expect(page.getByTestId('profile-name')).toBeVisible();
      await look(page, language, 'profile');

      // The cards fill the page's room, one under the other and all the same width.
      const account = await box(page.getByTestId('profile-account'));
      const security = await box(page.getByTestId('profile-security'));
      expect(account.width).toBeGreaterThan(1100);

      // In the account card the name comes first and the button is at the far end; and the
      // switch, which is on, has its knob at the right, as the design draws it.
      const name = await box(page.getByTestId('profile-name'));
      const email = await box(page.getByTestId('profile-email'));
      const change = await box(page.getByTestId('change-password'));
      const mobile = await box(page.getByTestId('profile-mobile'));
      // Each value begins at the same edge as the caption under it.
      for (const id of ['profile-email', 'profile-mobile']) {
        const edges = await page.getByTestId(id).evaluate((value) => {
          const range = document.createRange();
          range.selectNodeContents(value);
          const words = range.getBoundingClientRect();
          const caption = value.nextElementSibling!.getBoundingClientRect();
          return { words: [words.left, words.right], caption: [caption.left, caption.right] };
        });
        const side = language === 'ar' ? 1 : 0;
        expect(Math.abs(edges.words[side] - edges.caption[side]), id).toBeLessThanOrEqual(2);
      }
      if (language === 'ar') {
        expect(name.x).toBeGreaterThan(email.x + email.width);
        expect(change.x - account.x).toBeLessThan(40);
        // Name, email and mobile share the card's room: each begins well apart from the last.
        expect(name.x + name.width - (email.x + email.width)).toBeGreaterThan(250);
        expect(email.x + email.width - (mobile.x + mobile.width)).toBeGreaterThan(200);
      } else {
        expect(name.x + name.width).toBeLessThan(email.x);
        expect(account.x + account.width - (change.x + change.width)).toBeLessThan(40);
        expect(email.x - name.x).toBeGreaterThan(250);
        expect(mobile.x - email.x).toBeGreaterThan(200);
      }
      expect(
        await page.getByTestId('two-factor').evaluate((track) => getComputedStyle(track, '::after').right),
      ).toBe('2px');
      expect(Math.abs(account.width - security.width)).toBeLessThanOrEqual(1);
      expect(security.y).toBeGreaterThan(account.y + account.height);

      await page.getByTestId('change-password').click();
      const frame = page.getByTestId('dialog').filter({ has: page.getByTestId('change-dialog') });
      await expect(page.getByTestId('current-password')).toBeVisible();
      await look(page, language, 'change-open');

      // The design's dialog: 480 wide, in the middle of the window, its heading at the start of
      // the line and no ×; two buttons of one width across it, the saving one at the right.
      const dialog = await box(frame);
      expect(Math.abs(dialog.width - 480)).toBeLessThanOrEqual(1);
      // Its first line begins where the fields begin.
      expect(
        await page.getByTestId('change-dialog').locator('p').first().evaluate((line) => getComputedStyle(line).textAlign),
      ).toBe('start');
      expect(Math.abs(dialog.x + dialog.width / 2 - WIDE.width / 2)).toBeLessThanOrEqual(1);
      const heading = await box(frame.getByTestId('dialog-title'));
      const firstField = await box(page.getByTestId('current-password').locator('xpath=ancestor::scs-text-field'));
      if (language === 'ar') {
        expect(Math.abs(heading.x + heading.width - (firstField.x + firstField.width))).toBeLessThanOrEqual(2);
      } else {
        expect(Math.abs(heading.x - firstField.x)).toBeLessThanOrEqual(2);
      }
      await expect(frame.getByTestId('dialog-close')).toHaveCount(0);
      const save = await box(page.getByTestId('change-save'));
      const cancel = await box(page.getByTestId('change-cancel'));
      const field = await box(page.getByTestId('current-password').locator('xpath=ancestor::scs-text-field'));
      expect(Math.abs(save.width - cancel.width)).toBeLessThanOrEqual(1);
      expect(Math.abs(save.y - cancel.y)).toBeLessThanOrEqual(1);
      expect(Math.abs(Math.min(save.x, cancel.x) - field.x)).toBeLessThanOrEqual(1);
      expect(Math.abs(Math.max(save.x + save.width, cancel.x + cancel.width) - (field.x + field.width))).toBeLessThanOrEqual(1);
      expect(save.x).toBeGreaterThan(cancel.x);
      // And the keyboard meets them in the order they are read.
      const order = await frame.locator('.buttons button').evaluateAll((buttons) => buttons.map((button) => button.getAttribute('data-testid')));
      expect(order).toEqual(language === 'ar' ? ['change-save', 'change-cancel'] : ['change-cancel', 'change-save']);

      const colour = () => page.getByTestId('change-hint').evaluate((hint) => getComputedStyle(hint).color);
      const calm = await colour();
      await page.getByTestId('current-password').fill('Operator-Horse-9!');
      await page.getByTestId('new-password').fill('short');
      await page.getByTestId('confirm-password').fill('other');
      await page.getByTestId('change-save').click();
      await expect(page.getByTestId('change-hint')).toHaveClass(/unmet/);
      await look(page, language, 'change-error');
      // The rule that is not met is seen, not only marked: its line changes colour.
      expect(await colour()).not.toBe(calm);

      platform.change = { status: 400, code: 'VALIDATION_FAILED', extra: { errors: { current: ['WRONG'] } } };
      await page.getByTestId('new-password').fill('Changed-Horse-11!');
      await page.getByTestId('confirm-password').fill('Changed-Horse-11!');
      await page.getByTestId('change-save').click();
      await expect(page.getByTestId('current-password')).toHaveAttribute('aria-invalid', 'true');
      await look(page, language, 'change-wrong-current');

      platform.change = () => ({
        status: 202,
        json: {
          email: 'ahmed.sami@smartclinicos.com',
          expiresAt: new Date(Date.now() + 600_000).toISOString(),
          resendAt: new Date(Date.now() + 60_000).toISOString(),
          resendsLeft: 3,
          now: new Date().toISOString(),
        },
      });
      await page.getByTestId('change-save').click();
      await expect(page.getByTestId('change-code-email')).toBeVisible();
      await look(page, language, 'change-code');

      // The way out is as wide as the button above it, not a small one in a corner.
      const confirm = await box(frame.getByTestId('code-submit'));
      const out = await box(page.getByTestId('change-cancel'));
      expect(Math.abs(confirm.width - out.width)).toBeLessThanOrEqual(1);
      expect(Math.abs(confirm.x - out.x)).toBeLessThanOrEqual(1);

      platform.changeConfirm = opsProblem(401, 'CODE_INVALID', { attemptsLeft: 4 });
      await frame.locator('scs-code-input input').first().click();
      await page.keyboard.type('111111');
      await expect(page.getByTestId('code-problem')).toBeVisible();
      await look(page, language, 'change-code-wrong');

      platform.changeConfirm = { status: 204 };
      await frame.locator('scs-code-input input').first().click();
      await page.keyboard.type('427913');
      await expect(page.getByTestId('change-done')).toBeVisible();
      await look(page, language, 'change-success');

      // Done: no heading, the words in the middle, and one button across the dialog.
      await expect(frame.getByTestId('dialog-title')).toHaveCount(0);
      expect(Math.abs((await middle(page.getByTestId('change-done'))) - WIDE.width / 2)).toBeLessThanOrEqual(2);
      expect(Math.abs((await box(frame)).width - 440)).toBeLessThanOrEqual(1);
      const done = await box(page.getByTestId('change-done-close'));
      expect(done.width).toBeGreaterThan(380);
      expect(Math.abs(done.x + done.width / 2 - WIDE.width / 2)).toBeLessThanOrEqual(1);
    });

    test('my profile: "sign out of all devices" asks first', async ({ page }) => {
      await start(page, language, { signedIn: true });
      await page.goto(`${OPS}/profile`);
      await page.getByTestId('sign-out-everywhere').click();
      await expect(page.getByTestId('everywhere-confirm')).toBeVisible();
      await look(page, language, 'everywhere');

      const frame = page.getByTestId('dialog').filter({ has: page.getByTestId('everywhere-dialog') });
      const confirm = await box(page.getByTestId('everywhere-confirm'));
      expect(confirm.x).toBeGreaterThan((await box(page.getByTestId('everywhere-cancel'))).x);
      const buttons = frame.locator('.buttons button');
      const first = await box(buttons.nth(0));
      const second = await box(buttons.nth(1));
      expect(Math.abs(first.width - second.width)).toBeLessThanOrEqual(1);
      expect(Math.abs(first.y - second.y)).toBeLessThanOrEqual(1);
    });

    test('the frame with no connection, and the profile while it is being read', async ({ page, context }) => {
      await start(page, language, { signedIn: true });
      await page.goto(`${OPS}/`);
      await expect(page.getByTestId('ops-home-title')).toBeVisible();

      await context.setOffline(true);
      await expect(page.getByTestId('ops-offline')).toBeVisible();
      await look(page, language, 'offline');
      // The line is in the frame, above the page, and does not push the top bar out of sight.
      await expect(page.getByTestId('ops-top-bar')).toBeInViewport();
      await context.setOffline(false);
      await expect(page.getByTestId('ops-offline')).toHaveCount(0);

      // The profile's answer is held back: the page shows that it is reading, in the frame.
      let release = () => {};
      const held = new Promise<void>((resolve) => (release = resolve));
      await page.route('**/v1/ops/profile', async (route) => {
        await held;
        await route.fallback();
      });
      await page.getByTestId('ops-account').click();
      await page.getByTestId('ops-menu-profile').click();
      await expect(page.getByTestId('profile-title')).toBeVisible();
      await expect(page.locator('scs-state[data-state="loading"]')).toBeVisible();
      await look(page, language, 'profile-loading');
      release();
      await expect(page.getByTestId('profile-name')).toBeVisible();
    });

    test('my profile: when it cannot be read', async ({ page }) => {
      await start(page, language, { signedIn: true, profile: opsProblem(500, 'INTERNAL') });
      await page.goto(`${OPS}/profile`);
      await expect(page.getByTestId('profile-title')).toBeVisible();
      await expect(page.getByTestId('profile-name')).toHaveCount(0);
      await look(page, language, 'profile-unreadable');
    });
  });
}

test('in Edge, a password box shows one eye: the field’s own, not the browser’s beside it', async () => {
  // Edge draws its own "reveal" eye once something is typed; the other browsers never do, so
  // this is looked at in Edge itself, and skipped where Edge is not installed.
  const edge = await chromium.launch({ channel: 'msedge' }).catch(() => null);
  test.skip(edge === null, 'Edge is not installed here');
  const page = await edge!.newPage({ viewport: WIDE });
  try {
    await opsPlatform(page);
    await page.goto(`${OPS}/sign-in`);
    await page.addStyleTag({ content: 'input { caret-color: transparent !important; }' });
    const password = page.getByTestId('password');
    await password.click();
    await page.keyboard.type('ab');
    const field = password.locator('xpath=ancestor::scs-text-field');
    await expect(field.getByTestId('password-eye')).toBeVisible();

    // Taking the browser's eye away changes nothing, because it is not drawn.
    const asBuilt = await field.screenshot();
    await page.addStyleTag({ content: 'input::-ms-reveal, input::-ms-clear { display: none !important; }' });
    expect((await field.screenshot()).equals(asBuilt)).toBe(true);
  } finally {
    await edge!.close();
  }
});
