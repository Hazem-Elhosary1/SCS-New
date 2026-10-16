import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsButtonDirective, ScsEmptyContext, ScsEmptyStateComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

type Words = Record<string, { title: string; text: string; action: string }>;

/** The Empty State page (374:35 / 374:68): empty, error, loading, success; each in its frame. */
@Component({
  selector: 'app-gallery-empty-state-section',
  imports: [ScsEmptyStateComponent, ScsButtonDirective, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .cells {
      --gallery-cell: min(340px, 100%);
    }
    .frame {
      display: grid;
      place-items: center;
      box-sizing: border-box;
      inline-size: 100%;
      max-inline-size: 320px;
      min-block-size: 320px;
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: 12px;
      background: var(--scs-color-bg-surface);
    }
  `,
  template: `
    <app-gallery-component class="cells" page="Empty State" set="Empty State">
      <ng-template appGallerySpecimen let-values>
        <div class="frame">
          <scs-empty-state [context]="contextOf(values['Type'])" [title]="words()[values['Type']].title" [text]="words()[values['Type']].text">
            @if (words()[values['Type']].action) {
              <button type="button" scsButton="primary">{{ words()[values['Type']].action }}</button>
            }
          </scs-empty-state>
        </div>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="frame">
          <scs-empty-state context="no-results" [title]="extra().title" [text]="extra().text">
            <button type="button" scsButton="secondary">{{ extra().action }}</button>
          </scs-empty-state>
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class EmptyStateSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Empty State 374:35 and its Arabic twin 374:68). */
  protected readonly words = computed<Words>(() =>
    this.state.language() === 'ar'
      ? {
          Empty: { title: 'لا توجد عيادات بعد', text: 'ابدأ بإضافة أول عيادة على المنصة لإدارتها من هنا.', action: 'إضافة عيادة جديدة' },
          Error: { title: 'حدث خطأ ما', text: 'تعذر تحميل البيانات، تحقق من اتصالك وحاول مرة أخرى.', action: 'إعادة المحاولة' },
          Loading: { title: 'جاري تحميل البيانات...', text: '', action: '' },
          Success: { title: 'تم بنجاح', text: 'تم حفظ التغييرات بنجاح.', action: 'متابعة' },
        }
      : {
          Empty: { title: 'No clinics yet', text: 'Start by adding your first clinic to manage it here.', action: 'Add New Clinic' },
          Error: { title: 'Something went wrong', text: "We couldn't load the data. Check your connection and try again.", action: 'Try Again' },
          Loading: { title: 'Loading data...', text: '', action: '' },
          Success: { title: 'Success', text: 'Your changes have been saved.', action: 'Continue' },
        },
  );
  protected readonly extra = computed(() =>
    this.state.language() === 'ar'
      ? { title: 'لا توجد نتائج مطابقة', text: 'جرّب كلمات أقل أو امسح عوامل التصفية.', action: 'مسح التصفية' }
      : { title: 'No matching results', text: 'Try fewer words or clear the filters.', action: 'Clear filters' },
  );

  protected contextOf(type: string): ScsEmptyContext {
    return ({ Empty: 'no-data', Error: 'error', Loading: 'loading', Success: 'success' } as const)[type as 'Empty'] ?? 'no-data';
  }
}
