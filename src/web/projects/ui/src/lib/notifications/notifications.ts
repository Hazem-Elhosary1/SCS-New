import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ScsIconComponent } from '../icon/icon.component';
import { ScsIconName } from '../icon/icon-registry';

let nextId = 0;

/**
 * The bell in the top bar (design: Notifications — Notification Bell, `362:23` / `365:59`): a round
 * button that tints under the pointer and carries a red mark while something is unread — the number
 * when `count` is given, a dot otherwise. Its name says how many are unread (`label`, already
 * translated with the number); Enter or Space opens the panel the page ties to it.
 */
@Component({
  selector: 'scs-notification-bell',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
    }
    button {
      position: relative;
      display: grid;
      place-items: center;
      inline-size: 40px;
      block-size: 40px;
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: none;
      color: var(--scs-color-icon-default);
      cursor: pointer;
    }
    button:hover,
    button[aria-expanded='true'],
    :host-context([data-scs-state~='hover']) button {
      background: var(--scs-color-bg-surface-secondary);
    }
    button:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    .mark {
      position: absolute;
      inset-block-start: 6px;
      inset-inline-end: 6px;
      box-sizing: border-box;
      min-inline-size: 10px;
      block-size: 10px;
      border: 1.5px solid var(--scs-color-bg-surface);
      border-radius: 999px;
      background: var(--scs-color-feedback-danger-icon);
    }
    .mark.number {
      inset-block-start: 2px;
      inset-inline-end: 2px;
      display: grid;
      place-items: center;
      min-inline-size: 18px;
      block-size: 18px;
      padding-inline: 4px;
      color: var(--scs-color-brand-on-brand);
      font-size: 11px;
      font-weight: 600;
      line-height: 1;
    }
  `,
  template: `
    <button
      type="button"
      data-testid="notification-bell"
      [attr.aria-label]="label()"
      [attr.aria-expanded]="expanded()"
      aria-haspopup="true"
      (click)="pressed.emit()"
    >
      <scs-icon name="bell" [size]="20" />
      @if (unread()) {
        @if (count() !== null) {
          <span class="mark number" aria-hidden="true" data-testid="notification-count">{{ shownCount() }}</span>
        } @else {
          <span class="mark" aria-hidden="true" data-testid="notification-dot"></span>
        }
      }
    </button>
  `,
})
export class ScsNotificationBellComponent {
  /** How many are unread, written on the mark; `null` draws a dot while `unread`. */
  readonly count = input<number | null>(null);
  /** Something is unread (true whenever `count` is above 0). */
  readonly hasUnread = input(false, { transform: booleanAttribute });
  /** Already translated, with the number: "Notifications, 3 unread". */
  readonly label = input.required<string>();
  /** The panel it opens is open. */
  readonly expanded = input(false, { transform: booleanAttribute });
  /** Writes the count in the page's digits. */
  readonly digits = input<(value: number) => string>((value) => String(value));

  readonly pressed = output<void>();

  protected readonly unread = computed(() => (this.count() ?? 0) > 0 || this.hasUnread());
  protected readonly shownCount = computed(() => {
    const count = this.count() ?? 0;
    return count > 9 ? `${this.digits()(9)}+` : this.digits()(count);
  });
}

/**
 * One notice in the panel (design: Notifications — Notification Item, `362:43` / `365:77`; the
 * clinic's Notification Item): its icon, what happened, a line more and when. An unread one is tinted
 * and its title is heavier, with a dot at its end. It is a menu item: Enter opens what it is about.
 */
@Component({
  selector: 'scs-notification-item',
  imports: [ScsIconComponent, RouterLink, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.unread]': 'unread()' },
  styles: `
    :host {
      display: block;
    }
    .row {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      box-sizing: border-box;
      inline-size: 100%;
      padding: 12px 16px;
      border: 0;
      border-radius: var(--scs-notice-radius, 0px);
      background: var(--scs-color-bg-surface);
      color: inherit;
      font: inherit;
      text-align: start;
      text-decoration: none;
      cursor: pointer;
    }
    :host(.unread) .row {
      background: var(--scs-notice-unread-bg, var(--scs-color-brand-subtle));
    }
    .row:hover,
    .row:focus,
    :host-context([data-scs-state~='hover']) .row {
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
      outline: none;
    }
    .row:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: -2px;
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 32px;
      block-size: 32px;
      border-radius: 50%;
      background: var(--scs-color-feedback-info-bg);
      color: var(--scs-color-feedback-info-icon);
    }
    .words {
      flex: 1;
      display: grid;
      gap: 4px;
      min-inline-size: 0;
    }
    .title {
      font-size: 14px;
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    :host(.unread) .title {
      font-weight: 600;
    }
    .text {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    .time {
      font-size: 12px;
      color: var(--scs-color-text-placeholder);
    }
    .dot {
      flex: none;
      inline-size: 6px;
      block-size: 6px;
      margin-block-start: 6px;
      border-radius: 50%;
    }
    :host(.unread) .dot {
      background: var(--scs-notice-dot, transparent);
    }
    .unseen {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
  template: `
    <ng-template #inside>
      <span class="mark" aria-hidden="true"><scs-icon [name]="icon()" [size]="14" /></span>
      <span class="words">
        <span class="title">
          @if (unread() && unreadLabel()) {
            <span class="unseen">{{ unreadLabel() }}: </span>
          }
          {{ title() }}
        </span>
        @if (text()) {
          <span class="text">{{ text() }}</span>
        }
        @if (time()) {
          <span class="time">{{ time() }}</span>
        }
      </span>
      <span class="dot" aria-hidden="true"></span>
    </ng-template>
    @if (link()) {
      <a class="row" role="menuitem" tabindex="-1" [routerLink]="link()" (click)="opened.emit()">
        <ng-container [ngTemplateOutlet]="inside" />
      </a>
    } @else {
      <button type="button" class="row" role="menuitem" tabindex="-1" (click)="opened.emit()">
        <ng-container [ngTemplateOutlet]="inside" />
      </button>
    }
  `,
})
export class ScsNotificationItemComponent {
  /** Already translated. */
  readonly title = input.required<string>();
  readonly text = input('');
  /** Already written: "5 minutes ago" · «منذ 5 دقائق». */
  readonly time = input('');
  readonly icon = input<ScsIconName>('ticket');
  readonly unread = input(false, { transform: booleanAttribute });
  /** Already translated: said before an unread notice's title ("Unread" · «غير مقروء»). */
  readonly unreadLabel = input('');
  /** Where it leads. */
  readonly link = input<string | null>(null);

  readonly opened = output<void>();
}

/**
 * The panel the bell opens (design: Notifications — notification-panel, `364:32`; the clinic's
 * Notification Panel `4262:1056`): its title and "Mark all as read", the notices as a menu — the
 * arrows move between them, Escape closes — and "View all notifications" under them. Each look draws
 * it through `--scs-notice-*`: the clinic's corners 16 and grey footer by default, the operators'
 * corners 12.
 */
@Component({
  selector: 'scs-notification-panel',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      inline-size: min(var(--scs-notice-panel-width, 360px), 100%);
      overflow: hidden;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-notice-panel-radius, 16px);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-notice-panel-shadow, 0 12px 24px rgb(10 15 20 / 14%), 0 4px 8px rgb(10 15 20 / 8%));
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: 14px 20px;
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    h2 {
      margin: 0;
      font-size: var(--scs-notice-title, 15px);
      font-weight: var(--scs-notice-title-weight, 600);
      color: var(--scs-color-text-primary);
    }
    .link {
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-link, var(--scs-color-brand-default));
      font: inherit;
      font-size: 12px;
      font-weight: 500;
      text-decoration: none;
      cursor: pointer;
    }
    .link:hover {
      text-decoration: underline;
    }
    .link:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: 2px;
    }
    [role='menu'] {
      display: grid;
      gap: var(--scs-notice-gap, 0px);
      max-block-size: 360px;
      overflow-y: auto;
      padding: var(--scs-notice-list-pad, 0px);
    }
    .none {
      padding: 32px 20px;
      color: var(--scs-color-text-tertiary);
      font-size: 13px;
      text-align: center;
    }
    footer {
      display: grid;
      place-items: center;
      padding: 14px 20px;
      border-block-start: 1px solid var(--scs-color-border-default);
      background: var(--scs-notice-footer-bg, var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary)));
    }
    footer .link {
      font-size: 13px;
    }
  `,
  template: `
    <header>
      <h2 [id]="id + '-title'">{{ heading() }}</h2>
      @if (markAllLabel()) {
        <button type="button" class="link" data-testid="notifications-mark-all" (click)="markAll.emit()">{{ markAllLabel() }}</button>
      }
    </header>
    <div role="menu" tabindex="-1" [attr.aria-labelledby]="id + '-title'" (keydown)="key($event)">
      <ng-content />
    </div>
    @if (empty() && emptyLabel()) {
      <p class="none" data-testid="notifications-none">{{ emptyLabel() }}</p>
    }
    @if (viewAllLabel()) {
      <footer>
        @if (viewAllLink()) {
          <a class="link" data-testid="notifications-view-all" [routerLink]="viewAllLink()">{{ viewAllLabel() }}</a>
        } @else {
          <button type="button" class="link" data-testid="notifications-view-all" (click)="viewAll.emit()">{{ viewAllLabel() }}</button>
        }
      </footer>
    }
  `,
})
export class ScsNotificationPanelComponent {
  protected readonly id = `scs-notices-${nextId++}`;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Already translated: "Notifications" · «الإشعارات». */
  readonly heading = input.required<string>();
  readonly markAllLabel = input('');
  readonly viewAllLabel = input('');
  readonly viewAllLink = input<string | null>(null);
  /** There are no notices: the panel says `emptyLabel`. */
  readonly empty = input(false, { transform: booleanAttribute });
  readonly emptyLabel = input('');

  readonly markAll = output<void>();
  readonly viewAll = output<void>();
  /** Escape was pressed: the page closes the panel and gives the focus back to the bell. */
  readonly closed = output<void>();

  /** The first notice takes the focus (the page calls it when the panel opens). */
  focus(): void {
    setTimeout(() => this.items()[0]?.focus());
  }

  private items(): HTMLElement[] {
    return [...this.host.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]')];
  }

  protected key(event: KeyboardEvent): void {
    const items = this.items();
    const at = items.indexOf(document.activeElement as HTMLElement);
    const to: Record<string, number> = { ArrowDown: at + 1, ArrowUp: at - 1, Home: 0, End: items.length - 1 };
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closed.emit();
    } else if (to[event.key] !== undefined && items.length) {
      event.preventDefault();
      items[(to[event.key] + items.length) % items.length].focus();
    }
  }
}

