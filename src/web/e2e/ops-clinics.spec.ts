import { Page, expect, test } from '@playwright/test';
import { ACTIVE, FakeClinic, OpsClinics, designClinics, fakeClinic, opsClinics } from './ops-clinics-platform';
import { OPS, OpsPlatform, opsEnglish, opsLooksWrong, opsOperator, opsPicture, opsPlatform } from './ops-platform';
import { SUCCESS, VIEWER, choose, opsTeam } from './ops-team-platform';

test.use({ locale: 'ar-EG' });

/**
 * Spec 015, User Stories 1 to 5: the clinics list, the add and edit form, the detail with its
 * account manager, sign-in address and invitation, and the settings tab — every part and word of
 * contracts/clinics-screens.md, in Arabic and English, at the design's size and at a phone's width.
 * With `SCS_SHOTS` set each state is pictured, to be laid beside its frame.
 */

const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };
const EASTERN = /[٠-٩۰-۹]/u;

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
  } else {
    await opsPicture(page, language, state);
  }
}

interface World {
  platform: OpsPlatform;
  book: OpsClinics;
}

async function open(page: Page, language: Language, path: string, role = SUCCESS, clinics: Partial<OpsClinics> = {}): Promise<World> {
  if (language === 'en') {
    await opsEnglish(page);
  }
  await page.setViewportSize(WIDE);
  const platform = await opsPlatform(page, {
    signedIn: true,
    operator: language === 'en' ? { ...opsOperator, fullName: 'Ahmed Sami', language: 'en', roleName: 'Customer Success' } : opsOperator,
  });
  await opsTeam(page, platform, { myRoleId: role });
  const book = await opsClinics(page, platform, clinics);
  await page.goto(`${OPS}${path}`);
  return { platform, book };
}

const rowOf = (page: Page, id: string) => page.locator(`[data-testid="clinic-row"][data-id="${id}"]`);

async function fill(page: Page, testId: string, value: string): Promise<void> {
  await page.getByTestId(testId).fill(value);
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`the clinics list — ${language}`, () => {
    test('opens from the side bar for whoever may view clinics, and is refused to anyone else', async ({ page }) => {
      const world = await open(page, language, '/', VIEWER, { clinics: designClinics() });
      await page.getByTestId('ops-nav-clinics').click();
      await expect(page).toHaveURL(/\/clinics$/u);
      await expect(page.getByRole('heading', { name: ar ? 'العيادات' : 'Clinics', exact: true })).toBeVisible();
      await expect(page.getByTestId('clinics-add')).toHaveCount(0);
      await expect(page.getByTestId('row-menu')).toHaveCount(0);

      world.platform.permissions = ['billing.view'];
      await page.goto(`${OPS}/clinics`);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
      await expect(page.getByTestId('ops-nav-clinics')).toHaveCount(0);
    });

    test('lists the clinics with the cards, every column’s words, newest first and eight a page', async ({ page }) => {
      const clinics = [...designClinics(), fakeClinic({ nameEn: 'Ninth Clinic', nameAr: 'العيادة التاسعة', createdAt: '2026-08-01T07:30:00Z' })];
      await open(page, language, '/clinics', SUCCESS, { clinics });

      await expect(page.getByTestId('clinics-card-total')).toContainText('9');
      await expect(page.getByTestId('clinics-card-total')).toContainText(ar ? '+2 هذا الشهر' : '+2 this month');
      await expect(page.getByTestId('clinics-card-active')).toContainText(ar ? '% من الإجمالي' : '% of total');
      await expect(page.getByTestId('clinics-card-trial')).toContainText(ar ? 'قاربت على الانتهاء' : 'Nearing expiry');
      await expect(page.getByTestId('clinics-card-follow')).toContainText(ar ? '0 متأخرة · 0 قراءة فقط · 0 موقوفة' : '0 past due · 0 read-only · 0 suspended');

      const rows = page.getByTestId('clinic-row');
      await expect(rows).toHaveCount(8);
      await expect(rows.first().getByTestId('clinic-name')).toHaveText(ar ? 'عيادة النور' : 'Al Noor Clinic');
      await expect(rows.first().getByTestId('clinic-mark')).toHaveText(ar ? 'ن' : 'AN');
      await expect(rows.first().getByTestId('clinic-status')).toHaveText(ar ? 'نشطة' : 'Active');
      await expect(rows.nth(2).getByTestId('clinic-status')).toHaveText(ar ? 'تجربة' : 'Trial');
      await expect(rows.nth(2).getByTestId('clinic-length')).toHaveText(ar ? 'تجربة 14 يومًا' : '14-day trial');
      await expect(rows.nth(2).getByTestId('clinic-ends')).toHaveText(ar ? 'خلال 4 أيام' : 'in 4 days');
      await expect(rows.first().getByTestId('clinic-added')).toContainText(ar ? 'أضيفت 25 أغسطس 2026 · ' : 'Added August 25, 2026 · ');
      await expect(page.getByText(ar ? 'عرض 1–8 من 9 عيادة' : 'Showing 1–8 of 9 clinics')).toBeVisible();
      await look(page, language, 'clinics-list');

      await page.getByTestId('scs-table-next').or(page.getByRole('button', { name: ar ? 'التالي' : 'Next' })).first().click();
      await expect(rows).toHaveCount(1);
    });

    test('searches, filters with chips, and sorts by the end with no date last', async ({ page }) => {
      const world = await open(page, language, '/clinics', SUCCESS, { clinics: designClinics() });
      await page.getByRole('searchbox').fill('rahma');
      await expect(page.getByTestId('clinic-row')).toHaveCount(1);
      await page.getByRole('searchbox').fill('');
      await expect(page.getByTestId('clinic-row')).toHaveCount(8);

      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await page.getByTestId('filter-status-trial').click();
      await look(page, language, 'clinics-filter');
      await page.getByRole('button', { name: ar ? 'تطبيق الفلاتر' : 'Apply filters' }).click();
      await expect(page.getByTestId('clinic-row')).toHaveCount(4);
      await expect(page.getByText(ar ? 'الحالة: تجربة' : 'Status: Trial')).toBeVisible();
      await page.getByRole('button', { name: ar ? 'مسح الكل' : 'Clear all' }).click();
      await expect(page.getByTestId('clinic-row')).toHaveCount(8);

      // A choice left unapplied is forgotten when the panel opens again (FR-010; T083).
      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await page.getByTestId('filter-status-trial').click();
      await page.keyboard.press('Escape');
      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await expect(page.getByTestId('filter-status-trial')).not.toBeChecked();
      await page.keyboard.press('Escape');

      await page.getByTestId('sort-ends').click();
      expect(world.platform.calls.at(-1) ?? '').toContain('/v1/ops/clinics');
      await expect(page.locator('th[aria-sort="ascending"]')).toHaveCount(1);
      await expect(page.getByTestId('clinic-row').first().getByTestId('clinic-name')).toHaveText(ar ? 'مركز نور الطب' : 'Nour Medical Center');
      await expect(page.getByTestId('clinic-row').last().getByTestId('clinic-ends')).toHaveText('');
      await page.getByTestId('sort-ends').click();
      await expect(page.locator('th[aria-sort="descending"]')).toHaveCount(1);
      await expect(page.getByTestId('clinic-row').last().getByTestId('clinic-ends')).toHaveText('');
    });

    test('says there are no clinics yet, finds none, and could not load', async ({ page }) => {
      await open(page, language, '/clinics', SUCCESS);
      await expect(page.getByTestId('clinics-empty')).toContainText(ar ? 'لا توجد عيادات' : 'No clinics');
      await expect(page.getByTestId('clinics-card-total')).toContainText(ar ? 'لا توجد بيانات' : 'No data');
      await expect(page.getByTestId('clinics-empty-add')).toBeVisible();
      await look(page, language, 'clinics-empty');

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/clinics', SUCCESS, { clinics: designClinics() });
      await page.getByRole('searchbox').fill('no-such-clinic');
      await expect(page.getByTestId('clinics-no-results')).toContainText(
        ar ? 'لا توجد عيادات مطابقة للبحث أو الفلاتر المستخدمة.' : 'No clinics match the search or filters used.',
      );
      await page.getByTestId('clinics-clear').click();
      await expect(page.getByTestId('clinic-row')).toHaveCount(8);

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/clinics', SUCCESS, { failing: { 'GET /v1/ops/clinics': 500 } });
      await expect(page.getByTestId('clinics-error')).toContainText(ar ? 'حدث خطأ ما' : 'Something went wrong');
      await expect(page.getByTestId('clinics-cards')).toHaveCount(0);
      await look(page, language, 'clinics-error');
    });

    test('opens a clinic from its row and edits it from the menu', async ({ page }) => {
      const clinics = designClinics();
      await open(page, language, '/clinics', SUCCESS, { clinics });
      await rowOf(page, clinics[0]!.id).getByTestId('row-menu').click();
      await page.getByTestId('row-edit').click();
      await expect(page).toHaveURL(new RegExp(`/clinics/${clinics[0]!.id}/edit$`, 'u'));
      await page.goBack();
      await rowOf(page, clinics[1]!.id).click();
      await expect(page).toHaveURL(new RegExp(`/clinics/${clinics[1]!.id}$`, 'u'));
    });
  });

  test.describe(`adding a clinic — ${language}`, () => {
    test('checks each field, and adds the clinic and lands on it', async ({ page }) => {
      const world = await open(page, language, '/clinics/new', SUCCESS, { takenEmails: ['taken@clinic.example'] });
      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'إضافة عيادة جديدة' : 'Add New Clinic');
      await look(page, language, 'clinic-add');

      await page.getByTestId('clinic-save').click();
      await expect(page.getByTestId('clinic-name-ar-error')).toContainText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.');
      await expect(page.locator('[aria-invalid="true"]').first()).toBeFocused();

      await fill(page, 'clinic-name-ar', 'عيادة الأمل');
      await fill(page, 'clinic-name-en', 'Al Amal Clinic');
      await fill(page, 'clinic-owner-name', 'رانيا فؤاد');
      await fill(page, 'clinic-email', 'taken@clinic.example');
      await fill(page, 'clinic-mobile', '12ab');
      await fill(page, 'clinic-address', '5 شارع الجامعة');
      await fill(page, 'clinic-tax', '12345');
      await choose(page, 'clinic-plan', 'plan-basic');
      await choose(page, 'clinic-specialty', 'dental');
      await page.getByTestId('clinic-save').click();
      await expect(page.getByTestId('clinic-tax-error')).toContainText(ar ? 'أدخل رقم التسجيل الضريبي المكوّن من 9 أرقام.' : 'Enter the 9-digit tax registration number.');
      await expect(page.getByTestId('clinic-mobile-error')).toContainText(ar ? 'رقم الموبايل غير صحيح.' : 'The mobile number is not valid.');
      await look(page, language, 'clinic-add-invalid');

      await fill(page, 'clinic-tax', '123 456 789');
      await fill(page, 'clinic-mobile', '+966 50 123 4567');
      await page.getByTestId('clinic-save').click();
      await expect(page.getByTestId('clinic-email-error')).toContainText(ar ? 'هذا البريد مسجَّل لحساب آخر على المنصة.' : 'This email belongs to another account on the platform.');

      await fill(page, 'clinic-email', 'rania@alamal.example');
      await page.getByTestId('clinic-save').click();
      await expect(page.getByText(ar ? 'تم إنشاء العيادة!' : 'Clinic created!')).toBeVisible();
      await expect(page).toHaveURL(/\/clinics\/0c11a000-/u);
      await expect(page.getByTestId('clinic-detail-name')).toHaveText(ar ? 'عيادة الأمل' : 'Al Amal Clinic');
      expect(world.platform.bodies.at(-1)).toMatchObject({ mobile: '+966 50 123 4567', taxNumber: '123 456 789', specialty: 'dental' });
    });

    test('says the plan was disabled meanwhile, and is refused to a viewer', async ({ page }) => {
      await open(page, language, '/clinics/new', SUCCESS, { disabledPlans: ['plan-basic'] });
      await fill(page, 'clinic-name-ar', 'عيادة');
      await fill(page, 'clinic-owner-name', 'رانيا');
      await fill(page, 'clinic-email', 'r@x.example');
      await fill(page, 'clinic-mobile', '01001234567');
      await fill(page, 'clinic-address', 'العنوان');
      await choose(page, 'clinic-plan', 'plan-basic');
      await choose(page, 'clinic-specialty', 'dental');
      await page.getByTestId('clinic-save').click();
      await expect(page.getByTestId('form-problem')).toContainText(ar ? 'تعذر إنشاء العيادة.' : 'The clinic could not be created.');
      // The banner keeps its icon beside its words (seen on the real stack).
      const icon = await page.getByTestId('form-problem').locator('scs-icon').first().boundingBox();
      const words = await page.getByTestId('form-problem').getByText(ar ? 'تعذر إنشاء العيادة.' : 'The clinic could not be created.').boundingBox();
      expect(Math.abs(icon!.y + icon!.height / 2 - (words!.y + words!.height / 2))).toBeLessThan(12);
      await look(page, language, 'clinic-add-refused');

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/clinics/new', VIEWER);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
    });
  });

  test.describe(`a clinic — ${language}`, () => {
    test('shows the head, the tabs and every row of the card', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      await open(page, language, `/clinics/${clinic.id}`, SUCCESS, { clinics: [clinic] });

      await expect(page.getByTestId('clinic-detail-name')).toHaveText(ar ? 'عيادة النور' : 'Al Noor Clinic');
      await expect(page.getByTestId('clinic-detail-mark')).toHaveText('AN');
      await expect(page.getByTestId('clinic-detail-status')).toHaveText(ar ? 'تجربة' : 'Trial');
      await expect(page.getByTestId('clinic-detail-line')).toContainText(ar ? 'جهة الاتصال: د. هيثم محمود · تاريخ الانضمام 25 أغسطس 2026 · باقة الاحترافية' : 'Contact: د. هيثم محمود · Joined August 25, 2026 · Professional plan');
      await expect(page.getByRole('tab')).toHaveCount(2);
      await expect(page.getByTestId('clinic-info-email')).toHaveText('haitham@alnoorclinic.example');
      await expect(page.getByTestId('clinic-info-tax')).toHaveText('310-456-789');
      await expect(page.getByTestId('clinic-info-cycle')).toHaveText('—');
      await expect(page.getByTestId('clinic-sign-in-address')).toHaveText('https://app.scs-heta.com');
      await expect(page.getByTestId('clinic-invitation-badge')).toHaveText(ar ? 'دعوة معلّقة' : 'Invitation pending');
      await expect(page.getByTestId('clinic-invitation-line')).toContainText(ar ? 'تنتهي صلاحية الدعوة في' : 'Invitation expires');
      await expect(page.getByTestId('clinic-manager-name')).toHaveText('أحمد سامي');
      await expect(page.getByTestId('clinic-info-team')).toHaveText(ar ? '0 أطباء · فرع واحد' : '0 doctors · 1 branch');
      await expect(page.getByTestId('clinic-info-last-sign-in')).toHaveText(ar ? 'لم يسجّل الدخول بعد' : 'Has not signed in yet');
      await look(page, language, 'clinic-detail');
    });

    test('asks for the clinic only when it opens, however long the page stays open', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      const asked: string[] = [];
      page.on('request', (request) => {
        const path = new URL(request.url()).pathname;
        if (request.method() === 'GET' && path.startsWith(`/v1/ops/clinics/${clinic.id}`)) {
          asked.push(path);
        }
      });
      await open(page, language, `/clinics/${clinic.id}`, SUCCESS, { clinics: [clinic] });
      await expect(page.getByTestId('clinic-detail-name')).toBeVisible();
      await page.getByRole('tab').nth(1).click();
      await expect(page.getByTestId('settings-general')).toBeVisible();
      // Each tab is a page of its own and reads the clinic as it opens; nothing asks again after.
      const opened = [...asked];
      expect(opened).toEqual([`/v1/ops/clinics/${clinic.id}`, `/v1/ops/clinics/${clinic.id}`, `/v1/ops/clinics/${clinic.id}/settings`]);
      await page.waitForTimeout(1500);
      expect(asked).toEqual(opened);
    });

    test('shows the three invitation lines, and nothing to resend once accepted', async ({ page }) => {
      const lapsed = fakeClinic({ nameEn: 'Lapsed' });
      // 08:40 UTC is 11:40 in Cairo, on summer time in September.
      lapsed.owner!.invitation = { state: 'lapsed', at: '2026-09-20T08:40:00Z' };
      const none = fakeClinic({ nameEn: 'None' });
      none.owner!.invitation = { state: 'none', at: null };
      const accepted = fakeClinic({ nameEn: 'Accepted' });
      accepted.owner = { ...accepted.owner!, state: 'active', invitation: null, lastSignInAt: new Date().toISOString() };
      await open(page, language, `/clinics/${lapsed.id}`, SUCCESS, { clinics: [lapsed, none, accepted] });

      await expect(page.getByTestId('clinic-invitation-badge')).toHaveText(ar ? 'انتهت صلاحية الدعوة' : 'Invitation expired');
      await expect(page.getByTestId('clinic-invitation-line')).toContainText(ar ? 'انتهت صلاحيتها في 20 سبتمبر 2026 · 11:40 ص' : 'Expired September 20, 2026 · 11:40 AM');
      await look(page, language, 'clinic-invitation-lapsed');

      await page.goto(`${OPS}/clinics/${none.id}`);
      await expect(page.getByTestId('clinic-invitation-badge')).toHaveText(ar ? 'لا توجد دعوة سارية' : 'No live invitation');
      await expect(page.getByTestId('clinic-invitation-line')).toHaveText(ar ? 'لا توجد دعوة سارية. أرسل واحدة.' : 'There is no live invitation. Send one.');
      await look(page, language, 'clinic-invitation-none');

      await page.goto(`${OPS}/clinics/${accepted.id}`);
      await expect(page.getByTestId('clinic-invitation')).toHaveCount(0);
      await expect(page.getByTestId('clinic-copy')).toBeVisible();
      // A Cairo date and time, not a relative one (contracts/clinics-screens.md; T082).
      await expect(page.getByTestId('clinic-info-last-sign-in')).toContainText('2026');
      await expect(page.getByTestId('clinic-info-last-sign-in')).not.toContainText(ar ? 'منذ' : 'ago');
    });

    test('resends the invitation, and says each refusal', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      const world = await open(page, language, `/clinics/${clinic.id}`, SUCCESS, { clinics: [clinic] });
      await page.getByTestId('clinic-invite-resend').click();
      await expect(page.getByText(ar ? 'تم إرسال الدعوة مرة أخرى — ينتهي الرابط الجديد في' : 'The invitation was sent again — the new link expires')).toBeVisible();

      for (const [code, words] of [
        ['RESET_EMAIL_COOLDOWN', ar ? 'أُرسلت دعوة لهذا البريد قبل أقل من دقيقة.' : 'An invitation was sent to this email less than a minute ago.'],
        ['ACCOUNT_LINK_NOT_SENT', ar ? 'تعذر إرسال البريد، فلم يصل شيء إلى المالك.' : 'The email could not be sent, so nothing reached the owner.'],
        ['INVITATION_ALREADY_ACCEPTED', ar ? 'مالك هذه العيادة قَبِل دعوته بالفعل.' : "This clinic's owner has already accepted the invitation."],
      ] as const) {
        world.book.resendRefusal = code;
        await page.getByTestId('clinic-invite-resend').click();
        await expect(page.getByText(words)).toBeVisible();
      }
    });

    test('copies the sign-in address, and says when the browser refuses', async ({ page, context }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: OPS });
      await open(page, language, `/clinics/${clinic.id}`, SUCCESS, { clinics: [clinic] });
      await page.getByTestId('clinic-copy').click();
      await expect(page.getByText(ar ? 'تم نسخ عنوان دخول العيادة.' : 'The clinic sign-in address was copied.')).toBeVisible();
      expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('https://app.scs-heta.com');

      await page.evaluate(() => Object.defineProperty(navigator, 'clipboard', { value: { writeText: () => Promise.reject(new Error('no')) } }));
      await page.getByTestId('clinic-copy').click();
      await expect(page.getByText(ar ? 'تعذر النسخ. حدّد الرابط وانسخه يدويًا.' : 'Copying failed. Select the address and copy it by hand.')).toBeVisible();
    });

    test('assigns the account manager, clears it, and keeps it when refused', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic', accountManager: null });
      const world = await open(page, language, `/clinics/${clinic.id}`, SUCCESS, { clinics: [clinic] });
      await expect(page.getByTestId('clinic-manager-name')).toHaveText(ar ? 'غير معيّن' : 'Unassigned');
      await page.getByTestId('clinic-manager-assign').click();
      await choose(page, 'clinic-manager-select', '0199c0de-0000-7000-8000-0000000000a1');
      await look(page, language, 'clinic-manager-assign');
      await page.getByTestId('clinic-manager-save').click();
      await expect(page.getByText(ar ? 'تم تحديث مسؤول الحساب.' : 'The account manager was updated.')).toBeVisible();
      await expect(page.getByTestId('clinic-manager-name')).toHaveText('منى يوسف');

      // Refused when the operator stopped being active meanwhile: the row keeps its value.
      await page.getByTestId('clinic-manager-assign').click();
      world.book.managers = [];
      await page.getByTestId('clinic-manager-save').click();
      await page.getByTestId('clinic-manager-cancel').click();
      await expect(page.getByTestId('clinic-manager-name')).toHaveText('منى يوسف');

      // A manager no longer offered is not preselected; the row names the choice and the select's
      // own label is read but not drawn (T082).
      await page.reload();
      await page.getByTestId('clinic-manager-assign').click();
      await expect(page.getByTestId('clinic-info-manager').locator('dt')).toBeVisible();
      await expect(page.getByTestId('clinic-info-manager').locator('scs-select .name')).toHaveCSS('position', 'absolute');
      await expect(page.getByTestId('clinic-manager-select')).toContainText(ar ? 'غير معيّن' : 'Unassigned');
      await page.getByTestId('clinic-manager-cancel').click();
      await expect(page.getByTestId('clinic-manager-name')).toHaveText('منى يوسف');
    });

    test('reads the same without edit rights, with nothing to change', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      await open(page, language, `/clinics/${clinic.id}`, VIEWER, { clinics: [clinic] });
      await expect(page.getByTestId('clinic-detail-name')).toBeVisible();
      for (const id of ['clinic-edit', 'clinic-manager-assign', 'clinic-invite-resend']) {
        await expect(page.getByTestId(id)).toHaveCount(0);
      }
      await expect(page.getByTestId('clinic-copy')).toBeVisible();
      await look(page, language, 'clinic-view-only');
    });

    test('says it could not load a clinic that is not there', async ({ page }) => {
      await open(page, language, '/clinics/0c11a000-0000-4000-8000-999999999999', SUCCESS);
      await expect(page.getByTestId('clinic-error')).toContainText(ar ? 'تعذر تحميل بيانات العيادة.' : "This clinic's details could not be loaded.");
      await look(page, language, 'clinic-error');
    });
  });

  test.describe(`editing a clinic — ${language}`, () => {
    test('opens filled, keeps the plan, saves the changes and the note', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      const world = await open(page, language, `/clinics/${clinic.id}/edit`, SUCCESS, { clinics: [clinic] });
      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'تعديل العيادة' : 'Edit Clinic');
      await expect(page.getByTestId('form-note')).toHaveText(ar ? 'تحديث بيانات عيادة النور.' : 'Update the details for Al Noor Clinic.');
      await expect(page.getByTestId('clinic-plan')).toBeDisabled();
      await expect(page.getByTestId('clinic-time-zone')).toContainText(ar ? 'القاهرة (GMT+2)' : 'Cairo (GMT+2)');
      await look(page, language, 'clinic-edit');

      await fill(page, 'clinic-address', '20 شارع النيل');
      await choose(page, 'clinic-language', 'en');
      await fill(page, 'clinic-note', 'بناءً على طلب العيادة');
      await page.getByTestId('clinic-save').click();
      await expect(page.getByText(ar ? 'تم تحديث بيانات العيادة.' : 'The clinic details were updated.')).toBeVisible();
      await expect(page).toHaveURL(new RegExp(`/clinics/${clinic.id}$`, 'u'));
      expect(world.platform.bodies.at(-1)).toMatchObject({ address: '20 شارع النيل', defaultLanguage: 'en', taxNumberSent: false });
      expect(world.platform.bodies.at(-1)).not.toHaveProperty('taxNumber');
    });

    test('removes the tax number when cleared, and says the clinic changed elsewhere', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      const world = await open(page, language, `/clinics/${clinic.id}/edit`, SUCCESS, { clinics: [clinic] });
      await fill(page, 'clinic-tax', '');
      await page.getByTestId('clinic-save').click();
      expect(world.platform.bodies.at(-1)).toMatchObject({ taxNumber: '', taxNumberSent: true });

      await page.goto(`${OPS}/clinics/${clinic.id}/edit`);
      await fill(page, 'clinic-address', 'عنوان آخر');
      // Someone else saves meanwhile: the form's version is now stale.
      clinic.version = 'v99';
      await page.getByTestId('clinic-save').click();
      await expect(page.getByTestId('form-stale')).toBeVisible();
      await expect(page.getByTestId('form-reload')).toBeVisible();
    });

    test('is refused to whoever may not edit clinics', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      await open(page, language, `/clinics/${clinic.id}/edit`, VIEWER, { clinics: [clinic] });
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
    });
  });

  test.describe(`the settings tab — ${language}`, () => {
    test('says it is read only, in the editor’s words or the viewer’s, with the general card', async ({ page }) => {
      const clinic: FakeClinic = fakeClinic({ nameEn: 'Al Noor Clinic', status: ACTIVE });
      await open(page, language, `/clinics/${clinic.id}/settings`, SUCCESS, { clinics: [clinic] });
      await expect(page.getByTestId('settings-strip')).toContainText(ar ? 'تغيير اللغة (من «تعديل العيادة»)' : 'can change the language (in “Edit Clinic”)');
      await expect(page.getByTestId('settings-strip')).not.toContainText(ar ? 'المنطقة الزمنية' : 'time zone');
      await expect(page.getByTestId('settings-time-zone')).toHaveText(ar ? 'القاهرة (GMT+2)' : 'Cairo (GMT+2)');
      await expect(page.getByTestId('settings-staff-address')).toHaveText('https://app.scs-heta.com');
      await expect(page.getByTestId('settings-edit')).toBeVisible();
      await look(page, language, 'clinic-settings');

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, `/clinics/${clinic.id}/settings`, VIEWER, { clinics: [clinic] });
      await expect(page.getByTestId('settings-strip')).toContainText(ar ? 'يحتاج إلى صلاحية «تعديل بيانات العيادات»' : 'needs the “Edit Clinic Data” permission');
      await expect(page.getByTestId('settings-edit')).toHaveCount(0);
      await look(page, language, 'clinic-settings-view-only');
    });

    test('says it could not load the settings', async ({ page }) => {
      const clinic = fakeClinic({ nameEn: 'Al Noor Clinic' });
      await open(page, language, `/clinics/${clinic.id}/settings`, SUCCESS, {
        clinics: [clinic],
        failing: { [`GET /v1/ops/clinics/${clinic.id}/settings`]: 500 },
      });
      await expect(page.getByTestId('settings-error')).toContainText(ar ? 'تعذر تحميل إعدادات العيادة.' : "The clinic's settings could not be loaded.");
    });
  });
}
