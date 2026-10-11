import { CdkConnectedOverlay, CdkOverlayOrigin } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, NotificationKind, NotificationsSource, ScsDatePipe } from 'core';
import {
  ScsEmptyStateComponent,
  ScsIconComponent,
  ScsIconName,
  ScsLinkDirective,
  ScsSkeletonComponent,
} from 'ui';
import { ago } from './ago';

const KIND_ICONS: Record<NotificationKind, ScsIconName> = {
  ticket: 'ticket',
  invoice: 'receipt',
  subscription: 'card',
  feedback: 'chat',
  notice: 'info',
  warning: 'alert-triangle',
  incident: 'alert-circle',
};

/**
 * The bell of the top bar and the panel it opens (spec 006, User Story 7; design: Notification
 * Panel, frames "Shell / Notifications Open" and "Notifications Empty"). What it shows comes
 * from `NotificationsSource`: the person's own notifications, from the platform (spec 008).
 */
@Component({
  selector: 'app-bell-panel',
  imports: [
    TranslocoPipe,
    ScsDatePipe,
    CdkOverlayOrigin,
    CdkConnectedOverlay,
    ScsIconComponent,
    ScsEmptyStateComponent,
    ScsSkeletonComponent,
    ScsLinkDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
    }
    .plain {
      display: inline-flex;
      align-items: center;
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      cursor: pointer;
    }
    .plain:hover,
    .plain[aria-expanded='true'] {
      background: var(--scs-color-bg-surface-secondary);
    }
    .plain:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .bell {
      position: relative;
      justify-content: center;
      inline-size: 40px;
      block-size: 40px;
      color: var(--scs-color-icon-default);
    }
    .dot {
      position: absolute;
      inset-block-start: 8px;
      inset-inline-end: 9px;
      inline-size: 8px;
      block-size: 8px;
      border: 2px solid var(--scs-color-bg-surface);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-feedback-danger-icon);
      box-sizing: content-box;
    }
    .panel {
      inline-size: 360px;
      max-block-size: 460px;
      display: flex;
      flex-direction: column;
    }
    .panel-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--scs-spacing-md);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    .panel-head h2 {
      margin: 0;
      font-size: var(--scs-text-body-lg);
      font-weight: 500;
    }
    .panel-head button {
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-link);
      font: inherit;
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      cursor: pointer;
    }
    .panel-list {
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
    }
    .panel-list button {
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-12);
      inline-size: 100%;
      padding: var(--scs-spacing-md);
      border: 0;
      border-block-end: 1px solid var(--scs-color-border-subtle, var(--scs-color-border-default));
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .panel-list button[data-read='false'] {
      background: var(--scs-color-brand-subtle);
    }
    .panel-list button:focus-visible,
    .panel-head button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: -2px;
    }
    .item-mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 36px;
      block-size: 36px;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-default);
    }
    .item-words {
      flex: 1;
      min-inline-size: 0;
    }
    .item-dot {
      flex: none;
      inline-size: 8px;
      block-size: 8px;
      margin-block-start: var(--scs-spacing-6);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-default);
    }
    .item-title {
      display: block;
      font-size: var(--scs-text-body);
      font-weight: 500;
    }
    .item-text {
      display: -webkit-box;
      overflow: hidden;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      margin-block-start: var(--scs-spacing-xs);
      font-size: var(--scs-text-body-sm);
      line-height: 1.7;
      color: var(--scs-color-text-secondary);
    }
    .item-time {
      display: block;
      margin-block-start: var(--scs-spacing-xs);
      font-size: var(--scs-text-caption);
      color: var(--scs-color-text-tertiary);
    }
    .panel-body {
      padding: var(--scs-spacing-md);
    }
  `,
  template: `
    <button
      #bell
      type="button"
      class="plain bell"
      data-testid="bell"
      cdkOverlayOrigin
      #bellOrigin="cdkOverlayOrigin"
      aria-haspopup="dialog"
      [attr.aria-expanded]="bellOpen()"
      [attr.aria-label]="'app.shell.notifications.title' | transloco"
      (click)="setOpen(!bellOpen())"
    >
      <scs-icon name="bell" [size]="20" />
      @if (notifications.unread()) {
        <span class="dot" data-testid="bell-dot"></span>
      }
    </button>
    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="bellOrigin"
      [cdkConnectedOverlayOpen]="bellOpen()"
      [cdkConnectedOverlayPositions]="underFarEdge"
      (overlayOutsideClick)="pressedOutside($event)"
      (overlayKeydown)="$event.key === 'Escape' && closeBell()"
    >
      <section
        class="scs-popover panel"
        role="dialog"
        data-testid="notification-panel"
        [attr.aria-label]="'app.shell.notifications.title' | transloco"
      >
        <div class="panel-head">
          <h2>{{ 'app.shell.notifications.title' | transloco }}</h2>
          @if (notifications.state() === 'ready' && items().length > 0) {
            <button type="button" data-testid="mark-all-read" (click)="notifications.markAllRead()">
              {{ 'app.shell.notifications.markAll' | transloco }}
            </button>
          }
        </div>
        @switch (notifications.state()) {
          @case ('loading') {
            <div class="panel-body">
              <scs-skeleton [rows]="5" [label]="'ui.state.loading.title' | transloco" />
            </div>
          }
          @case ('failed') {
            <scs-empty-state
              context="error"
              data-testid="notifications-failed"
              [title]="'app.shell.notifications.failed' | transloco"
            >
              <button
                type="button"
                scsLink
                data-testid="notifications-retry"
                (click)="notifications.retry()"
              >
                {{ 'ui.state.error.retry' | transloco }}
              </button>
            </scs-empty-state>
          }
          @default {
            @if (items().length === 0) {
              <scs-empty-state
                data-testid="notifications-empty"
            mark="bell"
                [title]="'app.shell.notifications.emptyTitle' | transloco"
                [text]="'app.shell.notifications.emptyText' | transloco"
              />
            } @else {
              <ul class="panel-list" data-testid="notification-list" (scroll)="scrolled($event)">
                @for (item of items(); track item.id) {
                  <li>
                    <button
                      type="button"
                      data-testid="notification-item"
                      [attr.data-read]="item.read"
                      (click)="openNotification(item.id)"
                    >
                      <span class="item-mark"
                        ><scs-icon [name]="kindIcons[item.kind]" [size]="18"
                      /></span>
                      <span class="item-words">
                        <span class="item-title">{{ item.title }}</span>
                        <span class="item-text">{{ item.text }}</span>
                        <span class="item-time" [attr.title]="item.at | scsDate: 'dateTime'">{{
                          ago(item.at)
                        }}</span>
                      </span>
                      @if (!item.read) {
                        <span class="item-dot"></span>
                      }
                    </button>
                  </li>
                }
              </ul>
            }
          }
        }
      </section>
    </ng-template>
  `,
})
export class BellPanelComponent {
  private readonly language = inject(LanguageService);
  protected readonly notifications = inject(NotificationsSource);

  protected readonly kindIcons = KIND_ICONS;
  protected readonly bellOpen = signal(false);
  private readonly bellButton = viewChild.required<ElementRef<HTMLButtonElement>>('bell');

  /** Under the top bar, aligned to the bell's far edge. */
  protected readonly underFarEdge = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 12 } as const,
  ];

  /** Newest first, whatever order the source keeps them in. */
  protected readonly items = computed(() =>
    [...this.notifications.items()].sort((a, b) => b.at.localeCompare(a.at)),
  );

  /** How long ago, as people say it: «منذ ساعتين», «أمس». The exact time is on hover. */
  protected ago(at: string): string {
    return ago(at, this.language.language());
  }

  protected setOpen(open: boolean): void {
    this.bellOpen.set(open);
    this.notifications.setOpen(open);
  }

  /** Reaching the end of the list brings the older ones (spec 008, FR-022). */
  protected scrolled(event: Event): void {
    const list = event.target as HTMLElement;
    if (
      this.notifications.hasMore() &&
      list.scrollTop + list.clientHeight >= list.scrollHeight - 48
    ) {
      this.notifications.loadMore();
    }
  }

  /** A press anywhere else closes the panel; a press on the bell itself is the bell's own. */
  protected pressedOutside(event: MouseEvent): void {
    if (!this.bellButton().nativeElement.contains(event.target as Node)) {
      this.setOpen(false);
    }
  }

  protected closeBell(): void {
    this.setOpen(false);
    this.bellButton().nativeElement.focus();
  }

  protected openNotification(id: string): void {
    const item = this.notifications.items().find((candidate) => candidate.id === id);
    if (item !== undefined) {
      this.notifications.open(item);
    }
    this.closeBell();
  }
}
