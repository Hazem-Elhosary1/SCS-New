import { ChangeDetectionStrategy, Component, computed, inject, signal, viewChild } from '@angular/core';
import { ScsNotificationBellComponent, ScsNotificationItemComponent, ScsNotificationPanelComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Notifications page (362:2): the bell (default, under the pointer, unread), a notice unread and
 * read, and the panel (364:32); working: a bell that opens its panel, whose notices are read one by
 * one or all at once.
 */
@Component({
  selector: 'app-gallery-notifications-section',
  imports: [
    ScsNotificationBellComponent,
    ScsNotificationItemComponent,
    ScsNotificationPanelComponent,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    app-gallery-component + app-gallery-component {
      display: block;
      margin-block-start: var(--scs-spacing-xl, 32px);
    }
    .items {
      --gallery-cell: min(380px, 100%);
    }
    scs-notification-item {
      inline-size: 100%;
      max-inline-size: 360px;
    }
    .live {
      display: grid;
      justify-items: start;
      gap: var(--scs-spacing-sm);
    }
  `,
  template: `
    <app-gallery-component page="Notifications" set="Notification Bell">
      <ng-template appGallerySpecimen let-values>
        <scs-notification-bell [count]="values['State'] === 'Unread' ? 3 : 0" [label]="words().bell(values['State'] === 'Unread' ? 3 : 0)" />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-notification-bell [count]="unread()" [label]="words().bell(unread())" [expanded]="open()" (pressed)="toggle()" />
          @if (open()) {
            <scs-notification-panel
              #panel
              [heading]="words().title"
              [markAllLabel]="words().markAll"
              [viewAllLabel]="words().viewAll"
              (markAll)="read.set([0, 1, 2])"
              (closed)="open.set(false)"
            >
              @for (notice of notices(); track $index; let index = $index) {
                <scs-notification-item
                  [title]="notice.title"
                  [text]="notice.text"
                  [time]="notice.time"
                  [icon]="notice.icon"
                  [unread]="!read().includes(index)"
                  [unreadLabel]="words().unread"
                  (opened)="markRead(index)"
                />
              }
            </scs-notification-panel>
          }
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="items" page="Notifications" set="Notification Item">
      <ng-template appGallerySpecimen let-values>
        <scs-notification-item
          [title]="notices()[0].title"
          [text]="notices()[0].text"
          [time]="notices()[0].time"
          [unread]="values['State'] === 'Unread'"
          [unreadLabel]="words().unread"
        />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Composed Example — Notification Panel" node="364:32" testId="specimen-notifications/parts/notification-panel">
      <scs-notification-panel [heading]="words().title" [markAllLabel]="words().markAll" [viewAllLabel]="words().viewAll">
        @for (notice of notices(); track $index; let index = $index) {
          <scs-notification-item [title]="notice.title" [text]="notice.text" [time]="notice.time" [icon]="notice.icon" [unread]="index < 2" [unreadLabel]="words().unread" />
        }
      </scs-notification-panel>
    </app-gallery-part>
  `,
})
export class NotificationsSection {
  private readonly state = inject(GalleryState);
  private readonly panel = viewChild<ScsNotificationPanelComponent>('panel');

  /** The page's words (Notifications 362:2 and its Arabic twins 365:59, 365:77). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          title: 'الإشعارات',
          markAll: 'تحديد الكل كمقروء',
          viewAll: 'عرض كل الإشعارات',
          unread: 'غير مقروء',
          bell: (count: number) => (count ? `الإشعارات، ${count} غير مقروءة` : 'الإشعارات'),
        }
      : {
          title: 'Notifications',
          markAll: 'Mark all as read',
          viewAll: 'View all notifications',
          unread: 'Unread',
          bell: (count: number) => (count ? `Notifications, ${count} unread` : 'Notifications'),
        },
  );
  protected readonly notices = computed(() =>
    this.state.language() === 'ar'
      ? [
          { title: 'تم تعيين تذكرة جديدة', text: 'تذكرة #482 من عيادة النور تحتاج مراجعة.', time: 'منذ 5 دقائق', icon: 'ticket' as const },
          { title: 'تم تحديث الباقة', text: 'انتقلت عيادة الشفاء إلى الباقة الاحترافية.', time: 'منذ ساعة', icon: 'receipt' as const },
          { title: 'اكتملت المزامنة', text: 'تمت مزامنة 12 عيادة بنجاح.', time: 'أمس', icon: 'refresh' as const },
        ]
      : [
          { title: 'New ticket assigned', text: 'Ticket #482 from Al Noor Clinic needs review.', time: '5 minutes ago', icon: 'ticket' as const },
          { title: 'Plan updated', text: 'Al Shifa Clinic moved to the Professional plan.', time: '1 hour ago', icon: 'receipt' as const },
          { title: 'Sync complete', text: '12 clinics were synced successfully.', time: 'Yesterday', icon: 'refresh' as const },
        ],
  );

  protected readonly open = signal(false);
  protected readonly read = signal<number[]>([2]);
  protected readonly unread = computed(() => this.notices().length - this.read().length);

  protected toggle(): void {
    this.open.set(!this.open());
    if (this.open()) setTimeout(() => this.panel()?.focus());
  }

  protected markRead(index: number): void {
    this.read.update((read) => (read.includes(index) ? read : [...read, index]));
  }
}
