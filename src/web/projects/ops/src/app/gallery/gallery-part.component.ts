import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { GalleryState } from './gallery-state';

/**
 * A drawn part beside a page's component sets (an open list, a calendar, a menu panel — FR-002), or
 * a part kept as it is ("Other parts"): its name, its node and its specimen, laid out as a
 * component's specimens are. `testId` names it for the gallery's checks.
 */
@Component({
  selector: 'app-gallery-part',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      margin-block-start: var(--scs-spacing-lg);
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--scs-spacing-sm) var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-sm);
    }
    h3 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    .node {
      font-family: ui-monospace, Consolas, monospace;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .body {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-lg);
      align-items: flex-start;
    }
  `,
  template: `
    @if (visible()) {
      <header>
        <h3 data-scs-label>{{ name() }}</h3>
        @if (node()) {
          <span class="node" data-scs-label>{{ node() }}</span>
        }
      </header>
      <div class="body" [attr.data-testid]="testId()"><ng-content /></div>
    }
  `,
})
export class GalleryPartComponent {
  private readonly state = inject(GalleryState);
  readonly name = input.required<string>();
  readonly node = input<string>();
  readonly testId = input.required<string>();
  /** Shown in the clinic look too. */
  readonly shared = input(true);
  protected readonly visible = computed(() => this.state.look() === 'ops' || this.shared());
}
