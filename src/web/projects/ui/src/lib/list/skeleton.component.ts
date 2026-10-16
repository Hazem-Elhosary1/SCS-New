import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input } from '@angular/core';

/**
 * Grey shapes in place of content that is loading (design: Loading — Skeleton Line, `289:48`; the
 * clinic's Skeleton `4252:59877`): `rows` lines of changing widths, or lines of the given
 * `widths` (the page draws 280, 220 and 140). Each look sets the line through `--scs-skeleton-*`;
 * the defaults are the lines the screens had before spec 095.
 */
@Component({
  selector: 'scs-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status', 'aria-busy': 'true', '[attr.aria-label]': 'label() || null' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-12);
    }
    span {
      display: block;
      max-inline-size: 100%;
      block-size: var(--scs-skeleton-line, 16px);
      border-radius: var(--scs-skeleton-radius, var(--scs-radius-sm));
      background: var(--scs-skeleton-bg, var(--scs-color-bg-surface-secondary));
      animation: pulse 1.4s ease-in-out infinite;
    }
    span:nth-child(3n + 1) {
      inline-size: 60%;
    }
    span:nth-child(3n + 2) {
      inline-size: 85%;
    }
    @keyframes pulse {
      50% {
        opacity: 0.5;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      span {
        animation: none;
      }
    }
  `,
  template: `
    @for (line of lines(); track $index) {
      <span [style.inline-size]="line"></span>
    }
  `,
})
export class ScsSkeletonComponent {
  readonly rows = input(4);

  /** Already translated; what is loading, for assistive technology. */
  readonly label = input('');

  /** Each line's width, in pixels or as any length; in place of `rows`. */
  readonly widths = input<readonly (number | string)[] | null>(null);

  protected readonly lines = computed(() =>
    this.widths()?.map((width) => (typeof width === 'number' ? `${width}px` : width)) ??
    Array.from({ length: this.rows() }, () => null),
  );
}

/**
 * One grey shape for a composed skeleton (design: Loading — "Skeleton — Composed Examples", `290:4`,
 * `290:11`): a block of a width and a height, or a circle. Only a picture; the skeleton or the page
 * around it says that something is loading.
 */
@Component({
  selector: 'scs-skeleton-block',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[class.round]': 'round()',
    '[style.inline-size]': 'length(width())',
    '[style.block-size]': 'length(height())',
  },
  styles: `
    :host {
      display: block;
      flex: none;
      max-inline-size: 100%;
      border-radius: var(--scs-skeleton-block-radius, var(--scs-radius-sm));
      background: var(--scs-skeleton-bg, var(--scs-color-bg-surface-secondary));
      animation: pulse 1.4s ease-in-out infinite;
    }
    :host(.round) {
      border-radius: 50%;
    }
    @keyframes pulse {
      50% {
        opacity: 0.5;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      :host {
        animation: none;
      }
    }
  `,
  template: '',
})
export class ScsSkeletonBlockComponent {
  readonly width = input<number | string>('100%');
  readonly height = input<number | string>(14);
  readonly round = input(false, { transform: booleanAttribute });

  protected length(value: number | string): string {
    return typeof value === 'number' ? `${value}px` : value;
  }
}
