import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * A small line of how a number moved, the operators console's own (design: Chart — Sparkline,
 * `394:15` / `395:14`): drawn from a list of numbers, green when it ends higher than it began, red
 * when lower, grey when level, with a dot at its last point. It is a picture named by `label`, which
 * says what it shows. It is drawn left to right in both languages, as time is.
 */
@Component({
  selector: 'app-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'img', '[attr.aria-label]': 'label() || null', '[attr.data-trend]': 'trend()' },
  styles: `
    :host {
      --ink: var(--scs-color-text-tertiary);
      display: inline-block;
      inline-size: 80px;
      block-size: 24px;
      color: var(--ink);
    }
    :host([data-trend='up']) {
      --ink: var(--scs-color-feedback-success-text);
    }
    :host([data-trend='down']) {
      --ink: var(--scs-color-feedback-danger-text);
    }
    svg {
      display: block;
      inline-size: 100%;
      block-size: 100%;
      overflow: visible;
    }
  `,
  template: `
    <svg viewBox="0 0 80 24" preserveAspectRatio="none" aria-hidden="true">
      <polyline [attr.points]="line()" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke" />
      @if (end(); as dot) {
        <circle [attr.cx]="dot.x" [attr.cy]="dot.y" r="2.5" fill="currentColor" />
      }
    </svg>
  `,
})
export class SparklineComponent {
  readonly points = input.required<readonly number[]>();
  /** Already translated: what it shows ("Clinics, rising over the month"). */
  readonly label = input('');

  private readonly placed = computed(() => {
    const points = this.points();
    const low = Math.min(...points);
    const high = Math.max(...points);
    const span = high - low || 1;
    const step = points.length > 1 ? 80 / (points.length - 1) : 0;
    // 2 of room at the top and the bottom, so the dot is whole.
    return points.map((value, index) => ({ x: +(index * step).toFixed(2), y: +(22 - ((value - low) / span) * 20).toFixed(2) }));
  });

  protected readonly line = computed(() => this.placed().map((point) => `${point.x},${point.y}`).join(' '));
  protected readonly end = computed(() => this.placed().at(-1) ?? null);
  protected readonly trend = computed(() => {
    const points = this.points();
    const first = points[0] ?? 0;
    const last = points.at(-1) ?? 0;
    return last > first ? 'up' : last < first ? 'down' : 'flat';
  });
}
