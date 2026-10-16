import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsNavbarComponent, ScsNotificationBellComponent } from 'ui';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Navbar page (278:2): the console's bar, with an unread count on its bell (2403:6546) and with an
 * uploaded logo (2467:133). The bars are drawn in place, at the width of the section.
 */
@Component({
  selector: 'app-gallery-navbar-section',
  imports: [ScsNavbarComponent, ScsNotificationBellComponent, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-navbar {
      inline-size: 100%;
      border: 1px solid var(--scs-color-border-default);
    }
  `,
  template: `
    <app-gallery-part [name]="ar() ? 'Navbar (عربي)' : 'Navbar'" [node]="ar() ? '311:16' : '278:3'" testId="specimen-navbar/parts/navbar">
      <scs-navbar [name]="words().name" mark="S" home="/dev/components/navbar" [person]="words().person" [role]="words().role" [accountLabel]="words().account">
        <scs-notification-bell hasUnread [label]="words().bell(0)" />
      </scs-navbar>
    </app-gallery-part>
    <app-gallery-part
      [name]="ar() ? 'State / Navbar (عربي) — Unread count on the bell' : 'State / Navbar — Unread count on the bell'"
      [node]="ar() ? '2403:6562' : '2403:6546'"
      testId="specimen-navbar/parts/unread-count"
    >
      <scs-navbar [name]="words().name" mark="S" home="/dev/components/navbar" [person]="words().person" [role]="words().role" [accountLabel]="words().account" [accountOpen]="open()" (account)="open.set(!open())">
        <scs-notification-bell [count]="3" [label]="words().bell(3)" />
      </scs-navbar>
    </app-gallery-part>
    <app-gallery-part
      [name]="ar() ? 'State / Navbar (عربي) — uploaded logo' : 'State / Navbar — uploaded logo'"
      [node]="ar() ? '2467:161' : '2467:133'"
      testId="specimen-navbar/parts/uploaded-logo"
    >
      <scs-navbar [name]="words().name" [logo]="logo" home="/dev/components/navbar" [person]="words().person" [role]="words().role" [accountLabel]="words().account">
        <scs-notification-bell hasUnread [label]="words().bell(0)" />
      </scs-navbar>
    </app-gallery-part>
  `,
})
export class NavbarSection {
  private readonly state = inject(GalleryState);
  protected readonly ar = computed(() => this.state.language() === 'ar');
  /** The page's words (Navbar 278:3 and its Arabic twin 311:16). */
  protected readonly words = computed(() =>
    this.ar()
      ? {
          name: 'SCS — لوحة التشغيل',
          person: 'أحمد سامي',
          role: 'مسؤول عام',
          account: 'حسابي',
          bell: (count: number) => (count ? `الإشعارات، ${count} غير مقروءة` : 'الإشعارات، توجد إشعارات غير مقروءة'),
        }
      : {
          name: 'SCS — Ops Console',
          person: 'Ahmed Sami',
          role: 'Super Admin',
          account: 'My account',
          bell: (count: number) => (count ? `Notifications, ${count} unread` : 'Notifications, some unread'),
        },
  );
  protected readonly open = signal(false);

  /** An uploaded logo, drawn as the page draws it: a teal glyph and a word mark. */
  protected readonly logo =
    'data:image/svg+xml,' +
    encodeURIComponent(
      '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 66 36" width="66" height="36"><rect y="4" width="28" height="28" rx="8" fill="#0b6b60"/><rect x="36" y="13" width="30" height="10" rx="3" fill="#1a2024"/></svg>',
    );
}
