import { CdkMenuTrigger } from '@angular/cdk/menu';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsAppliedChip,
  ScsAppliedChipsComponent,
  ScsBadgeComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsFilterPopoverComponent,
  ScsIconComponent,
  ScsKpiCardComponent,
  ScsListToolbarComponent,
  ScsMenuDirective,
  ScsMenuItemDirective,
  ScsPageHeaderComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsTableDirective,
  ScsTableFooterComponent,
  ToastService,
} from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { OpsCanDirective } from '../access/ops-can';
import { opsDate, opsNumber } from '../i18n/ops-format';
import { TABLE_NARROW } from '../operators/table-narrow';
import { DisablePlanDialog } from './disable-plan.dialog';
import {
  changeWords,
  clinicsCount,
  disabledNote,
  initials,
  money,
  monthlyWords,
  newThisMonth,
  planName,
  planRange,
  usersWords,
} from './plan-words';
import { PlanRow, PlanState, PlansService, PlansView } from './plans.service';

/** Eight rows a page, as §7-9 gives it. */
export const PLANS_PAGE_SIZE = 8;

/**
 * «الباقات» (spec 013, User Story 1 and the row menu of User Story 5; frames `1180:70`/`1180:291`,
 * `1250:70`, `1253:5666`): four cards, a search over both names, a filter on the state, and the
 * table — active plans by monthly price, then disabled ones, equal prices by name (clarified).
 * One read answers all of it; search, filter, order and paging work on what was read.
 */
@Component({
  selector: 'app-plans-list-page',
  imports: [
    TranslocoPipe,
    CdkMenuTrigger,
    RouterLink,
    OpsCanDirective,
    ScsPageHeaderComponent,
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
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsIconComponent,
    ScsMenuDirective,
    ScsMenuItemDirective,
    DisablePlanDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    button[scsButton] {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
    }
    .head {
      display: inline-flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-12);
    }
    .cards {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-lg);
    }
    .most {
      --scs-kpi-value-overflow: hidden;
      --scs-kpi-value-space: nowrap;
    }
    scs-list-toolbar {
      display: flex;
      margin-block-end: var(--scs-spacing-md);
    }
    tr[data-testid='plan-row'] {
      cursor: pointer;
    }
    .plan {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-inline-size: 220px;
    }
    .mark {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: var(--scs-avatar-sm, 32px);
      block-size: var(--scs-avatar-sm, 32px);
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-avatar-text, var(--scs-color-brand-active));
      font-size: var(--scs-avatar-sm-text, 12px);
      font-weight: var(--scs-avatar-weight, 600);
      line-height: 1;
    }
    .name {
      display: block;
      color: var(--scs-color-text-primary);
      font-weight: 500;
      text-decoration: none;
      overflow-wrap: anywhere;
    }
    .name:focus-visible {
      outline: 2px solid var(--scs-color-border-focus, var(--scs-color-brand-default));
      outline-offset: 2px;
    }
    .added {
      display: block;
      margin-block-start: var(--scs-spacing-2);
      color: var(--scs-color-text-tertiary, var(--scs-color-text-secondary));
      font-size: var(--scs-text-label);
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
      /* Two buttons do not fit beside the title at a phone's width: one above the other. */
      .head {
        flex-direction: column;
        align-items: stretch;
      }
    }
  `,
  template: `
    <scs-page-header
      [title]="'app.plans.plan.list.title' | transloco"
      [note]="'app.plans.plan.list.line' | transloco"
    >
      <span class="head">
        <button
          type="button"
          scsButton="secondary"
          data-testid="open-catalogue"
          (click)="go('/plans/features')"
        >
          {{ 'app.plans.plan.list.catalogue' | transloco }}
        </button>
        <button
          *appCan="can.billingManagePlans"
          type="button"
          scsButton
          data-testid="add-plan"
          (click)="go('/plans/new')"
        >
          <scs-icon name="plus" [size]="16" />
          <span>{{ 'app.plans.plan.list.add' | transloco }}</span>
        </button>
      </span>
    </scs-page-header>

    @if (view(); as page) {
      <div class="cards" data-testid="plan-cards">
        <scs-kpi-card
          data-testid="card-total"
          icon="gem"
          tone="brand"
          [label]="'app.plans.plan.cards.total' | transloco"
          [value]="number(page.cards.total)"
          [note]="page.cards.total === 0 ? noData() : disabled(page.cards.disabled)"
        />
        <scs-kpi-card
          data-testid="card-clinics"
          icon="building"
          tone="success"
          [label]="'app.plans.plan.cards.clinics' | transloco"
          [value]="number(page.cards.clinics)"
          [note]="page.cards.clinics === 0 ? noData() : fresh(page.cards.clinicsNewThisMonth)"
        />
        <scs-kpi-card
          class="most"
          data-testid="card-most"
          icon="trophy"
          tone="warning"
          [attr.title]="page.cards.mostWanted ? name(page.cards.mostWanted) : null"
          [label]="'app.plans.plan.cards.mostWanted' | transloco"
          [value]="page.cards.mostWanted ? name(page.cards.mostWanted) : number(0)"
          [note]="page.cards.mostWanted ? clinics(page.cards.mostWanted.clinics) : noData()"
        />
        <scs-kpi-card
          data-testid="card-revenue"
          icon="coins"
          tone="success"
          [label]="'app.plans.plan.cards.revenue' | transloco"
          [value]="page.cards.revenue ? egp(page.cards.revenue.now) : number(0)"
          [note]="page.cards.revenue ? change(page.cards.revenue) : noData()"
          [trend]="trend(page.cards.revenue)"
        />
      </div>
    } @else if (state() === 'loading') {
      <div class="cards" data-testid="plan-cards-loading">
        @for (card of [1, 2, 3, 4]; track card) {
          <scs-card><scs-skeleton [rows]="2" /></scs-card>
        }
      </div>
    }

    @if (state() === 'ready' && !listIsEmpty()) {
      <scs-list-toolbar
        [search]="search()"
        (searchChange)="searchFor($event)"
        [(filterOpen)]="filterOpen"
        [searchPlaceholder]="'app.plans.plan.list.search' | transloco"
        [clearLabel]="'app.plans.list.clearSearch' | transloco"
        [filterLabel]="'app.plans.list.filter' | transloco"
        [filterCount]="applied().length"
        [filterCountLabel]="
          applied().length === 0 ? '' : ('app.plans.list.applied.' + applied().length | transloco)
        "
      >
        <scs-filter-popover
          [heading]="'app.plans.plan.list.filterTitle' | transloco"
          [closeLabel]="'app.plans.list.close' | transloco"
          [applyLabel]="'app.plans.list.apply' | transloco"
          [clearLabel]="'app.plans.list.reset' | transloco"
          (closed)="filterOpen.set(false)"
          (apply)="applyFilters()"
          (clear)="clearFilters()"
        >
          <!-- «الحالة» alone: a plan has no billing cycle (spec, Differences found). -->
          <scs-select
            class="facet"
            testId="filter-state"
            [label]="'app.plans.plan.list.filterState' | transloco"
            [placeholder]="'app.plans.list.filterAny' | transloco"
            [options]="stateOptions()"
            [(value)]="draftState"
          />
        </scs-filter-popover>
      </scs-list-toolbar>
      @if (applied().length > 0) {
        <scs-applied-chips
          [chips]="applied()"
          [clearAllLabel]="'app.plans.list.clearAll' | transloco"
          [removeLabel]="'app.plans.list.removeFilter' | transloco"
          (remove)="removeFilter()"
          (clearAll)="clearFilters()"
        />
      }
    }

    <scs-card>
      @switch (state()) {
        @case ('loading') {
          <scs-skeleton
            data-testid="plans-loading"
            [rows]="8"
            [label]="'app.plans.plan.list.loading' | transloco"
          />
        }
        @case ('failed') {
          <scs-empty-state
            context="error"
            data-testid="plans-failed"
            [title]="'app.plans.plan.list.failedTitle' | transloco"
            [text]="'app.plans.plan.list.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="plans-retry" (click)="load()">
              {{ 'app.plans.retry' | transloco }}
            </button>
          </scs-empty-state>
        }
        @default {
          @if (listIsEmpty()) {
            <scs-empty-state
              data-testid="plans-empty"
              mark="search"
              [title]="'app.plans.plan.list.emptyTitle' | transloco"
              [text]="'app.plans.plan.list.emptyText' | transloco"
            >
              <button
                *appCan="can.billingManagePlans"
                type="button"
                scsButton
                data-testid="plans-empty-add"
                (click)="go('/plans/new')"
              >
                {{ 'app.plans.plan.list.emptyAdd' | transloco }}
              </button>
            </scs-empty-state>
          } @else if (shown().length === 0) {
            <scs-empty-state
              context="no-results"
              data-testid="plans-none"
              [title]="'app.plans.plan.list.noneMatch' | transloco"
            >
              <button
                type="button"
                scsButton="secondary"
                data-testid="plans-clear"
                (click)="clearAll()"
              >
                {{ 'app.plans.plan.list.clearFilters' | transloco }}
              </button>
            </scs-empty-state>
          } @else {
            <div>
              <table scsTable data-testid="plans-table">
                <caption class="sr-only">
                  {{
                    'app.plans.plan.list.caption' | transloco
                  }}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.plans.plan.list.name' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.plan.list.monthly' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.plan.list.clinics' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.plan.list.state' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.plan.list.max' | transloco }}</th>
                    @if (manages()) {
                      <th scope="col" data-kind="menu">
                        {{ 'app.plans.plan.list.actions' | transloco }}
                      </th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (row of pageRows(); track row.id) {
                    <tr
                      data-testid="plan-row"
                      [attr.data-id]="row.id"
                      [attr.data-marked]="marked() === row.id"
                      (click)="open(row)"
                    >
                      <td>
                        <span class="plan">
                          <span class="mark" aria-hidden="true" data-testid="plan-mark">{{
                            letters(row)
                          }}</span>
                          <span>
                            <a
                              class="name"
                              data-testid="plan-name"
                              [routerLink]="['/plans', row.id]"
                              (click)="$event.stopPropagation()"
                              ><bdi>{{ name(row) }}</bdi></a
                            >
                            <span class="added" data-testid="plan-added">{{
                              'app.plans.plan.list.added' | transloco: { date: date(row.createdAt) }
                            }}</span>
                          </span>
                        </span>
                      </td>
                      <td
                        class="quiet"
                        data-testid="plan-monthly"
                        [attr.data-label]="'app.plans.plan.list.monthly' | transloco"
                      >
                        <bdi>{{ monthly(row.monthlyEgp) }}</bdi>
                      </td>
                      <td
                        class="quiet"
                        data-testid="plan-clinics"
                        [attr.data-label]="'app.plans.plan.list.clinics' | transloco"
                      >
                        {{ clinics(row.clinics) }}
                      </td>
                      <td [attr.data-label]="'app.plans.plan.list.state' | transloco">
                        <scs-badge
                          dot
                          [intent]="row.state === 'active' ? 'success' : 'neutral'"
                          data-testid="plan-state"
                          >{{ 'app.plans.plan.state.' + row.state | transloco }}</scs-badge
                        >
                      </td>
                      <td
                        data-testid="plan-max"
                        [attr.data-label]="'app.plans.plan.list.max' | transloco"
                      >
                        {{ users(row.users.limit) }}
                      </td>
                      @if (manages()) {
                        <td data-kind="menu" (click)="$event.stopPropagation()">
                          <button
                            type="button"
                            class="scs-row-menu"
                            data-testid="row-menu"
                            [attr.aria-label]="
                              'app.plans.plan.list.menu' | transloco: { name: name(row) }
                            "
                            [cdkMenuTriggerFor]="rowMenu"
                            [cdkMenuTriggerData]="{ row }"
                            (cdkMenuOpened)="marked.set(row.id)"
                            (cdkMenuClosed)="marked.set(null)"
                          >
                            <scs-icon name="more" [size]="18" />
                          </button>
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <scs-table-footer
              [count]="range()"
              [page]="page()"
              (pageChange)="page.set($event)"
              [pages]="pages()"
              [digits]="number"
              [previousLabel]="'app.plans.list.previous' | transloco"
              [nextLabel]="'app.plans.list.next' | transloco"
              [pagingLabel]="'app.plans.plan.list.paging' | transloco"
            />
          }
        }
      }
    </scs-card>

    <ng-template #rowMenu let-opened="row">
      <!-- The menu is drawn from the plan as the list holds it now, not as it was when first opened. -->
      @if (latest(opened); as row) {
        <div scsMenu data-testid="row-menu-items">
          <button type="button" scsMenuItem data-testid="row-view" (chosen)="open(row)">
            <scs-icon name="menu-view" [size]="16" />
            {{ 'app.plans.plan.list.view' | transloco }}
          </button>
          <button
            type="button"
            scsMenuItem
            data-testid="row-edit"
            (chosen)="go('/plans/' + row.id + '/edit')"
          >
            <scs-icon name="menu-edit" [size]="16" />
            {{ 'app.plans.plan.list.edit' | transloco }}
          </button>
          @if (row.state === 'active') {
            <div class="scs-menu-separator" role="separator"></div>
            <button
              type="button"
              scsMenuItem
              danger
              data-testid="row-disable"
              (chosen)="disabling.set(row)"
            >
              <scs-icon name="menu-disable" [size]="16" />
              {{ 'app.plans.plan.list.disable' | transloco }}
            </button>
          } @else {
            <button type="button" scsMenuItem data-testid="row-enable" (chosen)="enable(row)">
              <scs-icon name="menu-enable" [size]="16" />
              {{ 'app.plans.plan.list.enable' | transloco }}
            </button>
          }
        </div>
      }
    </ng-template>

    <app-disable-plan
      [plan]="disabling()"
      (closed)="closeDialog()"
      (done)="closeDialog(true)"
    />
  `,
})
export class PlansListPage {
  private readonly plans = inject(PlansService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly words = inject(TranslocoService);
  private readonly access = inject(OpsAccess);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  // Words written in code follow the page's language when it changes.
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });

  protected readonly can = OpsCan;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly view = signal<PlansView | null>(null);

  protected readonly search = signal('');
  protected readonly stateFilter = signal('');
  protected readonly page = signal(1);

  protected readonly filterOpen = signal(false);
  protected readonly draftState = signal('');
  protected readonly marked = signal<string | null>(null);

  protected readonly disabling = signal<PlanRow | null>(null);

  /** Whoever manages plans: what the platform said with the list, and the session's codes. */
  protected readonly manages = computed(
    () => (this.view()?.can.manage ?? false) && this.access.can(OpsCan.billingManagePlans),
  );

  private readonly all = computed(() => this.view()?.items ?? []);

  protected readonly listIsEmpty = computed(
    () => this.state() === 'ready' && this.all().length === 0,
  );

  /** What the search and the filter leave: active by price, then disabled, equal prices by name. */
  protected readonly shown = computed(() => {
    const words = this.search().trim().toLocaleLowerCase();
    const language = this.language.language();
    return this.all()
      .filter(
        (row) =>
          (words === '' ||
            [row.nameAr, row.nameEn].some((text) => text.toLocaleLowerCase().includes(words))) &&
          (this.stateFilter() === '' || row.state === this.stateFilter()),
      )
      .sort(
        (a, b) =>
          (a.state === 'active' ? 0 : 1) - (b.state === 'active' ? 0 : 1) ||
          a.monthlyEgp - b.monthlyEgp ||
          planName(a, language).localeCompare(planName(b, language), language),
      );
  });

  protected readonly pages = computed(() =>
    Math.max(1, Math.ceil(this.shown().length / PLANS_PAGE_SIZE)),
  );
  protected readonly pageRows = computed(() => {
    const page = Math.min(this.page(), this.pages());
    return this.shown().slice((page - 1) * PLANS_PAGE_SIZE, page * PLANS_PAGE_SIZE);
  });
  protected readonly range = computed(() => {
    this.spoken();
    const total = this.shown().length;
    const from = total === 0 ? 0 : (Math.min(this.page(), this.pages()) - 1) * PLANS_PAGE_SIZE + 1;
    return planRange(
      this.words,
      from,
      from + this.pageRows().length - 1,
      total,
      this.language.language(),
    );
  });

  protected readonly stateOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const present = new Set(this.all().map((row) => row.state));
    return (['active', 'disabled'] as const)
      .filter((state) => present.has(state))
      .map((state) => ({
        value: state,
        label: this.words.translate(`app.plans.plan.state.${state}`),
      }));
  });

  /** One chip for the state: «الحالة: نشطة». */
  protected readonly applied = computed<ScsAppliedChip[]>(() => {
    this.spoken();
    const value = this.stateFilter() as PlanState | '';
    return value === ''
      ? []
      : [
          {
            key: 'state',
            label: `${this.words.translate('app.plans.plan.list.filterState')}: ${this.words.translate(`app.plans.plan.state.${value}`)}`,
          },
        ];
  });

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
  }

  /** The plan of this id as last read: a disable or an enable changes it under an open menu. */
  protected latest(row: PlanRow): PlanRow {
    return this.all().find((known) => known.id === row.id) ?? row;
  }

  protected name(plan: { nameAr: string; nameEn: string }): string {
    return planName(plan, this.language.language());
  }

  protected letters(row: PlanRow): string {
    return initials(this.name(row), this.language.language());
  }

  protected date(value: string): string {
    return opsDate(value, this.language.language());
  }

  protected monthly(value: number): string {
    this.spoken();
    return monthlyWords(this.words, value, this.language.language());
  }

  protected egp(value: number): string {
    this.spoken();
    return money(this.words, value, 'egp', this.language.language());
  }

  protected clinics(count: number): string {
    this.spoken();
    return clinicsCount(this.words, count, this.language.language());
  }

  protected users(limit: number | null): string {
    this.spoken();
    return usersWords(this.words, limit, this.language.language());
  }

  protected disabled(count: number): string {
    this.spoken();
    return disabledNote(this.words, count, this.language.language());
  }

  protected fresh(count: number): string {
    this.spoken();
    return newThisMonth(this.words, count, this.language.language());
  }

  protected change(revenue: { now: number; lastMonthEnd: number | null }): string {
    this.spoken();
    return changeWords(this.words, revenue.now, revenue.lastMonthEnd, this.language.language());
  }

  protected trend(
    revenue: { now: number; lastMonthEnd: number | null } | null,
  ): 'positive' | 'negative' {
    return revenue !== null && revenue.lastMonthEnd !== null && revenue.now < revenue.lastMonthEnd
      ? 'negative'
      : 'positive';
  }

  protected noData(): string {
    this.spoken();
    return this.words.translate('app.plans.plan.cards.noData');
  }

  /** The list and its cards in one answer, so they never disagree. */
  protected async load(): Promise<void> {
    if (this.view() === null) {
      this.state.set('loading');
    }
    try {
      this.view.set(await this.plans.list());
      this.state.set('ready');
    } catch {
      // A reload that fails after a change keeps what was last read, and says so (FR-009a).
      if (this.view() === null) {
        this.state.set('failed');
      } else {
        this.toasts.show(this.words.translate('app.plans.plan.list.failedTitle'), '', 'danger');
      }
    }
  }

  /** «تفعيل الباقة» has no dialog (§5-45). */
  protected async enable(row: PlanRow): Promise<void> {
    try {
      await this.plans.enable(row.id);
      this.toasts.show(this.words.translate('app.plans.plan.disable.enabled'));
      await this.load();
    } catch {
      this.toasts.show(this.words.translate('app.plans.plan.disable.enableFailed'), '', 'danger');
    }
    this.focusMenu(row.id);
  }

  /** Closes the dialog however it was closed, and gives the focus back to the row's menu. */
  protected async closeDialog(reload = false): Promise<void> {
    const id = this.disabling()?.id;
    this.disabling.set(null);
    if (reload) {
      await this.load();
    }
    if (id !== undefined) {
      this.focusMenu(id);
    }
  }

  protected open(row: PlanRow): void {
    this.go(`/plans/${row.id}`);
  }

  protected searchFor(words: string): void {
    this.search.set(words);
    this.page.set(1);
  }

  protected applyFilters(): void {
    this.stateFilter.set(this.draftState());
    this.page.set(1);
    this.filterOpen.set(false);
  }

  protected removeFilter(): void {
    this.clearFilters();
  }

  protected clearFilters(): void {
    this.draftState.set('');
    this.applyFilters();
  }

  protected clearAll(): void {
    this.search.set('');
    this.clearFilters();
  }

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }

  private focusMenu(id: string): void {
    setTimeout(() =>
      this.host.nativeElement
        .querySelector<HTMLElement>(
          `[data-testid="plan-row"][data-id="${CSS.escape(id)}"] [data-testid="row-menu"]`,
        )
        ?.focus(),
    );
  }
}
