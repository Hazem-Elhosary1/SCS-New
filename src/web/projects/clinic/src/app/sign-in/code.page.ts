import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthCodes, AuthService, ScsProblemError } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsButtonDirective,
  ScsCodeInputComponent,
  ScsCodeState,
  ScsIconComponent,
  ScsLinkDirective,
} from 'ui';
import { HelpWindowComponent } from './help-window.component';
import { SignInFlow } from './sign-in-flow';

type Banner =
  | { kind: 'invalid' | 'resendFailed' | 'accepted' | 'unreachable' }
  | { kind: 'rateLimited'; seconds: number };

/**
 * Step two of sign-in: the six-digit code from the email (spec 003; BRD B-5-2, B-5-3). Wrong,
 * expired and spent codes all look the same. Under the boxes a countdown runs until the code
 * may be sent again; after three resends there are no more for this sign-in.
 */
@Component({
  selector: 'app-code-page',
  imports: [
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsCodeInputComponent,
    ScsIconComponent,
    ScsLinkDirective,
    HelpWindowComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    h1 {
      margin: 0;
      font-size: var(--scs-text-display-sm);
      font-weight: 700;
    }
    .subtitle {
      margin: var(--scs-spacing-sm) 0 0;
      color: var(--scs-color-text-secondary);
    }
    form {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-lg);
      margin-block-start: var(--scs-spacing-lg);
    }
    .subtitle {
      font-size: var(--scs-text-body-lg);
      line-height: 1.8;
    }
    .under {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .under .scs-link {
      font-size: var(--scs-text-body-sm);
    }
    .back {
      align-self: flex-start;
    }
    /* The second button sits close under the first: the two are one choice. */
    .again {
      margin-block-start: calc(var(--scs-spacing-12) * -1);
    }
  `,
  template: `
    <scs-auth-frame (help)="helpOpen.set(true)">
      <h1 data-testid="code-title">{{ 'app.code.title' | transloco }}</h1>
      <p class="subtitle">
        {{ 'app.code.subtitle' | transloco }}
        <bdi data-testid="code-email">{{ email() }}</bdi>
      </p>

      <form novalidate (submit)="submit($event)">
        @if (banner(); as banner) {
          <scs-banner
            [tone]="
              banner.kind === 'accepted'
                ? 'success'
                : banner.kind === 'rateLimited'
                  ? 'warning'
                  : 'danger'
            "
            [attr.data-testid]="'banner-' + banner.kind"
          >
            @if (banner.kind === 'rateLimited') {
              {{ 'app.signIn.banner.rateLimitedBefore' | transloco }}
              <bdi data-testid="retry-after">{{ banner.seconds }}</bdi>
              {{ 'app.signIn.banner.rateLimitedAfter' | transloco }}
            } @else if (banner.kind === 'unreachable') {
              {{ 'app.signIn.banner.unreachable' | transloco }}
            } @else {
              {{ 'app.code.banner.' + banner.kind | transloco }}
            }
          </scs-banner>
        }

        <scs-code-input
          #boxes
          [label]="'app.code.label' | transloco"
          [state]="boxState()"
          [(value)]="code"
        />

        <button type="submit" scsButton data-testid="code-submit" [busy]="busy() || accepted()">
          {{ (accepted() ? 'app.code.opening' : 'app.code.submit') | transloco }}
        </button>

        <!-- Sending the code again is a button, with its icon, as soon as it is allowed: when
             the countdown is over, and after a resend that failed (owner decision, 10 Oct 2026). -->
        @if (!accepted() && resendsLeft() > 0 && (secondsLeft() === 0 || resendFailed())) {
          <button
            type="button"
            scsButton="secondary"
            class="again"
            data-testid="resend-button"
            [busy]="busy()"
            (click)="resend()"
          >
            <scs-icon name="refresh" [size]="20" />
            <span>{{ 'app.code.resendButton' | transloco }}</span>
          </button>
        }

        @if (!accepted()) {
          @if (resendsLeft() <= 0) {
            <p class="under" role="status" data-testid="no-more-resends">
              {{ 'app.code.noMoreResends' | transloco }}
            </p>
          } @else if (secondsLeft() > 0) {
            <p class="under" data-testid="resend-countdown">
              {{ 'app.code.countdown' | transloco }} <bdi>{{ clock() }}</bdi>
            </p>
          }
        }

        @if (!accepted()) {
          <button
            type="button"
            scsLink
            class="back scs-back"
            data-testid="back-to-sign-in"
            (click)="back()"
          >
            <scs-icon name="back" [size]="16" />
            <span>{{ 'app.code.back' | transloco }}</span>
          </button>
        }
      </form>
    </scs-auth-frame>

    <app-help-window [(open)]="helpOpen" />
  `,
})
export class CodePage implements AfterViewInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly flow = inject(SignInFlow);

  protected readonly email = computed(() => this.auth.pendingSignIn()?.email ?? '');
  protected readonly code = signal('');
  protected readonly busy = signal(false);
  protected readonly accepted = signal(false);
  protected readonly resendFailed = signal(false);
  protected readonly helpOpen = signal(false);
  protected readonly banner = signal<Banner | null>(null);
  protected readonly secondsLeft = signal(0);
  protected readonly resendsLeft = signal(0);

  protected readonly boxState = computed<ScsCodeState>(() =>
    this.accepted() ? 'accepted' : this.banner()?.kind === 'invalid' ? 'error' : 'idle',
  );

  /** Minutes and seconds, always in Western digits. */
  protected readonly clock = computed(() => {
    const seconds = this.secondsLeft();
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  });

  private readonly boxes = viewChild.required<ScsCodeInputComponent>('boxes');
  private deadline = 0;

  constructor() {
    const sent = this.auth.pendingSignIn()?.code;
    this.resendsLeft.set(sent?.resendsLeft ?? 0);
    this.startCountdown(sent?.resendAvailableInSeconds ?? 0);

    const timer = setInterval(() => this.tick(), 250);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  ngAfterViewInit(): void {
    this.boxes().focus();
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy() || this.accepted()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.confirmCode(this.code());
      this.banner.set({ kind: 'accepted' });
      this.accepted.set(true);
      await this.router.navigateByUrl('/');
    } catch (error) {
      await this.refused(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async resend(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      const sent = await this.auth.resendCode();
      this.resendFailed.set(false);
      this.banner.set(null);
      this.code.set('');
      this.resendsLeft.set(sent.resendsLeft);
      this.startCountdown(sent.resendAvailableInSeconds);
      this.boxes().focus();
    } catch (error) {
      await this.refused(error, true);
    } finally {
      this.busy.set(false);
    }
  }

  protected back(): void {
    this.auth.abandonSignIn();
    void this.router.navigateByUrl('/sign-in');
  }

  private async refused(error: unknown, resending = false): Promise<void> {
    const problem =
      error instanceof ScsProblemError && !error.local
        ? (error.problem as typeof error.problem & { resendAvailableInSeconds?: number })
        : null;

    switch (problem?.code) {
      case AuthCodes.accountDisabled:
      case AuthCodes.clinicSuspended:
      case AuthCodes.accountLocked:
      case AuthCodes.networkBlocked:
        // The sign-in is over; the form says why.
        this.flow.carry(error);
        this.auth.abandonSignIn();
        await this.router.navigateByUrl('/sign-in');
        return;
      case AuthCodes.rateLimited:
        this.banner.set({
          kind: 'rateLimited',
          seconds: (error as ScsProblemError).retryAfterSeconds ?? 60,
        });
        return;
      case AuthCodes.resendTooEarly:
        // The countdown here was ahead of the platform's; follow the platform.
        this.startCountdown(problem.resendAvailableInSeconds ?? 0);
        return;
      case AuthCodes.resendLimitReached:
        this.resendsLeft.set(0);
        return;
      case AuthCodes.codeNotResent:
        this.resendFailed.set(true);
        this.banner.set({ kind: 'resendFailed' });
        return;
      default:
        if (resending) {
          this.resendFailed.set(true);
          this.banner.set({ kind: 'resendFailed' });
        } else if (problem === null) {
          // The platform never answered: the code was not judged, so it is not called wrong.
          this.banner.set({ kind: 'unreachable' });
        } else {
          this.banner.set({ kind: 'invalid' });
          this.boxes().focus();
        }
    }
  }

  private startCountdown(seconds: number): void {
    this.deadline = Date.now() + seconds * 1000;
    this.tick();
  }

  private tick(): void {
    this.secondsLeft.set(Math.max(0, Math.ceil((this.deadline - Date.now()) / 1000)));
  }
}
