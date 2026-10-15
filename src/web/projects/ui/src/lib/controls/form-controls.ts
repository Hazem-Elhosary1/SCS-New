import { ChangeDetectionStrategy, Component, booleanAttribute, input, model } from '@angular/core';

let nextId = 0;

const FIELD = `
  :host {
    display: block;
  }
  label.name {
    display: block;
    margin-block-end: var(--scs-spacing-sm);
    font-size: var(--scs-text-body-sm);
    font-weight: var(--scs-field-label-weight, 600);
    color: var(--scs-field-label-color, var(--scs-color-text-primary));
  }
  .hint,
  .error {
    margin: var(--scs-spacing-xs) 0 0;
    font-size: var(--scs-text-label);
    color: var(--scs-color-text-secondary);
  }
  .error {
    color: var(--scs-color-feedback-danger-text);
  }
`;

/**
 * A box to tick (design: Checkbox). A box that may not be changed says why underneath, and
 * the reason is its description, so a screen reader reads it with the box (spec 011, FR-014).
 */
@Component({
  selector: 'scs-checkbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    label {
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-sm);
      min-block-size: 40px;
      padding-block: var(--scs-spacing-sm);
      cursor: pointer;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    label.off {
      cursor: not-allowed;
    }
    input {
      flex: none;
      inline-size: 18px;
      block-size: 18px;
      margin: 3px 0 0;
      accent-color: var(--scs-color-brand-default);
      cursor: inherit;
    }
    input:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .words {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      min-inline-size: 0;
    }
    .off .text {
      color: var(--scs-color-text-secondary);
    }
    .reason {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    <label [class.off]="disabled()" [attr.for]="id">
      <input
        type="checkbox"
        [id]="id"
        [checked]="checked()"
        [disabled]="disabled()"
        [attr.aria-describedby]="reason() ? id + '-reason' : null"
        [attr.data-testid]="testId() || null"
        (change)="checked.set($any($event.target).checked)"
      />
      <span class="words">
        <span class="text">{{ label() }}</span>
        @if (reason()) {
          <span class="reason" [id]="id + '-reason'" [attr.data-testid]="(testId() || 'checkbox') + '-reason'">{{
            reason()
          }}</span>
        }
      </span>
    </label>
  `,
})
export class ScsCheckboxComponent {
  protected readonly id = `scs-checkbox-${nextId++}`;
  readonly label = input.required<string>();
  readonly checked = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });
  /** Why the box may not be changed, or what it is tied to. */
  readonly reason = input('');
  readonly testId = input('');
}

/** On or off (design: Switch). A switch that is locked says why beside it. */
@Component({
  selector: 'scs-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .row {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-block-size: 40px;
    }
    /* What is pressed is 40 by 40; the track drawn inside it is the design's 40 by 22. */
    button {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 40px;
      block-size: 40px;
      padding: 0;
      border: 0;
      border-radius: var(--scs-radius-full);
      background: none;
      cursor: pointer;
    }
    .track {
      position: relative;
      inline-size: 40px;
      block-size: 22px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-border-default);
      transition: background 0.15s;
    }
    /* On is the knob at the right, in both languages: the design draws it so in Arabic too, as
       spec 010's profile does. It is not mirrored. */
    .knob {
      position: absolute;
      inset-block-start: 2px;
      left: 2px;
      inline-size: 18px;
      block-size: 18px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-shadow-subtle);
      transition: left 0.15s;
    }
    button[aria-checked='true'] .track {
      background: var(--scs-color-brand-default);
    }
    button[aria-checked='true'] .knob {
      left: 20px;
    }
    button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    button:focus-visible .track {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    button:focus-visible {
      outline: none;
    }
    .words {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    .reason {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    @media (prefers-reduced-motion: reduce) {
      .track,
      .knob {
        transition: none;
      }
    }
  `,
  template: `
    <div class="row">
      <button
        type="button"
        role="switch"
        [attr.aria-checked]="on()"
        [attr.aria-labelledby]="id + '-label'"
        [attr.aria-describedby]="reason() ? id + '-reason' : null"
        [disabled]="disabled()"
        [attr.data-testid]="testId() || null"
        (click)="on.set(!on())"
      >
        <span class="track"><span class="knob"></span></span>
      </button>
      <span class="words">
        <span [id]="id + '-label'">{{ label() }}</span>
        @if (reason()) {
          <span class="reason" [id]="id + '-reason'" [attr.data-testid]="(testId() || 'switch') + '-reason'">{{
            reason()
          }}</span>
        }
      </span>
    </div>
  `,
})
export class ScsSwitchComponent {
  protected readonly id = `scs-switch-${nextId++}`;
  readonly label = input.required<string>();
  readonly on = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly reason = input('');
  readonly testId = input('');
}

/** A few lines of free text (design: Textarea), with how much of its room is used. */
@Component({
  selector: 'scs-textarea',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    ${FIELD}
    textarea {
      display: block;
      inline-size: 100%;
      box-sizing: border-box;
      min-block-size: 70px;
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-shadow-subtle);
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body-lg);
      line-height: 1.6;
      resize: vertical;
    }
    textarea:focus-visible {
      border-color: var(--scs-color-brand-default);
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    textarea.invalid {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    textarea::placeholder {
      color: var(--scs-color-text-tertiary);
    }
    textarea:read-only {
      background: var(--scs-color-bg-surface-secondary);
    }
  `,
  template: `
    <label class="name" [attr.for]="id">{{ label() }}</label>
    <textarea
      #field
      [id]="id"
      [rows]="rows()"
      [value]="value()"
      [class.invalid]="error() !== ''"
      [readOnly]="readOnly()"
      [attr.maxlength]="max() || null"
      [attr.placeholder]="placeholder() || null"
      [attr.aria-invalid]="error() !== '' ? 'true' : null"
      [attr.aria-describedby]="error() ? id + '-error' : hint() ? id + '-hint' : null"
      [attr.data-testid]="testId() || null"
      (input)="value.set(field.value)"
    ></textarea>
    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'textarea') + '-error'">{{ error() }}</p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'">{{ hint() }}</p>
    }
  `,
})
export class ScsTextareaComponent {
  protected readonly id = `scs-textarea-${nextId++}`;
  readonly label = input.required<string>();
  readonly value = model('');
  readonly rows = input(3);
  readonly max = input(0);
  readonly placeholder = input('');
  readonly readOnly = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly testId = input('');
}

export type ScsStatusTone = 'success' | 'warning' | 'neutral' | 'danger' | 'brand';

/**
 * A short state in a pill with its dot (design: Badge): «نشط», «مدعو», «معطل». The colour is
 * never the only sign — the word says it.
 */
@Component({
  selector: 'scs-status-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()', '[class.plain]': '!dot()' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-6);
      padding: var(--scs-spacing-2) var(--scs-spacing-10);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-label);
      font-weight: 500;
      line-height: 1.6;
      white-space: nowrap;
    }
    :host::before {
      content: '';
      inline-size: 6px;
      block-size: 6px;
      border-radius: var(--scs-radius-full);
      background: currentColor;
    }
    :host(.plain)::before {
      display: none;
    }
    :host([data-tone='success']) {
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-text);
    }
    :host([data-tone='warning']) {
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    :host([data-tone='danger']) {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-text);
    }
    :host([data-tone='brand']) {
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
    }
  `,
  template: `<ng-content />`,
})
export class ScsStatusMarkComponent {
  readonly tone = input<ScsStatusTone>('neutral');
  /** The dot before the word; a plain mark (a permission's name) has none. */
  readonly dot = input(true, { transform: booleanAttribute });
}
