import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsKpiCardComponent } from './page-parts';

@Component({
  imports: [ScsKpiCardComponent],
  template: `
    <scs-kpi-card data-testid="up" label="Total Active Clinics" value="148" note="+6 this month" />
    <scs-kpi-card data-testid="down" label="Active Tickets" value="12" note="-3 this month" trend="negative" />
    <scs-kpi-card data-testid="clinic" label="Label" value="1,250" note="12%" caption="vs last period" trend="neutral" icon="chart" />
    <scs-kpi-card data-testid="none" label="Label" value="1,250" note="12%" trend="none" />
  `,
})
class Host {}

/** The KPI card (spec 095, `contracts/components.md`). */
describe('ScsKpiCardComponent', () => {
  it('shows what it counts, the number, and how it moved, tinted by its trend', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    expect(find('up').getAttribute('data-trend')).toBe('positive');
    expect(find('up').querySelector('[data-testid="kpi-value"]')?.textContent?.trim()).toBe('148');
    expect(find('down').getAttribute('data-trend')).toBe('negative');
    expect(find('clinic').querySelector('[data-testid="kpi-caption"]')?.textContent?.trim()).toBe('vs last period');
    expect(find('clinic').querySelector('.icon')).not.toBeNull();
    expect(find('none').querySelector('[data-testid="kpi-note"]')).toBeNull();
  });
});
