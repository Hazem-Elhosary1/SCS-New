import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

export type ScsKpiTone = 'brand' | 'success' | 'warning' | 'info';

/** Which way the number went: up, down, the same, or no change line at all (the clinic's "None"). */
export type ScsKpiTrend = 'positive' | 'negative' | 'neutral' | 'none';

/**
 * One number over a list (design: KPI Card — `294:315` / `8:135`; the clinic's `4267:1175`): what
 * it counts, the number, and a short line about how it moved in a mark tinted by its `trend` —
 * green up, red down, grey the same — with the clinic's caption after it. The operators' list
 * template (spec 011) adds an icon at the reading end, kept as the option `icon` with its `tone`.
 * Each look draws its own card through the `--scs-kpi-*` settings: the clinic's by default, the
 * operators' corners 14 and 28-pixel number.
 */
@Component({
  selector: 'scs-kpi-card',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()', '[attr.data-trend]': 'trend()' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--scs-kpi-gap, 8px);
      box-sizing: border-box;
      min-inline-size: 0;
      padding: var(--scs-kpi-pad, 20px);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-kpi-radius, 10px);
      background: var(--scs-color-bg-surface);
    }
    .top {
      align-self: stretch;
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--scs-spacing-12);
    }
    .label {
      font-size: var(--scs-kpi-label-size, 13px);
      font-weight: 500;
      color: var(--scs-kpi-label-color, var(--scs-color-text-secondary));
    }
    .icon {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: var(--scs-kpi-icon, 36px);
      block-size: var(--scs-kpi-icon, 36px);
      border-radius: var(--scs-kpi-icon-radius, 10px);
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
      font-size: var(--scs-kpi-value-size, 26px);
      font-weight: 700;
      line-height: 1.3;
      color: var(--scs-color-text-primary);
    }
    .delta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .note {
      display: inline-flex;
      align-items: center;
      block-size: 22px;
      padding-inline: var(--scs-kpi-note-pad, 6px);
      border-radius: var(--scs-kpi-note-radius, 999px);
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-text);
      font-size: var(--scs-kpi-note-size, 12px);
      font-weight: 600;
    }
    :host([data-trend='negative']) .note {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-text);
    }
    :host([data-trend='neutral']) .note {
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-kpi-label-color, var(--scs-color-text-secondary));
    }
    .caption {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
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
    @if (trend() !== 'none' && (note() || caption())) {
      <span class="delta">
        @if (note()) {
          <span class="note" data-testid="kpi-note">{{ note() }}</span>
        }
        @if (caption()) {
          <span class="caption" data-testid="kpi-caption">{{ caption() }}</span>
        }
      </span>
    }
  `,
})
export class ScsKpiCardComponent {
  readonly label = input.required<string>();
  /** Already written in the page's digits. */
  readonly value = input.required<string>();
  /** How it moved: "+6 this month", «١٢٪». */
  readonly note = input('');
  /** After the mark, what it is compared with: "vs last period" (the clinic's caption). */
  readonly caption = input('');
  readonly trend = input<ScsKpiTrend>('positive');
  readonly icon = input<ScsIconName | null>(null);
  readonly tone = input<ScsKpiTone>('brand');
}

export interface ScsCrumb {
  readonly label: string;
  /** Where it leads; the last crumb — where the person is — has none. */
  readonly link?: string;
}

/**
 * Where a page sits (design: Navigation — Breadcrumb Item, `284:21` / `312:71`; the clinic's
 * `4252:60901`): «المشغلون / تفاصيل المشغل». Each look sets its words through `--scs-crumb-*`; the
 * defaults are the trail before spec 095.
 */
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
      font-size: var(--scs-crumb-size, var(--scs-text-label));
      color: var(--scs-crumb-text, var(--scs-color-text-secondary));
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
      display: inline-flex;
      align-items: center;
      min-block-size: var(--scs-touch, 0px);
      min-inline-size: var(--scs-touch, 0px);
      margin-block: min(0px, calc((1lh - var(--scs-touch, 0px)) / 2));
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
      font-weight: var(--scs-crumb-on-weight, 500);
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
