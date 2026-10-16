import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsSkeletonBlockComponent, ScsSkeletonComponent } from '../list/list';
import { ScsProgressComponent } from './progress.component';
import { ScsSpinnerComponent } from './spinner.component';

@Component({
  imports: [ScsSpinnerComponent, ScsProgressComponent, ScsSkeletonComponent, ScsSkeletonBlockComponent],
  template: `
    <scs-spinner data-testid="named" size="xl" label="Loading your dashboard" />
    <scs-spinner data-testid="quiet" size="sm" />
    <scs-progress data-testid="known" label="Syncing — 62%" [value]="value()" intent="success" />
    <scs-progress data-testid="unknown" name="Loading" />
    <scs-skeleton data-testid="lines" [widths]="[280, 220, 140]" label="Loading" />
    <scs-skeleton-block data-testid="circle" round [width]="60" [height]="60" />
  `,
})
class Host {
  readonly value = signal<number | null>(62);
}

/** The loading parts (spec 095, `contracts/components.md`). */
describe('loading', () => {
  it('a spinner says what is loading when it is alone, and is quiet beside words', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    expect(find('named').getAttribute('role')).toBe('status');
    expect(find('named').getAttribute('aria-label')).toBe('Loading your dashboard');
    expect(find('named').getAttribute('data-size')).toBe('xl');
    expect(find('quiet').getAttribute('aria-hidden')).toBe('true');
  });

  it('a progress bar tells its share, or runs when the share is not known', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const bar = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"] [role="progressbar"]`) as HTMLElement;
    expect(bar('known').getAttribute('aria-valuenow')).toBe('62');
    expect(bar('known').getAttribute('aria-labelledby')).toBeTruthy();
    expect((bar('known').firstElementChild as HTMLElement).style.inlineSize).toBe('62%');
    expect(bar('unknown').getAttribute('aria-valuenow')).toBeNull();
    expect(bar('unknown').getAttribute('aria-label')).toBe('Loading');
    expect(bar('unknown').firstElementChild?.classList).toContain('running');
    fixture.componentInstance.value.set(140);
    fixture.detectChanges();
    expect(bar('known').getAttribute('aria-valuenow')).toBe('100');
  });

  it('a skeleton draws lines of the given widths, and blocks for composed ones', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const lines = [...fixture.nativeElement.querySelectorAll('[data-testid="lines"] span')] as HTMLElement[];
    expect(lines.map((line) => line.style.inlineSize)).toEqual(['280px', '220px', '140px']);
    const circle = fixture.nativeElement.querySelector('[data-testid="circle"]') as HTMLElement;
    expect(circle.classList).toContain('round');
    expect(circle.style.inlineSize).toBe('60px');
    expect(circle.getAttribute('aria-hidden')).toBe('true');
  });
});
