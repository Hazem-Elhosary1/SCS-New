import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';

/** One step of a stepper. */
export interface ScsStep {
  readonly label: string;
}

/**
 * Where a person is in a task of several steps (design: Navigation — Stepper Step, `284:53` /
 * `312:103`; the clinic's `4252:61106`): each step's number in a circle — filled with a tick when
 * done, ringed in the brand's colour where the person is, grey ahead — and its name. The list says
 * which step is current to assistive technology. Each look draws its steps through `--scs-stepper-*`:
 * the clinic's name beside the circle and a line to the next step by default, the operators' name
 * under it.
 */
@Component({
  selector: 'scs-stepper',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    ol {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: var(--scs-stepper-gap, 12px);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: flex;
      flex-direction: var(--scs-stepper-flow, row);
      align-items: center;
      gap: var(--scs-stepper-inner-gap, 8px);
      inline-size: var(--scs-stepper-width, auto);
      text-align: center;
    }
    .circle {
      display: grid;
      place-items: center;
      flex: none;
      box-sizing: border-box;
      inline-size: 28px;
      block-size: 28px;
      border: var(--scs-stepper-ring, 1px) solid var(--scs-stepper-ring-color, var(--scs-color-border-strong, var(--scs-color-border-control)));
      border-radius: 50%;
      background: var(--scs-color-bg-surface);
      color: var(--scs-stepper-ahead-number, var(--scs-color-text-secondary));
      font-size: 12px;
      font-weight: var(--scs-stepper-number-weight, 600);
    }
    .name {
      font-size: var(--scs-stepper-text, 13px);
      font-weight: var(--scs-stepper-weight, 500);
      color: var(--scs-color-text-primary);
    }
    /* The line to the next step (the clinic's). */
    .line {
      display: var(--scs-stepper-line, block);
      flex: none;
      inline-size: 40px;
      block-size: 2px;
      border-radius: 999px;
      background: var(--scs-color-border-default);
    }
    li:last-child .line {
      display: none;
    }
    [data-state='done'] .circle {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
    }
    [data-state='done'] .line {
      background: var(--scs-color-brand-default);
    }
    [data-state='now'] .circle {
      border-width: var(--scs-stepper-now-ring, 2px);
      border-color: var(--scs-color-brand-default);
      color: var(--scs-stepper-now-number, var(--scs-color-brand-active));
    }
    /* Read out, not shown. */
    .unseen {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    [data-state='ahead'] .name {
      color: var(--scs-stepper-ahead-text, var(--scs-color-text-tertiary));
    }
  `,
  template: `
    <ol [attr.aria-label]="label() || null">
      @for (step of steps(); track $index; let index = $index) {
        <li [attr.data-state]="stateOf(index)" [attr.aria-current]="index === current() ? 'step' : null" [attr.data-testid]="'step-' + (index + 1)">
          <span class="circle" aria-hidden="true">
            @if (stateOf(index) === 'done') {
              <scs-icon name="check" [size]="14" />
            } @else {
              {{ numbers()[index] }}
            }
          </span>
          <span class="name">
            {{ step.label }}
            @if (stateOf(index) === 'done' && doneLabel()) {
              <span class="unseen">({{ doneLabel() }})</span>
            }
          </span>
          <span class="line" aria-hidden="true"></span>
        </li>
      }
    </ol>
  `,
})
export class ScsStepperComponent {
  readonly steps = input.required<readonly ScsStep[]>();
  /** The step the person is on, counted from 0; the steps before it are done. */
  readonly current = input(0);
  /** Already translated: the list's name. */
  readonly label = input('');
  /** Already translated: said after a step that is done ("done" · «مكتملة»). */
  readonly doneLabel = input('');
  /** Writes a step's number in the page's digits. */
  readonly digits = input<(value: number) => string>((value) => String(value));

  protected readonly numbers = computed(() => this.steps().map((_, index) => this.digits()(index + 1)));

  protected stateOf(index: number): 'done' | 'now' | 'ahead' {
    return index < this.current() ? 'done' : index === this.current() ? 'now' : 'ahead';
  }
}
