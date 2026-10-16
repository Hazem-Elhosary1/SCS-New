import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsNavItemComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The Sidebar Nav Item page (294:323 / 8:119): default, hover, active, disabled; working: a group with
 * its count that opens on its sub-items (the clinic's options).
 */
@Component({
  selector: 'app-gallery-sidebar-nav-item-section',
  imports: [ScsNavItemComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-nav-item,
    .bar {
      inline-size: 216px;
      max-inline-size: 100%;
    }
    .bar {
      display: grid;
      gap: 2px;
    }
  `,
  template: `
    <app-gallery-component page="Sidebar Nav Item" set="Sidebar Nav Item">
      <ng-template appGallerySpecimen let-values>
        <scs-nav-item
          icon="home"
          [label]="values['State'] === 'Active' ? words().home : words().clinics"
          [active]="values['State'] === 'Active'"
          [disabled]="values['State'] === 'Disabled'"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <nav class="bar" [attr.aria-label]="words().clinics">
          <scs-nav-item icon="home" [label]="words().home" link="/dev/components" exact />
          <scs-nav-item icon="building" [label]="words().clinics" link="/dev/components/sidebar-nav-item" />
          <scs-nav-item icon="settings" [label]="words().settings" expandable count="3" [(expanded)]="open" />
          @if (open()) {
            <scs-nav-item level="sub" [label]="words().roles" link="/dev/components/button" />
            <scs-nav-item level="sub" [label]="words().plans" link="/dev/components/badge" />
          }
        </nav>
      </ng-template>
    </app-gallery-component>
  `,
})
export class SidebarNavItemSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Sidebar Nav Item 294:323 and its Arabic twin 8:119). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { home: 'الرئيسية', clinics: 'العيادات', settings: 'الإعدادات', roles: 'الأدوار', plans: 'الباقات' }
      : { home: 'Home', clinics: 'Clinics', settings: 'Settings', roles: 'Roles', plans: 'Plans' },
  );
  protected readonly open = signal(true);
}
