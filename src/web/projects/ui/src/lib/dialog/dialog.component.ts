import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  effect,
  input,
  model,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsIconComponent } from '../icon/icon.component';

let nextId = 0;

/**
 * A small window over a dimmed page. Built on the browser's own dialog, which keeps focus
 * inside while it is open, closes on Escape, and returns focus to what opened it. The × button
 * and a click on the dimmed area close it too.
 */
@Component({
  selector: 'scs-dialog',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    dialog {
      inline-size: min(var(--scs-dialog-width, 400px), calc(100vw - 2 * var(--scs-spacing-md)));
      /* Centred: the page reset removes the margin the browser would give it. */
      margin: auto;
      padding: 0;
      border: 0;
      border-radius: calc(var(--scs-radius-md) + var(--scs-spacing-6));
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      box-shadow: 0 20px 48px 0 #0a0f1433;
    }
    dialog::backdrop {
      background: var(--scs-color-overlay);
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-lg) var(--scs-spacing-lg) 0;
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 700;
    }
    .close {
      display: inline-grid;
      place-items: center;
      inline-size: 32px;
      block-size: 32px;
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
      padding: var(--scs-spacing-md) var(--scs-spacing-lg) var(--scs-spacing-lg);
    }
  `,
  template: `
    <dialog
      #dialog
      data-testid="dialog"
      [style.--scs-dialog-width.px]="width()"
      [attr.aria-labelledby]="heading() ? id : null"
      [attr.aria-label]="heading() ? null : label() || null"
      (close)="open.set(false)"
    >
      @if (heading()) {
        <header>
          <h2 [id]="id" data-testid="dialog-title">{{ heading() }}</h2>
          @if (closable()) {
            <button
              type="button"
              class="close"
              data-testid="dialog-close"
              [attr.aria-label]="'ui.dialog.close' | transloco"
              (click)="open.set(false)"
            >
              <scs-icon name="close" [size]="18" />
            </button>
          }
        </header>
      }
      <div class="body"><ng-content /></div>
    </dialog>
  `,
})
export class ScsDialogComponent {
  protected readonly id = `scs-dialog-${nextId++}`;

  /** Already translated. */
  readonly heading = input.required<string>();

  /**
   * Whether the heading has a × beside it. The operators' design draws none: its dialogs close
   * by their own buttons, by Escape and by the dimmed area.
   */
  readonly closable = input(true, { transform: booleanAttribute });

  /** How wide, in pixels, when the window allows it. */
  readonly width = input(400);

  /** Already translated: the dialog's name when it shows no heading. */
  readonly label = input('');

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
