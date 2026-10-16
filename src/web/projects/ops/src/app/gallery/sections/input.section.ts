import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsTextFieldComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

const SIZES = { Small: 'sm', Medium: 'md', Large: 'lg' } as const;

/** The Input page (41:2): twelve states in three sizes, and a working field that checks itself. */
@Component({
  selector: 'app-gallery-input-section',
  imports: [ScsTextFieldComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .field {
      inline-size: 100%;
      max-inline-size: 320px;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-md);
      max-inline-size: 360px;
    }
  `,
  template: `
    <app-gallery-component page="Input" set="Input">
      <ng-template appGallerySpecimen let-values>
        <scs-text-field
          class="field"
          required
          [label]="words().label"
          [size]="size(values['Size'])"
          [placeholder]="words().placeholder"
          [value]="valueFor(values['State'])"
          [disabled]="values['State'] === 'Disabled'"
          [readonly]="values['State'] === 'ReadOnly'"
          [busy]="values['State'] === 'Loading'"
          [error]="values['State'] === 'Error' ? words().invalid : ''"
          [state]="values['State'] === 'Success' ? 'success' : values['State'] === 'Warning' ? 'warning' : null"
          [stateMessage]="values['State'] === 'Success' ? words().good : values['State'] === 'Warning' ? words().old : ''"
          [attr.data-scs-state]="values['State'] === 'Active' ? 'active' : null"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-text-field
            testId="live-email"
            kind="email"
            size="md"
            required
            [lead]="false"
            [label]="words().label"
            [placeholder]="words().placeholder"
            [hint]="words().hint"
            [(value)]="email"
            [error]="emailError()"
            [state]="emailGood() ? 'success' : null"
            [stateMessage]="words().good"
          />
          <scs-text-field testId="live-password" kind="password" size="md" [lead]="false" [label]="words().password" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class InputSection {
  private readonly state = inject(GalleryState);

  /** The page's words (Input 54:257 and its Arabic twin 304:257, whose messages the gallery puts in Arabic). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          label: 'البريد الإلكتروني',
          placeholder: 'نص العنصر النائب',
          invalid: 'أدخل بريدًا إلكترونيًا صحيحًا',
          good: 'يبدو صحيحًا',
          old: 'سيتوقف هذا النطاق قريبًا',
          checking: 'جارٍ التحقق…',
          hint: 'سيصل إليه رابط الدعوة',
          password: 'كلمة المرور',
        }
      : {
          label: 'Email address',
          placeholder: 'Placeholder text',
          invalid: 'Please enter a valid email address',
          good: 'Looks good!',
          old: 'This domain will be deprecated soon',
          checking: 'Verifying...',
          hint: 'The invitation link is sent there',
          password: 'Password',
        },
  );

  protected size(value: string): 'sm' | 'md' | 'lg' {
    return SIZES[value as keyof typeof SIZES];
  }

  /** What each drawn state shows in the field, as the page writes it. */
  protected valueFor(state: string): string {
    if (['Empty', 'Default', 'Hover'].includes(state)) return '';
    if (state === 'Error') return 'invalid-email';
    if (state === 'Warning') return 'user@old-domain.com';
    if (state === 'Loading') return this.words().checking;
    return 'user@example.com';
  }

  protected readonly email = signal('');
  protected readonly emailError = computed(() => (this.email() && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(this.email()) ? this.words().invalid : ''));
  protected readonly emailGood = computed(() => !!this.email() && !this.emailError());
}
