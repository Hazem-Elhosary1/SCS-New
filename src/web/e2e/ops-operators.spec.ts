import { Page, expect, test } from '@playwright/test';
import { OPS, OpsPlatform, opsEnglish, opsLooksWrong, opsOperator, opsPicture, opsPlatform } from './ops-platform';
import { BILLING, OpsTeam, SUCCESS, SUPER, SUPPORT, VIEWER, choose, opsTeam, rowsOf } from './ops-team-platform';

test.use({ locale: 'ar-EG' });

/**
 * Spec 011, User Stories 2 to 5: the team's list, an operator's page, the form in its two
 * modes and the account's dialogs — every part and word of contracts/operator-screens.md §1
 * to §6, in Arabic and English, at the design's size and at a phone's width. With `SCS_SHOTS`
 * set each state is pictured, to be laid beside its frame.
 */
const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };
const EASTERN = /[\u0660-\u0669\u06f0-\u06f9]/u;

type Language = 'ar' | 'en';

/**
 * A menu or a panel that is open: it was placed for the window as it was, so it is looked at
 * in that window alone. `open` opens it again at a phone's width, to be looked at there too.
 */
async function lookOpen(page: Page, language: Language, state: string, close: () => Promise<void>, open: () => Promise<void>): Promise<void> {
  expect(await page.locator('body').innerText(), `${language} ${state}: digits`).not.toMatch(EASTERN);
  expect(await opsLooksWrong(page), `${language} ${state}, wide`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/ops-${language}-${state}.png`, fullPage: true });
  }
  await close();
  await page.setViewportSize(NARROW);
  await open();
  expect(await opsLooksWrong(page), `${language} ${state}, narrow`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/narrow/${language}-${state}.png` });
  }
  await close();
  await page.setViewportSize(WIDE);
}

async function look(page: Page, language: Language, state: string): Promise<void> {
  // No Arabic-Indic digit on any screen of the operators console (FR-049).
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
  } else {
    await opsPicture(page, language, state);
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

const AHMED = '0199c0de-0000-7000-8000-0000000000a1';
const SARA = '0199c0de-0000-7000-8000-0000000000a2';
const MONA = '0199c0de-0000-7000-8000-0000000000a8';
const NOUR = '0199c0de-0000-7000-8000-0000000000a9';
const YOUSSEF = '0199c0de-0000-7000-8000-0000000000ab';
const RANA = '0199c0de-0000-7000-8000-0000000000ac';

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`the team's list — ${language}`, () => {
    test('shows the header, the tabs, the four cards, the table and the pager', async ({ page }) => {
      await open(page, language, '/operators');

      await expect(page.getByTestId('page-title')).toHaveText(ar ? 'المشغلون' : 'Operators');
      await expect(page.getByTestId('page-note')).toHaveText(
        ar ? 'إدارة فريق تشغيل المنصة وأدوارهم وصلاحياتهم.' : "Manage the platform's operations team, roles, and permissions.",
      );
      await expect(page.getByTestId('add-operator')).toContainText(ar ? 'إضافة مشغل' : 'Add Operator');
      await expect(page.getByRole('tab')).toHaveText(ar ? ['المشغلون', 'الأدوار والصلاحيات'] : ['Operators', 'Roles & Permissions']);
      await expect(page.getByTestId('ops-nav-operators')).toHaveAttribute('aria-current', 'page');

      const cards = page.getByTestId('operator-cards');
      await expect(cards.getByTestId('kpi-label')).toHaveText(
        ar
          ? ['إجمالي المشغلين', 'المشغلون النشطون', 'دعوات معلّقة', 'مفعّلون بالتحقق بخطوتين']
          : ['Total Operators', 'Active Operators', 'Pending Invitations', '2FA Enabled'],
      );
      await expect(cards.getByTestId('kpi-value')).toHaveText(['12', '10', '1', '12 / 12']);
      await expect(cards.getByTestId('kpi-note')).toHaveText(
        ar
          ? ['جميع الأدوار', '+2 هذا الشهر', 'بانتظار قبول الدعوة', '100% من الفريق']
          : ['All roles', '+2 this month', 'Awaiting acceptance', '100% of team'],
      );

      await expect(page.locator('thead th')).toHaveText(
        ar
          ? ['الاسم', 'الدور', 'آخر تسجيل دخول', 'الحالة', 'التحقق بخطوتين', 'الإجراءات']
          : ['Name', 'Role', 'Last Login', 'Status', '2FA', 'Actions'],
      );
      await expect(page.getByTestId('operator-row')).toHaveCount(8);
      const first = page.getByTestId('operator-row').first();
      await expect(first.getByTestId('operator-name')).toHaveText('أحمد سامي');
      await expect(first.getByTestId('operator-email')).toHaveText('ahmed.sami@smartclinicos.com');
      await expect(first.getByTestId('operator-role')).toHaveText(ar ? 'مسؤول عام' : 'Super Admin');
      await expect(first.getByTestId('operator-last')).toHaveText(ar ? 'منذ 5 دقائق' : '5 minutes ago');
      await expect(first.getByTestId('operator-state')).toHaveText(ar ? 'نشط' : 'Active');
      await expect(first.getByTestId('operator-two-step')).toHaveText(ar ? 'مفعّل' : 'Enabled');
      await expect(page.getByTestId('operator-row').nth(1).getByTestId('operator-role')).toHaveText('وكيل دعم فني');
      await expect(page.getByTestId('operator-row').nth(7).getByTestId('operator-state')).toHaveText(ar ? 'معطل' : 'Suspended');

      await expect(page.getByTestId('table-count')).toHaveText(ar ? 'عرض 1–8 من 12 مشغلًا' : 'Showing 1–8 of 12 operators');

      // Each card's number and note stand at the reading start — the right in Arabic — under
      // the label, whatever the number's own direction (`12 / 12` is written left to right).
      for (let card = 0; card < 4; card++) {
        const frame = await cards.locator('scs-kpi-card').nth(card).boundingBox();
        for (const part of ['kpi-value', 'kpi-note', 'kpi-label']) {
          const box = (await cards.locator('scs-kpi-card').nth(card).getByTestId(part).locator('bdi, :scope').first().boundingBox())!;
          const fromStart = ar ? frame!.x + frame!.width - (box.x + box.width) : box.x - frame!.x;
          expect(fromStart, `${language} card ${card} ${part}: distance from the reading start`).toBeLessThan(24);
        }
      }
      await look(page, language, 'operators-list');

      await page.getByTestId('page-2').click();
      await expect(page.getByTestId('operator-row')).toHaveCount(4);
      await expect(page.getByTestId('table-count')).toHaveText(ar ? 'عرض 9–12 من 12 مشغلًا' : 'Showing 9–12 of 12 operators');
      await expect(page.getByTestId('operator-row').first().getByTestId('operator-state')).toHaveText(ar ? 'مدعو' : 'Invited');
      await expect(page.getByTestId('operator-row').first().getByTestId('operator-last')).toHaveText(
        ar ? 'لم يسجل الدخول بعد' : "Hasn't signed in yet",
      );
    });

    test('searches, filters by what exists, and says when nothing matches', async ({ page }) => {
      const { platform } = await open(page, language, '/operators');

      await page.getByTestId('list-search').fill('sara');
      await expect(page.getByTestId('operator-row')).toHaveCount(1);
      await expect(page.getByTestId('operator-name')).toHaveText('سارة أحمد');
      // The cards are about the whole team, whatever is searched for.
      await expect(page.getByTestId('operator-cards').getByTestId('kpi-value').first()).toHaveText('12');

      await page.getByTestId('list-search').fill('nobody-at-all');
      await expect(page.getByTestId('operators-none')).toContainText(
        ar ? 'لا يوجد مشغلون مطابقون لبحثك أو فلاترك.' : 'No operators match your search or filters.',
      );
      await look(page, language, 'operators-none');
      await page.getByTestId('operators-clear').click();
      await expect(page.getByTestId('operator-row')).toHaveCount(8);

      await page.getByTestId('list-filter').click();
      await expect(page.getByTestId('filter-title')).toHaveText(ar ? 'فلترة النتائج' : 'Filter results');
      // Only what some operator has is offered.
      await expect(await rowsOf(page, 'filter-state')).toHaveText(
        ar ? ['الكل', 'نشط', 'مدعو', 'معطل'] : ['All', 'Active', 'Invited', 'Suspended'],
      );
      await page.keyboard.press('Escape');
      await expect(await rowsOf(page, 'filter-role')).toHaveText(
        ar
          ? ['الكل', 'مسؤول عام', 'مسؤول الفوترة', 'مشاهد فقط', 'وكيل دعم فني', 'نجاح العملاء']
          : ['All', 'Super Admin', 'Billing Manager', 'Viewer Only', 'وكيل دعم فني', 'نجاح العملاء'],
      );
      await page.keyboard.press('Escape');
      await lookOpen(
        page,
        language,
        'operators-filter',
        () => page.getByTestId('filter-close').click(),
        () => page.getByTestId('list-filter').click(),
      );
      await page.getByTestId('list-filter').click();
      await choose(page, 'filter-state', 'suspended');
      await page.getByTestId('filter-apply').click();
      await expect(page.getByTestId('operator-row')).toHaveCount(1);
      await expect(page.getByTestId('operator-name')).toHaveText('منى إبراهيم');
      expect(platform.calls.filter((call) => call === 'GET /v1/ops/operators').length).toBeGreaterThan(2);
    });

    test('says so while loading, when it failed, and when there is nobody', async ({ page }) => {
      const { team } = await open(page, language, '/operators', SUPER, { failing: { 'GET /v1/ops/operators': 500 } });
      await expect(page.getByTestId('operators-failed')).toContainText(ar ? 'حدث خطأ ما' : 'Something went wrong');
      await expect(page.getByTestId('operators-failed')).toContainText(
        ar ? 'تعذّر جلب قائمة المشغلين. تأكد من الاتصال وحاول مرة أخرى.' : "Couldn't load the operators list. Check your connection and try again.",
      );
      await look(page, language, 'operators-failed');

      team.failing = {};
      team.delay = 1500;
      await page.getByTestId('operators-retry').click();
      await expect(page.getByTestId('operators-loading')).toBeVisible();
      await look(page, language, 'operators-loading');
      await expect(page.getByTestId('operator-row')).toHaveCount(8);

      team.delay = 0;
      team.operators = [];
      await page.reload();
      await expect(page.getByTestId('operators-empty')).toContainText(ar ? 'لا يوجد مشغلون' : 'No operators');
      await expect(page.getByTestId('operators-empty-add')).toBeVisible();
      await look(page, language, 'operators-empty');
    });

    test('a row opens the operator, and its menu offers what the role allows', async ({ page }) => {
      const { team } = await open(page, language, '/operators');

      await page.getByTestId('row-menu').nth(1).click();
      await expect(page.getByTestId('row-menu-items').getByRole('menuitem')).toHaveText(
        ar ? ['عرض التفاصيل', 'تعديل المشغل'] : ['View details', 'Edit operator'],
      );
      await lookOpen(
        page,
        language,
        'operators-row-menu',
        () => page.keyboard.press('Escape'),
        () => page.getByTestId('row-menu').nth(1).click(),
      );
      // Escape closes the menu and gives the focus back to what opened it.
      await page.getByTestId('row-menu').nth(1).click();
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('row-menu').nth(1)).toBeFocused();

      await page.getByTestId('operator-name').nth(1).click();
      await expect(page).toHaveURL(`${OPS}/operators/${SARA}`);

      // Whoever only sees the team: no add button, and no "edit" in the menu.
      team.actAs(VIEWER);
      await page.goto(`${OPS}/operators`);
      await expect(page.getByTestId('operator-row')).toHaveCount(8);
      await expect(page.getByTestId('add-operator')).toHaveCount(0);
      await page.getByTestId('row-menu').first().click();
      await expect(page.getByTestId('row-menu-items').getByRole('menuitem')).toHaveText(ar ? ['عرض التفاصيل'] : ['View details']);
      await look(page, language, 'operators-viewer');
    });
  });

  test.describe(`keyboard and screen reader — ${language}`, () => {
    test('the list names its parts, its menu works by keys, and what can be pressed is big enough on a phone', async ({ page }) => {
      await open(page, language, '/operators');

      // A tab list with its two tabs; a table with a caption and header cells.
      const tabs = page.getByRole('tablist');
      await expect(tabs).toHaveAttribute('aria-label', ar ? 'أقسام الصفحة' : 'Page sections');
      await expect(tabs.getByRole('tab', { selected: true })).toHaveText(ar ? 'المشغلون' : 'Operators');
      await expect(page.getByTestId('operators-table').locator('caption')).toHaveText(ar ? 'قائمة المشغلين' : 'Operators list');
      await expect(page.locator('thead th[scope="col"]')).toHaveCount(6);
      await expect(page.getByTestId('row-menu').first()).toHaveAttribute(
        'aria-label',
        ar ? 'إجراءات أحمد سامي' : 'Actions for أحمد سامي',
      );

      // The row menu: Enter opens it, the arrows move in it, Escape closes it and gives the focus back.
      await page.getByTestId('row-menu').first().focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('row-menu-items')).toBeVisible();
      await page.keyboard.press('ArrowDown');
      await expect(page.getByTestId('row-edit')).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('row-menu-items')).toHaveCount(0);
      await expect(page.getByTestId('row-menu').first()).toBeFocused();

      // And Enter on a choice takes it.
      await page.keyboard.press('Enter');
      await page.keyboard.press('Enter');
      await expect(page).toHaveURL(`${OPS}/operators/${AHMED}`);

      // A dialog names itself by its title; a passing notice is announced politely.
      await page.goto(`${OPS}/operators/${SARA}`);
      await page.getByTestId('operator-reset').focus();
      await page.keyboard.press('Enter');
      const dialog = page.getByRole('dialog');
      await expect(dialog).toHaveAccessibleName(ar ? 'إعادة تعيين كلمة المرور' : 'Reset Password');
      await page.getByTestId('link-confirm').focus();
      await page.keyboard.press('Enter');
      await expect(page.locator('scs-toast-outlet')).toHaveAttribute('aria-live', 'polite');
      await expect(page.getByTestId('toast').last()).toBeVisible();

      // At a phone's width, everything that is pressed is at least 40 by 40.
      await page.setViewportSize(NARROW);
      for (const path of ['/operators', `/operators/${SARA}`, '/operators/new', '/operators/roles', '/operators/roles/new']) {
        await page.goto(`${OPS}${path}`);
        await page.locator('main').getByRole('button').first().waitFor();
        const small = await page.evaluate(() =>
          [...document.querySelectorAll('main button, main a[href], main select, main input:not([type="checkbox"]), main textarea')]
            .filter((element) => {
              // A field is pressed anywhere in the box drawn around it.
              const pressed = element.matches('input, select, textarea') ? (element.closest('label.search, .box') ?? element) : element;
              const box = pressed.getBoundingClientRect();
              return box.width > 0 && box.height > 0 && (box.width < 40 || box.height < 40) && !element.closest('scs-breadcrumb');
            })
            .map((element) => {
              const box = element.getBoundingClientRect();
              return `${element.getAttribute('data-testid') ?? element.tagName.toLowerCase()} ${Math.round(box.width)}x${Math.round(box.height)}`;
            }),
        );
        expect(small, `${language} ${path}: controls under 40 by 40`).toEqual([]);
      }
    });
  });

  test.describe(`one operator — ${language}`, () => {
    test('shows who they are, what their role holds and what they did', async ({ page }) => {
      await open(page, language, `/operators/${AHMED}`);

      await expect(page.getByTestId('breadcrumb')).toContainText(ar ? 'تفاصيل المشغل' : 'Operator Details');
      await expect(page.getByTestId('operator-title')).toHaveText('أحمد سامي');
      await expect(page.getByTestId('operator-under')).toContainText(ar ? 'مسؤول عام' : 'Super Admin');
      await expect(page.getByTestId('operator-under')).toContainText('ahmed.sami@smartclinicos.com');

      const info = page.getByTestId('operator-info');
      await expect(info.locator('dt')).toHaveText(
        ar
          ? ['الدور', 'تاريخ الانضمام', 'آخر تسجيل دخول', 'التحقق بخطوتين (2FA)', 'رقم الموبايل', 'القسم', 'المسمى الوظيفي', 'أضيف بواسطة']
          : ['Role', 'Join Date', 'Last Login', 'Two-Factor Authentication (2FA)', 'Mobile Number', 'Department', 'Job Title', 'Added By'],
      );
      await expect(page.getByTestId('info-joined')).toHaveText(ar ? '11 يناير 2026' : 'January 11, 2026');
      await expect(page.getByTestId('info-mobile')).toHaveText('+20 100 111 2233');
      await expect(page.getByTestId('info-department')).toHaveText(ar ? 'الإدارة العليا' : 'Senior Management');
      await expect(page.getByTestId('info-added-by')).toHaveText(ar ? 'النظام (أول مشغل)' : 'System (first operator)');
      await expect(page.getByTestId('operator-permissions').locator('li')).toHaveCount(28);
      await expect(page.getByTestId('operator-permissions').locator('li').first()).toHaveText(ar ? 'عرض لوحة التحكم' : 'View Dashboard');

      await expect(page.getByTestId('performance-activeSessions')).toContainText(ar ? 'الجلسات النشطة' : 'Active Sessions');
      await expect(page.getByTestId('activity-line')).toHaveCount(4);
      await expect(page.getByTestId('activity-line').nth(0)).toContainText(
        ar ? 'سجّل الدخول من Chrome على Windows' : 'Logged in from Chrome على Windows',
      );
      await expect(page.getByTestId('activity-line').nth(1)).toContainText(
        ar ? 'دعا نور خالد بدور مشاهد فقط' : 'Invited نور خالد as Viewer Only',
      );
      await expect(page.getByTestId('activity-line').nth(2)).toContainText(
        ar ? 'غيّر دور كريم عادل إلى وكيل دعم فني' : "Changed كريم عادل's role to وكيل دعم فني",
      );

      // One's own page: edit and reset, and no suspending oneself.
      await expect(page.getByTestId('operator-actions').getByRole('button')).toHaveText(
        ar ? ['تعديل', 'إعادة تعيين كلمة المرور'] : ['Edit', 'Reset Password'],
      );
      await look(page, language, 'operator');
    });

    test('offers each action only when it applies, and nothing to a viewer', async ({ page }) => {
      const { team } = await open(page, language, `/operators/${SARA}`);
      const actions = page.getByTestId('operator-actions').getByRole('button');
      await expect(actions).toHaveText(ar ? ['تعديل', 'إعادة تعيين كلمة المرور', 'تعليق الحساب'] : ['Edit', 'Reset Password', 'Suspend Account']);

      await page.goto(`${OPS}/operators/${MONA}`);
      await expect(actions).toHaveText(ar ? ['تعديل', 'إعادة التفعيل'] : ['Edit', 'Reactivate']);
      await look(page, language, 'operator-suspended');

      await page.goto(`${OPS}/operators/${NOUR}`);
      await expect(actions).toHaveText(ar ? ['تعديل', 'إعادة تعيين كلمة المرور'] : ['Edit', 'Reset Password']);
      await expect(page.getByTestId('activity-none')).toHaveText(ar ? 'لا نشاط بعد' : 'No activity yet');
      await expect(page.getByTestId('info-last')).toHaveText(ar ? 'لم يسجل الدخول بعد' : "Hasn't signed in yet");

      await page.goto(`${OPS}/operators/${YOUSSEF}`);
      await expect(page.getByTestId('operator-unlock')).toHaveText(ar ? 'فك قفل الحساب' : 'Unlock Account');
      await look(page, language, 'operator-locked');
      await page.getByTestId('operator-unlock').click();
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم فك قفل الحساب.' : 'Account unlocked.');
      await expect(page.getByTestId('operator-unlock')).toHaveCount(0);

      team.actAs(VIEWER);
      await page.goto(`${OPS}/operators/${SARA}`);
      await expect(page.getByTestId('operator-title')).toHaveText('سارة أحمد');
      await expect(page.getByTestId('operator-actions').getByRole('button')).toHaveCount(0);
    });

    test('says when the operator does not exist or could not be read', async ({ page }) => {
      const { team } = await open(page, language, '/operators/0199c0de-0000-7000-8000-0000000000ff');
      await expect(page.getByTestId('operator-missing')).toContainText(ar ? 'المشغل غير موجود' : 'Operator not found');
      await look(page, language, 'operator-missing');

      team.failing = { [`GET /v1/ops/operators/${SARA}`]: 500 };
      await page.goto(`${OPS}/operators/${SARA}`);
      await expect(page.getByTestId('operator-failed')).toContainText(ar ? 'تعذّر جلب بيانات المشغل.' : "Couldn't load the operator.");
      team.failing = {};
      await page.getByTestId('operator-retry').click();
      await expect(page.getByTestId('operator-title')).toHaveText('سارة أحمد');
    });

    test('suspends with a reason, reactivates, and refuses the last super admin inside the dialog', async ({ page }) => {
      const { platform, team } = await open(page, language, `/operators/${SARA}`);

      await page.getByTestId('operator-suspend').click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText(ar ? 'تعليق حساب المشغل' : 'Suspend Operator Account');
      await expect(page.getByTestId('suspend-text')).toHaveText(
        ar
          ? 'سيتم تعليق حساب المشغل سارة أحمد ولن يتمكن من تسجيل الدخول إلى لوحة التشغيل حتى تتم إعادة تفعيله.'
          : "سارة أحمد's account will be suspended and they won't be able to sign in to the Ops Console until reactivated.",
      );
      await expect(page.getByTestId('suspend-kept')).toHaveText(
        ar ? 'ستُحفظ بياناته ويمكن إعادة تفعيل الحساب لاحقًا في أي وقت.' : 'Their data will be preserved and the account can be reactivated at any time.',
      );
      await expect(dialog.getByText(ar ? 'السبب (اختياري)' : 'Reason (optional)')).toBeVisible();
      await look(page, language, 'suspend-dialog');

      await page.getByTestId('suspend-reason').fill('غادر الفريق');
      await page.getByTestId('suspend-confirm').click();
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم تعليق حساب المشغل.' : 'The operator account was suspended.');
      expect(platform.bodies.at(-1)).toEqual({ reason: 'غادر الفريق' });
      await expect(dialog).toHaveCount(0);

      await page.getByTestId('operator-reactivate').click();
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم إعادة تفعيل حساب المشغل.' : 'The operator account was reactivated.');
      await expect(page.getByTestId('operator-suspend')).toBeVisible();

      // The only other active super admin, once the signed-in one no longer counts.
      team.operators.find((person) => person.id === AHMED)!.state = 'suspended';
      await page.goto(`${OPS}/operators/${RANA}`);
      await page.getByTestId('operator-suspend').click();
      await page.getByTestId('suspend-confirm').click();
      await expect(page.getByTestId('suspend-problem')).toHaveText(
        ar ? 'يجب أن يبقى مسؤول عام واحد نشط على الأقل.' : 'At least one active Super Admin must remain.',
      );
      await look(page, language, 'suspend-last');
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('operator-suspend')).toBeFocused();
    });

    test('sends a password link, and says when one was sent a moment ago', async ({ page }) => {
      const { platform, team } = await open(page, language, `/operators/${SARA}`);

      await page.getByTestId('operator-reset').click();
      await expect(page.getByRole('dialog')).toContainText(ar ? 'إعادة تعيين كلمة المرور' : 'Reset Password');
      await expect(page.getByTestId('link-text')).toHaveText(
        ar
          ? 'سيتم إرسال رابط إعادة تعيين كلمة المرور إلى بريد سارة أحمد الإلكتروني (sara.ahmed@smartclinicos.com).'
          : "A password reset link will be sent to سارة أحمد's email (sara.ahmed@smartclinicos.com).",
      );
      await look(page, language, 'link-dialog');

      team.link = 'too-soon';
      await page.getByTestId('link-confirm').click();
      await expect(page.getByTestId('link-problem')).toHaveText(
        ar
          ? 'أُرسل رابط لهذا الحساب منذ أقل من دقيقة. انتظر قليلًا ثم حاول مرة أخرى.'
          : 'A link was sent to this account less than a minute ago. Wait a moment and try again.',
      );
      await look(page, language, 'link-too-soon');

      team.link = 'not-sent';
      await page.getByTestId('link-confirm').click();
      await expect(page.getByTestId('link-problem')).toHaveText(ar ? 'تعذّر إرسال رابط إعادة التعيين.' : "Couldn't send the reset link.");

      team.link = 'ok';
      await page.getByTestId('link-confirm').click();
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم إرسال رابط إعادة التعيين.' : 'The reset link was sent.');
      expect(platform.calls).toContain(`POST /v1/ops/operators/${SARA}/password-link`);
      // Nothing on the screen holds a link.
      expect(await page.locator('body').innerText()).not.toContain('token');
    });
  });

  test.describe(`the operator's form — ${language}`, () => {
    test('adds an operator: the fields, the role picker with its reasons, and the errors at their fields', async ({ page }) => {
      const { platform } = await open(page, language, '/operators/new');

      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'إضافة مشغل جديد' : 'Add New Operator');
      await expect(page.getByTestId('form-note')).toHaveText(
        ar ? 'أضف عضو جديد للفريق وحدد دوره وصلاحياته داخل المنصة.' : 'Add a new team member and set their role and permissions on the platform.',
      );
      await expect(page.locator('form .name, form scs-text-field label')).toHaveText(
        ar
          ? ['الاسم الكامل', 'البريد الإلكتروني', 'الدور', 'رقم الموبايل (اختياري)', 'القسم', 'المسمى الوظيفي', 'لغة الواجهة الافتراضية']
          : ['Full Name', 'Email Address', 'Role', 'Mobile Number (optional)', 'Department', 'Job Title', 'Default Interface Language'],
      );
      await expect(page.getByTestId('field-name')).toHaveAttribute('placeholder', ar ? 'مثال: نور خالد' : 'e.g. Nour Khaled');
      await expect(page.getByTestId('field-email')).toHaveAttribute('placeholder', 'name@smartclinicos.com');
      await expect(page.getByTestId('switch-two-step')).toBeDisabled();
      await expect(page.getByTestId('switch-two-step-reason')).toHaveText(ar ? 'مطلوب لكل المشغلين' : 'Required for all operators');
      await expect(page.getByTestId('switch-invite')).toHaveAttribute('aria-checked', 'true');
      // On is the knob at the right, in Arabic as in English (the frame; spec 010's profile).
      const knobSide = async () => {
        const track = (await page.getByTestId('switch-invite').locator('.track').boundingBox())!;
        const knob = (await page.getByTestId('switch-invite').locator('.knob').boundingBox())!;
        return knob.x + knob.width / 2 > track.x + track.width / 2 ? 'right' : 'left';
      };
      expect(await knobSide()).toBe('right');
      await page.getByTestId('switch-invite').click();
      await page.waitForTimeout(250);
      expect(await knobSide()).toBe('left');
      await page.getByTestId('switch-invite').click();
      await page.waitForTimeout(250);
      await expect(page.getByTestId('form-save')).toHaveText(ar ? 'إرسال الدعوة' : 'Send Invite');
      await look(page, language, 'operator-add');

      // Empty: each required field says so, and the first takes the focus.
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('field-name-error')).toHaveText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.');
      await expect(page.getByTestId('field-email-error')).toHaveText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.');
      await expect(page.getByTestId('field-role-error')).toHaveText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.');
      await expect(page.getByTestId('field-name')).toBeFocused();
      await look(page, language, 'operator-add-errors');

      await page.getByTestId('field-name').fill('نور خالد');
      await page.getByTestId('field-email').fill('not-an-address');
      await choose(page, 'field-role', VIEWER);
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('field-email-error')).toHaveText(ar ? 'أدخل بريدًا إلكترونيًّا صحيحًا.' : 'Enter a valid email address.');

      // A role once chosen shows what it holds.
      await expect(page.getByTestId('role-permissions').locator('li')).toHaveCount(11);

      await page.getByTestId('field-email').fill('sara.ahmed@smartclinicos.com');
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('field-email-error')).toHaveText(
        ar ? 'هذا البريد مستعمل لمشغل آخر.' : 'This email already belongs to an operator.',
      );

      await page.getByTestId('field-email').fill('nour.new@smartclinicos.com');
      await choose(page, 'field-department', 'support');
      await page.getByTestId('field-job').fill('أخصائي دعم فني');
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('operator-title')).toHaveText('نور خالد');
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم إرسال الدعوة.' : 'Invitation sent.');
      expect(platform.bodies.at(-1)).toMatchObject({
        fullName: 'نور خالد', email: 'nour.new@smartclinicos.com', roleId: VIEWER, department: 'support', sendInvitation: true, twoStep: true,
      });
      // After inviting, no address on the screen holds a token: the link goes by email alone.
      expect(await page.content()).not.toMatch(/token=|#t=/u);
      expect(page.url()).not.toContain('token');
    });

    test('a role that may not be granted is disabled with its reason, and without the invitation the button says "save"', async ({ page }) => {
      const { team } = await open(page, language, '/operators/new', SUPPORT);
      // A support agent who was also given "add and edit operators".
      team.roles.find((role) => role.id === SUPPORT)!.codes.push('operators.view', 'operators.manage');
      team.actAs(SUPPORT);
      await page.reload();

      // The list is required: no "nothing" row. A role that may not be given is grey with its reason, and cannot be chosen.
      const options = await rowsOf(page, 'field-role');
      // The open list is as wide as its field, beneath it (design: Dropdown, Menu Panel).
      const field = (await page.getByTestId('field-role').boundingBox())!;
      const panel = (await page.getByTestId('field-role-panel').boundingBox())!;
      expect(Math.abs(panel.width - field.width)).toBeLessThan(2);
      expect(panel.y).toBeGreaterThan(field.y + field.height - 1);
      await expect(options.nth(0)).toHaveAttribute('aria-disabled', 'true');
      await expect(options.nth(0)).toContainText(ar ? 'يمنحه مسؤول عام فقط' : 'Only a Super Admin can grant it');
      await expect(options.nth(1)).toHaveAttribute('aria-disabled', 'true');
      await expect(options.nth(1)).toContainText(ar ? 'به صلاحيات ليست لديك' : "Holds permissions you don't have");
      await expect(options.nth(3)).not.toHaveAttribute('aria-disabled', 'true');
      await lookOpen(
        page,
        language,
        'operator-add-role-list',
        () => page.keyboard.press('Escape'),
        () => page.getByTestId('field-role').click(),
      );
      // Pressing a refused row chooses nothing and leaves the list open.
      await (await rowsOf(page, 'field-role')).nth(0).click({ force: true });
      await expect(page.getByTestId('field-role-panel')).toBeVisible();
      await page.keyboard.press('Escape');

      await page.getByTestId('switch-invite').click();
      await expect(page.getByTestId('form-save')).toHaveText(ar ? 'حفظ' : 'Save');

      // The platform refuses whatever the form let through: said at the role field.
      await page.getByTestId('field-name').fill('نور خالد');
      await page.getByTestId('field-email').fill('nour.other@smartclinicos.com');
      await choose(page, 'field-role', SUPPORT);
      await page.route('**/v1/ops/operators', async (route) => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 403,
            headers: { 'access-control-allow-origin': OPS, 'access-control-allow-credentials': 'true', 'content-type': 'application/problem+json' },
            body: JSON.stringify({ type: 'x', title: 'x', status: 403, code: 'OPS_ROLE_NOT_GRANTABLE', traceId: 't' }),
          });
        } else {
          await route.fallback();
        }
      });
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('field-role-error')).toHaveText(
        ar ? 'لا يمكنك منح دور به صلاحيات ليست لديك.' : "You can't give a role with permissions you don't have.",
      );
      await look(page, language, 'operator-add-role-refused');
    });

    test('edits an operator: the email is fixed, the note is there, and saving returns to the page', async ({ page }) => {
      const { platform } = await open(page, language, `/operators/${SARA}/edit`);

      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'تعديل بيانات المشغل' : 'Edit Operator');
      await expect(page.getByTestId('form-note')).toHaveText(
        ar ? 'حدّث بيانات سارة أحمد ودوره وصلاحياته داخل المنصة.' : "Update سارة أحمد's information, role, and permissions.",
      );
      await expect(page.getByTestId('field-email')).toHaveText('sara.ahmed@smartclinicos.com');
      await expect(page.locator('input[data-testid="field-email"]')).toHaveCount(0);
      await expect(page.getByTestId('email-fixed')).toHaveText(ar ? 'لا يمكن تغيير البريد الإلكتروني.' : "The email address can't be changed.");
      await expect(page.getByTestId('switch-invite')).toHaveCount(0);
      await expect(page.getByText(ar ? 'تفعيل التحقق بخطوتين (2FA) لهذا المشغل' : 'Require two-factor authentication (2FA) for this operator')).toBeVisible();
      await expect(page.getByText(ar ? 'ملاحظة على التعديل (اختياري)' : 'Change note (optional)')).toBeVisible();
      await expect(page.getByText(ar ? 'تظهر في سجل نشاط العنصر وفي سجل نشاطك.' : "Shown in this record's activity log and in yours.")).toBeVisible();
      await expect(page.getByTestId('form-save')).toHaveText(ar ? 'حفظ التغييرات' : 'Save Changes');
      await look(page, language, 'operator-edit');

      await page.getByTestId('field-job').fill('قائدة فريق الدعم');
      await choose(page, 'field-role', BILLING);
      await page.getByTestId('field-note').fill('انتقلت إلى الفوترة');
      await page.getByTestId('form-save').click();

      await expect(page).toHaveURL(`${OPS}/operators/${SARA}`);
      await expect(page.getByTestId('toast').last()).toContainText(ar ? 'تم تحديث بيانات المشغل.' : 'Operator details updated.');
      await expect(page.getByTestId('info-role')).toHaveText(ar ? 'مسؤول الفوترة' : 'Billing Manager');
      expect(platform.bodies.at(-1)).toMatchObject({ roleId: BILLING, jobTitle: 'قائدة فريق الدعم', note: 'انتقلت إلى الفوترة', version: 'v1' });
      expect(platform.bodies.at(-1)).not.toHaveProperty('email');
    });

    test('the three refusals of editing a role, and a record changed elsewhere', async ({ page }) => {
      const { team } = await open(page, language, `/operators/${AHMED}/edit`);

      // One's own account: the role is locked, with its sentence.
      await expect(page.getByTestId('field-role')).toBeDisabled();
      await expect(page.getByTestId('field-role-hint')).toHaveText(ar ? 'لا يمكنك تغيير دورك.' : "You can't change your own role.");
      await look(page, language, 'operator-edit-own-role');

      // The last active super admin.
      team.operators.find((person) => person.id === AHMED)!.state = 'suspended';
      await page.goto(`${OPS}/operators/${RANA}/edit`);
      await choose(page, 'field-role', VIEWER);
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('field-role-error')).toHaveText(
        ar ? 'يجب أن يبقى مسؤول عام واحد نشط على الأقل.' : 'At least one active Super Admin must remain.',
      );
      await expect(page.getByTestId('field-role')).toBeFocused();
      await look(page, language, 'operator-edit-last-super-admin');

      // Changed by someone else while the form was open.
      await page.goto(`${OPS}/operators/${SARA}/edit`);
      await expect(page.getByTestId('field-job')).toBeVisible();
      team.operators.find((person) => person.id === SARA)!.version = 'v2';
      await page.getByTestId('field-job').fill('شيء آخر');
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('form-changed')).toContainText(
        ar ? 'تغيّرت هذه البيانات منذ فتحتها.' : 'This record changed since you opened it.',
      );
      await look(page, language, 'operator-edit-changed');
      await page.getByTestId('form-changed').getByRole('button', { name: ar ? 'إعادة التحميل' : 'Reload' }).click();
      await expect(page.getByTestId('form-changed')).toHaveCount(0);
      await expect(page.getByTestId('field-job')).toHaveValue('');

      // A role the editor may not grant, refused by the platform.
      team.roles.find((role) => role.id === SUCCESS)!.codes = ['operators.view', 'operators.manage', 'tickets.view'];
      team.actAs(SUCCESS);
      await page.goto(`${OPS}/operators/${SARA}/edit`);
      await expect((await rowsOf(page, 'field-role')).filter({ hasText: ar ? 'مسؤول عام' : 'Super Admin' })).toHaveAttribute('aria-disabled', 'true');
    });
  });
}
