import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  AuthCodes,
  PasswordCodes,
  PasswordRule,
  ScsProblemError,
  checkPassword,
  rulesRefused,
} from 'core';
import { ScsPasswordRulesComponent, ScsTextFieldComponent } from 'ui';

/** What the banner above a password form says when the platform could not do what was asked. */
export type PasswordBanner =
  { kind: 'unreachable' | 'unexpected' } | { kind: 'rateLimited'; seconds: number };

/** Turns a failed call into the banner that explains it. */
export function bannerFor(error: unknown): PasswordBanner {
  if (!(error instanceof ScsProblemError)) {
    return { kind: 'unexpected' };
  }
  if (error.local) {
    return { kind: 'unreachable' };
  }
  return error.problem.code === AuthCodes.rateLimited
    ? { kind: 'rateLimited', seconds: error.retryAfterSeconds ?? 60 }
    : { kind: 'unexpected' };
}

/**
 * What is typed into a new-password field and how it stands against the rules (spec 004,
 * FR-025 to FR-029). The same on every screen that sets a password: one field, four lines, no
 * second field to type it again.
 */
export class NewPasswordForm {
  readonly password = signal('');

  /** The form was submitted at least once: unmet rules are now shown as refused. */
  readonly submitted = signal(false);

  /** The platform said this is the password being replaced. */
  readonly sameAsCurrent = signal(false);

  /** Rules the platform refused although the browser thought them met. Never expected; never hidden. */
  private readonly refused = signal<readonly PasswordRule[]>([]);

  private readonly check = computed(() => checkPassword(this.password()));

  readonly met = computed<Record<PasswordRule, boolean>>(() => {
    const check = this.check();
    const refused = this.refused();
    return {
      length: check.length && !refused.includes('length'),
      cases: check.cases && !refused.includes('cases'),
      digit: check.digit && !refused.includes('digit'),
      symbol: check.symbol && !refused.includes('symbol'),
    };
  });

  readonly tooLong = computed(() => this.submitted() && this.check().tooLong);

  type(text: string): void {
    this.password.set(text);
    this.sameAsCurrent.set(false);
    this.refused.set([]);
  }

  /** Marks the form submitted. True when the password may be sent; otherwise no call is made. */
  submit(): boolean {
    this.submitted.set(true);
    this.sameAsCurrent.set(false);
    return this.check().ok;
  }

  /** Shows the platform's refusal of the password. False when the failure was about something else. */
  refuse(error: unknown): boolean {
    const codes = error instanceof ScsProblemError ? error.problem.errors?.['password'] : undefined;
    if (codes === undefined) {
      return false;
    }
    this.sameAsCurrent.set(codes.includes(PasswordCodes.sameAsCurrent));
    this.refused.set(rulesRefused(codes));
    return true;
  }
}

/**
 * The new-password field with its four rules under it. A refused password is shown by its red
 * rule alone — no second message under the field — except for the two things no rule line
 * describes: a password equal to the current one, and one that is too long.
 */
@Component({
  selector: 'app-new-password',
  imports: [TranslocoPipe, ScsTextFieldComponent, ScsPasswordRulesComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-spacing-12);
    }
  `,
  template: `
    <scs-text-field
      #field
      kind="password"
      testId="new-password"
      autocomplete="new-password"
      [label]="'app.password.field.label' | transloco"
      [error]="
        form().sameAsCurrent()
          ? ('app.password.sameAsCurrent' | transloco)
          : form().tooLong()
            ? ('app.password.tooLong' | transloco)
            : ''
      "
      [value]="form().password()"
      (valueChange)="form().type($event)"
    />
    <scs-password-rules
      [submitted]="form().submitted()"
      [rules]="[
        { key: 'length', label: 'app.password.rules.length' | transloco, met: form().met().length },
        { key: 'cases', label: 'app.password.rules.cases' | transloco, met: form().met().cases },
        { key: 'digit', label: 'app.password.rules.digit' | transloco, met: form().met().digit },
        { key: 'symbol', label: 'app.password.rules.symbol' | transloco, met: form().met().symbol },
      ]"
    />
  `,
})
export class NewPasswordComponent {
  readonly form = input.required<NewPasswordForm>();

  private readonly field = viewChild.required<ScsTextFieldComponent>('field');

  focus(): void {
    this.field().focus();
  }
}
