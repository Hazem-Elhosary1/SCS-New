import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsBannerComponent, ScsBannerTone, ScsToastComponent, ScsToastOutletComponent, ScsToastTone, ToastService } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

type Words = Record<string, { title: string; text: string }>;

/**
 * The Alert page (60:46 / 257:71): the banner and the toast in four intents; working: a banner with
 * its action and its close, and a button that sends a real passing notice to the console's outlet.
 */
@Component({
  selector: 'app-gallery-alert-section',
  imports: [ScsBannerComponent, ScsToastComponent, ScsToastOutletComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    scs-banner {
      inline-size: 100%;
      max-inline-size: 420px;
    }
    .cells {
      --gallery-cell: min(440px, 100%);
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-md);
      justify-items: start;
    }
    .send {
      min-block-size: 40px;
      padding: var(--scs-spacing-sm) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      cursor: pointer;
    }
  `,
  template: `
    <app-gallery-component class="cells" page="Alert" set="Alert">
      <ng-template appGallerySpecimen let-values>
        @if (values['Style'] === 'Banner') {
          <scs-banner [tone]="bannerTone(values['Intent'])" [title]="banner()[values['Intent']].title" [closeLabel]="words().close">
            {{ banner()[values['Intent']].text }}
          </scs-banner>
        } @else {
          <scs-toast
            [tone]="toastTone(values['Intent'])"
            [title]="toast()[values['Intent']].title"
            [text]="toast()[values['Intent']].text"
            [closeLabel]="words().close"
          />
        }
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-banner tone="warning" [title]="banner()['Warning'].title" [actionLabel]="words().renew" [closeLabel]="words().close">
            {{ banner()['Warning'].text }}
          </scs-banner>
          <button type="button" class="send" (click)="send()">{{ words().send }}</button>
          <!-- The gallery is outside the console's frame, which holds the outlet: it has its own here. -->
          <scs-toast-outlet [closeLabel]="words().close" />
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class AlertSection {
  private readonly state = inject(GalleryState);
  private readonly toasts = inject(ToastService);

  /** The page's words (Alert 60:46 and its Arabic twin 257:71). */
  protected readonly banner = computed<Words>(() =>
    this.state.language() === 'ar'
      ? {
          Info: { title: 'ميزة جديدة متاحة', text: 'التصدير المجمع للتذاكر متاح الآن ضمن التقارير.' },
          Success: { title: 'تم تحديث الباقة', text: 'تم حفظ تغييرات الباقة الاحترافية بنجاح.' },
          Warning: { title: 'الاشتراك على وشك الانتهاء', text: '3 عيادات ستفقد الوصول خلال 5 أيام ما لم يتم التجديد.' },
          Error: { title: 'فشل مزامنة العيادة', text: 'أعد المحاولة أو تواصل مع فريق الهندسة إذا استمرت المشكلة.' },
        }
      : {
          Info: { title: 'New feature available', text: 'Bulk export for tickets is now live under Reports.' },
          Success: { title: 'Plan updated', text: 'The Professional plan changes were saved successfully.' },
          Warning: { title: 'Subscription expiring soon', text: '3 clinics will lose access in 5 days unless renewed.' },
          Error: { title: 'Failed to sync tenant', text: 'Retry the sync or contact engineering if this persists.' },
        },
  );
  protected readonly toast = computed<Words>(() =>
    this.state.language() === 'ar'
      ? {
          Info: { title: 'رسالة جديدة', text: 'وصلت رسالة جديدة من مريض عبر واتساب.' },
          Success: { title: 'تم بنجاح', text: 'تم حفظ بيانات الحجز.' },
          Warning: { title: 'تنبيه', text: 'الموعد قريب من ساعات الذروة.' },
          Error: { title: 'حدث خطأ', text: 'تعذّر إرسال الرسالة، حاول مجدداً.' },
        }
      : {
          Info: { title: 'New Message', text: 'New message received from a patient via WhatsApp.' },
          Success: { title: 'Success', text: 'Booking details saved.' },
          Warning: { title: 'Warning', text: 'This appointment is close to peak hours.' },
          Error: { title: 'Error', text: 'Failed to send message. Please try again.' },
        },
  );
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { close: 'إغلاق', renew: 'تجديد', send: 'أرسل إشعاراً' }
      : { close: 'Close', renew: 'Renew', send: 'Send a notice' },
  );

  protected bannerTone(intent: string): ScsBannerTone {
    return intent === 'Error' ? 'danger' : (intent.toLowerCase() as ScsBannerTone);
  }

  protected toastTone(intent: string): ScsToastTone {
    return intent === 'Error' ? 'danger' : (intent.toLowerCase() as ScsToastTone);
  }

  protected send(): void {
    const words = this.toast()['Success'];
    this.toasts.show(words.title, words.text, 'success');
  }
}
