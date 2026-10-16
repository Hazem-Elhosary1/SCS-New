import { ChangeDetectionStrategy, Component, booleanAttribute, input, model } from '@angular/core';

let nextId = 0;

/** One choice of a radio group. */
export interface ScsRadioOption {
  readonly value: string;
  readonly label: string;
  readonly disabled?: boolean;
  /** Why it may not be chosen, read with it. */
  readonly reason?: string;
}

/**
 * One choice among a few, all in view (design: Radio — ops `43:66`, clinic `4252:58465`; spec 095):
 * real radio buttons, so the arrows move and choose and Tab leaves the group, drawn as the page draws
 * them — unselected, selected, disabled, hover, focus, error.
 */
@Component({
  selector: 'scs-radio-group',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
      min-inline-size: 0;
    }
    legend {
      margin-block-end: var(--scs-spacing-sm);
      padding: 0;
      font-size: var(--scs-text-body-sm);
      font-weight: var(--scs-field-label-weight, 600);
      color: var(--scs-field-label-color, var(--scs-color-text-primary));
    }
    .options {
      display: flex;
      flex-direction: column;
    }
    .options.row {
      flex-direction: row;
      flex-wrap: wrap;
      column-gap: var(--scs-spacing-lg);
    }
    label {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      min-block-size: 40px;
      padding-block: var(--scs-spacing-sm);
      cursor: pointer;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    label.off {
      cursor: not-allowed;
      color: var(--scs-color-text-tertiary);
    }
    .control {
      position: relative;
      flex: none;
      inline-size: 20px;
      block-size: 20px;
      margin-block-start: 2px;
    }
    input {
      position: absolute;
      inset: 0;
      inline-size: 100%;
      block-size: 100%;
      margin: 0;
      opacity: 0;
      cursor: inherit;
    }
    .circle {
      display: grid;
      place-items: center;
      box-sizing: border-box;
      inline-size: 20px;
      block-size: 20px;
      border: 1.5px solid var(--scs-check-border, var(--scs-color-border-control, var(--scs-color-border-default)));
      border-radius: 50%;
      background: var(--scs-color-bg-surface);
      pointer-events: none;
    }
    .dot {
      display: none;
      inline-size: 10px;
      block-size: 10px;
      border-radius: 50%;
      background: var(--scs-color-brand-default);
    }
    input:checked + .circle {
      border-color: var(--scs-color-brand-default);
    }
    input:checked + .circle .dot {
      display: block;
    }
    label:not(.off):hover input + .circle,
    :host-context([data-scs-state~='hover']) input + .circle {
      border-color: var(--scs-color-brand-default);
    }
    input:focus-visible + .circle,
    :host-context([data-scs-state~='focus']) .circle {
      outline: 2px solid var(--scs-color-focus-ring, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    .invalid .circle {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    input:disabled + .circle {
      border-color: var(--scs-color-border-control, var(--scs-color-border-default));
      background: var(--scs-color-bg-muted);
    }
    input:disabled + .circle .dot {
      background: var(--scs-color-text-disabled, var(--scs-color-text-tertiary));
    }
    .words {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      min-inline-size: 0;
    }
    .reason {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .error {
      margin: 0;
      padding-inline-start: 30px;
      font-size: var(--scs-text-caption);
      color: var(--scs-color-feedback-danger-icon);
    }
  `,
  template: `
    <fieldset
      role="radiogroup"
      [attr.aria-labelledby]="label() ? id + '-legend' : null"
      [attr.aria-label]="label() ? null : ariaLabel() || null"
      [attr.aria-describedby]="error() ? id + '-error' : null"
      [attr.aria-invalid]="error() ? 'true' : null"
      [attr.data-testid]="testId() || null"
      [class.invalid]="!!error()"
    >
      @if (label()) {
        <legend [id]="id + '-legend'">{{ label() }}</legend>
      }
      <div class="options" [class.row]="direction() === 'row'">
        @for (option of options(); track option.value; let index = $index) {
          <label [class.off]="disabled() || option.disabled" [attr.for]="id + '-' + index">
            <span class="control">
              <input
                type="radio"
                [id]="id + '-' + index"
                [name]="id"
                [value]="option.value"
                [checked]="value() === option.value"
                [disabled]="disabled() || option.disabled"
                [attr.aria-describedby]="option.reason ? id + '-' + index + '-reason' : null"
                [attr.data-testid]="testId() ? testId() + '-' + option.value : null"
                (change)="value.set(option.value)"
              />
              <span class="circle" aria-hidden="true"><span class="dot"></span></span>
            </span>
            <span class="words">
              <span>{{ option.label }}</span>
              @if (option.reason) {
                <span class="reason" [id]="id + '-' + index + '-reason'">{{ option.reason }}</span>
              }
            </span>
          </label>
        }
      </div>
      @if (error()) {
        <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'radio') + '-error'">{{ error() }}</p>
      }
    </fieldset>
  `,
})
export class ScsRadioGroupComponent {
  protected readonly id = `scs-radio-${nextId++}`;
  /** The group's name, shown above it. */
  readonly label = input('');
  /** The group's name for a screen reader when none is shown. */
  readonly ariaLabel = input('');
  readonly options = input.required<readonly ScsRadioOption[]>();
  readonly value = model<string | null>(null);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  /** Choices under each other (the page's drawing) or side by side. */
  readonly direction = input<'column' | 'row'>('column');
  readonly testId = input('');
}
