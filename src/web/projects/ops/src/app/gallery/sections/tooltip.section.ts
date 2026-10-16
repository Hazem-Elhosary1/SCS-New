import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsIconComponent, ScsTooltipDirective, ScsTooltipPlacement } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The Tooltip page (383:23 / 384:22): above, below, left and right, still; working: an icon-only
 * button on each side whose tooltip shows on hover and on keyboard focus.
 */
@Component({
  selector: 'app-gallery-tooltip-section',
  imports: [ScsTooltipDirective, ScsIconComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .still {
      padding: 8px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-lg);
      padding: var(--scs-spacing-lg) var(--scs-spacing-xl, 32px);
    }
  `,
  template: `
    <app-gallery-component page="Tooltip" set="Tooltip">
      <ng-template appGallerySpecimen let-values>
        <div class="still"><span class="scs-tooltip" role="tooltip" [attr.data-placement]="sideOf(values['Placement'])">{{ words().tip }}</span></div>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          @for (side of sides; track side) {
            <button type="button" class="scs-icon-button" [scsTooltip]="words().tip + ' (' + side + ')'" [scsTooltipPlacement]="side">
              <scs-icon name="info" [size]="16" />
            </button>
          }
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class TooltipSection {
  private readonly state = inject(GalleryState);
  protected readonly sides: ScsTooltipPlacement[] = ['top', 'bottom', 'start', 'end'];
  /** The page's words (Tooltip 383:23 and its Arabic twin 384:22). */
  protected readonly words = computed(() => (this.state.language() === 'ar' ? { tip: 'نصيحة مفيدة هنا' } : { tip: 'Helpful tip goes here' }));

  /** The page names its sides left and right; the tooltip, start and end of the line. */
  protected sideOf(placement: string): ScsTooltipPlacement {
    const ltr = this.state.language() === 'en';
    if (placement === 'Left') return ltr ? 'start' : 'end';
    if (placement === 'Right') return ltr ? 'end' : 'start';
    return placement === 'Bottom' ? 'bottom' : 'top';
  }
}
