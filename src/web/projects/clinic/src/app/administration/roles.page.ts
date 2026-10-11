import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AccessService, LanguageService, PERMISSIONS } from 'core';
import {
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsPageHeaderComponent,
  ScsSkeletonComponent,
  ScsStateComponent,
  ScsTableDirective,
} from 'ui';

export const ROLES_VIEW = 'admin.roles.view';
export const ROLES_MANAGE = 'admin.roles.manage';

/** A role as `GET /v1/roles` lists it. */
export interface RoleRow {
  readonly id: string;
  readonly name: string;
  readonly readyKey: string | null;
  readonly description: string | null;
  readonly category: 'ready' | 'custom';
  readonly fixed: boolean;
  readonly permissionCount: number;
  readonly holders: number;
}

/**
 * «الأدوار والصلاحيات», for reading (spec 006, FR-038; design: page "Roles", "Roles / Roles
 * List" and "Roles List Read Only"). Two short lists — the roles that ship with the platform
 * and the clinic's own — each row with the role, how many accounts hold it and how much of the
 * catalogue it carries. Creating, editing, deleting and a role's detail are spec 027.
 */
@Component({
  selector: 'app-roles-page',
  imports: [
    TranslocoPipe,
    ScsPageHeaderComponent,
    ScsCardComponent,
    ScsTableDirective,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsStateComponent,
    ScsButtonDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .notice {
      display: block;
      margin-block-end: var(--scs-spacing-md);
    }
    scs-card + scs-card {
      display: block;
      margin-block-start: var(--scs-spacing-md);
    }
    .name {
      font-weight: 500;
    }
  `,
  template: `
    <scs-page-header
      [title]="'app.administration.roles.title' | transloco"
      [parent]="'app.nav.administration' | transloco"
      [note]="
        state() === 'ready'
          ? ('app.administration.roles.note'
            | transloco
              : {
                  roles: ('app.administration.roles.count.' + form() | transloco: { count: digits(count()) }),
                  permissions: digits(all),
                })
          : '—'
      "
    />

    @if (viewOnly()) {
      <scs-state
        class="notice"
        state="view-only"
        [message]="'app.administration.roles.viewOnly' | transloco: { permission: manageName() }"
      />
    }

    @switch (state()) {
      @case ('loading') {
        <!-- Design: "Roles / Roles List Loading" — both cards with their names, grey rows inside. -->
        <div data-testid="roles-loading">
          @for (category of categories; track category) {
            <scs-card
              [heading]="'app.administration.roles.' + category + '.heading' | transloco"
              [note]="'app.administration.roles.' + category + '.note' | transloco"
            >
              <scs-skeleton [rows]="3" [label]="'ui.state.loading.title' | transloco" />
            </scs-card>
          }
        </div>
      }
      @case ('failed') {
        <scs-card data-testid="roles-failed" [heading]="'app.administration.roles.heading' | transloco">
          <scs-empty-state
            context="error"
            [title]="'app.administration.roles.failedTitle' | transloco"
            [text]="'app.administration.roles.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="roles-retry" (click)="load()">
              {{ 'ui.state.error.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @for (list of lists(); track list.category) {
          <scs-card
            [attr.data-testid]="'roles-' + list.category"
            [heading]="'app.administration.roles.' + list.category + '.heading' | transloco"
            [note]="'app.administration.roles.' + list.category + '.note' | transloco"
          >
            @if (list.rows.length === 0) {
              <scs-empty-state
                data-testid="roles-custom-empty"
                mark="people"
                [title]="'app.administration.roles.custom.emptyTitle' | transloco"
                [text]="'app.administration.roles.custom.emptyText' | transloco"
              />
            } @else {
              <table scsTable>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.administration.roles.column.role' | transloco }}</th>
                    <th scope="col" data-kind="number">
                      {{ 'app.administration.roles.column.holders' | transloco }}
                    </th>
                    <th scope="col" data-kind="number">
                      {{ 'app.administration.roles.column.permissions' | transloco }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (role of list.rows; track role.id) {
                    <tr data-testid="role-row" [attr.data-role]="role.readyKey ?? role.id">
                      <td>
                        <span class="name" data-testid="role-name"
                          ><bdi>{{
                            role.readyKey ? ('ui.access.role.' + role.readyKey | transloco) : role.name
                          }}</bdi></span
                        >
                        <!-- A ready role still under its own name is described in the page's
                             language; what the clinic wrote is shown as written. -->
                        @if (role.readyKey) {
                          <span class="sub" data-testid="role-description">{{
                            'app.administration.roles.readyDescription.' + role.readyKey | transloco
                          }}</span>
                        } @else if (role.description) {
                          <span class="sub" data-testid="role-description"
                            ><bdi>{{ role.description }}</bdi></span
                          >
                        }
                      </td>
                      <td data-kind="number" data-testid="role-holders">{{ digits(role.holders) }}</td>
                      <td data-kind="number" data-testid="role-permissions">
                        {{
                          'app.administration.roles.ofAll'
                            | transloco: { held: digits(role.permissionCount), all: digits(all) }
                        }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </scs-card>
        }
      }
    }
  `,
})
export class RolesPage {
  private readonly http = inject(HttpClient);
  private readonly access = inject(AccessService);
  private readonly language = inject(LanguageService);

  /** How many permissions the catalogue has: what "of N" is counted against. */
  protected readonly all = PERMISSIONS.length;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  private readonly roles = signal<readonly RoleRow[]>([]);

  protected readonly viewOnly = computed(() => !this.access.holds(ROLES_MANAGE));
  protected readonly manageName = computed(() => this.access.nameOf(ROLES_MANAGE));

  protected readonly categories = ['ready', 'custom'] as const;

  protected readonly lists = computed(() =>
    this.categories.map((category) => ({
      category,
      rows: this.roles().filter((role) => role.category === category),
    })),
  );

  /** Which form the noun after the number of roles takes in the page's language. */
  protected readonly form = computed(() =>
    new Intl.PluralRules(this.language.language()).select(this.roles().length),
  );

  protected readonly count = computed(() => this.roles().length);

  constructor() {
    this.load();
  }

  /**
   * A refusal for a missing permission is handled where every refusal is: the list of what the
   * person may do is read again and this address is matched again.
   */
  protected load(): void {
    this.state.set('loading');
    this.http.get<{ roles: RoleRow[] }>('/v1/roles').subscribe({
      next: (answer) => {
        this.roles.set(answer.roles);
        this.state.set('ready');
      },
      error: () => this.state.set('failed'),
    });
  }

  protected digits(value: number): string {
    return new Intl.NumberFormat(
      this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB',
    ).format(value);
  }
}
