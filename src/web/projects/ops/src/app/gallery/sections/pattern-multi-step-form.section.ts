import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsStep, ScsStepperComponent, ScsTextFieldComponent } from 'ui';
import { GalleryState } from '../gallery-state';

type Field = 'name' | 'city' | 'phone' | 'address';

/**
 * Pattern — Multi-step Form (`123:2`; CL-SH-061): the stepper over a card holding one step's fields,
 * «رجوع» at the reading start and «التالي» at the end. "Next" checks only the step shown; a field in
 * error says why under it. The last step reviews what was typed.
 */
@Component({
  selector: 'app-gallery-pattern-multi-step-form-section',
  imports: [ScsStepperComponent, ScsTextFieldComponent, ScsButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-spacing-lg);
    }
    /* On a phone every block keeps to the window: what is wider scrolls inside its own part. */
    :host > *,
    :host > * > * {
      min-inline-size: 0;
      max-inline-size: 100%;
    }
    .block {
      display: grid;
      gap: var(--scs-spacing-md);
      inline-size: min(560px, 100%);
    }
    .caption {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .card {
      display: grid;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-lg, 12px);
      background: var(--scs-color-bg-surface);
    }
    h3 {
      margin: 0;
      font-size: var(--scs-text-h5, 18px);
      font-weight: 600;
    }
    .lead {
      margin: calc(-1 * var(--scs-spacing-12)) 0 0;
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
    }
    scs-stepper {
      inline-size: 100%;
    }
    scs-text-field {
      max-inline-size: 280px;
    }
    .actions {
      display: flex;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-sm);
    }
    .actions button {
      min-inline-size: 100px;
    }
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: var(--scs-spacing-xs) var(--scs-spacing-md);
      margin: 0;
    }
    dt {
      color: var(--scs-color-text-tertiary);
    }
    dd {
      margin: 0;
    }
  `,
  template: `
    <div class="block" data-testid="specimen-pattern-multi-step-form/pattern/step-two">
      <p class="caption">{{ words().stillCaption }}</p>
      <scs-stepper [label]="words().steps" [doneLabel]="words().done" [steps]="steps()" [current]="1" />
      <div class="card">
        <h3>{{ words().contact }}</h3>
        <p class="lead">{{ words().contactLead }}</p>
        <scs-text-field [label]="words().phone" ltr placeholder="+20 10 1234 5678" />
        <scs-text-field [label]="words().address" [placeholder]="words().addressExample" />
        <div class="actions">
          <button type="button" scsButton="secondary" size="md">{{ words().back }}</button>
          <button type="button" scsButton="primary" size="md">{{ words().next }}</button>
        </div>
      </div>
    </div>

    <div class="block" data-testid="live-pattern-multi-step-form">
      <p class="caption">{{ words().live }}</p>
      <scs-stepper [label]="words().steps" [doneLabel]="words().done" [steps]="steps()" [current]="step()" />
      <div class="card">
        @switch (step()) {
          @case (0) {
            <h3>{{ words().clinic }}</h3>
            <p class="lead">{{ words().clinicLead }}</p>
            <scs-text-field testId="live-step-name" [label]="words().name" required [error]="errorOf('name')" [(value)]="name" />
            <scs-text-field testId="live-step-city" [label]="words().city" [(value)]="city" />
          }
          @case (1) {
            <h3>{{ words().contact }}</h3>
            <p class="lead">{{ words().contactLead }}</p>
            <scs-text-field testId="live-step-phone" [label]="words().phone" required ltr placeholder="+20 10 1234 5678" [error]="errorOf('phone')" [(value)]="phone" />
            <scs-text-field testId="live-step-address" [label]="words().address" [placeholder]="words().addressExample" [(value)]="address" />
          }
          @default {
            <h3>{{ words().review }}</h3>
            <dl data-testid="live-step-review">
              <dt>{{ words().name }}</dt>
              <dd>{{ name() }}</dd>
              <dt>{{ words().city }}</dt>
              <dd>{{ city() || '—' }}</dd>
              <dt>{{ words().phone }}</dt>
              <dd dir="ltr">{{ phone() }}</dd>
              <dt>{{ words().address }}</dt>
              <dd>{{ address() || '—' }}</dd>
            </dl>
          }
        }
        <div class="actions">
          <button type="button" scsButton="secondary" size="md" data-testid="live-step-back" [disabled]="step() === 0" (click)="back()">{{ words().back }}</button>
          <button type="button" scsButton="primary" size="md" data-testid="live-step-next" (click)="next()">
            {{ step() === 2 ? words().finish : words().next }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class PatternMultiStepFormSection {
  private readonly state = inject(GalleryState);

  protected readonly step = signal(0);
  protected readonly name = signal('');
  protected readonly city = signal('');
  protected readonly phone = signal('');
  protected readonly address = signal('');
  /** The fields "Next" found empty on the step shown. */
  protected readonly missing = signal<readonly Field[]>([]);

  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          stillCaption: 'الخطوة الثانية من ثلاث',
          live: 'مثال يعمل',
          steps: 'خطوات إضافة عيادة',
          done: 'مكتملة',
          clinic: 'معلومات العيادة',
          clinicLead: 'ما اسم العيادة وأين تقع؟',
          contact: 'معلومات التواصل',
          contactLead: 'كيف يمكننا التواصل مع العيادة عند الحاجة؟',
          review: 'المراجعة والتأكيد',
          name: 'اسم العيادة',
          city: 'المدينة',
          phone: 'رقم الهاتف',
          address: 'العنوان',
          addressExample: '15 شارع التحرير، مدينة نصر، القاهرة',
          back: 'رجوع',
          next: 'التالي',
          finish: 'إضافة العيادة',
          required: 'هذا الحقل مطلوب',
        }
      : {
          stillCaption: 'Step two of three',
          live: 'Working example',
          steps: 'Steps to add a clinic',
          done: 'Complete',
          clinic: 'Clinic Info',
          clinicLead: 'What is the clinic called, and where is it?',
          contact: 'Contact Info',
          contactLead: 'How can we reach the clinic when needed?',
          review: 'Review & Confirm',
          name: 'Clinic name',
          city: 'City',
          phone: 'Phone Number',
          address: 'Address',
          addressExample: '15 Tahrir St, Nasr City, Cairo',
          back: 'Back',
          next: 'Next',
          finish: 'Add Clinic',
          required: 'This field is required',
        },
  );

  protected readonly steps = computed<ScsStep[]>(() => [
    { label: this.words().clinic },
    { label: this.words().contact },
    { label: this.words().review },
  ]);

  protected errorOf(field: Field): string {
    return this.missing().includes(field) ? this.words().required : '';
  }

  protected back(): void {
    this.missing.set([]);
    this.step.update((at) => Math.max(0, at - 1));
  }

  /** Checks only the step shown; an empty required field keeps the form on its step. */
  protected next(): void {
    const needed: Field[] = this.step() === 0 ? ['name'] : this.step() === 1 ? ['phone'] : [];
    const values: Record<Field, string> = { name: this.name(), city: this.city(), phone: this.phone(), address: this.address() };
    const missing = needed.filter((field) => values[field].trim() === '');
    this.missing.set(missing);
    if (missing.length === 0 && this.step() < 3) this.step.update((at) => at + 1);
  }
}
