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
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { ScsButtonDirective } from '../controls/controls';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsDigits } from './calendar';
import { ScsTime, clockOf, formatTime, fromClock, isTime, periodNames, twoDigits } from './time';
import { ScsTimeListComponent } from './time-list.component';
import { SCS_DATE_TRIGGER_STYLES } from './date-trigger.styles';

let nextId = 0;

/** One place on a wheel: two above the chosen, the chosen, two below. */
interface Turn {
  readonly label: string;
  readonly offset: number;
}

/**
 * A time of day (design: Time Picker — "Time Field Trigger" in six states, `77:53` / `308:103`; the
 * open "Time List", `78:4`, and the "Time Spinner", `78:16`; the clinic's Time Input `4256:1542` and
 * Time Slot `4256:1480`). The trigger names the chosen time — "2:30 PM" · «2:30 م» — or asks for one;
 * Down or Enter opens beneath it either a list of times every `step` minutes (`mode` list: the arrows
 * move, Home and End jump, Enter chooses) or three wheels — hour, minute, and the half of the day —
 * turned with the arrows and kept with "Done" (`mode` wheel). Escape closes without choosing, and the
 * focus goes back to the trigger.
 */
@Component({
  selector: 'scs-time-field',
  imports: [ScsIconComponent, ScsTimeListComponent, ScsButtonDirective, TranslocoPipe, CdkOverlayOrigin, CdkConnectedOverlay, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    SCS_DATE_TRIGGER_STYLES,
    `
    .panel {
      box-sizing: border-box;
      inline-size: var(--scs-time-panel-width, var(--trigger-width, 100%));
      padding: var(--scs-spacing-sm);
      border: 1px solid var(--scs-time-border, var(--scs-color-border-default));
      border-radius: var(--scs-time-radius, 10px);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-time-shadow, 0 4px 10px rgb(10 15 20 / 8%), 0 2px 4px rgb(10 15 20 / 4%));
    }
    /* The wheels (78:16): three columns of five, the chosen row banded, "Done" under them. */
    .wheels {
      position: relative;
      display: flex;
      justify-content: center;
      padding-block: var(--scs-spacing-xs);
    }
    .wheels::before {
      content: '';
      position: absolute;
      inset-inline: 0;
      inset-block-start: calc(var(--scs-spacing-xs) + 80px);
      block-size: 40px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-time-hover-bg, var(--scs-color-brand-subtle));
    }
    .wheel {
      position: relative;
      display: grid;
      grid-template-rows: repeat(5, 40px);
      inline-size: 70px;
      border-radius: var(--scs-radius-md);
      outline: none;
      cursor: ns-resize;
    }
    .wheel:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
    }
    .turn {
      display: grid;
      place-items: center;
      block-size: 40px;
      font-size: 14px;
      color: var(--scs-color-text-disabled, var(--scs-color-text-placeholder));
    }
    .turn[data-offset='0'] {
      font-size: 16px;
      font-weight: 700;
      color: var(--scs-color-text-primary);
    }
    /* The wheels' panel is as wide as its wheels; the colon sits on the line between hour and minute. */
    .panel.turning {
      inline-size: max-content;
      padding: var(--scs-spacing-12, 12px);
    }
    .colon {
      position: relative;
      display: grid;
      place-items: center;
      align-self: center;
      inline-size: 0;
      font-size: 16px;
      font-weight: 700;
      text-align: center;
    }
    .done {
      inline-size: 100%;
      margin-block-start: var(--scs-spacing-sm);
    }
  `,
  ],
  host: { '[class.off]': 'disabled()' },
  template: `
    <span class="name" [id]="id + '-label'"
      >{{ label() }}
      @if (required()) {
        <span class="required" aria-hidden="true">*</span>
      }</span
    >
    <button
      #trigger
      type="button"
      class="trigger"
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      [attr.aria-haspopup]="mode() === 'list' ? 'listbox' : 'dialog'"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="open() ? id + '-panel' : null"
      [attr.aria-labelledby]="id + '-label ' + id + '-value'"
      [attr.aria-invalid]="error() !== '' ? 'true' : null"
      [attr.aria-required]="required() ? 'true' : null"
      [attr.aria-describedby]="describedBy()"
      [attr.data-testid]="testId() || null"
      [attr.data-value]="value()"
      [disabled]="disabled()"
      (click)="toggle()"
      (keydown)="triggerKey($event)"
    >
      <span class="value" [class.empty]="shown() === ''" [id]="id + '-value'">{{ shown() || placeholder() }}</span>
      <scs-icon class="mark" name="clock" [size]="18" />
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
      (attach)="opened()"
    >
      <ng-container [ngTemplateOutlet]="panel" />
    </ng-template>
    @if (panelInline()) {
      <div class="inline"><ng-container [ngTemplateOutlet]="panel" /></div>
    }

    <ng-template #panel>
      <div
        #panelBox
        class="panel"
        [class.turning]="mode() === 'wheel'"
        [id]="id + '-panel'"
        [style.--trigger-width.px]="width()"
        [attr.role]="mode() === 'wheel' ? 'dialog' : null"
        [attr.aria-labelledby]="mode() === 'wheel' ? id + '-label' : null"
        [attr.data-testid]="(testId() || 'time') + '-panel'"
      >
        @if (mode() === 'list') {
          <scs-time-list
            [value]="value()"
            [step]="step()"
            [min]="min()"
            [max]="max()"
            [disabledTimes]="disabledTimes()"
            [digits]="digits()"
            [hoverTime]="hoverTime()"
            [labelledBy]="id + '-label'"
            [testId]="(testId() || 'time') + '-list'"
            (chosen)="choose($event)"
            (escape)="close(true)"
            (leave)="close(false)"
          />
        } @else {
          <div class="wheels">
            <div
              class="wheel"
              role="spinbutton"
              tabindex="0"
              [attr.aria-label]="'ui.date.hour' | transloco"
              [attr.aria-valuenow]="draft().hour"
              aria-valuemin="1"
              aria-valuemax="12"
              [attr.data-testid]="(testId() || 'time') + '-hour'"
              (keydown)="wheelKey($event, 'hour')"
              (wheel)="spin($event, 'hour')"
              (click)="pick($event, 'hour')"
            >
              @for (turn of hours(); track turn.offset) {
                <span class="turn" [attr.data-offset]="turn.offset" [style.grid-row]="turn.offset + 3">{{ turn.label }}</span>
              }
            </div>
            <span class="colon" aria-hidden="true">:</span>
            <div
              class="wheel"
              role="spinbutton"
              tabindex="0"
              [attr.aria-label]="'ui.date.minute' | transloco"
              [attr.aria-valuenow]="draft().minute"
              aria-valuemin="0"
              aria-valuemax="59"
              [attr.data-testid]="(testId() || 'time') + '-minute'"
              (keydown)="wheelKey($event, 'minute')"
              (wheel)="spin($event, 'minute')"
              (click)="pick($event, 'minute')"
            >
              @for (turn of minutes(); track turn.offset) {
                <span class="turn" [attr.data-offset]="turn.offset" [style.grid-row]="turn.offset + 3">{{ turn.label }}</span>
              }
            </div>
            <div
              class="wheel"
              role="spinbutton"
              tabindex="0"
              [attr.aria-label]="'ui.date.period' | transloco"
              [attr.aria-valuenow]="draft().pm ? 1 : 0"
              [attr.aria-valuetext]="periods()[draft().pm ? 1 : 0]"
              aria-valuemin="0"
              aria-valuemax="1"
              [attr.data-testid]="(testId() || 'time') + '-period'"
              (keydown)="wheelKey($event, 'period')"
              (wheel)="spin($event, 'period')"
              (click)="pick($event, 'period')"
            >
              @for (turn of halves(); track turn.offset) {
                <span class="turn" [attr.data-offset]="turn.offset" [style.grid-row]="turn.offset + 3">{{ turn.label }}</span>
              }
            </div>
          </div>
          <button
            type="button"
            class="done"
            scsButton="primary"
            size="sm"
            [attr.data-testid]="(testId() || 'time') + '-done'"
            (click)="keep()"
            (keydown.escape)="close(true)"
          >
            {{ 'ui.date.done' | transloco }}
          </button>
        }
      </div>
    </ng-template>

    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'time') + '-error'">{{ error() }}</p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'" [attr.data-testid]="(testId() || 'time') + '-hint'">{{ hint() }}</p>
    }
  `,
})
export class ScsTimeFieldComponent {
  protected readonly id = `scs-time-${nextId++}`;
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);

  readonly label = input.required<string>();
  /** The chosen time, `HH:mm`; empty while none is. */
  readonly value = model<ScsTime>('');
  /** Shown while no time is chosen: «اختر الوقت» · "Select a time". */
  readonly placeholder = input('');
  /** A list of times, or three wheels. */
  readonly mode = input<'list' | 'wheel'>('list');
  /** Minutes between two times of the list, and between two minutes of the wheel. */
  readonly step = input(30);
  /** The first and last times of the list. */
  readonly min = input<ScsTime | null>(null);
  readonly max = input<ScsTime | null>(null);
  /** Times of the list that may not be chosen. */
  readonly disabledTimes = input<readonly ScsTime[]>([]);
  readonly required = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly digits = input<ScsDigits | null>(null);
  readonly testId = input('');
  /** Shows the open panel in place under the trigger, still (the gallery's parts). */
  readonly panelInline = input(false, { transform: booleanAttribute });
  /** The time of the list drawn as under the pointer in the panel drawn in place. */
  readonly hoverTime = input<ScsTime | null>(null);

  protected readonly open = signal(false);
  protected readonly width = signal<number | null>(null);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panelBox = viewChild<ElementRef<HTMLElement>>('panelBox');
  private readonly list = viewChild(ScsTimeListComponent);

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  private readonly language = computed(() => this.languages.language());
  private readonly arabicDigits = computed(() => this.digits() ?? this.consoleDigits);
  private readonly time = computed(() => (isTime(this.value()) ? this.value() : null));

  protected readonly shown = computed(() => {
    const time = this.time();
    return time ? formatTime(time, this.language(), this.arabicDigits()) : '';
  });

  /** The wheels' time until "Done": the chosen time, or the next step from midnight. */
  protected readonly draft = linkedSignal(() => clockOf(this.time() ?? '09:00'));
  protected readonly periods = computed(() => periodNames(this.language()));

  private around(count: number, at: number, label: (index: number) => string): Turn[] {
    return [-2, -1, 0, 1, 2].map((offset) => ({ offset, label: label((((at + offset) % count) + count) % count) }));
  }

  protected readonly hours = computed(() =>
    this.around(12, this.draft().hour - 1, (index) => twoDigits(index + 1, this.language(), this.arabicDigits())),
  );
  protected readonly minutes = computed(() => {
    const step = Math.max(1, this.step());
    return this.around(Math.ceil(60 / step), Math.floor(this.draft().minute / step), (index) =>
      twoDigits(index * step, this.language(), this.arabicDigits()),
    );
  });
  /** The half of the day does not turn round: the morning above the afternoon. */
  protected readonly halves = computed<Turn[]>(() => {
    const [am, pm] = this.periods();
    return this.draft().pm ? [{ offset: -1, label: am }, { offset: 0, label: pm }] : [{ offset: 0, label: am }, { offset: 1, label: pm }];
  });

  protected describedBy(): string | null {
    return this.error() ? `${this.id}-error` : this.hint() ? `${this.id}-hint` : null;
  }

  protected toggle(): void {
    if (this.open()) {
      this.close(true);
    } else if (!this.disabled()) {
      this.width.set(this.trigger().nativeElement.getBoundingClientRect().width);
      this.draft.set(clockOf(this.time() ?? '09:00'));
      this.open.set(true);
    }
  }

  /** The panel is in the page: the focus goes into it, and the chosen time is in view. */
  protected opened(): void {
    if (this.mode() === 'list') {
      this.list()?.focus();
    } else {
      setTimeout(() => this.panelBox()?.nativeElement.querySelector<HTMLElement>('[role="spinbutton"]')?.focus());
    }
  }

  protected triggerKey(event: KeyboardEvent): void {
    if ((event.key === 'ArrowDown' || event.key === 'Enter') && !this.open()) {
      event.preventDefault();
      this.toggle();
    }
  }

  close(giveFocusBack: boolean): void {
    if (!this.open()) return;
    this.open.set(false);
    if (giveFocusBack) this.trigger().nativeElement.focus();
  }

  protected pressedOutside(event: MouseEvent): void {
    if (!this.trigger().nativeElement.contains(event.target as Node)) this.close(false);
  }

  protected choose(time: ScsTime): void {
    this.value.set(time);
    this.close(true);
  }

  /** Turns a wheel by so many places: down is later. */
  protected turnBy(wheel: 'hour' | 'minute' | 'period', places: number): void {
    const { hour, minute, pm } = this.draft();
    const step = Math.max(1, this.step());
    if (wheel === 'hour') {
      this.draft.set({ hour: ((((hour - 1 + places) % 12) + 12) % 12) + 1, minute, pm });
    } else if (wheel === 'minute') {
      const count = Math.ceil(60 / step);
      const at = (((Math.floor(minute / step) + places) % count) + count) % count;
      this.draft.set({ hour, minute: at * step, pm });
    } else if (places !== 0) {
      this.draft.set({ hour, minute, pm: places > 0 });
    }
  }

  protected wheelKey(event: KeyboardEvent, wheel: 'hour' | 'minute' | 'period'): void {
    const places: Record<string, number> = { ArrowDown: 1, ArrowUp: -1, PageDown: 2, PageUp: -2 };
    if (places[event.key] !== undefined) {
      event.preventDefault();
      this.turnBy(wheel, places[event.key]);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.keep();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.close(true);
    }
  }

  /** A place pressed on a wheel becomes its chosen one. */
  protected pick(event: MouseEvent, wheel: 'hour' | 'minute' | 'period'): void {
    const offset = (event.target as HTMLElement).closest<HTMLElement>('[data-offset]')?.dataset['offset'];
    if (offset !== undefined) this.turnBy(wheel, Number(offset));
  }

  protected spin(event: WheelEvent, wheel: 'hour' | 'minute' | 'period'): void {
    event.preventDefault();
    this.turnBy(wheel, Math.sign(event.deltaY));
  }

  /** "Done": the wheels' time is chosen. */
  protected keep(): void {
    const { hour, minute, pm } = this.draft();
    this.value.set(fromClock(hour, minute, pm));
    this.close(true);
  }
}
