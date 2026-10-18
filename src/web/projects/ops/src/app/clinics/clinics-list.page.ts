import { CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
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
  ScsCheckboxComponent,
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
  ScsSortDirection,
  ScsSortHeaderComponent,
  ScsTableDirective,
  ScsTableFooterComponent,
} from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { OpsCanDirective } from '../access/ops-can';
import { opsCairoDateTime, opsNumber } from '../i18n/ops-format';
import { TABLE_NARROW } from '../operators/table-narrow';
import { clinicName, endsIn, listMark, statusWord, trialLength } from './clinic-words';
import { ClinicRow, ClinicsPage, ClinicsService } from './clinics.service';

/** Eight rows a page (BRD Ops §7-1). */
export const CLINICS_PAGE_SIZE = 8;

/** The pause after the last key before the search is asked (FR-009). */
export const SEARCH_PAUSE_MS = 300;

interface Filters {
  readonly statuses: readonly string[];
  readonly plan: string;
  readonly length: string;
  readonly endsWithin: string;
}

const NO_FILTERS: Filters = { statuses: [], plan: '', length: '', endsWithin: '' };

/**
 * «العيادات» (spec 015, User Story 2; FR-007 – FR-013; frames `606:776`/`599:680`, loading
 * `607:120`, error `608:168`, empty `712:611`, the filter panel `2313:2714`): four cards, a search
 * over the clinic's names and its owner, a filter of four facets, the table with its sortable
 * «ينتهي في», eight rows a page. The platform searches, filters, sorts and pages; each change asks
 * again.
 */
@Component({
  selector: 'app-clinics-list-page',
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
    ScsCheckboxComponent,
    ScsTableDirective,
    ScsTableFooterComponent,
    ScsSortHeaderComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsAppliedChipsComponent,
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
    button[scsButton] {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
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
    tr[data-testid='clinic-row'] {
      cursor: pointer;
    }
    tr[data-testid='clinic-row']:focus-visible {
      outline: 2px solid var(--scs-color-border-focus, var(--scs-color-brand-default));
      outline-offset: -2px;
    }
    .clinic {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-inline-size: 220px;
    }
    .mark {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: 36px;
      block-size: 36px;
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
      font-size: 13px;
      font-weight: 600;
      line-height: 1;
    }
    .name {
      display: block;
      color: var(--scs-color-text-primary);
      font-weight: 500;
      text-decoration: none;
      overflow-wrap: anywhere;
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
      min-inline-size: 260px;
    }
    .statuses {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-sm) var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-md);
    }
    .facet-title {
      grid-column: 1 / -1;
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
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
    <scs-page-header [title]="'app.clinics.list.title' | transloco" [note]="'app.clinics.list.line' | transloco">
      <button *appCan="can.clinicsEdit" type="button" scsButton data-testid="clinics-add" (click)="go('/clinics/new')">
        <scs-icon name="plus" [size]="16" />
        <span>{{ 'app.clinics.list.add' | transloco }}</span>
      </button>
    </scs-page-header>

    @if (state() !== 'failed') {
      @if (page(); as view) {
        <div class="cards" data-testid="clinics-cards">
          <scs-kpi-card
            data-testid="clinics-card-total"
            icon="building"
            tone="brand"
            [label]="'app.clinics.list.cards.total' | transloco"
            [value]="number(view.cards.total)"
            [note]="view.cards.total === 0 ? noData() : words('app.clinics.list.cards.newThisMonth', { count: number(view.cards.newThisMonth) })"
          />
          <scs-kpi-card
            data-testid="clinics-card-active"
            icon="check"
            tone="success"
            [label]="'app.clinics.list.cards.active' | transloco"
            [value]="number(view.cards.active)"
            [note]="view.cards.total === 0 ? noData() : words('app.clinics.list.cards.activeShare', { share: number(view.cards.activeShare) })"
          />
          <scs-kpi-card
            data-testid="clinics-card-trial"
            icon="clock"
            tone="warning"
            [label]="'app.clinics.list.cards.trial' | transloco"
            [value]="number(view.cards.trial)"
            [note]="view.cards.total === 0 ? noData() : view.cards.trialEndingSoon ? words('app.clinics.list.cards.trialSoon') : ''"
          />
          <scs-kpi-card
            data-testid="clinics-card-follow"
            icon="alert-circle"
            tone="danger"
            [label]="'app.clinics.list.cards.follow' | transloco"
            [value]="number(view.cards.needsFollowUp.overdue + view.cards.needsFollowUp.readOnly + view.cards.needsFollowUp.suspended)"
            [note]="
              view.cards.total === 0
                ? noData()
                : words('app.clinics.list.cards.followParts', {
                    overdue: number(view.cards.needsFollowUp.overdue),
                    readOnly: number(view.cards.needsFollowUp.readOnly),
                    suspended: number(view.cards.needsFollowUp.suspended),
                  })
            "
          />
        </div>
      } @else {
        <div class="cards" data-testid="clinics-cards-loading">
          @for (card of [1, 2, 3, 4]; track card) {
            <scs-card><scs-skeleton [rows]="2" /></scs-card>
          }
        </div>
      }
    }

    @if (state() === 'ready' && !listIsEmpty()) {
      <scs-list-toolbar
        [search]="search()"
        (searchChange)="searchFor($event)"
        [(filterOpen)]="filterOpen"
        [searchPlaceholder]="'app.clinics.list.search' | transloco"
        [clearLabel]="'app.clinics.list.clearSearch' | transloco"
        [filterLabel]="'app.clinics.list.filter' | transloco"
        [filterCount]="applied().length"
        [filterCountLabel]="applied().length === 0 ? '' : ('app.clinics.list.applied.' + applied().length | transloco)"
      >
        <scs-filter-popover
          [heading]="'app.clinics.list.filterTitle' | transloco"
          [closeLabel]="'app.clinics.list.close' | transloco"
          [applyLabel]="'app.clinics.list.apply' | transloco"
          [clearLabel]="'app.clinics.list.reset' | transloco"
          (closed)="filterOpen.set(false)"
          (apply)="applyFilters()"
          (clear)="resetDraft()"
        >
          @if (statusChoices().length > 0) {
            <div class="statuses" data-testid="filter-statuses">
              <p class="facet-title">{{ 'app.clinics.list.facets.status' | transloco }}</p>
              @for (status of statusChoices(); track status) {
                <scs-checkbox
                  [testId]="'filter-status-' + status"
                  [label]="'app.clinics.list.statuses.' + status | transloco"
                  [checked]="draft().statuses.includes(status)"
                  (checkedChange)="toggleStatus(status, $event)"
                />
              }
            </div>
          }
          <scs-select
            class="facet"
            testId="filter-plan"
            [label]="'app.clinics.list.facets.plan' | transloco"
            [placeholder]="'app.clinics.list.facets.allPlans' | transloco"
            [options]="planChoices()"
            [value]="draft().plan"
            (valueChange)="setDraft({ plan: $event })"
          />
          <scs-select
            class="facet"
            testId="filter-length"
            [label]="'app.clinics.list.facets.length' | transloco"
            [placeholder]="'app.clinics.list.facets.allLengths' | transloco"
            [options]="lengthChoices()"
            [value]="draft().length"
            (valueChange)="setDraft({ length: $event })"
          />
          <scs-select
            class="facet"
            testId="filter-ends"
            [label]="'app.clinics.list.facets.endsWithin' | transloco"
            [placeholder]="'app.clinics.list.facets.anyTime' | transloco"
            [options]="endsChoices()"
            [value]="draft().endsWithin"
            (valueChange)="setDraft({ endsWithin: $event })"
          />
        </scs-filter-popover>
      </scs-list-toolbar>
      @if (applied().length > 0) {
        <scs-applied-chips
          [chips]="applied()"
          [clearAllLabel]="'app.clinics.list.clearAll' | transloco"
          [removeLabel]="'app.clinics.list.removeFilter' | transloco"
          (remove)="removeFilter($event)"
          (clearAll)="clearFilters()"
        />
      }
    }

    <scs-card>
      @switch (state()) {
        @case ('loading') {
          <scs-skeleton data-testid="clinics-loading" [rows]="8" [label]="'app.clinics.list.loading' | transloco" />
        }
        @case ('failed') {
          <scs-empty-state
            context="error"
            data-testid="clinics-error"
            [title]="'app.clinics.list.failedTitle' | transloco"
            [text]="'app.clinics.list.failedText' | transloco"
          >
            <button type="button" scsButton data-testid="clinics-retry" (click)="load()">
              {{ 'app.clinics.retry' | transloco }}
            </button>
          </scs-empty-state>
        }
        @default {
          @if (listIsEmpty()) {
            <scs-empty-state
              data-testid="clinics-empty"
              mark="search"
              [title]="'app.clinics.list.emptyTitle' | transloco"
              [text]="'app.clinics.list.emptyText' | transloco"
            >
              <button *appCan="can.clinicsEdit" type="button" scsButton data-testid="clinics-empty-add" (click)="go('/clinics/new')">
                {{ 'app.clinics.list.emptyAdd' | transloco }}
              </button>
            </scs-empty-state>
          } @else if ((page()?.items?.length ?? 0) === 0) {
            <scs-empty-state context="no-results" data-testid="clinics-no-results" [title]="'app.clinics.list.noneMatch' | transloco">
              <button type="button" scsButton="secondary" data-testid="clinics-clear" (click)="clearAll()">
                {{ 'app.clinics.list.clearFilters' | transloco }}
              </button>
            </scs-empty-state>
          } @else {
            <div>
              <table scsTable data-testid="clinics-table">
                <caption class="sr-only">{{ 'app.clinics.list.caption' | transloco }}</caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.clinics.list.columns.clinic' | transloco }}</th>
                    <th scope="col">{{ 'app.clinics.list.columns.addedBy' | transloco }}</th>
                    <th scope="col">{{ 'app.clinics.list.columns.plan' | transloco }}</th>
                    <th scope="col">{{ 'app.clinics.list.columns.status' | transloco }}</th>
                    <th scope="col">{{ 'app.clinics.list.columns.length' | transloco }}</th>
                    <th scope="col">
                      <button
                        scsSortHeader
                        data-testid="sort-ends"
                        [direction]="direction()"
                        [attr.aria-label]="'app.clinics.list.sortEnds' | transloco"
                        (sorted)="sortBy($event)"
                      >
                        {{ 'app.clinics.list.columns.ends' | transloco }}
                      </button>
                    </th>
                    @if (edits()) {
                      <th scope="col" data-kind="menu">{{ 'app.clinics.list.columns.actions' | transloco }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (row of page()?.items ?? []; track row.id) {
                    <tr
                      data-testid="clinic-row"
                      tabindex="0"
                      [attr.data-id]="row.id"
                      [attr.data-marked]="marked() === row.id"
                      (click)="open(row)"
                      (keydown.enter)="open(row)"
                    >
                      <td>
                        <span class="clinic">
                          <span class="mark" aria-hidden="true" data-testid="clinic-mark">{{ mark(row) }}</span>
                          <span>
                            <a class="name" data-testid="clinic-name" [routerLink]="['/clinics', row.id]" (click)="$event.stopPropagation()"
                              ><bdi>{{ name(row) }}</bdi></a
                            >
                            <span class="added" data-testid="clinic-added">{{ words('app.clinics.list.added', { at: when(row.createdAt) }) }}</span>
                          </span>
                        </span>
                      </td>
                      <td class="quiet" data-testid="clinic-added-by" [attr.data-label]="'app.clinics.list.columns.addedBy' | transloco">
                        <bdi>{{ row.createdBy?.name ?? ('app.clinics.none' | transloco) }}</bdi>
                      </td>
                      <td class="quiet" data-testid="clinic-plan" [attr.data-label]="'app.clinics.list.columns.plan' | transloco">
                        <bdi>{{ row.plan ? name(row.plan) : ('app.clinics.none' | transloco) }}</bdi>
                      </td>
                      <td [attr.data-label]="'app.clinics.list.columns.status' | transloco">
                        <scs-badge dot [intent]="row.status.tone" data-testid="clinic-status">{{ status(row) }}</scs-badge>
                      </td>
                      <td data-testid="clinic-length" [attr.data-label]="'app.clinics.list.columns.length' | transloco">
                        {{ row.length?.code === 'trial' ? trial(row.length?.days ?? 0) : ('app.clinics.none' | transloco) }}
                      </td>
                      <td data-testid="clinic-ends" [attr.data-label]="'app.clinics.list.columns.ends' | transloco">
                        {{ row.endsAt ? ends(row.endsAt) : '' }}
                      </td>
                      @if (edits()) {
                        <td data-kind="menu" (click)="$event.stopPropagation()" (keydown.enter)="$event.stopPropagation()">
                          <button
                            type="button"
                            class="scs-row-menu"
                            data-testid="row-menu"
                            [attr.aria-label]="words('app.clinics.list.menu', { name: name(row) })"
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
              [page]="current()"
              (pageChange)="toPage($event)"
              [pages]="pages()"
              [digits]="number"
              [previousLabel]="'app.clinics.list.previous' | transloco"
              [nextLabel]="'app.clinics.list.next' | transloco"
              [pagingLabel]="'app.clinics.list.paging' | transloco"
            />
          }
        }
      }
    </scs-card>

    <ng-template #rowMenu let-row="row">
      <div scsMenu data-testid="row-menu-items">
        <button type="button" scsMenuItem data-testid="row-edit" (chosen)="go('/clinics/' + row.id + '/edit')">
          <scs-icon name="menu-edit" [size]="16" />
          {{ 'app.clinics.list.edit' | transloco }}
        </button>
      </div>
    </ng-template>
  `,
})
export class ClinicsListPage {
  private readonly clinics = inject(ClinicsService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly translations = inject(TranslocoService);
  private readonly access = inject(OpsAccess);
  private readonly spoken = toSignal(this.translations.selectTranslation(), { initialValue: null });

  protected readonly can = OpsCan;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly page = signal<ClinicsPage | null>(null);
  protected readonly search = signal('');
  protected readonly filters = signal<Filters>(NO_FILTERS);
  protected readonly draft = signal<Filters>(NO_FILTERS);
  protected readonly direction = signal<ScsSortDirection>('none');
  protected readonly current = signal(1);
  protected readonly filterOpen = signal(false);
  protected readonly marked = signal<string | null>(null);
  private readonly now = Date.now();
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private asked = 0;

  /** Whoever may edit clinics, by the platform's answer and the session's codes; a row with nothing else has no menu (rule 37). */
  protected readonly edits = computed(() => (this.page()?.can.edit ?? false) && this.access.can(OpsCan.clinicsEdit));

  /** No clinic at all on the platform: not a search that found nothing. */
  protected readonly listIsEmpty = computed(() => this.state() === 'ready' && (this.page()?.cards.total ?? 0) === 0);

  protected readonly pages = computed(() => {
    const view = this.page();
    return view === null ? 1 : Math.max(1, Math.ceil(view.total / view.size));
  });

  protected readonly range = computed(() => {
    this.spoken();
    const view = this.page();
    if (view === null) {
      return '';
    }
    const from = view.total === 0 ? 0 : (view.page - 1) * view.size + 1;
    return this.translations.translate('app.clinics.list.count', {
      from: this.number(from),
      to: this.number(from + view.items.length - 1),
      total: this.number(view.total),
    });
  });

  protected readonly statusChoices = computed(() => this.page()?.facets.statuses ?? []);

  protected readonly planChoices = computed<ScsSelectOption[]>(() =>
    (this.page()?.facets.plans ?? []).map((plan) => ({ value: plan.id, label: this.name(plan) })),
  );

  protected readonly lengthChoices = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return (this.page()?.facets.lengths ?? []).map((length) => ({
      value: length,
      label: this.translations.translate(`app.clinics.list.lengths.${length}`),
    }));
  });

  protected readonly endsChoices = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return (this.page()?.facets.endsWithin ?? [7, 14, 30]).map((days) => ({
      value: String(days),
      label: this.translations.translate(`app.clinics.list.facets.within.${days}`),
    }));
  });

  /** One chip per facet in use: «الحالة: تجربة، نشطة». */
  protected readonly applied = computed<ScsAppliedChip[]>(() => {
    this.spoken();
    const chosen = this.filters();
    const t = (key: string) => this.translations.translate(key);
    const chips: ScsAppliedChip[] = [];
    if (chosen.statuses.length > 0) {
      chips.push({
        key: 'status',
        label: `${t('app.clinics.list.facets.status')}: ${chosen.statuses
          .map((s) => t(`app.clinics.list.statuses.${s}`))
          .join(this.language.language() === 'ar' ? '، ' : ', ')}`,
      });
    }
    if (chosen.plan !== '') {
      const plan = this.page()?.facets.plans.find((p) => p.id === chosen.plan);
      chips.push({ key: 'plan', label: `${t('app.clinics.list.facets.plan')}: ${plan ? this.name(plan) : ''}` });
    }
    if (chosen.length !== '') {
      chips.push({ key: 'length', label: `${t('app.clinics.list.facets.length')}: ${t(`app.clinics.list.lengths.${chosen.length}`)}` });
    }
    if (chosen.endsWithin !== '') {
      chips.push({ key: 'endsWithin', label: `${t('app.clinics.list.facets.endsWithin')}: ${t(`app.clinics.list.facets.within.${chosen.endsWithin}`)}` });
    }
    return chips;
  });

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    // Choices left unapplied are forgotten: the panel opens on the applied filters (FR-010).
    effect(() => {
      if (this.filterOpen()) {
        untracked(() => this.draft.set(this.filters()));
      }
    });
  }

  protected words(key: string, values?: Record<string, string>): string {
    this.spoken();
    return this.translations.translate(key, values);
  }

  protected noData(): string {
    return this.words('app.clinics.list.cards.noData');
  }

  protected name(clinic: { nameAr: string; nameEn: string }): string {
    return clinicName(clinic, this.language.language());
  }

  protected mark(row: ClinicRow): string {
    return listMark(row, this.language.language());
  }

  protected status(row: ClinicRow): string {
    return statusWord(row.status, this.language.language());
  }

  protected when(value: string): string {
    return opsCairoDateTime(value, this.language.language());
  }

  protected trial(days: number): string {
    this.spoken();
    return trialLength(this.translations, days, this.language.language());
  }

  protected ends(value: string): string {
    this.spoken();
    return endsIn(this.translations, value, this.now, this.language.language());
  }

  /** Asks the platform for the page the search, the filters, the order and the page number say. */
  protected async load(): Promise<void> {
    const ask = ++this.asked;
    if (this.page() === null) {
      this.state.set('loading');
    }
    const chosen = this.filters();
    try {
      const view = await this.clinics.list({
        search: this.search(),
        statuses: chosen.statuses,
        plan: chosen.plan || undefined,
        length: chosen.length || undefined,
        endsWithin: chosen.endsWithin === '' ? undefined : Number(chosen.endsWithin),
        sort: this.direction() === 'ascending' ? 'ends-asc' : this.direction() === 'descending' ? 'ends-desc' : 'new',
        page: this.current(),
        size: CLINICS_PAGE_SIZE,
      });
      if (ask === this.asked) {
        this.page.set(view);
        this.current.set(view.page);
        this.state.set('ready');
      }
    } catch {
      if (ask === this.asked) {
        this.page.set(null);
        this.state.set('failed');
      }
    }
  }

  protected searchFor(words: string): void {
    this.search.set(words);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.current.set(1);
      void this.load();
    }, SEARCH_PAUSE_MS);
  }

  protected sortBy(direction: ScsSortDirection): void {
    this.direction.set(direction);
    this.current.set(1);
    void this.load();
  }

  protected toPage(page: number): void {
    this.current.set(page);
    void this.load();
  }

  protected setDraft(change: Partial<Filters>): void {
    this.draft.update((draft) => ({ ...draft, ...change }));
  }

  protected toggleStatus(status: string, on: boolean): void {
    this.draft.update((draft) => ({
      ...draft,
      statuses: on ? [...draft.statuses.filter((s) => s !== status), status] : draft.statuses.filter((s) => s !== status),
    }));
  }

  protected resetDraft(): void {
    this.draft.set(NO_FILTERS);
  }

  protected applyFilters(): void {
    this.filters.set(this.draft());
    this.filterOpen.set(false);
    this.current.set(1);
    void this.load();
  }

  protected removeFilter(chip: ScsAppliedChip | string): void {
    const key = typeof chip === 'string' ? chip : chip.key;
    const cleared: Partial<Filters> =
      key === 'status' ? { statuses: [] } : key === 'plan' ? { plan: '' } : key === 'length' ? { length: '' } : { endsWithin: '' };
    this.filters.update((filters) => ({ ...filters, ...cleared }));
    this.draft.set(this.filters());
    this.current.set(1);
    void this.load();
  }

  protected clearFilters(): void {
    this.filters.set(NO_FILTERS);
    this.draft.set(NO_FILTERS);
    this.current.set(1);
    void this.load();
  }

  protected clearAll(): void {
    this.search.set('');
    this.clearFilters();
  }

  protected open(row: ClinicRow): void {
    this.go(`/clinics/${row.id}`);
  }

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }
}
