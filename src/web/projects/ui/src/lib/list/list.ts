import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
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
import { ScsTagComponent } from '../tag/tag.component';
import { ScsDialogComponent } from '../dialog/dialog.component';
import { ScsIconComponent } from '../icon/icon.component';

// Parts the clinic's first screen needs live in files of their own, so that screen does not carry
// the rest of this file (the table footer's menu, the filter panel, the chips); found here as before.
export * from './skeleton.component';
export * from './empty-state.component';
export * from './language-toggle.component';

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

/**
 * A list's container, with its name and one line about it. Design: Data Display — Card (`281:27` /
 * `313:26`; the clinic's `4252:61428`): its look is kept as the owner chose; the page's states are
 * `interactive` (it tints under the pointer) and `selected` (the brand's tint and border).
 */
@Component({
  selector: 'scs-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.interactive]': 'interactive()', '[class.selected]': 'selected()' },
  styles: `
    :host {
      display: block;
      padding: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    :host(.interactive) {
      cursor: pointer;
    }
    :host(.interactive:hover),
    :host-context([data-scs-state~='hover']) {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
    }
    :host(.selected),
    :host(.selected:hover) {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-subtle);
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
  /** It opens or chooses something: it tints under the pointer. */
  readonly interactive = input(false, { transform: booleanAttribute });
  readonly selected = input(false, { transform: booleanAttribute });
}

/**
 * The look of a table: put `scsTable` on a real `<table>`. A row that opens its record carries
 * `scsRowLink`; a cell of numbers carries `data-kind="number"`.
 */
@Directive({ selector: 'table[scsTable]', host: { class: 'scs-table' } })
export class ScsTableDirective {}


let nextToolbar = 0;

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
    /* The toolbar draws its own clear button; the browser's (Edge, Chrome) would be a second ×. */
    input::-webkit-search-cancel-button,
    input::-webkit-search-decoration {
      -webkit-appearance: none;
      appearance: none;
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
    /* How many filters are applied: a round count between the icon and the word. */
    .filter .count {
      display: inline-grid;
      place-items: center;
      box-sizing: border-box;
      min-inline-size: 18px;
      block-size: 18px;
      padding-inline: 4px;
      border-radius: 9px;
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font-size: 11px;
      font-weight: 700;
      line-height: 1;
    }
    .unseen {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
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
        [attr.aria-describedby]="filterCount() !== 0 && filterCountLabel() ? id + '-count' : null"
        aria-haspopup="dialog"
        (click)="filterOpen.set(!filterOpen())"
      >
        <scs-icon name="filter" [size]="18" />
        @if (filterCount() > 0) {
          <span class="count" aria-hidden="true" data-testid="list-filter-count">{{ filterCountText() || filterCount() }}</span>
          <span class="unseen" [id]="id + '-count'">{{ filterCountLabel() }}</span>
        }
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

  protected readonly id = `scs-list-toolbar-${nextToolbar++}`;

  /** How many filters are applied; 0 shows no count. */
  readonly filterCount = input(0);
  /** The count as the page writes its numbers (the operators' Western digits, the clinic's Arabic ones). */
  readonly filterCountText = input('');
  /** Already translated: the count said in words for a screen reader, "2 filters applied". */
  readonly filterCountLabel = input('');

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

/**
 * What is applied to a list, in one place: chips under the toolbar, and «مسح الكل» (CL-SH-048). Each
 * chip is an `scs-tag` that can be taken away; it keeps the chip's look of before spec 095 through
 * `--scs-chip-*`, which a look may set.
 */
@Component({
  selector: 'scs-applied-chips',
  imports: [ScsTagComponent],
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
    scs-tag {
      --scs-tag-gap: var(--scs-chip-gap, var(--scs-spacing-6));
      --scs-tag-height: var(--scs-chip-height, 28px);
      --scs-tag-pad: var(--scs-chip-pad, var(--scs-spacing-10));
      --scs-tag-radius: var(--scs-chip-radius, var(--scs-radius-full));
      --scs-tag-text: var(--scs-chip-text, var(--scs-text-body-sm));
      --scs-tag-hover-bg: var(--scs-chip-hover-bg, var(--scs-color-bg-surface-secondary));
      --scs-tag-border: var(--scs-chip-border, 0px);
      --scs-tag-remove: var(--scs-chip-remove, var(--scs-color-icon-default));
      /* Each look's chip colours; the neutral tag's own where a look sets none. */
      --scs-tag-neutral-bg: var(--scs-chip-bg, var(--scs-color-bg-surface-secondary));
      --scs-tag-neutral-text: var(--scs-chip-ink, var(--scs-color-text-primary));
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
    /* «مسح الكل» at the far end (the clinic), or right after the chips where a look says so. */
    .all {
      margin-inline-start: var(--scs-chips-clear-push, auto);
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
      <scs-tag
        class="chip"
        data-testid="applied-chip"
        intent="neutral"
        dismissible
        [removeLabel]="removeLabel() + ' ' + chip.label"
        (removed)="remove.emit(chip.key)"
      >
        {{ chip.label }}
      </scs-tag>
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

/**
 * An exclusive split of a list, with counts: tabs above the toolbar, never chips (BRD §A-7).
 * Design: Navigation — Tab Item (`284:12` / `312:62`; the clinic's `4252:60720`). The arrows move
 * between the tabs and choose (following the page's direction), Home and End go to the ends; only
 * the chosen tab is in the Tab order. Each look draws its tabs through `--scs-tabs-*`; the defaults
 * are the tabs before spec 095.
 */
@Component({
  selector: 'scs-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'tablist' },
  styles: `
    :host {
      display: flex;
      gap: var(--scs-tabs-gap, var(--scs-spacing-lg));
      margin-block-end: var(--scs-spacing-md);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--scs-spacing-sm);
      min-inline-size: var(--scs-tabs-min, 0px);
      padding: var(--scs-spacing-10) 0;
      border: 0;
      border-block-end: 2px solid transparent;
      margin-block-end: -1px;
      background: none;
      color: var(--scs-tabs-text, var(--scs-color-text-secondary));
      font: inherit;
      font-size: var(--scs-tabs-size, inherit);
      font-weight: var(--scs-tabs-weight, 500);
      cursor: pointer;
    }
    button[aria-selected='true'] {
      border-block-end-color: var(--scs-color-brand-default);
      color: var(--scs-tabs-on-text, var(--scs-color-brand-active));
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
        [attr.tabindex]="tab.key === active() || (!chosen() && $first) ? 0 : -1"
        (click)="active.set(tab.key)"
        (keydown)="key($event)"
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

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly chosen = computed(() => this.tabs().some((tab) => tab.key === this.active()));

  /** The arrows move and choose, in the page's direction; Home and End go to the ends. */
  protected key(event: KeyboardEvent): void {
    const tabs = this.tabs();
    const at = Math.max(0, tabs.findIndex((tab) => tab.key === this.active()));
    const rtl = getComputedStyle(this.host.nativeElement).direction === 'rtl';
    const to: Record<string, number> = {
      ArrowRight: at + (rtl ? -1 : 1),
      ArrowLeft: at + (rtl ? 1 : -1),
      Home: 0,
      End: tabs.length - 1,
    };
    if (to[event.key] === undefined) return;
    event.preventDefault();
    const next = tabs[(to[event.key] + tabs.length) % tabs.length];
    this.active.set(next.key);
    setTimeout(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="tab-${next.key}"]`)?.focus());
  }
}

/**
 * The one row under a table: the count at the reading start, the paging at the end
 * (CL-SH-051). «السابق» is disabled on the first page and «التالي» on the last; a table that
 * fits one page shows the count alone.
 *
 * Past six pages the paging folds (design: Navigation — "folded pagination", `2111:195`): the
 * three pages around the current one, "…", and the last three; "…" opens upwards a menu of every
 * page not shown (`2104:140`). Each page button is `.scs-page-item` (controls.css), drawn per look.
 */
@Component({
  selector: 'scs-table-footer',
  imports: [ScsIconComponent, CdkMenuTrigger, CdkMenu, CdkMenuItem],
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
    /* On a phone the paging goes under the count, and wraps when it is long. */
    :host {
      flex-wrap: wrap;
    }
    nav {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-pager-gap, var(--scs-spacing-xs));
    }
    /* «السابق» points back the way the page reads; «التالي» points onward. */
    .onward {
      transform: scaleX(-1);
    }
    /* The menu of the pages not shown (2104:140). */
    .hidden-pages {
      display: grid;
      min-inline-size: 96px;
      max-block-size: 240px;
      overflow-y: auto;
      box-sizing: border-box;
      padding: var(--scs-spacing-sm);
      border: 1px solid var(--scs-color-border-default);
      border-radius: 10px;
      background: var(--scs-color-bg-surface);
      box-shadow: 0 6px 16px rgb(10 15 20 / 12%);
    }
    .hidden-pages button {
      block-size: 40px;
      padding: 0;
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: 13px;
      cursor: pointer;
    }
    /* Drawn open in place, still, above its "…" (the gallery's specimen of 2104:140). */
    .more-still {
      position: relative;
      display: inline-flex;
    }
    .more-still .hidden-pages {
      position: absolute;
      inset-block-end: calc(100% + 4px);
      inset-inline-start: 50%;
      translate: -50% 0;
      z-index: 1;
    }
    :host-context([dir='rtl']) .more-still .hidden-pages {
      translate: 50% 0;
    }
    .hidden-pages button:hover,
    .hidden-pages button:focus {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
      outline: none;
    }
  `,
  template: `
    <span data-testid="table-count">{{ count() }}</span>
    @if (pages() > 1) {
      <nav [attr.aria-label]="pagingLabel()" data-testid="table-paging">
        <button
          type="button"
          class="scs-page-item arrow"
          data-testid="page-previous"
          [attr.aria-label]="previousLabel()"
          [attr.title]="previousLabel()"
          [disabled]="atFirst()"
          (click)="page.set(page() - 1)"
        >
          <scs-icon name="chevron-start" [size]="16" />
        </button>
        @for (item of items(); track item.key) {
          @if (item.hidden && menuShown()) {
            <span class="more-still">
              <button type="button" class="scs-page-item more" aria-expanded="true" [attr.aria-label]="moreLabel() || null">
                …<scs-icon name="chevron-down" [size]="14" />
              </button>
              <div class="hidden-pages" role="menu">
                @for (number of item.hidden; track number.value) {
                  <button type="button" role="menuitem" tabindex="-1">{{ number.shown }}</button>
                }
              </div>
            </span>
          } @else if (item.hidden) {
            <button
              type="button"
              class="scs-page-item more"
              data-testid="page-more"
              [attr.aria-label]="moreLabel() || null"
              [cdkMenuTriggerFor]="hiddenPages"
              [cdkMenuPosition]="upwards"
            >
              …<scs-icon name="chevron-down" [size]="14" />
            </button>
            <ng-template #hiddenPages>
              <div class="hidden-pages" cdkMenu data-testid="page-menu">
                @for (number of item.hidden; track number.value) {
                  <button type="button" cdkMenuItem [attr.data-testid]="'page-' + number.value" (cdkMenuItemTriggered)="page.set(number.value)">
                    {{ number.shown }}
                  </button>
                }
              </div>
            </ng-template>
          } @else {
            <button
              type="button"
              class="scs-page-item"
              [attr.data-testid]="'page-' + item.value"
              [attr.aria-current]="item.value === page() ? 'page' : null"
              (click)="page.set(item.value)"
            >
              {{ item.shown }}
            </button>
          }
        }
        <button
          type="button"
          class="scs-page-item arrow"
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

  /** Already translated: the name of "…", which opens the pages not shown. */
  readonly moreLabel = input('');
  /** Shows the menu of the pages not shown open in place, still (the gallery's specimen of it). */
  readonly menuShown = input(false, { transform: booleanAttribute });

  /** "…" opens its menu above it, or below when there is no room. */
  protected readonly upwards: ConnectedPosition[] = [
    { originX: 'center', originY: 'top', overlayX: 'center', overlayY: 'bottom', offsetY: -4 },
    { originX: 'center', originY: 'bottom', overlayX: 'center', overlayY: 'top', offsetY: 4 },
  ];

  /** The page buttons, folded past six pages, with "…" holding the pages not shown. */
  protected readonly items = computed(() => {
    const shown = (value: number) => ({ value, shown: this.digits()(value) });
    const pages = this.pages();
    const page = Math.min(Math.max(this.page(), 1), pages);
    const all = Array.from({ length: pages }, (_, i) => i + 1);
    if (pages <= 6) return all.map((value) => ({ key: String(value), ...shown(value), hidden: null }));
    const start = Math.max(1, Math.min(page - 1, pages - 5));
    const visible = [...new Set([start, start + 1, start + 2, pages - 2, pages - 1, pages])];
    const hidden = all.filter((value) => !visible.includes(value)).map(shown);
    const items: { key: string; value: number; shown: string; hidden: { value: number; shown: string }[] | null }[] =
      visible.map((value) => ({ key: String(value), ...shown(value), hidden: null }));
    if (hidden.length) {
      const gap = start + 2 < pages - 3;
      items.splice(gap ? 3 : 0, 0, { key: 'more', value: 0, shown: '…', hidden });
    }
    return items;
  });
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
