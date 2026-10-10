import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  effect,
  input,
  model,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

let nextId = 0;

/**
 * A small window over a dimmed page. Built on the browser's own dialog, which keeps focus
 * inside while it is open, closes on Escape, and returns focus to what opened it. The × button
 * and a click on the dimmed area close it too.
 */
@Component({
  selector: 'scs-dialog',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    dialog {
      inline-size: min(440px, calc(100vw - 2 * var(--scs-spacing-md)));
      /* Centred: the page reset removes the margin the browser would give it. */
      margin: auto;
      padding: 0;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      box-shadow: var(--scs-shadow-subtle);
    }
    dialog::backdrop {
      background: var(--scs-color-overlay);
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-md) var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 700;
    }
    .close {
      inline-size: 36px;
      block-size: 36px;
      border: 0;
      border-radius: var(--scs-radius-sm);
      background: none;
      color: var(--scs-color-icon-default);
      font-size: var(--scs-text-title-sm);
      line-height: 1;
      cursor: pointer;
    }
    .close:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    .close:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
    }
    .body {
      padding: var(--scs-spacing-lg);
    }
  `,
  template: `
    <dialog #dialog [attr.aria-labelledby]="id" data-testid="dialog" (close)="open.set(false)">
      <header>
        <h2 [id]="id" data-testid="dialog-title">{{ heading() }}</h2>
        <button
          type="button"
          class="close"
          data-testid="dialog-close"
          [attr.aria-label]="'ui.dialog.close' | transloco"
          (click)="open.set(false)"
        >
          <span aria-hidden="true">×</span>
        </button>
      </header>
      <div class="body"><ng-content /></div>
    </dialog>
  `,
})
export class ScsDialogComponent {
  protected readonly id = `scs-dialog-${nextId++}`;

  /** Already translated. */
  readonly heading = input.required<string>();

  readonly open = model(false);

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    // A click on the dimmed area closes the window. People using a keyboard have Escape and
    // the × button, so the dialog itself is not made a control.
    afterNextRender(() =>
      this.dialog().nativeElement.addEventListener('click', (event) => this.backdrop(event)),
    );

    effect(() => {
      const element = this.dialog().nativeElement;
      if (this.open() && !element.open) {
        element.showModal();
      } else if (!this.open() && element.open) {
        element.close();
      }
    });
  }

  /** A click that lands on the dialog element itself is a click on the dimmed area around it. */
  private backdrop(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.open.set(false);
    }
  }
}
