import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsIconName } from '../icon/icon-registry';

/** What a tag says: the console's own colour, done, to watch, or plain. */
export type ScsTagIntent = 'default' | 'success' | 'warning' | 'neutral';

/**
 * A short label in a pill (design: Data Display — Tag, `281:64` / `313:63`; the clinic's
 * `4253:53827`): a word, an optional icon before it, and — when `dismissible` — a button that takes
 * it away, named by `removeLabel`. The clinic's tag can be chosen (`selected`) or not available
 * (`disabled`). Each look draws it through `--scs-tag-*`: the clinic's grey 32-pixel pill by
 * default, the operators' 28-pixel pill in the console's purple.
 */
@Component({
  selector: 'scs-tag',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-intent]': 'intent()',
    '[attr.data-size]': 'size()',
    '[class.chosen]': 'selected()',
    '[class.off]': 'disabled()',
  },
  styles: `
    :host {
      --tint: var(--scs-tag-default-bg, var(--scs-color-bg-surface-secondary));
      --ink: var(--scs-tag-default-text, var(--scs-color-text-primary));
      display: inline-flex;
      align-items: center;
      gap: var(--scs-tag-gap, 6px);
      box-sizing: border-box;
      max-inline-size: 100%;
      block-size: var(--scs-tag-height, 32px);
      padding-inline: var(--scs-tag-pad, 10px);
      border: var(--scs-tag-border, 1px) solid transparent;
      border-radius: var(--scs-tag-radius, 999px);
      background: var(--tint);
      color: var(--ink);
      font-size: var(--scs-tag-text, 13px);
      font-weight: 500;
      white-space: nowrap;
    }
    :host([data-size='sm']) {
      gap: 4px;
      block-size: var(--scs-tag-height-sm, 24px);
      padding-inline: var(--scs-tag-pad-sm, 8px);
      font-size: 12px;
    }
    :host([data-intent='success']) {
      --tint: var(--scs-color-feedback-success-bg);
      --ink: var(--scs-color-feedback-success-text);
    }
    :host([data-intent='warning']) {
      --tint: var(--scs-color-feedback-warning-bg);
      --ink: var(--scs-color-feedback-warning-text);
    }
    :host([data-intent='neutral']) {
      --tint: var(--scs-tag-neutral-bg, var(--scs-color-bg-surface-secondary));
      --ink: var(--scs-tag-neutral-text, var(--scs-color-text-primary));
    }
    :host(:hover:not(.off)),
    :host-context([data-scs-state~='hover']) {
      background: var(--scs-tag-hover-bg, color-mix(in srgb, var(--tint) 100%, var(--scs-color-text-primary) 6%));
    }
    :host(.chosen) {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
    }
    :host(.off) {
      color: var(--scs-color-text-placeholder);
    }
    .words {
      overflow: hidden;
      text-overflow: ellipsis;
    }
    button {
      display: inline-grid;
      place-items: center;
      flex: none;
      /* On a phone it is 40 to press; the room is made around the mark, and the pill keeps its size. */
      min-inline-size: var(--scs-touch, 0px);
      min-block-size: var(--scs-touch, 0px);
      margin: min(0px, calc((16px - var(--scs-touch, 0px)) / 2));
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: none;
      color: var(--scs-tag-remove, inherit);
      cursor: pointer;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    button:disabled {
      cursor: not-allowed;
    }
  `,
  template: `
    @if (icon(); as name) {
      <scs-icon [name]="name" [size]="size() === 'sm' ? 12 : 16" />
    }
    <span class="words"><ng-content /></span>
    @if (dismissible()) {
      <button type="button" data-testid="tag-remove" [attr.aria-label]="removeLabel()" [disabled]="disabled()" (click)="removed.emit()">
        <scs-icon name="close" [size]="size() === 'sm' ? 12 : 14" />
      </button>
    }
  `,
})
export class ScsTagComponent {
  readonly intent = input<ScsTagIntent>('default');
  readonly size = input<'sm' | 'md'>('md');
  readonly icon = input<ScsIconName | null>(null);
  /** Has a button that takes it away. */
  readonly dismissible = input(false, { transform: booleanAttribute });
  /** Already translated: that button's name, with the tag's word ("Remove Dental"). */
  readonly removeLabel = input('');
  readonly selected = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });

  readonly removed = output<void>();
}
