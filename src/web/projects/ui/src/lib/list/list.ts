import { CdkConnectedOverlay, CdkOverlayOrigin } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  booleanAttribute,
  computed,
  inject,
  input,
  model,
  output,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { OnlineService } from 'core';
import { ScsBannerComponent, ScsButtonDirective } from '../controls/controls';
import { ScsDialogComponent } from '../dialog/dialog.component';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

/**
 * The parts every list of the console is built from, in one order: heading, toolbar, applied
 * chips, rows, footer (spec 006, FR-030; BRD Chapter 01 §A-7). The rules a page keeps when it
 * uses them are in specs/006-app-shell/contracts/list-contract.md. Every text arrives already
 * translated; appearance comes only from the app's token variables.
 */

/**
 * The head of a page: where it sits, its title, one line under it, and its buttons. While there
 * is no connection the frame's warning stands under it, above the page (design: "Shell /
 * Offline"; spec 006, FR-021) — every page of the console starts with this part, so none has to
 * show the warning itself.
 */
@Component({
  selector: 'scs-page-header',
  imports: [TranslocoPipe, ScsBannerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      margin-block-end: 18px;
    }
    .head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
    }
    scs-banner {
      margin-block-start: var(--scs-spacing-lg);
      font-weight: 400;
    }
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
    nav + h1 {
      margin-block-start: 14px;
    }
    h1:first-child {
      margin-block-start: 0;
    }
    p {
      margin: var(--scs-spacing-sm) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .actions {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      flex: none;
      /* A passing notice appears beside the buttons, never over them (CL-SH-058). */
      margin-inline-end: var(--scs-toast-room, 0);
      transition: margin 0.15s ease-out;
    }
    @media (prefers-reduced-motion: reduce) {
      .actions {
        transition: none;
      }
    }
  `,
  template: `
    <div class="head">
      <div>
        @if (parent()) {
          <nav [attr.aria-label]="parent()">
            <span>{{ parent() }}</span>
            <span class="chevron" aria-hidden="true"></span>
            <span aria-current="page">{{ title() }}</span>
          </nav>
        }
        <h1 data-testid="page-title">{{ title() }}</h1>
        @if (note()) {
          <p data-testid="page-note">{{ note() }}</p>
        }
      </div>
      <div class="actions" data-testid="page-actions"><ng-content /></div>
    </div>
    @if (!online.online()) {
      <scs-banner tone="warning" data-testid="offline-alert">{{
        'ui.state.offline.message' | transloco
      }}</scs-banner>
    }
  `,
})
export class ScsPageHeaderComponent {
  protected readonly online = inject(OnlineService);

  readonly title = input.required<string>();

  /** The group the page belongs to, shown before it in the breadcrumb; empty for none. */
  readonly parent = input('');

  /** One line under the title. */
  readonly note = input('');
}

/** A list's container, with its name and one line about it. */
@Component({
  selector: 'scs-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      padding: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    :host + :host {
      margin-block-start: var(--scs-spacing-md);
    }
    h2 {
      margin: var(--scs-spacing-sm) 0 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .note {
      margin: var(--scs-spacing-sm) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .body {
      margin-block-start: var(--scs-spacing-md);
    }
  `,
  template: `
    @if (heading()) {
      <h2>{{ heading() }}</h2>
    }
    @if (note()) {
      <p class="note">{{ note() }}</p>
    }
    <div class="body"><ng-content /></div>
  `,
})
export class ScsCardComponent {
  readonly heading = input('');
  readonly note = input('');
}

/**
 * The look of a table: put `scsTable` on a real `<table>`. A row that opens its record carries
 * `scsRowLink`; a cell of numbers carries `data-kind="number"`.
 */
@Directive({ selector: 'table[scsTable]', host: { class: 'scs-table' } })
export class ScsTableDirective {}

/** Grey shapes in place of content that is loading. */
@Component({
  selector: 'scs-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status', 'aria-busy': 'true', '[attr.aria-label]': 'label() || null' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-12);
    }
    span {
      display: block;
      block-size: 16px;
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-bg-surface-secondary);
      animation: pulse 1.4s ease-in-out infinite;
    }
    span:nth-child(3n + 1) {
      inline-size: 60%;
    }
    span:nth-child(3n + 2) {
      inline-size: 85%;
    }
    @keyframes pulse {
      50% {
        opacity: 0.5;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      span {
        animation: none;
      }
    }
  `,
  template: `
    @for (line of lines(); track line) {
      <span></span>
    }
  `,
})
export class ScsSkeletonComponent {
  readonly rows = input(4);

  /** Already translated; what is loading, for assistive technology. */
  readonly label = input('');

  protected readonly lines = computed(() => Array.from({ length: this.rows() }, (_, i) => i));
}

export type ScsEmptyContext = 'no-data' | 'no-results' | 'error' | 'not-in-plan' | 'not-found';

const EMPTY_ICONS: Record<ScsEmptyContext, ScsIconName> = {
  'no-data': 'clipboard',
  'no-results': 'search',
  error: 'alert-triangle',
  'not-in-plan': 'lock',
  'not-found': 'help-circle',
};

/**
 * What stands where rows would be when there are none to show (design: Empty State): nothing
 * yet, nothing matching, or a failure. A title, one line, and at most one action, projected.
 */
@Component({
  selector: 'scs-empty-state',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-context]': 'context()',
    '[attr.role]': 'context() === "error" ? "alert" : null',
  },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-40) var(--scs-spacing-lg);
      text-align: center;
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 64px;
      block-size: 64px;
      margin-block-end: var(--scs-spacing-sm);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-subtle, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-brand-default);
    }
    :host([data-context='error']) .mark {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-icon);
    }
    h3 {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    p {
      max-inline-size: 320px;
      margin: 0;
      font-size: var(--scs-text-body-sm);
      line-height: 1.7;
      color: var(--scs-color-text-secondary);
    }
    .action:not(:empty) {
      margin-block-start: var(--scs-spacing-sm);
    }
  `,
  template: `
    <span class="mark"><scs-icon [name]="icon()" [size]="26" /></span>
    <h3 data-testid="empty-title">{{ title() }}</h3>
    @if (text()) {
      <p data-testid="empty-text">{{ text() }}</p>
    }
    <div class="action"><ng-content /></div>
  `,
})
export class ScsEmptyStateComponent {
  readonly context = input<ScsEmptyContext>('no-data');
  readonly title = input.required<string>();
  readonly text = input('');

  /** The mark of what is missing, when the page has a better one than the context's own. */
  readonly mark = input<ScsIconName | null>(null);

  protected readonly icon = computed(() => this.mark() ?? EMPTY_ICONS[this.context()]);
}

/**
 * The one row above a list's rows: search at the reading start, «تصفية» at the other end, and
 * nothing else (CL-SH-047). The filter's panel is projected and opens downward over the rows.
 */
@Component({
  selector: 'scs-list-toolbar',
  imports: [ScsIconComponent, CdkOverlayOrigin, CdkConnectedOverlay],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-12);
    }
    .search {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      inline-size: 320px;
      block-size: 40px;
      padding-inline: var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-icon-default);
    }
    .search:focus-within {
      border-color: var(--scs-color-brand-default);
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    input {
      flex: 1;
      min-inline-size: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      outline: none;
    }
    input::placeholder {
      color: var(--scs-color-text-tertiary);
    }
    .clear,
    .filter {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      cursor: pointer;
    }
    .clear {
      padding: 0;
    }
    .filter {
      block-size: 40px;
      padding-inline: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font-weight: 500;
    }
    .filter[aria-expanded='true'] {
      background: var(--scs-color-bg-surface-secondary);
      border-color: var(--scs-color-brand-default);
    }
    .filter:focus-visible,
    .clear:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
  `,
  template: `
    <label class="search">
      <scs-icon name="search" [size]="18" />
      <input
        #box
        type="search"
        data-testid="list-search"
        [value]="search()"
        [attr.placeholder]="searchPlaceholder()"
        [attr.aria-label]="searchPlaceholder()"
        (input)="search.set(box.value)"
      />
      @if (search() !== '') {
        <button
          type="button"
          class="clear"
          data-testid="list-search-clear"
          [attr.aria-label]="clearLabel()"
          (click)="search.set('')"
        >
          <scs-icon name="close" [size]="16" />
        </button>
      }
    </label>

    @if (filterLabel()) {
      <button
        type="button"
        class="filter"
        data-testid="list-filter"
        cdkOverlayOrigin
        #origin="cdkOverlayOrigin"
        [attr.aria-expanded]="filterOpen()"
        aria-haspopup="dialog"
        (click)="filterOpen.set(!filterOpen())"
      >
        <scs-icon name="filter" [size]="18" />
        <span>{{ filterLabel() }}</span>
      </button>
      <ng-template
        cdkConnectedOverlay
        [cdkConnectedOverlayOrigin]="origin"
        [cdkConnectedOverlayOpen]="filterOpen()"
        [cdkConnectedOverlayPositions]="below"
        (overlayOutsideClick)="pressedOutside($event, origin)"
        (detach)="filterOpen.set(false)"
        (overlayKeydown)="$event.key === 'Escape' && filterOpen.set(false)"
      >
        <div
          class="scs-popover"
          role="dialog"
          data-testid="filter-popover"
          [attr.aria-label]="filterLabel()"
        >
          <ng-content />
        </div>
      </ng-template>
    }
  `,
})
export class ScsListToolbarComponent {
  readonly search = model('');

  /** Names what is searched, with no verb and no dots: «باسم المريض أو رقم الهاتف». */
  readonly searchPlaceholder = input.required<string>();

  readonly clearLabel = input('');

  /** «تصفية». Empty for a list with no filters. */
  readonly filterLabel = input('');

  /** Whether the filter's panel is open; its button is drawn pressed while it is. */
  readonly filterOpen = model(false);

  /** A press anywhere else closes the panel; a press on the button itself is the button's own. */
  protected pressedOutside(event: MouseEvent, origin: CdkOverlayOrigin): void {
    if (!(origin.elementRef.nativeElement as HTMLElement).contains(event.target as Node)) {
      this.filterOpen.set(false);
    }
  }

  /** Under the button, aligned to its end, opening downward. */
  protected readonly below = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top' } as const,
  ];
}

export interface ScsAppliedChip {
  readonly key: string;
  /** «‹الوجه›: ‹القيمة›», already translated. */
  readonly label: string;
}

/** What is applied to a list, in one place: chips under the toolbar, and «مسح الكل» (CL-SH-048). */
@Component({
  selector: 'scs-applied-chips',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[hidden]': 'chips().length === 0' },
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      margin-block-end: var(--scs-spacing-12);
    }
    :host([hidden]) {
      display: none;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-6);
      block-size: 28px;
      padding-inline: var(--scs-spacing-10);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
    }
    button {
      display: inline-grid;
      place-items: center;
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-icon-default);
      font: inherit;
      cursor: pointer;
    }
    .all {
      margin-inline-start: auto;
      color: var(--scs-color-text-link);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
  `,
  template: `
    @for (chip of chips(); track chip.key) {
      <span class="chip" data-testid="applied-chip">
        {{ chip.label }}
        <button
          type="button"
          [attr.aria-label]="removeLabel() + ' ' + chip.label"
          (click)="remove.emit(chip.key)"
        >
          <scs-icon name="close" [size]="14" />
        </button>
      </span>
    }
    @if (chips().length > 0) {
      <button type="button" class="all" data-testid="clear-all" (click)="clearAll.emit()">
        {{ clearAllLabel() }}
      </button>
    }
  `,
})
export class ScsAppliedChipsComponent {
  readonly chips = input.required<readonly ScsAppliedChip[]>();
  readonly clearAllLabel = input.required<string>();
  readonly removeLabel = input('');

  readonly remove = output<string>();
  readonly clearAll = output<void>();
}

export interface ScsTab {
  readonly key: string;
  readonly label: string;
  readonly count?: string;
}

/** An exclusive split of a list, with counts: tabs above the toolbar, never chips (BRD §A-7). */
@Component({
  selector: 'scs-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'tablist' },
  styles: `
    :host {
      display: flex;
      gap: var(--scs-spacing-lg);
      margin-block-end: var(--scs-spacing-md);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    button {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-10) 0;
      border: 0;
      border-block-end: 2px solid transparent;
      margin-block-end: -1px;
      background: none;
      color: var(--scs-color-text-secondary);
      font: inherit;
      font-weight: 500;
      cursor: pointer;
    }
    button[aria-selected='true'] {
      border-block-end-color: var(--scs-color-brand-default);
      color: var(--scs-color-brand-active);
      font-weight: 600;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .count {
      padding-inline: var(--scs-spacing-6);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      font-size: var(--scs-text-label);
    }
  `,
  template: `
    @for (tab of tabs(); track tab.key) {
      <button
        type="button"
        role="tab"
        [attr.data-testid]="'tab-' + tab.key"
        [attr.aria-selected]="tab.key === active()"
        (click)="active.set(tab.key)"
      >
        {{ tab.label }}
        @if (tab.count !== undefined) {
          <span class="count">{{ tab.count }}</span>
        }
      </button>
    }
  `,
})
export class ScsTabsComponent {
  readonly tabs = input.required<readonly ScsTab[]>();
  readonly active = model.required<string>();
}

/**
 * The one row under a table: the count at the reading start, the paging at the end
 * (CL-SH-051). «السابق» is disabled on the first page and «التالي» on the last; a table that
 * fits one page shows the count alone.
 */
@Component({
  selector: 'scs-table-footer',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-12);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    nav {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
    }
    button {
      display: inline-grid;
      place-items: center;
      min-inline-size: 32px;
      block-size: 32px;
      padding-inline: var(--scs-spacing-6);
      border: 0;
      border-radius: var(--scs-radius-sm);
      background: none;
      color: var(--scs-color-text-secondary);
      font: inherit;
      cursor: pointer;
    }
    button[aria-current='page'] {
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font-weight: 600;
    }
    button:hover:not(:disabled):not([aria-current]) {
      background: var(--scs-color-bg-surface-secondary);
    }
    /* «السابق» points back the way the page reads; «التالي» points onward. */
    .onward {
      transform: scaleX(-1);
    }
    button:disabled {
      opacity: 0.5;
      cursor: default;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    /* 32 at a desk; 40 where a finger presses them. */
    @media (max-width: 720px) {
      button {
        min-inline-size: 40px;
        block-size: 40px;
      }
    }
  `,
  template: `
    <span data-testid="table-count">{{ count() }}</span>
    @if (pages() > 1) {
      <nav [attr.aria-label]="pagingLabel()" data-testid="table-paging">
        <button
          type="button"
          class="arrow"
          data-testid="page-previous"
          [attr.aria-label]="previousLabel()"
          [attr.title]="previousLabel()"
          [disabled]="atFirst()"
          (click)="page.set(page() - 1)"
        >
          <scs-icon name="chevron-start" [size]="16" />
        </button>
        @for (number of numbers(); track number.value) {
          <button
            type="button"
            [attr.data-testid]="'page-' + number.value"
            [attr.aria-current]="number.value === page() ? 'page' : null"
            (click)="page.set(number.value)"
          >
            {{ number.shown }}
          </button>
        }
        <button
          type="button"
          class="arrow"
          data-testid="page-next"
          [attr.aria-label]="nextLabel()"
          [attr.title]="nextLabel()"
          [disabled]="atLast()"
          (click)="page.set(page() + 1)"
        >
          <scs-icon class="onward" name="chevron-start" [size]="16" />
        </button>
      </nav>
    }
  `,
})
export class ScsTableFooterComponent {
  /** «١–٥ من ٥٢ …», already written in the page's digits. */
  readonly count = input.required<string>();
  readonly page = model(1);
  readonly pages = input(1);
  readonly previousLabel = input.required<string>();
  readonly nextLabel = input.required<string>();
  readonly pagingLabel = input('');

  protected readonly atFirst = computed(() => this.page() <= 1);
  protected readonly atLast = computed(() => this.page() >= this.pages());

  /** Writes a page number in the page's digits. */
  readonly digits = input<(value: number) => string>((value) => String(value));

  protected readonly numbers = computed(() =>
    Array.from({ length: this.pages() }, (_, i) => ({ value: i + 1, shown: this.digits()(i + 1) })),
  );
}

/**
 * Marks a control that changes data (spec 006, FR-021). While there is no connection it is
 * disabled, and says so; when the connection returns it is as it was. Put it on every such
 * control: the frame's promise that nothing can be changed offline rests on it.
 */
@Directive({
  selector: 'button[scsWrite]',
  host: {
    '[disabled]': 'blocked() || disabled()',
    '[attr.data-blocked]': 'blocked() ? "offline" : null',
    '[attr.title]': 'blocked() ? scsWriteReason() : null',
  },
})
export class ScsWriteDirective {
  /** Whether there is a connection; the page passes the shared service's signal. */
  readonly scsWrite = input(true, { transform: (value: boolean | '') => value !== false });

  /** The control's own reason to be disabled, apart from the connection. */
  readonly disabled = input(false, { transform: booleanAttribute });

  /** Already translated: why it is disabled while offline. */
  readonly scsWriteReason = input('');

  protected readonly blocked = computed(() => !this.scsWrite());
}

/** A thin strip saying this is not the real product (design: Dev Environment Strip). */
@Component({
  selector: 'scs-env-strip',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'note', 'data-testid': 'env-strip' },
  styles: `
    :host {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--scs-spacing-sm);
      block-size: 32px;
      background: var(--scs-color-feedback-warning-icon);
      color: var(--scs-color-brand-on-brand);
      font-size: var(--scs-text-label);
      font-weight: 500;
    }
  `,
  template: `<scs-icon name="info" [size]="14" /><ng-content />`,
})
export class ScsEnvStripComponent {}

/** A two-way language pill: «عربي · EN». The active half names the current language; no flags. */
@Component({
  selector: 'scs-language-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { dir: 'ltr', role: 'group', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-2, 2px);
      padding: var(--scs-spacing-2, 2px);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
    }
    .segment {
      display: inline-grid;
      place-items: center;
      min-inline-size: 40px;
      block-size: 28px;
      padding: 0 var(--scs-spacing-10);
      border: 0;
      border-radius: var(--scs-radius-full);
      background: none;
      color: var(--scs-color-text-secondary);
      font: inherit;
      font-size: var(--scs-text-label);
      font-weight: 600;
      line-height: 1;
    }
    button.segment {
      cursor: pointer;
    }
    button.segment:hover {
      color: var(--scs-color-text-primary);
    }
    button.segment:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .segment.on {
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      box-shadow: var(--scs-shadow-subtle);
    }
  `,
  template: `
    @for (choice of choices; track choice.code) {
      @if (current() === choice.code) {
        <span class="segment on" aria-current="true" [attr.lang]="choice.code">{{
          choice.label
        }}</span>
      } @else {
        <button
          type="button"
          class="segment"
          data-testid="language-switch"
          [attr.lang]="choice.code"
          (click)="chosen.emit(choice.code)"
        >
          {{ choice.label }}
        </button>
      }
    }
  `,
})
export class ScsLanguageToggleComponent {
  readonly current = input.required<'ar' | 'en'>();

  /** Already translated: what the pill is, for assistive technology. */
  readonly label = input('');

  readonly chosen = output<'ar' | 'en'>();

  /** Not translated: a language is always named in its own words. */
  protected readonly choices = [
    { code: 'ar', label: 'عربي' },
    { code: 'en', label: 'EN' },
  ] as const;
}

/**
 * The body of a filter's panel (design: Filter Popover; frame "Shell / Table Filter Open"): its
 * name and a × at the top, the facets a page projects, and a foot with «مسح الكل» at the reading
 * start and «تطبيق» at the end. Nothing is applied until «تطبيق» is pressed or Enter is typed.
 */
@Component({
  selector: 'scs-filter-popover',
  imports: [ScsButtonDirective, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      inline-size: 320px;
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-md) var(--scs-spacing-md) var(--scs-spacing-sm);
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    .facets {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-sm) var(--scs-spacing-md) var(--scs-spacing-md);
    }
    footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
    }
    .plain {
      display: inline-grid;
      place-items: center;
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-icon-default);
      font: inherit;
      cursor: pointer;
    }
    .clear {
      color: var(--scs-color-text-link);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
    }
    .plain:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
  `,
  template: `
    <form (submit)="$event.preventDefault(); apply.emit()">
      <header>
        <h2 data-testid="filter-title">{{ heading() }}</h2>
        <button
          type="button"
          class="plain"
          data-testid="filter-close"
          [attr.aria-label]="closeLabel()"
          (click)="closed.emit()"
        >
          <scs-icon name="close" [size]="16" />
        </button>
      </header>
      <div class="facets"><ng-content /></div>
      <footer>
        <button type="button" class="plain clear" data-testid="filter-clear" (click)="clear.emit()">
          {{ clearLabel() }}
        </button>
        <button type="submit" scsButton data-testid="filter-apply">{{ applyLabel() }}</button>
      </footer>
    </form>
  `,
})
export class ScsFilterPopoverComponent {
  /** What is being filtered: «تصفية المواعيد». */
  readonly heading = input.required<string>();
  readonly applyLabel = input.required<string>();
  readonly clearLabel = input.required<string>();
  readonly closeLabel = input('');

  readonly apply = output<void>();
  readonly clear = output<void>();

  /** The × was pressed: the page closes the panel, and nothing is applied. */
  readonly closed = output<void>();
}

/**
 * A confirmation, for what cannot be undone or touches someone else (design: Modal; BRD §A-7,
 * CL-SH-060). Its button names the action — never «نعم» or «موافق» — and stays disabled until
 * what the action needs is chosen; the page projects that choice and says when it is `ready`.
 */
@Component({
  selector: 'scs-confirm',
  imports: [ScsDialogComponent, ScsButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
    }
    .danger {
      border-color: var(--scs-color-feedback-danger-icon);
      background: var(--scs-color-feedback-danger-icon);
    }
  `,
  template: `
    <scs-dialog [heading]="heading()" [(open)]="open">
      <ng-content />
      <div class="buttons">
        <button
          type="button"
          scsButton="secondary"
          data-testid="confirm-cancel"
          (click)="open.set(false)"
        >
          {{ cancelLabel() }}
        </button>
        <button
          type="button"
          scsButton
          data-testid="confirm-action"
          [class.danger]="danger()"
          [disabled]="!ready()"
          (click)="confirmed.emit()"
        >
          {{ actionLabel() }}
        </button>
      </div>
    </scs-dialog>
  `,
})
export class ScsConfirmComponent {
  readonly open = model(false);
  readonly heading = input.required<string>();

  /** Names the action: «حذف السجل». */
  readonly actionLabel = input.required<string>();

  /** The way back; «تراجع» when the action itself is "cancel …". */
  readonly cancelLabel = input.required<string>();

  /** Whether what the action needs has been chosen. */
  readonly ready = input(true, { transform: booleanAttribute });

  /** Draws the action as dangerous. */
  readonly danger = input(false, { transform: booleanAttribute });

  readonly confirmed = output<void>();
}
