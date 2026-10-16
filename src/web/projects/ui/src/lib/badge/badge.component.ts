import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsIconName } from '../icon/icon-registry';

/** What a badge says about its word: done, waiting, stopped, for information, plain, or the brand's. */
export type ScsBadgeIntent = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';


/**
 * A short word with its colour (design: Badge — `294:147` / `8:86`; the clinic's `4253:53652`). Soft
 * by default, `solid` for the clinic's filled badge; a dot before the word, which a plain mark (a
 * permission's name) leaves out, or a leading icon. The colour is never the only sign — the word says
 * it. Each look draws its own size and corners through the `--scs-badge-*` settings: the clinic's
 * round 24-pixel badge by default (`size` sm: 20), the operators' 28-pixel badge with corners 8.
 * It took the place of spec 011's `scs-status-mark`.
 */
@Component({
  selector: 'scs-badge',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-intent]': 'shownIntent()',
    '[attr.data-size]': 'size()',
    '[class.plain]': '!dot() || icon()',
    '[class.solid]': 'solid()',
  },
  styles: `
    :host {
      --tint: var(--scs-color-bg-surface-secondary);
      --ink: var(--scs-badge-neutral-text, var(--scs-color-text-secondary));
      --strong: var(--scs-color-text-placeholder);
      display: inline-flex;
      align-items: center;
      gap: var(--scs-badge-gap, 6px);
      box-sizing: border-box;
      block-size: var(--scs-badge-height, 24px);
      padding-inline: var(--scs-badge-pad, 8px);
      border-radius: var(--scs-badge-radius, 999px);
      background: var(--tint);
      color: var(--ink);
      font-size: var(--scs-badge-text, 12px);
      font-weight: 600;
      line-height: 1;
      white-space: nowrap;
      max-inline-size: 100%;
    }
    /* A word longer than the room ends with an ellipsis; a screen reader reads it whole. */
    .words {
      min-inline-size: 0;
      overflow-x: clip;
      text-overflow: ellipsis;
    }
    :host([data-size='sm']) {
      gap: 4px;
      block-size: var(--scs-badge-height-sm, 20px);
      padding-inline: var(--scs-badge-pad-sm, 6px);
      font-size: 11px;
    }
    :host::before {
      content: '';
      flex: none;
      inline-size: 6px;
      block-size: 6px;
      border-radius: 50%;
      background: currentColor;
    }
    :host(.plain)::before {
      display: none;
    }
    :host([data-intent='success']) {
      --tint: var(--scs-color-feedback-success-bg);
      --ink: var(--scs-color-feedback-success-text);
      --strong: var(--scs-color-feedback-success-icon);
    }
    :host([data-intent='warning']) {
      --tint: var(--scs-color-feedback-warning-bg);
      --ink: var(--scs-color-feedback-warning-text);
      --strong: var(--scs-color-feedback-warning-icon);
    }
    :host([data-intent='danger']) {
      --tint: var(--scs-color-feedback-danger-bg);
      --ink: var(--scs-color-feedback-danger-text);
      --strong: var(--scs-color-feedback-danger-icon);
    }
    :host([data-intent='info']) {
      --tint: var(--scs-color-feedback-info-bg);
      --ink: var(--scs-color-feedback-info-text);
      --strong: var(--scs-color-feedback-info-icon);
    }
    :host([data-intent='brand']) {
      --tint: var(--scs-color-brand-subtle);
      --ink: var(--scs-badge-brand-text, var(--scs-color-brand-active));
      --strong: var(--scs-color-brand-default);
    }
    :host(.solid) {
      background: var(--strong);
      color: var(--scs-color-brand-on-brand);
    }
  `,
  template: `
    @if (icon(); as name) {
      <scs-icon [name]="name" [size]="size() === 'sm' ? 12 : 14" />
    }
    <span class="words"><ng-content /></span>
  `,
})
export class ScsBadgeComponent {
  readonly intent = input<ScsBadgeIntent | null>(null);
  /** The dot before the word; a plain mark (a permission's name) has none. */
  readonly dot = input(true, { transform: booleanAttribute });
  /** Filled with the intent's colour, white words (the clinic's "Solid"). */
  readonly solid = input(false, { transform: booleanAttribute });
  readonly size = input<'sm' | 'md'>('md');
  /** An icon before the word, in place of the dot. */
  readonly icon = input<ScsIconName | null>(null);

  protected readonly shownIntent = computed<ScsBadgeIntent>(() => this.intent() ?? 'neutral');
}

