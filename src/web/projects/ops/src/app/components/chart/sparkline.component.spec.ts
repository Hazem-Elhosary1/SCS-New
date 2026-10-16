import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SparklineComponent } from './sparkline.component';

@Component({
  imports: [SparklineComponent],
  template: `
    <app-sparkline data-testid="up" label="Clinics, rising" [points]="[2, 4, 3, 6, 8]" />
    <app-sparkline data-testid="flat" [points]="[5, 6, 5]" />
  `,
})
class Host {}

/** The operators' sparkline (spec 095, `contracts/components.md`). */
describe('SparklineComponent', () => {
  it('draws the points left to right, ends with a dot, and takes its colour from where it ended', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const up = fixture.nativeElement.querySelector('[data-testid="up"]') as HTMLElement;
    expect(up.getAttribute('role')).toBe('img');
    expect(up.getAttribute('aria-label')).toBe('Clinics, rising');
    expect(up.getAttribute('data-trend')).toBe('up');
    expect(up.querySelector('polyline')?.getAttribute('points')).toBe('0,22 20,15.33 40,18.67 60,8.67 80,2');
    expect(up.querySelector('circle')?.getAttribute('cx')).toBe('80');
    expect((fixture.nativeElement.querySelector('[data-testid="flat"]') as HTMLElement).getAttribute('data-trend')).toBe('flat');
  });
});
