import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

/** One line under a new-password field. */
export interface ScsPasswordRule {
  /** A stable name for tests: `length`, `cases`, `digit`, `symbol`. */
  readonly key: string;
  /** Already translated. */
  readonly label: string;
  /** What is typed meets the rule. */
  readonly met: boolean;
}

export type ScsPasswordRuleState = 'pending' | 'met' | 'unmet';

/**
 * The rules a new password must meet, one line each, shown before anything is typed (spec 004,
 * FR-027; BRD B-R16). A line is neutral until its rule is met, when it turns green; a rule
 * still unmet when the form is submitted turns red. The state is also said in words to
 * assistive technology, never by colour alone.
 */
@Component({
  selector: 'scs-password-rules',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    ul {
      display: grid;
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    scs-icon {
      color: var(--scs-color-text-tertiary);
    }
    li[data-state='met'] {
      color: var(--scs-color-text-primary);
    }
    li[data-state='met'] scs-icon {
      color: var(--scs-color-feedback-success-icon);
    }
    li[data-state='unmet'],
    li[data-state='unmet'] scs-icon {
      color: var(--scs-color-feedback-danger-icon);
    }
    .said {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
  template: `
    <ul [attr.aria-label]="'ui.passwordRules.title' | transloco" data-testid="password-rules">
      @for (line of lines(); track line.key) {
        <li [attr.data-state]="line.state" [attr.data-testid]="'rule-' + line.key">
          <scs-icon [name]="line.icon" [size]="16" />
          <span>{{ line.label }}</span>
          <span class="said">{{ 'ui.passwordRules.' + line.state | transloco }}</span>
        </li>
      }
    </ul>
    <p class="said" role="status" aria-live="polite" data-testid="password-rules-summary">
      @if (submitted() && unmet() > 0) {
        {{ 'ui.passwordRules.summary' | transloco: { count: unmet() } }}
      }
    </p>
  `,
})
export class ScsPasswordRulesComponent {
  readonly rules = input.required<readonly ScsPasswordRule[]>();

  /** The form was submitted: rules still unmet are now shown as refused. */
  readonly submitted = input(false, { transform: booleanAttribute });

  protected readonly lines = computed(() =>
    this.rules().map((rule) => {
      const state: ScsPasswordRuleState = rule.met ? 'met' : this.submitted() ? 'unmet' : 'pending';
      const icon: ScsIconName =
        state === 'met' ? 'check-circle' : state === 'unmet' ? 'alert-circle' : 'circle';
      return { ...rule, state, icon };
    }),
  );

  protected readonly unmet = computed(() => this.rules().filter((rule) => !rule.met).length);
}
