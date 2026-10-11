import { Page, Route, expect, test } from '@playwright/test';
import {
  CLINIC,
  StaffPlatform,
  pictureFor,
  TOKEN,
  newAccountLink,
  passwordRefused,
  problem,
  staffPlatform,
  support,
  useEnglish,
} from './staff-platform';

/**
 * Spec 004, User Story 7 (FR-039, SC-009): every state of the password screens exists in
 * Arabic and in English, inside the frame of the sign-in screens, with the footer links, at
 * most one banner, no untranslated text — and a clinic's name on the new-account form only.
 */
const NEW = 'Brand-New-Pass-7!';

interface State {
  name: string;
  platform?: Partial<StaffPlatform>;
  reach: (page: Page) => Promise<void>;
  /** A test id that is visible in this state. */
  shows: string;
  /** The one state where a clinic is named. */
  namesClinic?: boolean;
}

const unreachable = (route: Route) => route.abort();

const requestForm = async (page: Page) => {
  await page.goto(`${CLINIC}/forgot-password`);
};

const requested = async (page: Page) => {
  await requestForm(page);
  await page.getByTestId('email').fill('owner@noor.example');
  await page.getByTestId('forgot-submit').click();
};

const linkOpened = async (page: Page) => {
  await page.goto(`${CLINIC}/set-password#t=${TOKEN}`);
};

const linkSubmitted = (password: string) => async (page: Page) => {
  await linkOpened(page);
  await page.getByTestId('new-password').fill(password);
  await page.getByTestId('set-password-submit').click();
};

const held = async (page: Page) => {
  await page.goto(`${CLINIC}/change-password`);
};

const heldSubmitted = (password: string) => async (page: Page) => {
  await held(page);
  await page.getByTestId('new-password').fill(password);
  await page.getByTestId('change-password-submit').click();
};

/** Holds an answer back, so the state while waiting stays on screen. */
const stalled = (pattern: string, then: (page: Page) => Promise<void>) => async (page: Page) => {
  await page.route(pattern, () => undefined);
  await then(page);
};

const newAccount = { link: { status: 200, json: newAccountLink } };
const temporary = { signedIn: true, mustChange: true };
const limited = { ...problem(429, 'RATE_LIMITED'), headers: { 'Retry-After': '37' } };

const states: State[] = [
  // The request form (states 1 to 6 of contracts/password-screens.md).
  { name: 'request form', reach: requestForm, shows: 'forgot-title' },
  {
    name: 'request form, address missing',
    reach: async (page) => {
      await requestForm(page);
      await page.getByTestId('forgot-submit').click();
    },
    shows: 'email-error',
  },
  {
    name: 'request form, sending',
    reach: stalled('**/v1/auth/password/forgot', requested),
    shows: 'forgot-submit',
  },
  { name: 'request sent', reach: requested, shows: 'forgot-sent-title' },
  {
    name: 'request form, too many requests',
    platform: { forgot: limited },
    reach: requested,
    shows: 'banner-rateLimited',
  },
  {
    name: 'request form, platform unreachable',
    reach: async (page) => {
      await page.route('**/v1/auth/password/forgot', unreachable);
      await requested(page);
    },
    shows: 'banner-unreachable',
  },

  // The page an emailed link opens (states 7 to 14).
  {
    name: 'link, opening',
    reach: stalled('**/v1/auth/password/link', linkOpened),
    shows: 'link-loading',
  },
  { name: 'reset form', reach: linkOpened, shows: 'reset-title' },
  {
    name: 'new-account form',
    platform: newAccount,
    reach: linkOpened,
    shows: 'new-account-title',
    namesClinic: true,
  },
  { name: 'reset form, a rule refused', reach: linkSubmitted('short'), shows: 'rule-length' },
  {
    name: 'new-account form, a rule refused',
    platform: newAccount,
    reach: linkSubmitted('NoSymbolHere12'),
    shows: 'rule-symbol',
    namesClinic: true,
  },
  {
    name: 'reset form, same password refused',
    platform: { linkSet: passwordRefused('SAME_AS_CURRENT') },
    reach: linkSubmitted('Correct-Horse-9!'),
    shows: 'new-password-error',
  },
  {
    name: 'reset form, saving',
    reach: stalled('**/v1/auth/password/link/set', linkSubmitted(NEW)),
    shows: 'set-password-submit',
  },
  {
    name: 'reset form, too many requests',
    platform: { linkSet: limited },
    reach: linkSubmitted(NEW),
    shows: 'banner-rateLimited',
  },
  {
    name: 'reset form, platform unreachable',
    reach: async (page) => {
      await page.route('**/v1/auth/password/link/set', unreachable);
      await linkSubmitted(NEW)(page);
    },
    shows: 'banner-unreachable',
  },
  {
    name: 'link, platform unreachable',
    reach: async (page) => {
      await page.route('**/v1/auth/password/link', unreachable);
      await linkOpened(page);
    },
    shows: 'link-retry',
  },
  {
    name: 'reset link no longer works',
    platform: { link: problem(410, 'LINK_INVALID', { kind: 'reset' }) },
    reach: linkOpened,
    shows: 'ask-again',
  },
  {
    name: 'new-account link no longer works',
    platform: { link: problem(410, 'LINK_INVALID', { kind: 'new-account' }) },
    reach: linkOpened,
    shows: 'dead-link-text',
  },

  // Replacing a temporary password (states 15 and 16).
  { name: 'temporary password form', platform: temporary, reach: held, shows: 'temporary-title' },
  {
    name: 'temporary password, a rule refused',
    platform: temporary,
    reach: heldSubmitted('alllowercase1!'),
    shows: 'rule-cases',
  },
  {
    name: 'temporary password, same password refused',
    platform: { ...temporary, change: passwordRefused('SAME_AS_CURRENT') },
    reach: heldSubmitted('Typed-By-Manager-5!'),
    shows: 'new-password-error',
  },
  {
    name: 'temporary password, saving',
    platform: temporary,
    reach: stalled('**/v1/auth/password/change', heldSubmitted(NEW)),
    shows: 'change-password-submit',
  },
  {
    name: 'temporary password, platform unreachable',
    platform: temporary,
    reach: async (page) => {
      await page.route('**/v1/auth/password/change', unreachable);
      await heldSubmitted(NEW)(page);
    },
    shows: 'banner-unreachable',
  },

  // The two success banners are states of the sign-in form (17 and 18).
  { name: 'password changed', reach: linkSubmitted(NEW), shows: 'banner-passwordChanged' },
  {
    name: 'account activated',
    platform: { ...newAccount, linkSet: { status: 200, json: { kind: 'new-account' } } },
    reach: linkSubmitted(NEW),
    shows: 'banner-accountActivated',
  },

  // The help window opens from any of them.
  {
    name: 'help window from the request form',
    reach: async (page) => {
      await requestForm(page);
      await page.getByTestId('help-link').click();
    },
    shows: 'support-hotline',
  },
  {
    name: 'help window from a dead link',
    platform: { link: problem(410, 'LINK_INVALID', { kind: 'reset' }) },
    reach: async (page) => {
      await linkOpened(page);
      await page.getByTestId('help-link').click();
    },
    shows: 'support-whatsapp',
  },
  {
    name: 'help window from the temporary-password form',
    platform: temporary,
    reach: async (page) => {
      await held(page);
      await page.getByTestId('help-link').click();
    },
    shows: 'support-email',
  },
];

for (const language of ['ar', 'en'] as const) {
  test.describe(`password states in ${language}`, () => {
    for (const state of states) {
      test(state.name, async ({ page }) => {
        if (language === 'en') {
          await useEnglish(page);
        }
        await staffPlatform(page, state.platform);

        await state.reach(page);
        await expect(page.getByTestId(state.shows).first()).toBeVisible();
        await pictureFor(page, 'password', language, state.name);

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

        // The frame of the sign-in screens: language switch and footer.
        await expect(page.getByTestId('language-switch')).toBeAttached();
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

        // Never a second field to type the password again.
        expect(await page.locator('input[type="password"]').count()).toBeLessThanOrEqual(1);

        const text = await page.locator('body').innerText();

        // A clinic is named on the new-account form and nowhere else.
        for (const name of ['عيادة الشفاء', 'Al Shifa Clinic', 'عيادة النور', 'Al Noor Clinic']) {
          const expected =
            state.namesClinic === true &&
            name === (language === 'ar' ? 'عيادة الشفاء' : 'Al Shifa Clinic');
          expect(text.includes(name), `"${name}" on the page`).toBe(expected);
        }

        // No translation key is shown raw.
        expect(text).not.toMatch(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);

        // A page in one language has no sentence left in the other.
        if (language === 'en') {
          const other = await page.getByTestId('language-switch').innerText();
          const body = text.replace(support.hoursAr, '').replace(other, '');
          expect(body).not.toMatch(/[؀-ۿ]{3,}/);
        } else {
          expect(text).not.toMatch(
            /\b(Reset the password|New password|Back to sign-in|Sign out|Activate|Try again|At least)\b/,
          );
        }
      });
    }

    test('the language switch turns a password screen over without losing what was typed', async ({
      page,
    }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
      await staffPlatform(page);
      await linkOpened(page);
      await page.getByTestId('new-password').fill('Half-typed1');

      await page.getByTestId('language-switch').click();

      const other = language === 'ar' ? 'en' : 'ar';
      await expect(page.locator('html')).toHaveAttribute('lang', other);
      await expect(page.getByTestId('reset-title')).toHaveText(
        other === 'ar' ? 'تعيين كلمة مرور جديدة' : 'Set a new password',
      );
      await expect(page.getByTestId('new-password')).toHaveValue('Half-typed1');
      await expect(page.getByTestId('rule-symbol')).toHaveAttribute('data-state', 'met');
    });
  });
}
