import { ChangeDetectionStrategy, Component, ElementRef, booleanAttribute, inject, input, model } from '@angular/core';

/** One segment of a segmented control. */
export interface ScsSegment {
  readonly value: string;
  readonly label: string;
  readonly disabled?: boolean;
}

/**
 * A few views of the same thing, one at a time (design: Segmented Control — ops `385:2`, clinic
 * `4260:1448`; spec 095): a track holding pills, the chosen one white and raised. It is a radio group:
 * the arrows move to the next segment and choose it, Home and End to the ends; a disabled segment is
 * skipped.
 */
@Component({
  selector: 'scs-segmented',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-block;
      max-inline-size: 100%;
    }
    .track {
      display: inline-flex;
      gap: 0;
      padding: 4px;
      border-radius: calc(max(var(--scs-touch, 0px), 28px) / 2 + 4px);
      background: var(--scs-segment-track, var(--scs-color-bg-muted));
      max-inline-size: 100%;
      overflow-x: auto;
    }
    button {
      flex: none;
      min-inline-size: 80px;
      block-size: max(var(--scs-touch, 0px), 28px);
      padding: 0 var(--scs-spacing-md);
      border: 0;
      border-radius: calc(max(var(--scs-touch, 0px), 28px) / 2 + 2px);
      background: none;
      color: var(--scs-color-text-tertiary);
      font: inherit;
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
      cursor: pointer;
    }
    button[aria-checked='true'] {
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font-weight: 600;
      box-shadow: 0 1px 3px rgb(0 0 0 / 12%);
    }
    button:not([aria-checked='true']):not(:disabled):hover {
      color: var(--scs-color-text-primary);
    }
    button:disabled {
      color: var(--scs-color-text-disabled, var(--scs-color-text-tertiary));
      cursor: not-allowed;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-focus-ring, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
  `,
  template: `
    <div
      class="track"
      role="radiogroup"
      [attr.aria-label]="ariaLabel() || null"
      [attr.data-testid]="testId() || null"
    >
      @for (segment of segments(); track segment.value) {
        <button
          type="button"
          role="radio"
          [attr.aria-checked]="segment.value === value()"
          [attr.tabindex]="segment.value === value() ? 0 : -1"
          [disabled]="disabled() || segment.disabled"
          [attr.data-value]="segment.value"
          [attr.data-testid]="testId() ? testId() + '-' + segment.value : null"
          (click)="value.set(segment.value)"
          (keydown)="key($event)"
        >
          {{ segment.label }}
        </button>
      }
    </div>
  `,
})
export class ScsSegmentedComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly segments = input.required<readonly ScsSegment[]>();
  readonly value = model<string>('');
  readonly disabled = input(false, { transform: booleanAttribute });
  /** What the segments choose between, for a screen reader. */
  readonly ariaLabel = input('');
  readonly testId = input('');

  protected key(event: KeyboardEvent): void {
    const usable = this.segments().filter((segment) => !segment.disabled);
    if (!usable.length || this.disabled()) return;
    const rtl = getComputedStyle(this.host.nativeElement).direction === 'rtl';
    const at = Math.max(0, usable.findIndex((segment) => segment.value === this.value()));
    const forward = ['ArrowDown', rtl ? 'ArrowLeft' : 'ArrowRight'];
    const back = ['ArrowUp', rtl ? 'ArrowRight' : 'ArrowLeft'];
    let next: number | null = null;
    if (forward.includes(event.key)) next = (at + 1) % usable.length;
    else if (back.includes(event.key)) next = (at - 1 + usable.length) % usable.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = usable.length - 1;
    if (next === null) return;
    event.preventDefault();
    this.value.set(usable[next].value);
    queueMicrotask(() =>
      this.host.nativeElement.querySelector<HTMLButtonElement>(`button[data-value="${usable[next].value}"]`)?.focus(),
    );
  }
}
