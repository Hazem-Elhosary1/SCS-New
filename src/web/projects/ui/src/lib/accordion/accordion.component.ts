import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';

let nextId = 0;

/**
 * A stack of sections that open and close (design: Data Display — Accordion Item, `281:77` /
 * `313:76`; the clinic's Accordion `4260:1383`). Each item's heading is a button: Enter or Space
 * opens and closes it, and it says whether it is open; its content follows it.
 */
@Component({
  selector: 'scs-accordion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-accordion-gap, 0px);
    }
  `,
  template: `<ng-content />`,
})
export class ScsAccordionComponent {}

/**
 * One section of `scs-accordion`. Each look draws it through `--scs-accordion-*`: the clinic's rows
 * divided by a line by default, the operators' separate cards with corners 12.
 */
@Component({
  selector: 'scs-accordion-item',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      border: var(--scs-accordion-frame, 0px) solid var(--scs-color-border-default);
      border-block-end: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-accordion-radius, 0px);
      background: var(--scs-color-bg-surface);
    }
    h3 {
      margin: 0;
    }
    button {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      inline-size: 100%;
      padding: var(--scs-accordion-pad, 12px 4px);
      border: 0;
      border-radius: inherit;
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: -2px;
    }
    .words {
      flex: 1;
      display: grid;
      min-inline-size: 0;
    }
    .title {
      font-size: var(--scs-accordion-title, 14px);
      font-weight: var(--scs-accordion-weight, 600);
    }
    .summary {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    scs-icon {
      flex: none;
      color: var(--scs-color-icon-default);
      transition: transform 0.15s;
    }
    :host ::ng-deep button scs-icon svg {
      inline-size: var(--scs-accordion-chevron, 20px);
      block-size: var(--scs-accordion-chevron, 20px);
    }
    [aria-expanded='true'] scs-icon {
      transform: rotate(180deg);
    }
    .body {
      padding: var(--scs-accordion-body-pad, 0 4px 12px);
      border-block-start: var(--scs-accordion-rule, 0px) solid var(--scs-color-border-default);
      color: var(--scs-accordion-body-text, var(--scs-color-text-secondary));
      font-size: var(--scs-accordion-body-size, 13px);
    }
    @media (prefers-reduced-motion: reduce) {
      scs-icon {
        transition: none;
      }
    }
  `,
  template: `
    <h3>
      <button type="button" [id]="id + '-head'" [attr.aria-expanded]="expanded()" [attr.aria-controls]="id + '-body'" (click)="expanded.set(!expanded())">
        <span class="words">
          <span class="title">{{ heading() }}</span>
          @if (summary()) {
            <span class="summary">{{ summary() }}</span>
          }
        </span>
        <scs-icon name="chevron-down" [size]="20" />
      </button>
    </h3>
    <div class="body" role="region" [id]="id + '-body'" [attr.aria-labelledby]="id + '-head'" [hidden]="!expanded()">
      <ng-content />
    </div>
  `,
})
export class ScsAccordionItemComponent {
  protected readonly id = `scs-accordion-${nextId++}`;
  /** Already translated. */
  readonly heading = input.required<string>();
  /** A line under the heading (the clinic's subtitle). */
  readonly summary = input('');
  readonly expanded = model(false);
}
