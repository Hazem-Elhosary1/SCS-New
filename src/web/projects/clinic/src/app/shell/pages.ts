import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AccessService, PlanSource } from 'core';
import {
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsLinkDirective,
  ScsNoAccessComponent,
  ScsPageHeaderComponent,
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

/** Destinations whose card the design file draws with words of its own (spec 014, research R14). */
const DRAWN_WORDS: Readonly<Record<string, string>> = {
  inbox: 'inbox',
  'reports.financial': 'reports',
  'reports.clinical': 'reports',
};

/**
 * A destination the clinic's plan leaves out (spec 006, FR-008; spec 014, FR-027): the page keeps
 * its header, and the card the design draws — «Inbox / Not In Plan», «Financial / Not In Plan» —
 * names the plan as the reason, not a permission. The way to the subscription is offered only to
 * whoever may open it (SH-055); anyone else is told who changes the plan.
 */
@Component({
  selector: 'app-not-in-plan-page',
  imports: [TranslocoPipe, RouterLink, ScsPageHeaderComponent, ScsCardComponent, ScsEmptyStateComponent, ScsLinkDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The frames draw the card's title at 16 SemiBold (`6701:1002017`, `6614:945146`).
  styles: `
    scs-card {
      --scs-empty-title-weight: 600;
    }
  `,
  template: `
    <scs-page-header
      [title]="at.place.labelKey | transloco"
      [parent]="at.parentKey ? (at.parentKey | transloco) : ''"
    />
    <scs-card data-testid="page-not-in-plan">
      <scs-empty-state
        context="not-in-plan"
        [title]="'app.plan.notIn.title' | transloco: { feature: noun() }"
        [text]="lineKey() | transloco: { feature: subject() }"
      >
        @if (canOpenSubscription()) {
          <a scsLink routerLink="/account/subscription" data-testid="plan-link">{{
            'app.plan.notIn.link' | transloco
          }}</a>
        }
      </scs-empty-state>
    </scs-card>
  `,
})
export class NotInPlanPage {
  private readonly access = inject(AccessService);
  private readonly plan = inject(PlanSource);
  private readonly transloco = inject(TranslocoService);
  private readonly language = toSignal(this.transloco.langChanges$, { initialValue: this.transloco.getActiveLang() });

  protected readonly at = routeDestination();
  protected readonly canOpenSubscription = computed(() => this.access.holds('subscription.view'));

  private readonly drawn = DRAWN_WORDS[this.at.place.key] ?? null;

  /** The catalogue's name, or the destination's own when the session names nothing. */
  private readonly named = computed(
    () =>
      this.plan.featureName(this.at.place.key, this.language()) ??
      this.transloco.translate(this.at.place.labelKey, {}, this.language()),
  );

  protected readonly noun = computed(() =>
    this.drawn ? this.transloco.translate(`app.plan.places.${this.drawn}.title`, {}, this.language()) : `«${this.named()}»`,
  );

  protected readonly subject = computed(() =>
    this.drawn ? this.transloco.translate(`app.plan.places.${this.drawn}.subject`, {}, this.language()) : this.named(),
  );

  /** The drawn line names a plural subject; an undrawn one says «… ليست ميزة …», whatever the name. */
  protected readonly lineKey = computed(() => {
    const kind = this.drawn ? 'text' : 'textNamed';
    return `app.plan.notIn.${kind}${this.canOpenSubscription() ? '' : 'NoLink'}`;
  });
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
