import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsRadioGroupComponent } from './radio.component';
import { ScsSegmentedComponent } from './segmented.component';
import { ScsSliderComponent } from './slider.component';
import { ScsSelectComponent } from './select.component';

@Component({
  imports: [ScsRadioGroupComponent, ScsSliderComponent, ScsSegmentedComponent],
  template: `
    <scs-radio-group
      label="Payment"
      testId="pay"
      [options]="[
        { value: 'card', label: 'Card' },
        { value: 'cash', label: 'Cash', disabled: true, reason: 'Not on this plan' },
      ]"
      [(value)]="pay"
      [error]="payError()"
    />
    <scs-slider label="Threshold" testId="one" [(value)]="one" [step]="5" />
    <scs-slider label="Ages" testId="two" [(range)]="two" [thumbNames]="['From', 'To']" />
    <scs-segmented
      ariaLabel="Period"
      testId="period"
      [segments]="[
        { value: 'day', label: 'Day' },
        { value: 'week', label: 'Week', disabled: true },
        { value: 'month', label: 'Month' },
      ]"
      [(value)]="period"
    />
  `,
})
class Host {
  readonly pay = signal<string | null>(null);
  readonly payError = signal('');
  readonly one = signal(60);
  readonly two = signal<readonly [number, number] | null>([30, 75]);
  readonly period = signal('day');
}

@Component({
  imports: [ScsSelectComponent],
  template: `
    <scs-select
      label="Services"
      testId="services"
      multiple
      [options]="[
        { value: 'xray', label: 'X-Ray' },
        { value: 'cleaning', label: 'Dental Cleaning' },
        { value: 'follow', label: 'Follow-up' },
      ]"
      [(values)]="chosen"
    />
  `,
})
class MultiHost {
  readonly chosen = signal<readonly string[]>(['xray']);
}

const key = (element: Element, name: string) =>
  element.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));

/** Radio, slider and segmented control (spec 095, `contracts/components.md`). */
describe('the controls of choice', () => {
  function draw() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = <T extends Element>(selector: string) => fixture.nativeElement.querySelector(selector) as T;
    return { fixture, host: fixture.componentInstance, find };
  }

  it('radio: a named group of real radios; a refused choice is disabled and says why; an error is read', () => {
    const { fixture, host, find } = draw();
    const group = find<HTMLElement>('[data-testid="pay"]');
    expect(group.getAttribute('role')).toBe('radiogroup');
    expect(group.getAttribute('aria-labelledby')).toBeTruthy();
    find<HTMLInputElement>('[data-testid="pay-card"]').click();
    expect(host.pay()).toBe('card');
    const cash = find<HTMLInputElement>('[data-testid="pay-cash"]');
    expect(cash.disabled).toBe(true);
    expect(find(`#${cash.getAttribute('aria-describedby')}`).textContent).toContain('Not on this plan');
    host.payError.set('Please select an option');
    fixture.detectChanges();
    expect(group.getAttribute('aria-invalid')).toBe('true');
    expect(find(`#${group.getAttribute('aria-describedby')}`).textContent).toContain('Please select an option');
  });

  it('slider: moves by its step, to its ends, and tells its value', () => {
    const { fixture, host, find } = draw();
    const thumb = find<HTMLElement>('[data-testid="one-thumb-0"]');
    expect(thumb.getAttribute('role')).toBe('slider');
    expect(thumb.getAttribute('aria-valuenow')).toBe('60');
    key(thumb, 'ArrowUp');
    expect(host.one()).toBe(65);
    key(thumb, 'PageDown');
    expect(host.one()).toBe(55);
    key(thumb, 'End');
    expect(host.one()).toBe(100);
    key(thumb, 'Home');
    fixture.detectChanges();
    expect(host.one()).toBe(0);
    expect(thumb.getAttribute('aria-valuenow')).toBe('0');
    expect(find('[data-testid="one-value"]').textContent).toBe('0');
  });

  it('slider: a range has two named thumbs that never cross', () => {
    const { fixture, host, find } = draw();
    const from = find<HTMLElement>('[data-testid="two-thumb-0"]');
    const to = find<HTMLElement>('[data-testid="two-thumb-1"]');
    expect(from.getAttribute('aria-label')).toBe('From');
    expect(to.getAttribute('aria-valuemin')).toBe('30');
    key(from, 'End');
    expect(host.two()).toEqual([75, 75]);
    fixture.detectChanges();
    key(to, 'Home');
    expect(host.two()).toEqual([75, 75]);
    expect(find('[data-testid="two-value"]').textContent).toBe('75 – 75');
  });

  it('select, several chosen: a box on each row, the list stays open, the trigger names them all', async () => {
    const fixture = TestBed.createComponent(MultiHost);
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector('[data-testid="services"]') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const list = document.querySelector('[data-testid="services-panel"] [role="listbox"]') as HTMLElement;
    expect(list.getAttribute('aria-multiselectable')).toBe('true');
    const rows = () => [...document.querySelectorAll<HTMLElement>('[data-testid="services-panel"] [role="option"]')];
    rows()[1].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.chosen()).toEqual(['xray', 'cleaning']);
    expect(document.querySelector('[data-testid="services-panel"]')).not.toBeNull();
    expect(rows().map((row) => row.getAttribute('aria-selected'))).toEqual(['true', 'true', 'false']);
    rows()[0].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.chosen()).toEqual(['cleaning']);
    expect(trigger.textContent).toContain('Dental Cleaning');
  });

  it('segmented: a radio group the arrows move through, skipping a disabled segment', () => {
    const { fixture, host, find } = draw();
    const day = find<HTMLButtonElement>('[data-testid="period-day"]');
    expect(find('[data-testid="period"]').getAttribute('role')).toBe('radiogroup');
    expect(day.getAttribute('aria-checked')).toBe('true');
    expect(find('[data-testid="period-month"]').getAttribute('tabindex')).toBe('-1');
    key(day, 'ArrowDown');
    fixture.detectChanges();
    expect(host.period()).toBe('month');
    find<HTMLButtonElement>('[data-testid="period-week"]').click();
    expect(host.period()).toBe('month');
    key(find('[data-testid="period-month"]'), 'Home');
    expect(host.period()).toBe('day');
  });
});
