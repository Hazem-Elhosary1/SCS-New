import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  input,
  linkedSignal,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { ScsIconComponent } from '../icon/icon.component';

let nextId = 0;

export interface ScsSelectOption {
  readonly value: string;
  readonly label: string;
  readonly disabled?: boolean;
  /** Why this choice may not be made; written under its name. */
  readonly reason?: string;
}

/** One row of the open list: a choice, or "nothing chosen" when that may be chosen again. */
interface Row extends ScsSelectOption {
  readonly id: string;
}

/**
 * One choice from a list (design: Dropdown — "Select Trigger" in six states and the open "Menu
 * Panel"). The trigger is a button; the list opens beneath it, follows the page's direction, and
 * is worked with the keyboard: the arrows move, Enter or Space chooses, Escape closes and gives
 * the focus back, Home and End jump, and a typed letter goes to the first choice it begins. A
 * long list may be searched. A choice that may not be made is drawn grey with its reason, and
 * cannot be chosen.
 */
@Component({
  selector: 'scs-select',
  imports: [ScsIconComponent, CdkOverlayOrigin, CdkConnectedOverlay, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .name {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: var(--scs-field-label-weight, 500);
      color: var(--scs-field-label-color, var(--scs-color-text-primary));
    }
    /* A choice that must be made is marked as a required text field is (the owner's answer of 17 Oct 2026). */
    .required {
      color: var(--scs-color-feedback-danger-icon);
    }
    /* A label read by assistive technology but not drawn, where the row already names the choice. */
    .name.hidden {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      margin: 0;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    :host(.off) .name {
      color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
    }
    .trigger {
      position: relative;
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      inline-size: 100%;
      min-block-size: 44px;
      padding-inline: var(--scs-spacing-md) var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body);
      text-align: start;
      cursor: pointer;
    }
    .trigger:hover:not(:disabled),
    :host-context([data-scs-state~='hover']) .trigger {
      border-color: var(--scs-color-icon-default);
    }
    /* Focus and open: a 1.5 purple border, and a 2 purple ring 4 outside it (Select Trigger / Focus). */
    .trigger:focus-visible,
    .trigger[aria-expanded='true'],
    :host-context([data-scs-state~='focus']) .trigger {
      border-color: var(--scs-color-brand-default);
      box-shadow: inset 0 0 0 0.5px var(--scs-color-brand-default);
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 3px;
    }
    .trigger[aria-invalid='true'] {
      border-color: var(--scs-color-feedback-danger-icon);
      box-shadow: inset 0 0 0 0.5px var(--scs-color-feedback-danger-icon);
    }
    .trigger:disabled {
      border-color: var(--scs-color-border-default);
      background: var(--scs-color-bg-surface-secondary);
      cursor: not-allowed;
    }
    .value {
      flex: 1;
      min-inline-size: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .value.empty {
      color: var(--scs-select-empty-color, var(--scs-color-text-placeholder));
    }
    .trigger:disabled .value {
      color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
    }
    .chevron {
      flex: none;
      color: var(--scs-color-icon-default);
      transition: transform 0.15s;
    }
    .trigger[aria-expanded='true'] .chevron {
      transform: rotate(180deg);
    }
    .hint,
    .error {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-field-message-size, var(--scs-text-label));
      color: var(--scs-color-text-secondary);
    }
    .error {
      color: var(--scs-field-error-color, var(--scs-color-feedback-danger-text));
    }
    /* The open list (Menu Panel): white, rounded 10, a hairline and a soft shadow, 8 inside. */
    .panel {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      box-sizing: border-box;
      max-block-size: 320px;
      padding: var(--scs-spacing-sm);
      border: 1px solid var(--scs-color-border-subtle);
      border-radius: 10px;
      background: var(--scs-color-bg-surface);
      box-shadow: 0 6px 16px rgb(10 15 20 / 12%);
    }
    .search {
      flex: none;
      block-size: 32px;
      padding-inline: var(--scs-spacing-12);
      border: 0;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body-sm);
      outline: none;
    }
    .search::placeholder {
      color: var(--scs-color-text-tertiary);
    }
    .search:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
    }
    [role='listbox'] {
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
      outline: none;
    }
    [role='option'] {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      min-block-size: 40px;
      padding: var(--scs-spacing-6) var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body-sm);
      cursor: pointer;
    }
    [role='option'][data-active='true'] {
      background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
    }
    [role='option'][aria-selected='true'] {
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-weight: 600;
    }
    [role='option'][aria-disabled='true'] {
      color: var(--scs-color-text-tertiary);
      cursor: not-allowed;
    }
    /* Several may be chosen: a chosen row keeps its colours and its box is ticked (58:14). */
    [aria-multiselectable='true'] [role='option'][aria-selected='true'] {
      background: none;
      color: var(--scs-color-text-primary);
      font-weight: 400;
    }
    [aria-multiselectable='true'] [role='option'][aria-selected='true'][data-active='true'] {
      background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
    }
    .box {
      display: grid;
      place-items: center;
      flex: none;
      box-sizing: border-box;
      inline-size: 16px;
      block-size: 16px;
      border: 1.5px solid var(--scs-check-border, var(--scs-color-border-control, var(--scs-color-border-default)));
      border-radius: 4px;
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-on-brand);
    }
    .box svg {
      visibility: hidden;
    }
    [aria-selected='true'] .box {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-default);
    }
    [aria-selected='true'] .box svg {
      visibility: visible;
    }
    /* The open list drawn in place, under the trigger (the gallery's still specimens). */
    .inline {
      margin-block-start: 4px;
    }
    .words {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-inline-size: 0;
    }
    .reason {
      font-size: var(--scs-text-label);
      font-weight: 400;
    }
    .none {
      padding: var(--scs-spacing-sm) var(--scs-spacing-12);
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
    }
    @media (prefers-reduced-motion: reduce) {
      .chevron {
        transition: none;
      }
    }
  `,
  host: { '[class.off]': 'disabled()' },
  template: `
    <span class="name" [class.hidden]="labelHidden()" [id]="id + '-label'">{{ label() }}@if (required()) {<span class="required" aria-hidden="true"> *</span>}</span>
    <button
      #trigger
      type="button"
      class="trigger"
      role="combobox"
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      aria-haspopup="listbox"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="open() ? id + '-list' : null"
      [attr.aria-labelledby]="id + '-label ' + id + '-value'"
      [attr.aria-invalid]="error() !== '' ? 'true' : null"
      [attr.aria-describedby]="describedBy()"
      [attr.data-testid]="testId() || null"
      [attr.data-value]="value()"
      [disabled]="disabled()"
      (click)="toggle()"
      (keydown)="triggerKey($event)"
    >
      <span class="value" [class.empty]="shownLabel() === ''" [id]="id + '-value'">{{ shownLabel() || placeholder() }}</span>
      <scs-icon class="chevron" name="chevron-down" [size]="18" />
    </button>

    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      [cdkConnectedOverlayPush]="true"
      [cdkConnectedOverlayViewportMargin]="8"
      [cdkConnectedOverlayWidth]="width()"
      (overlayOutsideClick)="pressedOutside($event)"
      (detach)="close(false)"
      (attach)="opened()"
    >
      <ng-container [ngTemplateOutlet]="panel" />
    </ng-template>
    @if (listInline()) {
      <div class="inline"><ng-container [ngTemplateOutlet]="panel" /></div>
    }

    <ng-template #panel>
      <div
        class="panel"
        [style.inline-size.px]="listInline() ? null : width()"
        [attr.data-testid]="(testId() || 'select') + '-panel'"
      >
        @if (searchable()) {
          <input
            #search
            class="search"
            type="search"
            [attr.aria-label]="searchLabel() || label()"
            [attr.aria-controls]="id + '-list'"
            [attr.aria-activedescendant]="activeId()"
            [placeholder]="searchLabel()"
            [value]="query()"
            [attr.data-testid]="(testId() || 'select') + '-search'"
            (input)="filter($any($event.target).value)"
            (keydown)="listKey($event)"
          />
        }
        <ul
          #list
          role="listbox"
          [attr.aria-multiselectable]="multiple() ? 'true' : null"
          [id]="id + '-list'"
          [attr.aria-labelledby]="id + '-label'"
          [attr.aria-activedescendant]="activeId()"
          [attr.tabindex]="searchable() ? -1 : 0"
          (keydown)="listKey($event)"
        >
          @for (row of rows(); track row.id; let index = $index) {
            <li
              role="option"
              [id]="row.id"
              [attr.data-value]="row.value"
              [attr.aria-selected]="isChosen(row.value)"
              [attr.aria-disabled]="row.disabled ? 'true' : null"
              [attr.data-active]="index === active()"
              (pointerenter)="active.set(index)"
              tabindex="-1"
              (click)="choose(row)"
              (keydown.enter)="choose(row)"
            >
              @if (multiple()) {
                <span class="box" aria-hidden="true">
                  <svg width="10" height="9" viewBox="0 0 10 9" fill="none">
                    <path d="M1 4.6 3.7 7.2 9 1.6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </span>
              }
              <span class="words">
                <span>{{ row.label }}</span>
                @if (row.reason) {
                  <span class="reason">{{ row.reason }}</span>
                }
              </span>
              @if (!multiple() && row.value === value() && row.value !== '') {
                <scs-icon name="check" [size]="16" />
              }
            </li>
          } @empty {
            <li class="none" role="presentation" [attr.data-testid]="(testId() || 'select') + '-none'">{{ emptyLabel() }}</li>
          }
        </ul>
      </div>
    </ng-template>

    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'select') + '-error'">{{ error() }}</p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'" [attr.data-testid]="(testId() || 'select') + '-hint'">{{ hint() }}</p>
    }
  `,
})
export class ScsSelectComponent {
  protected readonly id = `scs-select-${nextId++}`;

  readonly label = input.required<string>();
  readonly options = input.required<readonly ScsSelectOption[]>();
  readonly value = model('');
  /** Shown while nothing is chosen. Without `required` it is also a choice: "nothing". */
  readonly placeholder = input('');
  readonly required = input(false, { transform: booleanAttribute });

  /** The label is read but not drawn: the row it sits in names the choice already. */
  readonly labelHidden = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly testId = input('');
  /** A search box at the top of the list, for a long one. */
  readonly searchable = input(false, { transform: booleanAttribute });
  /** The search box's placeholder: «بحث...» · "Search...". */
  readonly searchLabel = input('');
  /** What the list says when the search finds nothing: «لا توجد نتائج» · "No results found". */
  readonly emptyLabel = input('');
  /** Several may be chosen (the page's "Multi Select"): each row has its box, and the list stays open. */
  readonly multiple = input(false, { transform: booleanAttribute });
  /** The chosen values when `multiple`. */
  readonly values = model<readonly string[]>([]);
  /** Shows the open list in place under the trigger, still (the gallery's specimens of the list). */
  readonly listInline = input(false, { transform: booleanAttribute });
  /** What the search box holds at first (the gallery's still specimen of a search with results). */
  readonly searchText = input('');

  protected readonly open = signal(false);
  protected readonly query = linkedSignal(() => this.searchText());
  protected readonly active = signal(-1);
  protected readonly width = signal(0);

  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly list = viewChild<ElementRef<HTMLUListElement>>('list');
  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');

  /** Beneath the trigger, its width, aligned to the reading start; above it when there is no room. */
  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  protected readonly chosen = computed(() => this.options().find((option) => option.value === this.value() && option.value !== '') ?? null);

  /** What the trigger says: the chosen label, or every chosen label when several may be. */
  protected readonly shownLabel = computed(() =>
    this.multiple()
      ? this.options()
          .filter((option) => this.values().includes(option.value))
          .map((option) => option.label)
          .join(globalThis.document?.documentElement.dir === 'rtl' ? '، ' : ', ')
      : (this.chosen()?.label ?? ''),
  );

  protected isChosen(value: string): boolean {
    return this.multiple() ? this.values().includes(value) : value === this.value();
  }

  protected readonly rows = computed<Row[]>(() => {
    const words = this.query().trim().toLocaleLowerCase();
    const all: ScsSelectOption[] =
      this.placeholder() && !this.required() && !this.multiple()
        ? [{ value: '', label: this.placeholder() }, ...this.options()]
        : [...this.options()];
    return all
      .filter((option) => words === '' || option.label.toLocaleLowerCase().includes(words))
      .map((option, index) => ({ ...option, id: `${this.id}-option-${index}` }));
  });

  protected readonly activeId = computed(() => this.rows()[this.active()]?.id ?? null);

  protected describedBy(): string | null {
    return this.error() ? `${this.id}-error` : this.hint() ? `${this.id}-hint` : null;
  }

  protected toggle(): void {
    if (this.open()) {
      this.close(true);
    } else {
      this.show();
    }
  }

  private show(): void {
    if (this.disabled()) {
      return;
    }
    this.query.set('');
    this.width.set(this.trigger().nativeElement.getBoundingClientRect().width);
    const chosen = this.rows().findIndex((row) => row.value === this.value());
    this.active.set(chosen >= 0 ? chosen : this.next(-1, 1));
    this.open.set(true);
  }

  /** The list is in the page: the focus goes into it, and the chosen row is in view. */
  protected opened(): void {
    setTimeout(() => {
      (this.search()?.nativeElement ?? this.list()?.nativeElement)?.focus();
      this.reveal();
    });
  }

  close(giveFocusBack: boolean): void {
    if (!this.open()) {
      return;
    }
    this.open.set(false);
    if (giveFocusBack) {
      this.trigger().nativeElement.focus();
    }
  }

  protected pressedOutside(event: MouseEvent): void {
    if (!this.trigger().nativeElement.contains(event.target as Node)) {
      this.close(false);
    }
  }

  protected choose(row: Row): void {
    if (row.disabled) {
      return;
    }
    if (this.multiple()) {
      const values = this.values();
      this.values.set(values.includes(row.value) ? values.filter((value) => value !== row.value) : [...values, row.value]);
      return;
    }
    this.value.set(row.value);
    this.close(true);
  }

  protected filter(words: string): void {
    this.query.set(words);
    this.active.set(this.next(-1, 1));
  }

  protected triggerKey(event: KeyboardEvent): void {
    if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key) && !this.open()) {
      event.preventDefault();
      this.show();
    }
  }

  protected listKey(event: KeyboardEvent): void {
    const rows = this.rows();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.move(this.next(this.active(), 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.move(this.next(this.active(), -1));
        break;
      case 'Home':
        event.preventDefault();
        this.move(this.next(-1, 1));
        break;
      case 'End':
        event.preventDefault();
        this.move(this.next(rows.length, -1));
        break;
      case 'Enter':
        event.preventDefault();
        if (rows[this.active()]) {
          this.choose(rows[this.active()]);
        }
        break;
      case ' ':
        if (!this.searchable()) {
          event.preventDefault();
          if (rows[this.active()]) {
            this.choose(rows[this.active()]);
          }
        }
        break;
      case 'Escape':
        event.preventDefault();
        this.close(true);
        break;
      case 'Tab':
        this.close(false);
        break;
      default:
        // A letter goes to the first choice that begins with it.
        if (!this.searchable() && event.key.length === 1 && !event.ctrlKey && !event.metaKey) {
          const letter = event.key.toLocaleLowerCase();
          const found = rows.findIndex((row) => !row.disabled && row.label.toLocaleLowerCase().startsWith(letter));
          if (found >= 0) {
            this.move(found);
          }
        }
    }
  }

  /** The next row that may be chosen, from `from` in `step`'s direction; `from` itself if none. */
  private next(from: number, step: 1 | -1): number {
    const rows = this.rows();
    for (let at = from + step; at >= 0 && at < rows.length; at += step) {
      if (!rows[at].disabled) {
        return at;
      }
    }
    return from >= 0 && from < rows.length ? from : -1;
  }

  private move(index: number): void {
    this.active.set(index);
    this.reveal();
  }

  private reveal(): void {
    const id = this.activeId();
    if (id) {
      setTimeout(() => document.getElementById(id)?.scrollIntoView({ block: 'nearest' }));
    }
  }
}
