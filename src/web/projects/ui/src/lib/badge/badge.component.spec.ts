import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsBadgeComponent } from './badge.component';

@Component({
  imports: [ScsBadgeComponent],
  template: `
    <scs-badge intent="warning" data-testid="soft">Pending</scs-badge>
    <scs-badge intent="info" solid size="sm" data-testid="solid">Info</scs-badge>
    <scs-badge intent="brand" icon="check" data-testid="icon">Done</scs-badge>
    <scs-badge data-testid="plain">General</scs-badge>
    <scs-badge intent="danger" [dot]="false" data-testid="mark">Suspended</scs-badge>
  `,
})
class Host {}

/** The badge (spec 095, `contracts/components.md`). */
describe('ScsBadgeComponent', () => {
  it('says its word with its intent, soft or solid, with a dot, an icon or neither', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    expect(find('soft').getAttribute('data-intent')).toBe('warning');
    expect(find('soft').textContent?.trim()).toBe('Pending');
    expect(find('soft').classList).not.toContain('plain');
    expect(find('solid').classList).toContain('solid');
    expect(find('solid').getAttribute('data-size')).toBe('sm');
    expect(find('icon').querySelector('scs-icon')).not.toBeNull();
    expect(find('icon').classList).toContain('plain');
    expect(find('plain').getAttribute('data-intent')).toBe('neutral');
  });

  it('draws spec 011\'s status marks: a plain mark has no dot, and its word is held whole in one place', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const mark = fixture.nativeElement.querySelector('[data-testid="mark"]') as HTMLElement;
    expect(mark.getAttribute('data-intent')).toBe('danger');
    expect(mark.classList).toContain('plain');
    expect(mark.querySelector('.words')?.textContent?.trim()).toBe('Suspended');
  });
});
