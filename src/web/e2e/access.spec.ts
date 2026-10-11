import { expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  TEAM_PERMISSIONS,
  problem,
  roles,
  staffPlatform,
} from './staff-platform';

/**
 * Spec 005, User Story 5 (SH-055, ق-37, FR-027 to FR-033): the screen shows a person only what
 * they may do. One address, three people: the roles destination for someone who may change
 * roles, for someone who may only read them, and for someone whose role does not open it.
 */
const ROLES = `${CLINIC}/administration/roles`;
const DOCTOR = ALL_PERMISSIONS.filter((code) => code !== 'admin.roles.manage');

test('a person who may change roles sees the page with no notice', async ({ page }) => {
  const platform = await staffPlatform(page, { signedIn: true });

  await page.goto(ROLES);

  await expect(page.getByTestId('page-title')).toHaveText('الأدوار والصلاحيات');
  await expect(page.getByTestId('roles-ready')).toBeVisible();
  await expect(page.getByTestId('role-row')).toHaveCount(3);
  await expect(page.getByTestId('view-only')).toHaveCount(0);
  await expect(page.getByTestId('no-access')).toHaveCount(0);
  expect(platform.calls).toContain('GET /v1/roles');
});

test('a person who may read roles and not change them sees «للعرض فقط» naming what changing needs', async ({
  page,
}) => {
  await staffPlatform(page, { signedIn: true, role: roles.doctor, permissions: DOCTOR });

  await page.goto(ROLES);

  const notice = page.getByTestId('view-only');
  await expect(notice).toBeVisible();
  await expect(notice.getByTestId('banner-title')).toHaveText('للعرض فقط');
  await expect(notice).toContainText('التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».');
  await expect(notice).toHaveAttribute('role', 'status');
  await expect(page.getByTestId('roles-ready')).toBeVisible();

  // Nothing on the page changes anything: no button, no link that acts.
  await expect(page.locator('main button')).toHaveCount(0);
  await expect(page.locator('button:disabled')).toHaveCount(0);
});

test('a person whose role does not open the page is told so, and nothing of the page is asked for', async ({
  page,
}) => {
  const platform = await staffPlatform(page, {
    signedIn: true,
    role: roles.team,
    permissions: TEAM_PERMISSIONS,
  });

  await page.goto(ROLES);
  await expect(page.getByTestId('no-access')).toBeVisible();

  await expect(page).toHaveURL(ROLES);
  await expect(page.getByTestId('no-access-title')).toHaveText('هذه الصفحة ليست في دورك');
  await expect(page.getByTestId('no-access-text')).toHaveText(
    'تُفتح لمن يملك «عرض الأدوار والصلاحيات»، ودورك «فريق العيادة» لا يحملها. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.',
  );
  await expect(page.getByTestId('page-note')).toContainText('دورٌ لا يحمل «عرض الأدوار والصلاحيات»');
  await expect(page.getByTestId('roles-ready')).toHaveCount(0);
  await expect(page.getByTestId('view-only')).toHaveCount(0);
  expect(platform.calls).not.toContain('GET /v1/roles');
});

test('an account that holds no role is told so, and still opens the home page', async ({ page }) => {
  const platform = await staffPlatform(page, { signedIn: true, role: null, permissions: [] });

  await page.goto(ROLES);

  await expect(page.getByTestId('no-access-text')).toHaveText(
    'تُفتح لمن يملك «عرض الأدوار والصلاحيات»، وحسابك لا يحمل دورًا بعد. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.',
  );
  expect(platform.calls).not.toContain('GET /v1/roles');

  // «الرئيسية» needs no permission.
  await page.goto(`${CLINIC}/`);
  await expect(page.getByTestId('signed-in')).toBeVisible();
  await expect(page.getByTestId('no-access')).toHaveCount(0);
});

test('when the platform refuses under a list that has gone stale, the page gives way to "not in your role"', async ({
  page,
}) => {
  // The screen was told the person holds the view; the role lost it a moment ago.
  const platform = await staffPlatform(page, {
    signedIn: true,
    role: roles.doctor,
    permissions: DOCTOR,
  });
  platform.rolesAnswer = () => {
    platform.permissions = DOCTOR.filter((code) => code !== 'admin.roles.view');
    return problem(403, 'PERMISSION_REQUIRED', { permission: 'admin.roles.view' });
  };

  await page.goto(ROLES);

  await expect(page.getByTestId('no-access')).toBeVisible();
  await expect(page).toHaveURL(ROLES);
  await expect(page.getByTestId('no-access-text')).toContainText('«عرض الأدوار والصلاحيات»');
  await expect(page.getByTestId('no-access-text')).toContainText('«طبيب»');
  await expect(page.getByTestId('roles-ready')).toHaveCount(0);

  // Asked once for the roles, refused, then asked who is signed in again — and not for the roles again.
  expect(platform.calls.filter((call) => call === 'GET /v1/roles').length).toBe(1);
  expect(platform.calls.filter((call) => call === 'GET /v1/auth/session').length).toBe(2);
});

test('nobody reaches the page without signing in, and a temporary password comes first', async ({
  page,
}) => {
  const platform = await staffPlatform(page);

  await page.goto(ROLES);
  await expect(page).toHaveURL(`${CLINIC}/sign-in`);
  await expect(page.getByTestId('no-access')).toHaveCount(0);

  platform.signedIn = true;
  platform.mustChange = true;
  await page.goto(ROLES);
  await expect(page).toHaveURL(`${CLINIC}/change-password`);
});

test.describe('how soon', () => {
  // A clock on a machine that is running two hundred other browser tests at once: the figure
  // is the app's when one of three tries meets it, and the machine's when none does.
  test.describe.configure({ retries: 2 });

  test('the "not in your role" state is on screen within a second of opening the address (SC-008)', async ({
    page,
  }) => {
    const platform = await staffPlatform(page, {
      signedIn: true,
      role: roles.team,
      permissions: TEAM_PERMISSIONS,
    });

    // Warm the app, so what is timed is opening the address and not the first download.
    await page.goto(`${CLINIC}/`);
    await expect(page.getByTestId('signed-in')).toBeVisible();
    platform.calls.length = 0;

    const started = Date.now();
    await page.goto(ROLES);
    await expect(page.getByTestId('no-access')).toBeVisible();
    expect(Date.now() - started).toBeLessThan(1000);
    expect(platform.calls).not.toContain('GET /v1/roles');
  });
});
