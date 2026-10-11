import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsBannerComponent } from '../controls/controls';

/** The six non-content states every screen has (SH-097, contracts/frontend-runtime.md §5). */
export type ScsState = 'loading' | 'empty' | 'error' | 'offline' | 'view-only' | 'not-in-plan';

export const SCS_STATES: readonly ScsState[] = [
  'loading',
  'empty',
  'error',
  'offline',
  'view-only',
  'not-in-plan',
];

/**
 * Renders every non-content state. A screen says which state it is in; it does not draw its
 * own. Appearance comes only from the app's token variables, so the same component follows the
 * clinic design in two apps and the ops design in the third.
 *
 * - `error`: pass the platform's `title` as `message` and its `traceId` as `reference`.
 * - `view-only`: project the content; it is shown under the notice, without action controls.
 *   Pass as `message` the line that names the permission changing needs (spec 005, FR-030):
 *   the notice is the one of the design frame "Roles List Read Only", and the only one.
 * - `not-in-plan`: project the upgrade path with the attribute `scsUpgrade`.
 */
@Component({
  selector: 'scs-state',
  imports: [TranslocoPipe, ScsBannerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'state()' },
  styles: `
    :host {
      display: block;
    }
    .panel {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-primary);
    }
    .title {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
    }
    .detail {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .danger {
      border-color: var(--scs-color-feedback-danger-border);
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-text);
    }
    .warning {
      border-color: var(--scs-color-feedback-warning-border);
      background: var(--scs-color-feedback-warning-bg);
    }
    .info {
      border-color: var(--scs-color-feedback-info-border);
      background: var(--scs-color-feedback-info-bg);
    }
    .notice {
      margin-block-end: var(--scs-spacing-md);
    }
    .action {
      padding: var(--scs-spacing-6) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-brand-default);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .action:hover {
      background: var(--scs-color-brand-active);
    }
    .spinner {
      inline-size: var(--scs-spacing-20);
      block-size: var(--scs-spacing-20);
      border: 2px solid var(--scs-color-border-default);
      border-block-start-color: var(--scs-color-brand-default);
      border-radius: var(--scs-radius-full);
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .spinner {
        animation: none;
      }
    }
  `,
  template: `
    @switch (state()) {
      @case ('loading') {
        <div class="panel" role="status" aria-live="polite">
          <span class="spinner" aria-hidden="true"></span>
          <p class="title">{{ message() || ('ui.state.loading.title' | transloco) }}</p>
        </div>
      }
      @case ('empty') {
        <div class="panel">
          <p class="title">{{ message() || ('ui.state.empty.title' | transloco) }}</p>
          <ng-content select="[scsAction]" />
        </div>
      }
      @case ('error') {
        <div class="panel danger" role="alert">
          <p class="title" data-testid="state-message">
            {{ message() || ('ui.state.error.title' | transloco) }}
          </p>
          @if (reference(); as reference) {
            <p class="detail">
              {{ 'ui.state.error.reference' | transloco }}:
              <code data-testid="state-reference"
                ><bdi>{{ reference }}</bdi></code
              >
            </p>
          }
          <button type="button" class="action" data-testid="state-retry" (click)="retry.emit()">
            {{ 'ui.state.error.retry' | transloco }}
          </button>
        </div>
      }
      @case ('offline') {
        <div class="panel warning" role="status" aria-live="polite">
          <p class="title">{{ 'ui.state.offline.title' | transloco }}</p>
          <p class="detail">{{ 'ui.state.offline.detail' | transloco }}</p>
        </div>
      }
      @case ('view-only') {
        <scs-banner
          tone="info"
          class="notice"
          data-testid="view-only"
          [title]="'ui.state.viewOnly.title' | transloco"
        >
          @if (message()) {
            <span class="line">{{ message() }}</span>
          }
        </scs-banner>
        <ng-content />
      }
      @case ('not-in-plan') {
        <div class="panel info">
          <p class="title">{{ 'ui.state.notInPlan.title' | transloco }}</p>
          <ng-content select="[scsUpgrade]" />
        </div>
      }
    }
  `,
})
export class ScsStateComponent {
  readonly state = input.required<ScsState>();

  /**
   * Text to show instead of the default; for `error`, the platform's own `title`; for
   * `view-only`, the line under the title. Already translated.
   */
  readonly message = input<string>('');

  /** The request reference (`traceId`) shown with an error. */
  readonly reference = input<string>('');

  readonly retry = output<void>();
}
