import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from 'core';
import { ScsLinkDirective } from '../controls/controls';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsLanguageToggleComponent } from '../list/language-toggle.component';

/**
 * The frame every sign-in screen shares (BRD B-5; design file, page "Sign in"): the
 * introduction panel (620 of 1440) on the end side with the product's mark, badge, headline and
 * one paragraph, and no buttons; the form panel (820) with a 400-wide form in its middle; the
 * language toggle in the outer top corner; and a footer with the help link, beside which sit
 * the links to the privacy policy and the terms. It never shows a clinic's name: before sign-in
 * nothing reveals that a clinic exists (SH-043).
 */
@Component({
  selector: 'scs-auth-frame',
  imports: [TranslocoPipe, ScsLinkDirective, ScsIconComponent, ScsLanguageToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.console]': "panelTitle() !== ''" },
  styles: `
    :host {
      display: grid;
      grid-template-columns: minmax(0, 820fr) minmax(0, 620fr);
      min-block-size: calc(100dvh - var(--scs-env-strip, 0px));
      background: var(--scs-color-bg-surface);
    }
    .form-panel {
      display: flex;
      flex-direction: column;
      padding: var(--scs-spacing-lg) var(--scs-spacing-40, 40px);
    }
    .top {
      display: flex;
      justify-content: flex-start;
      min-block-size: 32px;
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
      align-items: center;
      justify-content: center;
      gap: var(--scs-spacing-sm) var(--scs-spacing-lg);
      min-block-size: 32px;
    }
    footer .scs-link {
      font-size: var(--scs-text-body-sm);
    }
    .help {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    .legal {
      font-weight: 400;
      color: var(--scs-color-text-tertiary);
    }
    .intro {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      justify-content: center;
      gap: var(--scs-spacing-lg);
      padding: var(--scs-spacing-3xl);
      background: var(--scs-color-brand-active);
      color: var(--scs-color-text-inverse);
    }
    .logo {
      display: grid;
      place-items: center;
      inline-size: 48px;
      block-size: 48px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-text-primary);
      color: var(--scs-color-text-inverse);
      font-size: var(--scs-text-title-sm);
      font-weight: 700;
      line-height: 1;
    }
    /* A console's own panel (the operators' design, page "Login"): half the screen in its
       brand colour, a light mark at its top, the title and the line in its middle. */
    :host(.console) {
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
    :host(.console) .form {
      max-inline-size: 440px;
    }
    :host(.console) .intro {
      position: relative;
      gap: var(--scs-spacing-md);
      padding-inline: var(--scs-spacing-60, 60px);
      background: var(--scs-color-brand-default);
    }
    .logo.plain {
      position: absolute;
      inset-block-start: var(--scs-spacing-60, 60px);
      inset-inline-start: var(--scs-spacing-60, 60px);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-default);
    }
    @media (max-width: 899px) {
      :host(.console) {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .badge {
      margin: 0;
      padding: var(--scs-spacing-sm) var(--scs-spacing-12);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-default);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      line-height: 1.4;
    }
    .headline {
      margin: 0;
      font-size: var(--scs-text-display);
      font-weight: 700;
      line-height: 1.5;
    }
    .about {
      margin: 0;
      max-inline-size: 30rem;
      font-size: var(--scs-text-heading-sm);
      line-height: 1.9;
      opacity: 0.9;
    }
    @media (max-width: 899px) {
      :host {
        grid-template-columns: minmax(0, 1fr);
      }
      .form-panel {
        padding-inline: var(--scs-spacing-lg);
      }
      .intro {
        display: none;
      }
    }
  `,
  template: `
    <div class="form-panel" data-testid="auth-form-panel">
      <div class="top">
        <scs-language-toggle
          [current]="language.language()"
          [label]="'ui.language.switch.label' | transloco"
          (chosen)="language.use($event)"
        />
      </div>

      <main class="middle">
        <div class="form"><ng-content /></div>
      </main>

      <footer data-testid="auth-footer">
        @if (panelTitle() === '') {
          <button type="button" scsLink class="help" data-testid="help-link" (click)="help.emit()">
            <scs-icon name="help-circle" [size]="16" />
            <span>{{ 'ui.auth.footer.help' | transloco }}</span>
          </button>
        }
        <a scsLink class="legal" data-testid="privacy-link" href="/privacy">{{
          'ui.auth.footer.privacy' | transloco
        }}</a>
        @if (panelTitle() === '') {
          <a scsLink class="legal" data-testid="terms-link" href="/terms">{{
            'ui.auth.footer.terms' | transloco
          }}</a>
        }
      </footer>
    </div>

    <aside class="intro" data-testid="auth-intro">
      @if (panelTitle() !== '') {
        <span class="logo plain" aria-hidden="true">{{ panelMark() }}</span>
        <p class="headline" data-testid="auth-panel-title">{{ panelTitle() }}</p>
        <p class="about" data-testid="auth-panel-line">{{ panelLine() }}</p>
      } @else {
        <span class="logo" lang="ar" aria-hidden="true">{{ mark }}</span>
        <p class="badge">{{ 'ui.auth.intro.badge' | transloco }}</p>
        <p class="headline">{{ 'ui.auth.intro.headline' | transloco }}</p>
        <p class="about">{{ 'ui.auth.intro.about' | transloco }}</p>
      }
    </aside>
  `,
})
export class ScsAuthFrameComponent {
  protected readonly language = inject(LanguageService);

  /** The product's mark: the letter the Arabic word for "clinic" begins with. */
  protected readonly mark = 'ع';

  /**
   * A console that is not the clinic's gives its own panel: a title and a line that change by
   * screen, already translated, and its mark (the operators console, spec 010). The clinic's
   * badge, the help link and the terms link are the clinic's and are then left out.
   */
  readonly panelTitle = input('');
  readonly panelLine = input('');
  readonly panelMark = input('S');

  /** The help link in the footer was pressed. */
  readonly help = output<void>();
}
