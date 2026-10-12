import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsButtonDirective,
  ScsIconComponent,
  ScsLinkDirective,
  ScsPasswordRule,
  ScsPasswordRulesComponent,
  ScsTextFieldComponent,
} from 'ui';
import { firstValueFrom } from 'rxjs';
import { OpsAuthService, OpsCodes, problemField } from '../auth/ops-auth.service';
import { OPS_EMAIL } from '../sign-in/sign-in.page';

/** The four rules of a password, each met or not by what is typed (FR-029). */
export function passwordRules(password: string, words: (key: string) => string): ScsPasswordRule[] {
  return [
    { key: 'length', label: words('app.password.rules.length'), met: password.length >= 10 },
    {
      key: 'cases',
      label: words('app.password.rules.cases'),
      met: /\p{Lu}/u.test(password) && /\p{Ll}/u.test(password),
    },
    { key: 'digit', label: words('app.password.rules.digit'), met: /[0-9]/.test(password) },
    {
      key: 'symbol',
      label: words('app.password.rules.symbol'),
      met: /[\p{P}\p{S}]/u.test(password),
    },
  ];
}

const HEADING = `
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
  .back {
    display: inline-flex;
    align-items: center;
    gap: var(--scs-spacing-xs);
    align-self: center;
  }
  .sent {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--scs-spacing-sm);
    text-align: center;
  }
  .sent .ops-tick {
    margin-block-end: var(--scs-spacing-sm);
  }
  .sent .subtitle,
  .sent .quiet {
    margin: 0;
  }
  .sent .resent {
    display: inline-flex;
    align-items: center;
    gap: var(--scs-spacing-xs);
    color: var(--scs-color-feedback-success-text);
  }
  .sent .problem {
    margin: 0;
    font-size: var(--scs-text-body-sm);
    color: var(--scs-color-feedback-danger-text);
  }
  .sent .quiet {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-block: var(--scs-spacing-md);
    font-size: var(--scs-text-body-sm);
  }
/* A link that no longer works: its two ways out begin together at the start. */
  .gone .back {
    align-self: flex-start;
  }
  .quiet {
    margin: var(--scs-spacing-lg) 0;
    color: var(--scs-color-text-secondary);
  }
`;

/** A link is sent for an account at most once a minute (FR-026). */
export const FORGOT_WAIT_SECONDS = 60;

/**
 * Asking for a reset link (spec 010, FR-025; design frames `141:53`, `158:52` and their
 * twins). The screen that follows is the same whoever the address belongs to. "Send again"
 * always says what it is doing: how long until it can be pressed, that it is sending, that a
 * new link went, or that it could not be sent.
 */

@Component({
  selector: 'app-forgot-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsIconComponent,
    ScsLinkDirective,
    ScsTextFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: HEADING,
  template: `
    <scs-auth-frame
      [panelTitle]="'app.signIn.panel.title' | transloco"
      [panelLine]="'app.signIn.panel.line' | transloco"
    >
      @if (sentTo(); as address) {
        <div class="sent" data-testid="forgot-sent">
          <span class="ops-tick" aria-hidden="true"><scs-icon name="check" [size]="32" /></span>
          <h1 data-testid="forgot-sent-title">{{ 'app.forgot.sent.title' | transloco }}</h1>
          <p class="subtitle">
            {{ 'app.forgot.sent.line' | transloco }} <bdi data-testid="forgot-sent-to">{{ address }}</bdi>
          </p>
          <!-- What "send again" is doing is always said: sending, sent, how long until the next,
               or that it could not be sent. -->
          <div class="quiet" role="status" aria-live="polite">
            @if (resent()) {
              <span class="resent" data-testid="forgot-resent">
                <scs-icon name="check" [size]="14" />
                {{ 'app.forgot.sent.resent' | transloco }}
              </span>
            } @else {
              <span>{{ 'app.forgot.sent.notArrived' | transloco }}</span>
            }
            @if (busy()) {
              <span data-testid="forgot-sending">{{ 'app.forgot.sent.sending' | transloco }}</span>
            } @else if (seconds() > 0) {
              <span data-testid="forgot-wait">
                {{ 'app.forgot.sent.waitBefore' | transloco }}
                <bdi data-testid="forgot-countdown">{{ countdown() }}</bdi>
              </span>
            } @else {
              <button type="button" scsLink data-testid="forgot-again" (click)="send()">
                {{ 'app.forgot.sent.again' | transloco }}
              </button>
            }
          </div>
          @if (failed(); as kind) {
            <p class="problem" role="alert" data-testid="forgot-not-resent">
              {{ 'app.signIn.banner.' + kind | transloco }}
            </p>
          }
          <a scsLink class="back" data-testid="forgot-back" routerLink="/sign-in">
            <scs-icon name="back" [size]="16" />
            <span>{{ 'app.forgot.back' | transloco }}</span>
          </a>
        </div>
      } @else {
        <h1 data-testid="forgot-title">{{ 'app.forgot.title' | transloco }}</h1>
        <p class="subtitle">{{ 'app.forgot.subtitle' | transloco }}</p>

        <form novalidate (submit)="submit($event)">
          @if (failed(); as kind) {
            <scs-banner tone="danger" [attr.data-testid]="'banner-' + kind">
              {{ 'app.signIn.banner.' + kind | transloco }}
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
          <button type="submit" scsButton data-testid="forgot-submit" [busy]="busy()">
            {{ 'app.forgot.submit' | transloco }}
          </button>
          <a scsLink class="back" data-testid="forgot-back" routerLink="/sign-in">
            <scs-icon name="back" [size]="16" />
            <span>{{ 'app.forgot.back' | transloco }}</span>
          </a>
        </form>
      }
    </scs-auth-frame>
  `,
})
export class ForgotPage {
  private readonly http = inject(HttpClient);

  protected readonly emailPlaceholder = 'you@company.com';
  protected readonly email = signal('');
  protected readonly emailError = signal<'required' | 'invalid' | null>(null);
  protected readonly busy = signal(false);
  protected readonly sentTo = signal<string | null>(null);
  protected readonly failed = signal<'unreachable' | 'rateLimited' | 'unexpected' | null>(null);

  /** "Send again" rests for a minute after each sending (FR-026): the seconds left of it. */
  protected readonly seconds = signal(0);

  /** A link was sent again from the "check your email" screen. */
  protected readonly resent = signal(false);

  /** Minutes and seconds, in the reader's digits. */
  protected readonly countdown = computed(() => {
    const digits = new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG' : 'en-GB', {
      minimumIntegerDigits: 2,
      useGrouping: false,
    });
    return `${digits.format(Math.floor(this.seconds() / 60))}:${digits.format(this.seconds() % 60)}`;
  });

  private readonly language = inject(LanguageService);
  private waitEndsAt = 0;
  private readonly ticking = setInterval(
    () => this.seconds.set(Math.max(0, Math.ceil((this.waitEndsAt - Date.now()) / 1000))),
    250,
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => clearInterval(this.ticking));
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    const email = this.email().trim();
    this.emailError.set(email === '' ? 'required' : OPS_EMAIL.test(email) ? null : 'invalid');
    if (this.emailError() === null) {
      await this.send();
    }
  }

  protected async send(): Promise<void> {
    if (this.busy()) {
      return;
    }
    const email = this.email().trim();
    this.busy.set(true);
    this.failed.set(null);
    try {
      const again = this.sentTo() !== null;
      await firstValueFrom(this.http.post('/v1/ops/password/forgot', { email }));
      this.sentTo.set(email);
      this.resent.set(again);
      this.waitEndsAt = Date.now() + FORGOT_WAIT_SECONDS * 1000;
      this.seconds.set(FORGOT_WAIT_SECONDS);
    } catch (error) {
      // What was said of an earlier sending is not left beside this failure.
      this.resent.set(false);
      this.failed.set(
        error instanceof ScsProblemError && error.local
          ? 'unreachable'
          : error instanceof ScsProblemError && error.problem.code === OpsCodes.rateLimited
            ? 'rateLimited'
            : 'unexpected',
      );
    } finally {
      this.busy.set(false);
    }
  }
}

interface LinkView {
  readonly kind: 'reset' | 'first';
  readonly email: string;
}

/**
 * Choosing a password from a link: the first, from an invitation, or a new one after
 * forgetting (spec 010, FR-004a, FR-027; design frames `167:66`, `167:38`). The link is read
 * from the address's fragment, which a browser never sends to a server or keeps in a referrer.
 */
@Component({
  selector: 'app-set-password-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsIconComponent,
    ScsLinkDirective,
    ScsPasswordRulesComponent,
    ScsTextFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles:
    HEADING +
    `
    .email {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-xs);
    }
    .email span {
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .email .locked {
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      text-align: start;
      overflow-wrap: anywhere;
    }
  `,
  template: `
    <scs-auth-frame
      [panelTitle]="'app.signIn.panel.title' | transloco"
      [panelLine]="'app.signIn.panel.line' | transloco"
    >
      @switch (state()) {
        @case ('reading') {
          <p class="quiet" role="status" data-testid="link-reading">{{ 'app.password.reading' | transloco }}</p>
        }
        @case ('gone') {
          <h1 data-testid="link-gone-title">{{ 'app.password.gone.title' | transloco }}</h1>
          <p class="subtitle" data-testid="link-gone-line">
            {{ 'app.password.gone.' + (goneKind() ?? 'unknown') | transloco }}
          </p>
          <form class="gone">
            @if (goneKind() === 'reset') {
              <a scsLink data-testid="link-new" routerLink="/forgot-password">{{
                'app.password.gone.askNew' | transloco
              }}</a>
            }
            <a scsLink class="back" data-testid="forgot-back" routerLink="/sign-in">
              <scs-icon name="back" [size]="16" />
              <span>{{ 'app.forgot.back' | transloco }}</span>
            </a>
          </form>
        }
        @default {
          <h1 data-testid="set-password-title">{{ 'app.password.' + kind() + '.title' | transloco }}</h1>
          <p class="subtitle">{{ 'app.password.' + kind() + '.subtitle' | transloco }}</p>

          <form novalidate (submit)="submit($event)">
            @if (failed(); as kind) {
              <scs-banner tone="danger" [attr.data-testid]="'banner-' + kind">
                {{ 'app.signIn.banner.' + kind | transloco }}
              </scs-banner>
            }
            <div class="email">
              <span>{{ 'app.signIn.email.label' | transloco }}</span>
              <div class="locked"><bdi data-testid="link-email">{{ link()?.email }}</bdi></div>
            </div>
            <scs-text-field
          [lead]="false"
              kind="password"
              testId="new-password"
              autocomplete="new-password"
              [label]="'app.password.new' | transloco"
              [error]="same() ? ('app.password.same' | transloco) : ''"
              [(value)]="password"
            />
            <scs-text-field
              [lead]="false"
              kind="password"
              testId="confirm-password"
              autocomplete="new-password"
              [label]="'app.password.confirm' | transloco"
              [error]="mismatch() ? ('app.password.mismatch' | transloco) : ''"
              [(value)]="confirmation"
            />
            <scs-password-rules [rules]="rules()" />
            <button type="submit" scsButton data-testid="set-password-submit" [busy]="busy()">
              {{ 'app.password.' + kind() + '.submit' | transloco }}
            </button>
          </form>
        }
      }
    </scs-auth-frame>
  `,
})
export class SetPasswordPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly auth = inject(OpsAuthService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  private readonly token = new URLSearchParams(globalThis.location?.hash.replace(/^#/, '') ?? '').get('token') ?? '';

  protected readonly state = signal<'reading' | 'form' | 'gone'>('reading');
  protected readonly link = signal<LinkView | null>(null);
  protected readonly goneKind = signal<'reset' | 'first' | null>(null);
  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly mismatch = signal(false);
  protected readonly same = signal(false);
  protected readonly busy = signal(false);
  protected readonly failed = signal<'unreachable' | 'rateLimited' | 'unexpected' | null>(null);

  protected readonly kind = computed(() => this.link()?.kind ?? 'reset');

  protected readonly rules = computed(() => {
    // Read so the lines follow the language when it is switched.
    this.language.language();
    return passwordRules(this.password(), (key) => this.words.translate(key));
  });

  constructor() {
    void this.read();
  }

  private async read(): Promise<void> {
    try {
      this.link.set(
        await firstValueFrom(this.http.post<LinkView>('/v1/ops/password/link', { token: this.token })),
      );
      this.state.set('form');
    } catch (error) {
      this.gone(error);
    }
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    this.same.set(false);
    this.mismatch.set(this.password() !== this.confirmation());
    if (this.mismatch() || this.rules().some((rule) => !rule.met)) {
      return;
    }

    this.busy.set(true);
    this.failed.set(null);
    try {
      await firstValueFrom(
        this.http.post('/v1/ops/password/set', { token: this.token, password: this.password() }),
      );
      this.auth.say(this.kind() === 'first' ? 'account-ready' : 'password-changed');
      await this.router.navigateByUrl('/sign-in');
    } catch (error) {
      if (error instanceof ScsProblemError && error.problem.code === OpsCodes.linkInvalid) {
        this.gone(error);
      } else if (
        error instanceof ScsProblemError &&
        error.problem.errors?.['password']?.includes('SAME_AS_CURRENT')
      ) {
        this.same.set(true);
      } else {
        this.failed.set(
          error instanceof ScsProblemError && error.local
            ? 'unreachable'
            : error instanceof ScsProblemError && error.problem.code === OpsCodes.rateLimited
              ? 'rateLimited'
              : 'unexpected',
        );
      }
    } finally {
      this.busy.set(false);
    }
  }

  private gone(error: unknown): void {
    this.goneKind.set(problemField<'reset' | 'first'>(error, 'kind'));
    this.state.set('gone');
  }
}
