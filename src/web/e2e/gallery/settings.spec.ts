import { Page, expect, test } from '@playwright/test';
import { GALLERY_SECTIONS } from '../../projects/ops/src/app/gallery/gallery-sections';
import { NARROW, openSection } from './gallery-platform';

/**
 * The states that are settings (spec 095, User Story 2): what a page sets on a component shows as the
 * design draws it and is told to a screen reader (FR-009); long words stay whole; with reduced motion
 * nothing more than a state change moves (FR-013); every pressable control is 40 by 40 on a phone
 * (FR-012).
 */
const live = (page: Page, id: string) => page.getByTestId(`live-${id}`);

test.describe('states that are settings', () => {
  test('a field’s error takes the place of its hint and is its description', async ({ page }) => {
    await openSection(page, 'input', 'en');
    const email = live(page, 'input-input').getByTestId('live-email');
    const hintId = await email.getAttribute('aria-describedby');
    expect(hintId).toBeTruthy();
    await email.fill('not-an-email');
    await email.blur();
    const errorId = await email.getAttribute('aria-describedby');
    const message = page.locator(`[id="${errorId}"]`);
    await expect(message).toBeVisible();
    await expect(email).toHaveAttribute('aria-invalid', 'true');
    await expect(page.locator(`[id="${hintId}"]`)).toHaveCount(errorId === hintId ? 1 : 0);
  });

  test('a busy button keeps its width, cannot be pressed twice and says it is busy', async ({ page }) => {
    await openSection(page, 'button', 'en');
    const primary = live(page, 'button-button').getByRole('button', { name: 'Primary' });
    const before = (await primary.boundingBox())!.width;
    await primary.click();
    await expect(primary).toHaveAttribute('aria-busy', 'true');
    await expect(primary).toBeDisabled();
    expect(Math.abs((await primary.boundingBox())!.width - before)).toBeLessThan(1);
  });

  test('a searchable list says when nothing is found', async ({ page }) => {
    await openSection(page, 'dropdown', 'en');
    await live(page, 'dropdown-select-trigger').getByTestId('live-clinic').click();
    await page.getByTestId('live-clinic-search').fill('zzzz');
    await expect(page.getByTestId('live-clinic-none')).toHaveText('No results found');
  });

  for (const language of ['en', 'ar'] as const) {
    test(`a range is chosen in the calendar, start then end, and a start before it begins again (${language})`, async ({ page }) => {
      await openSection(page, 'date-picker', language);
      const calendar = live(page, 'date-picker-date-field-trigger').getByTestId('live-range');
      const days = calendar.locator('button.day');
      await days.nth(9).click();
      await days.nth(14).click();
      await expect(calendar.locator('[data-band="start"]')).toHaveCount(1);
      await expect(calendar.locator('[data-band="end"]')).toHaveCount(1);
      await expect(calendar.locator('[data-band="middle"]')).toHaveCount(4);
      await days.nth(3).click();
      await expect(calendar.locator('[data-band]')).toHaveCount(0);
      await expect(days.nth(3)).toHaveClass(/chosen/);
    });
  }

  test('a choice that may not be made shows its reason, and it is read with it', async ({ page }) => {
    await openSection(page, 'menu', 'en');
    await live(page, 'menu-menu-item').getByTestId('live-row-menu').click();
    const refused = page.locator('[role="menuitem"][aria-disabled="true"]');
    await expect(refused).toBeVisible();
    await expect(refused).toHaveAccessibleName(/Audit log.*Needs reports\.view/);
  });

  for (const language of ['en', 'ar'] as const) {
    test(`long words stay whole: they wrap or end with an ellipsis inside their part (${language})`, async ({ page }) => {
      const long =
        language === 'ar'
          ? 'اسم فرع طويل جداً لعيادة يتجاوز عرض أي حقل أو عمود أو قائمة في الصفحة'
          : 'A very long clinic branch name that goes on well past the width of any field';

      // A field keeps what is typed whole and inside its box.
      await openSection(page, 'input', language);
      const email = live(page, 'input-input').getByTestId('live-email');
      await email.fill(long);
      await expect(email).toHaveValue(long);
      expect(await wordsInside(email.locator('xpath=ancestor::*[contains(@class,"box")][1]'), 'input')).toEqual([]);

      // A drop-down's trigger ends with an ellipsis; its list wraps inside the panel.
      await openSection(page, 'dropdown', language);
      const trigger = live(page, 'dropdown-select-trigger').getByTestId('live-branch');
      await setWords(trigger.locator('.value'), long);
      expect(await trigger.locator('.value').evaluate((value) => getComputedStyle(value).textOverflow)).toBe('ellipsis');
      expect(await wordsInside(trigger, '.value')).toEqual([]);
      await live(page, 'dropdown-select-trigger').getByTestId('live-clinic').click();
      const option = page.locator('[role="option"]').first();
      await setWords(option.locator('.words > *').first(), long);
      await expect(option).toContainText(long);
      expect(await wordsInside(page.locator('[role="listbox"]'), '[role="option"]')).toEqual([]);
      await page.keyboard.press('Escape');

      // A tag.
      await openSection(page, 'data-display', language);
      const tag = live(page, 'data-display-tag').locator('scs-tag').first();
      await setWords(tag.locator('.words'), long);
      expect(await wordsInside(tag, '.words')).toEqual([]);

      // A table cell and the badge in it.
      await openSection(page, 'table', language);
      const row = live(page, 'table-table-row').locator('tbody tr').first();
      await setWords(row.locator('.who strong'), long);
      await setWords(row.locator('scs-badge .words'), long);
      await expect(row.locator('scs-badge')).toHaveText(long);
      expect(await wordsInside(row.locator('td').nth(1), 'strong')).toEqual([]);
      expect(await wordsInside(row.locator('td:has(scs-badge)'), 'scs-badge')).toEqual([]);
      expect(await row.locator('scs-badge .words').evaluate((words) => getComputedStyle(words).textOverflow)).toBe('ellipsis');

      // A menu item.
      await openSection(page, 'menu', language);
      await live(page, 'menu-menu-item').getByTestId('live-row-menu').click();
      const item = page.locator('[role="menuitem"]').first();
      await item.evaluate((element, said) => {
        const words = [...element.childNodes].find((node) => node.nodeType === Node.TEXT_NODE && node.textContent!.trim() !== '');
        words!.textContent = said;
      }, long);
      await expect(item).toContainText(long);
      expect(await wordsInside(page.locator('[role="menu"]'), '[role="menuitem"]')).toEqual([]);
    });
  }
});

/** Puts `words` in place of what an element says, as a long name from the platform would be. */
async function setWords(target: ReturnType<Page['locator']>, words: string): Promise<void> {
  await target.evaluate((element, said) => (element.textContent = said), words);
}

/** What inside `part` (matched by `inner`) reaches past the part's box, or spills out of its own. */
async function wordsInside(part: ReturnType<Page['locator']>, inner: string): Promise<string[]> {
  return part.first().evaluate((host, selector) => {
    const wrong: string[] = [];
    const box = host.getBoundingClientRect();
    for (const element of host.querySelectorAll<HTMLElement>(selector)) {
      const own = element.getBoundingClientRect();
      if (own.left < box.left - 1 || own.right > box.right + 1) wrong.push(`${selector} reaches past its part`);
      const style = getComputedStyle(element);
      if (element.scrollWidth > element.clientWidth + 1 && style.textOverflow !== 'ellipsis' && !(element instanceof HTMLInputElement)) {
        wrong.push(`${selector} spills out of its own box`);
      }
    }
    return wrong;
  }, inner);
}

test.describe('reduced motion', () => {
  test.use({ contextOptions: { reducedMotion: 'reduce' } });

  test('spinners turn slowly or stand still; shimmers, chevrons and the unknown progress do not move', async ({ page }) => {
    await openSection(page, 'loading', 'en');
    const spinnerDurations = await page.locator('scs-spinner svg').evaluateAll((svgs) =>
      svgs.map((svg) => parseFloat(getComputedStyle(svg).animationDuration) * (getComputedStyle(svg).animationName === 'none' ? 0 : 1)),
    );
    expect(spinnerDurations.length).toBeGreaterThan(0);
    for (const seconds of spinnerDurations) expect(seconds === 0 || seconds >= 2).toBe(true);
    const running = await page.locator('scs-progress .bar.running').evaluateAll((bars) => bars.map((bar) => getComputedStyle(bar).animationName));
    expect(running.every((name) => name === 'none')).toBe(true);
    const shapes = await page.locator('scs-skeleton span, scs-skeleton-block').evaluateAll((shapes) => shapes.map((shape) => getComputedStyle(shape).animationName));
    expect(shapes.every((name) => name === 'none')).toBe(true);

    await openSection(page, 'dropdown', 'en');
    const chevron = page.locator('scs-select .chevron').first();
    expect(await chevron.evaluate((element) => parseFloat(getComputedStyle(element).transitionDuration))).toBe(0);
  });
});

/** Every built section's working examples, on a phone. */
const built = GALLERY_SECTIONS.filter((section) => section.load && (section.kind === 'component' || section.kind === 'pattern'));

test.describe('controls on a phone', () => {
  for (const section of built) {
    test(`${section.title}: every pressable control of its working examples is at least 40 by 40 at 390`, async ({ page }) => {
      await page.setViewportSize(NARROW);
      await openSection(page, section.id, 'en');
      const small = await page.locator(`[data-testid="section-${section.id}"] [data-testid^="live-"]`).evaluateAll((areas) => {
        const found: string[] = [];
        const pressable = 'button, a[href], select, input:not([type="hidden"]), textarea, [role="button"], [role="tab"], [role="radio"], [role="slider"], [role="switch"], [role="option"], [role="menuitem"], [tabindex="0"]';
        for (const area of areas) {
          for (const element of area.querySelectorAll<HTMLElement>(pressable)) {
            const style = getComputedStyle(element);
            if (style.visibility === 'hidden' || style.display === 'none' || element.closest('[hidden], [aria-hidden="true"]')) continue;
            // A box drawn over a hidden native input is pressed through its label.
            // A field is pressed anywhere in the box drawn around it (as spec 011 measures it).
            const target = element.matches('input[type="checkbox"], input[type="radio"]')
              ? element.closest('label') ?? element
              : element.matches('input, textarea, select')
                ? element.closest('label.search, .box') ?? element
                : element;
            const box = target.getBoundingClientRect();
            if (box.width === 0 && box.height === 0) continue;
            if (box.width < 39.5 || box.height < 39.5) {
              const name = target.getAttribute('aria-label') ?? target.getAttribute('data-testid') ?? (target.textContent ?? '').trim().slice(0, 24);
              found.push(`${target.tagName.toLowerCase()} "${name}" ${Math.round(box.width)}x${Math.round(box.height)}`);
            }
          }
        }
        return found;
      });
      expect(small).toEqual([]);
    });
  }
});
