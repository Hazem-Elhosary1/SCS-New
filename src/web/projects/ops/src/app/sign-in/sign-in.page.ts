import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsBannerTone,
  ScsButtonDirective,
  ScsLinkDirective,
  ScsTextFieldComponent,
} from 'ui';
import { OpsAuthService, OpsCodes, OpsNotice, problemField } from '../auth/ops-auth.service';

/** What the banner between the line and the fields says. One at most. */
type Banner =
  | { kind: 'invalid' | 'suspended' | 'unreachable' | 'codeNotSent' | 'unexpected' }
  | { kind: 'locked'; until: string }
  | { kind: 'rateLimited' }
  | { kind: 'notice'; notice: OpsNotice };

const TONES: Record<Banner['kind'], ScsBannerTone> = {
  invalid: 'danger',
  suspended: 'danger',
  unreachable: 'danger',
  codeNotSent: 'danger',
  unexpected: 'danger',
  locked: 'warning',
  rateLimited: 'warning',
  notice: 'neutral',
};

const GOOD_NEWS: readonly OpsNotice[] = ['password-changed', 'account-ready'];

export const OPS_EMAIL = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;

/**
 * Step one of an operator's sign-in (spec 010; design frames `128:85`, `128:54`, `134:54`,
 * `134:15`): email, password, «تذكرني» and the way to a forgotten password. The form checks
 * only that the fields are filled and the email is shaped like one; everything else is the
 * platform's answer, shown as one banner with both fields left as typed.
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
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
    }
    .remember {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      cursor: pointer;
    }
    .remember input {
      inline-size: 16px;
      block-size: 16px;
      accent-color: var(--scs-color-brand-default);
    }
    .together {
      white-space: nowrap;
    }
    .news {
      display: block;
      font-weight: 700;
    }
  `,
  template: `
    <scs-auth-frame
      [panelTitle]="'app.signIn.panel.title' | transloco"
      [panelLine]="'app.signIn.panel.line' | transloco"
    >
      <h1 data-testid="sign-in-title">{{ 'app.signIn.title' | transloco }}</h1>
      <p class="subtitle">{{ 'app.signIn.subtitle' | transloco }}</p>

      <form novalidate (submit)="submit($event)">
        @if (banner(); as banner) {
          <scs-banner
            [tone]="tone()"
            [attr.data-testid]="'banner-' + banner.kind"
            [closeLabel]="'ui.banner.close' | transloco"
            (closed)="this.banner.set(null)"
          >
            @switch (banner.kind) {
              @case ('invalid') {
                <strong class="news">{{ 'app.signIn.banner.invalidTitle' | transloco }}</strong>
                {{ 'app.signIn.banner.invalid' | transloco }}
              }
              @case ('locked') {
                {{ 'app.signIn.banner.lockedBefore' | transloco }}
                <bdi class="together" data-testid="locked-until">{{ lockedUntil() }}</bdi
                >{{ 'app.signIn.banner.lockedAfter' | transloco }}
              }
              @case ('notice') {
                <span data-testid="sign-in-notice" [attr.data-notice]="banner.notice">{{
                  'app.signIn.notice.' + banner.notice | transloco
                }}</span>
              }
              @default {
                {{ 'app.signIn.banner.' + banner.kind | transloco }}
              }
            }
          </scs-banner>
        }

        <scs-text-field
          [lead]="false"
          kind="email"
          testId="email"
          autocomplete="username"
          [placeholder]="emailPlaceholder"
          [label]="'app.signIn.email.label' | transloco"
          [error]="emailError() ? ('app.signIn.email.' + emailError() | transloco) : ''"
          [(value)]="email"
        />

        <scs-text-field
          [lead]="false"
          kind="password"
          testId="password"
          autocomplete="current-password"
          [label]="'app.signIn.password.label' | transloco"
          [error]="passwordError() ? ('app.signIn.password.required' | transloco) : ''"
          [(value)]="password"
        />

        <div class="row">
          <label class="remember">
            <input
              type="checkbox"
              data-testid="remember"
              [checked]="remember()"
              (change)="remember.set($any($event.target).checked)"
            />
            <span>{{ 'app.signIn.remember' | transloco }}</span>
          </label>
          <a scsLink data-testid="forgot-password" routerLink="/forgot-password">{{
            'app.signIn.forgot' | transloco
          }}</a>
        </div>

        <button type="submit" scsButton data-testid="sign-in-submit" [busy]="busy()">
          {{ (busy() ? 'app.signIn.submitting' : 'app.signIn.submit') | transloco }}
        </button>
      </form>
    </scs-auth-frame>
  `,
})
export class SignInPage {
  private readonly auth = inject(OpsAuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(LanguageService);

  /** An address, not words: the same in both languages. */
  protected readonly emailPlaceholder = 'you@company.com';

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly remember = signal(false);
  protected readonly emailError = signal<'required' | 'invalid' | null>(null);
  protected readonly passwordError = signal(false);
  protected readonly busy = signal(false);
  protected readonly banner = signal<Banner | null>(null);

  protected readonly tone = computed<ScsBannerTone>(() => {
    const banner = this.banner();
    if (banner?.kind === 'notice') {
      return GOOD_NEWS.includes(banner.notice)
        ? 'success'
        : banner.notice === 'locked'
          ? 'warning'
          : 'neutral';
    }
    return TONES[banner?.kind ?? 'invalid'];
  });

  /** The time the lock ends, in the reader's digits, kept on one line with its AM/PM. */
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

  constructor() {
    // Why the last session ended, or what was just done: said once.
    const notice = this.auth.takeNotice();
    if (notice !== null) {
      this.banner.set({ kind: 'notice', notice });
    }
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    const email = this.email().trim();
    this.emailError.set(email === '' ? 'required' : OPS_EMAIL.test(email) ? null : 'invalid');
    this.passwordError.set(this.password() === '');
    if (this.emailError() !== null || this.passwordError()) {
      this.banner.set(null);
      return;
    }

    this.busy.set(true);
    this.banner.set(null);
    try {
      await this.auth.signIn(email, this.password(), this.remember());
      await this.router.navigate(['/sign-in/code'], {
        queryParamsHandling: 'preserve',
        queryParams: this.route.snapshot.queryParams,
      });
    } catch (error) {
      this.banner.set(this.toBanner(error));
    } finally {
      this.busy.set(false);
    }
  }

  private toBanner(error: unknown): Banner {
    if (!(error instanceof ScsProblemError)) {
      return { kind: 'unexpected' };
    }
    if (error.local) {
      return { kind: 'unreachable' };
    }
    switch (error.problem.code) {
      case OpsCodes.invalidCredentials:
        return { kind: 'invalid' };
      case OpsCodes.accountDisabled:
        return { kind: 'suspended' };
      case OpsCodes.accountLocked:
        return { kind: 'locked', until: problemField<string>(error, 'lockedUntil') ?? '' };
      case OpsCodes.codeNotSent:
        return { kind: 'codeNotSent' };
      case OpsCodes.rateLimited:
        return { kind: 'rateLimited' };
      default:
        return { kind: 'unexpected' };
    }
  }
}
