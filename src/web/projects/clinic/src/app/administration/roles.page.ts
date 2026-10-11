import { NgTemplateOutlet } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AccessService, ScsDatePipe } from 'core';
import { catchError, map, of } from 'rxjs';
import { ScsNoAccessComponent, ScsStateComponent } from 'ui';

export const ROLES_VIEW = 'admin.roles.view';
export const ROLES_MANAGE = 'admin.roles.manage';

/**
 * The head of a page of the administration group, as the design draws it: where the page sits,
 * its title, and one line under it. The console's bars around it arrive with spec 006.
 */
@Component({
  selector: 'app-admin-page-head',
  imports: [TranslocoPipe, ScsDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    nav {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-tertiary);
    }
    nav [aria-current] {
      color: var(--scs-color-text-primary);
      font-weight: 500;
    }
    .chevron {
      inline-size: 6px;
      block-size: 6px;
      border-top: 1.5px solid currentColor;
      border-right: 1.5px solid currentColor;
      /* Points onward: to the right in English, to the left in Arabic. */
      transform: rotate(45deg);
    }
    :host-context([dir='rtl']) .chevron {
      transform: rotate(-135deg);
    }
    h1 {
      margin: 14px 0 0;
      font-size: var(--scs-text-title);
      font-weight: 700;
      line-height: 1.5;
      color: var(--scs-color-text-primary);
    }
    p {
      margin: var(--scs-spacing-sm) 0 18px;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    <nav [attr.aria-label]="'app.administration.breadcrumb' | transloco">
      <span>{{ 'app.administration.breadcrumb' | transloco }}</span>
      <span class="chevron" aria-hidden="true"></span>
      <span aria-current="page">{{ title() }}</span>
    </nav>
    <h1 data-testid="page-title">{{ title() }}</h1>
    <p data-testid="page-note">
      {{ now | scsDate: 'long' }}
      @if (note()) {
        — {{ note() }}
      }
    </p>
  `,
})
export class AdminPageHeadComponent {
  /** Already translated. */
  readonly title = input.required<string>();

  /** Already translated; what the page is showing, after the date. */
  readonly note = input('');

  protected readonly now = new Date();
}

/** The canvas a page of the console sits on, until the console's own frame exists (spec 006). */
const PAGE = `
  :host {
    display: block;
    min-block-size: 100dvh;
    padding: var(--scs-spacing-xl);
    background: var(--scs-color-bg-canvas);
  }
`;

/**
 * «الأدوار والصلاحيات», until its pages are built (spec 027). It is here now so that the two
 * things spec 005 delivers can be seen: a person who may read roles and not change them gets
 * the «للعرض فقط» notice, and a person who may not read them never reaches this page at all.
 * It asks the platform for the clinic's roles, so the platform's own answer is part of the page.
 */
@Component({
  selector: 'app-roles-page',
  imports: [NgTemplateOutlet, TranslocoPipe, AdminPageHeadComponent, ScsStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    ${PAGE}

    .not-yet {
      padding: var(--scs-spacing-3xl) var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body);
      text-align: center;
    }
  `,
  template: `
    <app-admin-page-head [title]="'app.administration.roles.title' | transloco" />

    <ng-template #page>
      <section class="not-yet" data-testid="roles-not-yet" [attr.data-roles]="roles()">
        {{ 'ui.page.notYet.title' | transloco }}
      </section>
    </ng-template>

    @if (viewOnly()) {
      <scs-state
        state="view-only"
        [message]="'app.administration.roles.viewOnly' | transloco: { permission: manageName() }"
      >
        <ng-container [ngTemplateOutlet]="page" />
      </scs-state>
    } @else {
      <ng-container [ngTemplateOutlet]="page" />
    }
  `,
})
export class RolesPage {
  private readonly access = inject(AccessService);

  protected readonly viewOnly = computed(() => !this.access.holds(ROLES_MANAGE));
  protected readonly manageName = computed(() => this.access.nameOf(ROLES_MANAGE));

  /**
   * How many roles the platform listed; null until it answers, and when it refuses. A refusal
   * for a missing permission is handled where every refusal is: the list of what the person
   * may do is read again and this address is matched again.
   */
  protected readonly roles = toSignal(
    inject(HttpClient)
      .get<{ roles: unknown[] }>('/v1/roles')
      .pipe(
        map((answer) => answer.roles.length),
        catchError(() => of(null)),
      ),
    { initialValue: null },
  );
}

/**
 * What a destination shows to a person whose role does not open it (spec 005, FR-029). The
 * route says which permission the destination needs and what it is called; nothing of the
 * destination is loaded and nothing is asked of the platform.
 */
@Component({
  selector: 'app-no-access-page',
  imports: [TranslocoPipe, AdminPageHeadComponent, ScsNoAccessComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: PAGE,
  template: `
    <app-admin-page-head
      [title]="titleKey | transloco"
      [note]="
        (roleName() ? 'app.administration.roles.noAccessNote' : 'app.administration.roles.noRoleNote')
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
  private readonly data = inject(ActivatedRoute).snapshot.data as {
    permission: string;
    titleKey: string;
  };

  protected readonly titleKey = this.data.titleKey;
  protected readonly permissionName = computed(() => this.access.nameOf(this.data.permission));

  /** Set for a ready role that still has its original name: shown in the page's language. */
  protected readonly roleKey = this.access.roleNameKey;

  /** The name the clinic wrote; empty when the account holds no role. */
  protected readonly roleName = computed(() => this.access.role()?.name ?? '');
}
