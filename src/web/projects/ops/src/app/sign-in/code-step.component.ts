import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from 'core';
import { ScsButtonDirective, ScsCodeInputComponent, ScsCodeState, ScsLinkDirective } from 'ui';
import { OpsCodeStep } from '../auth/ops-auth.service';

/** What the code boxes have to say under them. */
export type CodeProblem = 'wrong' | 'notResent' | 'resendLimit' | 'unexpected' | null;

/**
 * The six boxes, the countdown and "send again": one piece, used by the sign-in's code step
 * and by the change of password (spec 010, FR-020 to FR-022, FR-045a; design frame `135:61`).
 * The countdown is computed from the platform's time, so a wrong clock does not spoil it.
 */
@Component({
  selector: 'app-code-step',
  imports: [TranslocoPipe, ScsButtonDirective, ScsCodeInputComponent, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-md);
    }
    scs-code-input {
      inline-size: 100%;
      max-inline-size: 372px;
      align-self: center;
    }
    .wait,
    .problem {
      margin: 0;
      text-align: center;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-tertiary);
    }
    .problem {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
    }
    .again {
      align-self: center;
    }
  `,
  template: `
    <scs-code-input
      #boxes
      [label]="'app.code.boxes' | transloco"
      [state]="state()"
      [(value)]="code"
      (completed)="submitted.emit($event)"
    />

    @if (problem(); as problem) {
      <p class="problem" role="alert" data-testid="code-problem">
        {{ 'app.code.problem.' + problem | transloco }}
      </p>
    }

    @if (seconds() > 0) {
      <p class="wait" data-testid="code-wait">
        {{ 'app.code.waitBefore' | transloco }}
        <bdi data-testid="code-countdown">{{ countdown() }}</bdi>
      </p>
    } @else if (step().resendsLeft > 0) {
      <button type="button" scsLink class="again" data-testid="code-resend" (click)="resend.emit()">
        {{ 'app.code.resend' | transloco }}
      </button>
    }

    <button
      type="button"
      scsButton
      data-testid="code-submit"
      [busy]="busy()"
      [disabled]="code().length !== 6"
      (click)="submitted.emit(code())"
    >
      {{ confirmLabel() }}
    </button>
  `,
})
export class CodeStepComponent {
  private readonly language = inject(LanguageService);

  readonly step = input.required<OpsCodeStep>();
  readonly problem = input<CodeProblem>(null);
  readonly busy = input(false);

  /** Already translated. */
  readonly confirmLabel = input.required<string>();

  readonly submitted = output<string>();
  readonly resend = output<void>();

  protected readonly code = signal('');
  protected readonly seconds = signal(0);

  protected readonly state = computed<ScsCodeState>(() => (this.problem() === 'wrong' ? 'error' : 'idle'));

  /** Minutes and seconds, in the reader's digits. */
  protected readonly countdown = computed(() => {
    const total = this.seconds();
    const digits = new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG' : 'en-GB', {
      minimumIntegerDigits: 2,
      useGrouping: false,
    });
    return `${digits.format(Math.floor(total / 60))}:${digits.format(total % 60)}`;
  });

  private readonly boxes = viewChild.required<ScsCodeInputComponent>('boxes');
  private waitEndsAt = 0;

  constructor() {
    const timer = setInterval(() => this.count(), 500);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    // A new code, or a wrong one: the boxes start empty again.
    effect(() => {
      const step = this.step();
      this.waitEndsAt = Date.now() + (Date.parse(step.resendAt) - Date.parse(step.now));
      this.count();
      this.clear();
    });
    effect(() => {
      if (this.problem() === 'wrong') {
        this.clear();
      }
    });
  }

  /** Empties the boxes and puts the cursor in the first. */
  clear(): void {
    this.code.set('');
    queueMicrotask(() => this.boxes().focus());
  }

  private count(): void {
    this.seconds.set(Math.max(0, Math.ceil((this.waitEndsAt - Date.now()) / 1000)));
  }
}
