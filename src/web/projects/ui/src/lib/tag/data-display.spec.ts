import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsAccordionComponent, ScsAccordionItemComponent } from '../accordion/accordion.component';
import { ScsAppliedChipsComponent, ScsCardComponent } from '../list/list';
import { ScsTagComponent } from './tag.component';

@Component({
  imports: [ScsTagComponent, ScsAccordionComponent, ScsAccordionItemComponent, ScsCardComponent, ScsAppliedChipsComponent],
  template: `
    <scs-tag data-testid="tag" intent="success" dismissible removeLabel="Remove Active" (removed)="removed.set(true)">Active</scs-tag>
    <scs-tag data-testid="plain" selected>Dental</scs-tag>
    <scs-accordion>
      <scs-accordion-item data-testid="billing" heading="Billing Details" summary="Next invoice" [(expanded)]="open">Next invoice: EGP 499</scs-accordion-item>
    </scs-accordion>
    <scs-card data-testid="card" interactive selected heading="Al Noor Clinic" />
    <scs-applied-chips removeLabel="Remove" clearAllLabel="Clear all" [chips]="[{ key: 'city', label: 'Nasr City' }]" (remove)="chip.set($event)" />
  `,
})
class Host {
  readonly removed = signal(false);
  readonly open = signal(false);
  readonly chip = signal('');
}

/** The data display parts (spec 095, `contracts/components.md`). */
describe('data display', () => {
  function draw() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement;
    return { fixture, host: fixture.componentInstance, find };
  }

  it('a tag says its word in its intent, and its remove button is named and works', () => {
    const { host, find } = draw();
    expect(find('[data-testid="tag"]').getAttribute('data-intent')).toBe('success');
    const remove = find('[data-testid="tag"] [data-testid="tag-remove"]') as HTMLButtonElement;
    expect(remove.getAttribute('aria-label')).toBe('Remove Active');
    remove.click();
    expect(host.removed()).toBe(true);
    expect(find('[data-testid="plain"]').classList).toContain('chosen');
    expect(find('[data-testid="plain"] [data-testid="tag-remove"]')).toBeNull();
  });

  it('an accordion item opens and closes from its heading, and says so', () => {
    const { fixture, host, find } = draw();
    const head = find('[data-testid="billing"] button');
    const body = find('[data-testid="billing"] [role="region"]');
    expect(head.getAttribute('aria-expanded')).toBe('false');
    expect(body.hidden).toBe(true);
    expect(body.getAttribute('aria-labelledby')).toBe(head.id);
    head.click();
    fixture.detectChanges();
    expect(host.open()).toBe(true);
    expect(head.getAttribute('aria-expanded')).toBe('true');
    expect(body.hidden).toBe(false);
  });

  it('a card can be interactive and selected; an applied chip is a tag that can be taken away', () => {
    const { host, find } = draw();
    expect(find('[data-testid="card"]').classList).toContain('interactive');
    expect(find('[data-testid="card"]').classList).toContain('selected');
    const chip = find('[data-testid="applied-chip"]');
    expect(chip.tagName).toBe('SCS-TAG');
    (chip.querySelector('[data-testid="tag-remove"]') as HTMLButtonElement).click();
    expect(host.chip()).toBe('city');
  });
});
