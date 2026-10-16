import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsButtonDirective, ScsButtonSize, ScsButtonType } from './controls';

@Component({
  imports: [ScsButtonDirective],
  template: `
    <button
      type="button"
      data-testid="button"
      [scsButton]="type()"
      [size]="size()"
      [busy]="busy()"
      [disabled]="disabled()"
      (click)="presses.set(presses() + 1)"
    >
      Save
    </button>
    <button type="button" data-testid="icon-only" scsButton="ghost" size="sm" label="Add"><span>+</span></button>
    <button type="button" data-testid="plain" scsButton>Plain</button>
  `,
})
class Host {
  readonly type = signal<ScsButtonType>('primary');
  readonly size = signal<ScsButtonSize | undefined>('md');
  readonly busy = signal(false);
  readonly disabled = signal(false);
  readonly presses = signal(0);
}

/** The button of the design's Button page (spec 095, `contracts/components.md`). */
describe('the button', () => {
  function draw() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLButtonElement;
    return { fixture, host: fixture.componentInstance, find };
  }

  it('carries its type and size for the styles, and no size keeps the earlier geometry', () => {
    const { fixture, host, find } = draw();
    const button = find('button');
    for (const type of ['primary', 'secondary', 'tertiary', 'ghost', 'link', 'destructive'] as ScsButtonType[]) {
      host.type.set(type);
      fixture.detectChanges();
      expect(button.getAttribute('data-variant')).toBe(type);
    }
    host.size.set('lg');
    fixture.detectChanges();
    expect(button.getAttribute('data-size')).toBe('lg');
    expect(find('plain').getAttribute('data-variant')).toBe('primary');
    expect(find('plain').hasAttribute('data-size')).toBe(false);
  });

  it('is locked while busy, says so, and cannot be pressed twice', () => {
    const { fixture, host, find } = draw();
    const button = find('button');
    button.click();
    expect(host.presses()).toBe(1);
    host.busy.set(true);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.getAttribute('aria-busy')).toBe('true');
    button.click();
    expect(host.presses()).toBe(1);
    // Its words stay, so it keeps its name and its width.
    expect(button.textContent?.trim()).toBe('Save');
  });

  it('is disabled when told, without saying busy', () => {
    const { fixture, host, find } = draw();
    host.disabled.set(true);
    fixture.detectChanges();
    expect(find('button').disabled).toBe(true);
    expect(find('button').hasAttribute('aria-busy')).toBe(false);
  });

  it('takes the name of an icon-only button from its label, and is square', () => {
    const { find } = draw();
    const button = find('icon-only');
    expect(button.getAttribute('aria-label')).toBe('Add');
    expect(button.hasAttribute('data-icon-only')).toBe(true);
    expect(find('button').hasAttribute('aria-label')).toBe(false);
  });
});
