import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsSliderComponent, ScsUsageLevel, ScsUsageMeterComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/** The Usage Meter page (387:24 / 388:23): normal, near the limit, at its edge; working: a meter to fill. */
@Component({
  selector: 'app-gallery-usage-meter-section',
  imports: [ScsUsageMeterComponent, ScsSliderComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-usage-meter {
      inline-size: 100%;
      max-inline-size: 320px;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-sm);
      max-inline-size: 320px;
    }
  `,
  template: `
    <app-gallery-component page="Usage Meter" set="Usage Meter">
      <ng-template appGallerySpecimen let-values>
        <scs-usage-meter
          [label]="meter(values['Level']).label"
          [used]="meter(values['Level']).used"
          [limit]="meter(values['Level']).limit"
          [value]="meter(values['Level']).value"
          [note]="meter(values['Level']).note"
          [level]="levelOf(values['Level'])"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-usage-meter [label]="words().messages" [used]="used()" [limit]="200" [value]="used() + ' / 200'" [note]="words().share(used())" />
          <scs-slider [label]="words().messages" [min]="0" [max]="220" [step]="4" [value]="used()" (valueChange)="used.set($event)" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class UsageMeterSection {
  private readonly state = inject(GalleryState);
  /** The page's meters (Usage Meter 387:24 and its Arabic twin 388:23). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          meters: {
            Normal: { label: 'التذاكر هذا الشهر', used: 3200, limit: 5000, value: '3,200 / 5,000', note: 'نسبة الاستخدام 64%' },
            Warning: { label: 'التخزين المستخدم', used: 85, limit: 100, value: '85 / 100 جيجا', note: 'نسبة الاستخدام 85% — قريب من الحد' },
            Critical: { label: 'طلبات الـ API هذا الشهر', used: 49000, limit: 50000, value: '49,000 / 50,000', note: 'نسبة الاستخدام 98% — قم بالترقية قريباً' },
          } as Record<string, { label: string; used: number; limit: number; value: string; note: string }>,
          messages: 'الرسائل',
          share: (used: number) => `نسبة الاستخدام ${Math.round((used / 200) * 100)}%`,
        }
      : {
          meters: {
            Normal: { label: 'Tickets this month', used: 3200, limit: 5000, value: '3,200 / 5,000', note: '64% used' },
            Warning: { label: 'Storage used', used: 85, limit: 100, value: '85 / 100 GB', note: '85% used — approaching limit' },
            Critical: { label: 'API calls this month', used: 49000, limit: 50000, value: '49,000 / 50,000', note: '98% used — upgrade soon' },
          } as Record<string, { label: string; used: number; limit: number; value: string; note: string }>,
          messages: 'Messages',
          share: (used: number) => `${Math.round((used / 200) * 100)}% used`,
        },
  );
  protected readonly used = signal(120);

  protected meter(level: string): { label: string; used: number; limit: number; value: string; note: string } {
    return this.words().meters[level];
  }

  protected levelOf(level: string): ScsUsageLevel {
    return level.toLowerCase() as ScsUsageLevel;
  }
}
