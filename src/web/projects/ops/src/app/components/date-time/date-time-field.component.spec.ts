import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { DateTimeFieldComponent, partsOfDayAndTime } from './date-time-field.component';

@Component({
  imports: [DateTimeFieldComponent],
  template: `<app-date-time-field
    label="Date & Time"
    placeholder="Select date & time"
    testId="when"
    today="2026-08-28"
    firstTime="13:00"
    lastTime="16:00"
    [disabledTimes]="['16:00']"
    [(value)]="when"
  />`,
})
class Host {
  readonly when = signal('');
}

const byTest = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const settle = () => new Promise((done) => setTimeout(done));

/** The operators' date and time field (spec 095, `contracts/components.md`). */
describe('DateTimeFieldComponent', () => {
  function setUp(language: 'ar' | 'en') {
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot({ langs: { ar: {}, en: {} }, translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: language } })],
      providers: [
        { provide: SCS_ARABIC_DIGITS, useValue: 'latn' },
      ],
    });
    TestBed.inject(LanguageService).use(language);
  }

  it('reads a value of a day and a time', () => {
    expect(partsOfDayAndTime('2026-08-28T14:30')).toEqual({ day: '2026-08-28', time: '14:30' });
    expect(partsOfDayAndTime('2026-08-28')).toBeNull();
  });

  it('takes a day, moves to the times, and keeps both on a time', async () => {
    setUp('en');
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const trigger = byTest('when') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    byTest('when-calendar-2026-08-28')!.click();
    fixture.detectChanges();
    await settle();
    const times = byTest('when-times')!;
    expect(document.activeElement).toBe(times);
    expect(fixture.componentInstance.when()).toBe('');
    const refused = times.querySelector('[data-time="16:00"]') as HTMLElement;
    expect(refused.getAttribute('aria-disabled')).toBe('true');
    (times.querySelector('[data-time="14:30"]') as HTMLElement).click();
    fixture.detectChanges();
    await settle();
    expect(fixture.componentInstance.when()).toBe('2026-08-28T14:30');
    expect(byTest('when-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(trigger.textContent).toContain('Aug 28, 2026 · 2:30 PM');
  });

  it('writes both in Arabic', () => {
    setUp('ar');
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.when.set('2026-08-28T14:30');
    fixture.detectChanges();
    expect(byTest('when')?.textContent).toContain('28 أغسطس 2026 · 2:30 م');
  });
});
