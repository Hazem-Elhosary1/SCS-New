import { Page, expect, test } from '@playwright/test';
import {
  CLINIC,
  StaffPlatform,
  TEAM_PERMISSIONS,
  pictureFor,
  roles,
  sampleSources,
  staffPlatform,
  useEnglish,
} from './staff-platform';

/**
 * Spec 006, User Story 9: every state of the frame, in both languages, at the size the design
 * is drawn (1440×900) — nothing untranslated, nothing wider than the page, and a picture of
 * each for laying beside its frame (contracts/shell-frame.md §9; run with SCS_SHOTS=<folder>).
 */
const NOW = Date.now();
const ITEMS = [
  { id: 'n-1', kind: 'ticket', title: 'ردّ جديد على تذكرتك', text: 'ردّ فريق الدعم على تذكرة «تعذّر طباعة الفاتورة».', at: new Date(NOW - 60_000).toISOString(), read: false },
  { id: 'n-2', kind: 'invoice', title: 'فاتورة مستحقة', text: 'فاتورة المريض أحمد سالم تجاوزت موعد السداد.', at: new Date(NOW - 3_600_000).toISOString(), read: false },
  { id: 'n-3', kind: 'subscription', title: 'اشتراك العيادة', text: 'ينتهي الاشتراك خلال سبعة أيام.', at: new Date(NOW - 86_400_000).toISOString(), read: true },
];
const BRANCHES = [
  { id: 'b-1', nameAr: 'فرع المعادي', nameEn: 'Maadi branch' },
  { id: 'b-2', nameAr: 'فرع مدينة نصر', nameEn: 'Nasr City branch' },
];

interface State {
  name: string;
  path?: string;
  platform?: Partial<StaffPlatform>;
  sample?: Record<string, unknown>;
  /** Brings the state on screen after the page is open. */
  reach?: (page: Page) => Promise<void>;
  /** A test id that must be visible in this state. */
  shows: string;
}

const folded = { language: null, sidebarFolded: true, dismissedBanner: null };

const STATES: State[] = [
  { name: 'default', shows: 'page-not-yet' },
  {
    name: 'limited-permissions',
    platform: { role: roles.team, permissions: TEAM_PERMISSIONS },
    shows: 'nav-invoices',
  },
  { name: 'folded', platform: { preferences: folded }, shows: 'side-bar-fold' },
  {
    name: 'group-opened',
    reach: (page) => page.getByTestId('nav-administration').click(),
    shows: 'nav-administration.roles',
  },
  {
    name: 'reports-opened',
    reach: (page) => page.getByTestId('nav-reports').click(),
    shows: 'nav-reports.feedback',
  },
  {
    name: 'data-opened',
    reach: (page) => page.getByTestId('nav-data').click(),
    shows: 'nav-data.consents',
  },
  {
    name: 'folded-on-a-child',
    path: 'administration/roles',
    platform: { preferences: folded },
    shows: 'side-bar-fold',
  },
  {
    name: 'account-menu',
    reach: (page) => page.getByTestId('account-chip').click(),
    shows: 'account-menu',
  },
  { name: 'bell-empty', reach: (page) => page.getByTestId('bell').click(), shows: 'notifications-empty' },
  {
    name: 'bell-items',
    sample: { notifications: ITEMS },
    reach: (page) => page.getByTestId('bell').click(),
    shows: 'mark-all-read',
  },
  {
    name: 'bell-failed',
    sample: { notificationsState: 'failed' },
    reach: (page) => page.getByTestId('bell').click(),
    shows: 'notifications-retry',
  },
  {
    name: 'branch-menu',
    sample: { branches: BRANCHES },
    reach: (page) => page.getByTestId('branch-switch').click(),
    shows: 'branch-menu',
  },
  ...(['trial', 'expiring', 'grace', 'readonly'] as const).map((state) => ({
    name: `banner-${state}`,
    sample: { subscription: { state, days: 5 } },
    shows: 'subscription-banner',
  })),
  {
    name: 'offline',
    path: 'administration/roles',
    reach: (page) => page.context().setOffline(true),
    shows: 'offline-alert',
  },
  { name: 'not-found', path: 'nowhere', shows: 'page-not-found' },
  { name: 'not-in-plan', path: 'tickets', sample: { outsidePlan: ['tickets'] }, shows: 'page-not-in-plan' },
  {
    name: 'not-in-role',
    path: 'settings',
    platform: { role: roles.team, permissions: TEAM_PERMISSIONS },
    shows: 'no-access',
  },
  { name: 'dental-and-doctor', sample: { dental: true, linkedToDoctor: true, unread: 4 }, shows: 'nav-dental-lab' },
];

for (const language of ['ar', 'en'] as const) {
  test.describe(`frame states — ${language}`, () => {
    for (const state of STATES) {
      test(state.name, async ({ page }) => {
        if (language === 'en') {
          await useEnglish(page);
        }
        if (state.sample !== undefined) {
          await sampleSources(page, state.sample);
        }
        await staffPlatform(page, { signedIn: true, ...state.platform });
        await page.setViewportSize({ width: 1440, height: 900 });
        await page.goto(`${CLINIC}/${state.path ?? ''}`);
        await expect(page.getByTestId('shell')).toBeVisible();
        await state.reach?.(page);

        await expect(page.getByTestId(state.shows)).toBeVisible();
        await expect(page.locator('html')).toHaveAttribute('dir', language === 'ar' ? 'rtl' : 'ltr');
        await expect(page.getByTestId('env-strip')).toBeVisible();

        // No key left untranslated; nothing wider or taller than the window.
        await expect(page.locator('body')).not.toContainText(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);
        const size = await page.evaluate(() => ({
          width: document.documentElement.scrollWidth,
          height: document.documentElement.scrollHeight,
        }));
        expect(size.width).toBeLessThanOrEqual(1440);
        expect(size.height).toBeLessThanOrEqual(900);

        // The side bar is on the reading side and the top bar spans the window.
        const [bar, top] = await Promise.all([
          page.getByTestId('side-bar').boundingBox(),
          page.getByTestId('top-bar').boundingBox(),
        ]);
        expect(top!.width).toBe(1440);
        expect(language === 'ar' ? bar!.x + bar!.width : bar!.x).toBe(language === 'ar' ? 1440 : 0);

        // In English the frame's own words are English; what the clinic wrote stays as written.
        if (language === 'en') {
          await expect(page.getByTestId('side-bar')).not.toContainText(/[؀-ۿ]/);
        }

        // No name in the side bar is cut short, in either language, while the bar is unfolded.
        if ((await page.getByTestId('side-bar').getAttribute('data-folded')) === 'false') {
          const cut = await page
            .getByTestId('side-bar')
            .locator('.words')
            .evaluateAll((words) =>
              words
                .filter((word) => word.scrollWidth > word.clientWidth)
                .map((word) => word.textContent),
            );
          expect(cut).toEqual([]);
        }

        await pictureFor(page, 'shell', language, state.name);
        await page.context().setOffline(false);
      });
    }
  });
}
