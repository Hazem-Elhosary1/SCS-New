import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsKpiCardComponent, ScsKpiTrend } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The KPI Card page (294:315 / 8:135): three trends; working: the clinic's caption and its card with
 * no change line, and the list template's icon (spec 011).
 */
@Component({
  selector: 'app-gallery-kpi-card-section',
  imports: [ScsKpiCardComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-kpi-card {
      inline-size: 260px;
      max-inline-size: 100%;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md);
    }
  `,
  template: `
    <app-gallery-component page="KPI Card" set="KPI Card">
      <ng-template appGallerySpecimen let-values>
        <scs-kpi-card
          [label]="card(values['Trend']).label"
          [value]="card(values['Trend']).value"
          [note]="card(values['Trend']).note"
          [trend]="trendOf(values['Trend'])"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          <scs-kpi-card [label]="words().label" value="1,250" [note]="words().delta" [caption]="words().caption" icon="chart" />
          <scs-kpi-card [label]="words().label" value="1,250" trend="none" />
          <scs-kpi-card [label]="card('Positive').label" [value]="card('Positive').value" [note]="card('Positive').note" icon="building" tone="success" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class KpiCardSection {
  private readonly state = inject(GalleryState);
  /** The page's cards (KPI Card 294:315; its Arabic twin 8:135 repeats the first card's words). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          Positive: { label: 'إجمالي العيادات النشطة', value: '148', note: '+6 هذا الشهر' },
          Negative: { label: 'التذاكر المفتوحة', value: '12', note: '-3 هذا الشهر' },
          Neutral: { label: 'الإيراد الشهري المتكرر', value: '73,900 ج.م', note: 'بدون تغيير' },
          label: 'التسمية',
          delta: '12%',
          caption: 'عن الفترة السابقة',
        }
      : {
          Positive: { label: 'Total Active Clinics', value: '148', note: '+6 this month' },
          Negative: { label: 'Active Tickets', value: '12', note: '-3 this month' },
          Neutral: { label: 'MRR', value: 'EGP 73,900', note: 'No change' },
          label: 'Label',
          delta: '12%',
          caption: 'vs last period',
        },
  );

  protected card(trend: string): { label: string; value: string; note: string } {
    const words = this.words();
    return trend === 'Negative' ? words.Negative : trend === 'Neutral' ? words.Neutral : words.Positive;
  }

  protected trendOf(trend: string): ScsKpiTrend {
    return trend.toLowerCase() as ScsKpiTrend;
  }
}
