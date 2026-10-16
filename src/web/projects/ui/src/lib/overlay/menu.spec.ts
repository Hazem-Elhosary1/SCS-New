import { CdkMenuTrigger } from '@angular/cdk/menu';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsMenuDirective, ScsMenuItemDirective } from './overlay';

@Component({
  imports: [CdkMenuTrigger, ScsMenuDirective, ScsMenuItemDirective],
  template: `
    <button type="button" class="scs-row-menu" data-testid="kebab" aria-label="Actions for Al Noor Clinic" [cdkMenuTriggerFor]="menu">⋯</button>
    <ng-template #menu>
      <div scsMenu data-testid="menu">
        <button type="button" scsMenuItem data-testid="view" (chosen)="chosen.set('view')">View Details</button>
        <button type="button" scsMenuItem data-testid="renew" disabled (chosen)="chosen.set('renew')">
          <span class="words">Renew Subscription<span class="reason">Needs clinics.edit</span></span>
        </button>
        <div class="scs-menu-separator" role="separator"></div>
        <button type="button" scsMenuItem danger data-testid="suspend" (chosen)="chosen.set('suspend')">Suspend</button>
      </div>
    </ng-template>
  `,
})
class Host {
  readonly chosen = signal('');
}

/** The menu (spec 095, `contracts/components.md`). */
describe('menu', () => {
  it('opens from its button, refuses a disabled choice, marks a destructive one, and takes a choice', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const kebab = fixture.nativeElement.querySelector('[data-testid="kebab"]') as HTMLButtonElement;
    kebab.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(kebab.getAttribute('aria-expanded')).toBe('true');
    const item = (id: string) => document.querySelector(`[data-testid="${id}"]`) as HTMLButtonElement;
    expect(item('renew').getAttribute('aria-disabled')).toBe('true');
    item('renew').click();
    expect(fixture.componentInstance.chosen()).toBe('');
    expect(item('suspend').getAttribute('data-danger')).toBe('true');
    item('view').click();
    fixture.detectChanges();
    expect(fixture.componentInstance.chosen()).toBe('view');
  });
});
