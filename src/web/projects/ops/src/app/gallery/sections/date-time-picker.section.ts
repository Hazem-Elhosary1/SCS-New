import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DateTimeFieldComponent } from '../../components/date-time/date-time-field.component';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Date & Time Picker page (79:2), the operators console's own: the trigger's six states and the
 * panel of a calendar beside its times; a working field. August 2026 is pinned, today the 28th.
 */
@Component({
  selector: 'app-gallery-date-time-picker-section',
  imports: [DateTimeFieldComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .field {
      inline-size: 100%;
      max-inline-size: 280px;
    }
    .composite {
      inline-size: 100%;
      min-inline-size: 0;
      max-inline-size: 500px;
    }
  `,
  template: `
    <app-gallery-component page="Date & Time Picker" set="Date & Time Field Trigger">
      <ng-template appGallerySpecimen let-values>
        <app-date-time-field
          class="field"
          [label]="words().label"
          [placeholder]="words().placeholder"
          [value]="values['State'] === 'Filled' ? '2026-08-28T14:30' : ''"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
          today="2026-08-28"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <app-date-time-field class="field" testId="live-when" [label]="words().label" [placeholder]="words().placeholder" firstTime="08:00" lastTime="20:00" [(value)]="when" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Calendar and times" node="80:4" testId="specimen-date-and-time-picker/parts/composite-popover" [shared]="false">
      <app-date-time-field
        class="composite"
        panelInline
        [label]="words().label"
        value="2026-08-28T14:30"
        today="2026-08-28"
        firstTime="13:00"
        lastTime="16:00"
        hoverTime="14:00"
        [disabledTimes]="['16:00']"
      />
    </app-gallery-part>
  `,
})
export class DateTimePickerSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Date & Time Field Trigger 79:71 and its Arabic twin 308:172; its English error put in Arabic). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'التاريخ والوقت', placeholder: 'اختر تاريخاً ووقتاً', error: 'يرجى اختيار تاريخ ووقت صالحين' }
      : { label: 'Date & Time', placeholder: 'Select date & time', error: 'Please select a valid date & time' },
  );
  protected readonly when = signal('');
}
