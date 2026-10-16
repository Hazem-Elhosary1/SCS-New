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
 * One labelled field for text, an email address or a password (design: Input — ops `54:257`,
 * clinic `4252:56552`; spec 095). Three sizes; the states the page draws: empty, filled, hover,
 * focus, active, disabled, read only, error, success, warning, busy. A message under it — an error,
 * a success, a warning, or a hint — is tied to it for assistive technology; an error takes the
 * hint's place (CL-SH-061). A password field has an eye that shows and hides what was typed;
 * nothing is sent when it is pressed (BRD B-5-8).
 *
 * With no `size` it keeps the geometry the screens had before spec 095 (the clinic's sign-in and
 * password screens); each look sets what its file draws through the `--scs-field-*` settings, whose
 * defaults are that earlier field (FR-023).
 */
@Component({
  selector: 'scs-text-field',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-size]': 'size() ?? null' },
  styles: `
    :host {
      display: block;
    }
    :host([data-size='sm']) {
      --scs-field-height: 32px;
      --scs-field-pad: 10px;
      --scs-field-text: 12px;
      --scs-field-radius: var(--scs-field-radius-sm, 6px);
      --scs-field-label-size: 11px;
    }
    :host([data-size='md']) {
      --scs-field-height: 44px;
      --scs-field-pad: 14px;
      --scs-field-text: 14px;
      --scs-field-radius: var(--scs-field-radius-md, 8px);
      --scs-field-label-size: 13px;
    }
    :host([data-size='lg']) {
      --scs-field-height: 60px;
      --scs-field-pad: 20px;
      --scs-field-text: 17px;
      --scs-field-radius: var(--scs-field-radius-lg, 11px);
      --scs-field-label-size: 15px;
    }
    label {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-field-label-size, var(--scs-text-body-sm));
      font-weight: var(--scs-field-label-weight, 600);
      color: var(--scs-field-label-color, var(--scs-color-text-primary));
    }
    .required {
      color: var(--scs-color-feedback-danger-icon);
    }
    .off label {
      color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
    }
    .box {
      box-sizing: border-box;
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-block-size: var(--scs-field-height, 48px);
      padding-inline-start: var(--scs-field-pad, var(--scs-spacing-md));
      border: 1px solid var(--scs-field-border, var(--scs-color-border-default));
      border-radius: var(--scs-field-radius, var(--scs-radius-md));
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-field-shadow, var(--scs-shadow-subtle));
    }
    .box.plain {
      padding-inline-start: 0;
    }
    .lead {
      color: var(--scs-color-icon-default);
    }
    .mark {
      flex: none;
    }
    .box:not(.has-eye) .mark {
      margin-inline-end: var(--scs-field-pad, var(--scs-spacing-md));
    }
    .warn {
      color: var(--scs-color-feedback-danger-icon);
    }
    .good {
      color: var(--scs-color-feedback-success-icon);
    }
    .careful {
      color: var(--scs-color-feedback-warning-icon);
    }
    .marked input {
      padding-inline-end: 0;
    }
    .box:not(.off):not(.read):hover,
    :host-context([data-scs-state~='hover']) .box {
      border-color: var(--scs-field-hover-border, var(--scs-field-border, var(--scs-color-border-default)));
    }
    .box:focus-within,
    :host-context([data-scs-state~='focus']) .box {
      border-color: var(--scs-color-brand-default);
      border-width: var(--scs-field-focus-border, 1px);
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: var(--scs-field-focus-offset, 1px);
    }
    /* Typing: the page's "Active" — a thicker border and no ring. */
    :host-context([data-scs-state~='active']) .box {
      border-color: var(--scs-color-brand-default);
      border-width: 2px;
      outline: none;
    }
    .box.invalid,
    .box.invalid:hover {
      border-color: var(--scs-color-feedback-danger-icon);
      border-width: var(--scs-field-state-border, 1px);
    }
    .box.success,
    .box.success:hover {
      border-color: var(--scs-color-feedback-success-icon);
      border-width: var(--scs-field-state-border, 1px);
    }
    .box.warning,
    .box.warning:hover {
      border-color: var(--scs-color-feedback-warning-icon);
      border-width: var(--scs-field-state-border, 1px);
    }
    .box.read {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-canvas));
      box-shadow: none;
    }
    .box.off {
      border-color: var(--scs-field-border, var(--scs-color-border-default));
      background: var(--scs-color-bg-muted);
      box-shadow: none;
    }
    input {
      flex: 1;
      align-self: stretch;
      min-inline-size: 0;
      padding: 0;
      padding-inline-end: var(--scs-field-pad, var(--scs-spacing-md));
      border: 0;
      background: transparent;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-field-text, var(--scs-text-body-lg));
      outline: none;
    }
    .off input {
      color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
      cursor: not-allowed;
    }
    .plain input {
      padding-inline-start: var(--scs-field-pad, var(--scs-spacing-md));
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
      color: var(--scs-field-placeholder, var(--scs-color-text-tertiary));
    }
    /* A password, an email and a code are written left to right; in an Arabic page they still
       begin at the start of the line, beside the icon. */
    :host-context([dir='rtl']) input[dir='ltr'] {
      text-align: right;
    }
    /* Without a leading mark, an email or a password in an Arabic page begins at the box's own
       edge: its room there is the input's right side, whatever follows it on the left. */
    :host-context([dir='rtl']) .plain input[dir='ltr'] {
      padding-inline: var(--scs-field-pad, var(--scs-spacing-md));
    }
    :host-context([dir='rtl']) .plain.has-eye input[dir='ltr'],
    :host-context([dir='rtl']) .plain.marked input[dir='ltr'] {
      padding-left: 0;
    }
    .eye {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: 46px;
      block-size: 46px;
      max-block-size: 100%;
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
    /* Busy: a ring turns at the end of the box (the page's "Loading"). */
    .spin {
      flex: none;
      box-sizing: border-box;
      inline-size: 18px;
      block-size: 18px;
      border-width: 1.5px;
      border-style: solid;
      border-top-color: var(--scs-color-brand-default);
      border-bottom-color: color-mix(in srgb, var(--scs-color-brand-default) 25%, transparent);
      border-inline-start-color: color-mix(in srgb, var(--scs-color-brand-default) 25%, transparent);
      border-inline-end-color: color-mix(in srgb, var(--scs-color-brand-default) 25%, transparent);
      border-radius: 50%;
      animation: scs-field-turn 0.8s linear infinite;
    }
    @keyframes scs-field-turn {
      to {
        transform: rotate(360deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .spin {
        animation: none;
      }
    }
    .message {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-field-message-size, var(--scs-text-label));
      color: var(--scs-color-text-secondary);
    }
    .message.error {
      color: var(--scs-field-error-color, var(--scs-color-feedback-danger-text));
    }
    .message.success {
      color: var(--scs-color-feedback-success-icon);
    }
    .message.warning {
      color: var(--scs-color-feedback-warning-icon);
    }
  `,
  template: `
    <div [class.off]="disabled()">
      <label [attr.for]="id"
        >{{ label() }}@if (required()) {<span class="required" aria-hidden="true"> *</span>}</label
      >
      <div
        class="box"
        [class.invalid]="error() !== ''"
        [class.success]="!error() && state() === 'success'"
        [class.warning]="!error() && state() === 'warning'"
        [class.read]="readonly()"
        [class.off]="disabled()"
        [class.plain]="kind() === 'text' || !lead()"
        [class.has-eye]="kind() === 'password'"
        [class.marked]="!!mark()"
      >
        @if (kind() !== 'text' && lead()) {
          <scs-icon class="lead" [name]="kind() === 'email' ? 'mail' : 'lock'" [size]="20" />
        }
        <input
          #field
          [id]="id"
          [type]="shownType()"
          [value]="value()"
          [readOnly]="readonly()"
          [disabled]="disabled()"
          [required]="required()"
          [attr.dir]="kind() === 'text' && !ltr() ? null : 'ltr'"
          [attr.placeholder]="shownPlaceholder()"
          [attr.autocomplete]="autocomplete() || null"
          [attr.inputmode]="kind() === 'email' ? 'email' : null"
          [attr.aria-invalid]="error() !== '' ? 'true' : null"
          [attr.aria-describedby]="message() ? id + '-message' : null"
          [attr.aria-busy]="busy() ? 'true' : null"
          [attr.data-testid]="testId() || null"
          autocapitalize="none"
          spellcheck="false"
          (input)="value.set(field.value)"
        />
        @switch (mark()) {
          @case ('error') {
            <scs-icon class="mark warn" name="alert-circle" [size]="18" />
          }
          @case ('success') {
            <scs-icon class="mark good" name="check" [size]="18" />
          }
          @case ('warning') {
            <scs-icon class="mark careful" name="alert-circle" [size]="18" />
          }
          @case ('busy') {
            <span class="mark spin" aria-hidden="true"></span>
          }
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
      @if (message(); as shown) {
        <p
          class="message"
          [class.error]="shown.kind === 'error'"
          [class.success]="shown.kind === 'success'"
          [class.warning]="shown.kind === 'warning'"
          [id]="id + '-message'"
          [attr.data-testid]="(testId() || 'field') + '-' + shown.kind"
        >
          {{ shown.text }}
        </p>
      }
    </div>
  `,
})
export class ScsTextFieldComponent {
  protected readonly id = `scs-field-${nextId++}`;

  /** Already translated. */
  readonly label = input.required<string>();

  readonly kind = input<'text' | 'email' | 'password'>('text');

  /** The design's sizes; with none, the field keeps its earlier geometry. */
  readonly size = input<'sm' | 'md' | 'lg'>();

  readonly value = model('');

  /** Already translated; empty when the field is fine. */
  readonly error = input('');

  /** A good or a doubtful value, with its message (the page's Success and Warning). */
  readonly state = input<'success' | 'warning' | null>(null);
  readonly stateMessage = input('');

  /** Said under the field until an error takes its place. */
  readonly hint = input('');

  readonly placeholder = input('');

  readonly autocomplete = input('');

  readonly testId = input('');

  readonly autofocus = input(false, { transform: booleanAttribute });

  readonly required = input(false, { transform: booleanAttribute });

  readonly disabled = input(false, { transform: booleanAttribute });

  readonly readonly = input(false, { transform: booleanAttribute });

  /** The value is being checked (the page's Loading). */
  readonly busy = input(false, { transform: booleanAttribute });

  /** A plain text that is always written left to right: a phone number, a code. */
  readonly ltr = input(false, { transform: booleanAttribute });

  /**
   * The mark before an email or a password (an envelope, a lock). On by default, as the clinic's
   * design draws it; the operators' design draws its fields without one.
   */
  readonly lead = input(true, { transform: booleanAttribute });

  protected readonly revealed = signal(false);

  /** The mark at the end of the box: the error's, the state's, or the busy ring. */
  protected readonly mark = computed<'error' | 'success' | 'warning' | 'busy' | null>(() => {
    if (this.error() !== '') return 'error';
    if (this.busy()) return 'busy';
    return this.state();
  });

  /** One message under the field: the error, else the state's message, else the hint. */
  protected readonly message = computed<{ kind: 'error' | 'success' | 'warning' | 'hint'; text: string } | null>(() => {
    if (this.error()) return { kind: 'error', text: this.error() };
    const state = this.state();
    if (state && this.stateMessage()) return { kind: state, text: this.stateMessage() };
    if (this.hint()) return { kind: 'hint', text: this.hint() };
    return null;
  });

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
