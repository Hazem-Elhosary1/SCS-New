import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ScsNavbarComponent } from '../shell/navbar.component';
import { ScsNotificationBellComponent, ScsNotificationItemComponent, ScsNotificationPanelComponent } from './notifications';

@Component({
  imports: [ScsNavbarComponent, ScsNotificationBellComponent, ScsNotificationItemComponent, ScsNotificationPanelComponent],
  template: `
    <scs-navbar name="SCS — Ops Console" mark="S" person="Ahmed Sami" role="Super Admin" accountLabel="Account" [accountOpen]="menu()" (account)="menu.set(!menu())">
      <scs-notification-bell [count]="count()" label="Notifications, 3 unread" (pressed)="opened.set(true)" />
    </scs-navbar>
    <scs-notification-panel heading="Notifications" markAllLabel="Mark all as read" viewAllLabel="View all notifications" (markAll)="count.set(0)" (closed)="opened.set(false)">
      <scs-notification-item data-testid="first" title="New ticket assigned" text="Ticket #482 needs review." time="5 minutes ago" unread unreadLabel="Unread" />
      <scs-notification-item data-testid="second" title="Plan updated" time="1 hour ago" />
    </scs-notification-panel>
  `,
})
class Host {
  readonly count = signal<number | null>(3);
  readonly menu = signal(false);
  readonly opened = signal(false);
}

const press = (element: Element, key: string) =>
  element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));

/** The top bar and the notifications (spec 095, `contracts/components.md`). */
describe('navbar and notifications', () => {
  function draw() {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement;
    return { fixture, host: fixture.componentInstance, find };
  }

  it('the bar links home with the mark and the name, and opens the account menu', () => {
    const { fixture, host, find } = draw();
    const brand = find('[data-testid="navbar-brand"]');
    expect(brand.getAttribute('href')).toBe('/');
    expect(brand.textContent).toContain('SCS — Ops Console');
    const account = find('[data-testid="navbar-account"]');
    expect(account.getAttribute('aria-haspopup')).toBe('menu');
    account.click();
    fixture.detectChanges();
    expect(host.menu()).toBe(true);
    expect(account.getAttribute('aria-expanded')).toBe('true');
  });

  it('the bell says how many are unread and shows the number, or a dot', () => {
    const { fixture, host, find } = draw();
    const bell = find('[data-testid="notification-bell"]');
    expect(bell.getAttribute('aria-label')).toBe('Notifications, 3 unread');
    expect(find('[data-testid="notification-count"]').textContent?.trim()).toBe('3');
    host.count.set(12);
    fixture.detectChanges();
    expect(find('[data-testid="notification-count"]').textContent?.trim()).toBe('9+');
    host.count.set(0);
    fixture.detectChanges();
    expect(find('[data-testid="notification-count"]')).toBeNull();
    bell.click();
    expect(host.opened()).toBe(true);
  });

  it('the panel is a menu: the arrows move between the notices, Escape closes, an unread one says so', () => {
    const { host, find } = draw();
    const first = find('[data-testid="first"] [role="menuitem"]');
    const second = find('[data-testid="second"] [role="menuitem"]');
    expect(first.textContent).toContain('Unread:');
    expect(find('[data-testid="first"]').classList).toContain('unread');
    first.focus();
    press(first, 'ArrowDown');
    expect(document.activeElement).toBe(second);
    press(second, 'ArrowDown');
    expect(document.activeElement).toBe(first);
    host.opened.set(true);
    press(first, 'Escape');
    expect(host.opened()).toBe(false);
    find('[data-testid="notifications-mark-all"]').click();
    expect(host.count()).toBe(0);
  });
});
