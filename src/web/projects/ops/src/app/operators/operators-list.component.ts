import { CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsAppliedChip,
  ScsAppliedChipsComponent,
  ScsAvatarComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsFilterPopoverComponent,
  ScsIconComponent,
  ScsKpiCardComponent,
  ScsListToolbarComponent,
  ScsMenuDirective,
  ScsMenuItemDirective,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsBadgeComponent,
  ScsBadgeIntent,
  ScsTableDirective,
  ScsTableFooterComponent,
} from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { opsNumber, opsPercent, opsRelative } from '../i18n/ops-format';
import { OperatorState, OperatorsPageView, OperatorsService } from './operators.service';
import { TABLE_NARROW } from './table-narrow';

export const STATE_TONES: Record<OperatorState, ScsBadgeIntent> = {
  active: 'success',
  invited: 'warning',
  suspended: 'neutral',
};

/**
 * The team's list (spec 011, FR-022 to FR-024; frames `1615:71`/`1615:225`): four cards, a
 * search, a filter that offers only what exists, and the table — newest first, eight to a
 * page. One read answers all of it, so the cards and the rows never disagree.
 */
@Component({
  selector: 'app-operators-list',
  imports: [
    RouterLink,
    TranslocoPipe,
    CdkMenuTrigger,
    ScsCardComponent,
    ScsKpiCardComponent,
    ScsListToolbarComponent,
    ScsFilterPopoverComponent,
    ScsSelectComponent,
    ScsTableDirective,
    ScsTableFooterComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsAppliedChipsComponent,
    ScsAvatarComponent,
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsIconComponent,
    ScsMenuDirective,
    ScsMenuItemDirective,
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
    .who {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-inline-size: 180px;
    }
    .name {
      color: var(--scs-color-text-primary);
      font-weight: 500;
      text-decoration: none;
    }
    .name:hover {
      text-decoration: underline;
    }
    .name:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .quiet {
      color: var(--scs-color-text-secondary);
    }
    .facet {
      display: block;
      min-inline-size: 240px;
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
    @if (view(); as page) {
      <div class="cards" data-testid="operator-cards">
        <scs-kpi-card
          data-testid="card-total"
          icon="user"
          tone="info"
          [label]="'app.operators.cards.total' | transloco"
          [value]="number(page.cards.total)"
          [note]="'app.operators.cards.totalNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-active"
          icon="check"
          tone="success"
          [label]="'app.operators.cards.active' | transloco"
          [value]="number(page.cards.active)"
          [note]="'app.operators.cards.activeNote' | transloco: { count: number(page.cards.activatedThisMonth) }"
        />
        <scs-kpi-card
          data-testid="card-invited"
          icon="clock"
          tone="warning"
          [label]="'app.operators.cards.invited' | transloco"
          [value]="number(page.cards.invited)"
          [note]="(page.cards.invited === 0 ? 'app.operators.cards.invitedNone' : 'app.operators.cards.invitedNote') | transloco"
        />
        <scs-kpi-card
          data-testid="card-two-step"
          icon="shield-check"
          tone="brand"
          [label]="'app.operators.cards.twoStep' | transloco"
          [value]="number(page.cards.twoStepOn) + ' / ' + number(page.cards.total)"
          [note]="'app.operators.cards.twoStepNote' | transloco: { share: percent(page.cards.twoStepOn, page.cards.total) }"
        />
      </div>
    } @else if (state() === 'loading') {
      <div class="cards" data-testid="operator-cards-loading">
        @for (card of [1, 2, 3, 4]; track card) {
          <scs-card><scs-skeleton [rows]="2" /></scs-card>
        }
      </div>
    }

    @if (state() !== 'failed' && !teamIsEmpty()) {
      <scs-list-toolbar
        [search]="search()"
        (searchChange)="searchFor($event)"
        [(filterOpen)]="filterOpen"
        [searchPlaceholder]="'app.operators.list.search' | transloco"
        [clearLabel]="'app.operators.list.clearSearch' | transloco"
        [filterLabel]="'app.operators.list.filter' | transloco"
        [filterCount]="applied().length"
        [filterCountLabel]="applied().length === 0 ? '' : ('app.operators.list.applied.' + applied().length | transloco)"
      >
        <scs-filter-popover
          [heading]="'app.operators.list.filterTitle' | transloco"
          [closeLabel]="'app.operators.list.close' | transloco"
          [applyLabel]="'app.operators.list.apply' | transloco"
          [clearLabel]="'app.operators.list.reset' | transloco"
          (closed)="filterOpen.set(false)"
          (apply)="applyFilters()"
          (clear)="clearFilters()"
        >
          <!-- Two short lists, each offering only what some operator has (FR-023). -->
          <scs-select
            class="facet"
            testId="filter-state"
            [label]="'app.operators.list.filterState' | transloco"
            [placeholder]="'app.operators.list.filterAny' | transloco"
            [options]="stateOptions()"
            [(value)]="draftState"
          />
          <scs-select
            class="facet"
            testId="filter-role"
            [label]="'app.operators.list.filterRole' | transloco"
            [placeholder]="'app.operators.list.filterAny' | transloco"
            [options]="roleOptions()"
            [(value)]="draftRole"
          />
        </scs-filter-popover>
      </scs-list-toolbar>
      @if (applied().length > 0) {
        <scs-applied-chips
          [chips]="applied()"
          [clearAllLabel]="'app.operators.list.clearAll' | transloco"
          [removeLabel]="'app.operators.list.removeFilter' | transloco"
          (remove)="removeFilter($event)"
          (clearAll)="clearFilters()"
        />
      }
    }

    <scs-card>
      @switch (state()) {
        @case ('loading') {
          <scs-skeleton data-testid="operators-loading" [rows]="8" [label]="'app.operators.list.loading' | transloco" />
        }
        @case ('failed') {
          <scs-empty-state
            context="error"
            data-testid="operators-failed"
            [title]="'app.operators.list.failedTitle' | transloco"
            [text]="'app.operators.list.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="operators-retry" (click)="load()">
              {{ 'app.operators.retry' | transloco }}
            </button>
          </scs-empty-state>
        }
        @default {
          @if (teamIsEmpty()) {
            <scs-empty-state
              data-testid="operators-empty"
              mark="people"
              [title]="'app.operators.list.emptyTitle' | transloco"
              [text]="'app.operators.list.emptyText' | transloco"
            >
              @if (access.can(can.operatorsManage)) {
                <button type="button" scsButton data-testid="operators-empty-add" (click)="go('/operators/new')">
                  {{ 'app.operators.add' | transloco }}
                </button>
              }
            </scs-empty-state>
          } @else if (rows().length === 0) {
            <scs-empty-state
              context="no-results"
              data-testid="operators-none"
              [title]="'app.operators.list.emptyTitle' | transloco"
              [text]="'app.operators.list.noneText' | transloco"
            >
              <button type="button" scsButton="secondary" data-testid="operators-clear" (click)="clearAll()">
                {{ 'app.operators.list.clearFilters' | transloco }}
              </button>
            </scs-empty-state>
          } @else {
            <div>
              <table scsTable data-testid="operators-table">
                <caption class="sr-only">
                  {{
                    'app.operators.list.caption' | transloco
                  }}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.operators.list.column.name' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.list.column.role' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.list.column.lastSignIn' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.list.column.state' | transloco }}</th>
                    <th scope="col">{{ 'app.operators.list.column.twoStep' | transloco }}</th>
                    <th scope="col" data-kind="menu">{{ 'app.operators.list.column.actions' | transloco }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.id) {
                    <tr data-testid="operator-row" [attr.data-marked]="marked() === row.id">
                      <td>
                        <span class="who">
                          <scs-avatar size="sm" [name]="row.fullName" aria-hidden="true" />
                          <span>
                            <a class="name" data-testid="operator-name" [routerLink]="['/operators', row.id]"
                              ><bdi>{{ row.fullName }}</bdi></a
                            >
                            <span class="sub" data-testid="operator-email"><bdi dir="ltr">{{ row.email }}</bdi></span>
                          </span>
                        </span>
                      </td>
                      <td class="quiet" data-testid="operator-role" [attr.data-label]="'app.operators.list.column.role' | transloco">
                        <bdi>{{ access.nameOfRole(row.role) }}</bdi>
                      </td>
                      <td class="quiet" data-testid="operator-last" [attr.data-label]="'app.operators.list.column.lastSignIn' | transloco">
                        {{ row.lastSignInAtUtc === null ? ('app.operators.never' | transloco) : lastSignIn(row.lastSignInAtUtc) }}
                      </td>
                      <td [attr.data-label]="'app.operators.list.column.state' | transloco">
                        <scs-badge [intent]="tones[row.state]" data-testid="operator-state">{{
                          'app.operators.state.' + row.state | transloco
                        }}</scs-badge>
                      </td>
                      <td data-testid="operator-two-step" [attr.data-label]="'app.operators.list.column.twoStep' | transloco">
                        {{ (row.twoStep ? 'app.operators.twoStep.on' : 'app.operators.twoStep.off') | transloco }}
                      </td>
                      <td data-kind="menu">
                        <button
                          type="button"
                          class="scs-row-menu"
                          data-testid="row-menu"
                          [attr.aria-label]="'app.operators.list.menu' | transloco: { name: row.fullName }"
                          [cdkMenuTriggerFor]="rowMenu"
                          [cdkMenuTriggerData]="{ row }"
                          (cdkMenuOpened)="marked.set(row.id)"
                          (cdkMenuClosed)="marked.set(null)"
                        >
                          <scs-icon name="more" [size]="18" />
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <scs-table-footer
              [count]="'app.operators.list.count' | transloco: { from: number(from()), to: number(to()), total: number(total()) }"
              [page]="page()"
              (pageChange)="openPage($event)"
              [pages]="pages()"
              [digits]="number"
              [previousLabel]="'app.operators.list.previous' | transloco"
              [nextLabel]="'app.operators.list.next' | transloco"
              [pagingLabel]="'app.operators.list.paging' | transloco"
            />
          }
        }
      }
    </scs-card>

    <ng-template #rowMenu let-row="row">
      <div scsMenu data-testid="row-menu-items">
        <button type="button" scsMenuItem data-testid="row-view" (chosen)="go('/operators/' + row.id)">
          <scs-icon name="eye" [size]="18" />
          {{ 'app.operators.list.view' | transloco }}
        </button>
        @if (access.can(can.operatorsManage)) {
          <button type="button" scsMenuItem data-testid="row-edit" (chosen)="go('/operators/' + row.id + '/edit')">
            <scs-icon name="pencil" [size]="18" />
            {{ 'app.operators.list.edit' | transloco }}
          </button>
        }
      </div>
    </ng-template>
  `,
})
export class OperatorsListComponent {
  private readonly team = inject(OperatorsService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  protected readonly access = inject(OpsAccess);

  protected readonly can = OpsCan;
  protected readonly tones = STATE_TONES;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly view = signal<OperatorsPageView | null>(null);

  protected readonly search = signal('');
  protected readonly stateFilter = signal('');
  protected readonly roleFilter = signal('');
  protected readonly page = signal(1);

  protected readonly filterOpen = signal(false);
  protected readonly draftState = signal('');
  protected readonly draftRole = signal('');
  protected readonly marked = signal<string | null>(null);

  protected readonly rows = computed(() => this.view()?.items ?? []);
  protected readonly total = computed(() => this.view()?.total ?? 0);
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / (this.view()?.pageSize ?? 8))));
  protected readonly from = computed(() => (this.total() === 0 ? 0 : (this.page() - 1) * (this.view()?.pageSize ?? 8) + 1));
  protected readonly to = computed(() => Math.min(this.total(), this.from() + this.rows().length - 1));

  private readonly words = inject(TranslocoService);

  protected readonly stateOptions = computed<ScsSelectOption[]>(() =>
    (this.view()?.filters.states ?? []).map((state) => ({ value: state, label: this.words.translate(`app.operators.state.${state}`) })),
  );

  protected readonly roleOptions = computed<ScsSelectOption[]>(() =>
    (this.view()?.filters.roles ?? []).map((role) => ({ value: role.id ?? '', label: this.access.nameOfRole(role) })),
  );

  /** What is filtered, one chip each: «الحالة: نشطة», «الدور: وكيل دعم فني». */
  protected readonly applied = computed<ScsAppliedChip[]>(() => {
    const chips: ScsAppliedChip[] = [];
    const named = (options: ScsSelectOption[], value: string) => options.find((option) => option.value === value)?.label ?? value;
    if (this.stateFilter()) {
      chips.push({ key: 'state', label: `${this.words.translate('app.operators.list.filterState')}: ${named(this.stateOptions(), this.stateFilter())}` });
    }
    if (this.roleFilter()) {
      chips.push({ key: 'role', label: `${this.words.translate('app.operators.list.filterRole')}: ${named(this.roleOptions(), this.roleFilter())}` });
    }
    return chips;
  });

  /** Nobody at all — not "nobody matching". */
  protected readonly teamIsEmpty = computed(() => this.state() === 'ready' && this.view()?.cards.total === 0);

  private asked = 0;
  private typing: ReturnType<typeof setTimeout> | null = null;
  private readonly now = Date.now();

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
  }

  protected percent(part: number, whole: number): string {
    return opsPercent(part, whole, this.language.language());
  }

  protected lastSignIn(at: string): string {
    return opsRelative(at, Math.max(this.now, Date.parse(at)), this.language.language());
  }

  /** The rows that match, and the cards and filter values of the whole team, in one answer. */
  protected async load(): Promise<void> {
    const asked = ++this.asked;
    if (this.view() === null) {
      this.state.set('loading');
    }
    try {
      const view = await this.team.list({
        search: this.search().trim(),
        state: this.stateFilter(),
        roleId: this.roleFilter(),
        page: this.page(),
      });
      if (asked === this.asked) {
        this.view.set(view);
        this.page.set(view.page);
        this.state.set('ready');
      }
    } catch {
      if (asked === this.asked) {
        this.view.set(null);
        this.state.set('failed');
      }
    }
  }

  /** Asked a moment after the last key, so a name being typed is one question. */
  protected searchFor(words: string): void {
    this.search.set(words);
    this.page.set(1);
    if (this.typing !== null) {
      clearTimeout(this.typing);
    }
    this.typing = setTimeout(() => void this.load(), 250);
  }

  protected applyFilters(): void {
    this.stateFilter.set(this.draftState());
    this.roleFilter.set(this.draftRole());
    this.page.set(1);
    this.filterOpen.set(false);
    void this.load();
  }

  /** One chip's ×: that filter goes, the other stays. */
  protected removeFilter(key: string): void {
    this.draftState.set(key === 'state' ? '' : this.stateFilter());
    this.draftRole.set(key === 'role' ? '' : this.roleFilter());
    this.applyFilters();
  }

  protected clearFilters(): void {
    this.draftState.set('');
    this.draftRole.set('');
    this.applyFilters();
  }

  protected clearAll(): void {
    this.search.set('');
    this.clearFilters();
  }

  protected openPage(page: number): void {
    this.page.set(page);
    void this.load();
  }

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }
}
