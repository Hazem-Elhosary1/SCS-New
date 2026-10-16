import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsCalendarComponent, ScsDayRange } from 'ui';
import { DateRangeFieldComponent } from '../../components/date-range/date-range-field.component';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Date Range Picker page (392:2), the operators console's own: the trigger's six states and the
 * range calendar beside it; a working field. August 2026 is pinned, today the 28th.
 */
@Component({
  selector: 'app-gallery-date-range-picker-section',
  imports: [DateRangeFieldComponent, ScsCalendarComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .field {
      inline-size: 100%;
      max-inline-size: 280px;
    }
  `,
  template: `
    <app-gallery-component page="Date Range Picker" set="Date Range Trigger">
      <ng-template appGallerySpecimen let-values>
        <app-date-range-field
          class="field"
          [label]="words().label"
          [placeholder]="words().placeholder"
          [value]="values['State'] === 'Filled' ? filled : null"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
          today="2026-08-28"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <app-date-range-field class="field" testId="live-range" [label]="words().label" [placeholder]="words().placeholder" [(value)]="range" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Composed Example" node="393:2" testId="specimen-date-range-picker/parts/calendar-panel-range" [shared]="false">
      <scs-calendar mode="range" [range]="drawn" today="2026-08-28" />
    </app-gallery-part>
  `,
})
export class DateRangePickerSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Date Range Trigger 392:53 and its Arabic twin 393:112). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'النطاق الزمني', placeholder: 'اختر نطاق تاريخ', error: 'يرجى اختيار نطاق صالح' }
      : { label: 'Date Range', placeholder: 'Select a date range', error: 'Please select a valid range' },
  );
  /** The page's filled range: the 1st to the 28th of August. */
  protected readonly filled: ScsDayRange = { start: '2026-08-01', end: '2026-08-28' };
  /** The calendar's range (393:2): the 10th to the 18th. */
  protected readonly drawn: ScsDayRange = { start: '2026-08-10', end: '2026-08-18' };
  protected readonly range = signal<ScsDayRange | null>(null);
}
