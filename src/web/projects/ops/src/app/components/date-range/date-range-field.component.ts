import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { ChangeDetectionStrategy, Component, ElementRef, booleanAttribute, computed, inject, input, model, signal, viewChild } from '@angular/core';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { SCS_DATE_TRIGGER_STYLES, ScsCalendarComponent, ScsDay, ScsDayRange, ScsDigits, ScsIconComponent, formatRange } from 'ui';

let nextId = 0;

/**
 * A range of days, the operators console's own (design: Date Range Picker — "Date Range Trigger" in
 * six states, `392:53` / `393:112`, and its calendar, `393:2`). The trigger names the range — "Aug 1 –
 * Aug 28, 2026" · «1 أغسطس – 28 أغسطس 2026» — or asks for one; the calendar opens beneath it as the
 * date field's does, takes a start and then an end, and closes when the end is chosen. Escape closes
 * it, and the focus goes back to the trigger.
 */
@Component({
  selector: 'app-date-range-field',
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
      [attr.aria-describedby]="error() ? id + '-error' : hint() ? id + '-hint' : null"
      [attr.data-testid]="testId() || null"
      [attr.data-start]="value()?.start ?? null"
      [attr.data-end]="value()?.end ?? null"
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
        [attr.data-testid]="(testId() || 'range') + '-panel'"
        (keydown.tab)="keepFocus($event)"
        (keydown.shift.tab)="keepFocus($event)"
      >
        <scs-calendar
          #calendar
          mode="range"
          [range]="value()"
          [min]="min()"
          [max]="max()"
          [today]="today()"
          [digits]="digits()"
          [testId]="(testId() || 'range') + '-calendar'"
          (rangeChange)="draft.set($event)"
          (chosen)="keep()"
          (escape)="close(true)"
        />
      </div>
    </ng-template>

    @if (error()) {
      <p class="error" [id]="id + '-error'" [attr.data-testid]="(testId() || 'range') + '-error'">{{ error() }}</p>
    } @else if (hint()) {
      <p class="hint" [id]="id + '-hint'" [attr.data-testid]="(testId() || 'range') + '-hint'">{{ hint() }}</p>
    }
  `,
})
export class DateRangeFieldComponent {
  protected readonly id = `app-date-range-${nextId++}`;
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);

  readonly label = input.required<string>();
  /** The chosen range; `null` while none is. */
  readonly value = model<ScsDayRange | null>(null);
  /** Shown while no range is chosen: «اختر نطاق تاريخ» · "Select a date range". */
  readonly placeholder = input('');
  readonly required = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly error = input('');
  readonly hint = input('');
  readonly min = input<ScsDay | null>(null);
  readonly max = input<ScsDay | null>(null);
  readonly today = input<ScsDay | null>(null);
  readonly digits = input<ScsDigits | null>(null);
  readonly testId = input('');

  protected readonly open = signal(false);
  /** The range being chosen: its start until its end is chosen too. */
  protected readonly draft = signal<ScsDayRange | null>(null);
  protected readonly calendar = viewChild<ScsCalendarComponent>('calendar');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  protected readonly shown = computed(() => {
    const range = this.value();
    return range?.end ? formatRange(range, this.languages.language(), this.digits() ?? this.consoleDigits) : '';
  });

  protected toggle(): void {
    if (this.open()) {
      this.close(true);
    } else if (!this.disabled()) {
      this.draft.set(this.value());
      this.open.set(true);
    }
  }

  protected triggerKey(event: KeyboardEvent): void {
    if ((event.key === 'ArrowDown' || event.key === 'Enter') && !this.open()) {
      event.preventDefault();
      this.toggle();
    }
  }

  /** The end is chosen: the range is kept and the calendar closes. */
  protected keep(): void {
    const range = this.draft();
    if (range?.end) this.value.set(range);
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
    event.preventDefault();
    stops[(at + ((event as KeyboardEvent).shiftKey ? -1 : 1) + stops.length) % stops.length].focus();
  }
}
