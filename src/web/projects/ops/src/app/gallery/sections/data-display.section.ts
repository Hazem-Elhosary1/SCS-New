import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsAccordionComponent, ScsAccordionItemComponent, ScsAvatarComponent, ScsCardComponent, ScsTagComponent, ScsTagIntent } from 'ui';
import { DividerComponent } from '../../components/divider/divider.component';
import { StatComponent } from '../../components/stat/stat.component';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The Data Display page (281:2): the card in its three states, the tag in four intents with and
 * without its remove button, the accordion item open and closed, and the console's own divider and
 * stat; working: cards to choose from, tags to take away, an accordion to open.
 */
@Component({
  selector: 'app-gallery-data-display-section',
  imports: [
    NgTemplateOutlet,
    ScsCardComponent,
    ScsTagComponent,
    ScsAccordionComponent,
    ScsAccordionItemComponent,
    ScsAvatarComponent,
    DividerComponent,
    StatComponent,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    app-gallery-component + app-gallery-component {
      display: block;
      margin-block-start: var(--scs-spacing-xl, 32px);
    }
    .wide {
      --gallery-cell: min(420px, 100%);
    }
    scs-card {
      inline-size: 100%;
      max-inline-size: 220px;
    }
    .who {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .who strong {
      display: block;
      font-size: 13px;
      font-weight: 600;
    }
    .who span,
    .plan {
      font-size: 11px;
      color: var(--scs-color-text-tertiary);
    }
    .plan {
      margin: 12px 0 0;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md);
    }
    .tags {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-sm);
    }
    scs-accordion,
    app-divider {
      inline-size: 100%;
      max-inline-size: 400px;
    }
  `,
  template: `
    <app-gallery-component page="Data Display" set="Card">
      <ng-template appGallerySpecimen let-values>
        <scs-card [interactive]="values['State'] === 'Hover'" [selected]="values['State'] === 'Selected'">
          <ng-container [ngTemplateOutlet]="clinicCard" />
        </scs-card>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          @for (card of [0, 1]; track card) {
            <scs-card
              interactive
              role="button"
              tabindex="0"
              [attr.aria-pressed]="chosen() === card"
              [selected]="chosen() === card"
              (click)="chosen.set(card)"
              (keydown.enter)="chosen.set(card)"
              (keydown.space)="$any($event).preventDefault(); chosen.set(card)"
            >
              <ng-container [ngTemplateOutlet]="clinicCard" />
            </scs-card>
          }
        </div>
      </ng-template>
    </app-gallery-component>
    <ng-template #clinicCard>
      <div class="who">
        <scs-avatar size="sm" [name]="words().clinic" />
        <div>
          <strong>{{ words().clinic }}</strong>
          <span>{{ words().city }}</span>
        </div>
      </div>
      <p class="plan">{{ words().plan }}</p>
    </ng-template>

    <app-gallery-component page="Data Display" set="Tag">
      <ng-template appGallerySpecimen let-values>
        <scs-tag
          [intent]="intentOf(values['Intent'])"
          [dismissible]="values['Dismissible'] === 'true'"
          [removeLabel]="words().remove + ' ' + words().tags[values['Intent']]"
        >
          {{ words().tags[values['Intent']] }}
        </scs-tag>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="tags">
          @for (tag of kept(); track tag) {
            <scs-tag dismissible [intent]="intentOf(tag)" [removeLabel]="words().remove + ' ' + words().tags[tag]" (removed)="drop(tag)">{{ words().tags[tag] }}</scs-tag>
          }
          <scs-tag size="sm" selected>{{ words().tags['Default'] }}</scs-tag>
          <scs-tag size="sm" disabled>{{ words().tags['Neutral'] }}</scs-tag>
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="wide" page="Data Display" set="Accordion Item">
      <ng-template appGallerySpecimen let-values>
        <scs-accordion>
          @if (values['State'] === 'Expanded') {
            <scs-accordion-item [heading]="words().billing" [expanded]="true">{{ words().invoice }}</scs-accordion-item>
          } @else {
            <scs-accordion-item [heading]="words().limits" />
          }
        </scs-accordion>
      </ng-template>
      <ng-template appGalleryLive>
        <scs-accordion>
          <scs-accordion-item [heading]="words().billingOpen">{{ words().invoice }}</scs-accordion-item>
          <scs-accordion-item [heading]="words().limitsOpen">{{ words().usage }}</scs-accordion-item>
        </scs-accordion>
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="wide" page="Data Display" set="Divider">
      <ng-template appGallerySpecimen let-values>
        <app-divider [label]="values['Type'] === 'With Label' ? words().or : ''" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component page="Data Display" set="Stat">
      <ng-template appGallerySpecimen let-values>
        <app-stat
          [label]="stat(values['Trend']).label"
          [value]="stat(values['Trend']).value"
          [note]="stat(values['Trend']).note"
          [trend]="trendOf(values['Trend'])"
        />
      </ng-template>
    </app-gallery-component>
  `,
})
export class DataDisplaySection {
  private readonly state = inject(GalleryState);
  /** The page's words (Data Display 281:2 and its Arabic twins 313:26 … 313:106). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          clinic: 'عيادة النور',
          city: 'مدينة نصر',
          plan: 'الباقة الاحترافية — 8 مستخدمين، فرعين',
          tags: { Default: 'أسنان', Success: 'نشط', Warning: 'كبار الشخصيات', Neutral: 'عام' } as Record<string, string>,
          remove: 'إزالة',
          billing: 'تفاصيل الفواتير (موسّع)',
          limits: 'حدود الاستخدام (مطوي)',
          billingOpen: 'تفاصيل الفواتير',
          limitsOpen: 'حدود الاستخدام',
          invoice: 'الفاتورة القادمة: 499 ج.م في 15 سبتمبر 2026. طريقة الدفع: فيزا •••• 4242.',
          usage: '8 من 10 مستخدمين، 2 من 3 فروع.',
          or: 'أو',
          stats: {
            Positive: { label: 'إجمالي العيادات', value: '148', note: '+6 هذا الشهر' },
            Negative: { label: 'التذاكر النشطة', value: '12', note: '-3 هذا الشهر' },
            Neutral: { label: 'الإيراد الشهري', value: '73,900 ج.م', note: 'بدون تغيير' },
          } as Record<string, { label: string; value: string; note: string }>,
        }
      : {
          clinic: 'Al Noor Clinic',
          city: 'Nasr City',
          plan: 'Professional plan — 8 users, 2 branches',
          tags: { Default: 'Dental', Success: 'Active', Warning: 'VIP', Neutral: 'General' } as Record<string, string>,
          remove: 'Remove',
          billing: 'Billing Details (expanded)',
          limits: 'Usage Limits (collapsed)',
          billingOpen: 'Billing Details',
          limitsOpen: 'Usage Limits',
          invoice: 'Next invoice: EGP 499 on Sep 15, 2026. Payment method: Visa •••• 4242.',
          usage: '8 of 10 users, 2 of 3 branches.',
          or: 'or',
          stats: {
            Positive: { label: 'Total Clinics', value: '148', note: '+6 this month' },
            Negative: { label: 'Active Tickets', value: '12', note: '-3 this month' },
            Neutral: { label: 'MRR', value: 'EGP 73,900', note: 'No change' },
          } as Record<string, { label: string; value: string; note: string }>,
        },
  );

  protected readonly chosen = signal(0);
  protected readonly kept = signal(['Default', 'Success', 'Warning', 'Neutral']);

  protected intentOf(intent: string): ScsTagIntent {
    return intent.toLowerCase() as ScsTagIntent;
  }

  protected trendOf(trend: string): 'positive' | 'negative' | 'neutral' {
    return trend.toLowerCase() as 'positive' | 'negative' | 'neutral';
  }

  protected stat(trend: string): { label: string; value: string; note: string } {
    return this.words().stats[trend];
  }

  protected drop(tag: string): void {
    this.kept.update((tags) => tags.filter((kept) => kept !== tag));
  }
}
