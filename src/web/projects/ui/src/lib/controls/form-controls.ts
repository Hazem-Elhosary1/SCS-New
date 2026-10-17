import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input, model } from '@angular/core';

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
 * A box to tick (design: Checkbox — ops `43:36`, clinic `4252:58206`; spec 095): a real checkbox,
 * drawn as the page draws it — unchecked, checked, indeterminate, disabled, hover, focus, error. A
 * box that may not be changed says why underneath, and the reason is its description, so a screen
 * reader reads it with the box (spec 011, FR-014); an error takes the reason's place.
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
      gap: 10px;
      min-block-size: 40px;
      min-inline-size: var(--scs-touch, 0px);
      padding-block: var(--scs-spacing-sm);
      cursor: pointer;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    label.off {
      cursor: not-allowed;
    }
    .control {
      position: relative;
      flex: none;
      inline-size: 20px;
      block-size: 20px;
      margin-block-start: 2px;
    }
    /* The real box, unseen, over the drawn one: it takes the clicks, the keys and the focus. */
    input {
      position: absolute;
      inset: 0;
      inline-size: 100%;
      block-size: 100%;
      margin: 0;
      opacity: 0;
      cursor: inherit;
    }
    .box {
      display: grid;
      place-items: center;
      box-sizing: border-box;
      inline-size: 20px;
      block-size: 20px;
      border: 1.5px solid var(--scs-check-border, var(--scs-color-border-control, var(--scs-color-border-default)));
      border-radius: 5px;
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-on-brand);
      pointer-events: none;
    }
    .box svg {
      display: none;
    }
    input:checked + .box,
    input:indeterminate + .box {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-default);
    }
    input:checked + .box .tick,
    input:indeterminate + .box .dash {
      display: block;
    }
    input:indeterminate + .box .tick {
      display: none;
    }
    label:not(.off):hover input:not(:checked):not(:indeterminate) + .box,
    :host-context([data-scs-state~='hover']) input:not(:checked) + .box {
      border-color: var(--scs-color-brand-default);
    }
    input:focus-visible + .box,
    :host-context([data-scs-state~='focus']) .box {
      outline: 2px solid var(--scs-color-focus-ring, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    .error-state .box {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    input:disabled + .box {
      border-color: var(--scs-color-border-control, var(--scs-color-border-default));
      background: var(--scs-color-bg-muted);
      color: var(--scs-color-text-disabled, var(--scs-color-text-tertiary));
    }
    .words {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      min-inline-size: 0;
    }
    .off .text {
      color: var(--scs-color-text-tertiary);
    }
    .reason,
    .error {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .error {
      font-size: var(--scs-text-caption);
      color: var(--scs-color-feedback-danger-icon);
    }
    /* A row's box in a table: its name is read out, not shown. */
    .words.unseen {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
  template: `
    <label [class.off]="disabled()" [class.error-state]="!!error()" [attr.for]="id">
      <span class="control">
        <input
          type="checkbox"
          [id]="id"
          [checked]="checked()"
          [indeterminate]="indeterminate()"
          [disabled]="disabled()"
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.aria-describedby]="described()"
          [attr.data-testid]="testId() || null"
          (change)="choose($any($event.target).checked)"
        />
        <span class="box" aria-hidden="true">
          <svg class="tick" width="12" height="10" viewBox="0 0 12 10" fill="none">
            <path d="M1.2 5.2 4.4 8.2 10.8 1.6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          <svg class="dash" width="12" height="2" viewBox="0 0 12 2" fill="none">
            <path d="M1 1h10" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
          </svg>
        </span>
      </span>
      <span class="words" [class.unseen]="labelHidden()">
        <span class="text">{{ label() }}</span>
        @if (error()) {
          <span class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'checkbox') + '-error'">{{ error() }}</span>
        } @else if (reason()) {
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
  /** The name is read out but not shown (a row's box in a table). */
  readonly labelHidden = input(false, { transform: booleanAttribute });
  readonly checked = model(false);
  /** Some of what it stands for is chosen, not all: drawn with a dash; choosing it clears that. */
  readonly indeterminate = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });
  /** Why the box may not be changed, or what it is tied to. */
  readonly reason = input('');
  /** What is wrong with it; drawn red, and read with the box in the reason's place. */
  readonly error = input('');
  readonly testId = input('');

  protected readonly described = computed(() =>
    this.error() ? `${this.id}-error` : this.reason() ? `${this.id}-reason` : null,
  );

  protected choose(checked: boolean): void {
    this.indeterminate.set(false);
    this.checked.set(checked);
  }
}

/**
 * On or off (design: Switch — ops `43:102`, clinic `4252:58656`; spec 095): off, on, disabled,
 * hover, focus and busy (a small ring turns in the knob while the change is saved). A switch that is
 * locked says why beside it.
 */
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
      gap: 10px;
      min-block-size: 40px;
    }
    /* What is pressed is 44 by 40; the track drawn inside it is the design's 44 by 24. */
    button {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 44px;
      block-size: 40px;
      padding: 0;
      border: 0;
      background: none;
      cursor: pointer;
    }
    .track {
      position: relative;
      inline-size: 44px;
      block-size: 24px;
      border-radius: 12px;
      background: var(--scs-switch-off, var(--scs-color-border-control, var(--scs-color-border-default)));
      transition: background 0.15s;
    }
    /* On is the knob at the right, in both languages: the design draws it so in Arabic too, as
       spec 010's profile does (owner, 14 Oct 2026). It is not mirrored. */
    .knob {
      position: absolute;
      inset-block-start: 3px;
      left: 3px;
      display: grid;
      place-items: center;
      inline-size: 18px;
      block-size: 18px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface);
      transition: left 0.15s;
    }
    button:not(:disabled):hover[aria-checked='false'] .track,
    :host-context([data-scs-state~='hover']) button[aria-checked='false'] .track {
      background: var(--scs-switch-off-hover, var(--scs-color-text-disabled, var(--scs-color-text-tertiary)));
    }
    button[aria-checked='true'] .track {
      background: var(--scs-color-brand-default);
    }
    button[aria-checked='true'] .knob {
      left: 23px;
    }
    button:disabled {
      cursor: not-allowed;
    }
    button:disabled:not([aria-busy='true']) .track {
      background: var(--scs-color-bg-muted);
    }
    button:disabled:not([aria-busy='true']) .knob {
      background: var(--scs-switch-knob-disabled, var(--scs-color-bg-canvas));
    }
    /* On and locked (a setting requires it): each look's own drawing; grey by default. */
    button[aria-checked='true']:disabled:not([aria-busy='true']) .track {
      background: var(--scs-switch-locked, var(--scs-color-bg-muted));
    }
    button[aria-checked='true']:disabled:not([aria-busy='true']) .knob {
      background: var(--scs-switch-locked-knob, var(--scs-switch-knob-disabled, var(--scs-color-bg-canvas)));
    }
    button:focus-visible {
      outline: none;
    }
    button:focus-visible .track,
    :host-context([data-scs-state~='focus']) .track {
      outline: 2px solid var(--scs-color-focus-ring, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    /* Busy: a ring of the brand turns in the knob. */
    .spin {
      box-sizing: border-box;
      inline-size: 10px;
      block-size: 10px;
      border-width: 1.5px;
      border-style: solid;
      border-top-color: var(--scs-color-brand-default);
      border-bottom-color: color-mix(in srgb, var(--scs-color-brand-default) 30%, transparent);
      border-inline-start-color: color-mix(in srgb, var(--scs-color-brand-default) 30%, transparent);
      border-inline-end-color: color-mix(in srgb, var(--scs-color-brand-default) 30%, transparent);
      border-radius: var(--scs-radius-full);
      animation: scs-switch-turn 0.8s linear infinite;
    }
    @keyframes scs-switch-turn {
      to {
        transform: rotate(360deg);
      }
    }
    .words {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    .words.unseen {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    .off {
      color: var(--scs-color-text-tertiary);
    }
    .off.locked {
      color: var(--scs-switch-locked-text, var(--scs-color-text-tertiary));
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
      .spin {
        animation: none;
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
        [attr.aria-busy]="busy() ? 'true' : null"
        [disabled]="disabled() || busy()"
        [attr.data-testid]="testId() || null"
        (click)="on.set(!on())"
      >
        <span class="track"
          ><span class="knob">
            @if (busy()) {
              <span class="spin"></span>
            }
          </span></span
        >
      </button>
      <span
        class="words"
        [class.unseen]="labelHidden()"
        [class.off]="disabled()"
        [class.locked]="disabled() && on()"
      >
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
  /** The name is read out but not shown: a row that draws its own name elsewhere (spec 013's plan grid). */
  readonly labelHidden = input(false, { transform: booleanAttribute });
  readonly on = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });
  /** The change is being saved: it cannot be pressed again, and says so. */
  readonly busy = input(false, { transform: booleanAttribute });
  readonly reason = input('');
  readonly testId = input('');
}

/**
 * A few lines of free text (design: Textarea — ops `146:95`, clinic `4272:885`; spec 095), with how
 * much of its room is used. Three sizes; default, hover, focus, filled, disabled, read only, error.
 * A message under it — the error, or else the hint — is read with it; the count sits at the end.
 */
@Component({
  selector: 'scs-textarea',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-size]': 'size()' },
  styles: `
    ${FIELD}
    :host([data-size='sm']) {
      --scs-area-height: 72px;
      --scs-area-pad: 10px;
      --scs-area-text: 13px;
      --scs-area-label: 11px;
      --scs-area-count: 10px;
    }
    :host([data-size='md']) {
      --scs-area-height: 96px;
      --scs-area-pad: 10px 14px;
      --scs-area-text: 14px;
      --scs-area-label: 13px;
      --scs-area-count: 11px;
    }
    :host([data-size='lg']) {
      --scs-area-height: 120px;
      --scs-area-pad: 10px 16px;
      --scs-area-text: 15px;
      --scs-area-label: 15px;
      --scs-area-count: 12px;
      --scs-area-radius: 9px;
    }
    label.name {
      font-size: var(--scs-area-label, var(--scs-text-body-sm));
    }
    .required {
      color: var(--scs-color-feedback-danger-icon);
    }
    textarea {
      display: block;
      inline-size: 100%;
      box-sizing: border-box;
      min-block-size: var(--scs-area-height, 96px);
      padding: var(--scs-area-pad, 10px 14px);
      border: 1px solid var(--scs-field-border, var(--scs-color-border-default));
      border-radius: var(--scs-area-radius, 8px);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-area-text, 14px);
      line-height: 1.6;
      resize: vertical;
    }
    textarea:not(:disabled):not(:read-only):hover,
    :host-context([data-scs-state~='hover']) textarea {
      border-color: var(--scs-area-hover-border, var(--scs-color-text-tertiary));
    }
    textarea:focus-visible,
    :host-context([data-scs-state~='focus']) textarea {
      border-color: var(--scs-color-brand-default);
      border-width: 2px;
      outline: none;
    }
    textarea.invalid,
    textarea.invalid:hover {
      border-color: var(--scs-color-feedback-danger-icon);
      border-width: var(--scs-field-state-border, 1.5px);
    }
    textarea::placeholder {
      color: var(--scs-field-placeholder, var(--scs-color-text-tertiary));
    }
    textarea:read-only {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
    }
    textarea:disabled {
      background: var(--scs-area-disabled, var(--scs-color-bg-muted));
      color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
      cursor: not-allowed;
    }
    .under {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: var(--scs-spacing-md);
    }
    .under .hint,
    .under .error {
      font-size: var(--scs-field-message-size, var(--scs-text-label));
    }
    .under .error {
      color: var(--scs-field-error-color, var(--scs-color-feedback-danger-text));
    }
    .count {
      margin: var(--scs-spacing-xs) 0 0;
      margin-inline-start: auto;
      flex: none;
      font-size: var(--scs-area-count, 11px);
      color: var(--scs-color-text-tertiary);
    }
    .count.over {
      color: var(--scs-color-feedback-danger-icon);
    }
  `,
  template: `
    <label class="name" [attr.for]="id"
      >{{ label() }}@if (required()) {<span class="required" aria-hidden="true"> *</span>}</label
    >
    <textarea
      #field
      [id]="id"
      [rows]="rows()"
      [value]="value()"
      [class.invalid]="error() !== ''"
      [readOnly]="readOnly()"
      [disabled]="disabled()"
      [required]="required()"
      [attr.maxlength]="max() || null"
      [attr.placeholder]="placeholder() || null"
      [attr.dir]="dir() || null"
      [attr.aria-invalid]="error() !== '' ? 'true' : null"
      [attr.aria-describedby]="error() ? id + '-error' : hint() ? id + '-hint' : null"
      [attr.data-testid]="testId() || null"
      (input)="value.set(field.value)"
    ></textarea>
    <div class="under">
      @if (error()) {
        <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'textarea') + '-error'">{{ error() }}</p>
      } @else if (hint()) {
        <p class="hint" [id]="id + '-hint'">{{ hint() }}</p>
      }
      @if (max()) {
        <p class="count" [class.over]="error() !== ''" [attr.data-testid]="(testId() || 'textarea') + '-count'">
          <bdi dir="ltr">{{ value().length }} / {{ max() }}</bdi>
        </p>
      }
    </div>
  `,
})
export class ScsTextareaComponent {
  protected readonly id = `scs-textarea-${nextId++}`;
  readonly label = input.required<string>();
  readonly value = model('');
  /** The design's sizes: 72, 96 or 120 high. */
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly rows = input(3);
  /** The most it takes; its count is shown under it. */
  readonly max = input(0);
  readonly placeholder = input('');
  /** Written in one direction whatever the page's: Arabic in an English page, or the other way (spec 012). */
  readonly dir = input<'ltr' | 'rtl' | ''>('');
  readonly readOnly = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly required = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly testId = input('');
}
