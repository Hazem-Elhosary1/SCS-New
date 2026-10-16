import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsBannerComponent } from '../controls/controls';
import { ScsToastComponent } from './overlay';

@Component({
  imports: [ScsToastComponent, ScsBannerComponent],
  template: `
    <scs-toast data-testid="toast" tone="warning" title="Heads up" text="Peak hours are near." closeLabel="Close" (closed)="closed.set(true)" />
    <scs-banner data-testid="banner" tone="danger" title="Failed to sync tenant" closeLabel="Close">Retry the sync.</scs-banner>
  `,
})
class Host {
  readonly closed = signal(false);
}

/** The two styles of the Alert page (spec 095, `contracts/components.md`). */
describe('alerts', () => {
  it('a toast says what happened and a line more, and closes', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const toast = fixture.nativeElement.querySelector('[data-testid="toast"]') as HTMLElement;
    expect(toast.getAttribute('data-tone')).toBe('warning');
    expect(toast.querySelector('.title')?.textContent).toBe('Heads up');
    expect(toast.querySelector('.text')?.textContent).toBe('Peak hours are near.');
    const close = toast.querySelector('button') as HTMLButtonElement;
    expect(close.getAttribute('aria-label')).toBe('Close');
    close.click();
    expect(fixture.componentInstance.closed()).toBe(true);
  });

  it('a refusal banner is announced at once, with its icon in its mark and its title', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('[data-testid="banner"]') as HTMLElement;
    expect(banner.getAttribute('role')).toBe('alert');
    expect(banner.querySelector('.mark scs-icon')).not.toBeNull();
    expect(banner.querySelector('[data-testid="banner-title"]')?.textContent).toBe('Failed to sync tenant');
  });
});
