import { Page, expect, test } from '@playwright/test';
import { OpsCatalogue, opsFeatures, seededFeatures } from './ops-features-platform';
import {
  OPS,
  OpsPlatform,
  opsEnglish,
  opsLooksWrong,
  opsOperator,
  opsPicture,
  opsPlatform,
} from './ops-platform';
import {
  BILLING,
  OpsTeam,
  SUPER,
  SUPPORT,
  VIEWER,
  choose,
  opsTeam,
  rowsOf,
} from './ops-team-platform';

test.use({ locale: 'ar-EG' });

/**
 * Spec 012, User Stories 1 to 4: the feature catalogue, its form in two modes and its three
 * dialogs — every part and word of contracts/catalogue-screens.md, in Arabic and English, at the
 * design's size and at a phone's width. With `SCS_SHOTS` set each state is pictured, to be laid
 * beside its frame.
 */
const WIDE = { width: 1440, height: 900 };
const NARROW = { width: 390, height: 844 };
const EASTERN = /[٠-٩۰-۹]/u;

type Language = 'ar' | 'en';

async function look(page: Page, language: Language, state: string): Promise<void> {
  // No Arabic-Indic digit on any screen of the operators console (FR-049 of spec 011).
  expect(await page.locator('body').innerText(), `${language} ${state}: digits`).not.toMatch(
    EASTERN,
  );
  await page.setViewportSize(NARROW);
  expect(await opsLooksWrong(page), `${language} ${state}, narrow`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({
      path: `${process.env['SCS_SHOTS']}/narrow/${language}-${state}.png`,
      fullPage: true,
    });
  }
  await page.setViewportSize(WIDE);
  expect(await opsLooksWrong(page), `${language} ${state}, wide`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({
      path: `${process.env['SCS_SHOTS']}/ops-${language}-${state}.png`,
      fullPage: true,
    });
  } else {
    await opsPicture(page, language, state);
  }
}

/** A dialog is placed for the window it opened in: it is looked at there, wide, then narrow. */
async function lookOpen(
  page: Page,
  language: Language,
  state: string,
  open: () => Promise<void>,
): Promise<void> {
  expect(await page.locator('body').innerText(), `${language} ${state}: digits`).not.toMatch(
    EASTERN,
  );
  expect(await opsLooksWrong(page), `${language} ${state}, wide`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({
      path: `${process.env['SCS_SHOTS']}/ops-${language}-${state}.png`,
      fullPage: true,
    });
  }
  await page.keyboard.press('Escape');
  await page.setViewportSize(NARROW);
  await open();
  expect(await opsLooksWrong(page), `${language} ${state}, narrow`).toEqual([]);
  if (process.env['SCS_SHOTS']) {
    await page.screenshot({ path: `${process.env['SCS_SHOTS']}/narrow/${language}-${state}.png` });
  }
  await page.keyboard.press('Escape');
  await page.setViewportSize(WIDE);
}

async function open(
  page: Page,
  language: Language,
  path: string,
  role = BILLING,
  with_: Partial<OpsCatalogue> = {},
  team_: Partial<OpsTeam> = {},
): Promise<{ platform: OpsPlatform; team: OpsTeam; catalogue: OpsCatalogue }> {
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
  const team = await opsTeam(page, platform, { myRoleId: role, ...team_ });
  const catalogue = await opsFeatures(page, platform, with_);
  await page.goto(`${OPS}${path}`);
  return { platform, team, catalogue };
}

const row = (page: Page, key: string) =>
  page.locator(`[data-testid="feature-row"][data-key="${key}"]`);

async function menuOf(page: Page, key: string): Promise<void> {
  await row(page, key).getByTestId('row-menu').click();
  await expect(page.getByTestId('row-menu-items')).toBeVisible();
}

const KEEPERS = [
  { id: 'p-1', name: 'الباقة الاحترافية', state: 'active' as const, clinics: 64 },
  { id: 'p-2', name: 'الباقة المؤسسية', state: 'inactive' as const, clinics: 12 },
];

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`the feature catalogue — ${language}`, () => {
    test('shows the header, the four cards, the table by group and thirteen rows a page', async ({
      page,
    }) => {
      await open(page, language, '/plans/features');

      await expect(page.getByRole('heading', { level: 1 })).toHaveText(
        ar ? 'كتالوج الميزات' : 'Feature Catalogue',
      );
      await expect(page.getByTestId('add-feature')).toContainText(
        ar ? 'إضافة ميزة' : 'Add Feature',
      );

      await expect(page.getByTestId('card-total')).toContainText('35');
      await expect(page.getByTestId('card-total')).toContainText(ar ? '7 مجموعات' : '7 groups');
      await expect(page.getByTestId('card-built')).toContainText(
        ar ? 'تُباع اليوم' : 'sellable today',
      );
      await expect(page.getByTestId('card-upcoming')).toContainText('35');
      await expect(page.getByTestId('card-sold')).toContainText(
        ar ? 'عبر 0 باقات' : 'across 0 plans',
      );

      await expect(page.getByTestId('features-table').locator('th')).toHaveText(
        ar
          ? ['الميزة', 'المجموعة', 'النوع', 'الحالة', 'في الباقات', 'إجراءات']
          : ['Feature', 'Group', 'Type', 'Status', 'In Plans', 'Actions'],
      );
      await expect(page.getByTestId('feature-row')).toHaveCount(13);
      await expect(page.getByTestId('feature-key').first()).toHaveText('core_clinic');

      const doctors = row(page, 'max_doctors');
      await expect(doctors.getByTestId('feature-name')).toHaveText(ar ? 'الأطباء' : 'Doctors');
      await expect(doctors.getByTestId('feature-group')).toHaveText(ar ? 'الأساس' : 'Core');
      await expect(doctors.getByTestId('feature-type')).toHaveText(
        ar ? 'حدّ · طبيب' : 'Limit · doctors',
      );
      await expect(doctors.getByTestId('feature-status')).toHaveText(ar ? 'قادمة' : 'Planned');
      await expect(doctors.getByTestId('feature-plans')).toHaveText(
        ar ? 'لا تُباع بعد' : 'Not sold yet',
      );

      await expect(
        page.getByText(ar ? 'عرض 1–13 من 35 ميزة' : 'Showing 1–13 of 35 features'),
      ).toBeVisible();
      await look(page, language, 'features');

      await page.getByRole('button', { name: '2', exact: true }).click();
      await expect(row(page, 'specialty_exam_modules').getByTestId('feature-type')).toHaveText(
        ar ? 'يتبع التخصص' : 'Follows specialty',
      );
      await expect(row(page, 'dental_chart').getByTestId('feature-type')).toHaveText(
        ar ? 'مفتاح · للأسنان' : 'Switch · dental',
      );
      await expect(
        page.getByText(ar ? 'عرض 14–26 من 35 ميزة' : 'Showing 14–26 of 35 features'),
      ).toBeVisible();
      await page.getByRole('button', { name: '3', exact: true }).click();
      await expect(page.getByTestId('feature-row')).toHaveCount(9);
    });

    test('is in the side bar for whoever may view plans, and refused to anyone else', async ({
      page,
    }) => {
      await open(page, language, '/plans/features', SUPPORT);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
      await expect(page.getByTestId('ops-nav-plans')).toHaveCount(0);

      await page.unrouteAll({ behavior: 'ignoreErrors' });
      await open(page, language, '/', VIEWER);
      await page.getByTestId('ops-nav-plans').click();
      await expect(page).toHaveURL(/\/plans\/features$/u);
      await expect(page.getByTestId('ops-nav-plans')).toContainText(ar ? 'الباقات' : 'Plans');
    });

    test('finds a feature by part of either name or of its key, whatever the case', async ({
      page,
    }) => {
      await open(page, language, '/plans/features');
      const search = page.getByRole('searchbox');

      await search.fill('الأسنان');
      await expect(page.getByTestId('feature-key')).toHaveText(['dental_chart', 'dental_lab']);
      await expect(
        page.getByText(ar ? 'عرض 1–2 من 2 ميزة' : 'Showing 1–2 of 2 features'),
      ).toBeVisible();
      await search.fill('X-RAY');
      await expect(page.getByTestId('feature-key')).toHaveText(['ai_xray_analysis']);
      await search.fill('TELECONSULT_M');
      await expect(page.getByTestId('feature-key')).toHaveText(['teleconsult_minutes']);

      await search.fill('nothing like this');
      await expect(page.getByTestId('features-none')).toContainText(
        ar ? 'لا توجد ميزات تطابق البحث.' : 'No features match your search.',
      );
      await look(page, language, 'features-none');
      await page.getByTestId('features-clear').click();
      await expect(page.getByTestId('feature-row')).toHaveCount(13);
    });

    test('filters by group, type and status, offering only what exists, with chips and a count', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      catalogue.features.find((f) => f.key === 'allow_telegram')!.isRetired = true;
      await page.reload();

      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await expect(await rowsOf(page, 'filter-status')).toHaveText(
        ar ? ['الكل', 'قادمة', 'موقوفة'] : ['All', 'Planned', 'Retired'],
      );
      await page.keyboard.press('Escape');
      await choose(page, 'filter-group', 'clinical');
      await choose(page, 'filter-type', 'switch');
      await page.getByRole('button', { name: ar ? 'تطبيق الفلاتر' : 'Apply filters' }).click();

      await expect(page.getByTestId('feature-row')).toHaveCount(10);
      await expect(page.getByText(ar ? 'فلتران مطبّقان' : '2 filters applied')).toBeAttached();
      await expect(page.getByText(ar ? 'المجموعة: السريرية' : 'Group: Clinical')).toBeVisible();
      await look(page, language, 'features-filtered');

      await page.getByRole('button', { name: ar ? 'مسح الكل' : 'Clear all' }).click();
      await expect(page.getByTestId('feature-row')).toHaveCount(13);

      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await choose(page, 'filter-status', 'retired');
      await page.getByRole('button', { name: ar ? 'تطبيق الفلاتر' : 'Apply filters' }).click();
      await expect(page.getByTestId('feature-key')).toHaveText(['allow_telegram']);
      await expect(row(page, 'allow_telegram').getByTestId('feature-retired')).toHaveText(
        ar ? 'موقوفة' : 'Retired',
      );
    });

    test('gives a viewer no add button and a row menu of where it is used alone', async ({
      page,
    }) => {
      await open(page, language, '/plans/features', VIEWER);
      await expect(page.getByTestId('feature-row').first()).toBeVisible();
      await expect(page.getByTestId('add-feature')).toHaveCount(0);

      await menuOf(page, 'max_users');
      await expect(page.getByTestId('row-menu-items').getByRole('menuitem')).toHaveText([
        ar ? 'أين تُستعمل' : 'Where it is used',
      ]);
      await page.keyboard.press('Escape');

      await page.goto(`${OPS}/plans/features/new`);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
    });

    test('shows that it is loading, and says when it cannot load, with a way to try again', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features', BILLING, { delay: 1500 });
      await expect(page.getByTestId('features-loading')).toBeVisible();
      await look(page, language, 'features-loading');
      catalogue.delay = 0;

      catalogue.failing['GET /v1/ops/features'] = 500;
      await page.reload();
      await expect(page.getByTestId('features-failed')).toContainText(
        ar ? 'تعذّر تحميل الكتالوج.' : "Couldn't load the catalogue.",
      );
      await expect(page.getByTestId('features-retry')).toHaveText(
        ar ? 'حاول مرة أخرى' : 'Try again',
      );
      await look(page, language, 'features-failed');

      delete catalogue.failing['GET /v1/ops/features'];
      await page.getByTestId('features-retry').click();
      await expect(page.getByTestId('feature-row')).toHaveCount(13);
    });

    test('wraps a long name and never cuts it, and keeps the key on one line', async ({ page }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      const first = catalogue.features[0];
      first.nameAr = 'ميزة باسم طويل جدًا '.repeat(6).slice(0, 120);
      first.nameEn = 'A feature with a very long name '.repeat(4).slice(0, 120);
      await page.reload();

      const name = row(page, 'core_clinic').getByTestId('feature-name');
      await expect(name).toHaveText(ar ? first.nameAr.trim() : first.nameEn.trim());
      const box = await name.boundingBox();
      expect(box!.height).toBeGreaterThan(30);
      const key = await row(page, 'core_clinic').getByTestId('feature-key').boundingBox();
      expect(key!.height).toBeLessThan(24);
      await look(page, language, 'features-long-name');
    });
  });

  test.describe(`adding a feature — ${language}`, () => {
    test('shows the units and «بدون هذه القدرة» for a limit only, and adds a switch at the end of its group', async ({
      page,
    }) => {
      const { catalogue, platform } = await open(page, language, '/plans/features');
      await page.getByTestId('add-feature').click();
      await expect(page).toHaveURL(/\/plans\/features\/new$/u);

      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'إضافة ميزة' : 'Add Feature');
      await expect(page.getByTestId('form-note')).toContainText(
        ar ? 'تظهر في نموذج الباقة فورًا' : 'It appears on the plan form straight away',
      );
      await expect(page.getByTestId('field-unit-ar')).toHaveCount(0);
      await look(page, language, 'feature-add');

      await choose(page, 'field-kind', 'limit');
      await expect(page.getByTestId('field-unit-ar')).toBeVisible();
      await expect(page.getByTestId('field-unit-en')).toBeVisible();
      await expect(page.getByTestId('field-baseline')).toBeVisible();
      await look(page, language, 'feature-add-limit');
      await choose(page, 'field-kind', 'switch');
      await expect(page.getByTestId('field-baseline')).toHaveCount(0);

      await page.getByTestId('field-name-ar').fill('قناة إنستجرام');
      await page.getByTestId('field-name-en').fill('Instagram channel');
      await page.getByTestId('field-key').fill('allow_instagram');
      await choose(page, 'field-group', 'channels');
      await page.getByTestId('form-save').click();

      await expect(page).toHaveURL(/\/plans\/features$/u);
      await expect(page.getByTestId('toast')).toContainText(
        ar ? 'أُضيفت الميزة.' : 'Feature added.',
      );
      await expect(row(page, 'allow_instagram').getByTestId('feature-group')).toHaveText(
        ar ? 'القنوات' : 'Channels',
      );
      const added = catalogue.features.find((f) => f.key === 'allow_instagram')!;
      expect(added.sortOrder).toBe(50);
      expect(platform.bodies.at(-1)).toMatchObject({
        kind: 'switch',
        unitAr: null,
        unitEn: null,
        baseline: null,
        isSold: true,
        status: 'upcoming',
      });
    });

    test('says each refusal beside its field, in the page’s language, and saves nothing', async ({
      page,
    }) => {
      const { platform } = await open(page, language, '/plans/features/new');
      await choose(page, 'field-kind', 'limit');
      await page.getByTestId('field-key').fill('Bad-Key');
      await page.getByTestId('form-save').click();

      await expect(page.getByText(ar ? 'هذا الحقل مطلوب.' : 'This field is required.')).toHaveCount(
        2,
      );
      await expect(
        page.getByText(
          ar
            ? 'حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.'
            : 'Lower-case English letters, digits and underscores, starting with a letter.',
        ),
      ).toBeVisible();
      await expect(
        page.getByText(
          ar
            ? 'الحدّ يحتاج وحدة بالعربية والإنجليزية: ماذا يعدّ؟'
            : 'A limit needs its unit in Arabic and English: what does it count?',
        ),
      ).toHaveCount(2);
      await expect(page.getByTestId('field-name-ar')).toBeFocused();
      await look(page, language, 'feature-add-refused');
      expect(platform.bodies).toEqual([]);

      await choose(page, 'field-kind', 'switch');
      await page.getByTestId('field-name-ar').fill('المستخدمون');
      await page.getByTestId('field-name-en').fill('Users');
      await page.getByTestId('field-key').fill('max_users');
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('form-problem')).toHaveText(
        ar ? 'تعذّر حفظ الميزة.' : "Couldn't save the feature.",
      );
      await expect(
        page.getByText(ar ? 'هذا المفتاح لميزة أخرى.' : 'This key belongs to another feature.'),
      ).toBeVisible();
      await expect(page.getByTestId('field-key')).toHaveValue('max_users');
      await look(page, language, 'feature-add-key-taken');
    });
  });

  test.describe(`editing a feature — ${language}`, () => {
    test('opens with its values and the key fixed, saves, and moves to the end of a new group', async ({
      page,
    }) => {
      const { catalogue, platform } = await open(page, language, '/plans/features');
      await menuOf(page, 'allow_whatsapp');
      await page.getByTestId('row-edit').click();
      await expect(page).toHaveURL(/\/plans\/features\/allow_whatsapp\/edit$/u);

      await expect(page.getByTestId('form-title')).toHaveText(ar ? 'تعديل ميزة' : 'Edit Feature');
      await expect(page.getByTestId('field-key')).toHaveValue('allow_whatsapp');
      await expect(page.getByTestId('field-key')).toHaveAttribute('readonly', '');
      await expect(
        page.getByText(ar ? 'لا يتغيّر المفتاح بعد الإنشاء.' : 'The key never changes once made.'),
      ).toBeVisible();
      await look(page, language, 'feature-edit');

      await page.getByTestId('field-name-en').fill('WhatsApp');
      await choose(page, 'field-group', 'growth');
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('toast')).toContainText(
        ar ? 'حُفظت الميزة.' : 'Feature saved.',
      );

      await page.getByRole('searchbox').fill('allow_whatsapp');
      await expect(row(page, 'allow_whatsapp').getByTestId('feature-group')).toHaveText(
        ar ? 'النمو' : 'Growth',
      );
      await expect(row(page, 'allow_whatsapp').getByTestId('feature-name')).toHaveText(
        ar ? 'قناة واتساب' : 'WhatsApp',
      );
      const saved = catalogue.features.find((f) => f.key === 'allow_whatsapp')!;
      expect([saved.nameEn, saved.group, saved.sortOrder, saved.version]).toEqual([
        'WhatsApp',
        'growth',
        90,
        2,
      ]);
      expect(platform.bodies.at(-1)).toMatchObject({ version: 1 });
      expect(platform.bodies.at(-1)).not.toHaveProperty('key');
    });

    test('says when the feature of the address does not exist, and when it cannot be read', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features/no_such_feature/edit');
      await expect(page.getByTestId('form-missing')).toContainText(
        ar ? 'لم تعد هذه الميزة موجودة.' : 'This feature no longer exists.',
      );
      await look(page, language, 'feature-edit-missing');

      catalogue.failing['GET /v1/ops/features/max_users'] = 500;
      await page.goto(`${OPS}/plans/features/max_users/edit`);
      await expect(page.getByTestId('form-failed')).toContainText(
        ar ? 'تعذّر تحميل الميزة.' : "Couldn't load the feature.",
      );
      delete catalogue.failing['GET /v1/ops/features/max_users'];
      await page.getByTestId('form-retry').click();
      await expect(page.getByTestId('field-key')).toHaveValue('max_users');
    });

    test('keeps «تُباع في الباقات» on for a retired feature, and says why', async ({ page }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      catalogue.features.find((f) => f.key === 'allow_refunds')!.isRetired = true;
      await page.goto(`${OPS}/plans/features/allow_refunds/edit`);
      await expect(
        page.getByRole('switch', { name: ar ? 'تُباع في الباقات' : 'Sold in plans' }),
      ).toBeDisabled();
      await expect(
        page.getByText(ar ? 'الميزة الموقوفة تبقى مُباعة' : 'A retired feature stays sold', {
          exact: false,
        }),
      ).toBeAttached();
    });
  });

  test.describe(`where a feature is used, retiring and selling again — ${language}`, () => {
    test('says plainly that no plan includes it yet, and lists the plans when there are', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      await menuOf(page, 'max_users');
      await page.getByTestId('row-where').click();
      await expect(page.getByRole('dialog')).toContainText(
        ar ? 'أين تُستعمل ميزة المستخدمون' : 'Where Users is used',
      );
      await expect(page.getByTestId('where-used-none')).toHaveText(
        ar ? 'لا تشملها أي باقة بعد.' : 'No plan includes it yet.',
      );
      await lookOpen(page, language, 'where-used-none', async () => {
        await menuOf(page, 'max_users');
        await page.getByTestId('row-where').click();
      });

      catalogue.plans['ai_xray_analysis'] = KEEPERS;
      await page.reload();
      await page.getByRole('button', { name: '2', exact: true }).click();
      await menuOf(page, 'ai_xray_analysis');
      await page.getByTestId('row-where').click();
      await expect(page.getByTestId('where-used-row')).toHaveCount(2);
      await expect(page.getByTestId('where-used-row').nth(1)).toContainText(
        ar ? 'معطلة' : 'Inactive',
      );
      await expect(page.getByTestId('where-used-row').nth(1)).toContainText(
        ar ? '12 عيادة' : '12 clinics',
      );
      await expect(page.getByTestId('where-used-close')).toHaveText(ar ? 'إغلاق' : 'Close');
      await expect(page.getByTestId('where-used-total')).toContainText(
        ar ? '76 عيادة' : '76 clinics',
      );
      await expect(page.getByTestId('where-used-text')).toContainText(
        ar ? 'تشملها باقتان، وعليها 76 عيادة.' : 'Included in 2 plans, with 76 clinics on them.',
      );
      await lookOpen(page, language, 'where-used', async () => {
        await page.getByRole('button', { name: '2', exact: true }).click();
        await menuOf(page, 'ai_xray_analysis');
        await page.getByTestId('row-where').click();
      });
    });

    test('retires a sold feature after a confirmation, names the plans that keep it, then sells it again', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      catalogue.plans['max_users'] = KEEPERS;
      await page.reload();

      await menuOf(page, 'max_users');
      await expect(page.getByTestId('row-retire')).toHaveText(
        ar ? 'إيقاف الميزة…' : 'Retire feature...',
      );
      await page.getByTestId('row-retire').click();
      await expect(page.getByRole('dialog')).toContainText(
        ar ? 'إيقاف ميزة المستخدمون؟' : 'Retire Users?',
      );
      await expect(page.getByTestId('retire-warning')).toContainText(
        ar ? 'الباقة الاحترافية والباقة المؤسسية' : 'الباقة الاحترافية and الباقة المؤسسية',
      );
      await expect(page.getByTestId('retire-warning')).toContainText(
        ar ? '76 عيادة' : '76 clinics',
      );
      await lookOpen(page, language, 'retire', async () => {
        await menuOf(page, 'max_users');
        await page.getByTestId('row-retire').click();
      });

      await menuOf(page, 'max_users');
      await page.getByTestId('row-retire').click();
      await page.getByTestId('retire-confirm').click();
      await expect(page.getByTestId('toast')).toContainText(
        ar ? 'أُوقفت الميزة المستخدمون.' : 'Users was retired.',
      );
      await expect(row(page, 'max_users').getByTestId('feature-retired')).toBeVisible();
      expect(catalogue.features.find((f) => f.key === 'max_users')!.isRetired).toBe(true);

      await menuOf(page, 'max_users');
      await expect(page.getByTestId('row-retire')).toHaveCount(0);
      await page.getByTestId('row-resume').click();
      await expect(page.getByRole('dialog')).toContainText(
        ar ? 'إعادة بيع ميزة المستخدمون؟' : 'Sell Users again?',
      );
      await lookOpen(page, language, 'resume', async () => {
        await menuOf(page, 'max_users');
        await page.getByTestId('row-resume').click();
      });
      await menuOf(page, 'max_users');
      await page.getByTestId('row-resume').click();
      await page.getByTestId('resume-confirm').click();
      // The retire's message may still be showing: the new one is found by its words.
      await expect(
        page.getByTestId('toast').filter({
          hasText: ar ? 'أُعيدت الميزة المستخدمون إلى البيع.' : 'Users is on sale again.',
        }),
      ).toBeVisible();
      await expect(row(page, 'max_users').getByTestId('feature-retired')).toHaveCount(0);
    });

    test('offers neither to a feature that is not sold, and says when retiring fails', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      await menuOf(page, 'core_clinic');
      await expect(page.getByTestId('row-retire')).toHaveCount(0);
      await expect(page.getByTestId('row-resume')).toHaveCount(0);
      await page.keyboard.press('Escape');

      catalogue.failing['POST /v1/ops/features/max_users/retire'] = 500;
      await menuOf(page, 'max_users');
      await page.getByTestId('row-retire').click();
      await expect(page.getByTestId('retire-text')).toContainText(
        ar ? 'لن تستطيع الباقات الجديدة أن تشملها' : 'New plans can no longer include it',
      );
      await expect(page.getByTestId('retire-warning')).toHaveCount(0);
      await page.getByTestId('retire-confirm').click();
      await expect(page.getByTestId('retire-failed')).toHaveText(
        ar ? 'تعذّر إيقاف الميزة.' : "Couldn't retire the feature.",
      );
      expect(catalogue.features.find((f) => f.key === 'max_users')!.isRetired).toBe(false);
    });
  });
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`offline, and by keyboard — ${language}`, () => {
    test('without a connection keeps what was read, and changes nothing it cannot send (FR-008a)', async ({
      page,
      context,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      await expect(page.getByTestId('feature-row')).toHaveCount(13);
      catalogue.features.find((f) => f.key === 'allow_telegram')!.isRetired = true;
      await page.reload();
      await expect(row(page, 'allow_telegram').getByTestId('feature-retired')).toBeVisible();

      await context.setOffline(true);
      await page.route('**/v1/ops/features**', (route) => route.abort('internetdisconnected'));
      await expect(page.getByTestId('ops-offline')).toBeVisible();
      await expect(page.getByTestId('feature-row')).toHaveCount(13);
      await look(page, language, 'features-offline');

      await menuOf(page, 'max_users');
      await page.getByTestId('row-retire').click();
      await page.getByTestId('retire-confirm').click();
      await expect(page.getByTestId('retire-failed')).toHaveText(
        ar ? 'تعذّر إيقاف الميزة.' : "Couldn't retire the feature.",
      );
      await page.keyboard.press('Escape');
      expect(catalogue.features.find((f) => f.key === 'max_users')!.isRetired).toBe(false);

      await menuOf(page, 'allow_telegram');
      await page.getByTestId('row-resume').click();
      await page.getByTestId('resume-confirm').click();
      await expect(page.getByTestId('resume-failed')).toHaveText(
        ar ? 'تعذّر إعادة بيع الميزة.' : "Couldn't put the feature back on sale.",
      );
      await page.keyboard.press('Escape');
      expect(catalogue.features.find((f) => f.key === 'allow_telegram')!.isRetired).toBe(true);

      await context.setOffline(false);
      await expect(page.getByTestId('ops-offline')).toHaveCount(0);

      // A save sent offline ends in its message, and the form keeps what was typed.
      await page.goto(`${OPS}/plans/features/new`);
      await page.getByTestId('field-name-ar').fill('قناة');
      await page.getByTestId('field-name-en').fill('Channel');
      await page.getByTestId('field-key').fill('offline_key');
      await context.setOffline(true);
      await page.getByTestId('form-save').click();
      await expect(page.getByTestId('form-problem')).toHaveText(
        ar ? 'تعذّر حفظ الميزة.' : "Couldn't save the feature.",
      );
      await expect(page.getByTestId('field-key')).toHaveValue('offline_key');
      await context.setOffline(false);
      expect(catalogue.features.some((f) => f.key === 'offline_key')).toBe(false);
    });

    test('gives the focus back to the row’s menu however a dialog closes (§6)', async ({
      page,
    }) => {
      await open(page, language, '/plans/features');
      const trigger = row(page, 'max_users').getByTestId('row-menu');
      const opened = async (item: string) => {
        await trigger.click();
        await page.getByTestId(item).click();
        await expect(page.getByRole('dialog')).toBeVisible();
      };
      const back = async () => {
        await expect(page.getByRole('dialog')).toHaveCount(0);
        await expect(trigger).toBeFocused();
      };

      await opened('row-where');
      await page.getByTestId('where-used-close').click();
      await back();

      await opened('row-retire');
      await page.getByTestId('retire-cancel').click();
      await back();

      await opened('row-retire');
      await page
        .getByRole('dialog')
        .getByRole('button', { name: ar ? 'إغلاق' : 'Close' })
        .first()
        .click();
      await back();

      await opened('row-retire');
      await page.getByTestId('retire-confirm').click();
      await expect(row(page, 'max_users').getByTestId('feature-retired')).toBeVisible();
      await back();

      await opened('row-resume');
      await page.getByTestId('resume-confirm').click();
      await expect(row(page, 'max_users').getByTestId('feature-retired')).toHaveCount(0);
      await back();
    });

    test('opens and closes a row menu and a dialog by keyboard, and gives the focus back', async ({
      page,
    }) => {
      await open(page, language, '/plans/features');
      await expect(page.getByTestId('features-table').locator('caption')).toHaveText(
        ar ? 'كتالوج الميزات' : 'Feature catalogue',
      );

      const trigger = row(page, 'max_users').getByTestId('row-menu');
      await expect(trigger).toHaveAccessibleName(ar ? 'إجراءات المستخدمون' : 'Actions for Users');
      await trigger.focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('row-menu-items')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('row-menu-items')).toHaveCount(0);
      await expect(trigger).toBeFocused();

      await page.keyboard.press('Enter');
      await page.getByTestId('row-where').click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toHaveAccessibleName(
        ar ? 'أين تُستعمل ميزة المستخدمون' : 'Where Users is used',
      );
      await page.keyboard.press('Escape');
      await expect(dialog).toHaveCount(0);

      // At a phone's width every control is at least 40 by 40.
      await page.setViewportSize(NARROW);
      const box = await trigger.boundingBox();
      expect(box!.width).toBeGreaterThanOrEqual(40);
      expect(box!.height).toBeGreaterThanOrEqual(40);
    });
  });
}

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';
  const card = (page: Page, testId: string) => page.getByTestId(testId).getByTestId('kpi-value');

  test.describe(`what the catalogue holds — ${language}`, () => {
    test('says so when it holds no feature at all (FR-007)', async ({ page }) => {
      await open(page, language, '/plans/features', BILLING, { features: [] });
      await expect(page.getByTestId('features-empty')).toContainText(
        ar ? 'لا ميزات بعد' : 'No features yet',
      );
      await expect(page.getByTestId('features-empty')).toContainText(
        ar ? 'أضف أول قدرة يمكن أن تشملها باقة.' : 'Add the first capability a plan can include.',
      );
      await expect(page.getByRole('searchbox')).toHaveCount(0);
      await look(page, language, 'features-empty');
    });

    test('counts the four cards from what it holds, a built feature and the plans included (FR-003)', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      await expect(card(page, 'card-total')).toHaveText('35');
      await expect(card(page, 'card-built')).toHaveText('0');
      await expect(card(page, 'card-upcoming')).toHaveText('35');
      await expect(card(page, 'card-sold')).toHaveText('0');
      await expect(page.getByTestId('card-upcoming')).toContainText(
        ar ? 'لم تُبنَ بعد' : 'not built yet',
      );

      catalogue.features.find((f) => f.key === 'allow_whatsapp')!.status = 'built';
      catalogue.plans['max_users'] = KEEPERS;
      await page.reload();
      await expect(card(page, 'card-built')).toHaveText('1');
      await expect(card(page, 'card-upcoming')).toHaveText('34');
      await expect(card(page, 'card-sold')).toHaveText('1');
      await expect(page.getByTestId('card-sold')).toContainText(
        ar ? 'عبر 2 باقات' : 'across 2 plans',
      );
      await expect(row(page, 'allow_whatsapp').getByTestId('feature-status')).toHaveText(
        ar ? 'مبنية' : 'Built',
      );
      await expect(row(page, 'max_users').getByTestId('feature-plans')).toHaveText(
        ar ? 'باقتان' : '2 plans',
      );

      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await choose(page, 'filter-status', 'built');
      await page.getByRole('button', { name: ar ? 'تطبيق الفلاتر' : 'Apply filters' }).click();
      await expect(page.getByTestId('feature-key')).toHaveText(['allow_whatsapp']);
    });

    test('offers in the filter only the groups and the types that some feature has (FR-006)', async ({
      page,
    }) => {
      const { catalogue } = await open(page, language, '/plans/features');
      catalogue.features = catalogue.features.filter(
        (f) => f.kind === 'switch' && f.group !== 'reports',
      );
      await page.reload();
      await page.getByRole('button', { name: ar ? 'فلترة' : 'Filter' }).click();
      await expect(await rowsOf(page, 'filter-type')).toHaveText(
        ar ? ['الكل', 'مفتاح'] : ['All', 'Switch'],
      );
      await page.keyboard.press('Escape');
      const groups = await rowsOf(page, 'filter-group');
      await expect(groups).toHaveText(
        ar
          ? ['الكل', 'الأساس', 'القنوات', 'المال', 'السريرية', 'النمو']
          : ['All', 'Core', 'Channels', 'Money', 'Clinical', 'Growth'],
      );
    });

    test('refuses a viewer the edit page by its address (FR-002)', async ({ page }) => {
      await open(page, language, '/plans/features/max_users/edit', VIEWER);
      await expect(page.getByTestId('ops-no-access')).toBeVisible();
      await expect(page.getByTestId('field-key')).toHaveCount(0);
    });

    test('finds every seeded feature by part of its Arabic name, its English name and its key (SC-005)', async ({
      page,
    }) => {
      await open(page, language, '/plans/features');
      const search = page.getByRole('searchbox');
      const missed: string[] = [];
      for (const feature of seededFeatures()) {
        for (const words of [
          feature.nameAr.slice(1),
          feature.nameEn.slice(1).toUpperCase(),
          feature.key.slice(1).toUpperCase(),
        ]) {
          await search.fill(words);
          if ((await row(page, feature.key).count()) === 0) {
            missed.push(`${feature.key} ← "${words}"`);
          }
        }
      }
      expect(missed).toEqual([]);
    });
  });
}

test('a super admin sees the plans between the home and the operators', async ({ page }) => {
  await open(page, 'ar', '/', SUPER);
  await expect(page.getByTestId('ops-side-bar').getByRole('link')).toHaveText([
    'الرئيسية',
    'الباقات',
    'المشغلون',
  ]);
});
