import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * One number with its name and how it moved, without a card, the operators console's own (design:
 * Data Display — Stat, `281:107` / `313:106`): the name, the number, and a line in green when it rose,
 * red when it fell, grey when it did not change.
 */
@Component({
  selector: 'app-stat',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-trend]': 'trend()' },
  styles: `
    :host {
      display: grid;
      align-content: start;
    }
    .label {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    .value {
      margin: 0;
      font-size: 24px;
      font-weight: 700;
      line-height: 1.35;
      color: var(--scs-color-text-primary);
    }
    .note {
      font-size: 11px;
      color: var(--scs-color-text-tertiary);
    }
    :host([data-trend='positive']) .note {
      color: var(--scs-color-feedback-success-text);
    }
    :host([data-trend='negative']) .note {
      color: var(--scs-color-feedback-danger-text);
    }
  `,
  template: `
    <span class="label" data-testid="stat-label">{{ label() }}</span>
    <p class="value" data-testid="stat-value"><bdi dir="ltr">{{ value() }}</bdi></p>
    @if (note()) {
      <span class="note" data-testid="stat-note">{{ note() }}</span>
    }
  `,
})
export class StatComponent {
  readonly label = input.required<string>();
  /** Already written in the page's digits. */
  readonly value = input.required<string>();
  readonly note = input('');
  readonly trend = input<'positive' | 'negative' | 'neutral'>('neutral');
}
