import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, booleanAttribute, input, model } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsIconName } from '../icon/icon-registry';

/**
 * One place in the side bar (design: Sidebar Nav Item — `294:323` / `8:119`; the clinic's
 * `4252:60310`): its icon and its name, a link to its page. The page the person is on is marked —
 * tinted, its name in the brand's colour, a bar at its start — and announced as the current page. A
 * place that may not be opened is grey. The clinic's item can also carry a count and open a group
 * of sub-items (`expandable`), which a sub-item (`level` sub) sits under. Each look draws its own
 * item through the `--scs-nav-*` settings: the clinic's 40-pixel row by default, the operators' 48.
 */
@Component({
  selector: 'scs-nav-item',
  imports: [ScsIconComponent, RouterLink, RouterLinkActive, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-level]': 'level()' },
  styles: `
    :host {
      display: block;
    }
    .item {
      position: relative;
      display: flex;
      align-items: center;
      gap: var(--scs-nav-gap, 12px);
      box-sizing: border-box;
      inline-size: 100%;
      block-size: var(--scs-nav-height, 40px);
      padding-inline: var(--scs-nav-pad, 12px);
      border: 0;
      border-radius: var(--scs-nav-radius, 10px);
      background: none;
      color: var(--scs-nav-text, var(--scs-color-text-secondary));
      font: inherit;
      font-size: 14px;
      font-weight: 500;
      text-align: start;
      text-decoration: none;
      cursor: pointer;
    }
    :host([data-level='sub']) .item {
      padding-inline-start: calc(var(--scs-nav-pad, 12px) + var(--scs-nav-icon, 20px) + var(--scs-nav-gap, 12px));
    }
    .item:hover:not([aria-disabled='true']),
    :host-context([data-scs-state~='hover']) .item {
      background: var(--scs-nav-hover-bg, var(--scs-color-bg-surface-secondary));
    }
    .item:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: -2px;
    }
    .item.on {
      background: var(--scs-color-brand-subtle);
      color: var(--scs-nav-on-text, var(--scs-color-brand-active));
      font-weight: var(--scs-nav-on-weight, 500);
    }
    /* The bar at the start of the current page's item. */
    .item.on::before {
      content: '';
      position: absolute;
      inset-inline-start: 0;
      inset-block-start: 50%;
      inline-size: 3px;
      block-size: var(--scs-nav-bar, 20px);
      border-radius: var(--scs-nav-bar-radius, 999px);
      background: var(--scs-color-brand-default);
      transform: translateY(-50%);
    }
    .item[aria-disabled='true'] {
      color: var(--scs-nav-off-text, var(--scs-color-text-placeholder));
      cursor: not-allowed;
    }
    .mark {
      flex: none;
    }
    /* Each look's icon size (20 in the clinic's file, 18 in the operators'). */
    :host ::ng-deep .mark svg {
      inline-size: var(--scs-nav-icon, 20px);
      block-size: var(--scs-nav-icon, 20px);
    }
    .words {
      flex: 1;
      min-inline-size: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .count {
      display: inline-grid;
      place-items: center;
      min-inline-size: 20px;
      block-size: 20px;
      padding-inline: 6px;
      box-sizing: border-box;
      border-radius: 999px;
      background: var(--scs-color-border-default);
      color: var(--scs-color-text-secondary);
      font-size: 11px;
      font-weight: 600;
    }
    .chevron {
      flex: none;
      transition: transform 0.15s;
    }
    [aria-expanded='true'] .chevron {
      transform: rotate(180deg);
    }
    @media (prefers-reduced-motion: reduce) {
      .chevron {
        transition: none;
      }
    }
  `,
  template: `
    <ng-template #inside>
      @if (icon(); as name) {
        <scs-icon class="mark" [name]="name" [size]="20" />
      }
      <span class="words">{{ label() }}</span>
      @if (count()) {
        <span class="count" data-testid="nav-count">{{ count() }}</span>
      }
      @if (expandable()) {
        <scs-icon class="chevron" name="chevron-down" [size]="16" />
      }
    </ng-template>
    @if (link() && !expandable() && !disabled()) {
      <a
        class="item"
        [class.on]="active() ?? linked.isActive"
        [routerLink]="link()"
        routerLinkActive
        #linked="routerLinkActive"
        [routerLinkActiveOptions]="{ exact: exact() }"
        [attr.aria-current]="(active() ?? linked.isActive) ? 'page' : null"
        [attr.data-testid]="testId() || null"
      >
        <ng-container [ngTemplateOutlet]="inside" />
      </a>
    } @else {
      <button
        type="button"
        class="item"
        [class.on]="active() === true"
        [attr.aria-current]="active() === true ? 'page' : null"
        [attr.aria-expanded]="expandable() ? expanded() : null"
        [attr.aria-disabled]="disabled() ? 'true' : null"
        [attr.data-testid]="testId() || null"
        (click)="toggle()"
      >
        <ng-container [ngTemplateOutlet]="inside" />
      </button>
    }
  `,
})
export class ScsNavItemComponent {
  readonly label = input.required<string>();
  readonly icon = input<ScsIconName | null>(null);
  /** The page it opens. */
  readonly link = input<string | null>(null);
  /** Marked only on its own page, not on the pages under it. */
  readonly exact = input(false, { transform: booleanAttribute });
  /** Marks it as the current page whatever the address says (the gallery's still specimens). */
  readonly active = input<boolean | null>(null);
  readonly disabled = input(false, { transform: booleanAttribute });
  /** A number after the name: how many wait there. */
  readonly count = input('');
  readonly level = input<'top' | 'sub'>('top');
  /** Opens and closes a group of sub-items instead of opening a page. */
  readonly expandable = input(false, { transform: booleanAttribute });
  readonly expanded = model(false);
  readonly testId = input('');

  protected toggle(): void {
    if (!this.disabled() && this.expandable()) this.expanded.set(!this.expanded());
  }
}
