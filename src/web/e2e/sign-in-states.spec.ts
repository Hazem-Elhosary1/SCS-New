import { Page, expect, test } from '@playwright/test';
import {
  CLINIC,
  StaffPlatform,
  pictureFor,
  problem,
  staffPlatform,
  submitSignIn,
  support,
  typeCode,
  useEnglish,
} from './staff-platform';

/**
 * Spec 003, User Story 8 (FR-046, FR-047, SC-010): every state of the sign-in screens exists in
 * Arabic and in English, inside the same frame, with the footer links, at most one banner, no
 * untranslated text and never a clinic's name.
 */
const instant = {
  status: 200,
  json: { expiresInSeconds: 600, resendAvailableInSeconds: 0, resendsLeft: 3 },
};

interface State {
  name: string;
  /** How the platform answers. */
  platform?: Partial<StaffPlatform>;
  /** Brings the page to the state. */
  reach: (page: Page) => Promise<void>;
  /** A test id that is visible in this state. */
  shows: string;
  /** Whether the state carries a banner that reports a failure. */
  failure?: boolean;
}

const form = async (page: Page) => {
  await page.goto(`${CLINIC}/sign-in`);
};

const submitted = async (page: Page) => {
  await form(page);
  await submitSignIn(page);
};

const atCode = async (page: Page) => {
  await submitted(page);
  await expect(page.getByTestId('code-title')).toBeVisible();
};

const states: State[] = [
  { name: 'default', reach: form, shows: 'sign-in-title' },
  {
    name: 'session ended',
    reach: (page) => page.goto(`${CLINIC}/sign-in?ended=1`).then(() => undefined),
    shows: 'banner-ended',
  },
  {
    name: 'filled',
    reach: async (page) => {
      await form(page);
      await page.getByTestId('email').fill('owner@noor.example');
      await page.getByTestId('password').fill('Correct-Horse-9!');
    },
    shows: 'sign-in-submit',
  },
  {
    name: 'password shown',
    reach: async (page) => {
      await form(page);
      await page.getByTestId('password').fill('Correct-Horse-9!');
      await page.getByTestId('password-eye').click();
    },
    shows: 'eye-crossed',
  },
  {
    name: 'validation',
    reach: async (page) => {
      await form(page);
      await page.getByTestId('sign-in-submit').click();
    },
    shows: 'email-error',
  },
  {
    name: 'error',
    platform: { signIn: problem(401, 'INVALID_CREDENTIALS') },
    reach: submitted,
    shows: 'banner-invalid',
    failure: true,
  },
  {
    name: 'request failed',
    reach: async (page) => {
      await page.route('**/v1/auth/sign-in', (route) =>
        route.request().method() === 'POST' ? route.abort() : route.fallback(),
      );
      await submitted(page);
    },
    shows: 'banner-unreachable',
    failure: true,
  },
  {
    name: 'code not sent',
    platform: { signIn: problem(503, 'SIGN_IN_CODE_NOT_SENT') },
    reach: submitted,
    shows: 'banner-codeNotSent',
    failure: true,
  },
  {
    name: 'account locked',
    platform: {
      signIn: problem(403, 'ACCOUNT_LOCKED', { lockedUntil: '2026-10-10T15:45:00.000Z' }),
    },
    reach: submitted,
    shows: 'banner-locked',
    failure: true,
  },
  {
    name: 'account disabled',
    platform: { signIn: problem(403, 'ACCOUNT_DISABLED') },
    reach: submitted,
    shows: 'banner-disabled',
    failure: true,
  },
  {
    name: 'clinic suspended',
    platform: { signIn: problem(403, 'CLINIC_SUSPENDED') },
    reach: submitted,
    shows: 'banner-suspended',
    failure: true,
  },
  {
    name: 'network blocked',
    platform: { signIn: problem(403, 'NETWORK_BLOCKED') },
    reach: submitted,
    shows: 'banner-blocked',
    failure: true,
  },
  {
    name: 'too many requests',
    platform: { signIn: { ...problem(429, 'RATE_LIMITED'), headers: { 'retry-after': '40' } } },
    reach: submitted,
    shows: 'banner-rateLimited',
    failure: true,
  },
  { name: 'code entry', reach: atCode, shows: 'code-box-0' },
  {
    name: 'code error',
    platform: { code: problem(401, 'CODE_INVALID') },
    reach: async (page) => {
      await atCode(page);
      await typeCode(page, '000000');
      await page.getByTestId('code-submit').click();
    },
    shows: 'banner-invalid',
    failure: true,
  },
  {
    name: 'code step, platform unreachable',
    reach: async (page) => {
      await atCode(page);
      await page.route('**/v1/auth/sign-in/code', (route) =>
        route.request().method() === 'POST' ? route.abort() : route.fallback(),
      );
      await typeCode(page, '123456');
      await page.getByTestId('code-submit').click();
    },
    shows: 'banner-unreachable',
    failure: true,
  },
  {
    name: 'resend failed',
    platform: { signIn: instant, resend: problem(503, 'SIGN_IN_CODE_NOT_RESENT') },
    reach: async (page) => {
      await atCode(page);
      await page.getByTestId('resend-button').click();
    },
    shows: 'resend-button',
    failure: true,
  },
  {
    name: 'no more resends',
    platform: {
      signIn: {
        status: 200,
        json: { expiresInSeconds: 600, resendAvailableInSeconds: 0, resendsLeft: 0 },
      },
    },
    reach: atCode,
    shows: 'no-more-resends',
  },
  {
    name: 'code accepted',
    reach: async (page) => {
      // Hold the way inside, so the accepted state stays on screen.
      await page.route('**/v1/meta/version', () => undefined);
      await atCode(page);
      await page.addStyleTag({ content: 'app-landing-page { display: none !important; }' });
      await typeCode(page, '123456');
      await page.evaluate(() => {
        const push = history.pushState.bind(history);
        history.pushState = () => undefined;
        setTimeout(() => (history.pushState = push), 3000);
      });
    },
    shows: 'code-title',
  },
  {
    name: 'help window',
    reach: async (page) => {
      await form(page);
      await page.getByTestId('help-link').click();
    },
    shows: 'support-hotline',
  },
  {
    name: 'help window from the code step',
    reach: async (page) => {
      await atCode(page);
      await page.getByTestId('help-link').click();
    },
    shows: 'support-whatsapp',
  },
];

for (const language of ['ar', 'en'] as const) {
  test.describe(`sign-in states in ${language}`, () => {
    for (const state of states) {
      test(state.name, async ({ page }) => {
        if (language === 'en') {
          await useEnglish(page);
        }
        await staffPlatform(page, state.platform);

        await state.reach(page);
        await expect(page.getByTestId(state.shows).first()).toBeVisible();
        await pictureFor(page, 'sign-in', language, state.name);

        // The language and its direction; the introduction panel on the end side.
        await expect(page.locator('html')).toHaveAttribute('lang', language);
        await expect(page.locator('html')).toHaveAttribute(
          'dir',
          language === 'ar' ? 'rtl' : 'ltr',
        );
        const panel = await page.getByTestId('auth-form-panel').boundingBox();
        const intro = await page.getByTestId('auth-intro').boundingBox();
        expect(intro, 'the introduction panel is shown at this width').not.toBeNull();
        if (language === 'ar') {
          expect(intro!.x).toBeLessThan(panel!.x);
        } else {
          expect(intro!.x).toBeGreaterThan(panel!.x);
        }

        // The footer of every sign-in screen.
        await expect(page.getByTestId('help-link')).toBeAttached();
        await expect(page.getByTestId('privacy-link')).toHaveAttribute('href', '/privacy');
        await expect(page.getByTestId('terms-link')).toHaveAttribute('href', '/terms');

        // One banner at most, and never beside a field error.
        const banners = await page.locator('scs-banner').count();
        expect(banners).toBeLessThanOrEqual(1);
        if (banners === 1) {
          await expect(page.locator('[data-testid$="-error"]')).toHaveCount(0);
          await expect(page.locator('scs-banner')).not.toBeEmpty();
        }

        const text = await page.locator('body').innerText();

        // Nothing before sign-in names a clinic, and no translation key is shown raw.
        for (const name of ['عيادة النور', 'Al Noor Clinic', 'noor.example/']) {
          expect(text).not.toContain(name);
        }
        expect(text).not.toMatch(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);

        // A page in one language has no sentence left in the other.
        if (language === 'en') {
          // The switch names the other language in that language; nothing else is Arabic.
          const other = await page.getByTestId('language-switch').innerText();
          const body = text.replace(support.hoursAr, '').replace(other, '');
          expect(body).not.toMatch(/[؀-ۿ]{3,}/);
        } else {
          expect(text).not.toMatch(/\b(Sign in|Password|Confirm code|Back to sign-in)\b/);
        }
      });
    }

    test('the help window: hours, three channels that leave the application, and × back to the same screen', async ({
      page,
    }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
      await staffPlatform(page);
      await form(page);
      await page.getByTestId('email').fill('owner@noor.example');

      await page.getByTestId('help-link').click();

      await expect(page.getByTestId('dialog-title')).toHaveText(
        language === 'ar' ? 'تحتاج مساعدة؟' : 'Need help?',
      );
      await expect(page.getByTestId('support-hours')).toHaveText(
        language === 'ar' ? support.hoursAr : support.hoursEn,
      );
      await expect(page.getByTestId('support-hotline')).toHaveAttribute('href', 'tel:19999');
      await expect(page.getByTestId('support-whatsapp')).toHaveAttribute(
        'href',
        'https://wa.me/201000000000',
      );
      await expect(page.getByTestId('support-email')).toHaveAttribute(
        'href',
        'mailto:support@scs.example',
      );
      await expect(page.locator('dialog form, dialog input')).toHaveCount(0);

      await page.getByTestId('dialog-close').click();
      await expect(page.getByTestId('dialog')).not.toBeVisible();
      await expect(page.getByTestId('email')).toHaveValue('owner@noor.example');
      await expect(page.getByTestId('help-link')).toBeFocused();

      // Escape closes it as well.
      await page.getByTestId('help-link').click();
      await expect(page.getByTestId('dialog')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('dialog')).not.toBeVisible();
    });

    test('the line about a new account stays while typing', async ({ page }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
      await staffPlatform(page);
      await form(page);

      await page.getByTestId('email').pressSequentially('owner@');
      await page.getByTestId('password').pressSequentially('abc');

      await expect(page.getByTestId('new-account-hint')).toBeVisible();
      await expect(page.getByTestId('forgot-password')).toHaveAttribute('href', '/forgot-password');
    });
  });
}

test('the links that later specs fill lead to a page that says so', async ({ page }) => {
  await staffPlatform(page);

  for (const path of ['/privacy', '/terms']) {
    await page.goto(`${CLINIC}${path}`);
    await expect(page.getByTestId('not-yet')).toContainText('هذه الصفحة غير متاحة بعد');
  }
});
