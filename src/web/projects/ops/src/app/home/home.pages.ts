import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsAuthFrameComponent, ScsIconComponent, ScsLinkDirective } from 'ui';
import { OpsAuthService } from '../auth/ops-auth.service';

const PAGE = `
  :host {
    display: block;
    max-inline-size: 720px;
  }
  h1 {
    margin: 0;
    font-size: var(--scs-text-title);
    font-weight: 700;
  }
  p {
    margin: var(--scs-spacing-sm) 0 var(--scs-spacing-lg);
    color: var(--scs-color-text-secondary);
  }
`;

/**
 * The console's home until the dashboard exists (spec 022): a greeting by name and one line
 * (spec 010, FR-050).
 */
@Component({
  selector: 'app-home-page',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: PAGE,
  template: `
    <h1 data-testid="ops-home-title">
      {{ 'app.home.greeting' | transloco }} <bdi>{{ name() }}</bdi>
    </h1>
    <p data-testid="ops-home-line">{{ 'app.home.line' | transloco }}</p>
  `,
})
export class HomePage {
  private readonly auth = inject(OpsAuthService);
  protected readonly name = computed(() => this.auth.session()?.operator.fullName ?? '');
}

/** An address of the console that does not exist, inside the frame, with a way home. */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, TranslocoPipe, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: PAGE,
  template: `
    <h1 data-testid="ops-not-found">{{ 'app.notFound.title' | transloco }}</h1>
    <p>{{ 'app.notFound.line' | transloco }}</p>
    <a scsLink routerLink="/" data-testid="ops-not-found-home">{{ 'app.notFound.home' | transloco }}</a>
  `,
})
export class NotFoundPage {}

/**
 * A page that is not available yet: the privacy policy until its own spec builds it (spec 010,
 * FR-056). It needs no session, so it stands in the frame of the screens before sign-in.
 */
@Component({
  selector: 'app-not-yet-page',
  imports: [RouterLink, TranslocoPipe, ScsAuthFrameComponent, ScsIconComponent, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    h1 {
      margin: 0 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-title);
      font-weight: 700;
    }
    p {
      margin: 0 0 var(--scs-spacing-lg);
      color: var(--scs-color-text-secondary);
    }
    a {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
    }
  `,
  template: `
    <scs-auth-frame
      [panelTitle]="'app.signIn.panel.title' | transloco"
      [panelLine]="'app.signIn.panel.line' | transloco"
    >
      <h1 data-testid="ops-not-yet">{{ 'app.notYet.title' | transloco }}</h1>
      <p>{{ 'app.notYet.line' | transloco }}</p>
      <a scsLink routerLink="/sign-in" data-testid="ops-not-yet-back">
        <scs-icon name="back" [size]="16" />
        <span>{{ 'app.forgot.back' | transloco }}</span>
      </a>
    </scs-auth-frame>
  `,
})
export class NotYetPage {}
