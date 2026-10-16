import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

let nextId = 0;

/** How close a limit is: well within, near it, at its edge, or past it (the clinic's "Exceeded"). */
export type ScsUsageLevel = 'normal' | 'warning' | 'critical' | 'exceeded';

/**
 * How much of a plan's limit is used (design: Usage Meter — `387:24` / `388:23`; the clinic's Plan
 * Usage Meter `4252:63959`): what is counted and how much — "3,200 / 5,000" — over a track filled to
 * the share used, and a line under it. Its colour follows `level`, which is worked out from the share
 * when not given (from 80% near the limit, from 95% at its edge, at 100% past it). It is a `meter`
 * for assistive technology. Each look draws it through `--scs-meter-*`.
 */
@Component({
  selector: 'scs-usage-meter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-level]': 'shownLevel()' },
  styles: `
    :host {
      --fill: var(--scs-color-brand-default);
      --mark: var(--scs-meter-value-color, var(--scs-color-text-secondary));
      display: grid;
      gap: var(--scs-meter-gap, 6px);
    }
    :host([data-level='warning']) {
      --fill: color-mix(in srgb, var(--scs-color-feedback-warning-text) calc(var(--scs-meter-fill-ink, 0) * 100%), var(--scs-color-feedback-warning-icon));
      --mark: var(--scs-color-feedback-warning-text);
    }
    :host([data-level='critical']),
    :host([data-level='exceeded']) {
      --fill: color-mix(in srgb, var(--scs-color-feedback-danger-text) calc(var(--scs-meter-fill-ink, 0) * 100%), var(--scs-color-feedback-danger-icon));
      --mark: var(--scs-color-feedback-danger-text);
    }
    .head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
      font-size: 13px;
    }
    .label {
      font-weight: var(--scs-meter-label-weight, 500);
      color: var(--scs-color-text-primary);
    }
    .value {
      flex: none;
      font-weight: var(--scs-meter-value-weight, 600);
      color: color-mix(in srgb, var(--mark) calc(var(--scs-meter-value-tint, 1) * 100%), var(--scs-meter-value-plain, var(--scs-color-text-tertiary)));
    }
    .track {
      block-size: 8px;
      overflow: hidden;
      border-radius: var(--scs-meter-radius, 999px);
      background: var(--scs-meter-track, var(--scs-color-border-default));
    }
    .fill {
      block-size: 100%;
      border-radius: inherit;
      background: var(--fill);
    }
    .note {
      font-size: 11px;
      font-weight: var(--scs-meter-note-weight, 400);
      color: color-mix(in srgb, var(--mark) calc(var(--scs-meter-note-tint, 0) * 100%), var(--scs-color-text-tertiary));
    }
    :host([data-level='exceeded']) .note {
      color: var(--scs-color-feedback-danger-text);
    }
  `,
  template: `
    <div class="head">
      <span class="label" [id]="id + '-label'">{{ label() }}</span>
      <bdi class="value" dir="auto" data-testid="meter-value">{{ value() }}</bdi>
    </div>
    <div
      class="track"
      role="meter"
      aria-valuemin="0"
      [attr.aria-valuemax]="limit()"
      [attr.aria-valuenow]="nowValue()"
      [attr.aria-valuetext]="value() + (note() ? ' — ' + note() : '')"
      [attr.aria-labelledby]="id + '-label'"
    >
      <div class="fill" [style.inline-size.%]="share()"></div>
    </div>
    @if (note()) {
      <span class="note" data-testid="meter-note">{{ note() }}</span>
    }
  `,
})
export class ScsUsageMeterComponent {
  protected readonly id = `scs-meter-${nextId++}`;

  /** Already translated: what is counted. */
  readonly label = input.required<string>();
  readonly used = input.required<number>();
  readonly limit = input.required<number>();
  /** Already written in the page's digits: "3,200 / 5,000". */
  readonly value = input.required<string>();
  /** Already translated: the line under the track ("64% used"). */
  readonly note = input('');
  /** Set by the page, or worked out from the share. */
  readonly level = input<ScsUsageLevel | null>(null);

  protected readonly share = computed(() => {
    const limit = this.limit();
    return limit > 0 ? Math.min(100, Math.max(0, (this.used() / limit) * 100)) : 0;
  });
  protected readonly nowValue = computed(() => Math.min(this.used(), this.limit()));
  protected readonly shownLevel = computed<ScsUsageLevel>(() => {
    const given = this.level();
    if (given) return given;
    const share = this.limit() > 0 ? this.used() / this.limit() : 0;
    return share >= 1 ? 'exceeded' : share >= 0.95 ? 'critical' : share >= 0.8 ? 'warning' : 'normal';
  });
}

