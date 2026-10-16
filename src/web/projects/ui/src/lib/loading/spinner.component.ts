import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A ring turning while something loads (design: Loading — Spinner, `289:27` / `314:54`; the clinic's
 * `4252:59832`): a faint track and a third of it in colour. In four sizes; the brand's colour, grey,
 * or white on a dark ground. With a `label` it says what is loading to assistive technology;
 * without, it is a picture beside words that already say it. Each look sets its sizes through
 * `--scs-spinner-*`: the clinic's 16, 20, 24 and 32 by default, the operators' 16, 24, 32 and 48.
 */
@Component({
  selector: 'scs-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.role]': 'label() ? "status" : null',
    '[attr.aria-label]': 'label() || null',
    '[attr.aria-hidden]': 'label() ? null : "true"',
    '[attr.data-size]': 'size()',
    '[attr.data-tone]': 'tone()',
  },
  styles: `
    :host {
      --side: var(--scs-spinner-md, 20px);
      display: inline-block;
      flex: none;
      inline-size: var(--side);
      block-size: var(--side);
      color: var(--scs-color-brand-default);
      line-height: 0;
    }
    :host([data-size='sm']) {
      --side: var(--scs-spinner-sm, 16px);
    }
    :host([data-size='lg']) {
      --side: var(--scs-spinner-lg, 24px);
    }
    :host([data-size='xl']) {
      --side: var(--scs-spinner-xl, 32px);
    }
    :host([data-tone='neutral']) {
      color: var(--scs-color-icon-default);
    }
    :host([data-tone='inverse']) {
      color: var(--scs-color-brand-on-brand);
    }
    svg {
      inline-size: 100%;
      block-size: 100%;
      animation: scs-spin 0.8s linear infinite;
    }
    circle {
      fill: none;
      stroke: currentColor;
      stroke-width: 3;
    }
    .track {
      opacity: 0.2;
    }
    @keyframes scs-spin {
      to {
        transform: rotate(360deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      svg {
        animation-duration: 2.4s;
      }
    }
  `,
  // A 25-unit box with a 3-unit stroke: 12% of the side, as drawn. The arc is three eighths of the
  // ring from the top, as drawn (from -90° to 45°).
  template: `
    <svg viewBox="0 0 25 25" aria-hidden="true">
      <circle class="track" cx="12.5" cy="12.5" r="11" />
      <circle cx="12.5" cy="12.5" r="11" stroke-dasharray="25.92 69.12" transform="rotate(-90 12.5 12.5)" />
    </svg>
  `,
})
export class ScsSpinnerComponent {
  readonly size = input<'sm' | 'md' | 'lg' | 'xl'>('md');
  readonly tone = input<'brand' | 'neutral' | 'inverse'>('brand');
  /** Already translated: what is loading. */
  readonly label = input('');
}
