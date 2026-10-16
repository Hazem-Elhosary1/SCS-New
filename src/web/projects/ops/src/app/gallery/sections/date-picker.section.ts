import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { SCS_WEEK_START_SATURDAY, ScsCalendarComponent, ScsDateFieldComponent, ScsDayRange } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Date Picker page (74:2): the trigger's six states, and the calendar drawn beside it — one day
 * (default, today, chosen, under the pointer, refused) and a range; a working field and a working
 * range. The page's August 2026 is pinned, with today the 28th, so the pictures stay still. The
 * clinic look takes the clinic's week (from Saturday), its digits and its link to today.
 */
@Component({
  selector: 'app-gallery-date-picker-section',
  imports: [ScsDateFieldComponent, ScsCalendarComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .field {
      inline-size: 100%;
      max-inline-size: 280px;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-md);
      max-inline-size: 320px;
    }
  `,
  template: `
    <app-gallery-component page="Date Picker" set="Date Field Trigger">
      <ng-template appGallerySpecimen let-values>
        <scs-date-field
          class="field"
          [label]="words().label"
          [placeholder]="words().placeholder"
          [value]="values['State'] === 'Filled' ? '2026-08-28' : ''"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
          [weekStart]="weekStart"
          [digits]="digits()" [todayLink]="clinic()"
          today="2026-08-28"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-date-field
            testId="live-date"
            [label]="words().label"
            [placeholder]="words().placeholder"
            [weekStart]="weekStart"
            [digits]="digits()" [todayLink]="clinic()"
            [(value)]="day"
          />
          <scs-calendar mode="range" testId="live-range" [weekStart]="weekStart" [digits]="digits()" [todayLink]="clinic()" [(range)]="range" />
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Single Date — Default / Today / Selected / Hover / Disabled" node="75:4" testId="specimen-date-picker/parts/calendar-panel">
      <scs-calendar
        value="2026-08-15"
        today="2026-08-28"
        hoverDay="2026-08-22"
        [disabledDays]="['2026-08-05', '2026-08-06']"
        [weekStart]="weekStart"
        [digits]="digits()" [todayLink]="clinic()"
      />
    </app-gallery-part>
    <app-gallery-part name="Range — Start / In-Range / End / Today" node="76:3" testId="specimen-date-picker/parts/calendar-panel-range">
      <scs-calendar mode="range" [range]="drawnRange" today="2026-08-28" [weekStart]="weekStart" [digits]="digits()" [todayLink]="clinic()" />
    </app-gallery-part>
  `,
})
export class DatePickerSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Date Field Trigger 74:53 and its Arabic twin 308:52). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'التاريخ', placeholder: 'اختر تاريخاً', error: 'اختر تاريخاً صحيحاً' }
      : { label: 'Date', placeholder: 'Select a date', error: 'Please select a valid date' },
  );
  /** Each look's digits — Western in the operators', the clinic's own in its look; the week from Saturday in both (owner). */
  protected readonly clinic = computed(() => this.state.look() === 'clinic');
  protected readonly weekStart = SCS_WEEK_START_SATURDAY;
  protected readonly digits = computed(() => (this.clinic() ? 'arab' : 'latn'));

  /** The page's range: the 10th to the 18th of August. */
  protected readonly drawnRange: ScsDayRange = { start: '2026-08-10', end: '2026-08-18' };

  protected readonly day = signal('');
  protected readonly range = signal<ScsDayRange | null>(null);
}
