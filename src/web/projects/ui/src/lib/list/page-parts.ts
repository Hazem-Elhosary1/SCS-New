import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

export type ScsKpiTone = 'brand' | 'success' | 'warning' | 'info';

/**
 * One number over a list (design: KPI Card): what it counts, the number, and a short line
 * about it in a tinted mark, with its icon at the reading end.
 */
@Component({
  selector: 'scs-kpi-card',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      padding: var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-subtle);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      min-inline-size: 0;
    }
    .top {
      align-self: stretch;
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
    }
    .label {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .icon {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 28px;
      block-size: 28px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
    }
    :host([data-tone='success']) .icon {
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-icon);
    }
    :host([data-tone='warning']) .icon {
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-icon);
    }
    :host([data-tone='info']) .icon {
      background: var(--scs-color-feedback-info-bg);
      color: var(--scs-color-feedback-info-icon);
    }
    .value {
      margin: 0;
      font-size: var(--scs-text-title);
      font-weight: 700;
      line-height: 1.3;
      color: var(--scs-color-text-primary);
    }
    .note {
      display: inline-block;
      margin-block-start: var(--scs-spacing-2);
      padding: 0 var(--scs-spacing-6);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-text);
      font-size: var(--scs-text-caption);
      line-height: 1.7;
    }
  `,
  template: `
    <div class="top">
      <span class="label" data-testid="kpi-label">{{ label() }}</span>
      @if (icon(); as name) {
        <span class="icon" aria-hidden="true"><scs-icon [name]="name" [size]="16" /></span>
      }
    </div>
    <p class="value" data-testid="kpi-value"><bdi dir="ltr">{{ value() }}</bdi></p>
    @if (note()) {
      <span class="note" data-testid="kpi-note">{{ note() }}</span>
    }
  `,
})
export class ScsKpiCardComponent {
  readonly label = input.required<string>();
  /** Already written in the page's digits. */
  readonly value = input.required<string>();
  readonly note = input('');
  readonly icon = input<ScsIconName | null>(null);
  readonly tone = input<ScsKpiTone>('brand');
}

export interface ScsCrumb {
  readonly label: string;
  /** Where it leads; the last crumb — where the person is — has none. */
  readonly link?: string;
}

/** Where a page sits (design: Navigation, breadcrumb): «المشغلون / تفاصيل المشغل». */
@Component({
  selector: 'scs-breadcrumb',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    ol {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
      margin: 0 0 var(--scs-spacing-sm);
      padding: 0;
      list-style: none;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    li {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    li + li::before {
      content: '/';
      color: var(--scs-color-text-tertiary);
    }
    a {
      color: inherit;
      text-decoration: none;
    }
    a:hover {
      text-decoration: underline;
    }
    a:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    [aria-current] {
      color: var(--scs-color-text-primary);
      font-weight: 500;
    }
  `,
  template: `
    <nav [attr.aria-label]="label()" data-testid="breadcrumb">
      <ol>
        @for (crumb of crumbs(); track crumb.label) {
          <li>
            @if (crumb.link) {
              <a [routerLink]="crumb.link">{{ crumb.label }}</a>
            } @else {
              <span aria-current="page">{{ crumb.label }}</span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
})
export class ScsBreadcrumbComponent {
  readonly crumbs = input.required<readonly ScsCrumb[]>();
  /** Names the trail for a screen reader. */
  readonly label = input('');
}
