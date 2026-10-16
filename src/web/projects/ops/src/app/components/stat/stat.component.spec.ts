import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DividerComponent } from '../divider/divider.component';
import { StatComponent } from './stat.component';

@Component({
  imports: [StatComponent, DividerComponent],
  template: `
    <app-stat data-testid="up" label="Total Clinics" value="148" note="+6 this month" trend="positive" />
    <app-divider data-testid="plain" />
    <app-divider data-testid="or" label="or" />
  `,
})
class Host {}

/** The operators' stat and divider (spec 095, `contracts/components.md`). */
describe('stat and divider', () => {
  it('a stat shows its name, its number and how it moved', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const up = fixture.nativeElement.querySelector('[data-testid="up"]') as HTMLElement;
    expect(up.getAttribute('data-trend')).toBe('positive');
    expect(up.querySelector('[data-testid="stat-value"]')?.textContent?.trim()).toBe('148');
    expect(up.querySelector('[data-testid="stat-note"]')?.textContent?.trim()).toBe('+6 this month');
  });

  it('a plain divider is only a picture; one with a word is a named separator', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const plain = fixture.nativeElement.querySelector('[data-testid="plain"]') as HTMLElement;
    const or = fixture.nativeElement.querySelector('[data-testid="or"]') as HTMLElement;
    expect(plain.getAttribute('aria-hidden')).toBe('true');
    expect(or.getAttribute('role')).toBe('separator');
    expect(or.getAttribute('aria-label')).toBe('or');
  });
});
