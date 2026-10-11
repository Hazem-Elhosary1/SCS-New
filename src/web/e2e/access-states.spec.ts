import { Page, expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  StaffPlatform,
  TEAM_PERMISSIONS,
  pictureFor,
  problem,
  roles,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 005, User Story 8 (FR-029 to FR-031, SC-007): the "not in your role" state and the
 * «للعرض فقط» notice are complete in Arabic and in English, mirrored, and as the design draws
 * them. With `SCS_SHOTS` set, each state is pictured at the size of its frame
 * (specs/005-permissions-engine/contracts/access-screens.md §1).
 */
const ROLES = `${CLINIC}/administration/roles`;
const DOCTOR = ALL_PERMISSIONS.filter((code) => code !== 'admin.roles.manage');
const LONG_ROLE = { id: 'r-long', name: 'ف'.repeat(40), readyKey: null };

interface State {
  name: string;
  platform: Partial<StaffPlatform>;
  prepare?: (platform: StaffPlatform) => void;
  /** A test id that is visible in this state, and one that must not be. */
  shows: string;
  hides: string;
  /** What the page must say, per language. */
  says: { ar: string; en: string };
}

const STATES: State[] = [
  {
    name: 'no-access',
    platform: { role: roles.team, permissions: TEAM_PERMISSIONS },
    shows: 'no-access',
    hides: 'roles-ready',
    says: {
      ar: 'تُفتح لمن يملك «عرض الأدوار والصلاحيات»، ودورك «فريق العيادة» لا يحملها.',
      en: 'It opens for holders of “View roles & permissions”, and your role “فريق العيادة” does not carry it.',
    },
  },
  {
    name: 'no-access-no-role',
    platform: { role: null, permissions: [] },
    shows: 'no-access',
    hides: 'roles-ready',
    says: {
      ar: 'وحسابك لا يحمل دورًا بعد.',
      en: 'and your account does not hold a role yet.',
    },
  },
  {
    name: 'view-only',
    platform: { role: roles.doctor, permissions: DOCTOR },
    shows: 'view-only',
    hides: 'no-access',
    says: {
      ar: 'التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».',
      en: 'Changing roles needs “Manage roles & permissions”.',
    },
  },
  {
    name: 'full-access',
    platform: {},
    shows: 'roles-ready',
    hides: 'view-only',
    says: { ar: 'أدوار جاهزة', en: 'Ready roles' },
  },
  {
    name: 'no-access-after-refusal',
    platform: { role: roles.doctor, permissions: DOCTOR },
    prepare: (platform) => {
      platform.rolesAnswer = () => {
        platform.permissions = DOCTOR.filter((code) => code !== 'admin.roles.view');
        return problem(403, 'PERMISSION_REQUIRED', { permission: 'admin.roles.view' });
      };
    },
    shows: 'no-access',
    hides: 'roles-ready',
    says: {
      ar: 'ودورك «طبيب» لا يحملها.',
      en: 'and your role “Doctor” does not carry it.',
    },
  },
];

async function open(page: Page, state: State): Promise<void> {
  const platform = await staffPlatform(page, { signedIn: true, ...state.platform });
  state.prepare?.(platform);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(ROLES);
  await expect(page.getByTestId(state.shows)).toBeVisible();
}

for (const language of ['ar', 'en'] as const) {
  test.describe(`access states — ${language}`, () => {
    test.beforeEach(async ({ page }) => {
      if (language === 'en') {
        await useEnglish(page);
      }
    });

    for (const state of STATES) {
      test(`${state.name}`, async ({ page }) => {
        await open(page, state);

        await expect(page.locator('html')).toHaveAttribute('lang', language);
        await expect(page.locator('html')).toHaveAttribute('dir', language === 'ar' ? 'rtl' : 'ltr');
        await expect(page.getByTestId(state.hides)).toHaveCount(0);
        await expect(page.locator('body')).toContainText(state.says[language]);
        await expect(page.getByTestId('page-title')).toHaveText(
          language === 'ar' ? 'الأدوار والصلاحيات' : 'Roles & permissions',
        );

        // No key left untranslated, and nothing wider than the page.
        await expect(page.locator('body')).not.toContainText(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(1440);

        await pictureFor(page, 'access', language, state.name);
      });
    }

    test('a ready role is named in the language of the page until the clinic renames it', async ({
      page,
    }) => {
      const view = DOCTOR.filter((code) => code !== 'admin.roles.view');
      await staffPlatform(page, { signedIn: true, role: roles.doctor, permissions: view });
      await page.goto(ROLES);

      await expect(page.getByTestId('no-access-text')).toContainText(
        language === 'ar' ? '«طبيب»' : '“Doctor”',
      );
    });

    test('a forty-character role name wraps inside the card and is read as part of the sentence', async ({
      page,
    }) => {
      await staffPlatform(page, { signedIn: true, role: LONG_ROLE, permissions: TEAM_PERMISSIONS });
      await page.goto(ROLES);

      const card = page.getByTestId('no-access');
      const text = page.getByTestId('no-access-text');
      await expect(text).toContainText(LONG_ROLE.name);

      const [outer, inner] = await Promise.all([card.boundingBox(), text.boundingBox()]);
      expect(inner!.x).toBeGreaterThanOrEqual(outer!.x);
      expect(inner!.x + inner!.width).toBeLessThanOrEqual(outer!.x + outer!.width);

      // The names are text of the paragraph itself — nothing hidden from assistive technology.
      await expect(text.locator('[aria-hidden="true"]')).toHaveCount(0);
      await expect(card.getByRole('heading')).toHaveText(
        language === 'ar' ? 'هذه الصفحة ليست في دورك' : 'This page is not in your role',
      );
    });
  });
}
