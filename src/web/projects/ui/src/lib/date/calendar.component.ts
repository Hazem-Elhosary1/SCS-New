import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  InjectionToken,
  booleanAttribute,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  model,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { ScsIconComponent } from '../icon/icon.component';
import {
  SCS_WEEK_START_SATURDAY,
  ScsDay,
  ScsDayRange,
  ScsDigits,
  addDays,
  addMonths,
  compareDays,
  describeDay,
  formatNumber,
  isInRange,
  isWithin,
  monthGrid,
  monthTitle,
  partsOf,
  todayInCairo,
  weekEdges,
  weekdayNames,
} from './calendar';

/**
 * The week's first day in this console's calendars: Saturday, the default, in both consoles (the
 * clinic's calendar `4256:887`; the operators' `75:4` draws Sunday, and the owner chose Saturday).
 */
export const SCS_CALENDAR_WEEK_START = new InjectionToken<number>('SCS_CALENDAR_WEEK_START', {
  providedIn: 'root',
  factory: () => SCS_WEEK_START_SATURDAY,
});

let nextId = 0;

/** One day as the grid draws it. */
interface Cell {
  readonly day: ScsDay;
  readonly number: string;
  readonly inMonth: boolean;
  readonly name: string;
  readonly off: boolean;
  readonly chosen: boolean;
  readonly today: boolean;
  /** Inside a range: `start`, `end`, `both` (a range of one day), `middle`, or nothing. */
  readonly band: 'start' | 'end' | 'both' | 'middle' | null;
}

/**
 * A month to choose one day or a range from (design: Date Picker — "Calendar Popover", `75:4`, and
 * its range, `76:3`; the clinic's Calendar `4256:1424` and Calendar Day `4256:885`). The month's name
 * between the previous and next buttons, the days' initials, and six weeks of days; the edges of the
 * months beside it in grey. Today has a ring, the chosen day is filled, a day that may not be chosen
 * is grey. Worked with the keyboard: the arrows move by a day and a week (following the page's
 * direction), Page Up and Page Down by a month (with Shift, a year), Home and End to the week's
 * edges, Enter or Space choose, and Escape asks the field to close.
 */
@Component({
  selector: 'scs-calendar',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      inline-size: max-content;
      max-inline-size: 100%;
      --cell: var(--scs-calendar-cell, 40px);
      --gap: var(--scs-calendar-gap, 2px);
    }
    .panel {
      box-sizing: border-box;
      padding: var(--scs-calendar-pad, 12px);
      padding-block-end: var(--scs-calendar-pad-end, var(--scs-calendar-pad, 12px));
      border: 1px solid var(--scs-calendar-border, var(--scs-color-border-default));
      border-radius: var(--scs-calendar-radius, 10px);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-calendar-shadow, 0 6px 16px rgb(10 15 20 / 12%));
    }
    .head {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      margin-block-end: var(--scs-calendar-head-gap, 8px);
    }
    .step {
      display: grid;
      place-items: center;
      flex: none;
      box-sizing: border-box;
      inline-size: var(--scs-calendar-nav, 32px);
      block-size: var(--scs-calendar-nav, 32px);
      padding: 0;
      border: var(--scs-calendar-nav-border, 0px) solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: var(--scs-calendar-nav-radius, 6px);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-icon-default);
      cursor: pointer;
    }
    .step:hover {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
    }
    .step:focus-visible,
    .day:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    .next scs-icon {
      transform: scaleX(-1);
    }
    .title {
      flex: 1;
      margin: 0;
      font-size: var(--scs-text-body, 14px);
      font-weight: var(--scs-calendar-title-weight, 600);
      text-align: center;
      color: var(--scs-color-text-primary);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(7, var(--cell));
      gap: var(--gap);
    }
    [role='row'] {
      display: contents;
    }
    .weekdays {
      margin-block-end: var(--scs-calendar-week-gap, 8px);
    }
    .weekday {
      display: grid;
      place-items: center;
      block-size: var(--scs-calendar-weekday-height, 24px);
      font-size: 11px;
      font-weight: var(--scs-calendar-weekday-weight, 600);
      color: var(--scs-calendar-weekday-color, var(--scs-color-text-tertiary));
    }
    .cell {
      position: relative;
      display: grid;
      place-items: center;
      block-size: var(--cell);
    }
    .out {
      font-size: var(--scs-calendar-day-size, 13px);
      font-weight: var(--scs-calendar-day-weight, 500);
      color: var(--scs-calendar-out-color, var(--scs-color-text-placeholder));
    }
    /* A range: a band under its days, from the middle of the first to the middle of the last. */
    .cell[data-band]::before {
      content: '';
      position: absolute;
      inset-block: 0;
      inset-inline: calc(var(--gap) / -2);
      background: var(--scs-calendar-band, var(--scs-color-brand-subtle));
    }
    .cell[data-band='start']::before {
      inset-inline-start: 50%;
    }
    .cell[data-band='end']::before {
      inset-inline-end: 50%;
    }
    .cell[data-band='both']::before {
      content: none;
    }
    .cell[data-band]:first-child::before {
      inset-inline-start: 0;
    }
    .cell[data-band]:last-child::before {
      inset-inline-end: 0;
    }
    .cell[data-band='start']:first-child::before {
      inset-inline-start: 50%;
    }
    .cell[data-band='end']:last-child::before {
      inset-inline-end: 50%;
    }
    .day {
      position: relative;
      display: grid;
      place-items: center;
      box-sizing: border-box;
      inline-size: var(--cell);
      block-size: var(--cell);
      padding: 0;
      border: 0;
      border-radius: 999px;
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-calendar-day-size, 13px);
      font-weight: var(--scs-calendar-day-weight, 500);
      cursor: pointer;
    }
    .day:hover:not([aria-disabled='true']),
    .day[data-still='hover'] {
      background: var(--scs-calendar-hover-bg, var(--scs-color-bg-surface-secondary));
    }
    .cell[data-band='middle'] .day {
      color: var(--scs-calendar-band-color, var(--scs-color-brand-active));
    }
    .cell[data-band='middle'] .day:hover {
      background: none;
    }
    .day.today {
      box-shadow: inset 0 0 0 var(--scs-calendar-today-ring, 1px) var(--scs-color-brand-default);
      color: var(--scs-calendar-today-color, var(--scs-color-brand-active));
      font-weight: var(--scs-calendar-today-weight, 500);
    }
    .day[aria-disabled='true'] {
      background: var(--scs-calendar-off-bg, none);
      color: var(--scs-calendar-off-color, var(--scs-color-text-placeholder));
      cursor: not-allowed;
    }
    /* The clinic's calendar ends with a link to today (4256:887). */
    .foot {
      display: flex;
      margin-block-start: var(--scs-spacing-sm);
    }
    .to-today {
      padding: var(--scs-spacing-xs) var(--scs-spacing-sm);
      border: 0;
      border-radius: var(--scs-radius-sm, 4px);
      background: none;
      color: var(--scs-color-text-link, var(--scs-color-brand-default));
      font: inherit;
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
      cursor: pointer;
    }
    .to-today:hover {
      text-decoration: underline;
    }
    .to-today:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
    }
    .day.chosen,
    .day.chosen:hover {
      background: var(--scs-color-brand-default);
      box-shadow: none;
      color: var(--scs-color-brand-on-brand);
      font-weight: var(--scs-calendar-chosen-weight, 500);
    }
  `,
  template: `
    <div class="panel" [attr.data-testid]="testId() || null">
      <div class="head">
        <button
          type="button"
          class="step"
          [attr.aria-label]="'ui.date.previous' | transloco"
          [attr.data-testid]="(testId() || 'calendar') + '-previous'"
          (click)="turn(-1)"
          (keydown.escape)="close($event)"
        >
          <scs-icon name="chevron-start" [size]="16" />
        </button>
        <p class="title" [id]="id + '-title'" aria-live="polite" [attr.data-testid]="(testId() || 'calendar') + '-title'">{{ title() }}</p>
        <button
          type="button"
          class="step next"
          [attr.aria-label]="'ui.date.next' | transloco"
          [attr.data-testid]="(testId() || 'calendar') + '-next'"
          (click)="turn(1)"
          (keydown.escape)="close($event)"
        >
          <scs-icon name="chevron-start" [size]="16" />
        </button>
      </div>
      <div role="grid" [attr.aria-labelledby]="id + '-title'">
        <div class="grid weekdays" role="rowgroup">
          <div role="row">
            @for (weekday of weekdays(); track $index) {
              <span class="weekday" role="columnheader" [attr.aria-label]="weekday.long">{{ weekday.short }}</span>
            }
          </div>
        </div>
        <div class="grid" role="rowgroup">
          @for (week of weeks(); track $index) {
            <div role="row">
              @for (cell of week; track cell.day) {
                <div
                  class="cell"
                  role="gridcell"
                  [attr.aria-selected]="cell.inMonth ? cell.chosen || cell.band === 'middle' : null"
                  [attr.data-band]="cell.band"
                >
                  @if (cell.inMonth) {
                    <button
                      type="button"
                      class="day"
                      [class.today]="cell.today"
                      [class.chosen]="cell.chosen"
                      [attr.data-day]="cell.day"
                      [attr.data-testid]="(testId() || 'calendar') + '-' + cell.day"
                      [attr.data-still]="cell.day === hoverDay() ? 'hover' : null"
                      [attr.aria-label]="cell.name"
                      [attr.aria-current]="cell.today ? 'date' : null"
                      [attr.aria-disabled]="cell.off ? 'true' : null"
                      [attr.tabindex]="cell.day === focused() ? 0 : -1"
                      (click)="choose(cell)"
                      (keydown)="key($event)"
                      (pointerenter)="pointing.set(cell.day)"
                      (pointerleave)="pointing.set(null)"
                    >
                      {{ cell.number }}
                    </button>
                  } @else {
                    <span class="out" aria-hidden="true">{{ cell.number }}</span>
                  }
                </div>
              }
            </div>
          }
        </div>
      </div>
      @if (todayLink()) {
        <div class="foot">
          <button
            type="button"
            class="to-today"
            [attr.data-testid]="(testId() || 'calendar') + '-today'"
            (click)="chooseToday()"
            (keydown.escape)="close($event)"
          >
            {{ 'ui.date.today' | transloco }}
          </button>
        </div>
      }
    </div>
  `,
})
export class ScsCalendarComponent {
  protected readonly id = `scs-calendar-${nextId++}`;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);
  private readonly consoleWeekStart = inject(SCS_CALENDAR_WEEK_START);

  /** One day, or a start and an end. */
  readonly mode = input<'single' | 'range'>('single');
  /** The chosen day (`mode` single). */
  readonly value = model<ScsDay | null>(null);
  /** The chosen range (`mode` range); its end is `null` while only the start is chosen. */
  readonly range = model<ScsDayRange | null>(null);
  /** The first and last days that may be chosen. */
  readonly min = input<ScsDay | null>(null);
  readonly max = input<ScsDay | null>(null);
  /** Other days that may not be chosen. */
  readonly disabledDays = input<readonly ScsDay[]>([]);
  /** The week's first day, 0 Sunday … 6 Saturday; the console's when not given. */
  readonly weekStart = input<number | null>(null);
  /** Arabic pages' digits; the console's when not given. */
  readonly digits = input<ScsDigits | null>(null);
  /** Today; Cairo's when not given (the gallery pins it so its pictures stay still). */
  readonly today = input<ScsDay | null>(null);
  /** Draws this day as under the pointer (the gallery's still parts). */
  readonly hoverDay = input<ScsDay | null>(null);
  readonly testId = input('');
  /** A link to today under the month (the clinic's calendar draws one). */
  readonly todayLink = input(false, { transform: booleanAttribute });

  /** A day was chosen, or a range's end. The field closes. */
  readonly chosen = output<ScsDay>();
  /** Escape was pressed. The field closes and takes the focus back. */
  readonly escape = output<void>();

  protected readonly pointing = signal<ScsDay | null>(null);

  private readonly todayDay = computed(() => this.today() ?? todayInCairo());
  private readonly start = computed(() => (this.mode() === 'range' ? (this.range()?.start ?? null) : this.value()));

  /** The day that has the focus in the grid; the month shown is its month. */
  protected readonly focused = linkedSignal(() => this.start() ?? this.todayDay());

  private readonly shown = computed(() => partsOf(this.focused())!);
  private readonly language = computed(() => this.languages.language());
  private readonly arabicDigits = computed(() => this.digits() ?? this.consoleDigits);
  private readonly firstDay = computed(() => this.weekStart() ?? this.consoleWeekStart);

  protected readonly title = computed(() => monthTitle(this.shown().year, this.shown().month, this.language(), this.arabicDigits()));
  protected readonly weekdays = computed(() => weekdayNames(this.language(), this.firstDay()));

  /** The range drawn: the chosen one, or — while only its start is chosen — up to the day pointed at. */
  private readonly drawnRange = computed<ScsDayRange | null>(() => {
    const range = this.range();
    if (this.mode() !== 'range' || !range) return null;
    if (range.end) return range;
    const towards = this.pointing() ?? this.hoverDay();
    return towards && compareDays(towards, range.start) > 0 ? { start: range.start, end: towards } : range;
  });

  protected readonly weeks = computed<Cell[][]>(() => {
    const language = this.language();
    const digits = this.arabicDigits();
    const range = this.drawnRange();
    const today = this.todayDay();
    return monthGrid(this.shown().year, this.shown().month, this.firstDay()).map((week) =>
      week.map((cell) => {
        const ends = range ? [range.start, range.end ?? range.start] : [];
        const band: Cell['band'] =
          !cell.inMonth || !range || !range.end || range.end === range.start || !isInRange(cell.day, range)
            ? null
            : cell.day === ends[0]
              ? 'start'
              : cell.day === ends[1]
                ? 'end'
                : 'middle';
        return {
          day: cell.day,
          number: formatNumber(cell.date, language, digits),
          inMonth: cell.inMonth,
          name: describeDay(cell.day, language, digits),
          off: !this.allowed(cell.day),
          chosen: this.mode() === 'range' ? this.chosenEnd(cell.day) : cell.day === this.value(),
          today: cell.day === today,
          band,
        };
      }),
    );
  });

  /** In a range, its start and its end are filled; a day only pointed at is not. */
  private chosenEnd(day: ScsDay): boolean {
    const range = this.range();
    return !!range && (day === range.start || day === range.end);
  }

  private allowed(day: ScsDay): boolean {
    return isWithin(day, this.min(), this.max()) && !this.disabledDays().includes(day);
  }

  /** Gives the focus to the focused day (the field calls it when the panel opens). */
  focus(): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-day="${this.focused()}"]`)?.focus(), {
      injector: this.injector,
    });
  }

  /** Today: shown, and chosen when it may be. */
  protected chooseToday(): void {
    const today = this.todayDay();
    this.focused.set(today);
    const cell = this.weeks()
      .flat()
      .find((found) => found.day === today && found.inMonth);
    if (cell) this.choose(cell);
    this.focus();
  }

  protected turn(months: number): void {
    this.focused.set(addMonths(this.focused(), months));
  }

  protected choose(cell: Cell): void {
    if (cell.off) return;
    this.focused.set(cell.day);
    if (this.mode() === 'single') {
      this.value.set(cell.day);
      this.chosen.emit(cell.day);
      return;
    }
    const range = this.range();
    if (!range || range.end || compareDays(cell.day, range.start) < 0) {
      this.range.set({ start: cell.day, end: null });
      return;
    }
    this.range.set({ start: range.start, end: cell.day });
    this.chosen.emit(cell.day);
  }

  protected key(event: KeyboardEvent): void {
    const day = this.focused();
    const rtl = getComputedStyle(this.host.nativeElement).direction === 'rtl';
    const moves: Record<string, () => ScsDay> = {
      ArrowRight: () => addDays(day, rtl ? -1 : 1),
      ArrowLeft: () => addDays(day, rtl ? 1 : -1),
      ArrowDown: () => addDays(day, 7),
      ArrowUp: () => addDays(day, -7),
      PageDown: () => addMonths(day, event.shiftKey ? 12 : 1),
      PageUp: () => addMonths(day, event.shiftKey ? -12 : -1),
      Home: () => weekEdges(day, this.firstDay()).first,
      End: () => weekEdges(day, this.firstDay()).last,
    };
    if (moves[event.key]) {
      event.preventDefault();
      this.focused.set(moves[event.key]());
      this.focus();
      return;
    }
    if (event.key === 'Escape') {
      this.close(event);
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      const cell = this.weeks()
        .flat()
        .find((found) => found.day === day);
      if (cell) this.choose(cell);
    }
  }

  protected close(event: Event): void {
    event.preventDefault();
    this.escape.emit();
  }
}
