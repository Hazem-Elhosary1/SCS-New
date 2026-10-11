import { Page, expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  pictureFor,
  problem,
  roles,
  rolesList,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 006, User Story 8 (FR-038): the roles list, for reading — the first real list built
 * from the shared parts. Design: page "Roles", "Roles / Roles List" and "Roles List Read Only".
 */
const ROLES = `${CLINIC}/administration/roles`;
const VIEW_ONLY = ALL_PERMISSIONS.filter((code) => code !== 'admin.roles.manage');
const ALL = ALL_PERMISSIONS.length;

async function open(page: Page): Promise<void> {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(ROLES);
  await expect(page.getByTestId('page-title')).toBeVisible();
}

test('ready roles first, then the clinic’s own, each with its holders and its share of the catalogue', async ({
  page,
}) => {
  await staffPlatform(page, { signedIn: true });
  await open(page);

  await expect(page.locator('scs-page-header nav')).toContainText('الإدارة');
  await expect(page.getByTestId('page-note')).toHaveText('٣ أدوار · ٨٣ صلاحية');
  expect(ALL).toBe(83);

  const ready = page.getByTestId('roles-ready');
  await expect(ready.getByRole('heading')).toHaveText('أدوار جاهزة');
  await expect(ready).toContainText('ثلاثة أدوار تأتي مع المنصّة');
  await expect(ready.locator('th')).toHaveText(['الدور', 'الحاملون', 'الصلاحيات']);
  await expect(ready.getByTestId('role-name')).toHaveText(['مالك العيادة', 'طبيب']);
  await expect(ready.getByTestId('role-holders')).toHaveText(['١', '٢']);
  await expect(ready.getByTestId('role-permissions')).toHaveText(['٨٣ من ٨٣', '٤٠ من ٨٣']);
  await expect(ready.getByTestId('role-row').nth(0)).toContainText('كل صلاحيات العيادة');

  const custom = page.getByTestId('roles-custom');
  await expect(custom.getByRole('heading')).toHaveText('أدوار مخصّصة');
  await expect(custom).toContainText('ما أنشأته العيادة لنفسها');
  await expect(custom.getByTestId('role-name')).toHaveText(['فريق العيادة']);
  await expect(custom.getByTestId('role-holders')).toHaveText(['٠']);
  await expect(custom.getByTestId('role-permissions')).toHaveText(['٦ من ٨٣']);

  // Ready above custom; a short list that is read has no toolbar, and nothing here writes.
  expect((await ready.boundingBox())!.y).toBeLessThan((await custom.boundingBox())!.y);
  await expect(page.locator('scs-list-toolbar')).toHaveCount(0);
  await expect(page.locator('main button')).toHaveCount(0);
  await expect(page.getByTestId('view-only')).toHaveCount(0);
});

test('reads in English, with the ready roles named in English and the numbers in Latin digits', async ({
  page,
}) => {
  await useEnglish(page);
  await staffPlatform(page, { signedIn: true });
  await open(page);

  await expect(page.getByTestId('page-note')).toHaveText('3 roles · 83 permissions');
  await expect(page.getByTestId('roles-ready').getByRole('heading')).toHaveText('Ready roles');
  await expect(page.getByTestId('roles-ready').locator('th')).toHaveText(['Role', 'Holders', 'Permissions']);
  await expect(page.getByTestId('roles-ready').getByTestId('role-name')).toHaveText(['Clinic owner', 'Doctor']);
  await expect(page.getByTestId('roles-ready').getByTestId('role-permissions')).toHaveText(['83 of 83', '40 of 83']);
  await expect(page.getByTestId('roles-custom').getByRole('heading')).toHaveText('Custom roles');
  // A ready role is described in the page's language: no Arabic is left in its rows.
  await expect(page.getByTestId('roles-ready').getByTestId('role-description')).toHaveText([
    'Every clinic permission',
    'The exam room, patient files and their own appointments',
  ]);
  await expect(page.getByTestId('roles-ready')).not.toContainText(/[؀-ۿ]/);
  // What the clinic wrote stays as written, and starts where the page's lines start.
  const [name, cell] = await Promise.all([
    page.getByTestId('roles-custom').getByTestId('role-name').boundingBox(),
    page.getByTestId('roles-custom').locator('td').first().boundingBox(),
  ]);
  expect(name!.x - cell!.x).toBeLessThan(20);
  await expect(page.getByTestId('roles-custom').getByTestId('role-name')).toHaveText(['فريق العيادة']);
});

test('the count in the line under the title follows the Arabic rule of the number', async ({ page }) => {
  const eleven = Array.from({ length: 9 }, (_, index) => ({
    ...rolesList.roles[2],
    id: `custom-${index}`,
    name: `دور ${index + 1}`,
  }));
  const platform = await staffPlatform(page, {
    signedIn: true,
    rolesAnswer: { status: 200, json: { ...rolesList, roles: [...rolesList.roles.slice(0, 2), ...eleven] } },
  });
  await open(page);
  await expect(page.getByTestId('page-note')).toHaveText('١١ دورًا · ٨٣ صلاحية');

  platform.rolesAnswer = { status: 200, json: { ...rolesList, roles: rolesList.roles.slice(0, 2) } };
  await page.reload();
  await expect(page.getByTestId('page-note')).toHaveText('دوران · ٨٣ صلاحية');
});

test('with the view alone it is under the «للعرض فقط» notice, and is the same list', async ({ page }) => {
  await staffPlatform(page, { signedIn: true, role: roles.doctor, permissions: VIEW_ONLY });
  await open(page);

  const notice = page.getByTestId('view-only');
  await expect(notice).toContainText('التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».');
  await expect(page.getByTestId('role-row')).toHaveCount(3);
  expect((await notice.boundingBox())!.y).toBeLessThan((await page.getByTestId('roles-ready').boundingBox())!.y);
  await expect(page.locator('main button')).toHaveCount(0);
});

test('with no custom role, that list shows its empty state', async ({ page }) => {
  await staffPlatform(page, {
    signedIn: true,
    rolesAnswer: { status: 200, json: { ...rolesList, roles: rolesList.roles.slice(0, 2) } },
  });
  await open(page);

  const custom = page.getByTestId('roles-custom');
  await expect(custom.getByTestId('empty-title')).toHaveText('لا أدوار مخصّصة بعد');
  await expect(custom.locator('table')).toHaveCount(0);
  await expect(page.getByTestId('roles-ready').getByTestId('role-row')).toHaveCount(2);
});

test('while loading it shows skeletons and a dash for the counts', async ({ page }) => {
  await staffPlatform(page, { signedIn: true });
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/v1/roles', async (route) => {
    await held;
    await route.fallback();
  });
  await open(page);

  await expect(page.getByTestId('roles-loading').locator('scs-skeleton')).toHaveCount(2);
  await expect(page.getByTestId('roles-loading').getByRole('heading')).toHaveText(['أدوار جاهزة', 'أدوار مخصّصة']);
  await expect(page.getByTestId('page-note')).toHaveText('—');
  await expect(page.getByTestId('role-row')).toHaveCount(0);

  release();
  await expect(page.getByTestId('role-row')).toHaveCount(3);
  await expect(page.getByTestId('roles-loading')).toHaveCount(0);
});

test('a failure keeps the page, shows a dash, and «إعادة المحاولة» loads it', async ({ page }) => {
  const platform = await staffPlatform(page, {
    signedIn: true,
    rolesAnswer: problem(503, 'SERVICE_UNAVAILABLE'),
  });
  await open(page);

  const failed = page.getByTestId('roles-failed');
  await expect(failed.getByTestId('empty-title')).toHaveText('تعذّر تحميل الأدوار');
  await expect(failed.locator('scs-empty-state')).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId('page-title')).toHaveText('الأدوار والصلاحيات');
  await expect(page.getByTestId('page-note')).toHaveText('—');
  await expect(page.getByTestId('side-bar')).toBeVisible();

  platform.rolesAnswer = { status: 200, json: rolesList };
  await page.getByTestId('roles-retry').click();

  await expect(page.getByTestId('role-row')).toHaveCount(3);
  await expect(page.getByTestId('page-note')).toHaveText('٣ أدوار · ٨٣ صلاحية');
});

test('a long role name and description stay inside the card', async ({ page }) => {
  const long = {
    ...rolesList.roles[2],
    name: 'ف'.repeat(40),
    description: 'وصف طويل لدورٍ أنشأته العيادة. '.repeat(12),
  };
  await staffPlatform(page, {
    signedIn: true,
    rolesAnswer: { status: 200, json: { ...rolesList, roles: [...rolesList.roles.slice(0, 2), long] } },
  });
  await open(page);

  const [card, row] = await Promise.all([
    page.getByTestId('roles-custom').boundingBox(),
    page.getByTestId('roles-custom').getByTestId('role-row').boundingBox(),
  ]);
  expect(row!.x).toBeGreaterThanOrEqual(card!.x);
  expect(row!.x + row!.width).toBeLessThanOrEqual(card!.x + card!.width);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(1440);
});

for (const language of ['ar', 'en'] as const) {
  test(`pictures for the design comparison — ${language}`, async ({ page }) => {
    test.skip(!process.env['SCS_SHOTS'], 'only when SCS_SHOTS names a folder');
    if (language === 'en') {
      await useEnglish(page);
    }
    const platform = await staffPlatform(page, { signedIn: true });

    await open(page);
    await expect(page.getByTestId('role-row')).toHaveCount(3);
    await pictureFor(page, 'roles', language, 'owner');

    platform.role = roles.doctor;
    platform.permissions = VIEW_ONLY;
    await page.reload();
    await expect(page.getByTestId('view-only')).toBeVisible();
    await pictureFor(page, 'roles', language, 'view-only');

    platform.rolesAnswer = { status: 200, json: { ...rolesList, roles: rolesList.roles.slice(0, 2) } };
    await page.reload();
    await expect(page.getByTestId('empty-title')).toBeVisible();
    await pictureFor(page, 'roles', language, 'no-custom');

    platform.rolesAnswer = problem(503, 'SERVICE_UNAVAILABLE');
    await page.reload();
    await expect(page.getByTestId('roles-failed')).toBeVisible();
    await pictureFor(page, 'roles', language, 'failed');
  });
}
