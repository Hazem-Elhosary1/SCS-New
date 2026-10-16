import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';

/** A thin strip saying this is not the real product (design: Dev Environment Strip). */
@Component({
  selector: 'scs-env-strip',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'note', 'data-testid': 'env-strip' },
  styles: `
    :host {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--scs-spacing-sm);
      block-size: 32px;
      background: var(--scs-color-feedback-warning-icon);
      color: var(--scs-color-brand-on-brand);
      font-size: var(--scs-text-label);
      font-weight: 500;
    }
  `,
  template: `<scs-icon name="info" [size]="14" /><ng-content />`,
})
export class ScsEnvStripComponent {}

/** A two-way language pill: «عربي · EN». The active half names the current language; no flags. */
@Component({
  selector: 'scs-language-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { dir: 'ltr', role: 'group', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-2, 2px);
      padding: var(--scs-spacing-2, 2px);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
    }
    .segment {
      display: inline-grid;
      place-items: center;
      min-inline-size: 40px;
      block-size: 28px;
      padding: 0 var(--scs-spacing-10);
      border: 0;
      border-radius: var(--scs-radius-full);
      background: none;
      color: var(--scs-color-text-secondary);
      font: inherit;
      font-size: var(--scs-text-label);
      font-weight: 600;
      line-height: 1;
    }
    button.segment {
      cursor: pointer;
    }
    button.segment:hover {
      color: var(--scs-color-text-primary);
    }
    button.segment:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .segment.on {
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      box-shadow: var(--scs-shadow-subtle);
    }
  `,
  template: `
    @for (choice of choices; track choice.code) {
      @if (current() === choice.code) {
        <span class="segment on" aria-current="true" [attr.lang]="choice.code">{{
          choice.label
        }}</span>
      } @else {
        <button
          type="button"
          class="segment"
          data-testid="language-switch"
          [attr.lang]="choice.code"
          (click)="chosen.emit(choice.code)"
        >
          {{ choice.label }}
        </button>
      }
    }
  `,
})
export class ScsLanguageToggleComponent {
  readonly current = input.required<'ar' | 'en'>();

  /** Already translated: what the pill is, for assistive technology. */
  readonly label = input('');

  readonly chosen = output<'ar' | 'en'>();

  /** Not translated: a language is always named in its own words. */
  protected readonly choices = [
    { code: 'ar', label: 'عربي' },
    { code: 'en', label: 'EN' },
  ] as const;
}
