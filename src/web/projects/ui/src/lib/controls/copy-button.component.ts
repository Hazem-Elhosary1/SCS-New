import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';
import { ToastService } from '../overlay/overlay';

/**
 * A square button that copies a value (spec 015; the sign-in address row, frames `2330:3095`,
 * `2330:3050`): the copy icon in a 32 box with a thin border. It says so with a toast — the
 * copied words, or the refusal's words when the browser does not allow copying.
 */
@Component({
  selector: 'scs-copy-button',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
    }
    button {
      display: inline-grid;
      place-items: center;
      inline-size: 32px;
      block-size: 32px;
      padding: 0;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-sm, 8px);
      background: var(--scs-color-bg-surface, #fff);
      color: var(--scs-color-text-secondary);
      cursor: pointer;
    }
    button:hover {
      background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-text-primary);
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-border-focus, var(--scs-color-brand-default));
      outline-offset: 2px;
    }
  `,
  template: `
    <button type="button" [attr.aria-label]="label()" [attr.title]="label()" [attr.data-testid]="testId()" (click)="copy()">
      <scs-icon name="menu-copy" [size]="16" />
    </button>
  `,
})
export class ScsCopyButtonComponent {
  private readonly toasts = inject(ToastService);

  readonly value = input.required<string>();
  /** The button's accessible name, e.g. «نسخ عنوان دخول العيادة». */
  readonly label = input.required<string>();
  readonly copiedText = input.required<string>();
  readonly failedText = input.required<string>();
  readonly testId = input<string>('copy');

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.value());
      this.toasts.show(this.copiedText());
    } catch {
      this.toasts.show(this.failedText(), '', 'danger');
    }
  }
}
