import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsRadioGroupComponent, ScsSegmentedComponent, ScsSliderComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/** The Radio page (35:3): six states and a working group. */
@Component({
  selector: 'app-gallery-radio-section',
  imports: [ScsRadioGroupComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-gallery-component page="Radio" set="Radio">
      <ng-template appGallerySpecimen let-values>
        <scs-radio-group
          [ariaLabel]="words().option"
          [options]="[{ value: 'a', label: words().option }]"
          [value]="values['State'] === 'Selected' ? 'a' : null"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-radio-group testId="live-radio" [label]="words().group" [options]="words().choices" [(value)]="chosen" />
      </ng-template>
    </app-gallery-component>
  `,
})
export class RadioSection {
  private readonly state = inject(GalleryState);
  /** The page's own words (Radio 43:66 and its Arabic twin 298:29). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          option: 'خيار أ',
          error: 'هذا الحقل مطلوب',
          group: 'طريقة الدفع',
          choices: [
            { value: 'card', label: 'بطاقة' },
            { value: 'transfer', label: 'تحويل بنكي' },
            { value: 'cash', label: 'نقدًا', disabled: true, reason: 'غير متاح لهذه الباقة' },
          ],
        }
      : {
          option: 'Option A',
          error: 'Please select an option',
          group: 'Payment method',
          choices: [
            { value: 'card', label: 'Card' },
            { value: 'transfer', label: 'Bank transfer' },
            { value: 'cash', label: 'Cash', disabled: true, reason: 'Not available on this plan' },
          ],
        },
  );
  protected readonly chosen = signal<string | null>('card');
}

/** The Slider page (35:5): single and range in four states, and two working sliders. */
@Component({
  selector: 'app-gallery-slider-section',
  imports: [ScsSliderComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .slider {
      inline-size: 260px;
      max-inline-size: 100%;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-lg);
      max-inline-size: 360px;
    }
  `,
  template: `
    <app-gallery-component page="Slider" set="Slider">
      <ng-template appGallerySpecimen let-values>
        <scs-slider
          class="slider"
          [value]="60"
          [range]="values['Type'] === 'Range' ? [30, 75] : null"
          [disabled]="values['State'] === 'Disabled'"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-slider testId="live-slider" [label]="words().single" [(value)]="single" />
          <scs-slider testId="live-range" [label]="words().range" [(range)]="range" [thumbNames]="words().ends" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class SliderSection {
  private readonly state = inject(GalleryState);
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { single: 'حد التنبيه', range: 'نطاق العمر', ends: ['من', 'إلى'] as [string, string] }
      : { single: 'Alert threshold', range: 'Age range', ends: ['From', 'To'] as [string, string] },
  );
  protected readonly single = signal(60);
  protected readonly range = signal<readonly [number, number] | null>([30, 75]);
}

/** The Segmented Control page (385:2): the two states of a segment, the track, a working control. */
@Component({
  selector: 'app-gallery-segmented-section',
  imports: [ScsSegmentedComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-gallery-component page="Segmented Control" set="Segment">
      <ng-template appGallerySpecimen let-values>
        <scs-segmented
          [ariaLabel]="words().period"
          [segments]="[{ value: 'week', label: words().week }]"
          [value]="values['State'] === 'Selected' ? 'week' : ''"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-segmented testId="live-segmented" [ariaLabel]="words().period" [segments]="words().periods" [(value)]="period" />
      </ng-template>
    </app-gallery-component>
    <app-gallery-part name="Composed Example" node="385:12" testId="specimen-segmented-control/parts/track">
      <scs-segmented [ariaLabel]="words().period" [segments]="words().periods" value="week" />
    </app-gallery-part>
  `,
})
export class SegmentedSection {
  private readonly state = inject(GalleryState);
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          period: 'الفترة',
          week: 'أسبوع',
          periods: [
            { value: 'day', label: 'يوم' },
            { value: 'week', label: 'أسبوع' },
            { value: 'month', label: 'شهر' },
          ],
        }
      : {
          period: 'Period',
          week: 'Week',
          periods: [
            { value: 'day', label: 'Day' },
            { value: 'week', label: 'Week' },
            { value: 'month', label: 'Month' },
          ],
        },
  );
  protected readonly period = signal('week');
}
