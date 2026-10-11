import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsIconComponent } from '../icon/icon.component';

/**
 * What a person sees at an address their role does not open (spec 005, FR-029; design frame
 * "Roles / No Access"): a lock, "this page is not in your role", which permission opens it,
 * which role they hold, and whom to ask. It shows nothing of the page itself.
 *
 * Both names arrive already in the page's language and are part of the sentence, so assistive
 * technology reads them. With no role name, the sentence says the account holds no role yet.
 */
@Component({
  selector: 'scs-no-access',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'data-testid': 'no-access' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 96px var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      text-align: center;
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 64px;
      block-size: 64px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-icon);
    }
    h2 {
      margin: var(--scs-spacing-md) 0 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
      line-height: 1.5;
      color: var(--scs-color-text-primary);
    }
    p {
      max-inline-size: 272px;
      margin: var(--scs-spacing-12) 0 0;
      font-size: var(--scs-text-body-sm);
      line-height: 24px;
      color: var(--scs-color-text-secondary);
      overflow-wrap: anywhere;
    }
  `,
  template: `
    <span class="mark"><scs-icon name="lock" [size]="24" /></span>
    <h2 data-testid="no-access-title">{{ 'ui.access.noAccess.title' | transloco }}</h2>
    <p data-testid="no-access-text">
      @if (roleName()) {
        {{
          'ui.access.noAccess.text'
            | transloco: { permission: permissionName(), role: roleName() }
        }}
      } @else {
        {{ 'ui.access.noAccess.textNoRole' | transloco: { permission: permissionName() } }}
      }
    </p>
  `,
})
export class ScsNoAccessComponent {
  /** The name of the permission that opens the page, in the page's language. */
  readonly permissionName = input.required<string>();

  /** The name of the person's role; empty when their account holds none. */
  readonly roleName = input('');
}
