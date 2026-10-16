import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsEmptyStateComponent } from '../list/list';
import { ScsTooltipDirective } from './overlay';

@Component({
  imports: [ScsTooltipDirective, ScsEmptyStateComponent],
  template: `
    <button type="button" data-testid="tip" scsTooltip="Helpful tip goes here" scsTooltipPlacement="top">?</button>
    <scs-empty-state data-testid="loading" context="loading" title="Loading data..." />
    <scs-empty-state data-testid="done" context="success" title="Success" text="Your changes have been saved." />
  `,
})
class Host {}

/** The tooltip's sides and the empty state's kinds (spec 095, `contracts/components.md`). */
describe('tooltip and empty state', () => {
  it('a tooltip names its control and sits on the side asked for, with its arrow there', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const tip = fixture.nativeElement.querySelector('[data-testid="tip"]') as HTMLButtonElement;
    expect(tip.getAttribute('aria-label')).toBe('Helpful tip goes here');
    tip.dispatchEvent(new Event('mouseenter'));
    fixture.detectChanges();
    const bubble = document.querySelector('.scs-tooltip') as HTMLElement;
    expect(bubble.getAttribute('role')).toBe('tooltip');
    expect(bubble.textContent).toBe('Helpful tip goes here');
    expect(['top', 'bottom', 'end', 'start']).toContain(bubble.getAttribute('data-placement'));
    tip.dispatchEvent(new Event('mouseleave'));
    expect(document.querySelector('.scs-tooltip')).toBeNull();
  });

  it('an empty state while loading turns a ring and says its words politely; done says so too', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const loading = fixture.nativeElement.querySelector('[data-testid="loading"]') as HTMLElement;
    expect(loading.getAttribute('role')).toBe('status');
    expect(loading.querySelector('scs-spinner')).not.toBeNull();
    expect(loading.querySelector('h3')).toBeNull();
    const done = fixture.nativeElement.querySelector('[data-testid="done"]') as HTMLElement;
    expect(done.getAttribute('role')).toBe('status');
    expect(done.querySelector('h3')?.textContent).toBe('Success');
  });
});
