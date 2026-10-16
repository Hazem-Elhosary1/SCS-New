import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  ScsAppliedChip,
  ScsAppliedChipsComponent,
  ScsBadgeComponent,
  ScsBadgeIntent,
  ScsCheckboxComponent,
  ScsFilterPopoverComponent,
  ScsListToolbarComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
} from 'ui';
import { GalleryState } from '../gallery-state';

type Status = 'active' | 'trial' | 'suspended';
type Plan = 'starter' | 'professional';

interface Clinic {
  readonly name: { ar: string; en: string };
  readonly status: Status;
  readonly plan: Plan;
}

/** The example's clinics: no "trial" one, so the panel offers only what some row has (CL-SH-050). */
const CLINICS: readonly Clinic[] = [
  { name: { ar: 'عيادة النور', en: 'Al Noor Clinic' }, status: 'active', plan: 'professional' },
  { name: { ar: 'مركز النيل للأسنان', en: 'Nile Dental Center' }, status: 'active', plan: 'starter' },
  { name: { ar: 'عيادة سيتي كير', en: 'City Care Clinic' }, status: 'suspended', plan: 'professional' },
  { name: { ar: 'مركز طب الأسرة', en: 'Family Med Center' }, status: 'active', plan: 'starter' },
];

const INTENTS: Record<Status, ScsBadgeIntent> = { active: 'success', trial: 'info', suspended: 'neutral' };

/**
 * Pattern — Search & Filter (`111:2`; CL-SH-046 … CL-SH-050), composed from the shared parts: the
 * toolbar with search at the reading start and the filter at the other end, its count on the button;
 * the applied filters as chips with «مسح الكل»; the panel, opening downward, with only the values that
 * have rows, its loading values, and "nothing to filter". The working example filters four clinics.
 */
@Component({
  selector: 'app-gallery-pattern-search-filter-section',
  imports: [
    ScsListToolbarComponent,
    ScsFilterPopoverComponent,
    ScsAppliedChipsComponent,
    ScsCheckboxComponent,
    ScsSelectComponent,
    ScsSkeletonComponent,
    ScsBadgeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-spacing-lg);
    }
    /* On a phone every block keeps to the window: what is wider scrolls inside its own part. */
    :host > *,
    :host > * > * {
      min-inline-size: 0;
      max-inline-size: 100%;
    }
    .block {
      display: grid;
      gap: var(--scs-spacing-sm);
      min-inline-size: 0;
    }
    .caption {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: var(--scs-spacing-sm);
    }
    .toolbar scs-list-toolbar {
      flex: 1 1 360px;
      min-inline-size: 0;
    }
    .toolbar scs-select {
      flex: 0 1 220px;
    }
    /* The pattern draws the sort with no name above it: the name stays for a screen reader. */
    .toolbar scs-select ::ng-deep .name {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    .panels {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: var(--scs-spacing-md);
    }
    .panels scs-filter-popover {
      inline-size: min(320px, 100%);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-shadow-subtle);
    }
    .facet {
      display: grid;
      gap: var(--scs-spacing-xs);
    }
    .facet-name {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .nothing {
      margin: 0;
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
    }
    .rows {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    .rows li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border-block-end: 1px solid var(--scs-color-border-subtle, var(--scs-color-border-default));
    }
    .rows li:last-child {
      border-block-end: 0;
    }
    .plan {
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
    }
  `,
  template: `
    <div class="block" data-testid="specimen-pattern-search-filter/pattern/toolbar">
      <p class="caption">{{ words().toolbarCaption }}</p>
      <div class="toolbar">
        <scs-list-toolbar
          [searchPlaceholder]="words().search"
          [clearLabel]="words().clear"
          [filterLabel]="words().filter"
          [filterCount]="2"
          [filterCountLabel]="words().applied(2)"
        />
        <scs-select [label]="words().sort" [options]="sorts()" value="newest" />
      </div>
    </div>

    <div class="block" data-testid="specimen-pattern-search-filter/pattern/applied">
      <p class="caption">{{ words().appliedCaption }}</p>
      <scs-applied-chips [chips]="stillChips()" [clearAllLabel]="words().clearAll" [removeLabel]="words().remove" />
    </div>

    <div class="block">
      <p class="caption">{{ words().panelCaption }}</p>
      <div class="panels">
        <scs-filter-popover
          data-testid="specimen-pattern-search-filter/pattern/panel"
          [heading]="words().panelTitle"
          [applyLabel]="words().apply"
          [clearLabel]="words().reset"
          [closeLabel]="words().close"
        >
          <div class="facet">
            <p class="facet-name">{{ words().status }}</p>
            @for (status of statuses; track status) {
              <scs-checkbox [label]="words().statusOf[status]" [checked]="status === 'active'" />
            }
          </div>
          <scs-select [label]="words().plan" [options]="plans()" value="professional" />
        </scs-filter-popover>

        <scs-filter-popover
          data-testid="specimen-pattern-search-filter/pattern/panel-loading"
          [heading]="words().panelTitle"
          [applyLabel]="words().apply"
          [clearLabel]="words().reset"
          [closeLabel]="words().close"
        >
          <scs-skeleton [rows]="3" [label]="words().loading" />
        </scs-filter-popover>

        <scs-filter-popover
          data-testid="specimen-pattern-search-filter/pattern/panel-nothing"
          [heading]="words().panelTitle"
          [applyLabel]="words().apply"
          [clearLabel]="words().reset"
          [closeLabel]="words().close"
        >
          <p class="nothing">{{ words().nothing }}</p>
        </scs-filter-popover>
      </div>
    </div>

    <div class="block" data-testid="live-pattern-search-filter">
      <p class="caption">{{ words().live }}</p>
      <scs-list-toolbar
        [searchPlaceholder]="words().search"
        [clearLabel]="words().clear"
        [filterLabel]="words().filter"
        [filterCount]="applied().length"
        [filterCountLabel]="applied().length ? words().applied(applied().length) : ''"
        [(search)]="search"
        [(filterOpen)]="filterOpen"
      >
        <scs-filter-popover
          [heading]="words().panelTitle"
          [applyLabel]="words().apply"
          [clearLabel]="words().reset"
          [closeLabel]="words().close"
          (closed)="filterOpen.set(false)"
          (apply)="applyFilters()"
          (clear)="clearFilters()"
        >
          <div class="facet">
            <p class="facet-name">{{ words().status }}</p>
            <!-- Only the statuses some clinic has: there is no "trial" clinic, so it is not offered. -->
            @for (status of offeredStatuses(); track status) {
              <scs-checkbox
                [testId]="'live-filter-' + status"
                [label]="words().statusOf[status]"
                [checked]="draftStatuses().includes(status)"
                (checkedChange)="toggleDraft(status, $event)"
              />
            }
          </div>
          <scs-select testId="live-filter-plan" [label]="words().plan" [options]="plans()" [placeholder]="words().any" [(value)]="draftPlan" />
        </scs-filter-popover>
      </scs-list-toolbar>
      <scs-applied-chips [chips]="applied()" [clearAllLabel]="words().clearAll" [removeLabel]="words().remove" (remove)="removeChip($event)" (clearAll)="clearFilters()" />
      <ul class="rows" data-testid="live-pattern-rows">
        @for (clinic of shown(); track clinic.name.en) {
          <li data-testid="live-pattern-row">
            <span>{{ clinic.name[language()] }}</span>
            <span class="plan">{{ words().planOf[clinic.plan] }}</span>
            <scs-badge [intent]="intents[clinic.status]">{{ words().statusOf[clinic.status] }}</scs-badge>
          </li>
        } @empty {
          <li class="nothing" data-testid="live-pattern-none">{{ words().none }}</li>
        }
      </ul>
    </div>
  `,
})
export class PatternSearchFilterSection {
  private readonly state = inject(GalleryState);
  protected readonly language = this.state.language;

  protected readonly statuses: readonly Status[] = ['active', 'trial', 'suspended'];
  protected readonly intents = INTENTS;

  protected readonly search = signal('');
  protected readonly filterOpen = signal(false);
  protected readonly statusFilter = signal<readonly Status[]>([]);
  protected readonly planFilter = signal('');
  protected readonly draftStatuses = signal<readonly Status[]>([]);
  protected readonly draftPlan = signal('');

  protected readonly words = computed(() =>
    this.language() === 'ar'
      ? {
          toolbarCaption: 'شريط الأدوات — بحث + فلترة + ترتيب',
          appliedCaption: 'الفلاتر المطبّقة',
          panelCaption: 'لوحة الفلترة — مفتوحة · أثناء تحميل القيم · لا توجد قيم بعد',
          live: 'مثال يعمل',
          search: 'ابحث عن عيادة أو تذكرة أو مستخدم',
          clear: 'مسح البحث',
          filter: 'فلترة',
          sort: 'الترتيب',
          panelTitle: 'تصفية النتائج',
          apply: 'تطبيق الفلاتر',
          reset: 'إعادة تعيين',
          close: 'إغلاق',
          clearAll: 'مسح الكل',
          remove: 'إزالة',
          status: 'الحالة',
          plan: 'الخطة',
          any: 'الكل',
          loading: 'جارٍ تحميل القيم',
          nothing: 'لا توجد قيم للفلترة بعد.',
          none: 'لا توجد عيادات مطابقة لبحثك أو فلاترك.',
          applied: (count: number) => (count === 1 ? 'فلتر واحد مطبّق' : count === 2 ? 'فلتران مطبّقان' : `${count} فلاتر مطبّقة`),
          statusOf: { active: 'نشطة', trial: 'تجريبية', suspended: 'موقوفة' } as Record<Status, string>,
          planOf: { starter: 'المبتدئة', professional: 'الاحترافية' } as Record<Plan, string>,
          newest: 'الأحدث أولاً',
          oldest: 'الأقدم أولاً',
        }
      : {
          toolbarCaption: 'Toolbar — Search + Filter + Sort',
          appliedCaption: 'Applied filters',
          panelCaption: 'Filter panel — open · loading values · nothing to filter yet',
          live: 'Working example',
          search: 'Search for a clinic, ticket, or user',
          clear: 'Clear search',
          filter: 'Filter',
          sort: 'Sort',
          panelTitle: 'Filter results',
          apply: 'Apply filters',
          reset: 'Reset',
          close: 'Close',
          clearAll: 'Clear all',
          remove: 'Remove',
          status: 'Status',
          plan: 'Plan',
          any: 'All',
          loading: 'Loading values',
          nothing: 'No values to filter by yet.',
          none: 'No clinics match your search or filters.',
          applied: (count: number) => (count === 1 ? '1 filter applied' : `${count} filters applied`),
          statusOf: { active: 'Active', trial: 'Trial', suspended: 'Suspended' } as Record<Status, string>,
          planOf: { starter: 'Starter', professional: 'Professional' } as Record<Plan, string>,
          newest: 'Newest first',
          oldest: 'Oldest first',
        },
  );

  protected readonly sorts = computed<ScsSelectOption[]>(() => [
    { value: 'newest', label: this.words().newest },
    { value: 'oldest', label: this.words().oldest },
  ]);
  protected readonly plans = computed<ScsSelectOption[]>(() =>
    (['starter', 'professional'] as const).map((plan) => ({ value: plan, label: this.words().planOf[plan] })),
  );

  protected readonly stillChips = computed<ScsAppliedChip[]>(() => [
    { key: 'status', label: `${this.words().status}: ${this.words().statusOf.active}` },
    { key: 'plan', label: `${this.words().plan}: ${this.words().planOf.professional}` },
  ]);

  /** Only the statuses some clinic has (CL-SH-050). */
  protected readonly offeredStatuses = computed(() => this.statuses.filter((status) => CLINICS.some((clinic) => clinic.status === status)));

  protected readonly applied = computed<ScsAppliedChip[]>(() => [
    ...this.statusFilter().map((status) => ({ key: `status:${status}`, label: `${this.words().status}: ${this.words().statusOf[status]}` })),
    ...(this.planFilter() ? [{ key: 'plan', label: `${this.words().plan}: ${this.words().planOf[this.planFilter() as Plan]}` }] : []),
  ]);

  protected readonly shown = computed(() => {
    const words = this.search().trim().toLowerCase();
    return CLINICS.filter(
      (clinic) =>
        (words === '' || clinic.name.ar.includes(words) || clinic.name.en.toLowerCase().includes(words)) &&
        (this.statusFilter().length === 0 || this.statusFilter().includes(clinic.status)) &&
        (this.planFilter() === '' || clinic.plan === this.planFilter()),
    );
  });

  protected toggleDraft(status: Status, on: boolean): void {
    this.draftStatuses.update((all) => (on ? [...all.filter((one) => one !== status), status] : all.filter((one) => one !== status)));
  }

  protected applyFilters(): void {
    this.statusFilter.set(this.draftStatuses());
    this.planFilter.set(this.draftPlan());
    this.filterOpen.set(false);
  }

  protected clearFilters(): void {
    this.draftStatuses.set([]);
    this.draftPlan.set('');
    this.applyFilters();
  }

  /** One chip's ×: that filter goes, the others stay. */
  protected removeChip(key: string): void {
    if (key === 'plan') {
      this.planFilter.set('');
      this.draftPlan.set('');
    } else {
      this.statusFilter.update((all) => all.filter((status) => `status:${status}` !== key));
      this.draftStatuses.set(this.statusFilter());
    }
  }
}
