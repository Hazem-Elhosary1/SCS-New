import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsBarChartComponent } from 'ui';
import { SparklineComponent } from '../../components/chart/sparkline.component';
import { GalleryComponentComponent, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/** The Chart page (394:2): the console's own sparkline up, down and flat, and the bar chart (394:16). */
@Component({
  selector: 'app-gallery-chart-section',
  imports: [SparklineComponent, ScsBarChartComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-bar-chart {
      inline-size: 100%;
      max-inline-size: 400px;
    }
  `,
  template: `
    <app-gallery-component page="Chart" set="Sparkline">
      <ng-template appGallerySpecimen let-values>
        <app-sparkline [points]="lines[values['Trend']]" [label]="words().spark[values['Trend']]" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Composed Example — Bar Chart" [node]="ar() ? '395:15' : '394:16'" testId="specimen-chart/parts/bar-chart">
      <scs-bar-chart [label]="words().title" [bars]="bars()" />
    </app-gallery-part>
  `,
})
export class ChartSection {
  private readonly state = inject(GalleryState);
  protected readonly ar = computed(() => this.state.language() === 'ar');

  /** The page's three lines (394:15). */
  protected readonly lines: Record<string, number[]> = {
    Up: [3, 5, 4, 7, 6, 9, 10],
    Down: [10, 8, 9, 6, 7, 4, 2],
    Flat: [5, 6, 5, 4, 6, 5, 5],
  };

  /** The page's words (Chart 394:2 and its Arabic twins 395:14, 395:15). */
  protected readonly words = computed(() =>
    this.ar()
      ? {
          title: 'التذاكر المحلولة هذا الأسبوع',
          days: ['اثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة', 'سبت', 'أحد'],
          spark: { Up: 'في ارتفاع', Down: 'في انخفاض', Flat: 'ثابت' } as Record<string, string>,
        }
      : {
          title: 'Tickets resolved this week',
          days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
          spark: { Up: 'Rising', Down: 'Falling', Flat: 'Level' } as Record<string, string>,
        },
  );

  /** The page's week: Saturday the highest, 112. */
  protected readonly bars = computed(() => [60, 80, 50, 96, 70, 112, 40].map((value, index) => ({ label: this.words().days[index], value })));
}
