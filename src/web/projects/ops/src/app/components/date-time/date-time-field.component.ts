import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
  linkedSignal,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import {
  SCS_DATE_TRIGGER_STYLES,
  ScsCalendarComponent,
  ScsDay,
  ScsDigits,
  ScsIconComponent,
  ScsTime,
  ScsTimeListComponent,
  formatDay,
  formatTime,
  isDay,
  isTime,
} from 'ui';

let nextId = 0;

/** A day and a time on the wall clock, `YYYY-MM-DDTHH:mm`. */
export type DayAndTime = string;

/** The day and the time of a value, or nothing. */
export function partsOfDayAndTime(value: DayAndTime): { day: ScsDay; time: ScsTime } | null {
  const [day, time] = value.split('T');
  return isDay(day) && isTime(time) ? { day, time } : null;
}

/**
 * A day and a time, the operators console's own (design: Date & Time Picker — "Date & Time Field
 * Trigger" in six states, `79:71` / `308:172`, and its panel, `80:4`). The trigger names both —
 * "Aug 28, 2026 · 2:30 PM" · «28 أغسطس 2026 · 2:30 م» — or asks for them; the panel holds the
 * calendar and, beside it, the times. Choosing a day moves to the times; choosing a time with a day
 * chosen keeps both and closes. Tab goes between the two sides; Escape closes without keeping.
 */
@Component({
  selector: 'app-date-time-field',
  imports: [
    ScsIconComponent,
    ScsCalendarComponent,
    ScsTimeListComponent,
    CdkOverlayOrigin,
    CdkConnectedOverlay,
    NgTemplateOutlet,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    SCS_DATE_TRIGGER_STYLES,
    `
      .marks {
        display: flex;
        gap: var(--scs-spacing-sm);
      }
      /* One panel, the calendar and the times side by side, a line between (80:4). */
      .both {
        box-sizing: border-box;
        inline-size: fit-content;
        max-inline-size: 100%;
        overflow: hidden;
        border: 1px solid var(--scs-calendar-border, var(--scs-color-border-default));
        border-radius: var(--scs-calendar-radius, 10px);
        background: var(--scs-color-bg-surface);
        box-shadow: var(--scs-calendar-shadow, 0 6px 16px rgb(10 15 20 / 12%));
      }
      /* Side by side; too narrow for both, the times wrap under the calendar. */
      .sides {
        display: flex;
        flex-wrap: wrap;
      }
      .both scs-calendar {
        --scs-calendar-border: transparent;
        --scs-calendar-shadow: none;
        --scs-calendar-pad-end: var(--scs-calendar-pad, 12px);
      }
      /* Its line on the start and on the top, each laid over the panel's own border when it has
         nothing beside it there: a line between the sides, or between calendar and times. */
      .times {
        flex: 1 1 160px;
        min-inline-size: 160px;
        box-sizing: border-box;
        padding: 18px var(--scs-spacing-sm) var(--scs-spacing-sm);
        margin-block-start: -1px;
        margin-inline-start: -1px;
        border-block-start: 1px solid var(--scs-calendar-border, var(--scs-color-border-default));
        border-inline-start: 1px solid var(--scs-calendar-border, var(--scs-color-border-default));
        --scs-time-columns: 1;
        --scs-time-list-height: 314px;
        --scs-time-tick: none;
      }
    `,
  ],
  host: { '[class.off]': 'disabled()' },
  template: `
    <span class="name" [id]="id + '-label'"
      >{{ label() }}
      @if (required()) {
        <span class="required" aria-hidden="true">*</span>
      }
    </span>
    <button
      #trigger
      type="button"
      class="trigger"
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      aria-haspopup="dialog"
      [attr.aria-expanded]="open()"
      [attr.aria-labelledby]="id + '-label ' + id + '-value'"
      [attr.aria-invalid]="error() !== '' ? 'true' : null"
      [attr.aria-required]="required() ? 'true' : null"
      [attr.aria-describedby]="error() ? id + '-error' : hint() ? id + '-hint' : null"
      [attr.data-testid]="testId() || null"
      [attr.data-value]="value()"
      [disabled]="disabled()"
      (click)="toggle()"
      (keydown)="triggerKey($event)"
    >
      <span class="value" [class.empty]="shown() === ''" [id]="id + '-value'">{{
        shown() || placeholder()
      }}</span>
      <span class="marks">
        <scs-icon class="mark" name="calendar" [size]="18" />
        <scs-icon class="mark" name="clock" [size]="18" />
      </span>
    </button>

    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      [cdkConnectedOverlayPush]="true"
      [cdkConnectedOverlayViewportMargin]="8"
      (overlayOutsideClick)="pressedOutside($event)"
      (detach)="close(false)"
      (attach)="calendar()?.focus()"
    >
      <ng-container [ngTemplateOutlet]="panel" />
    </ng-template>
    @if (panelInline()) {
      <div class="inline"><ng-container [ngTemplateOutlet]="panel" /></div>
    }

    <ng-template #panel>
      <div
        class="both"
        role="dialog"
        [attr.aria-labelledby]="id + '-label'"
        [attr.data-testid]="(testId() || 'when') + '-panel'"
        (keydown.tab)="keepFocus($event)"
        (keydown.shift.tab)="keepFocus($event)"
      >
        <div class="sides">
          <scs-calendar
            #calendar
            [value]="day()"
            [min]="min()"
            [max]="max()"
            [today]="today()"
            [digits]="digits()"
            [testId]="(testId() || 'when') + '-calendar'"
            (chosen)="chooseDay($event)"
            (escape)="close(true)"
          />
          <div class="times">
            <scs-time-list
              #times
              [value]="time()"
              [step]="step()"
              [min]="firstTime()"
              [max]="lastTime()"
              [disabledTimes]="disabledTimes()"
              [digits]="digits()"
              [hoverTime]="hoverTime()"
              [labelledBy]="id + '-label'"
              [testId]="(testId() || 'when') + '-times'"
              (chosen)="chooseTime($event)"
              (escape)="close(true)"
            />
          </div>
        </div>
      </div>
    </ng-template>

    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'when') + '-error'">
        {{ error() }}
      </p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'" [attr.data-testid]="(testId() || 'when') + '-hint'">
        {{ hint() }}
      </p>
    }
  `,
})
export class DateTimeFieldComponent {
  protected readonly id = `app-date-time-${nextId++}`;
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);

  readonly label = input.required<string>();
  /** The chosen day and time, `YYYY-MM-DDTHH:mm`; empty while not both are chosen. */
  readonly value = model<DayAndTime>('');
  /** Shown while nothing is chosen: «اختر تاريخاً ووقتاً» · "Select date & time". */
  readonly placeholder = input('');
  readonly required = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly min = input<ScsDay | null>(null);
  readonly max = input<ScsDay | null>(null);
  readonly today = input<ScsDay | null>(null);
  /** Minutes between two times. */
  readonly step = input(30);
  readonly firstTime = input<ScsTime | null>(null);
  readonly lastTime = input<ScsTime | null>(null);
  readonly disabledTimes = input<readonly ScsTime[]>([]);
  readonly digits = input<ScsDigits | null>(null);
  readonly testId = input('');
  /** Shows the panel in place under the trigger, still (the gallery's part). */
  readonly panelInline = input(false, { transform: booleanAttribute });
  /** The time drawn as under the pointer in the panel drawn in place. */
  readonly hoverTime = input<ScsTime | null>(null);

  protected readonly open = signal(false);
  /** The day and the time chosen in the open panel, kept when both are. */
  protected readonly day = linkedSignal<ScsDay | null>(
    () => partsOfDayAndTime(this.value())?.day ?? null,
  );
  protected readonly time = linkedSignal<ScsTime>(
    () => partsOfDayAndTime(this.value())?.time ?? '',
  );
  protected readonly calendar = viewChild<ScsCalendarComponent>('calendar');
  private readonly times = viewChild<ScsTimeListComponent>('times');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  protected readonly shown = computed(() => {
    const parts = partsOfDayAndTime(this.value());
    if (!parts) return '';
    const language = this.languages.language();
    const digits = this.digits() ?? this.consoleDigits;
    return `${formatDay(parts.day, language, digits)} · ${formatTime(parts.time, language, digits)}`;
  });

  protected toggle(): void {
    if (this.open()) {
      this.close(true);
    } else if (!this.disabled()) {
      const parts = partsOfDayAndTime(this.value());
      this.day.set(parts?.day ?? null);
      this.time.set(parts?.time ?? '');
      this.open.set(true);
    }
  }

  protected triggerKey(event: KeyboardEvent): void {
    if ((event.key === 'ArrowDown' || event.key === 'Enter') && !this.open()) {
      event.preventDefault();
      this.toggle();
    }
  }

  protected chooseDay(day: ScsDay): void {
    this.day.set(day);
    if (this.time()) {
      this.keep();
    } else {
      this.times()?.focus();
    }
  }

  protected chooseTime(time: ScsTime): void {
    this.time.set(time);
    if (this.day()) {
      this.keep();
    } else {
      this.calendar()?.focus();
    }
  }

  private keep(): void {
    this.value.set(`${this.day()}T${this.time()}`);
    this.close(true);
  }

  close(giveFocusBack: boolean): void {
    if (!this.open()) return;
    this.open.set(false);
    if (giveFocusBack) this.trigger().nativeElement.focus();
  }

  protected pressedOutside(event: MouseEvent): void {
    if (!this.trigger().nativeElement.contains(event.target as Node)) this.close(false);
  }

  /** Tab goes round the calendar's buttons, its focused day and the times, and does not leave. */
  protected keepFocus(event: Event): void {
    const panel = event.currentTarget as HTMLElement;
    const stops = [
      ...panel.querySelectorAll<HTMLElement>(
        'button:not([tabindex="-1"]):not([disabled]), [role="listbox"]',
      ),
    ];
    if (!stops.length) return;
    const at = stops.indexOf(document.activeElement as HTMLElement);
    event.preventDefault();
    stops[
      (at + ((event as KeyboardEvent).shiftKey ? -1 : 1) + stops.length) % stops.length
    ].focus();
  }
}
