import { CdkMenuTrigger } from '@angular/cdk/menu';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
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
import { opsNumber } from '../i18n/ops-format';
import { TABLE_NARROW } from '../operators/table-narrow';
import { featureName, featureType, groupsWords, inPlans, rangeWords } from './feature-words';
import { FEATURE_GROUPS, FeatureRow, FeaturesService, FeaturesView } from './features.service';
import { ResumeFeatureDialog } from './resume-feature.dialog';
import { RetireFeatureDialog } from './retire-feature.dialog';
import { WhereUsedDialog } from './where-used.dialog';

/** Thirteen rows a page, as the frame draws them (FR-004). */
export const FEATURES_PAGE_SIZE = 13;

type StatusFacet = 'built' | 'upcoming' | 'retired';

/**
 * «كتالوج الميزات» (spec 012, User Story 1 and the row menu of User Story 4; frames
 * `2503:1262`/`2503:1114`): four cards, a search over both names and the key, a filter that
 * offers only what exists, and the table by group then order. One read answers all of it; search,
 * filter and paging work on what was read.
 */
@Component({
  selector: 'app-feature-catalogue-page',
  imports: [
    TranslocoPipe,
    CdkMenuTrigger,
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
    WhereUsedDialog,
    RetireFeatureDialog,
    ResumeFeatureDialog,
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
    .feature {
      display: block;
      min-inline-size: 200px;
    }
    .name {
      display: block;
      color: var(--scs-color-text-primary);
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    .key {
      display: block;
      margin-block-start: var(--scs-spacing-2);
      color: var(--scs-color-text-tertiary, var(--scs-color-text-secondary));
      font-size: var(--scs-text-label);
      white-space: nowrap;
      max-inline-size: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .quiet {
      color: var(--scs-color-text-secondary);
    }
    .marks {
      display: inline-flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-xs);
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
    <scs-page-header [title]="'app.plans.title' | transloco" [note]="'app.plans.note' | transloco">
      <button
        *appCan="can.billingManagePlans"
        type="button"
        scsButton
        data-testid="add-feature"
        (click)="go('/plans/features/new')"
      >
        <scs-icon name="plus" [size]="16" />
        <span>{{ 'app.plans.add' | transloco }}</span>
      </button>
    </scs-page-header>

    @if (view(); as page) {
      <div class="cards" data-testid="feature-cards">
        <scs-kpi-card
          data-testid="card-total"
          icon="layers"
          tone="brand"
          [label]="'app.plans.cards.total' | transloco"
          [value]="number(page.cards.total)"
          [note]="groups(page.cards.groups)"
        />
        <scs-kpi-card
          data-testid="card-built"
          icon="check-square"
          tone="success"
          [label]="'app.plans.cards.built' | transloco"
          [value]="number(page.cards.built)"
          [note]="'app.plans.cards.builtNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-upcoming"
          icon="barrier"
          tone="warning"
          [label]="'app.plans.cards.upcoming' | transloco"
          [value]="number(page.cards.upcoming)"
          [note]="'app.plans.cards.upcomingNote' | transloco"
        />
        <scs-kpi-card
          data-testid="card-sold"
          icon="tag"
          tone="success"
          [label]="'app.plans.cards.sold' | transloco"
          [value]="number(page.cards.soldInPlans)"
          [note]="'app.plans.cards.soldNote' | transloco: { count: number(page.cards.plans) }"
        />
      </div>
    } @else if (state() === 'loading') {
      <div class="cards" data-testid="feature-cards-loading">
        @for (card of [1, 2, 3, 4]; track card) {
          <scs-card><scs-skeleton [rows]="2" /></scs-card>
        }
      </div>
    }

    @if (state() === 'ready' && !catalogueIsEmpty()) {
      <scs-list-toolbar
        [search]="search()"
        (searchChange)="searchFor($event)"
        [(filterOpen)]="filterOpen"
        [searchPlaceholder]="'app.plans.list.search' | transloco"
        [clearLabel]="'app.plans.list.clearSearch' | transloco"
        [filterLabel]="'app.plans.list.filter' | transloco"
        [filterCount]="applied().length"
        [filterCountLabel]="
          applied().length === 0 ? '' : ('app.plans.list.applied.' + applied().length | transloco)
        "
      >
        <scs-filter-popover
          [heading]="'app.plans.list.filterTitle' | transloco"
          [closeLabel]="'app.plans.list.close' | transloco"
          [applyLabel]="'app.plans.list.apply' | transloco"
          [clearLabel]="'app.plans.list.reset' | transloco"
          (closed)="filterOpen.set(false)"
          (apply)="applyFilters()"
          (clear)="clearFilters()"
        >
          <!-- Three short lists, each offering only what some feature has (FR-006). -->
          <scs-select
            class="facet"
            testId="filter-group"
            [label]="'app.plans.list.filterGroup' | transloco"
            [placeholder]="'app.plans.list.filterAny' | transloco"
            [options]="groupOptions()"
            [(value)]="draftGroup"
          />
          <scs-select
            class="facet"
            testId="filter-type"
            [label]="'app.plans.list.filterType' | transloco"
            [placeholder]="'app.plans.list.filterAny' | transloco"
            [options]="typeOptions()"
            [(value)]="draftType"
          />
          <scs-select
            class="facet"
            testId="filter-status"
            [label]="'app.plans.list.filterStatus' | transloco"
            [placeholder]="'app.plans.list.filterAny' | transloco"
            [options]="statusOptions()"
            [(value)]="draftStatus"
          />
        </scs-filter-popover>
      </scs-list-toolbar>
      @if (applied().length > 0) {
        <scs-applied-chips
          [chips]="applied()"
          [clearAllLabel]="'app.plans.list.clearAll' | transloco"
          [removeLabel]="'app.plans.list.removeFilter' | transloco"
          (remove)="removeFilter($event)"
          (clearAll)="clearFilters()"
        />
      }
    }

    <scs-card>
      @switch (state()) {
        @case ('loading') {
          <scs-skeleton
            data-testid="features-loading"
            [rows]="8"
            [label]="'app.plans.list.loading' | transloco"
          />
        }
        @case ('failed') {
          <scs-empty-state
            context="error"
            data-testid="features-failed"
            [title]="'app.plans.list.failedTitle' | transloco"
            [text]="'app.plans.list.failedText' | transloco"
          >
            <button
              type="button"
              scsButton="secondary"
              data-testid="features-retry"
              (click)="load()"
            >
              {{ 'app.plans.retry' | transloco }}
            </button>
          </scs-empty-state>
        }
        @default {
          @if (catalogueIsEmpty()) {
            <scs-empty-state
              data-testid="features-empty"
              mark="layers"
              [title]="'app.plans.list.emptyTitle' | transloco"
              [text]="'app.plans.list.emptyText' | transloco"
            />
          } @else if (shown().length === 0) {
            <scs-empty-state
              context="no-results"
              data-testid="features-none"
              [title]="'app.plans.list.noneTitle' | transloco"
              [text]="'app.plans.list.noneText' | transloco"
            >
              <button
                type="button"
                scsButton="secondary"
                data-testid="features-clear"
                (click)="clearAll()"
              >
                {{ 'app.plans.list.clearAll' | transloco }}
              </button>
            </scs-empty-state>
          } @else {
            <div>
              <table scsTable data-testid="features-table">
                <caption class="sr-only">
                  {{
                    'app.plans.list.caption' | transloco
                  }}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'app.plans.list.column.feature' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.list.column.group' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.list.column.type' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.list.column.status' | transloco }}</th>
                    <th scope="col">{{ 'app.plans.list.column.plans' | transloco }}</th>
                    <th scope="col" data-kind="menu">
                      {{ 'app.plans.list.column.actions' | transloco }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of pageRows(); track row.key) {
                    <tr
                      data-testid="feature-row"
                      [attr.data-key]="row.key"
                      [attr.data-marked]="marked() === row.key"
                    >
                      <td>
                        <span class="feature">
                          <span class="name" data-testid="feature-name"
                            ><bdi>{{ name(row) }}</bdi></span
                          >
                          <span class="key" data-testid="feature-key" [attr.title]="row.key"
                            ><bdi dir="ltr">{{ row.key }}</bdi></span
                          >
                        </span>
                      </td>
                      <td
                        class="quiet"
                        data-testid="feature-group"
                        [attr.data-label]="'app.plans.list.column.group' | transloco"
                      >
                        {{ 'app.plans.group.' + row.group | transloco }}
                      </td>
                      <td
                        class="quiet"
                        data-testid="feature-type"
                        [attr.data-label]="'app.plans.list.column.type' | transloco"
                      >
                        <bdi>{{ type(row) }}</bdi>
                      </td>
                      <td [attr.data-label]="'app.plans.list.column.status' | transloco">
                        <span class="marks">
                          <scs-badge
                            [intent]="row.status === 'built' ? 'success' : 'neutral'"
                            data-testid="feature-status"
                            >{{ 'app.plans.status.' + row.status | transloco }}</scs-badge
                          >
                          @if (row.isRetired) {
                            <scs-badge intent="warning" data-testid="feature-retired">{{
                              'app.plans.status.retired' | transloco
                            }}</scs-badge>
                          }
                        </span>
                      </td>
                      <td
                        data-testid="feature-plans"
                        [attr.data-label]="'app.plans.list.column.plans' | transloco"
                      >
                        {{ plansOf(row) }}
                      </td>
                      <td data-kind="menu">
                        <button
                          type="button"
                          class="scs-row-menu"
                          data-testid="row-menu"
                          [attr.aria-label]="'app.plans.list.menu' | transloco: { name: name(row) }"
                          [cdkMenuTriggerFor]="rowMenu"
                          [cdkMenuTriggerData]="{ row }"
                          (cdkMenuOpened)="marked.set(row.key)"
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
              [count]="range()"
              [page]="page()"
              (pageChange)="page.set($event)"
              [pages]="pages()"
              [digits]="number"
              [previousLabel]="'app.plans.list.previous' | transloco"
              [nextLabel]="'app.plans.list.next' | transloco"
              [pagingLabel]="'app.plans.list.paging' | transloco"
            />
          }
        }
      }
    </scs-card>

    <ng-template #rowMenu let-opened="row">
      <!-- The menu is drawn from the row as the catalogue holds it now, not as it was when first opened. -->
      @if (latest(opened); as row) {
        <div scsMenu data-testid="row-menu-items">
          @if (manages()) {
            <button
              type="button"
              scsMenuItem
              data-testid="row-edit"
              (chosen)="go('/plans/features/' + row.key + '/edit')"
            >
              <scs-icon name="menu-edit" [size]="16" />
              {{ 'app.plans.list.edit' | transloco }}
            </button>
          }
          <button type="button" scsMenuItem data-testid="row-where" (chosen)="whereUsed.set(row)">
            <scs-icon name="menu-view" [size]="16" />
            {{ 'app.plans.list.where' | transloco }}
          </button>
          @if (manages() && row.isSold) {
            <div class="scs-menu-separator" role="separator"></div>
            @if (row.isRetired) {
              <button
                type="button"
                scsMenuItem
                data-testid="row-resume"
                (chosen)="resuming.set(row)"
              >
                <scs-icon name="menu-enable" [size]="16" />
                {{ 'app.plans.list.resume' | transloco }}
              </button>
            } @else {
              <button
                type="button"
                scsMenuItem
                danger
                data-testid="row-retire"
                (chosen)="retiring.set(row)"
              >
                <scs-icon name="menu-disable" [size]="16" />
                {{ 'app.plans.list.retire' | transloco }}
              </button>
            }
          }
        </div>
      }
    </ng-template>

    <app-where-used [feature]="whereUsed()" (closed)="closeDialog(whereUsed)" />
    <app-retire-feature
      [feature]="retiring()"
      (closed)="closeDialog(retiring)"
      (done)="closeDialog(retiring, true)"
    />
    <app-resume-feature
      [feature]="resuming()"
      (closed)="closeDialog(resuming)"
      (done)="closeDialog(resuming, true)"
    />
  `,
})
export class FeatureCataloguePage {
  private readonly features = inject(FeaturesService);
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
  protected readonly view = signal<FeaturesView | null>(null);

  protected readonly search = signal('');
  protected readonly groupFilter = signal('');
  protected readonly typeFilter = signal('');
  protected readonly statusFilter = signal('');
  protected readonly page = signal(1);

  protected readonly filterOpen = signal(false);
  protected readonly draftGroup = signal('');
  protected readonly draftType = signal('');
  protected readonly draftStatus = signal('');
  protected readonly marked = signal<string | null>(null);

  protected readonly whereUsed = signal<FeatureRow | null>(null);
  protected readonly retiring = signal<FeatureRow | null>(null);
  protected readonly resuming = signal<FeatureRow | null>(null);

  /** Whoever manages plans: what the platform said with the catalogue, and the session's codes. */
  protected readonly manages = computed(
    () => (this.view()?.can.manage ?? false) && this.access.can(OpsCan.billingManagePlans),
  );

  private readonly all = computed(() => this.view()?.items ?? []);

  protected readonly catalogueIsEmpty = computed(
    () => this.state() === 'ready' && this.all().length === 0,
  );

  /** What the search and the filters leave, in the catalogue's order. */
  protected readonly shown = computed(() => {
    const words = this.search().trim().toLocaleLowerCase();
    return this.all().filter(
      (row) =>
        (words === '' ||
          [row.nameAr, row.nameEn, row.key].some((text) =>
            text.toLocaleLowerCase().includes(words),
          )) &&
        (this.groupFilter() === '' || row.group === this.groupFilter()) &&
        (this.typeFilter() === '' || row.kind === this.typeFilter()) &&
        (this.statusFilter() === '' ||
          statusesOf(row).includes(this.statusFilter() as StatusFacet)),
    );
  });

  protected readonly pages = computed(() =>
    Math.max(1, Math.ceil(this.shown().length / FEATURES_PAGE_SIZE)),
  );
  protected readonly pageRows = computed(() => {
    const page = Math.min(this.page(), this.pages());
    return this.shown().slice((page - 1) * FEATURES_PAGE_SIZE, page * FEATURES_PAGE_SIZE);
  });
  protected readonly range = computed(() => {
    this.spoken();
    const total = this.shown().length;
    const from =
      total === 0 ? 0 : (Math.min(this.page(), this.pages()) - 1) * FEATURES_PAGE_SIZE + 1;
    return rangeWords(
      this.words,
      from,
      from + this.pageRows().length - 1,
      total,
      this.language.language(),
    );
  });

  protected readonly groupOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const present = new Set(this.all().map((row) => row.group));
    return FEATURE_GROUPS.filter((group) => present.has(group)).map((group) => ({
      value: group,
      label: this.words.translate(`app.plans.group.${group}`),
    }));
  });

  protected readonly typeOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const present = new Set(this.all().map((row) => row.kind));
    return (['switch', 'limit'] as const)
      .filter((kind) => present.has(kind))
      .map((kind) => ({ value: kind, label: this.words.translate(`app.plans.type.${kind}`) }));
  });

  protected readonly statusOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const present = new Set(this.all().flatMap(statusesOf));
    return (['built', 'upcoming', 'retired'] as const)
      .filter((status) => present.has(status))
      .map((status) => ({
        value: status,
        label: this.words.translate(`app.plans.status.${status}`),
      }));
  });

  /** One chip per filter: «المجموعة: السريرية». */
  protected readonly applied = computed<ScsAppliedChip[]>(() => {
    this.spoken();
    const chip = (key: string, label: string, options: ScsSelectOption[], value: string) => ({
      key,
      label: `${this.words.translate(label)}: ${options.find((option) => option.value === value)?.label ?? value}`,
    });
    const chips: ScsAppliedChip[] = [];
    if (this.groupFilter()) {
      chips.push(
        chip('group', 'app.plans.list.filterGroup', this.groupOptions(), this.groupFilter()),
      );
    }
    if (this.typeFilter()) {
      chips.push(chip('type', 'app.plans.list.filterType', this.typeOptions(), this.typeFilter()));
    }
    if (this.statusFilter()) {
      chips.push(
        chip('status', 'app.plans.list.filterStatus', this.statusOptions(), this.statusFilter()),
      );
    }
    return chips;
  });

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
  }

  /** The row of this key as last read: a retire or a sell again changes it under an open menu's trigger. */
  protected latest(row: FeatureRow): FeatureRow {
    return this.all().find((known) => known.key === row.key) ?? row;
  }

  protected name(row: FeatureRow): string {
    return featureName(row, this.language.language());
  }

  protected type(row: FeatureRow): string {
    this.spoken();
    return featureType(this.words, row, this.language.language());
  }

  protected plansOf(row: FeatureRow): string {
    this.spoken();
    return inPlans(this.words, row.plans, this.language.language());
  }

  protected groups(count: number): string {
    this.spoken();
    return groupsWords(this.words, count, this.language.language());
  }

  /** The whole catalogue and its cards in one answer, so they never disagree. */
  protected async load(): Promise<void> {
    if (this.view() === null) {
      this.state.set('loading');
    }
    try {
      this.view.set(await this.features.list());
      this.state.set('ready');
    } catch {
      // A reload that fails after a change keeps what was last read, and says so (FR-008a).
      if (this.view() === null) {
        this.state.set('failed');
      } else {
        this.toasts.show(this.words.translate('app.plans.list.failedTitle'), '', 'danger');
      }
    }
  }

  /**
   * Closes a dialog however it was closed — Escape, ×, its own button, or after a change — and
   * gives the focus back to the menu of the row it was opened from (contracts/catalogue-screens.md §6).
   */
  protected async closeDialog(
    open: WritableSignal<FeatureRow | null>,
    reload = false,
  ): Promise<void> {
    const key = open()?.key;
    open.set(null);
    if (reload) {
      await this.load();
    }
    if (key !== undefined) {
      setTimeout(() =>
        this.host.nativeElement
          .querySelector<HTMLElement>(
            `[data-testid="feature-row"][data-key="${CSS.escape(key)}"] [data-testid="row-menu"]`,
          )
          ?.focus(),
      );
    }
  }

  protected searchFor(words: string): void {
    this.search.set(words);
    this.page.set(1);
  }

  protected applyFilters(): void {
    this.groupFilter.set(this.draftGroup());
    this.typeFilter.set(this.draftType());
    this.statusFilter.set(this.draftStatus());
    this.page.set(1);
    this.filterOpen.set(false);
  }

  /** One chip's ×: that filter goes, the others stay. */
  protected removeFilter(key: string): void {
    this.draftGroup.set(key === 'group' ? '' : this.groupFilter());
    this.draftType.set(key === 'type' ? '' : this.typeFilter());
    this.draftStatus.set(key === 'status' ? '' : this.statusFilter());
    this.applyFilters();
  }

  protected clearFilters(): void {
    this.draftGroup.set('');
    this.draftType.set('');
    this.draftStatus.set('');
    this.applyFilters();
  }

  protected clearAll(): void {
    this.search.set('');
    this.clearFilters();
  }

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }
}

/** The statuses a row answers to in the filter: built or upcoming, and retired as well. */
function statusesOf(row: FeatureRow): StatusFacet[] {
  return row.isRetired ? [row.status, 'retired'] : [row.status];
}
