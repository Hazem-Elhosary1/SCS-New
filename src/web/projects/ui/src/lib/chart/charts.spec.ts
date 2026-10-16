import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsUsageMeterComponent } from '../usage-meter/usage-meter.component';
import { ScsBarChartComponent } from './bar-chart.component';

@Component({
  imports: [ScsUsageMeterComponent, ScsBarChartComponent],
  template: `
    <scs-usage-meter data-testid="meter" label="Storage used" [used]="used()" [limit]="100" [value]="used() + ' / 100 GB'" note="85% used — approaching limit" />
    <scs-bar-chart data-testid="chart" label="Tickets resolved this week" [bars]="bars" />
  `,
})
class Host {
  readonly used = signal(85);
  readonly bars = [
    { label: 'Mon', value: 60 },
    { label: 'Tue', value: 80 },
    { label: 'Sat', value: 112 },
    { label: 'Sun', value: 40 },
  ];
}

/** The usage meter and the bar chart (spec 095, `contracts/components.md`). */
describe('meter and chart', () => {
  it('a meter is filled to the share used, takes its level from it, and tells it', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const meter = fixture.nativeElement.querySelector('[data-testid="meter"]') as HTMLElement;
    const track = meter.querySelector('[role="meter"]') as HTMLElement;
    expect(meter.getAttribute('data-level')).toBe('warning');
    expect(track.getAttribute('aria-valuenow')).toBe('85');
    expect(track.getAttribute('aria-valuetext')).toBe('85 / 100 GB — 85% used — approaching limit');
    expect((track.firstElementChild as HTMLElement).style.inlineSize).toBe('85%');
    fixture.componentInstance.used.set(120);
    fixture.detectChanges();
    expect(meter.getAttribute('data-level')).toBe('exceeded');
    expect((track.firstElementChild as HTMLElement).style.inlineSize).toBe('100%');
  });

  it('a chart draws each bar to its share, marks the highest, and lists the numbers in a table', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const chart = fixture.nativeElement.querySelector('[data-testid="chart"]') as HTMLElement;
    const bars = [...chart.querySelectorAll('.bar')] as HTMLElement[];
    expect(bars.map((bar) => bar.classList.contains('chosen'))).toEqual([false, false, true, false]);
    expect(bars[2].style.blockSize).toBe('100%');
    const rows = [...chart.querySelectorAll('table tr')].map((row) => [...row.children].map((cell) => cell.textContent?.trim()).join(' '));
    expect(rows).toEqual(['Mon 60', 'Tue 80', 'Sat 112', 'Sun 40']);
    expect(chart.querySelector('table')?.getAttribute('aria-labelledby')).toBeTruthy();
  });
});
