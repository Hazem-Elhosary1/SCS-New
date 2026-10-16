import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { ScsDayRange } from 'ui';
import { DateRangeFieldComponent } from './date-range-field.component';

@Component({
  imports: [DateRangeFieldComponent],
  template: `<app-date-range-field label="Date Range" placeholder="Select a date range" testId="range" today="2026-08-28" [(value)]="range" />`,
})
class Host {
  readonly range = signal<ScsDayRange | null>(null);
}

const byTest = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const settle = () => new Promise((done) => setTimeout(done));

/** The operators' date range field (spec 095, `contracts/components.md`). */
describe('DateRangeFieldComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot({ langs: { ar: {}, en: {} }, translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'en' } })],
      providers: [
        { provide: SCS_ARABIC_DIGITS, useValue: 'latn' },
      ],
    });
    TestBed.inject(LanguageService).use('en');
  });

  it('takes a start and an end, closes on the end, and names the range with its year once', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const trigger = byTest('range') as HTMLButtonElement;
    expect(trigger.textContent).toContain('Select a date range');
    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    expect(document.activeElement).toBe(byTest('range-calendar-2026-08-28'));
    byTest('range-calendar-2026-08-01')!.click();
    fixture.detectChanges();
    expect(byTest('range-panel')).not.toBeNull();
    expect(fixture.componentInstance.range()).toBeNull();
    byTest('range-calendar-2026-08-28')!.click();
    fixture.detectChanges();
    await settle();
    expect(fixture.componentInstance.range()).toEqual({ start: '2026-08-01', end: '2026-08-28' });
    expect(byTest('range-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(trigger.textContent).toContain('Aug 1 – Aug 28, 2026');
  });

  it('closes on Escape and keeps nothing', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const trigger = byTest('range') as HTMLButtonElement;
    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    byTest('range-calendar-2026-08-03')!.click();
    document.activeElement!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    expect(byTest('range-panel')).toBeNull();
    expect(fixture.componentInstance.range()).toBeNull();
  });
});
