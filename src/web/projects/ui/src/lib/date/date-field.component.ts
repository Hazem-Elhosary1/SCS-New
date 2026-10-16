import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsDay, ScsDigits, formatDay, isDay } from './calendar';
import { ScsCalendarComponent } from './calendar.component';
import { SCS_DATE_TRIGGER_STYLES } from './date-trigger.styles';

let nextId = 0;

/**
 * One day, chosen from a calendar (design: Date Picker — "Date Field Trigger" in six states,
 * `74:53` / `308:52`, and the "Calendar Popover", `75:4`). The trigger is a button that names the
 * chosen day — "Aug 28, 2026" · «28 أغسطس 2026» — or asks for one; Down or Enter opens the calendar
 * beneath it, with the focus on the chosen day (or today). Choosing a day closes it; Escape closes
 * it too, and the focus goes back to the trigger. Tab moves between the calendar's buttons and its
 * days without leaving it.
 */
@Component({
  selector: 'scs-date-field',
  imports: [ScsIconComponent, ScsCalendarComponent, CdkOverlayOrigin, CdkConnectedOverlay],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: SCS_DATE_TRIGGER_STYLES,
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
      aria-haspopup="dialog"
      [attr.aria-expanded]="open()"
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
      <scs-icon class="mark" name="calendar" [size]="18" />
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
      <div
        role="dialog"
        [attr.aria-labelledby]="id + '-label'"
        [attr.data-testid]="(testId() || 'date') + '-panel'"
        (keydown.tab)="keepFocus($event)"
        (keydown.shift.tab)="keepFocus($event)"
      >
        <scs-calendar
          #calendar
          [value]="day()"
          [min]="min()"
          [max]="max()"
          [disabledDays]="disabledDays()"
          [weekStart]="weekStart()"
          [digits]="digits()"
          [today]="today()"
          [todayLink]="todayLink()"
          [testId]="(testId() || 'date') + '-calendar'"
          (chosen)="choose($event)"
          (escape)="close(true)"
        />
      </div>
    </ng-template>
    @if (calendarInline()) {
      <div class="inline">
        <scs-calendar
          [value]="day()"
          [min]="min()"
          [max]="max()"
          [disabledDays]="disabledDays()"
          [weekStart]="weekStart()"
          [digits]="digits()"
          [today]="today()"
          [todayLink]="todayLink()"
          [hoverDay]="hoverDay()"
          [testId]="(testId() || 'date') + '-calendar'"
          (chosen)="value.set($event)"
        />
      </div>
    }

    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'date') + '-error'">{{ error() }}</p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'" [attr.data-testid]="(testId() || 'date') + '-hint'">{{ hint() }}</p>
    }
  `,
})
export class ScsDateFieldComponent {
  protected readonly id = `scs-date-${nextId++}`;
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);

  readonly label = input.required<string>();
  /** The chosen day, `YYYY-MM-DD`; empty while none is. */
  readonly value = model<ScsDay>('');
  /** Shown while no day is chosen: «اختر تاريخاً» · "Select a date". */
  readonly placeholder = input('');
  readonly required = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly min = input<ScsDay | null>(null);
  readonly max = input<ScsDay | null>(null);
  readonly disabledDays = input<readonly ScsDay[]>([]);
  readonly weekStart = input<number | null>(null);
  readonly digits = input<ScsDigits | null>(null);
  readonly today = input<ScsDay | null>(null);
  readonly testId = input('');
  /** A link to today under the calendar (the clinic's draws one). */
  readonly todayLink = input(false, { transform: booleanAttribute });
  /** Shows the calendar open in place under the trigger, still (the gallery's parts). */
  readonly calendarInline = input(false, { transform: booleanAttribute });
  /** The day drawn as under the pointer in the calendar drawn in place. */
  readonly hoverDay = input<ScsDay | null>(null);

  protected readonly open = signal(false);
  protected readonly calendar = viewChild<ScsCalendarComponent>('calendar');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');

  /** Beneath the trigger at the reading start; above it when there is no room. */
  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  protected readonly day = computed(() => (isDay(this.value()) ? this.value() : null));

  /** The chosen day as the trigger writes it. */
  protected readonly shown = computed(() => {
    const day = this.day();
    return day ? formatDay(day, this.languages.language(), this.digits() ?? this.consoleDigits) : '';
  });

  protected describedBy(): string | null {
    return this.error() ? `${this.id}-error` : this.hint() ? `${this.id}-hint` : null;
  }

  protected toggle(): void {
    if (this.open()) {
      this.close(true);
    } else if (!this.disabled()) {
      this.open.set(true);
    }
  }

  protected triggerKey(event: KeyboardEvent): void {
    if ((event.key === 'ArrowDown' || event.key === 'Enter') && !this.open()) {
      event.preventDefault();
      this.toggle();
    }
  }

  protected choose(day: ScsDay): void {
    this.value.set(day);
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

  /** Tab goes round the calendar's buttons and its focused day, and does not leave it. */
  protected keepFocus(event: Event): void {
    const panel = event.currentTarget as HTMLElement;
    const stops = [...panel.querySelectorAll<HTMLElement>('button:not([tabindex="-1"]):not([disabled])')];
    if (!stops.length) return;
    const at = stops.indexOf(document.activeElement as HTMLElement);
    const back = (event as KeyboardEvent).shiftKey;
    event.preventDefault();
    stops[(at + (back ? -1 : 1) + stops.length) % stops.length].focus();
  }
}
