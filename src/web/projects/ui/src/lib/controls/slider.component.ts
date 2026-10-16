import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
  model,
  numberAttribute,
  viewChild,
} from '@angular/core';

let nextId = 0;

/**
 * A value, or a range of values, chosen along a line (design: Slider — ops `55:58`, clinic
 * `4260:809`; spec 095). Single or range, default, hover, focus, disabled; the value written above
 * the line. Each thumb is a `role="slider"`: the arrows move it by a step, Page Up and Page Down by a
 * tenth, Home and End to the ends; a range's two thumbs never cross. In Arabic it fills from the right.
 */
@Component({
  selector: 'scs-slider',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-inline-size: 0;
    }
    .label {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: var(--scs-field-label-weight, 600);
      color: var(--scs-field-label-color, var(--scs-color-text-primary));
    }
    output {
      display: block;
      font-size: var(--scs-text-label);
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    .off output {
      color: var(--scs-color-text-disabled, var(--scs-color-text-placeholder, #9facb5));
    }
    /* The line is 4 high; the room around it makes each thumb at least 40 by 40 to press. */
    .line {
      position: relative;
      block-size: 40px;
      touch-action: none;
      cursor: pointer;
    }
    .off .line {
      cursor: not-allowed;
    }
    .track,
    .fill {
      position: absolute;
      inset-block-start: 18px;
      block-size: 4px;
      border-radius: 2px;
    }
    .track {
      inset-inline: 0;
      background: var(--scs-slider-track, var(--scs-color-border-control, var(--scs-color-border-default)));
    }
    .fill {
      background: var(--scs-color-brand-default);
    }
    .off .track {
      background: var(--scs-color-bg-muted);
    }
    /* Disabled, as the clinic file draws it (4260:809): a pale fill, a light knob ring. */
    .off .fill {
      background: var(--scs-slider-off, var(--scs-color-fill-strong, #e3e8ec));
    }
    .thumb {
      position: absolute;
      inset-block-start: 0;
      display: grid;
      place-items: center;
      inline-size: 40px;
      block-size: 40px;
      margin-inline-start: -20px;
      outline: none;
    }
    .knob {
      box-sizing: border-box;
      inline-size: 18px;
      block-size: 18px;
      border: 2.5px solid var(--scs-color-brand-default);
      border-radius: 50%;
      background: var(--scs-color-bg-surface);
      transition: inline-size 0.1s, block-size 0.1s;
    }
    .off .knob {
      border-color: var(--scs-slider-off, var(--scs-color-border-strong, #cbd3d9));
    }
    .on .thumb:hover .knob,
    :host-context([data-scs-state~='hover']) .knob {
      inline-size: 21px;
      block-size: 21px;
    }
    .thumb:focus-visible .knob,
    :host-context([data-scs-state~='focus']) .thumb:last-child .knob {
      outline: 2px solid var(--scs-color-focus-ring, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    @media (prefers-reduced-motion: reduce) {
      .knob {
        transition: none;
      }
    }
  `,
  template: `
    <div [class.off]="disabled()" [class.on]="!disabled()">
      @if (label()) {
        <span class="label" [id]="id + '-label'">{{ label() }}</span>
      }
      <output [attr.for]="id" [attr.data-testid]="testId() ? testId() + '-value' : null"
        ><bdi dir="ltr">{{ written() }}</bdi></output
      >
      <div #line class="line" [attr.data-testid]="testId() || null" (pointerdown)="press($event)">
        <span class="track"></span>
        <span class="fill" [style.inset-inline-start.%]="fillStart()" [style.inline-size.%]="fillSize()"></span>
        @for (thumb of thumbs(); track thumb.index) {
          <span
            class="thumb"
            role="slider"
            [id]="thumb.index === 0 ? id : id + '-' + thumb.index"
            [style.inset-inline-start.%]="thumb.at"
            [attr.tabindex]="disabled() ? -1 : 0"
            [attr.aria-valuemin]="thumb.min"
            [attr.aria-valuemax]="thumb.max"
            [attr.aria-valuenow]="thumb.value"
            [attr.aria-valuetext]="format()(thumb.value)"
            [attr.aria-labelledby]="label() ? id + '-label' : null"
            [attr.aria-label]="thumb.name"
            [attr.aria-disabled]="disabled() ? 'true' : null"
            [attr.data-testid]="testId() ? testId() + '-thumb-' + thumb.index : null"
            (keydown)="key($event, thumb.index)"
            (pointerdown)="grab($event, thumb.index)"
            ><span class="knob"></span
          ></span>
        }
      </div>
    </div>
  `,
})
export class ScsSliderComponent {
  protected readonly id = `scs-slider-${nextId++}`;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly line = viewChild.required<ElementRef<HTMLElement>>('line');

  readonly label = input('');
  readonly min = input(0, { transform: numberAttribute });
  readonly max = input(100, { transform: numberAttribute });
  readonly step = input(1, { transform: numberAttribute });
  /** A single value… */
  readonly value = model(0);
  /** …or, when given, a range [start, end] with two thumbs. */
  readonly range = model<readonly [number, number] | null>(null);
  readonly disabled = input(false, { transform: booleanAttribute });
  /** How a value is written, above the line and to a screen reader. */
  readonly format = input<(value: number) => string>((value) => String(value));
  /** The names of a range's two thumbs for a screen reader: "from", "to". */
  readonly thumbNames = input<readonly [string, string]>(['', '']);
  readonly testId = input('');

  private dragging: number | null = null;

  private readonly values = computed<number[]>(() => {
    const range = this.range();
    return range ? [range[0], range[1]] : [this.value()];
  });

  private readonly share = (value: number) =>
    ((value - this.min()) / Math.max(this.max() - this.min(), 1e-9)) * 100;

  protected readonly thumbs = computed(() => {
    const values = this.values();
    const ranged = values.length === 2;
    return values.map((value, index) => ({
      index,
      value,
      at: this.share(value),
      min: ranged && index === 1 ? values[0] : this.min(),
      max: ranged && index === 0 ? values[1] : this.max(),
      name: ranged ? this.thumbNames()[index] || null : null,
    }));
  });

  protected readonly fillStart = computed(() => (this.values().length === 2 ? this.share(this.values()[0]) : 0));
  protected readonly fillSize = computed(() => {
    const values = this.values();
    return values.length === 2 ? this.share(values[1]) - this.share(values[0]) : this.share(values[0]);
  });

  protected readonly written = computed(() => this.values().map((value) => this.format()(value)).join(' – '));

  protected key(event: KeyboardEvent, index: number): void {
    if (this.disabled()) return;
    const rtl = getComputedStyle(this.host.nativeElement).direction === 'rtl';
    const current = this.values()[index];
    const big = Math.max(this.step(), (this.max() - this.min()) / 10);
    const moves: Record<string, number> = {
      ArrowUp: this.step(),
      ArrowDown: -this.step(),
      ArrowRight: rtl ? -this.step() : this.step(),
      ArrowLeft: rtl ? this.step() : -this.step(),
      PageUp: big,
      PageDown: -big,
    };
    let next: number | null = null;
    if (event.key in moves) next = current + moves[event.key];
    else if (event.key === 'Home') next = -Infinity;
    else if (event.key === 'End') next = Infinity;
    if (next === null) return;
    event.preventDefault();
    this.set(index, next);
  }

  protected grab(event: PointerEvent, index: number): void {
    if (this.disabled()) return;
    event.stopPropagation();
    this.dragging = index;
    this.follow(event);
  }

  /** A press on the line moves the nearest thumb there, and the pointer then drags it. */
  protected press(event: PointerEvent): void {
    if (this.disabled()) return;
    const value = this.valueAt(event.clientX);
    const values = this.values();
    const nearest = values.length === 2 && Math.abs(values[1] - value) < Math.abs(values[0] - value) ? 1 : 0;
    this.dragging = nearest;
    this.follow(event);
  }

  private follow(event: PointerEvent): void {
    const target = event.currentTarget as HTMLElement;
    target.setPointerCapture?.(event.pointerId);
    this.set(this.dragging ?? 0, this.valueAt(event.clientX));
    const thumb = this.line().nativeElement.querySelectorAll<HTMLElement>('[role="slider"]')[this.dragging ?? 0];
    thumb?.focus();
    const move = (moved: PointerEvent) => this.set(this.dragging ?? 0, this.valueAt(moved.clientX));
    const stop = () => {
      this.dragging = null;
      target.removeEventListener('pointermove', move);
      target.removeEventListener('pointerup', stop);
      target.removeEventListener('pointercancel', stop);
    };
    target.addEventListener('pointermove', move);
    target.addEventListener('pointerup', stop);
    target.addEventListener('pointercancel', stop);
  }

  private valueAt(clientX: number): number {
    const box = this.line().nativeElement.getBoundingClientRect();
    const rtl = getComputedStyle(this.host.nativeElement).direction === 'rtl';
    const share = box.width ? (rtl ? box.right - clientX : clientX - box.left) / box.width : 0;
    return this.min() + share * (this.max() - this.min());
  }

  /** Puts a thumb at a value: on the step, inside the line, and never past the other thumb. */
  private set(index: number, wanted: number): void {
    const step = this.step();
    let next = Math.round((wanted - this.min()) / step) * step + this.min();
    next = Math.min(this.max(), Math.max(this.min(), next));
    const range = this.range();
    if (range) {
      const bounded = index === 0 ? Math.min(next, range[1]) : Math.max(next, range[0]);
      this.range.set(index === 0 ? [bounded, range[1]] : [range[0], bounded]);
    } else {
      this.value.set(next);
    }
  }
}
