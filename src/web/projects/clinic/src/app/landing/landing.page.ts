import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService, LanguageService } from 'core';
import { PlaceholderPanelComponent, ScsButtonDirective, ScsLinkDirective } from 'ui';

/**
 * Where a person lands after signing in, until the console's bars and home page exist
 * (spec 006). It shows who is signed in and for which clinic, and offers sign-out. Under it is
 * the platform placeholder of spec 001.
 */
@Component({
  selector: 'app-landing-page',
  imports: [TranslocoPipe, PlaceholderPanelComponent, ScsButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .who {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      max-inline-size: 36rem;
      margin: 0 auto;
      padding: var(--scs-spacing-md) var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    .clinic {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 700;
    }
    .person {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    @if (session(); as session) {
      <header class="who" data-testid="signed-in">
        <div>
          <p class="clinic" data-testid="signed-in-clinic">{{ clinicName() }}</p>
          <p class="person">
            {{ 'app.landing.signedInAs' | transloco }}
            <bdi data-testid="signed-in-email">{{ session.email }}</bdi>
          </p>
        </div>
        <button
          type="button"
          scsButton="secondary"
          data-testid="sign-out"
          [busy]="leaving()"
          (click)="signOut()"
        >
          {{ 'app.landing.signOut' | transloco }}
        </button>
      </header>
      <scs-placeholder-panel appNameKey="app.shell.name" />
    }
  `,
})
export class LandingPage {
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);

  protected readonly session = this.auth.session;
  protected readonly leaving = signal(false);

  protected readonly clinicName = computed(() => {
    const clinic = this.session()?.clinic;
    return clinic === undefined
      ? ''
      : this.language.language() === 'ar'
        ? clinic.nameAr
        : clinic.nameEn;
  });

  protected async signOut(): Promise<void> {
    this.leaving.set(true);
    await this.auth.signOut();
  }
}

/**
 * A page a later spec delivers: forgot password (004), the privacy policy and the terms (086).
 * The links to them exist from now on, so they lead somewhere that says so.
 */
@Component({
  selector: 'app-not-yet-page',
  imports: [TranslocoPipe, RouterLink, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    main {
      display: grid;
      place-content: center;
      gap: var(--scs-spacing-md);
      min-block-size: 100dvh;
      padding: var(--scs-spacing-lg);
      text-align: center;
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
    }
  `,
  template: `
    <main data-testid="not-yet">
      <h1>{{ 'ui.page.notYet.title' | transloco }}</h1>
      <a scsLink routerLink="/sign-in">{{ 'ui.page.notYet.back' | transloco }}</a>
    </main>
  `,
})
export class NotYetPage {}
