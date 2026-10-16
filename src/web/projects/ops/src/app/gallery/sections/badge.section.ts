import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsBadgeComponent, ScsBadgeIntent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The Badge page (294:147 / 8:86): five intents; working: the clinic's options — brand, solid, small,
 * a leading icon, no dot.
 */
@Component({
  selector: 'app-gallery-badge-section',
  imports: [ScsBadgeComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
  `,
  template: `
    <app-gallery-component page="Badge" set="Badge">
      <ng-template appGallerySpecimen let-values>
        <scs-badge [intent]="intentOf(values['Intent'])">{{ words()[values['Intent']] }}</scs-badge>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          <scs-badge intent="brand">{{ words()['Brand'] }}</scs-badge>
          <scs-badge intent="success" solid>{{ words()['Success'] }}</scs-badge>
          <scs-badge intent="danger" solid size="sm">{{ words()['Danger'] }}</scs-badge>
          <scs-badge intent="info" icon="info">{{ words()['Info'] }}</scs-badge>
          <scs-badge intent="brand" [dot]="false">{{ words()['Brand'] }}</scs-badge>
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class BadgeSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Badge 294:147 and its Arabic twin 8:86). */
  protected readonly words = computed<Record<string, string>>(() =>
    this.state.language() === 'ar'
      ? { Success: 'نشط', Warning: 'قيد الانتظار', Danger: 'موقوف', Info: 'معلومة', Neutral: 'عام', Brand: 'مخصص' }
      : { Success: 'Active', Warning: 'Pending', Danger: 'Suspended', Info: 'Info', Neutral: 'General', Brand: 'Custom' },
  );

  protected intentOf(intent: string): ScsBadgeIntent {
    return intent.toLowerCase() as ScsBadgeIntent;
  }
}
