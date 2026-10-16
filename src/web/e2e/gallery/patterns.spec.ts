import { Page, expect, test } from '@playwright/test';
import { openSection } from './gallery-platform';

/**
 * The five patterns' working examples (spec 095, US6, T059–T063): each does what its page says, by
 * pointer and by keyboard, in both languages.
 */
const live = (page: Page, id: string) => page.getByTestId(`live-pattern-${id}`);

for (const language of ['ar', 'en'] as const) {
  const ar = language === 'ar';

  test.describe(`patterns — ${language}`, () => {
    test('search & filter: the panel offers only what has rows; applying shows the count and a chip each', async ({ page }) => {
      await openSection(page, 'pattern-search-filter', language);
      const area = live(page, 'search-filter');
      await expect(page.getByTestId('live-pattern-row')).toHaveCount(4);
      await area.getByTestId('list-filter').click();
      // No clinic is on trial, so "trial" is not offered (CL-SH-050).
      await expect(page.getByTestId('live-filter-trial')).toHaveCount(0);
      await page.getByTestId('live-filter-suspended').check();
      await page.getByTestId('filter-popover').getByTestId('filter-apply').click();
      await expect(page.getByTestId('live-pattern-row')).toHaveCount(1);
      await expect(area.getByTestId('list-filter-count')).toHaveText('1');
      await expect(area.getByTestId('applied-chip')).toHaveText([ar ? 'الحالة: موقوفة' : 'Status: Suspended']);
      await area.getByTestId('applied-chip').getByTestId('tag-remove').click();
      await expect(page.getByTestId('live-pattern-row')).toHaveCount(4);
      await area.getByTestId('list-search').fill('zzzz');
      await expect(page.getByTestId('live-pattern-none')).toBeVisible();
    });

    test('page states: each state is chosen by the arrows and shown in the card', async ({ page }) => {
      await openSection(page, 'pattern-page-states', language);
      const chosen = page.getByTestId('live-page-state').locator('[aria-checked="true"]');
      await chosen.focus();
      await page.keyboard.press(ar ? 'ArrowLeft' : 'ArrowRight');
      await expect(page.getByTestId('live-page-state').locator('[aria-checked="true"]')).toHaveText(ar ? 'نجاح' : 'Success');
      await expect(live(page, 'page-states')).toContainText(ar ? 'تم بنجاح' : 'Success');
    });

    test('confirmation: the button names the action; Escape cancels and the focus comes back', async ({ page }) => {
      await openSection(page, 'pattern-confirmation', language);
      const opener = page.getByTestId('live-open-delete');
      await opener.click();
      const dialog = live(page, 'confirmation').getByRole('dialog', { name: ar ? 'حذف العيادة نهائياً' : 'Permanently Delete Clinic' });
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole('button', { name: ar ? 'حذف نهائياً' : 'Delete Permanently' })).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(dialog).toBeHidden();
      await expect(opener).toBeFocused();
      await opener.click();
      await dialog.getByRole('button', { name: ar ? 'حذف نهائياً' : 'Delete Permanently' }).click();
      await expect(page.getByTestId('live-confirm-said')).not.toBeEmpty();
    });

    test('multi-step form: "next" checks the step shown and the last step reviews it', async ({ page }) => {
      await openSection(page, 'pattern-multi-step-form', language);
      await page.getByTestId('live-step-next').click();
      await expect(page.getByTestId('live-step-name')).toHaveAttribute('aria-invalid', 'true');
      await page.getByTestId('live-step-name').fill(ar ? 'عيادة النور' : 'Al Noor Clinic');
      await page.getByTestId('live-step-next').click();
      await page.getByTestId('live-step-phone').fill('+20 10 1234 5678');
      await page.getByTestId('live-step-next').click();
      await expect(page.getByTestId('live-step-review')).toContainText(ar ? 'عيادة النور' : 'Al Noor Clinic');
      await expect(page.getByTestId('live-step-review')).toContainText('+20 10 1234 5678');
      await page.getByTestId('live-step-back').click();
      await expect(page.getByTestId('live-step-phone')).toHaveValue('+20 10 1234 5678');
    });

    test('onboarding: next walks the cards, the last says "get started"; skip ends it', async ({ page }) => {
      await openSection(page, 'pattern-onboarding', language);
      await page.getByTestId('live-onboarding-next').click();
      await page.getByTestId('live-onboarding-next').click();
      await expect(page.getByTestId('live-onboarding-next')).toHaveText(ar ? 'ابدأ الآن' : 'Get Started');
      await expect(page.getByTestId('live-onboarding-skip')).toHaveCount(0);
      await page.getByTestId('live-onboarding-next').click();
      await expect(page.getByTestId('live-onboarding-done')).toBeVisible();
      await page.getByTestId('live-onboarding-again').click();
      await page.getByTestId('live-onboarding-skip').click();
      await expect(page.getByTestId('live-onboarding-done')).toBeVisible();
    });
  });
}
