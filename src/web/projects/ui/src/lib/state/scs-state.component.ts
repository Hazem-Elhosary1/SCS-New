import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsBannerComponent, ScsButtonDirective } from '../controls/controls';
import { ScsEmptyStateComponent, ScsSkeletonComponent } from '../list/list';

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
 * - `offline`: the frame's warning banner and sentence. A page header shows it by itself.
 * - `not-in-plan`: pass the line that names the plan as `message`, and project the upgrade
 *   path with the attribute `scsUpgrade`.
 */
@Component({
  selector: 'scs-state',
  imports: [
    TranslocoPipe,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsEmptyStateComponent,
    ScsSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'state()' },
  styles: `
    :host {
      display: block;
    }
    .notice {
      margin-block-end: var(--scs-spacing-md);
    }
  `,
  template: `
    @switch (state()) {
      @case ('loading') {
        <scs-skeleton [label]="message() || ('ui.state.loading.title' | transloco)" />
      }
      @case ('empty') {
        <scs-empty-state [title]="message() || ('ui.state.empty.title' | transloco)">
          <ng-content select="[scsAction]" />
        </scs-empty-state>
      }
      @case ('error') {
        <scs-empty-state context="error" [title]="message() || ('ui.state.error.title' | transloco)">
          @if (reference(); as reference) {
            <p class="detail">
              {{ 'ui.state.error.reference' | transloco }}:
              <code data-testid="state-reference"
                ><bdi>{{ reference }}</bdi></code
              >
            </p>
          }
          <button type="button" scsButton="secondary" data-testid="state-retry" (click)="retry.emit()">
            {{ 'ui.state.error.retry' | transloco }}
          </button>
        </scs-empty-state>
      }
      @case ('offline') {
        <!-- The warning banner the frame shows under a page's header (design: "Shell / Offline"). -->
        <scs-banner tone="warning" data-testid="offline-alert">{{
          message() || ('ui.state.offline.message' | transloco)
        }}</scs-banner>
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
        <scs-empty-state
          context="not-in-plan"
          [title]="'ui.state.notInPlan.title' | transloco"
          [text]="message()"
        >
          <ng-content select="[scsUpgrade]" />
        </scs-empty-state>
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
