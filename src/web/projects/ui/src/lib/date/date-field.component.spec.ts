import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsDayRange } from './calendar';
import { SCS_CALENDAR_WEEK_START, ScsCalendarComponent } from './calendar.component';
import { ScsDateFieldComponent } from './date-field.component';

@Component({
  imports: [ScsDateFieldComponent],
  template: `
    <scs-date-field
      label="Date"
      placeholder="Select a date"
      testId="date"
      today="2026-08-28"
      [disabledDays]="['2026-08-05', '2026-08-06']"
      [(value)]="day"
    />
  `,
})
class FieldHost {
  readonly day = signal('');
}

@Component({
  imports: [ScsCalendarComponent],
  template: `<scs-calendar mode="range" testId="cal" today="2026-08-28" [(range)]="range" />`,
})
class RangeHost {
  readonly range = signal<ScsDayRange | null>({ start: '2026-08-10', end: null });
}

function setUp(language: 'ar' | 'en', extra: unknown[] = []) {
  TestBed.configureTestingModule({ imports: [uiTranslations(language)], providers: extra as never[] });
  TestBed.inject(LanguageService).use(language);
}

const press = (element: Element, key: string, shiftKey = false) =>
  element.dispatchEvent(new KeyboardEvent('keydown', { key, shiftKey, bubbles: true, cancelable: true }));
const byTest = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const settle = () => new Promise((done) => setTimeout(done));

/** The date field and its calendar (spec 095, `contracts/components.md`). */
describe('ScsDateFieldComponent', () => {
  afterEach(() => document.documentElement.removeAttribute('dir'));

  async function open(language: 'ar' | 'en' = 'en', extra: unknown[] = []) {
    setUp(language, extra);
    const fixture = TestBed.createComponent(FieldHost);
    fixture.detectChanges();
    const trigger = byTest('date') as HTMLButtonElement;
    press(trigger, 'ArrowDown');
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    return { fixture, trigger };
  }

  it('asks for a day, opens on Down with the focus on today, and names the panel', async () => {
    const { trigger } = await open();
    expect(trigger.getAttribute('aria-haspopup')).toBe('dialog');
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(byTest('date-panel')?.getAttribute('role')).toBe('dialog');
    expect(byTest('date-calendar-title')?.textContent?.trim()).toBe('August 2026');
    expect(document.activeElement).toBe(byTest('date-calendar-2026-08-28'));
    expect(byTest('date-calendar-2026-08-28')?.getAttribute('aria-current')).toBe('date');
    expect(byTest('date-calendar-2026-08-28')?.getAttribute('aria-label')).toBe('Friday, August 28, 2026');
  });

  it('moves by day, week, month and to the week edges, and chooses with Enter', async () => {
    const { fixture, trigger } = await open();
    const at = () => (document.activeElement as HTMLElement).dataset['day'];
    press(document.activeElement!, 'ArrowRight');
    fixture.detectChanges();
    await settle();
    expect(at()).toBe('2026-08-29');
    press(document.activeElement!, 'ArrowUp');
    fixture.detectChanges();
    await settle();
    expect(at()).toBe('2026-08-22');
    press(document.activeElement!, 'Home');
    fixture.detectChanges();
    await settle();
    // The clinic's week, the default, starts on Saturday.
    expect(at()).toBe('2026-08-22');
    press(document.activeElement!, 'End');
    fixture.detectChanges();
    await settle();
    expect(at()).toBe('2026-08-28');
    press(document.activeElement!, 'PageDown');
    fixture.detectChanges();
    await settle();
    expect(at()).toBe('2026-09-28');
    expect(byTest('date-calendar-title')?.textContent?.trim()).toBe('September 2026');
    press(document.activeElement!, 'Enter');
    fixture.detectChanges();
    await settle();
    expect(fixture.componentInstance.day()).toBe('2026-09-28');
    expect(byTest('date-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(trigger.textContent).toContain('Sep 28, 2026');
  });

  it('does not take a day that may not be chosen, and Escape closes it with the focus back', async () => {
    const { fixture, trigger } = await open();
    const fifth = byTest('date-calendar-2026-08-05')!;
    expect(fifth.getAttribute('aria-disabled')).toBe('true');
    fifth.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.day()).toBe('');
    press(document.activeElement!, 'Escape');
    fixture.detectChanges();
    await settle();
    expect(byTest('date-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('follows the page: in Arabic the right arrow goes back a day, and digits are the console own', async () => {
    document.documentElement.setAttribute('dir', 'rtl');
    const { fixture } = await open('ar', [
      { provide: SCS_ARABIC_DIGITS, useValue: 'latn' },
      { provide: SCS_CALENDAR_WEEK_START, useValue: 0 },
    ]);
    expect(byTest('date-calendar-title')?.textContent?.trim()).toBe('أغسطس 2026');
    const heads = [...document.querySelectorAll('[role="columnheader"]')].map((head) => head.textContent?.trim());
    expect(heads).toEqual(['ح', 'ن', 'ث', 'ر', 'خ', 'ج', 'س']);
    press(document.activeElement!, 'ArrowRight');
    fixture.detectChanges();
    await settle();
    expect((document.activeElement as HTMLElement).dataset['day']).toBe('2026-08-27');
    expect(document.activeElement?.textContent?.trim()).toBe('27');
    press(document.activeElement!, 'Enter');
    fixture.detectChanges();
    expect(byTest('date')?.textContent).toContain('27 أغسطس 2026');
  });

  it('writes Arabic-Indic digits where the console uses them (the clinic)', async () => {
    await open('ar');
    expect(byTest('date-calendar-title')?.textContent?.trim()).toBe('أغسطس ٢٠٢٦');
    expect(byTest('date-calendar-2026-08-28')?.textContent?.trim()).toBe('٢٨');
  });
});

@Component({
  imports: [ScsCalendarComponent],
  template: `<scs-calendar testId="cal" today="2026-08-28" todayLink [(value)]="day" />`,
})
class TodayHost {
  readonly day = signal<string | null>('2026-03-02');
}

describe('ScsCalendarComponent, the clinic link to today', () => {
  it('shows today and chooses it', () => {
    setUp('ar');
    const fixture = TestBed.createComponent(TodayHost);
    fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('[data-testid="cal-today"]') as HTMLButtonElement;
    expect(link.textContent?.trim()).toBe('اليوم');
    link.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.day()).toBe('2026-08-28');
    expect(fixture.nativeElement.querySelector('[data-testid="cal-2026-08-28"]').classList).toContain('chosen');
  });
});

describe('ScsCalendarComponent, a range', () => {
  it('takes a start and then an end, and bands the days between', () => {
    setUp('en');
    const fixture = TestBed.createComponent(RangeHost);
    fixture.detectChanges();
    const day = (date: string) => fixture.nativeElement.querySelector(`[data-testid="cal-${date}"]`) as HTMLButtonElement;
    day('2026-08-18').click();
    fixture.detectChanges();
    expect(fixture.componentInstance.range()).toEqual({ start: '2026-08-10', end: '2026-08-18' });
    const band = (date: string) => day(date).parentElement?.getAttribute('data-band') ?? null;
    expect([band('2026-08-10'), band('2026-08-12'), band('2026-08-18'), band('2026-08-19')]).toEqual(['start', 'middle', 'end', null]);
    expect(day('2026-08-12').parentElement?.getAttribute('aria-selected')).toBe('true');
    // A third choice begins a new range.
    day('2026-08-03').click();
    fixture.detectChanges();
    expect(fixture.componentInstance.range()).toEqual({ start: '2026-08-03', end: null });
  });
});
