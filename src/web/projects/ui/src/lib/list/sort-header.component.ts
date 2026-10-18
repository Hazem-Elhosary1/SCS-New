import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';

export type ScsSortDirection = 'none' | 'ascending' | 'descending';

/**
 * A table column a person can sort by (spec 015; the clinics list's «ينتهي في ▾», frames `606:776`,
 * `599:680`). Put it on a `<button>` inside the column's `<th>`: it names the order on that `th`
 * (`aria-sort`) and draws the frame's small caret — down for none and descending, up for ascending.
 * A press asks for the next order; the page decides what it is.
 */
@Component({
  // A component on the column's own button, so the keyboard and forms work as they always do; it
  // draws the caret, which a directive cannot.
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'button[scsSortHeader]',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    type: 'button',
    class: 'scs-sort-header',
    '[attr.data-direction]': 'direction()',
    '(click)': 'sorted.emit(next())',
  },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-4, 4px);
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      cursor: pointer;
    }
    :host(:focus-visible) {
      outline: 2px solid var(--scs-color-border-focus, var(--scs-color-brand-default));
      outline-offset: 2px;
      border-radius: var(--scs-radius-xs, 4px);
    }
    .caret {
      display: inline-flex;
      color: var(--scs-color-text-tertiary, var(--scs-color-text-secondary));
      transition: transform 120ms ease;
    }
    :host([data-direction='ascending']) .caret {
      transform: rotate(180deg);
    }
    :host(:not([data-direction='none'])) .caret {
      color: var(--scs-color-brand-default);
    }
  `,
  template: `<ng-content /><span class="caret" aria-hidden="true"><scs-icon name="chevron-down" [size]="12" /></span>`,
})
export class ScsSortHeaderComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly direction = input<ScsSortDirection>('none');
  /** Asks for the order after this one: none → ascending → descending → ascending. */
  readonly sorted = output<ScsSortDirection>();

  protected readonly next = computed<ScsSortDirection>(() =>
    this.direction() === 'ascending' ? 'descending' : 'ascending',
  );

  constructor() {
    effect(() => {
      const cell = this.host.nativeElement.closest('th');
      cell?.setAttribute('aria-sort', this.direction());
    });
  }
}
