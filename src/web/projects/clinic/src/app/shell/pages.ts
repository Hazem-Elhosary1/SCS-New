import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AccessService } from 'core';
import {
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsLinkDirective,
  ScsNoAccessComponent,
  ScsPageHeaderComponent,
  ScsStateComponent,
} from 'ui';
import { Destination, PARENTS, destination } from './destinations';

/** The destination a route of the frame was generated for, and the parent it sits under. */
function routeDestination(): { readonly place: Destination; readonly parentKey: string } {
  const key = (inject(ActivatedRoute).snapshot.data as { destination: string }).destination;
  const place = destination(key);
  return { place, parentKey: PARENTS.find((parent) => parent.key === place.parent)?.labelKey ?? '' };
}

/**
 * What a destination shows until the spec that owns it delivers its page (spec 006, FR-006):
 * its own name in the page header, and a card saying so. The side bar leads here from now on.
 */
@Component({
  selector: 'app-not-yet-destination-page',
  imports: [TranslocoPipe, ScsPageHeaderComponent, ScsCardComponent, ScsEmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <scs-page-header
      [title]="at.place.labelKey | transloco"
      [parent]="at.parentKey ? (at.parentKey | transloco) : ''"
    />
    <scs-card data-testid="page-not-yet">
      <scs-empty-state
        [title]="'ui.page.notYet.title' | transloco"
        [text]="'app.shell.notYetText' | transloco"
      />
    </scs-card>
  `,
})
export class NotYetDestinationPage {
  protected readonly at = routeDestination();
}

/**
 * What a destination shows to a person whose role does not open it (spec 005, FR-029), inside
 * the frame. Nothing of the destination is loaded and nothing is asked of the platform.
 */
@Component({
  selector: 'app-no-access-page',
  imports: [TranslocoPipe, ScsPageHeaderComponent, ScsNoAccessComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <scs-page-header
      [title]="at.place.labelKey | transloco"
      [parent]="at.parentKey ? (at.parentKey | transloco) : ''"
      [note]="
        (roleName() ? 'app.shell.noAccessNote' : 'app.shell.noRoleNote')
          | transloco: { permission: permissionName() }
      "
    />
    <scs-no-access
      [permissionName]="permissionName()"
      [roleName]="roleKey() ? (roleKey()! | transloco) : roleName()"
    />
  `,
})
export class NoAccessPage {
  private readonly access = inject(AccessService);

  protected readonly at = routeDestination();
  protected readonly permissionName = computed(() =>
    this.access.nameOf(this.at.place.permission ?? ''),
  );

  /** Set for a ready role that still has its original name: shown in the page's language. */
  protected readonly roleKey = this.access.roleNameKey;

  /** The name the clinic wrote; empty when the account holds no role. */
  protected readonly roleName = computed(() => this.access.role()?.name ?? '');
}

/** A destination the clinic's plan leaves out (spec 006, FR-008): the plan is named as the reason. */
@Component({
  selector: 'app-not-in-plan-page',
  imports: [
    TranslocoPipe,
    RouterLink,
    ScsPageHeaderComponent,
    ScsCardComponent,
    ScsStateComponent,
    ScsLinkDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <scs-page-header
      [title]="at.place.labelKey | transloco"
      [parent]="at.parentKey ? (at.parentKey | transloco) : ''"
    />
    <scs-card data-testid="page-not-in-plan">
      <scs-state state="not-in-plan" [message]="'app.shell.notInPlanText' | transloco">
        <a scsUpgrade scsLink routerLink="/account/subscription" data-testid="plan-link">{{
          'app.nav.subscription' | transloco
        }}</a>
      </scs-state>
    </scs-card>
  `,
})
export class NotInPlanPage {
  protected readonly at = routeDestination();
}

/** An address that is no destination of the console (spec 006, FR-007). */
@Component({
  selector: 'app-not-found-page',
  imports: [
    TranslocoPipe,
    RouterLink,
    ScsPageHeaderComponent,
    ScsCardComponent,
    ScsEmptyStateComponent,
    ScsLinkDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <scs-page-header [title]="'app.shell.notFoundTitle' | transloco" />
    <scs-card data-testid="page-not-found">
      <scs-empty-state context="not-found" [title]="'app.shell.notFoundText' | transloco">
        <a scsLink routerLink="/" data-testid="home-link">{{ 'app.nav.home' | transloco }}</a>
      </scs-empty-state>
    </scs-card>
  `,
})
export class NotFoundPage {}

/**
 * A page a later spec delivers that is reached before sign-in: the privacy policy and the
 * terms (086). The links to them exist from now on, so they lead somewhere that says so.
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
      min-block-size: calc(100dvh - var(--scs-env-strip, 0px));
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
