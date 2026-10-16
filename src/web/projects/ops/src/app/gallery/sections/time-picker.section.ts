import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsTimeFieldComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Time Picker page (77:2): the trigger's six states, and the two panels drawn beside it — the
 * list of times (default, under the pointer, chosen, refused) and the wheels; a working field of
 * each. The clinic look takes the clinic's digits.
 */
@Component({
  selector: 'app-gallery-time-picker-section',
  imports: [ScsTimeFieldComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
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
    <app-gallery-component page="Time Picker" set="Time Field Trigger">
      <ng-template appGallerySpecimen let-values>
        <scs-time-field
          class="field"
          [label]="words().label"
          [placeholder]="words().placeholder"
          [value]="values['State'] === 'Filled' ? '14:30' : ''"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
          [digits]="digits()"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-time-field
            testId="live-time"
            [label]="words().label"
            [placeholder]="words().placeholder"
            min="08:00"
            max="20:00"
            [digits]="digits()"
            [(value)]="time"
          />
          <scs-time-field
            testId="live-time-wheel"
            mode="wheel"
            [step]="5"
            [label]="words().label"
            [placeholder]="words().placeholder"
            [digits]="digits()"
            [(value)]="turned"
          />
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Time List — Default / Hover / Selected / Disabled" node="78:4" testId="specimen-time-picker/parts/time-list-panel">
      <scs-time-field
        class="field"
        panelInline
        [label]="words().label"
        value="11:00"
        min="09:00"
        max="12:00"
        hoverTime="10:30"
        [disabledTimes]="['09:00', '09:30']"
        [digits]="digits()"
      />
    </app-gallery-part>
    <app-gallery-part name="Time Spinner — Scroll Columns" node="78:16" testId="specimen-time-picker/parts/time-spinner-panel">
      <scs-time-field class="field" panelInline mode="wheel" [step]="5" [label]="words().label" value="12:30" [digits]="digits()" />
    </app-gallery-part>
  `,
})
export class TimePickerSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Time Field Trigger 77:53 and its Arabic twin 308:103). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'الوقت', placeholder: 'اختر وقتاً', error: 'اختر وقتاً صحيحاً' }
      : { label: 'Time', placeholder: 'Select a time', error: 'Please select a valid time' },
  );
  protected readonly digits = computed(() => (this.state.look() === 'clinic' ? 'arab' : 'latn'));

  protected readonly time = signal('');
  protected readonly turned = signal('');
}
