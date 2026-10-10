import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  booleanAttribute,
  computed,
  input,
  output,
} from '@angular/core';

export type ScsBannerTone = 'danger' | 'warning' | 'neutral' | 'success';

/**
 * A message above a form: what happened, and what to do next. Refusals and warnings are
 * announced at once (`role="alert"`); neutral notices and successes politely (`role="status"`).
 * One optional action sits inside the banner.
 */
@Component({
  selector: 'scs-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-tone]': 'tone()',
    '[attr.role]': 'role()',
    '[attr.aria-live]': 'role() === "alert" ? "assertive" : "polite"',
  },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body-sm);
      line-height: 1.6;
    }
    :host([data-tone='danger']) {
      border-color: var(--scs-color-feedback-danger-border);
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-text);
    }
    :host([data-tone='warning']) {
      border-color: var(--scs-color-feedback-warning-border);
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    :host([data-tone='success']) {
      border-color: var(--scs-color-feedback-success-border);
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-text);
    }
    button {
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      font-weight: 600;
      text-decoration: underline;
      cursor: pointer;
    }
  `,
  template: `
    <div><ng-content /></div>
    @if (actionLabel(); as label) {
      <button type="button" data-testid="banner-action" (click)="action.emit()">{{ label }}</button>
    }
  `,
})
export class ScsBannerComponent {
  readonly tone = input<ScsBannerTone>('neutral');

  /** Already translated. When set, the banner offers this one action. */
  readonly actionLabel = input<string>('');

  readonly action = output<void>();

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
