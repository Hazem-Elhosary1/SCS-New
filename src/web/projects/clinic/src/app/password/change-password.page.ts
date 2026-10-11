import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from 'core';
import {
  ScsAuthFrameComponent,
  ScsBannerComponent,
  ScsButtonDirective,
  ScsLinkDirective,
} from 'ui';
import { HelpWindowComponent } from '../sign-in/help-window.component';
import { NewPasswordComponent, NewPasswordForm, PasswordBanner, bannerFor } from './new-password';

/**
 * Replacing a temporary password (spec 004; BRD B-5-12). A person whose password was chosen by
 * someone else lands here after both sign-in steps and reaches nothing else until they choose
 * their own. There is no way to skip it; the platform refuses everything else for them, so the
 * screen only shows what is already true. Signing out stays possible.
 *
 * Its title and button wording are not drawn in the design file and wait for the page review.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [
    TranslocoPipe,
    ScsAuthFrameComponent,
    ScsBannerComponent,
    ScsButtonDirective,
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
    .leave {
      align-self: flex-start;
    }
  `,
  template: `
    <scs-auth-frame (help)="helpOpen.set(true)">
      <h1 data-testid="temporary-title">{{ 'app.password.temporary.title' | transloco }}</h1>
      <p class="subtitle">{{ 'app.password.temporary.subtitle' | transloco }}</p>
      <p class="subtitle">
        {{ 'app.password.forAccount' | transloco }}
        <bdi dir="ltr" data-testid="account-email">{{ session()?.email }}</bdi>
      </p>

      <form novalidate (submit)="submit($event)">
        @if (banner(); as banner) {
          <scs-banner tone="danger" [attr.data-testid]="'banner-' + banner.kind">
            {{
              'app.signIn.banner.' + (banner.kind === 'unreachable' ? 'unreachable' : 'unexpected')
                | transloco
            }}
          </scs-banner>
        }

        <app-new-password #fields [form]="form" />

        <button type="submit" scsButton data-testid="change-password-submit" [busy]="busy()">
          {{
            (busy() ? 'app.password.temporary.submitting' : 'app.password.temporary.submit')
              | transloco
          }}
        </button>

        <button type="button" scsLink class="leave" data-testid="sign-out" (click)="signOut()">
          {{ 'app.password.temporary.signOut' | transloco }}
        </button>
      </form>
    </scs-auth-frame>

    <app-help-window [(open)]="helpOpen" />
  `,
})
export class ChangePasswordPage implements AfterViewInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = new NewPasswordForm();
  protected readonly session = this.auth.session;
  protected readonly busy = signal(false);
  protected readonly helpOpen = signal(false);
  protected readonly banner = signal<PasswordBanner | null>(null);

  private readonly fields = viewChild.required<NewPasswordComponent>('fields');

  ngAfterViewInit(): void {
    this.fields().focus();
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    this.banner.set(null);
    if (!this.form.submit()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.changeTemporaryPassword(this.form.password());
      await this.router.navigateByUrl('/');
    } catch (error) {
      if (!this.form.refuse(error)) {
        this.banner.set(bannerFor(error));
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
  }
}
