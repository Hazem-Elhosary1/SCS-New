import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { ScsNavItemComponent } from './nav-item.component';

@Component({ template: '' })
class Blank {}

@Component({
  imports: [ScsNavItemComponent],
  template: `
    <scs-nav-item label="Clinics" icon="building" link="/clinics" testId="clinics" />
    <scs-nav-item label="Home" icon="home" link="/" exact testId="home" />
    <scs-nav-item label="Reports" icon="chart" link="/reports" disabled testId="reports" />
    <scs-nav-item label="Settings" icon="settings" expandable count="3" testId="settings" [(expanded)]="open" />
  `,
})
class Host {
  readonly open = signal(false);
}

/** The side bar's item (spec 095, `contracts/components.md`). */
describe('ScsNavItemComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([
        { path: '', component: Blank },
        { path: 'clinics', component: Blank },
        { path: 'reports', component: Blank },
      ])],
    });
  });

  it('links to its page and marks it as the current page there', async () => {
    const fixture = TestBed.createComponent(Host);
    await TestBed.inject(Router).navigateByUrl('/clinics');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    expect(find('clinics').tagName).toBe('A');
    expect(find('clinics').getAttribute('href')).toBe('/clinics');
    expect(find('clinics').getAttribute('aria-current')).toBe('page');
    expect(find('clinics').classList).toContain('on');
    expect(find('home').getAttribute('aria-current')).toBeNull();
  });

  it('greys a place that may not be opened, and opens a group with its count', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    expect(find('reports').tagName).toBe('BUTTON');
    expect(find('reports').getAttribute('aria-disabled')).toBe('true');
    const settings = find('settings');
    expect(settings.getAttribute('aria-expanded')).toBe('false');
    expect(settings.querySelector('[data-testid="nav-count"]')?.textContent).toBe('3');
    settings.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.open()).toBe(true);
    expect(settings.getAttribute('aria-expanded')).toBe('true');
  });
});
