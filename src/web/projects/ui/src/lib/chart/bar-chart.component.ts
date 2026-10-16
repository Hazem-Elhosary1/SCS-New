import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

let nextId = 0;

/** One bar: its name under it and its number. */
export interface ScsBar {
  readonly label: string;
  readonly value: number;
}

/**
 * A row of bars from a list of numbers (design: Chart — bar chart `394:16` / `395:15`; the clinic's
 * Bar Chart `5601:4517`): each bar's name under it; the chosen bar (the highest when not given) in the
 * brand's colour with its number over it, the others in its pale tint; under the pointer, a bar shows
 * its number. The clinic's chart draws value lines behind the bars (`--scs-chart-axis`). The bars are
 * a picture named by `label`; the numbers are also in a table for assistive technology. Bars follow
 * the page's direction.
 */
@Component({
  selector: 'scs-bar-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    figure {
      margin: 0;
    }
    figcaption {
      margin-block-end: var(--scs-spacing-12);
      font-size: 13px;
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    .plot {
      position: relative;
      display: flex;
      align-items: flex-end;
      gap: var(--scs-chart-gap, 16px);
      block-size: var(--scs-chart-height, 140px);
      padding-block-start: 20px;
      /* Room at the start for the value lines' numbers (the clinic's chart). */
      padding-inline-start: var(--scs-chart-axis-room, 48px);
    }
    /* The value lines behind the bars (the clinic's chart). */
    .lines {
      position: absolute;
      inset: 20px 0 0;
      display: var(--scs-chart-axis, flex);
      flex-direction: column;
      justify-content: space-between;
      pointer-events: none;
    }
    .lines span {
      text-align: end;
      display: grid;
      grid-template-columns: 40px 1fr;
      align-items: center;
      gap: 8px;
      block-size: 0;
      color: var(--scs-color-text-tertiary);
      font-size: 11px;
    }
    .lines span::after {
      content: '';
      block-size: 1px;
      background: var(--scs-color-border-default);
    }
    .column {
      position: relative;
      flex: 1;
      display: grid;
      align-items: end;
      justify-items: center;
      max-inline-size: var(--scs-chart-bar, 32px);
      block-size: 100%;
    }
    .bar {
      position: relative;
      inline-size: 100%;
      min-block-size: 2px;
      border-radius: var(--scs-chart-radius, 6px) var(--scs-chart-radius, 6px) 0 0;
      background: var(--scs-chart-pale, var(--scs-color-brand-subtle));
    }
    .bar.chosen,
    .column:hover .bar {
      background: var(--scs-color-brand-default);
    }
    .number {
      position: absolute;
      inset-block-end: calc(100% + 4px);
      inset-inline: -12px;
      visibility: hidden;
      font-size: 12px;
      font-weight: 600;
      text-align: center;
      color: var(--scs-color-brand-default);
    }
    .bar.chosen .number,
    .column:hover .number {
      visibility: visible;
    }
    .names {
      display: flex;
      gap: var(--scs-chart-gap, 16px);
      margin-block-start: 8px;
      padding-inline-start: var(--scs-chart-axis-room, 48px);
    }
    .names span {
      flex: 1;
      max-inline-size: var(--scs-chart-bar, 32px);
      font-size: 11px;
      text-align: center;
      color: var(--scs-color-text-tertiary);
    }
    table {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }
  `,
  template: `
    <figure>
      @if (label()) {
        <figcaption [id]="id">{{ label() }}</figcaption>
      }
      <div class="plot" aria-hidden="true">
        <div class="lines">
          @for (line of lines(); track $index) {
            <span>{{ line }}</span>
          }
        </div>
        @for (bar of shown(); track $index) {
          <div class="column">
            <div class="bar" [class.chosen]="bar.chosen" [style.block-size.%]="bar.share">
              <span class="number">{{ bar.number }}</span>
            </div>
          </div>
        }
      </div>
      <div class="names" aria-hidden="true">
        @for (bar of shown(); track $index) {
          <span>{{ bar.label }}</span>
        }
      </div>
      <table [attr.aria-labelledby]="label() ? id : null">
        <tbody>
          @for (bar of shown(); track $index) {
            <tr>
              <th scope="row">{{ bar.label }}</th>
              <td>{{ bar.number }}</td>
            </tr>
          }
        </tbody>
      </table>
    </figure>
  `,
})
export class ScsBarChartComponent {
  protected readonly id = `scs-chart-${nextId++}`;
  readonly bars = input.required<readonly ScsBar[]>();
  /** Already translated: what the bars count. */
  readonly label = input('');
  /** The bar drawn in the brand's colour; the highest when not given. */
  readonly chosen = input<number | null>(null);
  /** Writes a number in the page's digits. */
  readonly digits = input<(value: number) => string>((value) => String(value));

  private readonly top = computed(() => Math.max(1, ...this.bars().map((bar) => bar.value)));

  protected readonly shown = computed(() => {
    const bars = this.bars();
    const chosen = this.chosen() ?? bars.findIndex((bar) => bar.value === this.top());
    return bars.map((bar, index) => ({
      label: bar.label,
      number: this.digits()(bar.value),
      share: (bar.value / this.top()) * 100,
      chosen: index === chosen,
    }));
  });

  /** Four value lines, from the top value down to a quarter of it. */
  protected readonly lines = computed(() => [1, 0.75, 0.5, 0.25].map((part) => this.digits()(Math.round(this.top() * part))));
}

