import { CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsIconComponent,
  ScsKpiCardComponent,
  ScsListToolbarComponent,
  ScsMenuDirective,
  ScsMenuItemDirective,
  ScsSkeletonComponent,
  ScsBadgeComponent,
  ScsTableDirective,
  ScsTableFooterComponent,
} from 'ui';
import { OpsAccess } from '../access/ops-access.service';
import { opsNumber } from '../i18n/ops-format';
import { countWords } from './count-words';
import { DeleteRoleDialog } from './delete-role.dialog';
import { OperatorsService, RoleRow, RolesListView } from './operators.service';
import { TABLE_NARROW } from './table-narrow';

/**
 * The roles tab (spec 011, FR-013; frames `1735:557`/`1735:772`): four cards, a search over
 * what is on the page, and the roles — the platform's own three first. A system role has no
 * menu: it is never edited or deleted.
 */
@Component({
  selector: 'app-roles-list',
  imports: [
    TranslocoPipe,
    CdkMenuTrigger,
    ScsCardComponent,
    ScsKpiCardComponent,
    ScsListToolbarComponent,
    ScsTableDirective,
    ScsTableFooterComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsIconComponent,
    ScsMenuDirective,
    ScsMenuItemDirective,
    DeleteRoleDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .cards {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-lg);
    }
    scs-list-toolbar {
      display: flex;
      margin-block-end: var(--scs-spacing-md);
    }
    .name {
      font-weight: 500;
    }
    .quiet {
      color: var(--scs-color-text-secondary);
    }
    td:first-child {
      min-inline-size: 200px;
    }
    .sr-only {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    ${TABLE_NARROW}
    @media (max-width: 900px) {
      .cards {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
    @media (max-width: 480px) {
      .cards {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
  template: `
    @if (view(); as list) {
      <div class="cards" data-testid="role-cards">
        <scs-kpi-card
          data-testid="card-roles-total"
          icon="key"
          tone="info"
          [label]="'app.operators.roles.cards.total' | transloco"
          [value]="number(list.cards.total)"
          [note]="'app.operators.roles.cards.totalNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-roles-in-use"
          icon="check"
          tone="success"
          [label]="'app.operators.roles.cards.inUse' | transloco"
          [value]="number(list.cards.inUse)"
          [note]="'app.operators.roles.cards.inUseNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-roles-custom"
          icon="clock"
          tone="warning"
          [label]="'app.operators.roles.cards.custom' | transloco"
          [value]="number(list.cards.custom)"
          [note]="'app.operators.roles.cards.customNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-roles-system"
          icon="shield-check"
          tone="brand"
          [label]="'app.operators.roles.cards.system' | transloco"
          [value]="number(list.cards.system)"
          [note]="'app.operators.roles.cards.systemNote' | transloco"
        />
      </div>

      <scs-list-toolbar
        [(search)]="search"
        [searchPlaceholder]="'app.operators.roles.search' | transloco"
        [clearLabel]="'app.operators.list.clearSearch' | transloco"
      />
    } @else if (state() === 'loading') {
      <div class="cards">
        @for (card of [1, 2, 3, 4]; track card) {
          <scs-card><scs-skeleton [rows]="2" /></scs-card>
        }
      </div>
    }

    <scs-card>
      @switch (state()) {
        @case ('loading') {
          <scs-skeleton data-testid="roles-loading" [rows]="5" [label]="'app.operators.roles.loading' | transloco" />
        }
        @case ('failed') {
          <scs-empty-state
            context="error"
            data-testid="roles-failed"
            [title]="'app.operators.roles.failedTitle' | transloco"
            [text]="'app.operators.roles.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="roles-retry" (click)="load()">
              {{ 'app.operators.retry' | transloco }}
            </button>
          </scs-empty-state>
        }
        @default {
          @if (shown().length === 0) {
            <scs-empty-state context="no-results" data-testid="roles-none" [title]="'app.operators.roles.none' | transloco" />
          } @else {
            <div>
              <table scsTable data-testid="roles-table">
                <caption class="sr-only">
                  {{
                    'app.operators.roles.caption' | transloco
                  }}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.operators.roles.column.name' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.roles.column.category' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.roles.column.operators' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.roles.column.state' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.roles.column.permissions' | transloco }}</th>
                    <th scope="col" data-kind="menu">{{ 'app.operators.roles.column.actions' | transloco }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (role of shown(); track role.id) {
                    <tr data-testid="role-row" [attr.data-role]="role.systemKey ?? role.id" [attr.data-marked]="marked() === role.id">
                      <td>
                        <span class="name" data-testid="role-name"
                          ><bdi>{{ access.nameOfRole(role) }}</bdi></span
                        >
                        @if (describe(role); as words) {
                          <span class="sub" data-testid="role-description"
                            ><bdi>{{ words }}</bdi></span
                          >
                        }
                      </td>
                      <td class="quiet" data-testid="role-category" [attr.data-label]="'app.operators.roles.column.category' | transloco">
                        {{ (role.systemKey ? 'app.operators.roles.system' : 'app.operators.roles.custom') | transloco }}
                      </td>
                      <td class="quiet" data-testid="role-operators" [attr.data-label]="'app.operators.roles.column.operators' | transloco">{{ count('operators', role.operators) }}</td>
                      <td [attr.data-label]="'app.operators.roles.column.state' | transloco">
                        <scs-badge [intent]="role.inUse ? 'success' : 'neutral'" data-testid="role-state">{{
                          (role.inUse ? 'app.operators.roles.inUse' : 'app.operators.roles.notInUse') | transloco
                        }}</scs-badge>
                      </td>
                      <td data-testid="role-permissions" [attr.data-label]="'app.operators.roles.column.permissions' | transloco">{{ count('permissions', role.permissions) }}</td>
                      <td data-kind="menu">
                        @if (role.can.edit || role.can.delete) {
                          <button
                            type="button"
                            class="scs-row-menu"
                            data-testid="role-menu"
                            [attr.aria-label]="'app.operators.roles.menu' | transloco: { name: role.name }"
                            [cdkMenuTriggerFor]="roleMenu"
                            [cdkMenuTriggerData]="{ role }"
                            (cdkMenuOpened)="marked.set(role.id)"
                            (cdkMenuClosed)="marked.set(null)"
                          >
                            <scs-icon name="more" [size]="18" />
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <scs-table-footer
              [count]="'app.operators.roles.count' | transloco: { count: number(shown().length) }"
              [previousLabel]="'app.operators.list.previous' | transloco"
              [nextLabel]="'app.operators.list.next' | transloco"
            />
          }
        }
      }
    </scs-card>

    <ng-template #roleMenu let-role="role">
      <div scsMenu data-testid="role-menu-items">
        @if (role.can.edit) {
          <button type="button" scsMenuItem data-testid="role-edit" (chosen)="edit(role)">
            <scs-icon name="pencil" [size]="18" />
            {{ 'app.operators.roles.edit' | transloco }}
          </button>
        }
        @if (role.can.delete) {
          <button type="button" scsMenuItem danger data-testid="role-delete" (chosen)="deleting.set(role)">
            <scs-icon name="trash" [size]="18" />
            {{ 'app.operators.roles.delete' | transloco }}
          </button>
        }
      </div>
    </ng-template>

    <app-delete-role [role]="deleting()" (closed)="deleting.set(null)" (deleted)="afterDelete()" />
  `,
})
export class RolesListComponent {
  private readonly team = inject(OperatorsService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly words = inject(TranslocoService);
  protected readonly access = inject(OpsAccess);

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly view = signal<RolesListView | null>(null);
  protected readonly search = signal('');
  protected readonly marked = signal<string | null>(null);
  protected readonly deleting = signal<RoleRow | null>(null);

  /** The list is one page: the search works on what is already here (research R13). */
  protected readonly shown = computed(() => {
    const wanted = this.search().trim().toLocaleLowerCase();
    const all = this.view()?.items ?? [];
    return wanted === ''
      ? all
      : all.filter((role) =>
          `${this.access.nameOfRole(role)} ${this.describe(role)}`.toLocaleLowerCase().includes(wanted),
        );
  });

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    if (this.view() === null) {
      this.state.set('loading');
    }
    try {
      this.view.set(await this.team.roles());
      this.state.set('ready');
    } catch {
      this.view.set(null);
      this.state.set('failed');
    }
  }

  /** A system role is described in the page's language; a custom one as it was written. */
  protected describe(role: RoleRow): string {
    return role.systemKey !== null
      ? this.words.translate(`app.operators.roles.description.${role.systemKey}`)
      : (role.description ?? '');
  }

  protected count(what: 'operators' | 'permissions', value: number): string {
    return countWords(this.words, `app.operators.roles.${what}`, value, this.language.language());
  }

  protected edit(role: RoleRow): void {
    void this.router.navigate(['/operators/roles', role.id, 'edit']);
  }

  protected afterDelete(): void {
    this.deleting.set(null);
    void this.load();
  }
}
