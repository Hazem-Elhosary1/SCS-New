import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { uiTranslations } from '../../testing/ui-testing';
import { clockOf, formatTime, fromClock, isTime, periodNames, timesBetween } from './time';
import { ScsTimeFieldComponent } from './time-field.component';

@Component({
  imports: [ScsTimeFieldComponent],
  template: `
    <scs-time-field label="Time" placeholder="Select a time" testId="list" min="09:00" max="12:00" [disabledTimes]="['09:00', '09:30']" [(value)]="list" />
    <scs-time-field label="Time" testId="wheel" mode="wheel" [step]="5" [(value)]="wheel" />
  `,
})
class Host {
  readonly list = signal('');
  readonly wheel = signal('14:30');
}

function setUp(language: 'ar' | 'en', extra: unknown[] = []) {
  TestBed.configureTestingModule({ imports: [uiTranslations(language)], providers: extra as never[] });
  TestBed.inject(LanguageService).use(language);
}

const press = (element: Element, key: string) =>
  element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
const byTest = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const settle = () => new Promise((done) => setTimeout(done));

describe('times of day', () => {
  it('reads and writes the twelve-hour clock', () => {
    expect([isTime('14:30'), isTime('24:00'), isTime('9:30')]).toEqual([true, false, false]);
    expect(clockOf('00:15')).toEqual({ hour: 12, minute: 15, pm: false });
    expect(clockOf('12:00')).toEqual({ hour: 12, minute: 0, pm: true });
    expect(clockOf('14:30')).toEqual({ hour: 2, minute: 30, pm: true });
    expect(fromClock(12, 0, false)).toBe('00:00');
    expect(fromClock(2, 30, true)).toBe('14:30');
    expect(timesBetween(30, '09:00', '11:00')).toEqual(['09:00', '09:30', '10:00', '10:30', '11:00']);
    expect(timesBetween(60).length).toBe(24);
  });

  it('writes a time as the pages do, in either language', () => {
    expect(formatTime('14:30', 'en')).toBe('2:30 PM');
    expect(formatTime('14:30', 'ar')).toBe('2:30 م');
    expect(formatTime('14:30', 'ar', 'arab')).toBe('٢:٣٠ م');
    expect(periodNames('en')).toEqual(['AM', 'PM']);
    expect(periodNames('ar')).toEqual(['ص', 'م']);
  });
});

/** The time field (spec 095, `contracts/components.md`). */
describe('ScsTimeFieldComponent', () => {
  async function draw(language: 'ar' | 'en' = 'en', extra: unknown[] = []) {
    setUp(language, extra);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  }

  async function open(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }, id: string) {
    press(byTest(id)!, 'ArrowDown');
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
  }

  it('lists the times every step, skips those refused, and chooses with Enter', async () => {
    const fixture = await draw();
    const trigger = byTest('list') as HTMLButtonElement;
    expect(trigger.textContent).toContain('Select a time');
    await open(fixture, 'list');
    const list = document.querySelector('[data-testid="list-panel"] [role="listbox"]') as HTMLElement;
    expect(document.activeElement).toBe(list);
    const rows = [...list.querySelectorAll('[role="option"]')].map((row) => row.textContent?.trim());
    expect(rows).toEqual(['9:00 AM', '9:30 AM', '10:00 AM', '10:30 AM', '11:00 AM', '11:30 AM', '12:00 PM']);
    // The first time that may be chosen is active.
    expect(list.getAttribute('aria-activedescendant')).toContain('1000');
    press(list, 'ArrowDown');
    press(list, 'ArrowDown');
    fixture.detectChanges();
    press(list, 'Enter');
    fixture.detectChanges();
    await settle();
    expect(fixture.componentInstance.list()).toBe('11:00');
    expect(byTest('list-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(trigger.textContent).toContain('11:00 AM');
  });

  it('does not take a refused time, and Escape closes without choosing', async () => {
    const fixture = await draw();
    await open(fixture, 'list');
    const refused = document.querySelector('[data-time="09:00"]') as HTMLElement;
    expect(refused.getAttribute('aria-disabled')).toBe('true');
    refused.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.list()).toBe('');
    press(document.activeElement!, 'Escape');
    fixture.detectChanges();
    expect(byTest('list-panel')).toBeNull();
  });

  it('turns three wheels and keeps the time with Done', async () => {
    const fixture = await draw();
    await open(fixture, 'wheel');
    const hour = byTest('wheel-hour')!;
    expect(document.activeElement).toBe(hour);
    expect(hour.getAttribute('aria-valuenow')).toBe('2');
    expect([...hour.querySelectorAll('.turn')].map((turn) => turn.textContent?.trim())).toEqual(['12', '01', '02', '03', '04']);
    press(hour, 'ArrowDown');
    fixture.detectChanges();
    const minute = byTest('wheel-minute')!;
    press(minute, 'ArrowUp');
    fixture.detectChanges();
    expect(minute.getAttribute('aria-valuenow')).toBe('25');
    const period = byTest('wheel-period')!;
    expect(period.getAttribute('aria-valuetext')).toBe('PM');
    press(period, 'ArrowUp');
    fixture.detectChanges();
    expect(period.getAttribute('aria-valuetext')).toBe('AM');
    byTest('wheel-done')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.wheel()).toBe('03:25');
    expect(byTest('wheel')?.textContent).toContain('3:25 AM');
  });

  it('writes the time in Arabic with the console digits', async () => {
    await draw('ar', [{ provide: SCS_ARABIC_DIGITS, useValue: 'latn' }]);
    expect(byTest('wheel')?.textContent).toContain('2:30 م');
  });
});
