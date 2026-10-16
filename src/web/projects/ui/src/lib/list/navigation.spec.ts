import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsStepperComponent } from '../stepper/stepper.component';
import { ScsTableFooterComponent, ScsTabsComponent } from './list';

@Component({
  imports: [ScsTableFooterComponent, ScsTabsComponent, ScsStepperComponent],
  template: `
    <scs-table-footer count="1–10 of 100" previousLabel="Previous" nextLabel="Next" moreLabel="More pages" [pages]="pages()" [(page)]="page" />
    <scs-tabs [tabs]="tabs" [(active)]="tab" />
    <scs-stepper label="Steps" doneLabel="done" [steps]="[{ label: 'Clinic' }, { label: 'Plan & Billing' }, { label: 'Review' }]" [current]="1" />
  `,
})
class Host {
  readonly pages = signal(10);
  readonly page = signal(2);
  readonly tab = signal('all');
  readonly tabs = [
    { key: 'all', label: 'All' },
    { key: 'open', label: 'Open' },
    { key: 'closed', label: 'Closed' },
  ];
}

/** The paging with the menu of the pages not shown drawn open in place (the gallery's still specimen). */
@Component({
  imports: [ScsTableFooterComponent],
  template: `<scs-table-footer menuShown count="11–20 of 100" previousLabel="Previous" nextLabel="Next" moreLabel="More pages" [pages]="10" [page]="2" />`,
})
class StillHost {}

const press = (element: Element, key: string) =>
  element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));

/** The navigation parts (spec 095, `contracts/components.md`). */
describe('navigation', () => {
  function draw() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const shown = () =>
      [...fixture.nativeElement.querySelectorAll('[data-testid="table-paging"] .scs-page-item:not(.arrow)')].map((button) =>
        (button as HTMLElement).textContent?.trim().replace(/\s+/g, ''),
      );
    return { fixture, host: fixture.componentInstance, shown };
  }

  it('folds the paging past six pages: three around the current page, "…", the last three', () => {
    const { fixture, host, shown } = draw();
    expect(shown()).toEqual(['1', '2', '3', '…', '8', '9', '10']);
    host.page.set(5);
    fixture.detectChanges();
    expect(shown()).toEqual(['4', '5', '6', '…', '8', '9', '10']);
    host.page.set(9);
    fixture.detectChanges();
    expect(shown()).toEqual(['…', '5', '6', '7', '8', '9', '10']);
    host.pages.set(6);
    host.page.set(1);
    fixture.detectChanges();
    expect(shown()).toEqual(['1', '2', '3', '4', '5', '6']);
  });

  it('"…" opens a menu of every page not shown, and choosing one goes there', async () => {
    const { fixture, host } = draw();
    host.page.set(5);
    fixture.detectChanges();
    const more = fixture.nativeElement.querySelector('[data-testid="page-more"]') as HTMLButtonElement;
    expect(more.getAttribute('aria-label')).toBe('More pages');
    more.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const menu = document.querySelector('[data-testid="page-menu"]') as HTMLElement;
    expect([...menu.querySelectorAll('button')].map((button) => button.textContent?.trim())).toEqual(['1', '2', '3', '7']);
    (menu.querySelector('[data-testid="page-7"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(host.page()).toBe(7);
  });

  it('tabs: the arrows move and choose, only the chosen tab is in the Tab order', async () => {
    const { fixture, host } = draw();
    const tab = (key: string) => fixture.nativeElement.querySelector(`[data-testid="tab-${key}"]`) as HTMLButtonElement;
    expect(tab('all').getAttribute('tabindex')).toBe('0');
    expect(tab('open').getAttribute('tabindex')).toBe('-1');
    press(tab('all'), 'ArrowRight');
    fixture.detectChanges();
    expect(host.tab()).toBe('open');
    press(tab('open'), 'End');
    fixture.detectChanges();
    expect(host.tab()).toBe('closed');
    press(tab('closed'), 'ArrowRight');
    fixture.detectChanges();
    expect(host.tab()).toBe('all');
  });

  it('"…" may be drawn open in place, still: every page not shown, and nothing to press (spec 095)', () => {
    const fixture = TestBed.createComponent(StillHost);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const more = element.querySelector('.more-still .scs-page-item.more') as HTMLButtonElement;
    expect(more.getAttribute('aria-expanded')).toBe('true');
    expect(more.getAttribute('aria-label')).toBe('More pages');
    const items = [...element.querySelectorAll('.more-still [role="menu"] [role="menuitem"]')];
    expect(items.map((item) => item.textContent?.trim())).toEqual(['4', '5', '6', '7']);
    expect(items.every((item) => item.getAttribute('tabindex') === '-1')).toBe(true);
    // Not the working "…": no menu opens on the page, and the working trigger is not drawn.
    expect(element.querySelector('[data-testid="page-more"]')).toBeNull();
    expect(document.querySelector('[data-testid="page-menu"]')).toBeNull();
  });

  it('stepper: done steps are ticked, the current one is marked, the rest are ahead', () => {
    const { fixture } = draw();
    const step = (n: number) => fixture.nativeElement.querySelector(`[data-testid="step-${n}"]`) as HTMLElement;
    expect(step(1).getAttribute('data-state')).toBe('done');
    expect(step(1).querySelector('scs-icon')).not.toBeNull();
    expect(step(1).textContent).toContain('(done)');
    expect(step(2).getAttribute('aria-current')).toBe('step');
    expect(step(2).querySelector('.circle')?.textContent?.trim()).toBe('2');
    expect(step(3).getAttribute('data-state')).toBe('ahead');
  });
});
