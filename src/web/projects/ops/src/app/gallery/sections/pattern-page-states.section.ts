import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  ScsBannerComponent,
  ScsButtonDirective,
  ScsEmptyContext,
  ScsEmptyStateComponent,
  ScsIconName,
  ScsSegment,
  ScsSegmentedComponent,
  ScsTextFieldComponent,
} from 'ui';
import { GalleryState } from '../gallery-state';

type Drawn = 'empty' | 'error' | 'loading' | 'success';
type Undrawn = 'offline' | 'read-only' | 'not-in-plan';
type State = Drawn | Undrawn;

interface StateWords {
  readonly caption: string;
  readonly title: string;
  readonly text: string;
  readonly action: string;
}

const CONTEXTS: Record<State, ScsEmptyContext> = {
  empty: 'no-data',
  error: 'error',
  loading: 'loading',
  success: 'success',
  offline: 'error',
  'read-only': 'no-data',
  'not-in-plan': 'not-in-plan',
};

/** The marks the page draws (`117:2`) where they differ from the empty state's own. */
const MARKS: Partial<Record<State, ScsIconName>> = { empty: 'card', offline: 'wifi-off' };

/**
 * Pattern — Page States (`117:2`; SH-097), composed from `scs-empty-state` in a card: the four states
 * the page draws — empty, error, loading, success — and, marked as not drawn, the three the platform
 * needs that it does not: offline, read only (the page as it is, its fields locked, with the reason
 * above them) and not in the plan. The working example walks a card through them.
 */
@Component({
  selector: 'app-gallery-pattern-page-states-section',
  imports: [NgTemplateOutlet, ScsEmptyStateComponent, ScsButtonDirective, ScsBannerComponent, ScsTextFieldComponent, ScsSegmentedComponent],
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
      grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
      gap: var(--scs-spacing-md);
    }
    /* The caption keeps its height; the card takes the rest, so the cards of a row line up. */
    .state {
      display: grid;
      grid-template-rows: auto 1fr;
      gap: var(--scs-spacing-xs);
      min-inline-size: 0;
    }
    .caption {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .undrawn {
      color: var(--scs-color-feedback-warning-text);
    }
    .card {
      display: grid;
      place-items: center;
      gap: var(--scs-spacing-md);
      min-block-size: 280px;
      padding: var(--scs-spacing-lg);
      box-sizing: border-box;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    .card.form {
      place-items: stretch;
      align-content: start;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-sm);
      justify-items: start;
    }
    .live .card {
      inline-size: min(420px, 100%);
    }
  `,
  template: `
    <div class="grid">
      @for (state of drawn; track state) {
        <div class="state" [attr.data-testid]="'specimen-pattern-page-states/pattern/' + state">
          <p class="caption">{{ words()[state].caption }}</p>
          <div class="card">
            <ng-container *ngTemplateOutlet="body; context: { $implicit: state }" />
          </div>
        </div>
      }
      @for (state of undrawn; track state) {
        <div class="state" [attr.data-testid]="'specimen-pattern-page-states/pattern/' + state">
          <p class="caption">{{ words()[state].caption }} · <span class="undrawn">{{ words().undrawn }}</span></p>
          <div class="card" [class.form]="state === 'read-only'">
            <ng-container *ngTemplateOutlet="body; context: { $implicit: state }" />
          </div>
        </div>
      }
    </div>

    <div class="live" data-testid="live-pattern-page-states">
      <p class="caption">{{ words().live }}</p>
      <scs-segmented [ariaLabel]="words().pick" [segments]="segments()" [(value)]="shown" testId="live-page-state" />
      <div class="card" [class.form]="shown() === 'read-only'" aria-live="polite">
        <ng-container *ngTemplateOutlet="body; context: { $implicit: shown() }" />
      </div>
    </div>

    <ng-template #body let-state>
      @if (state === 'read-only') {
        <scs-banner tone="info">{{ words()['read-only'].text }}</scs-banner>
        <scs-text-field [label]="words().field" [value]="words().fieldValue" disabled />
      } @else {
        <scs-empty-state [context]="contextOf(state)" [mark]="markOf(state)" [title]="wordsOf(state).title" [text]="wordsOf(state).text">
          @if (wordsOf(state).action) {
            <button type="button" scsButton size="md">{{ wordsOf(state).action }}</button>
          }
        </scs-empty-state>
      }
    </ng-template>
  `,
})
export class PatternPageStatesSection {
  private readonly state = inject(GalleryState);

  protected readonly drawn: readonly Drawn[] = ['empty', 'error', 'loading', 'success'];
  protected readonly undrawn: readonly Undrawn[] = ['offline', 'read-only', 'not-in-plan'];
  protected readonly shown = signal<State>('loading');

  protected readonly words = computed(() => {
    const ar = this.state.language() === 'ar';
    const all: Record<State, StateWords> = ar
      ? {
          empty: { caption: 'لا توجد بيانات', title: 'لا توجد عيادات بعد', text: 'ابدأ بإضافة أول عيادة على المنصة لإدارتها من هنا.', action: 'إضافة عيادة جديدة' },
          error: { caption: 'خطأ', title: 'حدث خطأ ما', text: 'تعذر تحميل البيانات. تحقق من اتصالك وحاول مرة أخرى.', action: 'إعادة المحاولة' },
          loading: { caption: 'تحميل', title: 'جاري تحميل البيانات...', text: '', action: '' },
          success: { caption: 'نجاح', title: 'تم بنجاح', text: 'تم حفظ التغييرات بنجاح.', action: 'متابعة' },
          offline: { caption: 'دون اتصال', title: 'لا يوجد اتصال بالإنترنت', text: 'ما يظهر هو آخر ما تم تحميله. أعد المحاولة عند عودة الاتصال.', action: 'إعادة المحاولة' },
          'read-only': { caption: 'للقراءة فقط', title: '', text: 'تعرض هذه الصفحة دون تعديل: دورك يسمح بالاطلاع فقط.', action: '' },
          'not-in-plan': { caption: 'خارج الباقة', title: 'غير متاح في باقتك', text: 'هذه الميزة متاحة في الباقة الاحترافية.', action: 'ترقية الباقة' },
        }
      : {
          empty: { caption: 'Empty', title: 'No clinics yet', text: 'Start by adding your first clinic to manage it here.', action: 'Add New Clinic' },
          error: { caption: 'Error', title: 'Something went wrong', text: "We couldn't load the data. Check your connection and try again.", action: 'Try Again' },
          loading: { caption: 'Loading', title: 'Loading data...', text: '', action: '' },
          success: { caption: 'Success', title: 'Success', text: 'Your changes have been saved.', action: 'Continue' },
          offline: { caption: 'Offline', title: "You're offline", text: 'What is shown is the last copy loaded. Try again when you are back online.', action: 'Try Again' },
          'read-only': { caption: 'Read only', title: '', text: 'This page is shown without editing: your role can only view it.', action: '' },
          'not-in-plan': { caption: 'Not in the plan', title: 'Not in your plan', text: 'This feature comes with the Professional plan.', action: 'Upgrade Plan' },
        };
    return {
      ...all,
      undrawn: ar ? 'غير مرسومة في الصفحة' : 'not drawn on the page',
      live: ar ? 'مثال يعمل' : 'Working example',
      pick: ar ? 'حالة الصفحة' : 'Page state',
      field: ar ? 'اسم العيادة' : 'Clinic name',
      fieldValue: ar ? 'عيادة النور' : 'Al Noor Clinic',
    };
  });

  protected contextOf(state: State): ScsEmptyContext {
    return CONTEXTS[state];
  }

  protected markOf(state: State): ScsIconName | null {
    return MARKS[state] ?? null;
  }

  protected wordsOf(state: State): StateWords {
    return this.words()[state];
  }

  protected readonly segments = computed<ScsSegment[]>(() =>
    ([...this.drawn, ...this.undrawn] as State[]).map((state) => ({ value: state, label: this.words()[state].caption })),
  );
}
