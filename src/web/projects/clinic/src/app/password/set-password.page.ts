import { Location } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService, LanguageService, PasswordLinkKind, PasswordLinkView } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsButtonDirective,
  ScsIconComponent,
  ScsLinkDirective,
} from 'ui';
import { HelpWindowComponent } from '../sign-in/help-window.component';
import { SignInFlow } from '../sign-in/sign-in-flow';
import { NewPasswordComponent, NewPasswordForm, PasswordBanner, bannerFor } from './new-password';

/** What the page shows: the link is being looked at, it is live, or it no longer works. */
type Stage =
  | { is: 'loading' }
  | { is: 'failed'; banner: PasswordBanner }
  | { is: 'form'; link: PasswordLinkView }
  | { is: 'dead'; kind: PasswordLinkKind };

const TOKEN = /^t=([A-Za-z0-9_-]+)$/;

/**
 * Where an emailed link leads (spec 004; BRD B-5-5, B-5-6, B-5-7): choosing a new password
 * after forgetting one, choosing the first password of a new account, or learning that the
 * link no longer works and what to do about it.
 *
 * The link's secret arrives after the `#` of the address, which no server ever sees. It is
 * read once, removed from the address at once, and kept in memory only — so a reload, a
 * bookmark or the browser's history holds nothing that opens anything.
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
    HelpWindowComponent,
    NewPasswordComponent,
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
    form,
    .actions {
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
    form .scs-back,
    .actions .scs-back {
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
    .hint {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .together {
      white-space: nowrap;
    }
    .loading {
      text-align: center;
    }
  `,
  template: `
    <scs-auth-frame (help)="helpOpen.set(true)">
      @switch (stage().is) {
        @case ('loading') {
          <p class="subtitle loading" role="status" data-testid="link-loading">
            {{ 'app.password.loading' | transloco }}
          </p>
        }

        @case ('failed') {
          <!-- The platform could not be asked about the link. The link itself is untouched. -->
          @if (failure(); as failure) {
            <scs-banner
              [tone]="failure.kind === 'rateLimited' ? 'warning' : 'danger'"
              [attr.data-testid]="'banner-' + failure.kind"
            >
              @if (failure.kind === 'rateLimited') {
                {{ 'app.signIn.banner.rateLimitedBefore' | transloco }}
                <bdi class="together" data-testid="retry-after">{{ failure.seconds }}</bdi>
                {{ 'app.signIn.banner.rateLimitedAfter' | transloco }}
              } @else {
                {{ 'app.signIn.banner.' + failure.kind | transloco }}
              }
            </scs-banner>
          }
          <div class="actions">
            <button type="button" scsButton data-testid="link-retry" (click)="retry()">
              {{ 'app.password.retry' | transloco }}
            </button>
            <a scsLink class="scs-back" data-testid="back-to-sign-in" routerLink="/sign-in">
              <scs-icon name="back" [size]="16" />
              <span>{{ 'app.password.back' | transloco }}</span>
            </a>
          </div>
        }

        @case ('form') {
          @if (link(); as link) {
            @if (link.kind === 'new-account') {
              <h1 data-testid="new-account-title">
                {{ 'app.password.newAccount.welcome' | transloco }}
                <bdi data-testid="clinic-name">{{ clinicName() }}</bdi>
              </h1>
              <p class="subtitle">
                {{ 'app.password.newAccount.choose' | transloco }}
                <bdi dir="ltr" data-testid="link-email">{{ link.email }}</bdi>
              </p>
            } @else {
              <h1 data-testid="reset-title">{{ 'app.password.reset.title' | transloco }}</h1>
              <p class="subtitle">
                {{ 'app.password.forAccount' | transloco }}
                <bdi dir="ltr" data-testid="link-email">{{ link.email }}</bdi>
              </p>
            }

            <form novalidate (submit)="submit($event, link.kind)">
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

              <app-new-password #fields [form]="form" />

              @if (link.kind === 'new-account') {
                <p class="hint" data-testid="link-life">
                  {{ 'app.password.newAccount.hint' | transloco }}
                </p>
              }

              <button type="submit" scsButton data-testid="set-password-submit" [busy]="busy()">
                {{
                  (link.kind === 'new-account'
                    ? busy()
                      ? 'app.password.newAccount.submitting'
                      : 'app.password.newAccount.submit'
                    : busy()
                      ? 'app.password.reset.submitting'
                      : 'app.password.reset.submit'
                  ) | transloco
                }}
              </button>

              <a scsLink class="scs-back" data-testid="back-to-sign-in" routerLink="/sign-in">
                <scs-icon name="back" [size]="16" />
                <span>{{ 'app.password.back' | transloco }}</span>
              </a>
            </form>
          }
        }

        @case ('dead') {
          <div class="result">
            <span class="tile warning"><scs-icon name="alert-triangle" [size]="26" /></span>
            <h1 #deadTitle tabindex="-1" data-testid="dead-link-title">
              {{ 'app.password.dead.title' | transloco }}
            </h1>
            <p class="subtitle" data-testid="dead-link-text">
              {{
                (deadKind() === 'new-account'
                  ? 'app.password.dead.newAccount'
                  : 'app.password.dead.reset'
                ) | transloco
              }}
            </p>
            @if (deadKind() === 'reset') {
              <!-- Only a reset link can be asked for again by its owner (BRD B-R21). -->
              <a class="scs-button" data-testid="ask-again" routerLink="/forgot-password">{{
                'app.password.dead.askAgain' | transloco
              }}</a>
            }
            <a scsLink class="scs-back" data-testid="back-to-sign-in" routerLink="/sign-in">
              <scs-icon name="back" [size]="16" />
              <span>{{ 'app.password.back' | transloco }}</span>
            </a>
          </div>
        }
      }
    </scs-auth-frame>

    <app-help-window [(open)]="helpOpen" />
  `,
})
export class SetPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly flow = inject(SignInFlow);

  protected readonly form = new NewPasswordForm();
  protected readonly stage = signal<Stage>({ is: 'loading' });
  protected readonly busy = signal(false);
  protected readonly helpOpen = signal(false);
  protected readonly banner = signal<PasswordBanner | null>(null);

  protected readonly link = computed(() => {
    const stage = this.stage();
    return stage.is === 'form' ? stage.link : null;
  });

  protected readonly failure = computed(() => {
    const stage = this.stage();
    return stage.is === 'failed' ? stage.banner : null;
  });

  protected readonly deadKind = computed(() => {
    const stage = this.stage();
    return stage.is === 'dead' ? stage.kind : 'reset';
  });

  /** Shown on the new-account form only: its holder is already identified by the link. */
  protected readonly clinicName = computed(() => {
    const link = this.link();
    return (this.language.language() === 'ar' ? link?.clinicNameAr : link?.clinicNameEn) ?? '';
  });

  /** In memory only. */
  private readonly token: string | null;

  private readonly fields = viewChild<NewPasswordComponent>('fields');
  private readonly deadTitle = viewChild<ElementRef<HTMLElement>>('deadTitle');

  constructor() {
    this.token = TOKEN.exec(inject(ActivatedRoute).snapshot.fragment ?? '')?.[1] ?? null;

    // The address says nothing once it has been read.
    inject(Location).replaceState('/set-password');

    effect(() => this.fields()?.focus());
    effect(() => this.deadTitle()?.nativeElement.focus());

    void this.open();
  }

  private async open(): Promise<void> {
    if (this.token === null) {
      this.stage.set({ is: 'dead', kind: 'reset' });
      return;
    }

    try {
      const answer = await this.auth.inspectLink(this.token);
      this.stage.set(
        answer.live ? { is: 'form', link: answer.value } : { is: 'dead', kind: answer.kind },
      );
    } catch (error) {
      // The platform could not be asked. Saying the link is dead would be untrue: it is kept,
      // and the person can try again.
      this.stage.set({ is: 'failed', banner: bannerFor(error) });
    }
  }

  protected retry(): void {
    this.stage.set({ is: 'loading' });
    void this.open();
  }

  protected async submit(event: Event, kind: PasswordLinkKind): Promise<void> {
    event.preventDefault();
    const link = this.link();
    if (this.busy() || this.token === null || link === null) {
      return;
    }

    this.banner.set(null);
    if (!this.form.submit()) {
      return;
    }

    this.busy.set(true);
    try {
      const answer = await this.auth.setPasswordWithLink(this.token, this.form.password());
      if (!answer.live) {
        this.stage.set({ is: 'dead', kind: answer.kind });
        return;
      }

      this.flow.announce({
        kind: kind === 'new-account' ? 'accountActivated' : 'passwordChanged',
        email: link.email,
      });
      await this.router.navigateByUrl('/sign-in');
    } catch (error) {
      if (!this.form.refuse(error)) {
        this.banner.set(bannerFor(error));
      }
    } finally {
      this.busy.set(false);
    }
  }
}
