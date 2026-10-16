import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

let nextId = 0;

/**
 * How far something has gone (design: Loading — Progress Bar, `289:38` / `314:65`; the clinic's
 * Progress `4252:59743`): its words over a track filled to the share done, or — when the share is not
 * known (`value` null) — a segment running along it. In the brand's colour or an intent's. Each look
 * sets its track through `--scs-progress-*`: the clinic's 8-pixel round track by default, the
 * operators' 6-pixel one.
 */
@Component({
  selector: 'scs-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-intent]': 'intent()' },
  styles: `
    :host {
      --fill: var(--scs-color-brand-default);
      display: block;
    }
    :host([data-intent='success']) {
      --fill: var(--scs-color-feedback-success-icon);
    }
    :host([data-intent='warning']) {
      --fill: var(--scs-color-feedback-warning-icon);
    }
    :host([data-intent='danger']) {
      --fill: var(--scs-color-feedback-danger-icon);
    }
    .words {
      display: block;
      margin-block-end: var(--scs-progress-gap, 6px);
      font-size: var(--scs-progress-text, 12px);
      font-weight: var(--scs-progress-weight, 500);
      color: var(--scs-progress-color, var(--scs-color-text-secondary));
    }
    .track {
      position: relative;
      overflow: hidden;
      block-size: var(--scs-progress-height, 8px);
      border-radius: var(--scs-progress-radius, 999px);
      background: var(--scs-progress-track, var(--scs-color-border-default));
    }
    .bar {
      position: absolute;
      inset-block: 0;
      inset-inline-start: 0;
      border-radius: inherit;
      background: var(--fill);
      transition: inline-size 0.2s;
    }
    /* Not known: a third of the track runs across it. */
    .bar.running {
      inline-size: 35%;
      animation: scs-run 1.4s ease-in-out infinite;
    }
    @keyframes scs-run {
      from {
        inset-inline-start: -35%;
      }
      to {
        inset-inline-start: 100%;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .bar {
        transition: none;
      }
      .bar.running {
        inset-inline-start: 12.5%;
        animation: none;
      }
    }
  `,
  template: `
    @if (label()) {
      <span class="words" [id]="id + '-words'">{{ label() }}</span>
    }
    <div
      class="track"
      role="progressbar"
      aria-valuemin="0"
      aria-valuemax="100"
      [attr.aria-valuenow]="share()"
      [attr.aria-labelledby]="label() ? id + '-words' : null"
      [attr.aria-label]="label() ? null : name() || null"
    >
      <span class="bar" [class.running]="share() === null" [style.inline-size.%]="share()"></span>
    </div>
  `,
})
export class ScsProgressComponent {
  protected readonly id = `scs-progress-${nextId++}`;
  /** The share done, 0–100; `null` while it is not known. */
  readonly value = input<number | null>(null);
  /** Already translated words over the track: "Syncing — 62%". */
  readonly label = input('');
  /** Its name for assistive technology when it has no words. */
  readonly name = input('');
  readonly intent = input<'brand' | 'success' | 'warning' | 'danger'>('brand');

  protected readonly share = computed(() => {
    const value = this.value();
    return value === null ? null : Math.max(0, Math.min(100, Math.round(value)));
  });
}

