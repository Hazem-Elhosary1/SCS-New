import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsProblemError } from 'core';
import { ScsAuthFrameComponent, ScsLinkDirective } from 'ui';
import { OpsAuthService, OpsCodes } from '../auth/ops-auth.service';
import { CodeProblem, CodeStepComponent } from './code-step.component';

/**
 * Step two of an operator's sign-in (spec 010; design frames `135:61`, `135:38`): the six
 * digits sent by email. Opened with no sign-in in progress, it goes back to the form.
 */
@Component({
  selector: 'app-code-page',
  imports: [TranslocoPipe, ScsAuthFrameComponent, ScsLinkDirective, CodeStepComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    h1 {
      margin: 0;
      font-size: var(--scs-text-display-sm);
      font-weight: 700;
    }
    .subtitle {
      margin: var(--scs-spacing-sm) 0 var(--scs-spacing-lg);
      color: var(--scs-color-text-secondary);
    }
    .back {
      display: block;
      margin: var(--scs-spacing-md) auto 0;
    }
  `,
  template: `
    <scs-auth-frame
      [panelTitle]="'app.code.panel.title' | transloco"
      [panelLine]="'app.code.panel.line' | transloco"
    >
      @if (auth.codeStep(); as step) {
        <h1 data-testid="code-title">{{ 'app.code.title' | transloco }}</h1>
        <p class="subtitle">
          {{ 'app.code.sentTo' | transloco }} <bdi data-testid="code-email">{{ step.email }}</bdi>
        </p>

        <app-code-step
          [step]="step"
          [problem]="problem()"
          [busy]="busy()"
          [confirmLabel]="'app.code.submit' | transloco"
          (submitted)="confirm($event)"
          (resend)="resend()"
        />

        <button type="button" scsLink class="back" data-testid="code-back" (click)="back()">
          {{ 'app.code.back' | transloco }}
        </button>
      }
    </scs-auth-frame>
  `,
})
export class CodePage {
  protected readonly auth = inject(OpsAuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly problem = signal<CodeProblem>(null);
  protected readonly busy = signal(false);

  constructor() {
    if (this.auth.codeStep() === null) {
      void this.router.navigateByUrl('/sign-in');
    }
  }

  protected async confirm(code: string): Promise<void> {
    if (this.busy() || code.length !== 6) {
      return;
    }

    this.busy.set(true);
    this.problem.set(null);
    try {
      await this.auth.confirm(code);
      const next = this.route.snapshot.queryParamMap.get('next');
      await this.router.navigateByUrl(next !== null && next.startsWith('/') ? next : '/');
    } catch (error) {
      await this.refused(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async resend(): Promise<void> {
    this.problem.set(null);
    try {
      await this.auth.resend();
    } catch (error) {
      await this.refused(error);
    }
  }

  protected async back(): Promise<void> {
    await this.auth.cancel();
    await this.router.navigateByUrl('/sign-in');
  }

  private async refused(error: unknown): Promise<void> {
    const code = error instanceof ScsProblemError ? error.problem.code : '';
    switch (code) {
      case OpsCodes.codeInvalid:
        this.problem.set('wrong');
        return;
      case OpsCodes.codeNotResent:
      case OpsCodes.resendTooEarly:
        this.problem.set('notResent');
        return;
      case OpsCodes.resendLimit:
        this.problem.set('resendLimit');
        return;
      case OpsCodes.stepExpired:
      case OpsCodes.accountLocked:
      case OpsCodes.accountDisabled:
        // The step is over: the form starts again, and says why when there is a why.
        this.auth.forgetCodeStep();
        if (code === OpsCodes.accountLocked) {
          this.auth.say('locked');
        } else if (code === OpsCodes.accountDisabled) {
          this.auth.say('suspended');
        } else {
          this.auth.say('expired');
        }
        await this.router.navigateByUrl('/sign-in');
        return;
      default:
        this.problem.set('unexpected');
    }
  }
}
