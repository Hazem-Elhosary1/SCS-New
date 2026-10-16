import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { LanguageService, SCS_ARABIC_DIGITS } from 'core';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsDigits } from './calendar';
import { ScsTime, formatTime, timesBetween } from './time';

let nextId = 0;

/** One row of the list. */
interface Row {
  readonly time: ScsTime;
  readonly id: string;
  readonly label: string;
  readonly off: boolean;
}

/**
 * Times every `step` minutes to choose one from (design: Time Picker — "Time List", `78:4`; the
 * time side of the Date & Time panel, `80:4`; the clinic's Time Slot, `4256:1480`). A listbox: the
 * arrows move, Home and End jump, Enter or Space chooses; a refused time is grey and cannot be
 * chosen. Its look comes from the `--scs-time-*` settings: the clinic's two columns by default.
 */
@Component({
  selector: 'scs-time-list',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    [role='listbox'] {
      display: grid;
      grid-template-columns: repeat(var(--scs-time-columns, 2), minmax(0, 1fr));
      gap: var(--scs-time-gap, 4px);
      max-block-size: var(--scs-time-list-height, 240px);
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
      outline: none;
    }
    [role='listbox']:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: 2px;
    }
    [role='option'] {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      block-size: var(--scs-time-row, 36px);
      padding-inline: var(--scs-time-row-pad, 12px);
      border-radius: var(--scs-time-row-radius, 6px);
      color: var(--scs-color-text-primary);
      font-size: 13px;
      font-weight: var(--scs-time-row-weight, 500);
      cursor: pointer;
    }
    .words {
      flex: 1;
    }
    [role='option'][data-active='true'],
    [role='option'][data-still='hover'] {
      background: var(--scs-time-hover-bg, var(--scs-color-bg-surface-secondary));
    }
    [role='option'][aria-selected='true'] {
      background: var(--scs-time-chosen-bg, var(--scs-color-brand-subtle));
      color: var(--scs-time-chosen-color, var(--scs-color-brand-active));
      font-weight: var(--scs-time-chosen-weight, 500);
    }
    .tick {
      display: var(--scs-time-tick, none);
    }
    [role='option'][aria-disabled='true'] {
      background: none;
      color: var(--scs-color-text-placeholder);
      cursor: not-allowed;
    }
  `,
  template: `
    <ul
      role="listbox"
      tabindex="0"
      [attr.aria-labelledby]="labelledBy() || null"
      [attr.aria-label]="labelledBy() ? null : label() || null"
      [attr.aria-activedescendant]="rows()[active()]?.id ?? null"
      [attr.data-testid]="testId() || null"
      (keydown)="key($event)"
    >
      @for (row of rows(); track row.time; let index = $index) {
        <li
          role="option"
          tabindex="-1"
          [id]="row.id"
          [attr.data-time]="row.time"
          [attr.aria-selected]="row.time === value()"
          [attr.aria-disabled]="row.off ? 'true' : null"
          [attr.data-active]="index === active()"
          [attr.data-still]="row.time === hoverTime() ? 'hover' : null"
          (pointerenter)="active.set(index)"
          (click)="choose(row)"
          (keydown.enter)="choose(row)"
        >
          <span class="words">{{ row.label }}</span>
          @if (row.time === value()) {
            <scs-icon class="tick" name="check" [size]="14" />
          }
        </li>
      }
    </ul>
  `,
})
export class ScsTimeListComponent {
  private readonly id = `scs-time-list-${nextId++}`;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly languages = inject(LanguageService);
  private readonly consoleDigits = inject(SCS_ARABIC_DIGITS);

  /** The chosen time, `HH:mm`. */
  readonly value = model<ScsTime>('');
  readonly step = input(30);
  readonly min = input<ScsTime | null>(null);
  readonly max = input<ScsTime | null>(null);
  readonly disabledTimes = input<readonly ScsTime[]>([]);
  readonly digits = input<ScsDigits | null>(null);
  /** The id of what names the list, or its name. */
  readonly labelledBy = input('');
  readonly label = input('');
  /** Draws this time as under the pointer (the gallery's still parts). */
  readonly hoverTime = input<ScsTime | null>(null);
  readonly testId = input('');

  /** A time was chosen. */
  readonly chosen = output<ScsTime>();
  /** Escape was pressed. */
  readonly escape = output<void>();
  /** Tab is leaving the list. */
  readonly leave = output<void>();

  protected readonly active = signal(-1);

  protected readonly rows = computed<Row[]>(() =>
    timesBetween(this.step(), this.min() ?? '00:00', this.max() ?? '23:59').map((time) => ({
      time,
      id: `${this.id}-${time.replace(':', '')}`,
      label: formatTime(time, this.languages.language(), this.digits() ?? this.consoleDigits),
      off: this.disabledTimes().includes(time),
    })),
  );

  /** The chosen time, or the first that may be chosen, becomes active; the list takes the focus. */
  focus(): void {
    const chosen = this.rows().findIndex((row) => row.time === this.value());
    this.active.set(chosen >= 0 ? chosen : this.next(-1, 1));
    setTimeout(() => {
      this.host.nativeElement.querySelector<HTMLElement>('[role="listbox"]')?.focus();
      this.reveal();
    });
  }

  protected choose(row: Row): void {
    if (row.off) return;
    this.value.set(row.time);
    this.chosen.emit(row.time);
  }

  protected key(event: KeyboardEvent): void {
    const rows = this.rows();
    const moves: Record<string, () => number> = {
      ArrowDown: () => this.next(this.active(), 1),
      ArrowUp: () => this.next(this.active(), -1),
      Home: () => this.next(-1, 1),
      End: () => this.next(rows.length, -1),
    };
    if (moves[event.key]) {
      event.preventDefault();
      this.active.set(moves[event.key]());
      this.reveal();
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (rows[this.active()]) this.choose(rows[this.active()]);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.escape.emit();
    } else if (event.key === 'Tab') {
      this.leave.emit();
    }
  }

  /** The next time that may be chosen, from `from` in `step`'s direction; `from` itself if none. */
  private next(from: number, step: 1 | -1): number {
    const rows = this.rows();
    for (let at = from + step; at >= 0 && at < rows.length; at += step) {
      if (!rows[at].off) return at;
    }
    return from >= 0 && from < rows.length ? from : -1;
  }

  private reveal(): void {
    const row = this.rows()[this.active()];
    if (row) setTimeout(() => document.getElementById(row.id)?.scrollIntoView({ block: 'nearest' }));
  }
}
