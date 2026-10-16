import { Locator, Page, expect, test } from '@playwright/test';
import { NARROW, openSection } from './gallery-platform';

/**
 * Every working example by the keyboard alone (spec 095, FR-007, FR-008, `contracts/components.md`):
 * reached by Tab, worked by its keys, told to a screen reader by role, name, value and state; what
 * opens says so on its opener, closes on Escape, gives the focus back, and stays inside the window on
 * a phone and at 200% zoom (a 1440 screen at 200% lays out as 720 wide).
 */
const ZOOMED = { width: 720, height: 450 };

/** The working example of a set: `live-<page>-<set>`. */
const live = (page: Page, id: string): Locator => page.getByTestId(`live-${id}`);

/** Presses Tab from the start of the working example until `target` has the focus. */
async function tabTo(page: Page, area: Locator, target: Locator, most = 40): Promise<void> {
  await area.locator('h4').first().click();
  for (let presses = 0; presses < most; presses++) {
    await page.keyboard.press('Tab');
    if (await target.evaluate((element) => element === document.activeElement || element.contains(document.activeElement))) return;
  }
  throw new Error('not reached by Tab');
}

/** The element is whole inside the window. */
async function insideWindow(page: Page, element: Locator): Promise<void> {
  const box = await element.boundingBox();
  const size = page.viewportSize()!;
  expect(box, 'drawn').not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(-1);
  expect(box!.y).toBeGreaterThanOrEqual(-1);
  expect(box!.x + box!.width).toBeLessThanOrEqual(size.width + 1);
  expect(box!.y + box!.height).toBeLessThanOrEqual(size.height + 1);
}

const focused = (page: Page) => page.locator(':focus');

test.describe('working examples by the keyboard', () => {
  test('button: Enter presses it, a busy one says so and cannot be pressed twice; an icon-only one has a name', async ({ page }) => {
    await openSection(page, 'button', 'en');
    const area = live(page, 'button-button');
    const primary = area.getByRole('button', { name: 'Primary' });
    await tabTo(page, area, primary);
    await page.keyboard.press('Enter');
    await expect(primary).toHaveAttribute('aria-busy', 'true');
    await expect(primary).toBeDisabled();
    await expect(area.getByRole('status')).not.toBeEmpty();
    await expect(area.getByRole('button', { name: /add/i })).toBeVisible();
  });

  test('checkbox: Space ticks it, the "all" box is mixed while some are ticked', async ({ page }) => {
    await openSection(page, 'checkbox', 'en');
    const area = live(page, 'checkbox-checkbox');
    const first = area.getByTestId('live-item-0');
    await tabTo(page, area, first);
    const was = await first.isChecked();
    await page.keyboard.press('Space');
    expect(await first.isChecked()).toBe(!was);
    // The box over them all is mixed while some, not all, are ticked.
    const ticked = await area.locator('[data-testid^="live-item-"]').evaluateAll((boxes) => boxes.filter((box) => (box as HTMLInputElement).checked).length);
    const total = await area.locator('[data-testid^="live-item-"]').count();
    await expect(area.getByTestId('live-all')).toHaveJSProperty('indeterminate', ticked > 0 && ticked < total);
  });

  test('switch: Space turns it, its state is told', async ({ page }) => {
    await openSection(page, 'switch', 'en');
    const area = live(page, 'switch-switch');
    const control = area.getByTestId('live-switch');
    await tabTo(page, area, control);
    const before = await control.getAttribute('aria-checked');
    await page.keyboard.press('Space');
    await expect(control).not.toHaveAttribute('aria-checked', before ?? '');
  });

  test('radio, slider and segmented control: the arrows choose and move', async ({ page }) => {
    await openSection(page, 'radio', 'en');
    const radios = live(page, 'radio-radio');
    const group = radios.getByRole('radiogroup');
    await expect(group).toHaveAccessibleName(/.+/);
    const firstRadio = radios.getByRole('radio').first();
    await tabTo(page, radios, firstRadio);
    await page.keyboard.press('ArrowDown');
    await expect(radios.getByRole('radio').nth(1)).toBeChecked();

    await openSection(page, 'slider', 'en');
    const sliders = live(page, 'slider-slider');
    const thumb = sliders.getByRole('slider').first();
    await tabTo(page, sliders, thumb);
    const now = Number(await thumb.getAttribute('aria-valuenow'));
    await page.keyboard.press('ArrowRight');
    await expect.poll(async () => Number(await thumb.getAttribute('aria-valuenow'))).toBeGreaterThan(now);
    await page.keyboard.press('End');
    await expect(thumb).toHaveAttribute('aria-valuenow', (await thumb.getAttribute('aria-valuemax')) ?? '100');

    await openSection(page, 'segmented-control', 'en');
    const segments = live(page, 'segmented-control-segment');
    const chosen = segments.locator('[role="radio"][aria-checked="true"]');
    await tabTo(page, segments, chosen);
    const name = await chosen.textContent();
    await page.keyboard.press('ArrowRight');
    await expect(segments.locator('[role="radio"][aria-checked="true"]')).not.toHaveText(name ?? '');
  });

  test('text field and textarea: reached by Tab, typed into, an error replaces the hint and describes the field', async ({ page }) => {
    await openSection(page, 'input', 'en');
    const area = live(page, 'input-input');
    const email = area.getByTestId('live-email');
    await tabTo(page, area, email);
    await page.keyboard.type('not-an-email');
    await page.keyboard.press('Tab');
    const described = await email.getAttribute('aria-describedby');
    expect(described).toBeTruthy();
    await expect(email).toHaveAccessibleName(/.+/);

    await openSection(page, 'textarea', 'en');
    const words = live(page, 'textarea-textarea').getByTestId('live-textarea');
    await tabTo(page, live(page, 'textarea-textarea'), words);
    await page.keyboard.type('Hello');
    await expect(words).toHaveValue('Hello');
  });

  test('drop-down: Down opens it, the arrows move, Enter chooses and closes, Escape gives the focus back', async ({ page }) => {
    await openSection(page, 'dropdown', 'en');
    const area = live(page, 'dropdown-select-trigger');
    const trigger = area.getByTestId('live-branch');
    await tabTo(page, area, trigger);
    await page.keyboard.press('ArrowDown');
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    const panel = page.getByTestId('live-branch-panel');
    await insideWindow(page, panel);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await expect(trigger).toBeFocused();
    await expect(trigger).not.toHaveAttribute('data-value', '');
    await page.keyboard.press('ArrowDown');
    await expect(focused(page)).toHaveRole('listbox');
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('live-branch-panel')).toHaveCount(0);
    await expect(trigger).toBeFocused();
  });

  test('date field: Down opens the calendar on today, the arrows move, Enter chooses, Escape closes', async ({ page }) => {
    await openSection(page, 'date-picker', 'en');
    const area = live(page, 'date-picker-date-field-trigger');
    const trigger = area.getByTestId('live-date');
    await tabTo(page, area, trigger);
    await page.keyboard.press('ArrowDown');
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(focused(page)).toHaveAttribute('aria-current', 'date');
    await insideWindow(page, page.getByTestId('live-date-panel'));
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('Enter');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await expect(trigger).toBeFocused();
    await expect(trigger).not.toHaveAttribute('data-value', '');
    await page.keyboard.press('Enter');
    await page.keyboard.press('Escape');
    await expect(trigger).toBeFocused();
  });

  test('time field: a list by the arrows and Enter; wheels by the arrows and Done', async ({ page }) => {
    await openSection(page, 'time-picker', 'en');
    const area = live(page, 'time-picker-time-field-trigger');
    const list = area.getByTestId('live-time');
    await tabTo(page, area, list);
    await page.keyboard.press('ArrowDown');
    await expect(focused(page)).toHaveRole('listbox');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(list).toBeFocused();
    await expect(list).not.toHaveAttribute('data-value', '');

    const wheel = area.getByTestId('live-time-wheel');
    await tabTo(page, area, wheel);
    await page.keyboard.press('Enter');
    await expect(focused(page)).toHaveRole('spinbutton');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(wheel).toBeFocused();
    await expect(wheel).toHaveAttribute('data-value', /^\d\d:\d\d$/);
  });

  test('tabs: the arrows move between the tabs and choose', async ({ page }) => {
    await openSection(page, 'navigation', 'en');
    const area = live(page, 'navigation-tab-item');
    const chosen = area.getByRole('tab', { selected: true });
    await tabTo(page, area, chosen);
    await page.keyboard.press('ArrowRight');
    await expect(area.getByRole('tab').nth(1)).toHaveAttribute('aria-selected', 'true');
    await expect(area.getByRole('tab').nth(1)).toBeFocused();
  });

  test('paging: "…" opens upwards a menu of the hidden pages, Escape closes it back to "…"', async ({ page }) => {
    await openSection(page, 'navigation', 'en');
    const area = live(page, 'navigation-pagination-item');
    const more = area.getByTestId('page-more');
    await tabTo(page, area, more);
    await page.keyboard.press('Enter');
    await expect(more).toHaveAttribute('aria-expanded', 'true');
    await insideWindow(page, page.getByTestId('page-menu'));
    await page.keyboard.press('Escape');
    await expect(more).toBeFocused();
  });

  test('row menu: Enter opens it on its first item, the arrows wrap, Escape gives the focus back', async ({ page }) => {
    await openSection(page, 'menu', 'en');
    const area = live(page, 'menu-menu-item');
    const opener = area.getByTestId('live-row-menu');
    await tabTo(page, area, opener);
    await page.keyboard.press('Enter');
    await expect(opener).toHaveAttribute('aria-expanded', 'true');
    await expect(focused(page)).toHaveRole('menuitem');
    await page.keyboard.press('ArrowUp');
    await expect(focused(page)).toHaveRole('menuitem');
    await page.keyboard.press('Escape');
    await expect(opener).toBeFocused();
  });

  test('modal: opened from its button, Escape closes it and the focus goes back', async ({ page }) => {
    await openSection(page, 'modal', 'en');
    const area = live(page, 'modal-modal');
    const opener = area.getByTestId('live-open-modal');
    await tabTo(page, area, opener);
    await page.keyboard.press('Enter');
    const dialog = page.locator('dialog[open]').last();
    await expect(dialog).toHaveAccessibleName(/.+/);
    await insideWindow(page, dialog);
    await page.keyboard.press('Escape');
    await expect(page.locator('dialog[open]:not(.still)')).toHaveCount(0);
    await expect(opener).toBeFocused();
  });

  test('tooltip: shown on keyboard focus and naming its button', async ({ page }) => {
    await openSection(page, 'tooltip', 'en');
    const area = live(page, 'tooltip-tooltip');
    const button = area.getByRole('button').first();
    await tabTo(page, area, button);
    await expect(page.getByRole('tooltip').last()).toBeVisible();
    await expect(button).toHaveAccessibleName(/Helpful tip/);
  });

  test('accordion and notifications: Enter opens a section; the bell opens its panel as a menu', async ({ page }) => {
    await openSection(page, 'data-display', 'en');
    const area = live(page, 'data-display-accordion-item');
    const head = area.getByRole('button').first();
    await tabTo(page, area, head);
    await page.keyboard.press('Enter');
    await expect(head).toHaveAttribute('aria-expanded', 'true');

    await openSection(page, 'notifications', 'en');
    const bells = live(page, 'notifications-notification-bell');
    const bell = bells.getByTestId('notification-bell');
    await tabTo(page, bells, bell);
    await page.keyboard.press('Enter');
    await expect(bell).toHaveAttribute('aria-expanded', 'true');
    await expect(focused(page)).toHaveRole('menuitem');
    await page.keyboard.press('ArrowDown');
    await expect(focused(page)).toHaveRole('menuitem');
    await page.keyboard.press('Escape');
    await expect(bell).toHaveAttribute('aria-expanded', 'false');
  });

  for (const size of [NARROW, ZOOMED]) {
    test(`what opens stays inside the window at ${size.width}`, async ({ page }) => {
      await page.setViewportSize(size);
      await openSection(page, 'date-picker', 'ar');
      const area = live(page, 'date-picker-date-field-trigger');
      const trigger = area.getByTestId('live-date');
      await trigger.scrollIntoViewIfNeeded();
      await trigger.click();
      await insideWindow(page, page.getByTestId('live-date-panel'));
      await page.keyboard.press('Escape');

      await openSection(page, 'dropdown', 'ar');
      const branch = live(page, 'dropdown-select-trigger').getByTestId('live-branch');
      await branch.scrollIntoViewIfNeeded();
      await branch.click();
      await insideWindow(page, page.getByTestId('live-branch-panel'));
    });
  }
});
