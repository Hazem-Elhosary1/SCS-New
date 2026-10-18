import { Page, expect, test } from '@playwright/test';
import { OPS, OpsPlatform, opsEnglish, opsLooksWrong, opsOperator, opsPlatform } from './ops-platform';
import { OpsTeam, SUCCESS, SUPER, SUPPORT, VIEWER, choose, opsTeam, rowsOf } from './ops-team-platform';

test.use({ locale: 'ar-EG' });

/**
 * Spec 011, User Stories 1 and 6: the roles tab, the role's form and deleting a role
 * (contracts/operator-screens.md §7 to §9), and what the console shows and opens for each role
 * (§0), in Arabic and English, at the design's size and at a phone's width.
 */
const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };
const EASTERN = /[\u0660-\u0669\u06f0-\u06f9]/u;

type Language = 'ar' | 'en';

async function look(page: Page, language: Language, state: string): Promise<void> {
  expect(await page.locator('body').innerText(), `${language} ${state}: digits`).not.toMatch(EASTERN);
  await page.setViewportSize(NARROW);
  expect(await opsLooksWrong(page), `${language} ${state}, narrow`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/narrow/${language}-${state}.png`, fullPage: true });
  }
  await page.setViewportSize(WIDE);
  expect(await opsLooksWrong(page), `${language} ${state}, wide`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/ops-${language}-${state}.png`, fullPage: true });
  }
}

async function open(
  page: Page,
  language: Language,
  path: string,
  role = SUPER,
  with_: Partial<OpsTeam> = {},
): Promise<{ platform: OpsPlatform; team: OpsTeam }> {
  if (language === 'en') {
    await opsEnglish(page);
  }
  await page.setViewportSize(WIDE);
  const platform = await opsPlatform(page, {
    signedIn: true,
    operator: language === 'en' ? { ...opsOperator, fullName: 'Ahmed Sami', language: 'en', roleName: 'Super Admin' } : opsOperator,
  });
  const team = await opsTeam(page, platform, { myRoleId: role, ...with_ });
  await page.goto(`${OPS}${path}`);
  return { platform, team };
}

/** A support agent who was also given the roles to manage: someone who holds a part of the catalogue. */
function makeRoleManager(team: OpsTeam): void {
  team.roles.find((role) => role.id === SUPPORT)!.codes = ['tickets.view', 'tickets.reply', 'operators.view', 'operators.manageRoles'];
  team.actAs(SUPPORT);
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`what a role opens — ${language}`, () => {
    test('the side bar, the top bar and the pages follow the role', async ({ page }) => {
      const { team } = await open(page, language, '/');

      // Spec 012 adds «الباقات», for whoever may view plans.
      // Spec 015 adds «العيادات» after the home.
      await expect(page.getByTestId('ops-side-bar').getByRole('link')).toHaveText(
        ar ? ['الرئيسية', 'العيادات', 'الباقات', 'المشغلون'] : ['Home', 'Clinics', 'Plans', 'Operators'],
      );
      await expect(page.getByTestId('ops-role')).toHaveText(ar ? 'مسؤول عام' : 'Super Admin');

      // A custom role is named as it was written, in both languages.
      team.roles.find((role) => role.id === SUCCESS)!.codes = ['dashboard.view', 'clinics.view'];
      team.actAs(SUCCESS);
      await page.goto(`${OPS}/`);
      await expect(page.getByTestId('ops-role')).toHaveText('نجاح العملاء');
      // «clinics.view» opens «العيادات» since spec 015.
      await expect(page.getByTestId('ops-side-bar').getByRole('link')).toHaveText(ar ? ['الرئيسية', 'العيادات'] : ['Home', 'Clinics']);

      // Its address says so, in the frame, with a way back — and shows nothing of the page.
      for (const path of ['/operators', '/operators/roles', '/operators/new', '/operators/roles/new']) {
        await page.goto(`${OPS}${path}`);
        await expect(page.getByTestId('ops-no-access')).toHaveText(ar ? 'لا يمكنك فتح هذه الصفحة' : "You can't open this page");
        await expect(page).toHaveURL(`${OPS}${path}`);
        await expect(page.getByTestId('page-title')).toHaveCount(0);
      }
      await look(page, language, 'no-access');
      await page.getByTestId('ops-no-access-home').click();
      await expect(page.getByTestId('ops-home-title')).toBeVisible();

      // A viewer sees the team and its roles, and may not open a form by its address.
      team.actAs(VIEWER);
      await page.goto(`${OPS}/operators/roles`);
      await expect(page.getByTestId('ops-role')).toHaveText(ar ? 'مشاهد فقط' : 'Viewer Only');
      await expect(page.getByTestId('role-row')).toHaveCount(5);
      await expect(page.getByTestId('add-role')).toHaveCount(0);
      await expect(page.getByTestId('role-menu')).toHaveCount(0);
      await look(page, language, 'roles-viewer');
      for (const path of ['/operators/new', '/operators/roles/new', `/operators/roles/${SUPPORT}/edit`]) {
        await page.goto(`${OPS}${path}`);
        await expect(page.getByTestId('ops-no-access')).toBeVisible();
      }
    });

    test('a role changed while the console is open shows at the next page, and a refusal reads the session again', async ({ page }) => {
      const { team, platform } = await open(page, language, '/operators');
      await expect(page.getByTestId('add-operator')).toBeVisible();

      // Someone took "add and edit operators" from the role: the next page of the console knows.
      team.roles.find((role) => role.id === SUPPORT)!.codes = ['operators.view'];
      team.actAs(SUPPORT);
      await page.getByRole('tab').nth(1).click();
      await expect(page.getByTestId('role-row')).toHaveCount(5);
      await page.getByRole('tab').nth(0).click();
      await expect(page.getByTestId('operator-row')).toHaveCount(8);
      await expect(page.getByTestId('add-operator')).toHaveCount(0);

      // And the view itself is taken: the list's next read is refused, and the side bar follows.
      team.roles.find((role) => role.id === SUPPORT)!.codes = ['tickets.view'];
      team.actAs(SUPPORT);
      const before = platform.calls.filter((call) => call === 'GET /v1/ops/auth/session').length;
      await page.getByTestId('list-search').fill('sara');
      await expect(page.getByTestId('ops-nav-operators')).toHaveCount(0);
      expect(platform.calls.filter((call) => call === 'GET /v1/ops/auth/session').length).toBeGreaterThan(before);
    });
  });

  test.describe(`the roles tab — ${language}`, () => {
    test('shows the cards and the roles, the platform’s own first and without a menu', async ({ page }) => {
      await open(page, language, '/operators/roles');

      await expect(page.getByTestId('page-title')).toHaveText(ar ? 'الأدوار والصلاحيات' : 'Roles & Permissions');
      await expect(page.getByTestId('page-note')).toHaveText(
        ar ? 'إدارة أدوار المشغلين وصلاحياتهم داخل المنصة.' : 'Manage operator roles and their permissions across the platform.',
      );
      await expect(page.getByTestId('add-role')).toContainText(ar ? 'إضافة دور' : 'Add Role');
      await expect(page.getByRole('tab').nth(1)).toHaveAttribute('aria-selected', 'true');

      const cards = page.getByTestId('role-cards');
      await expect(cards.getByTestId('kpi-label')).toHaveText(
        ar ? ['إجمالي الأدوار', 'أدوار مُفعّلة', 'أدوار مخصصة', 'أدوار النظام'] : ['Total Roles', 'Active Roles', 'Custom Roles', 'System Roles'],
      );
      await expect(cards.getByTestId('kpi-value')).toHaveText(['5', '5', '2', '3']);
      await expect(cards.getByTestId('kpi-note')).toHaveText(
        ar
          ? ['جميع الأدوار المتاحة', 'قيد الاستخدام حالياً', 'قابلة للتعديل الكامل', 'غير قابلة للحذف']
          : ['All available roles', 'Currently in use', 'Fully editable', 'Cannot be deleted'],
      );

      await expect(page.locator('thead th')).toHaveText(
        ar
          ? ['اسم الدور', 'الفئة', 'عدد المشغلين', 'الحالة', 'الصلاحيات', 'الإجراءات']
          : ['Role Name', 'Category', 'Operators', 'Status', 'Permissions', 'Actions'],
      );
      await expect(page.getByTestId('role-name')).toHaveText(
        ar
          ? ['مسؤول عام', 'مسؤول الفوترة', 'مشاهد فقط', 'وكيل دعم فني', 'نجاح العملاء']
          : ['Super Admin', 'Billing Manager', 'Viewer Only', 'وكيل دعم فني', 'نجاح العملاء'],
      );
      await expect(page.getByTestId('role-category')).toHaveText(
        ar ? ['نظام', 'نظام', 'نظام', 'مخصص', 'مخصص'] : ['System', 'System', 'System', 'Custom', 'Custom'],
      );
      // Arabic counts take their grammatical form.
      await expect(page.getByTestId('role-operators')).toHaveText(
        ar ? ['مشغلان', '1 مشغل', '1 مشغل', '7 مشغلين', '1 مشغل'] : ['2 operators', '1 operator', '1 operator', '7 operators', '1 operator'],
      );
      await expect(page.getByTestId('role-permissions')).toHaveText(
        ar ? ['28 صلاحية', '11 صلاحية', '11 صلاحية', '5 صلاحيات', '6 صلاحيات'] : ['28 permissions', '11 permissions', '11 permissions', '5 permissions', '6 permissions'],
      );
      await expect(page.getByTestId('role-state').first()).toHaveText(ar ? 'قيد الاستخدام' : 'In use');
      await expect(page.getByTestId('role-menu')).toHaveCount(2);
      await expect(page.getByTestId('table-count')).toHaveText(ar ? 'عرض 1–5 من 5 أدوار' : 'Showing 1–5 of 5 roles');
      await expect(page.getByTestId('table-paging')).toHaveCount(0);
      await look(page, language, 'roles-list');

      await page.getByTestId('list-search').fill(ar ? 'فوتر' : 'billing');
      await expect(page.getByTestId('role-row')).toHaveCount(1);
      await page.getByTestId('list-search').fill('zzz');
      await expect(page.getByTestId('roles-none')).toContainText(ar ? 'لا توجد أدوار مطابقة لبحثك.' : 'No roles match your search.');
      await look(page, language, 'roles-none');
    });

    test('says so when the roles could not be read', async ({ page }) => {
      const { team } = await open(page, language, '/operators/roles', SUPER, { failing: { 'GET /v1/ops/roles': 500 } });
      await expect(page.getByTestId('roles-failed')).toContainText(ar ? 'تعذّر جلب الأدوار.' : "Couldn't load the roles.");
      await expect(page.getByTestId('roles-failed')).toContainText(
        ar ? 'تعذر تحميل قائمة الأدوار. تحقق من اتصالك وحاول مرة أخرى.' : "The roles list couldn't be loaded. Check your connection and try again.",
      );
      await look(page, language, 'roles-failed');
      team.failing = {};
      await page.getByTestId('roles-retry').click();
      await expect(page.getByTestId('role-row')).toHaveCount(5);
    });

    test('deletes a role nobody holds, and refuses one that is held', async ({ page }) => {
      const { team, platform } = await open(page, language, '/operators/roles');

      // Held by seven: the dialog says so and its button is disabled.
      await page.getByTestId('role-menu').first().click();
      await expect(page.getByTestId('role-menu-items').getByRole('menuitem')).toHaveText(
        ar ? ['تعديل الدور', 'حذف الدور…'] : ['Edit role', 'Delete role…'],
      );
      await page.getByTestId('role-delete').click();
      await expect(page.getByRole('dialog')).toContainText(ar ? 'حذف الدور' : 'Delete role');
      await expect(page.getByTestId('delete-role-held')).toHaveText(
        ar ? 'لا يمكن حذف دور «وكيل دعم فني» وهو مُسند إلى مشغلين.' : 'The role “وكيل دعم فني” can’t be deleted while operators hold it.',
      );
      await expect(page.getByTestId('delete-role-count')).toHaveText(
        ar ? 'يحمل هذا الدور 7 مشغلين. انقلهم إلى دور آخر قبل حذفه.' : 'Reassign the 7 operators on this role before deleting it.',
      );
      await expect(page.getByTestId('delete-role-confirm')).toBeDisabled();
      // The second sentence is a warning note, as frame 2788:16350 draws it.
      expect(
        await page.getByTestId('delete-role-count').evaluate((element) => getComputedStyle(element).backgroundColor),
      ).not.toBe('rgba(0, 0, 0, 0)');
      await expect(page.getByTestId('delete-role-note')).toHaveCount(0);
      await look(page, language, 'delete-role-held');
      await page.getByTestId('delete-role-cancel').click();

      // Nobody holds it once its one holder is moved.
      team.operators.find((person) => person.roleId === SUCCESS)!.roleId = VIEWER;
      await page.reload();
      await page.getByTestId('role-menu').nth(1).click();
      await page.getByTestId('role-delete').click();
      await expect(page.getByTestId('delete-role-text')).toHaveText(
        ar
          ? 'حذف دور «نجاح العملاء»؟ لا يحمله أي مشغل، فلن يفقد أحدٌ صلاحياته. يختفي من قائمة الأدوار ومن اختيار الدور، ويبقى تاريخه في سجل التدقيق.'
          : 'Delete the role “نجاح العملاء”? Nobody holds it, so no operator loses access. It leaves the roles list and the role picker, and its history stays in the audit log.',
      );
      await expect(page.getByTestId('delete-role-note')).toHaveAttribute(
        'placeholder',
        ar ? 'مثال: دُمج في دور وكيل دعم فني' : 'e.g. Merged into Support Agent',
      );
      await look(page, language, 'delete-role');

      // A holder appeared between the list and the press: the platform refuses, and the dialog turns.
      team.operators[0].roleId = SUCCESS;
      await page.getByTestId('delete-role-confirm').click();
      await expect(page.getByTestId('delete-role-held')).toBeVisible();
      await page.getByTestId('delete-role-cancel').click();

      team.operators[0].roleId = SUPER;
      await page.reload();
      await page.getByTestId('role-menu').nth(1).click();
      await page.getByTestId('role-delete').click();
      await page.getByTestId('delete-role-note').fill('دُمج في دور آخر');
      await page.getByTestId('delete-role-confirm').click();
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم حذف الدور.' : 'Role deleted.');
      await expect(page.getByTestId('role-row')).toHaveCount(4);
      expect(platform.bodies.at(-1)).toEqual({ note: 'دُمج في دور آخر' });
    });
  });

  test.describe(`the role's form — ${language}`, () => {
    test('adds a role: 28 boxes in nine groups, a needed box locked with its reason, and "select all"', async ({ page }) => {
      const { platform } = await open(page, language, '/operators/roles/new');

      await expect(page.getByTestId('breadcrumb')).toContainText(ar ? 'إضافة دور جديد' : 'Add New Role');
      await expect(page.getByTestId('role-form-title')).toHaveText(ar ? 'إضافة دور جديد' : 'Add New Role');
      await expect(page.getByTestId('role-name')).toHaveAttribute('placeholder', ar ? 'مثال: مشرف الفرع' : 'e.g. Branch Supervisor');
      await expect(page.getByText(ar ? 'إنشاء بناءً على دور موجود (اختياري)' : 'Base on existing role (optional)')).toBeVisible();
      await expect((await rowsOf(page, 'role-based-on')).first()).toHaveText(ar ? 'بدون — دور جديد بالكامل' : 'None — start from scratch');
      await page.keyboard.press('Escape');

      await expect(page.getByTestId('role-permissions').locator('legend')).toHaveText(
        ar
          ? ['لوحة التحكم', 'العيادات', 'التذاكر', 'التنبيهات', 'الباقات والاشتراكات', 'المشغلون', 'التقارير', 'الأمان', 'الإعدادات']
          : ['Dashboard', 'Clinics', 'Tickets', 'Notices', 'Plans & Subscriptions', 'Operators', 'Reports', 'Security', 'Settings'],
      );
      await expect(page.getByTestId('role-permissions').locator('input[type="checkbox"]')).toHaveCount(28);
      await expect(page.getByTestId('group-notices').locator('input')).toHaveCount(1);
      await look(page, language, 'role-add');

      // Nothing ticked, no name.
      await page.getByTestId('role-save').click();
      await expect(page.getByTestId('role-name-error')).toHaveText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.');
      await expect(page.getByTestId('role-codes-error')).toHaveText(ar ? 'حدد صلاحية واحدة على الأقل.' : 'Select at least one permission.');
      await expect(page.getByTestId('role-name')).toBeFocused();

      // «إرسال إشعارات للعيادات» ticks «عرض العيادات» and locks it, with why.
      await page.getByTestId('box-notices.send').check();
      await expect(page.getByTestId('box-clinics.view')).toBeChecked();
      await expect(page.getByTestId('box-clinics.view')).toBeDisabled();
      await expect(page.getByTestId('box-clinics.view-reason')).toHaveText(
        ar ? 'مطلوبة لـإرسال إشعارات للعيادات' : 'Required by Send Notices to Clinics',
      );
      await page.getByTestId('box-tickets.viewAll').check();
      await expect(page.getByTestId('box-tickets.view')).toBeDisabled();
      await look(page, language, 'role-add-needed');

      // Freed when nothing needs it any more.
      await page.getByTestId('box-notices.send').uncheck();
      await expect(page.getByTestId('box-clinics.view')).toBeEnabled();
      await expect(page.getByTestId('box-clinics.view')).toBeChecked();

      await page.getByTestId('select-all').click();
      await expect(page.getByTestId('role-permissions').locator('input:checked')).toHaveCount(28);
      await expect(page.getByTestId('select-all')).toHaveText(ar ? 'إلغاء التحديد' : 'Clear');
      await page.getByTestId('select-all').click();
      await expect(page.getByTestId('role-permissions').locator('input:checked')).toHaveCount(0);
      await expect(page.getByTestId('select-all')).toHaveText(ar ? 'حدد الكل' : 'Select All');

      // A name that is taken, then a good one.
      await page.getByTestId('box-tickets.reply').check();
      await page.getByTestId('role-name').fill('وكيل دعم فني');
      await page.getByTestId('role-save').click();
      await expect(page.getByTestId('role-name-error')).toHaveText(ar ? 'يوجد دور بهذا الاسم.' : 'A role with this name already exists.');

      await page.getByTestId('role-name').fill('مشرف الفرع');
      await page.getByTestId('role-description').fill('يتابع تذاكر فرعه.');
      await page.getByTestId('role-save').click();
      await expect(page).toHaveURL(`${OPS}/operators/roles`);
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم إنشاء الدور.' : 'Role created.');
      await expect(page.getByTestId('role-row')).toHaveCount(6);
      expect(platform.bodies.at(-1)).toMatchObject({ name: 'مشرف الفرع', description: 'يتابع تذاكر فرعه.', codes: ['tickets.view', 'tickets.reply'] });
    });

    test('whoever holds part of the catalogue ticks only that, and copying a role says what was left', async ({ page }) => {
      const { team } = await open(page, language, '/operators/roles');
      makeRoleManager(team);
      await page.goto(`${OPS}/operators/roles/new`);

      await expect(page.getByTestId('box-settings.edit')).toBeDisabled();
      await expect(page.getByTestId('box-settings.edit-reason')).toHaveText(ar ? 'ليست من صلاحياتك' : "You don't hold this permission");
      await expect(page.getByTestId('box-tickets.reply')).toBeEnabled();

      // "Select all" ticks what they may, and nothing else.
      await page.getByTestId('select-all').click();
      await expect(page.getByTestId('role-permissions').locator('input:checked')).toHaveCount(4);
      await page.getByTestId('select-all').click();

      await choose(page, 'role-based-on', SUCCESS);
      await expect(page.getByTestId('box-tickets.view')).toBeChecked();
      await expect(page.getByTestId('box-clinics.edit')).not.toBeChecked();
      await expect(page.getByTestId('role-based-on-hint')).toHaveText(
        ar ? 'تُركت 5 صلاحيات لا تحملها.' : "5 permissions you don't hold were left out.",
      );
      await look(page, language, 'role-add-copied');

      // The role they hold opens for reading only.
      await page.goto(`${OPS}/operators/roles/${SUPPORT}/edit`);
      await expect(page.getByTestId('role-read-only')).toContainText(ar ? 'لا يمكنك تعديل الدور الذي تحمله.' : "You can't edit the role you hold.");
      await expect(page.getByTestId('role-save')).toHaveCount(0);
      await expect(page.getByTestId('select-all')).toHaveCount(0);
      await expect(page.getByTestId('box-tickets.reply')).toBeDisabled();
      await look(page, language, 'role-edit-own');
    });

    test('edits a role: its name in the title, the note, a stale version, and refused permissions named', async ({ page }) => {
      const { team, platform } = await open(page, language, `/operators/roles/${SUPPORT}/edit`);

      await expect(page.getByTestId('role-form-title')).toHaveText(ar ? 'تعديل الدور: وكيل دعم فني' : 'Edit Role: وكيل دعم فني');
      await expect(page.getByTestId('role-name')).toHaveValue('وكيل دعم فني');
      await expect(page.getByTestId('role-based-on')).toHaveCount(0);
      // «الفئة» beside the name, as frame 1751:813 draws it.
      await expect(page.getByTestId('role-category')).toContainText(ar ? 'الفئة' : 'Category');
      await expect(page.getByTestId('role-category').locator('scs-badge')).toHaveText(ar ? 'مخصص' : 'Custom');
      await expect(page.getByTestId('role-category-hint')).toHaveText(
        ar ? 'الأدوار المخصصة قابلة للتعديل الكامل، بما في ذلك الحذف.' : 'Custom roles are fully editable, including deletion.',
      );
      await expect(page.getByTestId('role-permissions').locator('input:checked')).toHaveCount(5);
      await expect(page.getByText(ar ? 'ملاحظة على التعديل (اختياري)' : 'Change note (optional)')).toBeVisible();
      await expect(page.getByTestId('role-save')).toHaveText(ar ? 'حفظ التغييرات' : 'Save Changes');
      await look(page, language, 'role-edit');

      // Someone else saved it meanwhile.
      team.roles.find((role) => role.id === SUPPORT)!.version = 'r2';
      await page.getByTestId('box-tickets.assign').check();
      await page.getByTestId('role-save').click();
      await expect(page.getByTestId('role-changed')).toContainText(ar ? 'تغيّرت هذه البيانات منذ فتحتها.' : 'This record changed since you opened it.');
      await page.getByTestId('role-changed').getByRole('button').click();
      await expect(page.getByTestId('role-changed')).toHaveCount(0);

      await page.getByTestId('box-tickets.assign').check();
      await page.getByTestId('role-note').fill('يسند التذاكر الآن');
      await page.getByTestId('role-save').click();
      await expect(page).toHaveURL(`${OPS}/operators/roles`);
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم تحديث الدور.' : 'Role updated.');
      expect(platform.bodies.at(-1)).toMatchObject({ note: 'يسند التذاكر الآن', version: 'r2' });

      // The platform names what was refused, whatever the form let through.
      await page.goto(`${OPS}/operators/roles/${SUCCESS}/edit`);
      await page.route(`**/v1/ops/roles/${SUCCESS}`, async (route) => {
        if (route.request().method() === 'PUT') {
          await route.fulfill({
            status: 403,
            headers: { 'access-control-allow-origin': OPS, 'access-control-allow-credentials': 'true', 'content-type': 'application/problem+json' },
            body: JSON.stringify({ type: 'x', title: 'x', status: 403, code: 'OPS_PERMISSIONS_NOT_HELD', traceId: 't', permissions: ['settings.view', 'settings.edit'] }),
          });
        } else {
          await route.fallback();
        }
      });
      await page.getByTestId('role-save').click();
      await expect(page.getByTestId('role-problem')).toHaveText(
        ar
          ? 'لا يمكنك إضافة صلاحيات ليست لديك: عرض الإعدادات، تعديل إعدادات المنصة.'
          : "You can't add permissions you don't have: View Settings, Edit Platform Settings.",
      );
      await look(page, language, 'role-edit-refused');
    });

    test('is used with the keyboard alone', async ({ page }) => {
      await open(page, language, '/operators/roles/new');
      await page.getByTestId('role-name').focus();
      await page.keyboard.type('دور بلوحة المفاتيح');
      await page.getByTestId('box-dashboard.view').focus();
      await page.keyboard.press('Space');
      await expect(page.getByTestId('box-dashboard.view')).toBeChecked();
      await page.getByTestId('role-save').focus();
      await page.keyboard.press('Enter');
      await expect(page).toHaveURL(`${OPS}/operators/roles`);

      // The delete dialog keeps the focus inside it and gives it back.
      await page.getByTestId('role-menu').last().focus();
      await page.keyboard.press('Enter');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('Enter');
      await expect(page.getByRole('dialog')).toBeVisible();
      for (let press = 0; press < 6; press++) {
        await page.keyboard.press('Tab');
        // Inside the dialog, or — past its last control — on nothing of the page behind it.
        expect(
          await page.evaluate(() => document.activeElement === document.body || document.activeElement?.closest('dialog') !== null),
        ).toBe(true);
      }
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);
    });
  });
}
