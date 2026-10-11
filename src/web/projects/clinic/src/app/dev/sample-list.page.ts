import { CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService, OnlineService } from 'core';
import {
  ScsAppliedChip,
  ScsAppliedChipsComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsConfirmComponent,
  ScsEmptyStateComponent,
  ScsFilterPopoverComponent,
  ScsIconComponent,
  ScsListToolbarComponent,
  ScsMenuDirective,
  ScsMenuItemDirective,
  ScsPageHeaderComponent,
  ScsSkeletonComponent,
  ScsTableDirective,
  ScsTableFooterComponent,
  ScsTabsComponent,
  ScsWriteDirective,
  ToastService,
} from 'ui';

type Kind = 'checkup' | 'followup' | 'procedure';

interface SampleRow {
  readonly id: number;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly kind: Kind;
  readonly amount: number;
  readonly archived: boolean;
}

const KINDS: readonly Kind[] = ['checkup', 'followup', 'procedure'];
const PAGE_SIZE = 10;

const arabic = new Intl.NumberFormat('ar-EG-u-nu-arab', { useGrouping: false });

/** Fifty-two made-up rows; nothing here is anybody's data. */
function madeUpRows(): SampleRow[] {
  return Array.from({ length: 52 }, (_, index) => {
    const id = index + 1;
    return {
      id,
      nameAr: `سجل تجريبي ${arabic.format(id)}`,
      nameEn: `Sample record ${id}`,
      kind: KINDS[index % KINDS.length],
      amount: 150 + ((index * 35) % 400),
      archived: id % 9 === 0,
    };
  });
}

/**
 * A list with made-up rows, outside production only (spec 006, FR-042). It exists so that every
 * rule of specs/006-app-shell/contracts/list-contract.md can be seen working before the first
 * long list arrives: search, filtering, chips, tabs, paging, the row menu, the states, the
 * confirmation and the passing notice. `?state=loading|empty|error` shows a state;
 * `?as=viewer` shows it to a person with the view alone.
 */
@Component({
  selector: 'app-sample-list-page',
  imports: [
    TranslocoPipe,
    CdkMenuTrigger,
    ScsPageHeaderComponent,
    ScsCardComponent,
    ScsTabsComponent,
    ScsListToolbarComponent,
    ScsAppliedChipsComponent,
    ScsTableDirective,
    ScsTableFooterComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsConfirmComponent,
    ScsFilterPopoverComponent,
    ScsButtonDirective,
    ScsWriteDirective,
    ScsMenuDirective,
    ScsMenuItemDirective,
    ScsIconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .facet {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      border: 0;
    }
    .facet legend {
      padding: 0 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
    }
    .facet label,
    .confirm label {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
    }
    .confirm p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
    }
    .opened {
      margin: 0 0 var(--scs-spacing-12);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    <scs-page-header
      [title]="'app.sample.title' | transloco"
      [note]="state() === 'error' ? '—' : ('app.sample.note' | transloco: { total: digits(rows().length) })"
    >
      @if (state() === 'ready' && rows().length > 0) {
        <button type="button" scsButton="secondary" data-testid="sample-export" (click)="export()">
          {{ 'app.sample.export' | transloco }}
        </button>
      }
      @if (!viewer()) {
        <button
          type="button"
          scsButton
          data-testid="sample-add"
          [scsWrite]="online.online()"
          [scsWriteReason]="'app.shell.offlineReason' | transloco"
          (click)="add()"
        >
          {{ 'app.sample.add' | transloco }}
        </button>
      }
    </scs-page-header>

    @if (state() !== 'empty') {
      <scs-tabs [tabs]="tabs()" [active]="tab()" (activeChange)="chooseTab($event)" />
    }

    <scs-card data-testid="sample-card" [heading]="'app.sample.heading' | transloco">
      @if (state() === 'loading') {
        <scs-skeleton data-testid="sample-loading" [rows]="8" [label]="'ui.state.loading.title' | transloco" />
      } @else if (state() === 'empty' || rows().length === 0) {
        <scs-empty-state
          data-testid="sample-empty"
          [title]="'app.sample.emptyTitle' | transloco"
          [text]="'app.sample.emptyText' | transloco"
        />
      } @else {
        <scs-list-toolbar
          [search]="search()"
          (searchChange)="searchFor($event)"
          [(filterOpen)]="filterOpen"
          [searchPlaceholder]="'app.sample.searchPlaceholder' | transloco"
          [clearLabel]="'app.sample.clearSearch' | transloco"
          [filterLabel]="'app.sample.filter' | transloco"
        >
          <scs-filter-popover
            [heading]="'app.sample.filterTitle' | transloco"
            [closeLabel]="'ui.dialog.close' | transloco"
            (closed)="filterOpen.set(false)"
            [applyLabel]="'app.sample.apply' | transloco"
            [clearLabel]="'app.sample.clearAll' | transloco"
            (apply)="applyFilter()"
            (clear)="clearAll()"
          >
            <fieldset class="facet">
              <legend>{{ 'app.sample.column.kind' | transloco }}</legend>
              @for (kind of kinds; track kind) {
                <label>
                  <input
                    type="radio"
                    name="kind"
                    [attr.data-testid]="'filter-' + kind"
                    [checked]="draftKind() === kind"
                    (change)="draftKind.set(kind)"
                  />
                  {{ 'app.sample.kind.' + kind | transloco }}
                </label>
              }
            </fieldset>
          </scs-filter-popover>
        </scs-list-toolbar>

        <scs-applied-chips
          [chips]="chips()"
          [clearAllLabel]="'app.sample.clearAll' | transloco"
          [removeLabel]="'app.sample.remove' | transloco"
          (remove)="kind.set(null)"
          (clearAll)="clearAll()"
        />

        @if (opened(); as record) {
          <p class="opened" data-testid="opened-record">
            {{ 'app.sample.opened' | transloco: { name: nameOf(record) } }}
          </p>
        }

        @if (state() === 'error') {
          <scs-empty-state
            context="error"
            data-testid="sample-error"
            [title]="'app.sample.errorTitle' | transloco"
            [text]="'app.sample.errorText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="sample-retry" (click)="retried.set(true)">
              {{ 'ui.state.error.retry' | transloco }}
            </button>
          </scs-empty-state>
        } @else if (matching().length === 0) {
          <scs-empty-state
            context="no-results"
            data-testid="sample-no-results"
            [title]="'app.sample.noResultsTitle' | transloco"
            [text]="'app.sample.noResultsText' | transloco"
          />
        } @else {
          <table scsTable>
            <thead>
              <tr>
                <th scope="col">{{ 'app.sample.column.name' | transloco }}</th>
                <th scope="col">{{ 'app.sample.column.kind' | transloco }}</th>
                <th scope="col" data-kind="number">{{ 'app.sample.column.amount' | transloco }}</th>
                @if (!viewer()) {
                  <th scope="col" data-kind="menu">
                    <span class="sr-only">{{ 'app.sample.column.actions' | transloco }}</span>
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of shown(); track row.id) {
                <tr
                  tabindex="0"
                  data-testid="sample-row"
                  data-opens="true"
                  [attr.data-marked]="marked() === row.id"
                  (click)="opened.set(row)"
                  (keydown.enter)="opened.set(row)"
                >
                  <td data-testid="sample-name">{{ nameOf(row) }}</td>
                  <td>{{ 'app.sample.kind.' + row.kind | transloco }}</td>
                  <td data-kind="number">{{ digits(row.amount) }}</td>
                  @if (!viewer()) {
                    <td data-kind="menu">
                      <button
                        type="button"
                        class="scs-row-menu"
                        data-testid="row-menu"
                        [attr.aria-label]="'app.sample.column.actions' | transloco"
                        [cdkMenuTriggerFor]="rowMenu"
                        [cdkMenuTriggerData]="{ row }"
                        (cdkMenuOpened)="marked.set(row.id)"
                        (cdkMenuClosed)="unmark(row.id)"
                        (click)="$event.stopPropagation()"
                        (keydown.enter)="$event.stopPropagation()"
                      >
                        <scs-icon name="more" [size]="18" />
                      </button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>

          <scs-table-footer
            [count]="
              'app.sample.count'
                | transloco: { from: digits(from()), to: digits(to()), total: digits(matching().length) }
            "
            [page]="page()"
            (pageChange)="page.set($event)"
            [pages]="pages()"
            [digits]="digits"
            [previousLabel]="'app.sample.previous' | transloco"
            [nextLabel]="'app.sample.next' | transloco"
            [pagingLabel]="'app.sample.paging' | transloco"
          />
        }
      }
    </scs-card>

    <ng-template #rowMenu let-row="row">
      <div scsMenu data-testid="row-menu-items">
        <button
          type="button"
          scsMenuItem
          data-testid="row-archive"
          [scsWrite]="online.online()"
          (chosen)="archive(row)"
        >
          <scs-icon name="inbox" [size]="18" />
          {{ (row.archived ? 'app.sample.restore' : 'app.sample.archive') | transloco }}
        </button>
        <button
          type="button"
          scsMenuItem
          danger
          data-testid="row-delete"
          [scsWrite]="online.online()"
          (chosen)="askDelete(row)"
        >
          <scs-icon name="trash" [size]="18" />
          {{ 'app.sample.delete' | transloco }}
        </button>
      </div>
    </ng-template>

    <scs-confirm
      danger
      [heading]="'app.sample.confirm.title' | transloco"
      [actionLabel]="'app.sample.confirm.action' | transloco"
      [cancelLabel]="'app.sample.confirm.cancel' | transloco"
      [ready]="understood()"
      [open]="deleting() !== null"
      (openChange)="$event || cancelDelete()"
      (confirmed)="remove()"
    >
      @if (deleting(); as row) {
        <div class="confirm">
          <p data-testid="confirm-text">{{ 'app.sample.confirm.text' | transloco: { name: nameOf(row) } }}</p>
          <label>
            <input
              type="checkbox"
              data-testid="confirm-understood"
              [checked]="understood()"
              (change)="understood.set(!understood())"
            />
            {{ 'app.sample.confirm.understood' | transloco }}
          </label>
        </div>
      }
    </scs-confirm>
  `,
})
export class SampleListPage {
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly toasts = inject(ToastService);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap);

  protected readonly online = inject(OnlineService);
  protected readonly kinds = KINDS;

  protected readonly rows = signal<readonly SampleRow[]>(madeUpRows());
  protected readonly retried = signal(false);

  protected readonly tab = signal('all');
  protected readonly search = signal('');
  protected readonly kind = signal<Kind | null>(null);
  protected readonly draftKind = signal<Kind | null>(null);
  protected readonly filterOpen = signal(false);
  protected readonly page = signal(1);

  protected readonly opened = signal<SampleRow | null>(null);
  protected readonly marked = signal<number | null>(null);
  protected readonly deleting = signal<SampleRow | null>(null);
  protected readonly understood = signal(false);

  protected readonly viewer = computed(() => this.query()?.get('as') === 'viewer');

  protected readonly state = computed<'ready' | 'loading' | 'empty' | 'error'>(() => {
    const asked = this.query()?.get('state');
    if (asked === 'error' && this.retried()) {
      return 'ready';
    }
    return asked === 'loading' || asked === 'empty' || asked === 'error' ? asked : 'ready';
  });

  protected readonly tabs = computed(() => {
    const rows = this.rows();
    const archived = rows.filter((row) => row.archived).length;
    return [
      { key: 'all', label: this.word('app.sample.tab.all'), count: this.digits(rows.length) },
      { key: 'active', label: this.word('app.sample.tab.active'), count: this.digits(rows.length - archived) },
      { key: 'archived', label: this.word('app.sample.tab.archived'), count: this.digits(archived) },
    ];
  });

  protected readonly matching = computed(() => {
    const words = this.search().trim().toLocaleLowerCase();
    const kind = this.kind();
    const tab = this.tab();
    return this.rows().filter(
      (row) =>
        (tab === 'all' || (tab === 'archived') === row.archived) &&
        (kind === null || row.kind === kind) &&
        (words === '' ||
          row.nameAr.includes(words) ||
          row.nameEn.toLocaleLowerCase().includes(words)),
    );
  });

  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.matching().length / PAGE_SIZE)));
  protected readonly from = computed(() => (this.page() - 1) * PAGE_SIZE + 1);
  protected readonly to = computed(() => Math.min(this.page() * PAGE_SIZE, this.matching().length));
  protected readonly shown = computed(() => this.matching().slice(this.from() - 1, this.to()));

  protected readonly chips = computed<readonly ScsAppliedChip[]>(() => {
    const kind = this.kind();
    return kind === null
      ? []
      : [
          {
            key: 'kind',
            label: `${this.word('app.sample.column.kind')}: ${this.word('app.sample.kind.' + kind)}`,
          },
        ];
  });

  /** Writes a number in the digits of the page's language. */
  protected readonly digits = (value: number): string =>
    new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB', {
      useGrouping: false,
    }).format(value);

  protected nameOf(row: SampleRow): string {
    return this.language.language() === 'ar' ? row.nameAr : row.nameEn;
  }

  protected chooseTab(tab: string): void {
    this.tab.set(tab);
    this.page.set(1);
  }

  protected searchFor(words: string): void {
    this.search.set(words);
    this.page.set(1);
  }

  protected applyFilter(): void {
    this.kind.set(this.draftKind());
    this.page.set(1);
    this.filterOpen.set(false);
  }

  protected clearAll(): void {
    this.kind.set(null);
    this.draftKind.set(null);
    this.page.set(1);
    this.filterOpen.set(false);
  }

  protected unmark(id: number): void {
    if (this.marked() === id && this.deleting()?.id !== id) {
      this.marked.set(null);
    }
  }

  protected add(): void {
    const id = Math.max(0, ...this.rows().map((row) => row.id)) + 1;
    this.rows.update((rows) => [
      {
        id,
        nameAr: `سجل تجريبي ${arabic.format(id)}`,
        nameEn: `Sample record ${id}`,
        kind: 'checkup',
        amount: 200,
        archived: false,
      },
      ...rows,
    ]);
    this.toasts.show(this.word('app.sample.toast.added'));
  }

  protected export(): void {
    this.toasts.show(this.word('app.sample.toast.exported'));
  }

  protected archive(row: SampleRow): void {
    this.rows.update((rows) =>
      rows.map((each) => (each.id === row.id ? { ...each, archived: !each.archived } : each)),
    );
    this.toasts.show(this.word(row.archived ? 'app.sample.toast.restored' : 'app.sample.toast.archived'));
  }

  protected askDelete(row: SampleRow): void {
    this.understood.set(false);
    this.marked.set(row.id);
    this.deleting.set(row);
  }

  protected cancelDelete(): void {
    this.deleting.set(null);
    this.marked.set(null);
  }

  protected remove(): void {
    const row = this.deleting();
    if (row === null) {
      return;
    }
    this.rows.update((rows) => rows.filter((each) => each.id !== row.id));
    if (this.opened()?.id === row.id) {
      this.opened.set(null);
    }
    this.cancelDelete();
    if (this.page() > this.pages()) {
      this.page.set(this.pages());
    }
    this.toasts.show(this.word('app.sample.toast.deleted'));
  }

  private word(key: string): string {
    return this.transloco.translate(key, {}, this.language.language());
  }
}
