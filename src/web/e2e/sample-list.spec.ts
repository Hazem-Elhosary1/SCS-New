import { Page, expect, test } from '@playwright/test';
import { CLINIC, pictureFor, staffPlatform, useEnglish } from './staff-platform';

/**
 * Spec 006, User Story 8 (FR-030 to FR-037, FR-042): the rules of lists, tables, dialogs and
 * passing notices, each seen working on the sample list. The numbers are the rules of
 * specs/006-app-shell/contracts/list-contract.md §3.
 */
const LIST = `${CLINIC}/dev/sample-list`;

async function open(page: Page, query = ''): Promise<void> {
  await staffPlatform(page, { signedIn: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${LIST}${query}`);
  await expect(page.getByTestId('page-title')).toHaveText(/قائمة تجريبية|Sample list/);
}

const top = async (page: Page, selector: string) => (await page.locator(selector).first().boundingBox())!.y;

test('the anatomy is in one order: header, tabs, card heading, toolbar, chips, rows, footer', async ({
  page,
}) => {
  await open(page);
  await page.getByTestId('list-filter').click();
  await page.getByTestId('filter-checkup').check();
  await page.getByTestId('filter-apply').click();

  const order = await Promise.all(
    ['scs-page-header', 'scs-tabs', 'scs-card h2', 'scs-list-toolbar', 'scs-applied-chips', 'table', 'scs-table-footer'].map(
      (selector) => top(page, selector),
    ),
  );
  expect(order).toEqual([...order].sort((a, b) => a - b));
});

test('rules 1 and 2: search at the reading start with a fixed width; «تصفية» at the other end, no badge', async ({
  page,
}) => {
  await open(page);
  const search = (await page.locator('scs-list-toolbar .search').boundingBox())!;
  const filter = page.getByTestId('list-filter');
  const card = (await page.locator('scs-list-toolbar').boundingBox())!;

  expect(search.width).toBe(320);
  // Arabic: the start is the right edge, the end the left.
  expect(Math.round(search.x + search.width)).toBe(Math.round(card.x + card.width));
  expect(Math.round((await filter.boundingBox())!.x)).toBe(Math.round(card.x));
  await expect(filter).toHaveText('تصفية');
  await expect(page.getByTestId('list-search')).toHaveAttribute('placeholder', 'باسم السجل');
  await expect(page.locator('scs-list-toolbar button')).toHaveCount(1);
});

test('rule 3: the filter panel opens downward over the rows, with its button drawn pressed', async ({
  page,
}) => {
  await open(page);
  const button = page.getByTestId('list-filter');

  await button.click();

  const panel = page.getByTestId('filter-popover');
  await expect(button).toHaveAttribute('aria-expanded', 'true');
  const [opener, opened, footer, header] = await Promise.all([
    button.boundingBox(),
    panel.boundingBox(),
    page.locator('scs-table-footer').boundingBox(),
    page.locator('scs-page-header').boundingBox(),
  ]);
  expect(opened!.y).toBeGreaterThanOrEqual(opener!.y + opener!.height);
  expect(opened!.y + opened!.height).toBeLessThanOrEqual(footer!.y);
  expect(opened!.y).toBeGreaterThan(header!.y + header!.height);

  // As the frame draws it: its name and a × at the top, «مسح الكل» at the start of the foot
  // and «تطبيق» at its end.
  await expect(page.getByTestId('filter-title')).toHaveText('تصفية السجلات');
  const [clear, apply] = await Promise.all([
    page.getByTestId('filter-clear').boundingBox(),
    page.getByTestId('filter-apply').boundingBox(),
  ]);
  expect(clear!.x).toBeGreaterThan(apply!.x + apply!.width);
  await page.getByTestId('filter-close').click();
  await expect(panel).toHaveCount(0);
  await button.click();
  await expect(panel).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(panel).toHaveCount(0);
  await expect(button).toHaveAttribute('aria-expanded', 'false');

  // A press outside closes it too, and a press on the button itself opens and closes it.
  await button.click();
  await expect(panel).toBeVisible();
  await page.getByTestId('page-title').click();
  await expect(panel).toHaveCount(0);
  await button.click();
  await expect(panel).toBeVisible();
  await button.click();
  await expect(panel).toHaveCount(0);
});

test('rules 4 and 6: what is applied shows once, as chips under the toolbar, and clears from there', async ({
  page,
}) => {
  await open(page);
  await expect(page.locator('scs-applied-chips')).toBeHidden();

  await page.getByTestId('list-filter').click();
  await page.getByTestId('filter-procedure').check();
  await page.getByTestId('filter-apply').click();

  await expect(page.getByTestId('applied-chip')).toHaveText(['النوع: إجراء']);
  await expect(page.getByTestId('table-count')).toHaveText('١–١٠ من ١٧ سجلًا');
  await expect(page.getByTestId('list-filter')).toHaveText('تصفية');
  await expect(page.locator('tbody tr td:nth-child(2)')).toHaveText(Array(10).fill('إجراء'));
  // The tab the person was on is not changed by filtering.
  await expect(page.getByTestId('tab-all')).toHaveAttribute('aria-selected', 'true');

  await page.getByTestId('clear-all').click();

  await expect(page.locator('scs-applied-chips')).toBeHidden();
  await expect(page.getByTestId('table-count')).toHaveText('١–١٠ من ٥٢ سجلًا');
});

test('tabs split the list exclusively, with counts', async ({ page }) => {
  await open(page);

  await expect(page.getByTestId('tab-all')).toContainText('٥٢');
  await expect(page.getByTestId('tab-archived')).toContainText('٥');

  await page.getByTestId('tab-archived').click();

  await expect(page.getByTestId('sample-row')).toHaveCount(5);
  await expect(page.getByTestId('table-count')).toHaveText('١–٥ من ٥ سجلًا');
  // Rule 11: one page shows the count alone.
  await expect(page.getByTestId('table-paging')).toHaveCount(0);
});

test('rules 10 and 11: one footer row — the count at the start, the paging at the end, disabled at the ends', async ({
  page,
}) => {
  await open(page);
  const [count, paging] = await Promise.all([
    page.getByTestId('table-count').boundingBox(),
    page.getByTestId('table-paging').boundingBox(),
  ]);
  expect(Math.abs(count!.y + count!.height / 2 - (paging!.y + paging!.height / 2))).toBeLessThan(4);
  expect(count!.x).toBeGreaterThan(paging!.x);

  await expect(page.getByTestId('page-previous')).toBeDisabled();
  await expect(page.getByTestId('page-next')).toBeEnabled();
  await expect(page.getByTestId('page-1')).toHaveText('١');

  await page.getByTestId('page-next').click();
  await expect(page.getByTestId('table-count')).toHaveText('١١–٢٠ من ٥٢ سجلًا');
  await expect(page.getByTestId('page-previous')).toBeEnabled();

  await page.getByTestId('page-6').click();
  await expect(page.getByTestId('table-count')).toHaveText('٥١–٥٢ من ٥٢ سجلًا');
  await expect(page.getByTestId('page-next')).toBeDisabled();
  await expect(page.getByTestId('sample-row')).toHaveCount(2);
});

test('rules 7, 8 and 13: a row opens its record; «⋯» has no "open", draws delete red, and marks its row', async ({
  page,
}) => {
  await open(page);
  const row = page.getByTestId('sample-row').nth(2);

  await row.click();
  await expect(page.getByTestId('opened-record')).toHaveText('فُتح «سجل تجريبي ٣»');

  // With the keyboard too.
  await page.getByTestId('sample-row').nth(4).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('opened-record')).toHaveText('فُتح «سجل تجريبي ٥»');

  await row.getByTestId('row-menu').click();

  const items = page.getByTestId('row-menu-items').locator('[scsmenuitem]');
  await expect(items).toHaveText(['أرشفة', 'حذف']);
  await expect(items.nth(1)).toHaveAttribute('data-danger', 'true');
  await expect(row).toHaveAttribute('data-marked', 'true');
  await expect(row.getByTestId('row-menu')).toHaveAttribute('aria-expanded', 'true');
  // Pressing «⋯» did not open the record.
  await expect(page.getByTestId('opened-record')).toHaveText('فُتح «سجل تجريبي ٥»');

  await page.keyboard.press('Escape');
  await expect(row).toHaveAttribute('data-marked', 'false');
});

test('rule 9: with the view alone there is no «⋯» and no control that writes; rows still open', async ({
  page,
}) => {
  await open(page, '?as=viewer');

  await expect(page.getByTestId('row-menu')).toHaveCount(0);
  await expect(page.getByTestId('sample-add')).toHaveCount(0);
  await expect(page.locator('button:disabled:visible:not([data-testid="page-previous"])')).toHaveCount(0);

  await page.getByTestId('sample-row').first().click();
  await expect(page.getByTestId('opened-record')).toBeVisible();
});

test('rules 14, 18 and 19: deleting asks, the button names the action, then a notice and a visible change', async ({
  page,
}) => {
  await open(page);
  const row = page.getByTestId('sample-row').first();
  await row.getByTestId('row-menu').click();
  await page.getByTestId('row-delete').click();

  await expect(page.getByTestId('dialog-title')).toHaveText('حذف السجل');
  await expect(page.getByTestId('confirm-text')).toContainText('«سجل تجريبي ١»');
  const confirm = page.getByTestId('confirm-action');
  await expect(confirm).toHaveText('حذف السجل');
  await expect(confirm).toBeDisabled();
  await expect(row).toHaveAttribute('data-marked', 'true');

  // The way back changes nothing.
  await page.getByTestId('confirm-cancel').click();
  await expect(page.locator('dialog[open]')).toHaveCount(0);
  await expect(page.getByTestId('sample-name').first()).toHaveText('سجل تجريبي ١');
  await expect(page.getByTestId('toast')).toHaveCount(0);

  await row.getByTestId('row-menu').click();
  await page.getByTestId('row-delete').click();
  await page.getByTestId('confirm-understood').check();
  await expect(confirm).toBeEnabled();
  await confirm.click();

  await expect(page.getByTestId('sample-name').first()).toHaveText('سجل تجريبي ٢');
  await expect(page.getByTestId('table-count')).toHaveText('١–١٠ من ٥١ سجلًا');
  const toast = page.getByTestId('toast');
  await expect(toast).toContainText('حُذف السجل');

  // In the corner at the reading end — the left in Arabic — beside the header buttons, never over them.
  await expect
    .poll(async () => (await page.getByTestId('page-actions').boundingBox())!.x)
    .toBeGreaterThanOrEqual(32 + 360);
  const [notice, buttons] = await Promise.all([
    toast.boundingBox(),
    page.getByTestId('page-actions').boundingBox(),
  ]);
  expect(notice!.x).toBe(32);
  expect(notice!.y).toBeLessThan(buttons!.y + buttons!.height);
  expect(notice!.width).toBe(360);
  expect(notice!.y + notice!.height <= buttons!.y || notice!.y >= buttons!.y + buttons!.height || notice!.x + notice!.width <= buttons!.x).toBe(true);

  await expect(toast).toHaveCount(0, { timeout: 7000 });
});

test('an action from «⋯» is followed by a notice and a visible change', async ({ page }) => {
  await open(page);

  await page.getByTestId('sample-row').first().getByTestId('row-menu').click();
  await page.getByTestId('row-archive').click();

  await expect(page.getByTestId('toast')).toContainText('أُرشف السجل');
  await expect(page.getByTestId('tab-archived')).toContainText('٦');
  // Rule 6: the tab the person is on did not change.
  await expect(page.getByTestId('tab-all')).toHaveAttribute('aria-selected', 'true');
});

test('the states: empty has no toolbar and no export', async ({ page }) => {
  await open(page, '?state=empty');

  await expect(page.getByTestId('sample-empty').getByTestId('empty-title')).toHaveText('لا سجلات بعد');
  await expect(page.locator('scs-list-toolbar')).toHaveCount(0);
  await expect(page.locator('scs-tabs')).toHaveCount(0);
  await expect(page.locator('scs-table-footer')).toHaveCount(0);
  // Rule 12: a control with nothing to act on is absent; the create action stays.
  await expect(page.getByTestId('sample-export')).toHaveCount(0);
  await expect(page.getByTestId('sample-add')).toBeVisible();
});

test('the states: no results keeps the toolbar, and the way out is the × or «مسح الكل»', async ({ page }) => {
  await open(page);

  await page.getByTestId('list-search').fill('لا يوجد');

  await expect(page.getByTestId('sample-no-results').getByTestId('empty-title')).toHaveText('لا نتائج مطابقة');
  await expect(page.locator('scs-list-toolbar')).toBeVisible();
  await expect(page.locator('table')).toHaveCount(0);

  await page.getByTestId('list-search-clear').click();
  await expect(page.getByTestId('sample-row')).toHaveCount(10);
});

test('the states: an error keeps the page and the toolbar, shows a dash, and retries', async ({ page }) => {
  await open(page, '?state=error');

  await expect(page.getByTestId('sample-error')).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId('page-note')).toHaveText('—');
  await expect(page.locator('scs-list-toolbar')).toBeVisible();
  await expect(page.getByTestId('sample-export')).toHaveCount(0);
  await expect(page.locator('table')).toHaveCount(0);

  await page.getByTestId('sample-retry').click();
  await expect(page.getByTestId('sample-row')).toHaveCount(10);
});

test('the states: loading shows skeletons in place of the rows', async ({ page }) => {
  await open(page, '?state=loading');

  await expect(page.getByTestId('sample-loading')).toBeVisible();
  await expect(page.locator('table')).toHaveCount(0);
  await expect(page.getByTestId('sample-export')).toHaveCount(0);
});

test('rule 22: offline, every writing control is disabled with its reason while search still filters', async ({
  page,
  context,
}) => {
  await open(page);
  const add = page.getByTestId('sample-add');
  await expect(add).toBeEnabled();

  await context.setOffline(true);
  await expect(page.getByTestId('offline-alert')).toBeVisible();

  await expect(add).toBeDisabled();
  await expect(add).toHaveAttribute('title', 'معطَّل حتى يعود الاتصال بالإنترنت');
  await page.getByTestId('sample-row').first().getByTestId('row-menu').click();
  await expect(page.getByTestId('row-archive')).toBeDisabled();
  await expect(page.getByTestId('row-delete')).toBeDisabled();
  await page.keyboard.press('Escape');

  // Reading goes on with what was loaded.
  await page.getByTestId('list-search').fill('٤٢');
  await expect(page.getByTestId('sample-name')).toHaveText(['سجل تجريبي ٤٢']);

  await context.setOffline(false);
  await expect(page.getByTestId('offline-alert')).toHaveCount(0);
  await expect(page.getByTestId('sample-add')).toBeEnabled();
});

test('reads in English, mirrored, with nothing left untranslated', async ({ page }) => {
  await useEnglish(page);
  await open(page);

  await expect(page.getByTestId('table-count')).toHaveText('1–10 of 52 records');
  await expect(page.getByTestId('list-filter')).toHaveText('Filter');
  await expect(page.getByTestId('sample-name').first()).toHaveText('Sample record 1');
  await expect(page.locator('main')).not.toContainText(/[؀-ۿ]/);
  await expect(page.locator('body')).not.toContainText(/\b(app|ui)\.[a-zA-Z]+\.[a-zA-Z.]+/);
  const [search, card] = await Promise.all([
    page.locator('scs-list-toolbar .search').boundingBox(),
    page.locator('scs-list-toolbar').boundingBox(),
  ]);
  expect(Math.round(search!.x)).toBe(Math.round(card!.x));
});

for (const language of ['ar', 'en'] as const) {
  test(`pictures for the design comparison — ${language}`, async ({ page }) => {
    test.skip(!process.env['SCS_SHOTS'], 'only when SCS_SHOTS names a folder');
    if (language === 'en') {
      await useEnglish(page);
    }
    await open(page);
    await pictureFor(page, 'list', language, 'default');

    await page.getByTestId('list-filter').click();
    await pictureFor(page, 'list', language, 'filter-open');
    await page.getByTestId('filter-followup').check();
    await page.getByTestId('filter-apply').click();
    await pictureFor(page, 'list', language, 'filtered');

    await page.getByTestId('sample-row').first().getByTestId('row-menu').click();
    await pictureFor(page, 'list', language, 'row-menu');
    await page.getByTestId('row-delete').click();
    await page.getByTestId('confirm-understood').check();
    await pictureFor(page, 'list', language, 'confirmation');
    await page.getByTestId('confirm-action').click();
    await expect(page.getByTestId('toast')).toBeVisible();
    await pictureFor(page, 'list', language, 'toast');

    await page.getByTestId('list-search').fill('zzz');
    await pictureFor(page, 'list', language, 'no-results');

    for (const state of ['empty', 'error', 'loading']) {
      await page.goto(`${LIST}?state=${state}`);
      await expect(page.getByTestId('page-title')).toBeVisible();
      await pictureFor(page, 'list', language, state);
    }
  });
}
