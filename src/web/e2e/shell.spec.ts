import { Page, expect, test } from '@playwright/test';
import {
  ALL_PERMISSIONS,
  CLINIC,
  TEAM_PERMISSIONS,
  roles,
  signOut,
  staffPlatform,
} from './staff-platform';

/**
 * Spec 006, User Stories 1 to 5: the frame every page of the console is shown in — the side
 * bar by permission, every address inside the frame, the account menu, the language and the
 * fold a person keeps. The list of destinations is written out here on purpose: the test is
 * the second reading of BRD Chapter 01 §A-2, not a copy of the app's own table.
 */
const SIDE_BAR: { key: string; path: string; ar: string; needs: string | null; parent?: string }[] = [
  { key: 'home', path: '', ar: 'الرئيسية', needs: null },
  { key: 'bookings', path: 'bookings', ar: 'الحجوزات', needs: 'bookings.view' },
  { key: 'patients', path: 'patients', ar: 'المرضى', needs: 'patients.view' },
  { key: 'invoices', path: 'invoices', ar: 'الفواتير والتحصيل', needs: 'money.view' },
  { key: 'inbox', path: 'inbox', ar: 'الصندوق الوارد', needs: 'inbox.view' },
  { key: 'tickets', path: 'tickets', ar: 'التذاكر', needs: 'tickets.view' },
  { key: 'reports.financial', path: 'reports/financial', ar: 'التقارير المالية', needs: 'reports.financial', parent: 'reports' },
  { key: 'reports.clinical', path: 'reports/clinical', ar: 'التقارير الطبية', needs: 'reports.clinical', parent: 'reports' },
  { key: 'reports.activity', path: 'reports/activity', ar: 'سجل الأنشطة والأمان', needs: 'reports.activity', parent: 'reports' },
  { key: 'reports.weekly', path: 'reports/weekly', ar: 'الملخص الأسبوعي', needs: 'reports.weekly', parent: 'reports' },
  { key: 'reports.feedback', path: 'reports/feedback', ar: 'تقييمات المرضى', needs: 'reports.feedback', parent: 'reports' },
  { key: 'administration.users', path: 'administration/users', ar: 'المستخدمون', needs: 'admin.users.view', parent: 'administration' },
  { key: 'administration.roles', path: 'administration/roles', ar: 'الأدوار والصلاحيات', needs: 'admin.roles.view', parent: 'administration' },
  { key: 'administration.licences', path: 'administration/licences', ar: 'الرخص والتغطية', needs: 'admin.licences.view', parent: 'administration' },
  { key: 'data.doctors', path: 'data/doctors', ar: 'الأطباء', needs: 'data.doctors.view', parent: 'data' },
  { key: 'data.branches', path: 'data/branches', ar: 'الفروع', needs: 'data.branches.view', parent: 'data' },
  { key: 'data.services', path: 'data/services', ar: 'الخدمات والأسعار', needs: 'data.services.view', parent: 'data' },
  { key: 'data.consents', path: 'data/consents', ar: 'نصوص الموافقة', needs: 'data.consents.view', parent: 'data' },
  { key: 'settings', path: 'settings', ar: 'الإعدادات', needs: 'settings.view' },
];

const TOP = ['home', 'bookings', 'patients', 'invoices', 'inbox', 'tickets', 'reports', 'administration', 'data', 'settings'];

async function shownInSideBar(page: Page): Promise<string[]> {
  return page
    .getByTestId('side-bar')
    .locator('[data-testid^="nav-"]')
    .evaluateAll((items) => items.map((item) => item.getAttribute('data-testid')!.slice(4)));
}

async function inside(page: Page, path = ''): Promise<void> {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/${path}`);
  await expect(page.getByTestId('shell')).toBeVisible();
}

test.describe('the frame', () => {
  test('after sign-in the person is inside it: who they are, where they are, and the way around', async ({
    page,
  }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    await expect(page.getByTestId('top-bar')).toBeVisible();
    await expect(page.getByTestId('side-bar')).toBeVisible();
    await expect(page.getByTestId('signed-in-clinic')).toHaveText('عيادة النور');
    // Until accounts have a name (spec 026) the chip shows the email.
    await expect(page.getByTestId('signed-in-email')).toHaveText('owner@noor.example');
    await expect(page.getByTestId('account-role')).toHaveText('مالك العيادة');
    await expect(page.getByTestId('page-title')).toHaveText('الرئيسية');
    await expect(page.getByTestId('page-not-yet')).toContainText('هذه الصفحة غير متاحة بعد');
    await expect(page.getByTestId('nav-home')).toHaveAttribute('aria-current', 'page');

    // The side bar is on the reading side: the right in Arabic.
    const bar = (await page.getByTestId('side-bar').boundingBox())!;
    expect(bar.x + bar.width).toBe(1440);
    expect(bar.width).toBe(240);
  });

  test('an owner sees every destination, in the one order, under its parent', async ({ page }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    expect(await shownInSideBar(page)).toEqual(TOP);

    // One parent is open at a time: opening one closes the other.
    const shown = await shownInSideBar(page);
    for (const parent of ['reports', 'administration', 'data']) {
      await page.getByTestId(`nav-${parent}`).click();
      await expect(page.getByTestId(`nav-${parent}`)).toHaveAttribute('aria-expanded', 'true');
      await expect(page.locator('[data-testid^="nav-"][aria-expanded="true"]')).toHaveCount(1);
      shown.push(...(await shownInSideBar(page)));
    }
    for (const place of SIDE_BAR) {
      expect(shown, place.key).toContain(place.key);
      if (place.parent !== undefined) {
        await page.getByTestId(`nav-${place.parent}`).click();
        if (!(await page.getByTestId(`nav-${place.key}`).isVisible())) {
          await page.getByTestId(`nav-${place.parent}`).click();
        }
      }
      await expect(page.getByTestId(`nav-${place.key}`)).toHaveText(place.ar);
    }
    // «معمل الأسنان» is for dental clinics, and no clinic says it is one yet.
    expect(shown).not.toContain('dental-lab');
  });

  test('every destination opens inside the frame under its own name, by press and by address', async ({
    page,
  }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    for (const place of SIDE_BAR) {
      if (place.parent !== undefined && !(await page.getByTestId(`nav-${place.key}`).isVisible())) {
        await page.getByTestId(`nav-${place.parent}`).click();
      }
      await page.getByTestId(`nav-${place.key}`).click();

      await expect(page, place.key).toHaveURL(`${CLINIC}/${place.path}`);
      await expect(page.getByTestId('page-title'), place.key).toHaveText(place.ar);
      await expect(page.getByTestId(`nav-${place.key}`), place.key).toHaveAttribute('aria-current', 'page');
      await expect(page.getByTestId('side-bar')).toBeVisible();
      await expect(page.locator('[aria-current="page"][data-testid^="nav-"]')).toHaveCount(1);
    }

    // A parent that is closed over the open page carries the mark itself, so the place is never lost.
    await expect(page.getByTestId('nav-settings')).toHaveAttribute('aria-current', 'page');
    await inside(page, 'administration/roles');
    await expect(page.getByTestId('nav-administration')).toHaveAttribute('data-active', 'false');
    await page.getByTestId('nav-administration').click();
    await expect(page.getByTestId('nav-administration.roles')).toHaveCount(0);
    await expect(page.getByTestId('nav-administration')).toHaveAttribute('data-active', 'true');
    await page.getByTestId('nav-administration').click();
    await expect(page.getByTestId('nav-administration')).toHaveAttribute('data-active', 'false');
    await expect(page.getByTestId('nav-administration.roles')).toHaveAttribute('aria-current', 'page');
    await page.getByTestId('side-bar-fold').click();
    await expect(page.getByTestId('nav-administration')).toHaveAttribute('data-active', 'true');
    await page.getByTestId('side-bar-fold').click();

    // And straight from the address bar, with the parent already open.
    await inside(page, 'data/services');
    await expect(page.getByTestId('page-title')).toHaveText('الخدمات والأسعار');
    await expect(page.getByTestId('nav-data')).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByTestId('nav-data.services')).toHaveAttribute('aria-current', 'page');
  });

  for (const place of SIDE_BAR.filter((each) => each.needs !== null)) {
    test(`without «${place.needs}» there is no «${place.ar}» in the side bar, and its address says so`, async ({
      page,
    }) => {
      const platform = await staffPlatform(page, {
        signedIn: true,
        role: roles.team,
        permissions: ALL_PERMISSIONS.filter((code) => code !== place.needs),
      });
      await inside(page, place.path);

      await expect(page.getByTestId('no-access')).toBeVisible();
      await expect(page.getByTestId('page-title')).toHaveText(place.ar);
      await expect(page.getByTestId('side-bar')).toBeVisible();
      if (place.parent !== undefined) {
        await page.getByTestId(`nav-${place.parent}`).click();
      }
      expect(await shownInSideBar(page)).not.toContain(place.key);
      expect(platform.calls).not.toContain('GET /v1/roles');
    });
  }

  test('a person with a few permissions sees a short side bar, and a parent with no child is gone', async ({
    page,
  }) => {
    await staffPlatform(page, { signedIn: true, role: roles.team, permissions: TEAM_PERMISSIONS });
    await inside(page);

    expect(await shownInSideBar(page)).toEqual(['home', 'bookings', 'patients', 'invoices']);
    await expect(page.getByTestId('account-role')).toHaveText('فريق العيادة');
  });

  test('an account with no role still has the frame and the home page', async ({ page }) => {
    await staffPlatform(page, { signedIn: true, role: null, permissions: [] });
    await inside(page);

    expect(await shownInSideBar(page)).toEqual(['home']);
    await expect(page.getByTestId('account-role')).toHaveCount(0);
    await expect(page.getByTestId('page-title')).toHaveText('الرئيسية');
  });

  test('a parent leads to its first child the person may open, and to nothing for a person with none', async ({
    page,
  }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await inside(page, 'reports');
    await expect(page).toHaveURL(`${CLINIC}/reports/financial`);

    platform.permissions = ALL_PERMISSIONS.filter((code) => code !== 'reports.financial');
    await inside(page, 'reports');
    await expect(page).toHaveURL(`${CLINIC}/reports/clinical`);

    platform.permissions = TEAM_PERMISSIONS;
    await inside(page, 'reports');
    await expect(page.getByTestId('page-not-found')).toBeVisible();
  });

  test('an address that is no destination says so inside the frame, with the way home', async ({
    page,
  }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page, 'nowhere/at-all');

    await expect(page.getByTestId('page-title')).toHaveText('الصفحة غير موجودة');
    await expect(page.getByTestId('page-not-found')).toContainText(
      'العنوان المطلوب لا يقود إلى صفحة في لوحة العيادة.',
    );
    await expect(page.getByTestId('side-bar')).toBeVisible();
    await expect(page.locator('[aria-current="page"][data-testid^="nav-"]')).toHaveCount(0);

    await page.getByTestId('home-link').click();
    await expect(page).toHaveURL(`${CLINIC}/`);

    // A dental clinic's destination is not an address in a clinic that is not one.
    await inside(page, 'dental-lab');
    await expect(page.getByTestId('page-not-found')).toBeVisible();
    await inside(page, 'account/doctor-profile');
    await expect(page.getByTestId('page-not-found')).toBeVisible();
  });

  test('without a session no address shows the frame', async ({ page }) => {
    await staffPlatform(page);

    for (const path of ['', 'bookings', 'administration/roles', 'dev/sample-list', 'nowhere']) {
      await page.goto(`${CLINIC}/${path}`);
      await expect(page).toHaveURL(`${CLINIC}/sign-in`);
      await expect(page.getByTestId('shell')).toHaveCount(0);
    }
  });
});

test.describe('the account menu', () => {
  test('opens under the chip with the profile and sign-out, and closes on Escape and outside', async ({
    page,
  }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);
    const chip = page.getByTestId('account-chip');

    await chip.click();
    const menu = page.getByTestId('account-menu');
    await expect(menu).toBeVisible();
    await expect(chip).toHaveAttribute('aria-expanded', 'true');
    await expect(menu.locator('[scsmenuitem]')).toHaveText(['الملف والاشتراك', 'تسجيل الخروج']);

    const [opener, opened] = [(await chip.boundingBox())!, (await menu.boundingBox())!];
    expect(opened.width).toBe(240);
    expect(opened.y).toBeGreaterThanOrEqual(opener.y + opener.height);

    await page.keyboard.press('Escape');
    await expect(menu).toHaveCount(0);
    await expect(chip).toBeFocused();

    await chip.click();
    await page.getByTestId('page-title').click({ force: true });
    await expect(menu).toHaveCount(0);
  });

  test('«الملف والاشتراك» opens its page inside the frame', async ({ page }) => {
    await staffPlatform(page, { signedIn: true });
    await inside(page);

    await page.getByTestId('account-chip').click();
    await page.getByTestId('menu-subscription').click();

    await expect(page).toHaveURL(`${CLINIC}/account/subscription`);
    await expect(page.getByTestId('page-title')).toHaveText('الملف والاشتراك');
    await expect(page.getByTestId('account-menu')).toHaveCount(0);
  });

  test('«تسجيل الخروج» ends the session at once and shows the plain form', async ({ page }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await inside(page, 'bookings');

    await signOut(page);

    await expect(page).toHaveURL(`${CLINIC}/sign-in`);
    await expect(page.locator('scs-banner')).toHaveCount(0);
    await expect(page.locator('dialog[open]')).toHaveCount(0);
    expect(platform.calls).toContain('POST /v1/auth/sign-out');
    expect(platform.signedIn).toBe(false);
  });
});

test.describe('what a person keeps', () => {
  test('the language turns the whole console at once and is saved for the person', async ({ page }) => {
    const platform = await staffPlatform(page, {
      signedIn: true,
      preferences: { language: 'ar', sidebarFolded: null, dismissedBanner: null },
    });
    await inside(page, 'administration/roles');
    await page.evaluate(() => ((window as unknown as { marker: string }).marker = 'still here'));

    await page.getByTestId('top-bar').getByTestId('language-switch').click();

    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.getByTestId('page-title')).toHaveText('Roles & permissions');
    await expect(page.getByTestId('nav-bookings')).toHaveText('Bookings');
    await expect(page.getByTestId('signed-in-clinic')).toHaveText('Al Noor Clinic');
    await expect(page.getByTestId('account-role')).toHaveText('Clinic owner');
    expect((await page.getByTestId('side-bar').boundingBox())!.x).toBe(0);
    expect(await page.evaluate(() => (window as unknown as { marker: string }).marker)).toBe('still here');
    await expect.poll(() => platform.preferencesSent).toEqual([{ language: 'en' }]);
    // Nothing left in the other language or untranslated.
    await expect(page.getByTestId('side-bar')).not.toContainText(/[؀-ۿ]/);
    await expect(page.locator('body')).not.toContainText(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);

    // The next session, on any browser, starts in it.
    await page.evaluate(() => localStorage.clear());
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByTestId('page-title')).toHaveText('Roles & permissions');
  });

  test('a person who never chose keeps the language they signed in with as their first choice', async ({
    page,
  }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await page.addInitScript(() => localStorage.setItem('scs.language', 'en'));
    await inside(page);

    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect.poll(() => platform.preferencesSent).toEqual([{ language: 'en' }]);
  });

  test('a choice that cannot be saved still applies, and nothing is said about it', async ({ page }) => {
    const platform = await staffPlatform(page, {
      signedIn: true,
      preferences: { language: 'ar', sidebarFolded: null, dismissedBanner: null },
      preferencesAnswer: { status: 503, code: 'SERVICE_UNAVAILABLE' },
    });
    await inside(page);

    await page.getByTestId('top-bar').getByTestId('language-switch').click();

    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect.poll(() => platform.preferencesSent.length).toBe(1);
    await expect(page.locator('scs-banner, [data-testid="toast"], [role="alert"]')).toHaveCount(0);

    // It goes again with the next choice.
    platform.preferencesAnswer = { status: 204 };
    await page.getByTestId('side-bar-fold').click();
    await expect.poll(() => platform.preferencesSent.at(-1)).toEqual({ language: 'en', sidebarFolded: true });
  });

  test('the side bar folds to icons with their names on hover, and stays as the person left it', async ({
    page,
  }) => {
    const platform = await staffPlatform(page, { signedIn: true });
    await inside(page);
    const bar = page.getByTestId('side-bar');

    await page.getByTestId('side-bar-fold').click();

    await expect(bar).toHaveAttribute('data-folded', 'true');
    await expect.poll(async () => (await bar.boundingBox())!.width).toBe(72);
    await expect(page.getByTestId('side-bar-fold')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByTestId('nav-bookings').locator('.words')).toHaveCSS('opacity', '0');
    // Nothing of the folded bar is cut or scrolls sideways, and no group name shows through.
    expect(await bar.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    for (const label of await bar.locator('.label').all()) {
      expect((await label.boundingBox())?.height ?? 0).toBeLessThanOrEqual(1);
    }
    const icon = (await page.getByTestId('nav-bookings').locator('scs-icon').boundingBox())!;
    const box = (await bar.boundingBox())!;
    expect(Math.abs(icon.x + icon.width / 2 - (box.x + box.width / 2))).toBeLessThanOrEqual(1);
    await expect.poll(() => platform.preferencesSent.at(-1)).toMatchObject({ sidebarFolded: true });

    await page.getByTestId('nav-bookings').hover();
    await expect(page.locator('.scs-tooltip')).toHaveText('الحجوزات');

    // An icon still navigates; a group's icon unfolds the bar with the group open.
    await page.getByTestId('nav-patients').click();
    await expect(page).toHaveURL(`${CLINIC}/patients`);
    await expect(bar).toHaveAttribute('data-folded', 'true');

    await page.reload();
    await expect(bar).toHaveAttribute('data-folded', 'true');

    await page.getByTestId('nav-administration').click();
    await expect(bar).toHaveAttribute('data-folded', 'false');
    await expect(page.getByTestId('nav-administration.users')).toBeVisible();
    await expect.poll(() => platform.preferencesSent.at(-1)).toMatchObject({ sidebarFolded: false });
  });

  test('one person’s choices are not another’s', async ({ page }) => {
    const platform = await staffPlatform(page, {
      signedIn: true,
      preferences: { language: 'en', sidebarFolded: true, dismissedBanner: null },
    });
    await inside(page);
    await expect(page.getByTestId('side-bar')).toHaveAttribute('data-folded', 'true');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');

    await signOut(page);
    platform.preferences = { language: 'ar', sidebarFolded: false, dismissedBanner: null };
    platform.signedIn = true;
    await inside(page);

    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    await expect(page.getByTestId('side-bar')).toHaveAttribute('data-folded', 'false');
  });
});

test('the fold button is at the top of the side bar, beside the first group’s name', async ({ page }) => {
  await staffPlatform(page, { signedIn: true });
  await inside(page);
  const fold = page.getByTestId('side-bar-fold');
  const bar = page.getByTestId('side-bar');

  // On the line of «القائمة», at its far end, above every item — seen without looking for it.
  const [button, label, home] = await Promise.all([
    fold.boundingBox(),
    bar.locator('.label').first().boundingBox(),
    page.getByTestId('nav-home').boundingBox(),
  ]);
  expect(Math.abs(button!.y + button!.height / 2 - (label!.y + label!.height / 2))).toBeLessThan(6);
  expect(button!.x + button!.width).toBeLessThanOrEqual(label!.x);
  expect(button!.y + button!.height).toBeLessThanOrEqual(home!.y);
  await fold.hover();
  await expect(page.locator('.scs-tooltip')).toHaveText('طيّ القائمة');
  // Drawn as a button: a hairline around it.
  await expect(fold).toHaveCSS('border-top-width', '1px');

  // Folded, it stays at the top, centred over the icons.
  await fold.click();
  await expect(bar).toHaveAttribute('data-folded', 'true');
  await expect(fold).toBeVisible();
  await expect
    .poll(async () => {
      const [now, whole] = await Promise.all([fold.boundingBox(), bar.boundingBox()]);
      return Math.abs(now!.x + now!.width / 2 - (whole!.x + whole!.width / 2));
    })
    .toBeLessThanOrEqual(1);
  expect((await fold.boundingBox())!.y).toBeLessThan((await page.getByTestId('nav-home').boundingBox())!.y);
});
