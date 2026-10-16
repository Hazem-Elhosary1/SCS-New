import { ScsIconComponent, ScsIconName } from '../icon/icon.component';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  booleanAttribute,
  computed,
  input,
  output,
} from '@angular/core';

export type ScsBannerTone = 'danger' | 'warning' | 'neutral' | 'success' | 'info';

const BANNER_ICONS: Record<ScsBannerTone, ScsIconName> = {
  danger: 'alert-circle',
  warning: 'alert-triangle',
  success: 'check-circle',
  neutral: 'info',
  info: 'info',
};

/**
 * A message above a form: what happened, and what to do next. Refusals and warnings are
 * announced at once (`role="alert"`); neutral notices and successes politely (`role="status"`).
 * One optional action sits inside the banner.
 *
 * With a `title` the banner is a notice in two lines — the «للعرض فقط» notice of a page a
 * person may read and not change is `tone="info"` with that title (spec 005, FR-030).
 *
 * Design: Alert, "Banner" (`60:46` / `257:71`; the clinic's `4253:54128`). Its look before spec 095
 * is the clinic's banner and stays the default; the operators' banner — a bar of the intent's colour at
 * its start, the icon in a tinted circle, the words in the intent's colour, no border, corners 12 — is
 * set by the `--scs-banner-*` settings of its look (the switches `-ink` and `-mark-fill` are 0 or 1).
 */
@Component({
  selector: 'scs-banner',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-tone]': 'tone()',
    '[attr.role]': 'role()',
    '[attr.aria-live]': 'role() === "alert" ? "assertive" : "polite"',
  },
  styles: `
    :host {
      --icon: var(--scs-color-icon-default);
      --strong: var(--scs-color-text-secondary);
      --ring: var(--scs-color-border-default);
      /* Placed and clipped only for a look that draws the bar at its start (the operators'); the
         clinic's banner is neither, so its screens are drawn exactly as before (FR-023). */
      position: var(--scs-banner-anchor, static);
      overflow: var(--scs-banner-clip, visible);
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-12);
      padding: var(--scs-banner-pad, var(--scs-spacing-12) var(--scs-spacing-md));
      border: var(--scs-banner-border, 1px) solid var(--scs-color-border-default);
      border-radius: var(--scs-banner-radius, var(--scs-radius-md));
      background: var(--scs-color-bg-surface-secondary);
      color: color-mix(in srgb, var(--strong) calc(var(--scs-banner-ink, 0) * 100%), var(--scs-color-text-secondary));
      font-size: var(--scs-banner-text, var(--scs-text-body-sm));
      font-weight: var(--scs-banner-weight, 500);
      line-height: var(--scs-banner-line, 1.8);
    }
    /* The operators' bar of the intent's colour at the start. */
    :host::before {
      content: '';
      position: absolute;
      inset-block: 0;
      inset-inline-start: 0;
      inline-size: var(--scs-banner-bar, 0px);
      background: var(--strong);
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: var(--scs-banner-mark, 20px);
      block-size: var(--scs-banner-mark, 20px);
      margin-block-start: var(--scs-banner-mark-offset, 2px);
      border-radius: 50%;
      background: color-mix(in srgb, var(--ring) calc(var(--scs-banner-mark-fill, 0) * 100%), transparent);
    }
    :host ::ng-deep .mark svg {
      inline-size: var(--scs-banner-icon, 20px);
      block-size: var(--scs-banner-icon, 20px);
    }
    scs-icon {
      color: var(--icon);
    }
    .body {
      flex: 1;
      min-inline-size: 0;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--scs-spacing-xs);
    }
    :host ::ng-deep strong {
      color: color-mix(in srgb, var(--strong) calc(var(--scs-banner-ink, 0) * 100%), var(--scs-color-text-primary));
      font-weight: 600;
    }
    :host([data-tone='danger']) {
      --icon: var(--scs-color-feedback-danger-icon);
      --strong: var(--scs-color-feedback-danger-text);
      --ring: var(--scs-color-feedback-danger-border);
      border-color: var(--scs-color-feedback-danger-border);
      background: var(--scs-color-feedback-danger-bg);
    }
    :host([data-tone='danger']) button {
      color: var(--scs-color-feedback-danger-icon);
    }
    :host([data-tone='warning']) button {
      color: var(--scs-color-feedback-warning-icon);
    }
    :host([data-tone='warning']) {
      --icon: var(--scs-color-feedback-warning-icon);
      --strong: var(--scs-color-feedback-warning-text);
      --ring: var(--scs-color-feedback-warning-border);
      border-color: var(--scs-color-feedback-warning-border);
      background: var(--scs-color-feedback-warning-bg);
    }
    :host([data-tone='success']) {
      --icon: var(--scs-color-feedback-success-icon);
      --strong: var(--scs-color-feedback-success-text);
      --ring: var(--scs-color-feedback-success-border);
      border-color: var(--scs-color-feedback-success-border);
      background: var(--scs-color-feedback-success-bg);
    }
    :host([data-tone='info']) {
      --icon: var(--scs-color-feedback-info-icon);
      --strong: var(--scs-color-feedback-info-text);
      --ring: var(--scs-color-feedback-info-border);
      padding: var(--scs-banner-pad, var(--scs-spacing-md));
      border-color: var(--scs-color-feedback-info-border);
      background: var(--scs-color-feedback-info-bg);
      font-weight: 400;
      line-height: var(--scs-banner-line, 1.5);
    }
    .title {
      margin: 0 0 var(--scs-banner-title-gap, 0px);
      color: color-mix(in srgb, var(--strong) calc(var(--scs-banner-ink, 0) * 100%), var(--scs-color-text-primary));
      font-size: var(--scs-text-body);
      font-weight: 600;
      line-height: 1.5;
    }
    .body {
      flex: 1;
      min-inline-size: 0;
    }
    button.dismiss {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: max(var(--scs-touch, 0px), 24px);
      block-size: max(var(--scs-touch, 0px), 24px);
      margin: min(0px, calc((24px - var(--scs-touch, 0px)) / 2));
      border-radius: var(--scs-radius-sm);
      color: var(--scs-banner-close, var(--icon));
    }
    /* The close takes the intent's colour, as before spec 095; the operators' is grey. */
    :host([data-tone]) button.dismiss {
      color: var(--scs-banner-close, var(--icon));
    }
    button.dismiss scs-icon {
      color: inherit;
    }
    button.dismiss:focus-visible {
      outline: 2px solid currentcolor;
    }
    button {
      min-block-size: var(--scs-touch, 0px);
      min-inline-size: var(--scs-touch, 0px);
      margin-block: min(0px, calc((1lh - var(--scs-touch, 0px)) / 2));
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-link);
      font: inherit;
      font-weight: 700;
      text-decoration: none;
      cursor: pointer;
    }
  `,
  template: `
    <span class="mark"><scs-icon [name]="icon()" [size]="20" /></span>
    <div class="body">
      @if (title(); as title) {
        <p class="title" data-testid="banner-title">{{ title }}</p>
      }
      <div><ng-content /></div>
      @if (actionLabel(); as label) {
        <button type="button" data-testid="banner-action" (click)="action.emit()">
          {{ label }}
        </button>
      }
    </div>
    @if (closeLabel(); as label) {
      <button type="button" class="dismiss" data-testid="banner-close" [attr.aria-label]="label" (click)="closed.emit()">
        <scs-icon name="close" [size]="16" />
      </button>
    }
  `,
})
export class ScsBannerComponent {
  readonly tone = input<ScsBannerTone>('neutral');

  /** Already translated. A line above the banner's words, in the weight of a heading. */
  readonly title = input<string>('');

  /** Already translated. When set, the banner offers this one action. */
  readonly actionLabel = input<string>('');

  readonly action = output<void>();

  /**
   * Already translated: the name of the control that closes the banner. When set, the banner
   * can be closed; the page takes it away on `closed`.
   */
  readonly closeLabel = input<string>('');

  readonly closed = output<void>();

  protected readonly icon = computed(() => BANNER_ICONS[this.tone()]);

  protected readonly role = computed(() =>
    this.tone() === 'danger' || this.tone() === 'warning' ? 'alert' : 'status',
  );
}

/** The button's types, as the design's Button page names them (spec 095). */
export type ScsButtonType = 'primary' | 'secondary' | 'tertiary' | 'ghost' | 'link' | 'destructive';
export type ScsButtonSize = 'sm' | 'md' | 'lg';

/**
 * The look of a button, on a real `<button>` so forms and the keyboard work as they always do
 * (design: Button, `contracts/components.md`). Six types and three sizes; with no `size` it keeps
 * the geometry the screens had before spec 095. While `busy` it is locked, keeps its width and says
 * so to assistive technology. An icon-only button takes its name from `label`.
 */
@Directive({
  selector: 'button[scsButton]',
  host: {
    class: 'scs-button',
    '[attr.data-variant]': 'scsButton() || "primary"',
    '[attr.data-size]': 'size() ?? null',
    '[attr.data-icon-only]': 'label() ? "" : null',
    '[attr.aria-label]': 'label() ?? null',
    '[attr.aria-busy]': 'busy() ? "true" : null',
    '[disabled]': 'busy() || disabled()',
  },
})
export class ScsButtonDirective {
  readonly scsButton = input<ScsButtonType | ''>('primary');

  readonly size = input<ScsButtonSize>();

  /** The name of a button that shows only an icon; it makes the button square. */
  readonly label = input<string>();

  readonly busy = input(false, { transform: booleanAttribute });

  readonly disabled = input(false, { transform: booleanAttribute });
}

/** The look of a text link, for `<a>` and for buttons that read as links. */
@Directive({
  selector: 'a[scsLink], button[scsLink]',
  host: { class: 'scs-link' },
})
export class ScsLinkDirective {}
