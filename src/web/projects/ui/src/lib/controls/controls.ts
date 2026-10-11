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
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-12);
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
      line-height: 1.8;
    }
    scs-icon {
      margin-block-start: 2px;
      color: var(--scs-color-icon-default);
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
      color: var(--scs-color-text-primary);
      font-weight: 600;
    }
    :host([data-tone='danger']) {
      border-color: var(--scs-color-feedback-danger-border);
      background: var(--scs-color-feedback-danger-bg);
    }
    :host([data-tone='danger']) scs-icon {
      color: var(--scs-color-feedback-danger-icon);
    }
    :host([data-tone='danger']) button {
      color: var(--scs-color-feedback-danger-icon);
    }
    :host([data-tone='warning']) button {
      color: var(--scs-color-feedback-warning-icon);
    }
    :host([data-tone='warning']) {
      border-color: var(--scs-color-feedback-warning-border);
      background: var(--scs-color-feedback-warning-bg);
    }
    :host([data-tone='warning']) scs-icon {
      color: var(--scs-color-feedback-warning-icon);
    }
    :host([data-tone='success']) {
      border-color: var(--scs-color-feedback-success-border);
      background: var(--scs-color-feedback-success-bg);
    }
    :host([data-tone='success']) scs-icon {
      color: var(--scs-color-feedback-success-icon);
    }
    :host([data-tone='info']) {
      padding: var(--scs-spacing-md);
      border-color: var(--scs-color-feedback-info-border);
      background: var(--scs-color-feedback-info-bg);
      font-weight: 400;
      line-height: 1.5;
    }
    :host([data-tone='info']) scs-icon {
      color: var(--scs-color-feedback-info-icon);
    }
    .title {
      margin: 0;
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body);
      font-weight: 600;
      line-height: 1.5;
    }
    button {
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
    <scs-icon [name]="icon()" [size]="20" />
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
  `,
})
export class ScsBannerComponent {
  readonly tone = input<ScsBannerTone>('neutral');

  /** Already translated. A line above the banner's words, in the weight of a heading. */
  readonly title = input<string>('');

  /** Already translated. When set, the banner offers this one action. */
  readonly actionLabel = input<string>('');

  readonly action = output<void>();

  protected readonly icon = computed(() => BANNER_ICONS[this.tone()]);

  protected readonly role = computed(() =>
    this.tone() === 'danger' || this.tone() === 'warning' ? 'alert' : 'status',
  );
}

/**
 * The look of a button, on a real `<button>` so forms and the keyboard work as they always do.
 * While `busy` it is locked and says so to assistive technology; the caller changes its label.
 */
@Directive({
  selector: 'button[scsButton]',
  host: {
    class: 'scs-button',
    '[attr.data-variant]': 'scsButton() || "primary"',
    '[attr.aria-busy]': 'busy() ? "true" : null',
    '[disabled]': 'busy() || disabled()',
  },
})
export class ScsButtonDirective {
  readonly scsButton = input<'primary' | 'secondary' | ''>('primary');

  readonly busy = input(false, { transform: booleanAttribute });

  readonly disabled = input(false, { transform: booleanAttribute });
}

/** The look of a text link, for `<a>` and for buttons that read as links. */
@Directive({
  selector: 'a[scsLink], button[scsLink]',
  host: { class: 'scs-link' },
})
export class ScsLinkDirective {}
