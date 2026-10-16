import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsAvatarComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/** The Avatar page (294:131 / 8:111): three sizes; working: a picture, one that fails, and the grey tone. */
@Component({
  selector: 'app-gallery-avatar-section',
  imports: [ScsAvatarComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-md);
    }
  `,
  template: `
    <app-gallery-component page="Avatar" set="Avatar">
      <ng-template appGallerySpecimen let-values>
        <scs-avatar [name]="name()" [size]="values['Size'] === 'Small' ? 'sm' : values['Size'] === 'Large' ? 'lg' : 'md'" />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          <scs-avatar [name]="name()" [src]="picture" />
          <scs-avatar [name]="name()" src="/missing-picture.png" />
          <scs-avatar [name]="name()" tone="neutral" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class AvatarSection {
  private readonly state = inject(GalleryState);
  /** The page's person: "AS" · «أح». */
  protected readonly name = computed(() => (this.state.language() === 'ar' ? 'أحمد حسن' : 'Ahmed Salem'));
  /** A small drawn picture, so the example needs nothing from the network. */
  protected readonly picture =
    'data:image/svg+xml,' +
    encodeURIComponent(
      '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40"><rect width="40" height="40" fill="#cfd8dc"/><circle cx="20" cy="15" r="7" fill="#90a4ae"/><rect x="8" y="25" width="24" height="15" rx="12" fill="#90a4ae"/></svg>',
    );
}
