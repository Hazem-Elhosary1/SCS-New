import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsTextareaComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

const SIZES = { Small: 'sm', Medium: 'md', Large: 'lg' } as const;

/** The Textarea page (41:3): six states in three sizes, and a working field with its count. */
@Component({
  selector: 'app-gallery-textarea-section',
  imports: [ScsTextareaComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    /* Each specimen as wide as the page draws its field (280). */
    :host {
      --gallery-cell: min(300px, 100%);
    }
    .field {
      inline-size: 100%;
      max-inline-size: 320px;
    }
  `,
  template: `
    <app-gallery-component page="Textarea" set="Textarea">
      <ng-template appGallerySpecimen let-values>
        <scs-textarea
          class="field"
          [label]="words().label"
          [size]="size(values['Size'])"
          [placeholder]="words().placeholder"
          [value]="values['State'] === 'Filled' ? words().long : ''"
          [max]="500"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().required : ''"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-textarea
          class="field"
          testId="live-textarea"
          [label]="words().label"
          [placeholder]="words().placeholder"
          [hint]="words().hint"
          [max]="500"
          [(value)]="text"
        />
      </ng-template>
    </app-gallery-component>
  `,
})
export class TextareaSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Textarea 146:95 and its Arabic twin 304:369, whose sample text the gallery puts in Arabic). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          label: 'الوصف',
          placeholder: 'أدخل الوصف هنا...',
          long: 'مثال أطول لنص يكتبه المستخدم على أكثر من سطر، يلتف داخل مربع النص عبر عدة أسطر.',
          required: 'هذا الحقل مطلوب',
          hint: 'يظهر في سجل النشاط',
        }
      : {
          label: 'Description',
          placeholder: 'Enter your description here...',
          long: 'This is a longer example of user-entered multi-line text content that wraps across several lines inside the textarea box.',
          required: 'This field is required',
          hint: 'Shown in the activity log',
        },
  );
  protected size(value: string): 'sm' | 'md' | 'lg' {
    return SIZES[value as keyof typeof SIZES];
  }
  protected readonly text = signal('');
}
