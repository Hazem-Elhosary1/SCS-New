import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';
import { ScsSpinnerComponent } from '../loading/spinner.component';

export type ScsEmptyContext = 'no-data' | 'no-results' | 'error' | 'not-in-plan' | 'not-found' | 'loading' | 'success';

const EMPTY_ICONS: Record<ScsEmptyContext, ScsIconName> = {
  'no-data': 'clipboard',
  'no-results': 'search',
  error: 'alert-triangle',
  'not-in-plan': 'lock',
  'not-found': 'help-circle',
  loading: 'refresh',
  success: 'check',
};

/**
 * What stands where rows would be when there are none to show (design: Empty State — `374:35` /
 * `374:68`; the clinic's `4252:60047`): nothing yet, nothing matching, a failure, still loading, or
 * done. A title, one line, and at most one action, projected; while loading, a turning ring and its
 * words, said politely. Each look draws it through `--scs-empty-*`; the defaults are the empty
 * state before spec 095.
 */
@Component({
  selector: 'scs-empty-state',
  imports: [ScsIconComponent, ScsSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-context]': 'context()',
    '[attr.role]': 'context() === "error" ? "alert" : context() === "loading" || context() === "success" ? "status" : null',
  },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-40) var(--scs-spacing-lg);
      text-align: center;
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: var(--scs-empty-mark, 64px);
      block-size: var(--scs-empty-mark, 64px);
      margin-block-end: var(--scs-spacing-sm);
      border-radius: var(--scs-radius-full);
      background: var(--scs-empty-mark-bg, var(--scs-color-brand-subtle, var(--scs-color-bg-surface-secondary)));
      color: var(--scs-empty-mark-ink, var(--scs-color-brand-default));
    }
    :host ::ng-deep .mark svg {
      inline-size: var(--scs-empty-icon, 26px);
      block-size: var(--scs-empty-icon, 26px);
    }
    :host([data-context='error']) .mark {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-icon);
    }
    :host([data-context='success']) .mark {
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-icon);
    }
    scs-spinner {
      margin-block-end: var(--scs-spacing-sm);
    }
    h3 {
      margin: 0;
      font-size: var(--scs-empty-title, var(--scs-text-heading-sm));
      font-weight: var(--scs-empty-title-weight, 500);
      color: var(--scs-color-text-primary);
    }
    p {
      max-inline-size: 320px;
      margin: 0;
      font-size: var(--scs-empty-text, var(--scs-text-body-sm));
      line-height: 1.7;
      color: var(--scs-empty-text-color, var(--scs-color-text-secondary));
    }
    .action:not(:empty) {
      margin-block-start: var(--scs-spacing-sm);
    }
  `,
  template: `
    @if (context() === 'loading') {
      <scs-spinner size="xl" />
      <p data-testid="empty-title">{{ title() }}</p>
    } @else {
      <span class="mark"><scs-icon [name]="icon()" [size]="26" /></span>
      <h3 data-testid="empty-title">{{ title() }}</h3>
    }
    @if (text()) {
      <p data-testid="empty-text">{{ text() }}</p>
    }
    <div class="action"><ng-content /></div>
  `,
})
export class ScsEmptyStateComponent {
  readonly context = input<ScsEmptyContext>('no-data');
  readonly title = input.required<string>();
  readonly text = input('');

  /** The mark of what is missing, when the page has a better one than the context's own. */
  readonly mark = input<ScsIconName | null>(null);

  protected readonly icon = computed(() => this.mark() ?? EMPTY_ICONS[this.context()]);
}
