import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthCodes, AuthService, LanguageService, ScsProblemError } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsBannerTone,
  ScsButtonDirective,
  ScsLinkDirective,
  ScsTextFieldComponent,
} from 'ui';
import { HelpWindowComponent } from './help-window.component';
import { SignInFlow } from './sign-in-flow';

/** What the banner above the form says. One at most; never together with a field error. */
type Banner =
  | {
      kind:
        'ended' | 'invalid' | 'unreachable' | 'codeNotSent' | 'disabled' | 'blocked' | 'unexpected';
    }
  | { kind: 'suspended' }
  | { kind: 'locked'; until: string }
  | { kind: 'rateLimited'; seconds: number };

const TONES: Record<Banner['kind'], ScsBannerTone> = {
  ended: 'neutral',
  invalid: 'danger',
  unreachable: 'danger',
  codeNotSent: 'danger',
  disabled: 'danger',
  suspended: 'danger',
  unexpected: 'danger',
  locked: 'warning',
  blocked: 'warning',
  rateLimited: 'warning',
};

const EMAIL = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;

/**
 * Step one of sign-in: email and password (spec 003; BRD B-5-1). The form checks only that the
 * fields are filled and the email is shaped like one; everything else is the server's answer,
 * shown as one banner between the title and the fields with both fields left as typed.
 */
@Component({
  selector: 'app-sign-in-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsLinkDirective,
    ScsTextFieldComponent,
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
      gap: var(--scs-spacing-md);
      margin-block-start: var(--scs-spacing-lg);
    }
    .hint {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .together {
      white-space: nowrap;
    }
  `,
  template: `
    <scs-auth-frame (help)="helpOpen.set(true)">
      <h1 data-testid="sign-in-title">{{ 'app.signIn.title' | transloco }}</h1>
      <p class="subtitle">{{ 'app.signIn.subtitle' | transloco }}</p>

      <form novalidate (submit)="submit($event)">
        @if (banner(); as banner) {
          <scs-banner
            [tone]="tone()"
            [attr.data-testid]="'banner-' + banner.kind"
            [actionLabel]="
              banner.kind === 'suspended' ? ('app.signIn.banner.suspendedAction' | transloco) : ''
            "
            (action)="helpOpen.set(true)"
          >
            @switch (banner.kind) {
              @case ('locked') {
                {{ 'app.signIn.banner.lockedBefore' | transloco }}
                <bdi class="together" data-testid="locked-until">{{ lockedUntil() }}</bdi
                >{{ 'app.signIn.banner.lockedAfter' | transloco }}
              }
              @case ('rateLimited') {
                {{ 'app.signIn.banner.rateLimitedBefore' | transloco }}
                <bdi class="together" data-testid="retry-after">{{ banner.seconds }}</bdi>
                {{ 'app.signIn.banner.rateLimitedAfter' | transloco }}
              }
              @default {
                {{ 'app.signIn.banner.' + banner.kind | transloco }}
              }
            }
          </scs-banner>
        }

        <scs-text-field
          #emailField
          kind="email"
          testId="email"
          autocomplete="username"
          [label]="'app.signIn.email.label' | transloco"
          [placeholder]="emailPlaceholder"
          [error]="emailError() ? ('app.signIn.email.' + emailError() | transloco) : ''"
          [(value)]="email"
        />

        <scs-text-field
          kind="password"
          testId="password"
          autocomplete="current-password"
          [label]="'app.signIn.password.label' | transloco"
          [error]="passwordError() ? ('app.signIn.password.required' | transloco) : ''"
          [(value)]="password"
        />

        <button type="submit" scsButton data-testid="sign-in-submit" [busy]="busy()">
          {{ (busy() ? 'app.signIn.submitting' : 'app.signIn.submit') | transloco }}
        </button>

        <a scsLink data-testid="forgot-password" routerLink="/forgot-password">{{
          'app.signIn.forgot' | transloco
        }}</a>
        <p class="hint" data-testid="new-account-hint">{{ 'app.signIn.newAccount' | transloco }}</p>
      </form>
    </scs-auth-frame>

    <app-help-window [(open)]="helpOpen" />
  `,
})
export class SignInPage implements AfterViewInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly flow = inject(SignInFlow);

  protected readonly emailPlaceholder = 'clinic@example.com';

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly emailError = signal<'required' | 'invalid' | null>(null);
  protected readonly passwordError = signal(false);
  protected readonly busy = signal(false);
  protected readonly helpOpen = signal(false);
  protected readonly banner = signal<Banner | null>(null);

  protected readonly tone = computed(() => TONES[this.banner()?.kind ?? 'ended']);

  /** The time the lock ends, in the person's own time zone, kept on one line with its AM/PM. */
  protected readonly lockedUntil = computed(() => {
    const banner = this.banner();
    if (banner?.kind !== 'locked') {
      return '';
    }
    return new Intl.DateTimeFormat(this.language.language() === 'ar' ? 'ar-EG' : 'en-GB', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    })
      .format(new Date(banner.until))
      .replace(/\s/g, ' ');
  });

  private readonly emailField = viewChild.required<ScsTextFieldComponent>('emailField');

  constructor() {
    const route = inject(ActivatedRoute);

    // A refusal met at the code step (the account was disabled, the clinic suspended) is shown here.
    const carried = this.flow.takeRefusal();
    if (carried !== null) {
      this.show(carried);
    } else if (route.snapshot.queryParamMap.has('ended')) {
      this.banner.set({ kind: 'ended' });
      // The address says nothing about the person once it has been read.
      void this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
  }

  ngAfterViewInit(): void {
    this.emailField().focus();
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    const email = this.email().trim();
    this.banner.set(null);
    this.emailError.set(email === '' ? 'required' : EMAIL.test(email) ? null : 'invalid');
    this.passwordError.set(this.password() === '');
    if (this.emailError() !== null || this.passwordError()) {
      return;
    }

    this.busy.set(true);
    try {
      const next = await this.auth.signIn(email, this.password());
      await this.router.navigateByUrl(next === 'code' ? '/sign-in/code' : '/');
    } catch (error) {
      this.show(error);
    } finally {
      this.busy.set(false);
    }
  }

  private show(error: unknown): void {
    if (!(error instanceof ScsProblemError)) {
      this.banner.set({ kind: 'unexpected' });
      return;
    }

    if (error.local) {
      this.banner.set({ kind: 'unreachable' });
      return;
    }

    const problem = error.problem as typeof error.problem & { lockedUntil?: string };
    switch (problem.code) {
      case AuthCodes.invalidCredentials:
        this.banner.set({ kind: 'invalid' });
        break;
      case AuthCodes.codeNotSent:
        this.banner.set({ kind: 'codeNotSent' });
        break;
      case AuthCodes.accountLocked:
        this.banner.set({ kind: 'locked', until: problem.lockedUntil ?? new Date().toISOString() });
        break;
      case AuthCodes.accountDisabled:
        this.banner.set({ kind: 'disabled' });
        break;
      case AuthCodes.clinicSuspended:
        this.banner.set({ kind: 'suspended' });
        break;
      case AuthCodes.networkBlocked:
        this.banner.set({ kind: 'blocked' });
        break;
      case AuthCodes.rateLimited:
        this.banner.set({ kind: 'rateLimited', seconds: error.retryAfterSeconds ?? 60 });
        break;
      default:
        this.banner.set({ kind: 'unexpected' });
    }
  }
}
