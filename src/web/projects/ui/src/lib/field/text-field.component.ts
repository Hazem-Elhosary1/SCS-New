import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  input,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

let nextId = 0;

/**
 * One labelled field for text, an email address or a password. Its error is shown under it and
 * tied to it for assistive technology. A password field has an eye that shows and hides what
 * was typed; nothing is sent when it is pressed (BRD B-5-8).
 */
@Component({
  selector: 'scs-text-field',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    label {
      display: block;
      margin-block-end: var(--scs-spacing-xs);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .box {
      display: flex;
      align-items: center;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-bg-surface);
    }
    .box:focus-within {
      border-color: var(--scs-color-brand-default);
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .box.invalid {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    input {
      flex: 1;
      min-inline-size: 0;
      min-block-size: 44px;
      padding: 0 var(--scs-spacing-12);
      border: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      outline: none;
    }
    .eye {
      inline-size: 44px;
      block-size: 44px;
      border: 0;
      background: none;
      color: var(--scs-color-icon-default);
      cursor: pointer;
    }
    .eye:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: -4px;
    }
    .error {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-feedback-danger-text);
    }
  `,
  template: `
    <label [attr.for]="id">{{ label() }}</label>
    <div class="box" [class.invalid]="error() !== ''">
      <input
        #field
        [id]="id"
        [type]="shownType()"
        [value]="value()"
        [attr.dir]="kind() === 'text' ? null : 'ltr'"
        [attr.placeholder]="placeholder() || null"
        [attr.autocomplete]="autocomplete() || null"
        [attr.inputmode]="kind() === 'email' ? 'email' : null"
        [attr.aria-invalid]="error() !== '' ? 'true' : null"
        [attr.aria-describedby]="error() !== '' ? id + '-error' : null"
        [attr.data-testid]="testId() || null"
        autocapitalize="none"
        spellcheck="false"
        (input)="value.set(field.value)"
      />
      @if (kind() === 'password') {
        <button
          type="button"
          class="eye"
          data-testid="password-eye"
          [attr.aria-pressed]="revealed()"
          [attr.aria-label]="
            (revealed() ? 'ui.field.password.hide' : 'ui.field.password.show') | transloco
          "
          (click)="revealed.set(!revealed())"
        >
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
            <circle cx="12" cy="12" r="3" />
            @if (revealed()) {
              <path data-testid="eye-crossed" d="M4 4l16 16" />
            }
          </svg>
        </button>
      }
    </div>
    @if (error(); as error) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'field') + '-error'">
        {{ error }}
      </p>
    }
  `,
})
export class ScsTextFieldComponent {
  protected readonly id = `scs-field-${nextId++}`;

  /** Already translated. */
  readonly label = input.required<string>();

  readonly kind = input<'text' | 'email' | 'password'>('text');

  readonly value = model('');

  /** Already translated; empty when the field is fine. */
  readonly error = input('');

  readonly placeholder = input('');

  readonly autocomplete = input('');

  readonly testId = input('');

  readonly autofocus = input(false, { transform: booleanAttribute });

  protected readonly revealed = signal(false);

  protected readonly shownType = computed(() => {
    if (this.kind() === 'password') {
      return this.revealed() ? 'text' : 'password';
    }
    // An email is typed as text and checked on submit, so the browser's own bubble never
    // appears beside the field's message.
    return 'text';
  });

  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  focus(): void {
    this.field().nativeElement.focus();
  }
}
