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
import { ScsIconComponent } from '../icon/icon.component';

let nextId = 0;

/**
 * One labelled field for text, an email address or a password. Its error is shown under it and
 * tied to it for assistive technology. A password field has an eye that shows and hides what
 * was typed; nothing is sent when it is pressed (BRD B-5-8).
 */
@Component({
  selector: 'scs-text-field',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    label {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .box {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      padding-inline-start: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-shadow-subtle);
    }
    .box.plain {
      padding-inline-start: 0;
    }
    .lead {
      color: var(--scs-color-icon-default);
    }
    .warn {
      color: var(--scs-color-feedback-danger-icon);
    }
    .box:not(.has-eye) .warn {
      margin-inline-end: var(--scs-spacing-md);
    }
    .invalid input {
      padding-inline-end: 0;
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
      min-block-size: 46px;
      padding: 0;
      padding-inline-end: var(--scs-spacing-md);
      border: 0;
      background: transparent;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body-lg);
      outline: none;
    }
    .plain input {
      padding-inline-start: var(--scs-spacing-md);
    }
    .has-eye input {
      padding-inline-end: 0;
    }
    /* Edge draws its own eye and its own × inside the box; the field has its own eye. */
    input::-ms-reveal,
    input::-ms-clear {
      display: none;
    }
    input::placeholder {
      color: var(--scs-color-text-tertiary);
    }
    /* A password, an email and a code are written left to right; in an Arabic page they still
       begin at the start of the line, beside the icon. */
    :host-context([dir='rtl']) input[dir='ltr'] {
      text-align: right;
    }
    /* Without a leading mark, an email or a password in an Arabic page begins at the box's own
       edge: its room there is the input's right side, whatever follows it on the left. */
    :host-context([dir='rtl']) .plain input[dir='ltr'] {
      padding-inline: var(--scs-spacing-md);
    }
    :host-context([dir='rtl']) .plain.has-eye input[dir='ltr'],
    :host-context([dir='rtl']) .plain.invalid input[dir='ltr'] {
      padding-left: 0;
    }
    .eye {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: 46px;
      block-size: 46px;
      border: 0;
      border-radius: var(--scs-radius-md);
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
    <div
      class="box"
      [class.invalid]="error() !== ''"
      [class.plain]="kind() === 'text' || !lead()"
      [class.has-eye]="kind() === 'password'"
    >
      @if (kind() !== 'text' && lead()) {
        <scs-icon class="lead" [name]="kind() === 'email' ? 'mail' : 'lock'" [size]="20" />
      }
      <input
        #field
        [id]="id"
        [type]="shownType()"
        [value]="value()"
        [attr.dir]="kind() === 'text' ? null : 'ltr'"
        [attr.placeholder]="shownPlaceholder()"
        [attr.autocomplete]="autocomplete() || null"
        [attr.inputmode]="kind() === 'email' ? 'email' : null"
        [attr.aria-invalid]="error() !== '' ? 'true' : null"
        [attr.aria-describedby]="error() !== '' ? id + '-error' : null"
        [attr.data-testid]="testId() || null"
        autocapitalize="none"
        spellcheck="false"
        (input)="value.set(field.value)"
      />
      @if (error() !== '') {
        <scs-icon class="warn" name="alert-circle" [size]="18" />
      }
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
          @if (revealed()) {
            <scs-icon name="eye-off" [size]="20" data-testid="eye-crossed" />
          } @else {
            <scs-icon name="eye" [size]="20" />
          }
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

  /**
   * The mark before an email or a password (an envelope, a lock). On by default, as the clinic's
   * design draws it; the operators' design draws its fields without one.
   */
  readonly lead = input(true, { transform: booleanAttribute });

  protected readonly revealed = signal(false);

  /**
   * An empty password field shows eight dots, as the design draws it: the shape of what goes
   * there, in no language. Any other field shows the placeholder it was given, or none.
   */
  protected readonly shownPlaceholder = computed(
    () => this.placeholder() || (this.kind() === 'password' ? '••••••••' : null),
  );

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
