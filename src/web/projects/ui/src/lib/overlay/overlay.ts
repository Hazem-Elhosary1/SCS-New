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
  output,
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

/**
 * One choice of a menu. `danger` draws it red, wherever it sits (BRD §A-7); destructive choices come
 * last, under a separator. `disabled` greys it and it cannot be chosen; its reason goes in a
 * `.reason` line inside it.
 */
@Directive({
  selector: '[scsMenuItem]',
  hostDirectives: [
    { directive: CdkMenuItem, inputs: ['cdkMenuItemDisabled: disabled'], outputs: ['cdkMenuItemTriggered: chosen'] },
  ],
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
  host: { class: 'scs-tooltip', role: 'tooltip', '[attr.data-placement]': 'side()' },
  template: `{{ text() }}`,
})
export class ScsTooltipBubbleComponent {
  readonly text = signal('');
  /** Which side of its control it sits on, for its arrow. */
  readonly side = signal<ScsTooltipPlacement>('end');
}

/** Where a tooltip sits: above, below, or at the start or the end of the line (Tooltip 383:23). */
export type ScsTooltipPlacement = 'top' | 'bottom' | 'start' | 'end';

const TOOLTIP_PLACES: Record<ScsTooltipPlacement, { originX: 'start' | 'center' | 'end'; originY: 'top' | 'center' | 'bottom'; overlayX: 'start' | 'center' | 'end'; overlayY: 'top' | 'center' | 'bottom'; offsetX?: number; offsetY?: number }> = {
  top: { originX: 'center', originY: 'top', overlayX: 'center', overlayY: 'bottom', offsetY: -8 },
  bottom: { originX: 'center', originY: 'bottom', overlayX: 'center', overlayY: 'top', offsetY: 8 },
  start: { originX: 'start', originY: 'center', overlayX: 'end', overlayY: 'center', offsetX: -8 },
  end: { originX: 'end', originY: 'center', overlayX: 'start', overlayY: 'center', offsetX: 8 },
};

/** The other places to try, in order, when the chosen one has no room. */
const TOOLTIP_FALLBACKS: Record<ScsTooltipPlacement, ScsTooltipPlacement[]> = {
  top: ['bottom', 'end', 'start'],
  bottom: ['top', 'end', 'start'],
  start: ['end', 'bottom', 'top'],
  end: ['bottom', 'start', 'top'],
};

/**
 * The name of a control that shows only an icon (design: Tooltip — `383:23` / `384:22`; the
 * clinic's `4252:59606`). Shown on hover and on keyboard focus, at the reading end of the control
 * unless `scsTooltipPlacement` asks for another side (the other sides are tried when there is no
 * room); its arrow points at the control. It is also the control's accessible name when it has no
 * other.
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

  readonly scsTooltipPlacement = input<ScsTooltipPlacement>('end');

  protected show(): void {
    const text = this.scsTooltip();
    if (text === '' || this.open !== null) {
      return;
    }

    const sides = [this.scsTooltipPlacement(), ...TOOLTIP_FALLBACKS[this.scsTooltipPlacement()]];
    const strategy = this.overlay
      .position()
      .flexibleConnectedTo(this.element)
      .withPositions(sides.map((side) => TOOLTIP_PLACES[side]));
    this.open = this.overlay.create({ positionStrategy: strategy, scrollStrategy: this.overlay.scrollStrategies.close() });
    const bubble = this.open.attach(new ComponentPortal(ScsTooltipBubbleComponent)).instance;
    bubble.text.set(text);
    bubble.side.set(sides[0]);
    strategy.positionChanges.subscribe((change) => {
      const at = sides.findIndex((side) => {
        const place = TOOLTIP_PLACES[side];
        return place.originX === change.connectionPair.originX && place.originY === change.connectionPair.originY && place.overlayX === change.connectionPair.overlayX;
      });
      if (at >= 0) bubble.side.set(sides[at]);
    });
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

/**
 * One passing notice (design: Alert, "Toast" — `60:46` / `257:71`; the clinic's `4253:54128`): a
 * white card with what happened and, under it, a line more; the intent's icon and a close button.
 * Its look before spec 095 — the clinic's toast — is the default; the operators' toast (no border,
 * corners 12, a short bar of the intent's colour in place of the icon) is set by the
 * `--scs-toast-*` settings of its look.
 */
@Component({
  selector: 'scs-toast',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  styles: `
    :host {
      --strong: var(--scs-color-feedback-success-icon);
      --bar: var(--scs-color-feedback-success-text);
      display: flex;
      align-items: flex-start;
      gap: var(--scs-toast-gap, var(--scs-spacing-12));
      box-sizing: border-box;
      inline-size: var(--scs-toast-width, 360px);
      max-inline-size: 100%;
      padding: var(--scs-spacing-md);
      border: var(--scs-toast-border, 1px) solid var(--scs-color-border-default);
      border-radius: var(--scs-toast-radius, var(--scs-radius-md));
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-toast-shadow, 0 8px 24px -4px #0a0f1424);
      pointer-events: auto;
    }
    :host([data-tone='info']) {
      --strong: var(--scs-color-feedback-info-icon);
      --bar: var(--scs-color-feedback-info-text);
    }
    :host([data-tone='warning']) {
      --strong: var(--scs-color-feedback-warning-icon);
      --bar: var(--scs-color-feedback-warning-text);
    }
    :host([data-tone='danger']) {
      --strong: var(--scs-color-feedback-danger-icon);
      --bar: var(--scs-color-feedback-danger-text);
    }
    .mark {
      display: var(--scs-toast-icon, inline-flex);
      color: var(--strong);
    }
    /* The operators' short bar in place of the icon. */
    .bar {
      display: var(--scs-toast-bar, none);
      flex: none;
      inline-size: 4px;
      block-size: 20px;
      margin-block-start: 3px;
      border-radius: 2px;
      background: var(--bar);
    }
    .title {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .text {
      margin: var(--scs-toast-text-gap, var(--scs-spacing-2)) 0 0;
      font-size: var(--scs-toast-text, var(--scs-text-body-sm));
      color: var(--scs-color-text-secondary);
    }
    .words {
      flex: 1;
      min-inline-size: 0;
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
    <span class="bar" aria-hidden="true"></span>
    <scs-icon class="mark" [name]="icons[tone()]" [size]="20" />
    <div class="words">
      <p class="title">{{ title() }}</p>
      @if (text()) {
        <p class="text">{{ text() }}</p>
      }
    </div>
    <button type="button" [attr.aria-label]="closeLabel()" (click)="closed.emit()">
      <scs-icon name="close" [size]="16" />
    </button>
  `,
})
export class ScsToastComponent {
  readonly tone = input<ScsToastTone>('success');
  /** Already translated. */
  readonly title = input.required<string>();
  readonly text = input('');
  /** Already translated: the name of the button that closes it. */
  readonly closeLabel = input('');
  readonly closed = output<void>();

  protected readonly icons = TOAST_ICONS;
}

/** Where the passing notices appear. The frame holds one; a page never places its own. */
@Component({
  selector: 'scs-toast-outlet',
  imports: [ScsToastComponent],
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
  `,
  template: `
    @for (toast of service.toasts(); track toast.id) {
      <scs-toast
        data-testid="toast"
        [tone]="toast.tone"
        [title]="toast.title"
        [text]="toast.text"
        [closeLabel]="closeLabel()"
        (closed)="service.dismiss(toast.id)"
      />
    }
  `,
})
export class ScsToastOutletComponent {
  protected readonly service = inject(ToastService);

  /** Already translated: the name of the button that closes a notice. */
  readonly closeLabel = input('');
}
