import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsButtonDirective,
  ScsIconComponent,
  ScsLinkDirective,
  ScsTextFieldComponent,
} from 'ui';
import { HelpWindowComponent } from '../sign-in/help-window.component';
import { PasswordBanner, bannerFor } from './new-password';

const EMAIL = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;

/**
 * "Forgot the password?" (spec 004; BRD B-5-4). One email field. Whatever is typed, a
 * well-formed address ends on the same result screen: the form never says whether an account
 * exists, because the platform never tells it.
 */
@Component({
  selector: 'app-forgot-password-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
    ScsIconComponent,
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
      outline: none;
    }
    .subtitle {
      margin: var(--scs-spacing-sm) 0 0;
      color: var(--scs-color-text-secondary);
    }
    form {
      display: flex;
      flex-direction: column;
      align-items: stretch;
      gap: var(--scs-spacing-lg);
      margin-block-start: var(--scs-spacing-lg);
    }
    .subtitle {
      font-size: var(--scs-text-body-lg);
      line-height: 1.8;
    }
    form .scs-back {
      align-self: flex-start;
    }
    .result {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--scs-spacing-md);
      text-align: center;
    }
    .result h1 {
      font-size: var(--scs-text-title-sm);
    }
    .result .subtitle {
      margin: 0;
    }
    .result .scs-button {
      align-self: stretch;
      margin-block-start: var(--scs-spacing-sm);
    }
    .tile {
      display: grid;
      place-items: center;
      inline-size: 64px;
      block-size: 64px;
      border-radius: var(--scs-radius-full);
      margin-block-end: var(--scs-spacing-sm);
    }
    .tile.success {
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-icon);
    }
    .tile.warning {
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-icon);
    }
    .together {
      white-space: nowrap;
    }
  `,
  template: `
    <scs-auth-frame (help)="helpOpen.set(true)">
      @if (sentTo(); as sent) {
        <div class="result">
          <span class="tile success"><scs-icon name="check-circle" [size]="26" /></span>
          <h1 #sentTitle tabindex="-1" data-testid="forgot-sent-title">
            {{ 'app.password.forgot.sentTitle' | transloco }}
          </h1>
          <p class="subtitle" data-testid="forgot-sent-text">
            {{ 'app.password.forgot.sentBefore' | transloco }}
            <bdi data-testid="forgot-sent-email">{{ sent }}</bdi>
            {{ 'app.password.forgot.sentAfter' | transloco }}
          </p>
          <a scsLink class="scs-back" data-testid="back-to-sign-in" routerLink="/sign-in">
            <scs-icon name="back" [size]="16" />
            <span>{{ 'app.password.back' | transloco }}</span>
          </a>
        </div>
      } @else {
        <h1 data-testid="forgot-title">{{ 'app.password.forgot.title' | transloco }}</h1>
        <p class="subtitle">{{ 'app.password.forgot.subtitle' | transloco }}</p>

        <form novalidate (submit)="submit($event)">
          @if (banner(); as banner) {
            <scs-banner
              [tone]="banner.kind === 'rateLimited' ? 'warning' : 'danger'"
              [attr.data-testid]="'banner-' + banner.kind"
            >
              @if (banner.kind === 'rateLimited') {
                {{ 'app.signIn.banner.rateLimitedBefore' | transloco }}
                <bdi class="together" data-testid="retry-after">{{ banner.seconds }}</bdi>
                {{ 'app.signIn.banner.rateLimitedAfter' | transloco }}
              } @else {
                {{ 'app.signIn.banner.' + banner.kind | transloco }}
              }
            </scs-banner>
          }

          <scs-text-field
            #emailField
            kind="email"
            testId="email"
            autocomplete="username"
            [placeholder]="emailPlaceholder"
            [label]="'app.signIn.email.label' | transloco"
            [error]="emailError() ? ('app.signIn.email.' + emailError() | transloco) : ''"
            [(value)]="email"
          />

          <button type="submit" scsButton data-testid="forgot-submit" [busy]="busy()">
            {{
              (busy() ? 'app.password.forgot.submitting' : 'app.password.forgot.submit') | transloco
            }}
          </button>

          <a scsLink class="scs-back" data-testid="back-to-sign-in" routerLink="/sign-in">
            <scs-icon name="back" [size]="16" />
            <span>{{ 'app.password.back' | transloco }}</span>
          </a>
        </form>
      }
    </scs-auth-frame>

    <app-help-window [(open)]="helpOpen" />
  `,
})
export class ForgotPasswordPage implements AfterViewInit {
  private readonly auth = inject(AuthService);

  protected readonly emailPlaceholder = 'clinic@example.com';

  protected readonly email = signal('');
  protected readonly emailError = signal<'required' | 'invalid' | null>(null);
  protected readonly busy = signal(false);
  protected readonly helpOpen = signal(false);
  protected readonly banner = signal<PasswordBanner | null>(null);

  /** The address the request was made for, once it was made. */
  protected readonly sentTo = signal('');

  private readonly emailField = viewChild<ScsTextFieldComponent>('emailField');
  private readonly sentTitle = viewChild<ElementRef<HTMLElement>>('sentTitle');

  constructor() {
    // When the result replaces the form, focus goes to its title so it is read out.
    effect(() => this.sentTitle()?.nativeElement.focus());
  }

  ngAfterViewInit(): void {
    this.emailField()?.focus();
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    const email = this.email().trim();
    this.banner.set(null);
    this.emailError.set(email === '' ? 'required' : EMAIL.test(email) ? null : 'invalid');
    if (this.emailError() !== null) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.requestPasswordReset(email);
      this.sentTo.set(email);
    } catch (error) {
      this.banner.set(bannerFor(error));
    } finally {
      this.busy.set(false);
    }
  }
}
