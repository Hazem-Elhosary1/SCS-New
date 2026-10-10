import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from 'core';
import { ScsLinkDirective } from '../controls/controls';

/**
 * The frame every sign-in screen shares (BRD B-5): the introduction panel on the end side with
 * the product's three lines and no buttons, the form panel with a 400-wide form in its middle,
 * the language switch in the outer top corner, and a footer with the help link and the links to
 * the privacy policy and the terms. It never shows a clinic's name: before sign-in nothing
 * reveals that a clinic exists (SH-043).
 */
@Component({
  selector: 'scs-auth-frame',
  imports: [TranslocoPipe, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      grid-template-columns: minmax(0, 820fr) minmax(0, 620fr);
      min-block-size: 100dvh;
      background: var(--scs-color-bg-surface);
    }
    .form-panel {
      display: flex;
      flex-direction: column;
      padding: var(--scs-spacing-lg);
    }
    .top {
      display: flex;
      justify-content: flex-start;
    }
    .middle {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      padding-block: var(--scs-spacing-lg);
    }
    .form {
      inline-size: 100%;
      max-inline-size: 400px;
    }
    footer {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--scs-spacing-sm) var(--scs-spacing-lg);
    }
    .intro {
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-3xl);
      background: var(--scs-color-brand-active);
      color: var(--scs-color-text-inverse);
    }
    .logo {
      inline-size: 56px;
      block-size: 56px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-brand-on-brand);
      color: var(--scs-color-brand-active);
      display: grid;
      place-items: center;
    }
    .name {
      margin: 0;
      font-size: var(--scs-text-display-sm);
      font-weight: 700;
      line-height: 1.3;
    }
    .tagline {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
      opacity: 0.9;
    }
    .about {
      margin: 0;
      max-inline-size: 44ch;
      font-size: var(--scs-text-body-lg);
      line-height: 1.8;
      opacity: 0.85;
    }
    @media (max-width: 899px) {
      :host {
        grid-template-columns: minmax(0, 1fr);
      }
      .intro {
        display: none;
      }
    }
  `,
  template: `
    <div class="form-panel" data-testid="auth-form-panel">
      <div class="top">
        <button
          type="button"
          scsLink
          data-testid="language-switch"
          [attr.aria-label]="'ui.language.switch.label' | transloco"
          (click)="language.toggle()"
        >
          {{ 'ui.language.switch.target' | transloco }}
        </button>
      </div>

      <main class="middle">
        <div class="form"><ng-content /></div>
      </main>

      <footer data-testid="auth-footer">
        <button type="button" scsLink data-testid="help-link" (click)="help.emit()">
          {{ 'ui.auth.footer.help' | transloco }}
        </button>
        <a scsLink data-testid="privacy-link" href="/privacy">{{
          'ui.auth.footer.privacy' | transloco
        }}</a>
        <a scsLink data-testid="terms-link" href="/terms">{{
          'ui.auth.footer.terms' | transloco
        }}</a>
      </footer>
    </div>

    <aside class="intro" data-testid="auth-intro">
      <span class="logo" aria-hidden="true">
        <svg
          width="32"
          height="32"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2.2"
          stroke-linecap="round"
        >
          <path d="M12 5v14M5 12h14" />
        </svg>
      </span>
      <p class="name">{{ 'ui.auth.intro.name' | transloco }}</p>
      <p class="tagline">{{ 'ui.auth.intro.tagline' | transloco }}</p>
      <p class="about">{{ 'ui.auth.intro.about' | transloco }}</p>
    </aside>
  `,
})
export class ScsAuthFrameComponent {
  protected readonly language = inject(LanguageService);

  /** The help link in the footer was pressed. */
  readonly help = output<void>();
}
