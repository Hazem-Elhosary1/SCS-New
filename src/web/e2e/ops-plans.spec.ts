import { Page, expect, test } from '@playwright/test';
import { OpsCatalogue, opsFeatures } from './ops-features-platform';
import { OpsPlans, designPlans, fakePlan, opsPlans } from './ops-plans-platform';
import {
  OPS,
  OpsPlatform,
  opsEnglish,
  opsLooksWrong,
  opsOperator,
  opsPicture,
  opsPlatform,
} from './ops-platform';
import { BILLING, OpsTeam, SUPPORT, VIEWER, choose, opsTeam } from './ops-team-platform';

test.use({ locale: 'ar-EG' });

/**
 * Spec 013, User Stories 1 to 6: the plans list, the form in two modes with its grid, the detail
 * and the disable dialog — every part and word of contracts/plans-screens.md, in Arabic and
 * English, at the design's size and at a phone's width. With `SCS_SHOTS` set each state is
 * pictured, to be laid beside its frame.
 */
const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };
const EASTERN = /[٠-٩۰-۹]/u;

type Language = 'ar' | 'en';

async function look(page: Page, language: Language, state: string): Promise<void> {
  // No Arabic-Indic digit on any screen of the operators console (FR-049 of spec 011).
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
  team: OpsTeam;
  catalogue: OpsCatalogue;
  book: OpsPlans;
}

async function open(
  page: Page,
  language: Language,
  path: string,
  role = BILLING,
  plans: Partial<OpsPlans> = {},
): Promise<World> {
  if (language === 'en') {
    await opsEnglish(page);
  }
  await page.setViewportSize(WIDE);
  const platform = await opsPlatform(page, {
    signedIn: true,
    operator:
      language === 'en'
        ? { ...opsOperator, fullName: 'Ahmed Sami', language: 'en', roleName: 'Billing Admin' }
        : opsOperator,
  });
  const team = await opsTeam(page, platform, { myRoleId: role });
  const catalogue = await opsFeatures(page, platform);
  const book = await opsPlans(page, platform, catalogue, plans);
  await page.goto(`${OPS}${path}`);
  return { platform, team, catalogue, book };
}

const row = (page: Page, id: string) => page.locator(`[data-testid="plan-row"][data-id="${id}"]`);

async function menuOf(page: Page, id: string): Promise<void> {
  await row(page, id).getByTestId('row-menu').click();
  await expect(page.getByTestId('row-menu-items')).toBeVisible();
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`the plans list — ${language}`, () => {
    test('opens from the side bar for whoever may view plans, and is refused to anyone else', async ({ page }) => {
      await open(page, language, '/plans', SUPPORT);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
      await expect(page.getByTestId('ops-nav-plans')).toHaveCount(0);

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/', VIEWER);
      await page.getByTestId('ops-nav-plans').click();
      await expect(page).toHaveURL(/\/plans$/u);
      await expect(page.getByRole('heading', { name: ar ? 'الباقات' : 'Plans', exact: true })).toBeVisible();

      // «كتالوج الميزات» opens the catalogue, and its breadcrumb comes back (User Story 6 scenario 4).
      await page.getByTestId('open-catalogue').click();
      await expect(page).toHaveURL(/\/plans\/features$/u);
    });

    test('says there are no plans yet, with nothing to count', async ({ page }) => {
      await open(page, language, '/plans');
      await expect(page.getByTestId('plans-empty')).toContainText(ar ? 'لا توجد باقات' : 'No plans yet');
      await expect(page.getByTestId('plans-empty')).toContainText(
        ar ? 'أضف أول باقة لتتمكن العيادات من الاشتراك فيها.' : 'Add the first plan so clinics can subscribe to it.',
      );
      for (const card of ['card-total', 'card-clinics', 'card-most', 'card-revenue']) {
        await expect(page.getByTestId(card)).toContainText(ar ? 'لا توجد بيانات' : 'No data');
      }
      await expect(page.getByTestId('plans-empty-add')).toBeVisible();
      await expect(page.getByRole('searchbox')).toHaveCount(0);
      await look(page, language, 'plans-empty');
    });

    test('lists the plans with the cards, every column’s words, and the order of the clarification', async ({ page }) => {
      const plans = designPlans();
      const [basic, pro, enterprise, legacy] = plans;
      await open(page, language, '/plans', BILLING, {
        plans,
        subscribers: {
          [basic!.id]: { clinics: 18, newThisMonth: 3, onTrial: 2, monthlyRevenue: 4482, list: [] },
          [pro!.id]: { clinics: 64, newThisMonth: 5, onTrial: 4, monthlyRevenue: 31936, list: [] },
          [enterprise!.id]: { clinics: 12, newThisMonth: 0, onTrial: 0, monthlyRevenue: 11988, list: [] },
        },
        revenue: { now: 46750, lastMonthEnd: 41741 },
      });

      await expect(page.getByTestId('card-total')).toContainText('4');
      await expect(page.getByTestId('card-total')).toContainText(ar ? 'باقة واحدة معطلة' : '1 plan disabled');
      await expect(page.getByTestId('card-clinics')).toContainText('94');
      await expect(page.getByTestId('card-clinics')).toContainText(ar ? '+8 هذا الشهر' : '+8 this month');
      await expect(page.getByTestId('card-most')).toContainText(ar ? 'الباقة الاحترافية' : 'Professional Plan');
      await expect(page.getByTestId('card-most')).toContainText(ar ? '64 عيادة' : '64 clinics');
      await expect(page.getByTestId('card-revenue')).toContainText(ar ? '46,750 ج.م' : 'EGP 46,750');
      await expect(page.getByTestId('card-revenue')).toContainText(ar ? '+12٪ هذا الشهر' : '+12% this month');

      // Active by monthly price, then the disabled one.
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
      const ids = await page.getByTestId('plan-row').evaluateAll((rows) => rows.map((r) => r.getAttribute('data-id')));
      expect(ids).toEqual([basic!.id, pro!.id, enterprise!.id, legacy!.id]);

      const first = row(page, basic!.id);
      await expect(first.getByTestId('plan-name')).toHaveText(ar ? 'الباقة الأساسية' : 'Basic Plan');
      await expect(first.getByTestId('plan-mark')).toHaveText(ar ? 'أس' : 'BP');
      await expect(first.getByTestId('plan-added')).toContainText(ar ? 'أضيفت' : 'Added');
      await expect(first.getByTestId('plan-monthly')).toHaveText(ar ? '249 ج.م / شهريًا' : 'EGP 249 / mo');
      await expect(first.getByTestId('plan-clinics')).toHaveText(ar ? '18 عيادة' : '18 clinics');
      await expect(first.getByTestId('plan-max')).toHaveText(ar ? 'حتى 3 مستخدم' : 'Up to 3 users');
      await expect(row(page, enterprise!.id).getByTestId('plan-max')).toHaveText(
        ar ? 'مستخدمون غير محدودين' : 'Unlimited users',
      );
      await expect(row(page, legacy!.id).getByTestId('plan-max')).toHaveText(ar ? 'مستخدم واحد' : '1 user');
      await expect(row(page, legacy!.id).getByTestId('plan-state')).toHaveText(ar ? 'معطلة' : 'Disabled');
      await expect(row(page, legacy!.id).getByTestId('plan-clinics')).toHaveText(ar ? '0 عيادة' : '0 clinics');
      await expect(page.getByText(ar ? 'عرض 1–4 من 4 باقات' : 'Showing 1–4 of 4 plans')).toBeVisible();
      await look(page, language, 'plans-list');
    });

    test('cuts a long most-wanted name with … and keeps it whole on hover', async ({ page }) => {
      const long = fakePlan({
        nameAr: 'باقة العيادات الكبيرة متعددة الفروع والتخصصات مع كل الميزات',
        nameEn: 'The large multi-branch multi-specialty clinics plan with everything',
      });
      await open(page, language, '/plans', BILLING, {
        plans: [long],
        subscribers: { [long.id]: { clinics: 3, newThisMonth: 0, onTrial: 0, monthlyRevenue: 0, list: [] } },
      });
      const card = page.getByTestId('card-most');
      await expect(card).toHaveAttribute('title', ar ? long.nameAr : long.nameEn);
      const cut = await card.getByTestId('kpi-value').evaluate((value) => value.scrollWidth > value.clientWidth);
      expect(cut).toBe(true);
    });

    test('finds a plan by part of either name, whatever the case', async ({ page }) => {
      await open(page, language, '/plans', BILLING, { plans: designPlans() });
      const search = page.getByRole('searchbox');
      await search.fill('الاحتراف');
      await expect(page.getByTestId('plan-row')).toHaveCount(1);
      await search.fill('ENTERPRISE');
      await expect(page.getByTestId('plan-row')).toHaveCount(1);
      await search.fill('nothing like it');
      await expect(page.getByTestId('plans-none')).toContainText(
        ar ? 'لا توجد باقات مطابقة لبحثك حتى الآن.' : 'No plans match your search yet.',
      );
      await look(page, language, 'plans-none');
      await page.getByTestId('plans-clear').click();
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
    });

    test('filters by state, with the count, a chip and «مسح الكل»', async ({ page }) => {
      await open(page, language, '/plans', BILLING, { plans: designPlans() });
      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await expect(page.getByTestId('filter-title')).toHaveText(ar ? 'تصفية الباقات' : 'Filter Plans');
      await expect(page.getByText(ar ? 'دورة الفوترة' : 'Billing Cycle')).toHaveCount(0);
      await page.getByTestId('filter-state').click();
      await page.getByRole('option', { name: ar ? 'معطلة' : 'Disabled' }).click();
      await page.getByRole('button', { name: ar ? 'تطبيق' : 'Apply' }).click();
      await expect(page.getByTestId('plan-row')).toHaveCount(1);
      await expect(page.getByText(ar ? 'الحالة: معطلة' : 'Status: Disabled')).toBeVisible();
      await look(page, language, 'plans-filtered');
      await page.getByRole('button', { name: ar ? 'مسح الكل' : 'Clear all' }).click();
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
    });

    test('pages by eight', async ({ page }) => {
      const plans = Array.from({ length: 10 }, (_, i) =>
        fakePlan({ nameAr: `باقة ${i + 1}`, nameEn: `Plan ${i + 1}`, monthlyEgp: 100 + i }),
      );
      await open(page, language, '/plans', BILLING, { plans });
      await expect(page.getByTestId('plan-row')).toHaveCount(8);
      await expect(page.getByText(ar ? 'عرض 1–8 من 10 باقات' : 'Showing 1–8 of 10 plans')).toBeVisible();
      await page.getByRole('button', { name: '2', exact: true }).click();
      await expect(page.getByTestId('plan-row')).toHaveCount(2);
    });

    test('gives a manager the row menu and a viewer none, and opens the detail from the row', async ({ page }) => {
      const plans = designPlans();
      await open(page, language, '/plans', BILLING, { plans });
      await menuOf(page, plans[0]!.id);
      await expect(page.getByTestId('row-view')).toBeVisible();
      await expect(page.getByTestId('row-edit')).toBeVisible();
      await expect(page.getByTestId('row-disable')).toBeVisible();
      await expect(page.getByTestId('row-enable')).toHaveCount(0);
      await look(page, language, 'plans-menu');
      await page.keyboard.press('Escape');
      await menuOf(page, plans[3]!.id);
      await expect(page.getByTestId('row-enable')).toBeVisible();
      await expect(page.getByTestId('row-disable')).toHaveCount(0);
      await page.keyboard.press('Escape');

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/plans', VIEWER, { plans: designPlans() });
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
      await expect(page.getByTestId('row-menu')).toHaveCount(0);
      await expect(page.getByTestId('add-plan')).toHaveCount(0);
      await page.getByTestId('plan-row').first().click();
      await expect(page).toHaveURL(/\/plans\/[0-9a-f-]{36}$/u);
    });

    test('shows it is loading, says when the list cannot be read, and tries again', async ({ page }) => {
      const { book } = await open(page, language, '/plans', BILLING, { delay: 1500 });
      await expect(page.getByTestId('plans-loading')).toBeVisible();
      await page.unrouteAll({ behavior: 'ignoreErrors' });
      const again = await open(page, language, '/plans', BILLING, {
        plans: designPlans(),
        failing: { 'GET /v1/ops/plans': 500 },
      });
      await expect(page.getByTestId('plans-failed')).toContainText(ar ? 'حدث خطأ ما' : 'Something went wrong');
      await expect(page.getByTestId('plans-retry')).toHaveText(ar ? 'حاول مرة أخرى' : 'Try again');
      await look(page, language, 'plans-failed');
      delete again.book.failing['GET /v1/ops/plans'];
      await page.getByTestId('plans-retry').click();
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
      expect(book).toBeTruthy();
    });

    test('wraps a long name and never cuts it', async ({ page }) => {
      const long = fakePlan({ nameAr: 'ب'.repeat(100), nameEn: 'B'.repeat(100) });
      await open(page, language, '/plans', BILLING, { plans: [long] });
      const name = row(page, long.id).getByTestId('plan-name');
      const cut = await name.evaluate((el) => el.scrollWidth > el.clientWidth + 1);
      expect(cut).toBe(false);
      await look(page, language, 'plans-long-name');
    });
  });
}

const gridRow = (page: Page, key: string) => page.locator(`[data-testid="grid-row"][data-key="${key}"]`);

async function fillPlan(page: Page, nameAr: string, nameEn: string): Promise<void> {
  await page.getByTestId('field-name-ar').fill(nameAr);
  await page.getByTestId('field-name-en').fill(nameEn);
  await page.getByTestId('field-monthly-egp').fill('249');
  await page.getByTestId('field-annual-egp').fill('0');
  await page.getByTestId('field-monthly-usd').fill('12');
  await page.getByTestId('field-annual-usd').fill('0');
  await page.getByTestId('field-description').fill('للعيادات الصغيرة.');
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`adding a plan — ${language}`, () => {
    test('draws the form and its grid, and adds a plan out of the catalogue', async ({ page }) => {
      const { book, platform } = await open(page, language, '/plans');
      await page.getByTestId('plans-empty-add').click();
      await expect(page).toHaveURL(/\/plans\/new$/u);
      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'إضافة باقة جديدة' : 'Add New Plan');
      await expect(page.getByTestId('tax-line')).toContainText(ar ? 'الأسعار قبل الضريبة.' : 'Prices are before tax.');
      await expect(page.getByTestId('grid-group')).toHaveCount(7);
      await expect(gridRow(page, 'allow_custom_branding').getByTestId('grid-upcoming')).toBeVisible();
      await expect(gridRow(page, 'max_users').getByTestId('grid-limit')).toBeDisabled();
      await look(page, language, 'plan-add');

      await fillPlan(page, 'الباقة الأساسية', 'Basic Plan');
      await gridRow(page, 'max_users').getByTestId('grid-switch').click();
      await gridRow(page, 'max_users').getByTestId('grid-limit').fill('3');
      await gridRow(page, 'allow_whatsapp').getByTestId('grid-switch').click();
      await gridRow(page, 'message_credits').getByTestId('grid-switch').click();
      await gridRow(page, 'allow_custom_branding').getByTestId('grid-switch').click();
      await page.getByTestId('plan-save').click();

      await expect(page).toHaveURL(/\/plans$/u);
      await expect(page.getByTestId('toast')).toContainText(ar ? 'تمت إضافة الباقة.' : 'Plan added.');
      const made = book.plans[0]!;
      expect(made.code).toBe('basic-plan');
      expect(made.features).toEqual([
        { key: 'max_users', limit: 3 },
        { key: 'allow_whatsapp', limit: null },
        { key: 'message_credits', limit: null },
        { key: 'allow_custom_branding', limit: null },
      ]);
      expect(platform.bodies.at(-1)).not.toHaveProperty('code');
      await expect(row(page, made.id).getByTestId('plan-max')).toHaveText(ar ? 'حتى 3 مستخدم' : 'Up to 3 users');
    });

    test('says what is wrong at each field, and a server refusal at the top', async ({ page }) => {
      const { book } = await open(page, language, '/plans/new');
      await page.getByTestId('field-monthly-egp').fill('499.999');
      await gridRow(page, 'max_users').getByTestId('grid-switch').click();
      await gridRow(page, 'max_users').getByTestId('grid-limit').fill('0');
      await page.getByTestId('plan-save').click();
      await expect(page.getByText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.').first()).toBeVisible();
      await expect(
        page.getByText(ar ? 'اكتب سعرًا صحيحًا مثل 499 أو 499.99.' : 'Enter a valid price like 499 or 499.99.'),
      ).toBeVisible();
      await expect(page.getByTestId('grid-limit-error')).toBeVisible();
      await look(page, language, 'plan-add-errors');
      expect(book.plans).toHaveLength(0);

      await fillPlan(page, 'باقة', 'Plan');
      await gridRow(page, 'max_users').getByTestId('grid-limit').fill('');
      book.failing['POST /v1/ops/plans'] = 500;
      await page.getByTestId('plan-save').click();
      await expect(page.getByTestId('form-problem')).toContainText(ar ? 'تعذر إضافة الباقة.' : "Couldn't add the plan.");
      await expect(page.getByTestId('field-name-en')).toHaveValue('Plan');
    });

    test('names the badge holder and moves the badge to the new plan', async ({ page }) => {
      const plans = designPlans();
      const { book } = await open(page, language, '/plans/new', BILLING, { plans });
      await fillPlan(page, 'الباقة الجديدة', 'New Plan');
      await choose(page, 'field-badge', 'best_selling');
      await expect(page.getByTestId('badge-holder')).toContainText(
        ar ? 'تحملها الآن «الباقة الاحترافية»' : '“Professional Plan” holds it now',
      );
      await look(page, language, 'plan-add-badge');
      await page.getByTestId('plan-save').click();
      await expect(page).toHaveURL(/\/plans$/u);
      expect(book.plans.filter((p) => p.badge !== null).map((p) => p.nameEn)).toEqual(['New Plan']);
    });

    test('is refused to whoever may only view plans', async ({ page }) => {
      await open(page, language, '/plans/new', VIEWER);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
    });
  });

  test.describe(`editing a plan — ${language}`, () => {
    test('opens filled, keeps the code, replaces the grid, and returns to the detail', async ({ page }) => {
      const plans = designPlans();
      const pro = plans[1]!;
      const { book } = await open(page, language, `/plans/${pro.id}/edit`, BILLING, { plans });
      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'تعديل الباقة الاحترافية' : 'Edit Professional Plan');
      await expect(page.getByTestId('field-name-en')).toHaveValue('Professional Plan');
      await expect(gridRow(page, 'max_users').getByTestId('grid-limit')).toHaveValue('10');
      await look(page, language, 'plan-edit');

      await page.getByTestId('field-name-en').fill('Professional Plus');
      await gridRow(page, 'allow_telegram').getByTestId('grid-switch').click();
      await page.getByTestId('plan-save').click();
      await expect(page).toHaveURL(new RegExp(`/plans/${pro.id}$`, 'u'));
      await expect(page.getByTestId('toast')).toContainText(ar ? 'تم تحديث الباقة.' : 'Plan updated.');
      const saved = book.plans[1]!;
      expect(saved.code).toBe('professional-plan');
      expect(saved.features.some((f) => f.key === 'allow_telegram')).toBe(false);
    });

    test('marks a kept retired feature, and says when the plan changed since it was opened', async ({ page }) => {
      const plans = designPlans();
      const basic = plans[0]!;
      const { book, catalogue } = await open(page, language, '/plans', BILLING, { plans });
      catalogue.features.find((f) => f.key === 'allow_whatsapp')!.isRetired = true;
      await page.goto(`${OPS}/plans/${basic.id}/edit`);
      await expect(gridRow(page, 'allow_whatsapp').getByTestId('grid-retired')).toHaveText(ar ? 'موقوفة' : 'Retired');

      book.plans[0]!.version++;
      await page.getByTestId('plan-save').click();
      await expect(page.getByTestId('form-problem')).toContainText(ar ? 'تعذر حفظ الباقة.' : "Couldn't save the plan.");
      await expect(page.getByTestId('form-stale')).toBeVisible();
      await look(page, language, 'plan-edit-stale');
      await page.getByTestId('form-reload').click();
      await expect(page.getByTestId('form-problem')).toHaveCount(0);
    });

    test('removes the badge field of a disabled plan and says why', async ({ page }) => {
      const plans = designPlans();
      await open(page, language, `/plans/${plans[3]!.id}/edit`, BILLING, { plans });
      await expect(page.getByTestId('field-badge')).toHaveCount(0);
      await expect(page.getByTestId('badge-disabled')).toContainText(
        ar ? 'الباقة المعطلة لا تحمل شارة.' : 'A disabled plan holds no badge.',
      );
    });

    test('says a plan that cannot be read could not be loaded', async ({ page }) => {
      await open(page, language, '/plans/00000000-0000-4000-8000-999999999999/edit');
      await expect(page.getByTestId('form-failed')).toContainText(ar ? 'تعذر تحميل هذه الباقة' : "Couldn't load this plan");
    });
  });

  test.describe(`a plan detail — ${language}`, () => {
    test('reads its information, what it includes by group, its subscriptions and its clinics', async ({ page }) => {
      const plans = designPlans();
      const pro = plans[1]!;
      await open(page, language, `/plans/${pro.id}`, BILLING, {
        plans,
        subscribers: {
          [pro.id]: {
            clinics: 64,
            newThisMonth: 8,
            onTrial: 5,
            monthlyRevenue: 31936,
            list: [
              {
                id: 'c1',
                nameAr: 'عيادة النور',
                nameEn: 'Al Noor Clinic',
                stateAr: 'نشطة · تتجدد 25 أكتوبر 2026',
                stateEn: 'Active · Renews Oct 25, 2026',
              },
              {
                id: 'c2',
                nameAr: 'مركز الشفاء',
                nameEn: 'Al Shifa Center',
                stateAr: 'تجربة',
                stateEn: 'Trial',
                // Spec 015: a clinic in trial says when it ends, as the frame draws it.
                trialEndsAt: new Date(Date.now() + 4 * 86_400_000 - 60_000).toISOString(),
              },
            ],
          },
        },
      });
      await expect(page.getByTestId('detail-title')).toHaveText(ar ? 'الباقة الاحترافية' : 'Professional Plan');
      await expect(page.getByTestId('detail-under')).toContainText('professional-plan');
      await expect(page.getByTestId('info-monthly')).toHaveText(ar ? '499 ج.م · 25 دولار' : 'EGP 499 · USD 25');
      await expect(page.getByTestId('info-users')).toHaveText(ar ? 'حتى 10 مستخدم' : 'Up to 10 users');
      const value = (key: string) =>
        page.locator(`[data-testid="included-row"][data-key="${key}"] [data-testid="included-value"]`);
      await expect(value('allow_whatsapp')).toHaveText('✓');
      await expect(value('message_credits')).toHaveText(ar ? '1,000 رسالة/شهر' : '1,000 msgs / mo');
      await expect(page.getByTestId('features-closing')).toContainText(
        ar ? 'تُعدَّل الميزات وحدودها من «تعديل».' : 'Edit features and limits from “Edit”.',
      );
      await expect(page.getByTestId('sub-clinics')).toHaveText('64');
      await expect(page.getByTestId('clinic-row')).toHaveCount(2);
      await expect(page.getByTestId('clinic-row').nth(1)).toContainText(ar ? 'تجربة · تنتهي خلال 4 أيام' : 'Trial · ends in 4 days');
      await look(page, language, 'plan-detail');
    });

    test('says there is no annual price, the reason of a disabled plan, and no clinics yet', async ({ page }) => {
      const plans = designPlans();
      await open(page, language, `/plans/${plans[3]!.id}`, BILLING, { plans });
      await expect(page.getByTestId('info-annual')).toContainText(ar ? 'بلا سعر سنوي' : 'No annual price');
      await expect(page.getByTestId('info-reason')).toContainText('استُبدلت');
      await expect(page.getByTestId('clinics-none')).toContainText(
        ar ? 'لا توجد عيادات مشتركة في هذه الباقة حتى الآن.' : 'No clinics are on this plan yet.',
      );
      await expect(page.getByTestId('detail-enable')).toBeVisible();
      await look(page, language, 'plan-detail-disabled');
    });

    test('shows a viewer no «تعديل» and the closing line without it', async ({ page }) => {
      const plans = designPlans();
      await open(page, language, `/plans/${plans[1]!.id}`, VIEWER, { plans });
      await expect(page.getByTestId('detail-title')).toBeVisible();
      await expect(page.getByTestId('detail-actions')).toHaveCount(0);
      await expect(page.getByTestId('features-closing')).not.toContainText(ar ? '«تعديل»' : '“Edit”');
    });
  });

  test.describe(`disabling and enabling a plan — ${language}`, () => {
    test('disables a plan with its clinics warned of, a reason, and the badge gone', async ({ page }) => {
      const plans = designPlans();
      const pro = plans[1]!;
      const { book } = await open(page, language, '/plans', BILLING, {
        plans,
        subscribers: { [pro.id]: { clinics: 64, newThisMonth: 0, onTrial: 0, monthlyRevenue: 0, list: [] } },
      });
      await menuOf(page, pro.id);
      await page.getByTestId('row-disable').click();
      await expect(page.getByTestId('disable-dialog')).toBeVisible();
      await expect(page.getByTestId('disable-warning')).toContainText(
        ar ? 'باقة «الباقة الاحترافية» عليها 64 عيادة نشطة حاليًا' : '“Professional Plan” currently has 64 active clinics subscribed',
      );
      await look(page, language, 'plan-disable');
      await page.getByTestId('disable-reason').fill('استُبدلت');
      await page.getByTestId('disable-confirm').click();
      await expect(page.getByTestId('toast')).toContainText(ar ? 'تم تعطيل الباقة.' : 'Plan disabled.');
      await expect(row(page, pro.id).getByTestId('plan-state')).toHaveText(ar ? 'معطلة' : 'Disabled');
      await expect(page.getByTestId('card-total')).toContainText(ar ? '2 باقة معطلة' : '2 plans disabled');
      expect(book.plans[1]!.badge).toBeNull();
      await expect(row(page, pro.id).getByTestId('row-menu')).toBeFocused();
    });

    test('shows no warning for a plan without clinics, says a failure, and enables from the detail', async ({ page }) => {
      const plans = designPlans();
      const basic = plans[0]!;
      const { book } = await open(page, language, '/plans', BILLING, { plans });
      await menuOf(page, basic.id);
      await page.getByTestId('row-disable').click();
      await expect(page.getByTestId('disable-warning')).toHaveCount(0);
      book.failing[`POST /v1/ops/plans/${basic.id}/disable`] = 500;
      await page.getByTestId('disable-confirm').click();
      await expect(page.getByTestId('disable-failed')).toContainText(ar ? 'تعذر تعطيل الباقة.' : "Couldn't disable the plan.");
      await page.getByTestId('disable-cancel').click();

      await page.goto(`${OPS}/plans/${plans[3]!.id}`);
      await page.getByTestId('detail-enable').click();
      await expect(page.getByTestId('toast')).toContainText(ar ? 'تم تفعيل الباقة.' : 'Plan enabled.');
      await expect(page.getByTestId('info-state')).toHaveText(ar ? 'نشطة' : 'Active');
      expect(book.plans[3]!.badge).toBeNull();
    });
  });

  test.describe(`the catalogue counts its plans — ${language}`, () => {
    test('counts the plans in each row, lists them where used, and comes back to the plans', async ({ page }) => {
      await open(page, language, '/plans/features', BILLING, { plans: designPlans() });
      const whatsapp = page.locator('[data-testid="feature-row"][data-key="allow_whatsapp"]');
      await expect(whatsapp.getByTestId('feature-plans')).toHaveText(ar ? 'باقتان' : '2 plans');
      await whatsapp.getByTestId('row-menu').click();
      await page.getByTestId('row-where').click();
      await expect(page.getByRole('dialog')).toContainText(ar ? 'الباقة الأساسية' : 'Basic Plan');
      await expect(page.getByRole('dialog')).toContainText(ar ? 'الباقة الاحترافية' : 'Professional Plan');
      await page.keyboard.press('Escape');

      await page.getByTestId('add-feature').click();
      await page.getByRole('link', { name: ar ? 'الباقات' : 'Plans' }).first().click();
      await expect(page).toHaveURL(/\/plans$/u);
    });
  });
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`plans offline, and by keyboard — ${language}`, () => {
    test('without a connection keeps what was read, and changes nothing it cannot send (FR-009a)', async ({
      page,
      context,
    }) => {
      const plans = designPlans();
      const [basic, , , legacy] = plans;
      const { book } = await open(page, language, '/plans', BILLING, { plans });
      await expect(page.getByTestId('plan-row')).toHaveCount(4);

      // Offline, every plans address fails as a lost connection does; online, the stand-in answers again.
      let cut = true;
      await page.route('**/v1/ops/plans**', (route) => (cut ? route.abort('internetdisconnected') : route.fallback()));
      await context.setOffline(true);
      await expect(page.getByTestId('ops-offline')).toBeVisible();
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
      await look(page, language, 'plans-offline');

      await menuOf(page, basic!.id);
      await page.getByTestId('row-disable').click();
      await page.getByTestId('disable-confirm').click();
      await expect(page.getByTestId('disable-failed')).toHaveText(ar ? 'تعذر تعطيل الباقة.' : "Couldn't disable the plan.");
      await page.keyboard.press('Escape');
      expect(book.plans[0]!.state).toBe('active');

      await menuOf(page, legacy!.id);
      await page.getByTestId('row-enable').click();
      await expect(page.getByTestId('toast')).toContainText(ar ? 'تعذر تفعيل الباقة.' : "Couldn't enable the plan.");
      expect(book.plans[3]!.state).toBe('disabled');

      cut = false;
      await context.setOffline(false);
      await expect(page.getByTestId('ops-offline')).toHaveCount(0);

      // A save sent offline ends in its message, and the form keeps what was typed.
      await page.goto(`${OPS}/plans/${basic!.id}/edit`);
      await expect(page.getByTestId('field-name-en')).toHaveValue('Basic Plan');
      await page.getByTestId('field-name-en').fill('Basic Offline');
      await context.setOffline(true);
      cut = true;
      await page.getByTestId('plan-save').click();
      await expect(page.getByTestId('form-problem')).toContainText(ar ? 'تعذر حفظ الباقة.' : "Couldn't save the plan.");
      await expect(page.getByTestId('field-name-en')).toHaveValue('Basic Offline');
      await context.setOffline(false);
      expect(book.plans[0]!.nameEn).toBe('Basic Plan');
    });

    test('opens and closes a row menu and the dialog by keyboard, and names every control', async ({ page }) => {
      const plans = designPlans();
      const basic = plans[0]!;
      await open(page, language, '/plans', BILLING, { plans });
      await expect(page.getByTestId('plans-table').locator('caption')).toHaveText(ar ? 'الباقات' : 'Plans');

      const trigger = row(page, basic.id).getByTestId('row-menu');
      await expect(trigger).toHaveAccessibleName(ar ? 'إجراءات الباقة الأساسية' : 'Actions for Basic Plan');
      await trigger.focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('row-menu-items')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('row-menu-items')).toHaveCount(0);
      await expect(trigger).toBeFocused();

      await page.keyboard.press('Enter');
      await page.getByTestId('row-disable').click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toHaveAccessibleName(ar ? 'تعطيل الباقة' : 'Disable Plan');
      await page.keyboard.press('Escape');
      await expect(dialog).toHaveCount(0);
      await expect(trigger).toBeFocused();

      // Each grid switch is named by its feature, and its limit field too.
      await page.goto(`${OPS}/plans/new`);
      const users = page.locator('[data-testid="grid-row"][data-key="max_users"]');
      await expect(users.getByRole('switch')).toHaveAccessibleName(ar ? 'تشمل المستخدمون' : 'Include Users');
      await expect(users.getByTestId('grid-limit')).toHaveAccessibleName(ar ? 'حدّ المستخدمون' : 'Users limit');

      // The first field in error takes the focus.
      await page.getByTestId('plan-save').click();
      await expect(page.getByTestId('field-name-ar')).toBeFocused();

      // At a phone's width every control is at least 40 by 40.
      await page.goto(`${OPS}/plans`);
      await page.setViewportSize(NARROW);
      const box = await row(page, basic.id).getByTestId('row-menu').boundingBox();
      expect(box!.width).toBeGreaterThanOrEqual(40);
      expect(box!.height).toBeGreaterThanOrEqual(40);
      const toggle = await page.goto(`${OPS}/plans/new`).then(() =>
        page.locator('[data-testid="grid-row"][data-key="max_users"]').getByRole('switch').boundingBox(),
      );
      expect(toggle!.width).toBeGreaterThanOrEqual(40);
      expect(toggle!.height).toBeGreaterThanOrEqual(40);
    });
  });
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`the catalogue’s card and warning from real plans — ${language}`, () => {
    test('counts the features in a plan on its card, and warns of the plans before retiring (US6/AC1, AC3)', async ({ page }) => {
      await open(page, language, '/plans/features', BILLING, { plans: designPlans() });

      // The design's four plans hold fifteen features between them, every plan holding at least one.
      const sold = page.getByTestId('card-sold');
      await expect(sold.getByTestId('kpi-value')).toHaveText('15');
      await expect(sold.getByTestId('kpi-note')).toHaveText(ar ? 'عبر 4 باقات' : 'across 4 plans');

      const whatsapp = page.locator('[data-testid="feature-row"][data-key="allow_whatsapp"]');
      await whatsapp.getByTestId('row-menu').click();
      await page.getByTestId('row-retire').click();
      await expect(page.getByTestId('retire-warning')).toContainText(
        ar ? 'الباقة الأساسية والباقة الاحترافية' : 'Basic Plan and Professional Plan',
      );
      await page.getByTestId('retire-cancel').click();
    });
  });

  test.describe(`the detail’s head and its dialog — ${language}`, () => {
    test('wraps a long name on the head and never cuts it', async ({ page }) => {
      const long = fakePlan({ nameAr: 'ب'.repeat(100), nameEn: 'B'.repeat(100) });
      await open(page, language, `/plans/${long.id}`, BILLING, { plans: [long] });
      const title = page.getByTestId('detail-title');
      await expect(title).toBeVisible();
      for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: 900 });
        expect(await title.evaluate((el) => el.scrollWidth > el.clientWidth + 1), `cut at ${width}`).toBe(false);
        expect(await opsLooksWrong(page), `${language} long detail head at ${width}`).toEqual([]);
      }
    });

    test('gives the focus back to «تعطيل الباقة» however the dialog closes (§5)', async ({ page }) => {
      const plans = designPlans();
      await open(page, language, `/plans/${plans[0]!.id}`, BILLING, { plans });
      const trigger = page.getByTestId('detail-disable');
      const opened = async () => {
        await trigger.click();
        await expect(page.getByTestId('disable-dialog')).toBeVisible();
      };
      const back = async () => {
        await expect(page.getByRole('dialog')).toHaveCount(0);
        await expect(trigger).toBeFocused();
      };

      await opened();
      await page.keyboard.press('Escape');
      await back();

      await opened();
      await page.getByTestId('disable-cancel').click();
      await back();

      await opened();
      await page.getByRole('dialog').getByRole('button', { name: ar ? 'إغلاق' : 'Close' }).first().click();
      await back();
    });
  });
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`back from the catalogue, and the grid read aloud — ${language}`, () => {
    test('the catalogue leads back to the plans by its breadcrumb (owner, 17 Oct 2026)', async ({ page }) => {
      await open(page, language, '/plans', BILLING, { plans: designPlans() });
      await page.getByTestId('open-catalogue').click();
      await expect(page).toHaveURL(/\/plans\/features$/u);
      const crumbs = page.getByTestId('catalogue-crumbs');
      await expect(crumbs).toContainText(ar ? 'الباقات' : 'Plans');
      await expect(crumbs).toContainText(ar ? 'كتالوج الميزات' : 'Feature Catalogue');
      await look(page, language, 'catalogue-crumbs');
      await crumbs.getByRole('link', { name: ar ? 'الباقات' : 'Plans' }).click();
      await expect(page).toHaveURL(/\/plans$/u);
      await expect(page.getByTestId('plan-row')).toHaveCount(4);
    });

    test('says why a limit is wrong with its field (T051)', async ({ page }) => {
      await open(page, language, '/plans/new');
      const users = page.locator('[data-testid="grid-row"][data-key="max_users"]');
      await users.getByTestId('grid-switch').click();
      await users.getByTestId('grid-limit').fill('0');
      await page.getByTestId('plan-save').click();
      await expect(users.getByTestId('grid-limit')).toHaveAccessibleDescription(
        ar ? 'أدخل حدًّا من 1 إلى 1,000,000، أو اتركه فارغًا لبلا حدّ.' : 'Enter a limit from 1 to 1,000,000, or leave it empty for no limit.',
      );
    });
  });
}
