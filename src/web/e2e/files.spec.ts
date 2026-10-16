import { Locator, Page, expect, test } from '@playwright/test';
import { CLINIC, PlatformFile, StaffPlatform, pictureFor, problem, staffPlatform, useEnglish } from './staff-platform';

/**
 * Spec 009, User Story 4: the upload zone and the file card that every page with files will
 * place — seen on the builders' page, in every state, in both languages, with the keyboard,
 * and from choosing a file to downloading, removing and restoring it.
 */
const MB = 1024 * 1024;

const pdf = (name: string, size = 3000) => ({ name, mimeType: 'application/pdf', buffer: Buffer.alloc(size, 0x20) });

function saved(id: string, more: Partial<PlatformFile> = {}): PlatformFile {
  return {
    id,
    name: 'ملف-الأشعة.pdf',
    sizeBytes: Math.round(2.4 * MB),
    type: 'pdf',
    state: 'available',
    refusedFor: null,
    uploadedAt: '2026-08-21T09:00:00Z',
    uploadedByAccountId: '0199c0de-0000-7000-8000-000000000001',
    restorableUntil: null,
    held: false,
    ...more,
  };
}

async function open(page: Page, start: Partial<StaffPlatform> = {}): Promise<StaffPlatform> {
  const platform = await staffPlatform(page, { signedIn: true, ...start });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${CLINIC}/dev/files`);
  await expect(page.getByTestId('files-live')).toBeVisible();
  return platform;
}

const live = (page: Page) => page.getByTestId('live-field');
const chooser = (page: Page) => live(page).getByTestId('file-chooser');
const zone = (page: Page) => live(page).locator('scs-file-upload');
const rows = (page: Page) => live(page).locator('scs-file-card');
const savedCards = (page: Page) => page.getByTestId('files-saved').locator('scs-file-card');
const line = (card: Locator) => card.getByTestId('file-line');

test.describe('the upload zone and the file card, every state', () => {
  test('in Arabic, as the design writes them', async ({ page }) => {
    await open(page);
    const zones = page.getByTestId('files-zones');

    await expect(zones.getByTestId('zone-default').getByTestId('file-zone-title')).toHaveText('إفلات الملف هنا أو النقر للاختيار');
    await expect(zones.getByTestId('zone-default').getByTestId('file-zone-hint')).toHaveText('ملف PDF أو صورة · حتى ١٠ ميجابايت');
    await expect(zones.getByTestId('zone-uploading').getByTestId('file-zone-title')).toHaveText('جارٍ رفع الملف…');
    await expect(zones.getByTestId('zone-uploading').getByTestId('file-zone-bar')).toHaveAttribute('aria-valuenow', '64');
    await expect(zones.getByTestId('zone-error').getByTestId('file-zone-title')).toHaveText('تعذّر رفع الملف');
    await expect(zones.getByTestId('zone-error').getByTestId('file-zone-hint')).toHaveText('الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    await expect(zones.getByTestId('zone-disabled')).toHaveAttribute('data-state', 'disabled');
    await expect(zones.getByTestId('zone-full').getByTestId('file-zone-hint')).toHaveText('بلغ العدد حدَّه: ٣');
    await expect(zones.getByTestId('zone-photo').getByTestId('file-zone-hint')).toHaveText('ملف صورة · حتى ٢ ميجابايت');

    const cards = page.getByTestId('files-cards').locator('scs-file-card');
    await expect(line(cards.nth(0))).toHaveText('٢٫٤ ميجابايت · ٢١ أغسطس ٢٠٢٦');
    await expect(line(cards.nth(1))).toHaveText('جارٍ الرفع… ٦٤٪');
    await expect(line(cards.nth(2))).toHaveText('فشل الرفع — أعد المحاولة');
    await expect(page.getByTestId('files-cards').locator('[data-state="being-checked"]').getByTestId('file-line')).toHaveText('قيد الفحص');
    await expect(page.getByTestId('files-cards').locator('[data-state="refused"]').getByTestId('file-line')).toHaveText('مرفوض: لم يجتز الفحص الأمني');
    await expect(page.getByTestId('files-cards').locator('[data-state="not-kept"]').getByTestId('file-line')).toHaveText('لم يعد محفوظًا');

    // A removed file is set apart in red, dashed so it is not taken for a refused one.
    const removed = page.getByTestId('files-cards').locator('[data-state="removed"]');
    const refused = page.getByTestId('files-cards').locator('[data-state="refused"]');
    await expect(removed).toHaveCSS('border-top-style', 'dashed');
    await expect(refused).toHaveCSS('border-top-style', 'solid');
    expect(await removed.evaluate((card) => getComputedStyle(card).borderTopColor)).toBe(
      await refused.evaluate((card) => getComputedStyle(card).borderTopColor),
    );
    await expect(removed.getByTestId('file-name')).toHaveCSS('text-decoration-line', 'line-through');

    // The zone is the design's width, and the card's.
    expect(await zones.getByTestId('zone-default').boundingBox()).toMatchObject({ width: 320, height: 162 });
    expect(await zones.getByTestId('zone-uploading').boundingBox()).toMatchObject({ width: 320, height: 178 });
    expect((await cards.nth(0).boundingBox())!.width).toBe(320);

    await zones.scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'ar', 'zones');
    await page.getByTestId('files-cards').scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'ar', 'cards');
  });

  test('in English, with Western digits', async ({ page }) => {
    await useEnglish(page);
    await open(page);
    const zones = page.getByTestId('files-zones');

    await expect(zones.getByTestId('zone-default').getByTestId('file-zone-title')).toHaveText('Drag a file here or click to browse');
    await expect(zones.getByTestId('zone-default').getByTestId('file-zone-hint')).toHaveText('PDF or image · up to 10 MB');
    await expect(zones.getByTestId('zone-uploading').getByTestId('file-zone-title')).toHaveText('Uploading the file…');
    await expect(zones.getByTestId('zone-error').getByTestId('file-zone-title')).toHaveText('The file could not be uploaded');
    await expect(zones.getByTestId('zone-error').getByTestId('file-zone-hint')).toHaveText('Larger than 10 MB — choose a smaller file');
    await expect(zones.getByTestId('zone-full').getByTestId('file-zone-hint')).toHaveText('The limit is reached: 3');

    const cards = page.getByTestId('files-cards').locator('scs-file-card');
    await expect(line(cards.nth(0))).toHaveText('2.4 MB · 21 Aug 2026');
    await expect(line(cards.nth(1))).toHaveText('Uploading… 64%');
    await expect(line(cards.nth(2))).toHaveText('Upload failed — try again');

    // A long name is shortened by the card, never wrapping it.
    const long = cards.last();
    expect((await long.boundingBox())!.height).toBeLessThanOrEqual(74);

    await zones.scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'en', 'zones');
    await page.getByTestId('files-cards').scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'en', 'cards');
  });

  test('the zone is highlighted while a file is dragged over it', async ({ page }) => {
    await open(page);
    const target = page.getByTestId('zone-default');

    const transfer = await page.evaluateHandle(() => new DataTransfer());
    await target.getByTestId('file-zone').dispatchEvent('dragenter', { dataTransfer: transfer });
    await expect(target).toHaveAttribute('data-state', 'drag');
    await page.getByTestId('files-zones').scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'ar', 'zone-drag');

    await target.getByTestId('file-zone').dispatchEvent('dragleave', { dataTransfer: transfer });
    await expect(target).toHaveAttribute('data-state', 'default');
  });
});

test.describe('attaching files on the builders’ page', () => {
  test('a chosen file uploads at once with its progress, and saving waits for it', async ({ page }) => {
    const platform = await open(page, { uploadDelayMs: 900 });
    await expect(page.getByTestId('files-none')).toHaveText('لا ملفات محفوظة بعد');
    await expect(page.getByTestId('files-save')).toBeDisabled();

    await chooser(page).setInputFiles(pdf('تقرير المعمل.pdf'));

    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toHaveAttribute('data-state', 'uploading');
    await expect(zone(page)).toHaveAttribute('data-state', 'uploading');
    await expect(page.getByTestId('files-save')).toBeDisabled();

    await expect(rows(page).first()).toHaveAttribute('data-state', 'ready');
    await expect(rows(page).first().getByTestId('file-name')).toHaveText('تقرير المعمل.pdf');
    await expect(line(rows(page).first())).toContainText('كيلوبايت');
    await expect(live(page).getByTestId('file-said')).toHaveText('رُفع تقرير المعمل.pdf');
    await expect(zone(page)).toHaveAttribute('data-state', 'default');

    await page.getByTestId('files-save').click();

    await expect(rows(page)).toHaveCount(0);
    await expect(savedCards(page)).toHaveCount(1);
    await expect(savedCards(page).first()).toHaveAttribute('data-state', 'default');
    expect(platform.fileCalls).toContain('POST /v1/dev/files/sample');
    expect(platform.files.map((file) => file.name)).toEqual(['تقرير المعمل.pdf']);
  });

  test('a file dropped on the zone is taken the same way', async ({ page }) => {
    await open(page);
    const transfer = await page.evaluateHandle(() => {
      const data = new DataTransfer();
      data.items.add(new File([new Uint8Array(2048)], 'dropped.png', { type: 'image/png' }));
      return data;
    });

    await live(page).getByTestId('file-zone').dispatchEvent('drop', { dataTransfer: transfer });

    await expect(rows(page).first()).toHaveAttribute('data-state', 'ready');
    await expect(rows(page).first()).toHaveAttribute('data-type', 'image');
  });

  test('a file that is too large is refused by name and reason, and the zone is ready again', async ({ page }) => {
    const platform = await open(page);

    await chooser(page).setInputFiles(pdf('كبير.pdf', 11 * MB));

    await expect(rows(page).first()).toHaveAttribute('data-state', 'error');
    await expect(line(rows(page).first())).toHaveText('الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    await expect(zone(page)).toHaveAttribute('data-state', 'error');
    await expect(live(page).getByTestId('file-said')).toHaveText('لم يُرفع كبير.pdf: الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    await expect(rows(page).first().getByTestId('file-retry')).toHaveCount(0);
    expect(platform.fileCalls.filter((call) => call === 'POST /v1/files')).toHaveLength(0);

    // The next file goes through; the refused one stays listed until taken out.
    await chooser(page).setInputFiles(pdf('صغير.pdf'));
    await expect(rows(page).nth(1)).toHaveAttribute('data-state', 'ready');
    await rows(page).first().getByTestId('file-remove').click();
    await expect(rows(page)).toHaveCount(1);
  });

  test('what the platform refuses is said too, and does not stop the others', async ({ page }) => {
    await useEnglish(page);
    await open(page, {
      uploadAnswer: (name) =>
        name === 'renamed.pdf' ? problem(400, 'FILE_TYPE_NOT_ACCEPTED', { accepted: ['pdf', 'png', 'jpeg'] }) : null,
    });

    await chooser(page).setInputFiles([pdf('renamed.pdf'), pdf('real.pdf')]);

    await expect(rows(page).nth(0)).toHaveAttribute('data-state', 'error');
    await expect(line(rows(page).nth(0))).toHaveText('This type of file is not accepted — PDF or image only');
    await expect(rows(page).nth(1)).toHaveAttribute('data-state', 'ready');
    await expect(page.getByTestId('files-save')).toBeEnabled();
  });

  test('three files and no more: the zone says the limit, and taking one out frees it', async ({ page }) => {
    const platform = await open(page);

    await chooser(page).setInputFiles([pdf('a.pdf'), pdf('b.pdf'), pdf('c.pdf')]);

    await expect(rows(page)).toHaveCount(3);
    await expect(zone(page)).toHaveAttribute('data-state', 'full');
    await expect(zone(page).getByTestId('file-zone-hint')).toHaveText('بلغ العدد حدَّه: ٣');
    await expect(zone(page).getByTestId('file-zone')).toHaveAttribute('aria-disabled', 'true');

    await rows(page).first().getByTestId('file-remove').click();

    await expect(rows(page)).toHaveCount(2);
    await expect(zone(page)).toHaveAttribute('data-state', 'default');
    await expect.poll(() => platform.fileCalls.filter((call) => call.startsWith('DELETE /v1/files/'))).toHaveLength(1);
  });

  test('a failed upload offers another try, and the try can succeed', async ({ page }) => {
    let fail = true;
    await open(page, { uploadAnswer: () => (fail ? problem(503, 'FILE_NOT_STORED') : null) });

    await chooser(page).setInputFiles(pdf('scan.pdf'));

    await expect(rows(page).first()).toHaveAttribute('data-state', 'error');
    await expect(line(rows(page).first())).toHaveText('فشل الرفع — أعد المحاولة');

    fail = false;
    await rows(page).first().getByTestId('file-retry').click();

    await expect(rows(page).first()).toHaveAttribute('data-state', 'ready');
  });

  test('with no connection a chosen file is not lost: it reads as not uploaded and is tried again when the connection returns', async ({ page, context }) => {
    const platform = await open(page);
    await context.setOffline(true);

    await chooser(page).setInputFiles(pdf('scan.pdf'));

    await expect(rows(page).first()).toHaveAttribute('data-state', 'error');
    await expect(line(rows(page).first())).toHaveText('لا اتصال — لم يُرفع الملف');
    await expect(rows(page).first().getByTestId('file-retry')).toBeVisible();
    expect(platform.fileCalls.filter((call) => call === 'POST /v1/files')).toHaveLength(0);

    // When the connection returns the console reads what the person may do again, and leaves
    // a page that holds a file not yet saved as it is (spec 006, FR-021): the row is still there.
    const renewed = page.waitForResponse((response) => response.url().includes('/v1/auth/session'));
    await context.setOffline(false);
    await renewed;
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toHaveAttribute('data-state', 'error');

    await rows(page).first().getByTestId('file-retry').click();
    await expect(rows(page).first()).toHaveAttribute('data-state', 'ready');
    expect(platform.fileCalls.filter((call) => call === 'POST /v1/files')).toHaveLength(1);

    // Once nothing unsaved is left, the page is read again as before.
    await rows(page).first().getByTestId('file-remove').click();
    await expect(rows(page)).toHaveCount(0);
    await context.setOffline(true);
    await context.setOffline(false);
    await expect(page.getByTestId('files-live')).toBeVisible();
  });

  test('with the keyboard alone: reach the zone, choose, and take the file out', async ({ page }) => {
    await open(page);
    const target = live(page).getByTestId('file-zone');

    await target.focus();
    await expect(target).toBeFocused();
    const choosing = page.waitForEvent('filechooser');
    await page.keyboard.press('Enter');
    await (await choosing).setFiles(pdf('scan.pdf'));

    await expect(rows(page).first()).toHaveAttribute('data-state', 'ready');
    await expect(live(page).getByTestId('file-said')).toHaveText('رُفع scan.pdf');

    const remove = rows(page).first().getByTestId('file-remove');
    await expect(remove).toHaveAttribute('aria-label', 'إزالة scan.pdf');
    await remove.focus();
    await page.keyboard.press('Enter');

    await expect(rows(page)).toHaveCount(0);
    await expect(live(page).getByTestId('file-said')).toHaveText('أُزيل scan.pdf');

    // Space opens the chooser as Enter does.
    await target.focus();
    const again = page.waitForEvent('filechooser');
    await page.keyboard.press('Space');
    await again;
  });
});

test.describe('files that are saved', () => {
  test('one that is available downloads through a way in', async ({ page }) => {
    const platform = await open(page, { files: [saved('f-1')] });

    await expect(line(savedCards(page).first())).toHaveText('٢٫٤ ميجابايت · ٢١ أغسطس ٢٠٢٦');
    const download = page.waitForEvent('download');
    await savedCards(page).first().getByTestId('file-download').click();

    expect((await download).suggestedFilename()).toBe('ملف-الأشعة.pdf');
    expect(platform.fileCalls).toEqual(expect.arrayContaining(['POST /v1/files/f-1/access', 'GET /v1/files/content/way-in-f-1']));
  });

  test('one being checked, refused, behind a password or no longer kept says so, and offers no download', async ({ page }) => {
    await useEnglish(page);
    await open(page, {
      files: [
        saved('f-1', { state: 'being-checked' }),
        saved('f-2', { state: 'refused', refusedFor: 'infected' }),
        saved('f-3', { state: 'refused', refusedFor: 'unreadable' }),
        saved('f-4', { state: 'not-kept' }),
      ],
    });

    await expect(savedCards(page).locator('[data-testid="file-line"]')).toHaveText([
      'Being checked',
      'Refused: it did not pass the security check',
      'Refused: it could not be checked — to be sent again without a password',
      'No longer kept',
    ]);
    await expect(savedCards(page).getByTestId('file-download')).toHaveCount(0);
    await page.getByTestId('files-live').scrollIntoViewIfNeeded();
    await pictureFor(page, 'files', 'en', 'saved-states');
  });

  test('a file found unavailable when asked for says why', async ({ page }) => {
    const platform = await open(page, { files: [saved('f-1')] });
    platform.accessAnswer = problem(409, 'FILE_BEING_CHECKED');

    await savedCards(page).first().getByTestId('file-download').click();

    await expect(page.getByTestId('files-problem')).toHaveText('قيد الفحص');
  });

  test('"more" removes a file, and a removed one can be put back', async ({ page }) => {
    const platform = await open(page, { files: [saved('f-1')] });
    const card = savedCards(page).first();

    await card.getByTestId('file-more').click();
    await expect(card.getByRole('menuitem')).toHaveText(['إزالة']);
    await card.getByTestId('file-action-remove').click();

    await expect(card).toHaveAttribute('data-state', 'removed');
    await expect(line(card)).toContainText('أُزيل · يُستعاد حتى ');
    await expect(card.getByTestId('file-download')).toHaveCount(0);

    await card.getByTestId('file-more').click();
    await card.getByTestId('file-action-restore').click();

    await expect(card).toHaveAttribute('data-state', 'default');
    expect(platform.fileCalls).toEqual(
      expect.arrayContaining(['POST /v1/dev/files/sample/f-1/remove', 'POST /v1/dev/files/sample/f-1/restore']),
    );

    // Escape closes the menu without choosing.
    await card.getByTestId('file-more').click();
    await page.keyboard.press('Escape');
    await expect(card.getByTestId('file-menu')).toHaveCount(0);
  });

  test('what the record already holds counts against the limit', async ({ page }) => {
    await open(page, { files: [saved('f-1'), saved('f-2'), saved('f-3', { state: 'removed', restorableUntil: '2026-11-10T00:00:00Z' })] });

    await chooser(page).setInputFiles([pdf('a.pdf'), pdf('b.pdf')]);

    await expect(rows(page).nth(0)).toHaveAttribute('data-state', 'ready');
    await expect(rows(page).nth(1)).toHaveAttribute('data-state', 'error');
    await expect(line(rows(page).nth(1))).toHaveText('بلغ العدد حدَّه: ٣');
    await pictureFor(page, 'files', 'ar', 'live');
  });
});
