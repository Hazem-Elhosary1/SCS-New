import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsIconComponent, ScsIconName } from 'ui';
import { GalleryState } from '../gallery-state';

interface Slide {
  readonly mark: ScsIconName;
  readonly title: string;
  readonly text: string;
}

/**
 * Pattern — Onboarding (`253:2`), example data only: three cards, each with «تخطي» at the top's far
 * end, a round mark, a title and a line, the dots saying which card it is (they follow the reading
 * direction), and «التالي» — «ابدأ الآن» on the last. The working example walks through them; it
 * keeps nothing.
 */
@Component({
  selector: 'app-gallery-pattern-onboarding-section',
  imports: [NgTemplateOutlet, ScsIconComponent, ScsButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-spacing-lg);
    }
    /* On a phone every block keeps to the window: what is wider scrolls inside its own part. */
    :host > *,
    :host > * > * {
      min-inline-size: 0;
      max-inline-size: 100%;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(320px, 100%), 1fr));
      gap: var(--scs-spacing-md);
    }
    .caption {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .card {
      display: grid;
      grid-template-rows: auto 1fr auto auto;
      justify-items: center;
      gap: var(--scs-spacing-md);
      min-block-size: 520px;
      padding: var(--scs-spacing-lg) var(--scs-spacing-xl, 32px) var(--scs-spacing-lg);
      box-sizing: border-box;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-xl, 16px);
      background: var(--scs-color-bg-surface);
      text-align: center;
    }
    .top {
      justify-self: end;
      min-block-size: 20px;
    }
    /* A word to press: on a phone its room is 40 by 40, made around it. */
    .skip {
      min-block-size: var(--scs-touch, 0px);
      min-inline-size: var(--scs-touch, 0px);
      margin: min(0px, calc((1lh - var(--scs-touch, 0px)) / 2));
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-tertiary);
      font: inherit;
      font-size: var(--scs-text-label);
      cursor: pointer;
    }
    .middle {
      display: grid;
      justify-items: center;
      align-content: center;
      gap: var(--scs-spacing-md);
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 140px;
      block-size: 140px;
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
    }
    h3 {
      margin: var(--scs-spacing-md) 0 0;
      font-size: 22px;
      font-weight: 700;
      color: var(--scs-color-text-primary);
    }
    p.text {
      margin: 0;
      max-inline-size: 300px;
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
      line-height: 1.9;
    }
    .dots {
      display: flex;
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dots li {
      inline-size: 8px;
      block-size: 8px;
      border-radius: 50%;
      background: var(--scs-color-border-default);
    }
    .dots li[aria-current='step'] {
      background: var(--scs-color-brand-default);
    }
    .card > button {
      inline-size: 100%;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-sm);
      justify-items: start;
    }
    .live .card {
      inline-size: min(380px, 100%);
    }
  `,
  template: `
    <div class="grid">
      @for (slide of slides(); track slide.title; let at = $index) {
        <div [attr.data-testid]="'specimen-pattern-onboarding/pattern/card-' + (at + 1)">
          <ng-container *ngTemplateOutlet="card; context: { $implicit: at }" />
        </div>
      }
    </div>

    <div class="live" data-testid="live-pattern-onboarding">
      <p class="caption">{{ words().live }}</p>
      @if (done()) {
        <p class="caption" role="status" data-testid="live-onboarding-done">{{ words().doneSaid }}</p>
        <button type="button" scsButton="secondary" size="md" data-testid="live-onboarding-again" (click)="restart()">{{ words().again }}</button>
      } @else {
        <ng-container *ngTemplateOutlet="card; context: { $implicit: shown(), live: true }" />
      }
    </div>

    <ng-template #card let-at let-live="live">
      <section class="card" [attr.aria-label]="words().step(at + 1, slides().length)">
        <div class="top">
          @if (at < slides().length - 1) {
            <button type="button" class="skip" [attr.data-testid]="live ? 'live-onboarding-skip' : null" (click)="live && finish()">{{ words().skip }}</button>
          }
        </div>
        <div class="middle">
          <span class="mark" aria-hidden="true"><scs-icon [name]="slides()[at].mark" [size]="56" /></span>
          <h3>{{ slides()[at].title }}</h3>
          <p class="text">{{ slides()[at].text }}</p>
        </div>
        <ol class="dots" [attr.aria-label]="words().step(at + 1, slides().length)">
          @for (slide of slides(); track slide.title; let dot = $index) {
            <li [attr.aria-current]="dot === at ? 'step' : null"></li>
          }
        </ol>
        <button type="button" scsButton="primary" size="md" [attr.data-testid]="live ? 'live-onboarding-next' : null" (click)="live && next()">
          {{ at === slides().length - 1 ? words().start : words().next }}
        </button>
      </section>
    </ng-template>
  `,
})
export class PatternOnboardingSection {
  private readonly state = inject(GalleryState);

  protected readonly shown = signal(0);
  protected readonly done = signal(false);

  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          live: 'مثال يعمل',
          skip: 'تخطي',
          next: 'التالي',
          start: 'ابدأ الآن',
          again: 'من البداية',
          doneSaid: 'انتهى التعريف (مثال — لم يُحفظ شيء).',
          step: (at: number, of: number) => `الخطوة ${at} من ${of}`,
        }
      : {
          live: 'Working example',
          skip: 'Skip',
          next: 'Next',
          start: 'Get Started',
          again: 'Start again',
          doneSaid: 'The tour is over (an example — nothing was kept).',
          step: (at: number, of: number) => `Step ${at} of ${of}`,
        },
  );

  protected readonly slides = computed<Slide[]>(() =>
    this.state.language() === 'ar'
      ? [
          { mark: 'dashboard', title: 'مرحباً بك في لوحة تشغيل SCS', text: 'إدارة كل عيادات المنصة، الباقات، التذاكر، والفريق من مكان واحد.' },
          { mark: 'card', title: 'تابع كل عيادة أولاً بأول', text: 'راقب الاشتراكات، الاستخدام، وحالة كل عيادة على المنصة في الوقت الفعلي.' },
          { mark: 'check', title: 'كل شيء جاهز', text: 'ادخل إلى لوحة التحكم وابدأ يومك الأول كمشغّل.' },
        ]
      : [
          { mark: 'dashboard', title: 'Welcome to SCS Ops Console', text: 'Manage every clinic, plan, ticket, and team member — all from one place.' },
          { mark: 'card', title: 'Keep Every Clinic in View', text: 'Track subscriptions, usage, and health for every clinic on the platform in real time.' },
          { mark: 'check', title: "You're All Set", text: 'Jump into the dashboard and start managing your first day as an operator.' },
        ],
  );

  protected next(): void {
    if (this.shown() === this.slides().length - 1) {
      this.finish();
    } else {
      this.shown.update((at) => at + 1);
    }
  }

  protected finish(): void {
    this.done.set(true);
  }

  protected restart(): void {
    this.shown.set(0);
    this.done.set(false);
  }
}
