import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A line between two parts of a page, the operators console's own (design: Data Display — Divider,
 * `281:88` / `313:87`): plain, or with a word in its middle ("or" · «أو»). A plain line is only a
 * picture; one with a word is a separator that says it.
 */
@Component({
  selector: 'app-divider',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'separator',
    '[attr.aria-hidden]': 'label() ? null : "true"',
    '[attr.aria-label]': 'label() || null',
    '[class.plain]': '!label()',
  },
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      block-size: 30px;
      color: var(--scs-color-text-tertiary);
      font-size: 11px;
    }
    :host::before,
    :host::after {
      content: '';
      flex: 1;
      block-size: 1px;
      background: var(--scs-divider-line, var(--scs-color-border-default));
    }
    :host(.plain) {
      gap: 0;
    }
    span {
      flex: none;
      padding-inline: var(--scs-spacing-sm);
    }
  `,
  template: `
    @if (label()) {
      <span aria-hidden="true">{{ label() }}</span>
    }
  `,
})
export class DividerComponent {
  /** Already translated: the word in the middle. */
  readonly label = input('');
}
