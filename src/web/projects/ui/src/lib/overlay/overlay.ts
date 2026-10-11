import { CdkMenu, CdkMenuItem } from '@angular/cdk/menu';
import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  Injectable,
  OnDestroy,
  booleanAttribute,
  inject,
  input,
  signal,
} from '@angular/core';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

/**
 * A menu that opens under what was pressed (design: Menu). Put it in an `ng-template` and open
 * it with the CDK's `[cdkMenuTriggerFor]`: the CDK places it in both directions, moves through
 * it with the arrow keys, closes it on a choice, an outside press or Escape, and returns focus
 * to what opened it. The opener is marked while it is open (`aria-expanded`).
 */
@Directive({
  selector: '[scsMenu]',
  hostDirectives: [CdkMenu],
  host: { class: 'scs-menu' },
})
export class ScsMenuDirective {}

/** One choice of a menu. `danger` draws it red, wherever it sits (BRD §A-7). */
@Directive({
  selector: '[scsMenuItem]',
  hostDirectives: [{ directive: CdkMenuItem, outputs: ['cdkMenuItemTriggered: chosen'] }],
  host: {
    class: 'scs-menu-item',
    '[attr.data-danger]': 'danger() ? "true" : null',
    '[attr.data-active]': 'active() ? "true" : null',
  },
})
export class ScsMenuItemDirective {
  readonly danger = input(false, { transform: booleanAttribute });

  /** The choice that is in force now, in a menu that picks one of several. */
  readonly active = input(false, { transform: booleanAttribute });
}

@Component({
  selector: 'scs-tooltip-bubble',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'scs-tooltip', role: 'tooltip' },
  template: `{{ text() }}`,
})
export class ScsTooltipBubbleComponent {
  readonly text = signal('');
}

/**
 * The name of a control that shows only an icon (design: Tooltip). Shown on hover and on
 * keyboard focus, at the reading end of the control; it is also the control's accessible name
 * when it has no other.
 */
@Directive({
  selector: '[scsTooltip]',
  host: {
    '(mouseenter)': 'show()',
    '(focus)': 'show()',
    '(mouseleave)': 'hide()',
    '(blur)': 'hide()',
    '(click)': 'hide()',
    '[attr.aria-label]': 'scsTooltip() || null',
  },
})
export class ScsTooltipDirective implements OnDestroy {
  private readonly overlay = inject(Overlay);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private open: OverlayRef | null = null;

  /** Already translated. Empty shows nothing. */
  readonly scsTooltip = input('');

  protected show(): void {
    const text = this.scsTooltip();
    if (text === '' || this.open !== null) {
      return;
    }

    this.open = this.overlay.create({
      positionStrategy: this.overlay
        .position()
        .flexibleConnectedTo(this.element)
        .withPositions([
          { originX: 'end', originY: 'center', overlayX: 'start', overlayY: 'center', offsetX: 8 },
          { originX: 'center', originY: 'bottom', overlayX: 'center', overlayY: 'top', offsetY: 8 },
        ]),
      scrollStrategy: this.overlay.scrollStrategies.close(),
    });
    this.open.attach(new ComponentPortal(ScsTooltipBubbleComponent)).instance.text.set(text);
  }

  protected hide(): void {
    this.open?.dispose();
    this.open = null;
  }

  ngOnDestroy(): void {
    this.hide();
  }
}

export type ScsToastTone = 'success' | 'info' | 'warning' | 'danger';

export interface ScsToast {
  readonly id: number;
  readonly tone: ScsToastTone;
  readonly title: string;
  readonly text: string;
}

/**
 * The passing notice after an action that changed something (BRD §A-7, CL-SH-058). A page says
 * what happened; where and how long it shows is decided here once. The frame places the
 * notices in the corner at the reading end, beside the page's header buttons.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  /** How long a notice stays, in milliseconds. */
  static readonly life = 5000;

  private next = 1;
  private readonly shown = signal<readonly ScsToast[]>([]);

  readonly toasts = this.shown.asReadonly();

  /** `title` and `text` are already translated. */
  show(title: string, text = '', tone: ScsToastTone = 'success'): void {
    const toast = { id: this.next++, tone, title, text };
    this.shown.update((all) => [...all, toast]);
    setTimeout(() => this.dismiss(toast.id), ToastService.life);
  }

  dismiss(id: number): void {
    this.shown.update((all) => all.filter((toast) => toast.id !== id));
  }
}

const TOAST_ICONS: Record<ScsToastTone, ScsIconName> = {
  success: 'check-circle',
  info: 'info',
  warning: 'alert-triangle',
  danger: 'alert-circle',
};

/** Where the passing notices appear. The frame holds one; a page never places its own. */
@Component({
  selector: 'scs-toast-outlet',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status', 'aria-live': 'polite' },
  styles: `
    :host {
      position: fixed;
      inset-block-start: var(--scs-toast-top, 88px);
      inset-inline-end: var(--scs-spacing-xl, 32px);
      z-index: 900;
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      pointer-events: none;
    }
    .toast {
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-12);
      inline-size: 360px;
      padding: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: 0 8px 24px -4px #0a0f1424;
      pointer-events: auto;
    }
    .title {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .text {
      margin: var(--scs-spacing-2) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .words {
      flex: 1;
      min-inline-size: 0;
    }
    [data-tone='success'] scs-icon {
      color: var(--scs-color-feedback-success-icon);
    }
    [data-tone='info'] scs-icon {
      color: var(--scs-color-feedback-info-icon);
    }
    [data-tone='warning'] scs-icon {
      color: var(--scs-color-feedback-warning-icon);
    }
    [data-tone='danger'] scs-icon {
      color: var(--scs-color-feedback-danger-icon);
    }
    button {
      display: inline-grid;
      place-items: center;
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-icon-default);
      cursor: pointer;
    }
  `,
  template: `
    @for (toast of service.toasts(); track toast.id) {
      <div class="toast" data-testid="toast" [attr.data-tone]="toast.tone">
        <scs-icon [name]="icons[toast.tone]" [size]="20" />
        <div class="words">
          <p class="title">{{ toast.title }}</p>
          @if (toast.text) {
            <p class="text">{{ toast.text }}</p>
          }
        </div>
        <button type="button" [attr.aria-label]="closeLabel()" (click)="service.dismiss(toast.id)">
          <scs-icon name="close" [size]="16" />
        </button>
      </div>
    }
  `,
})
export class ScsToastOutletComponent {
  protected readonly service = inject(ToastService);
  protected readonly icons = TOAST_ICONS;

  /** Already translated: the name of the button that closes a notice. */
  readonly closeLabel = input('');
}
