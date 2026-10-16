import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ScsProgressComponent, ScsSkeletonBlockComponent, ScsSkeletonComponent, ScsSpinnerComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Loading page (59:2): the spinner in four sizes, the progress bar known and not, the skeleton
 * line in three widths; and the page's composed examples — a card, table rows, a page loading and a
 * loader beside its words.
 */
@Component({
  selector: 'app-gallery-loading-section',
  imports: [
    ScsSpinnerComponent,
    ScsProgressComponent,
    ScsSkeletonComponent,
    ScsSkeletonBlockComponent,
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
    /* The longest line is 280: its cells are as wide. */
    .lines {
      --gallery-cell: 300px;
    }
    scs-progress {
      inline-size: 320px;
      max-inline-size: 100%;
    }
    .box {
      box-sizing: border-box;
      max-inline-size: 100%;
      padding: 16px;
      border: 1px solid var(--scs-color-border-default);
      border-radius: 12px;
      background: var(--scs-color-bg-surface);
    }
    .card {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 8px 14px;
      inline-size: 260px;
    }
    .card .wide {
      grid-column: 1 / -1;
      display: grid;
      gap: 8px;
      margin-block-start: 12px;
    }
    .stack {
      display: grid;
      gap: 8px;
      align-content: center;
    }
    .rows {
      display: grid;
      gap: 16px;
      inline-size: 100%;
      max-inline-size: 300px;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .row .end {
      margin-inline-start: auto;
    }
    .page {
      display: grid;
      place-items: center;
      align-content: center;
      gap: 16px;
      inline-size: 100%;
      max-inline-size: 400px;
      block-size: 240px;
      color: var(--scs-color-text-tertiary);
      font-size: 14px;
    }
    .inline {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 13px;
    }
  `,
  template: `
    <app-gallery-component page="Loading" set="Spinner">
      <ng-template appGallerySpecimen let-values>
        <scs-spinner [size]="sizeOf(values['Size'])" />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-spinner size="lg" [label]="words().loading" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component page="Loading" set="Progress Bar">
      <ng-template appGallerySpecimen let-values>
        @if (values['Type'] === 'Determinate') {
          <scs-progress [label]="words().syncing" [value]="62" />
        } @else {
          <scs-progress [label]="words().loading" />
        }
      </ng-template>
      <ng-template appGalleryLive>
        <scs-progress [label]="words().done" [value]="100" intent="success" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="lines" page="Loading" set="Skeleton Line">
      <ng-template appGallerySpecimen let-values>
        <scs-skeleton [widths]="[+values['Width']]" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Skeleton — Card" node="290:4" testId="specimen-loading/parts/card">
      <div class="box card" role="status" [attr.aria-label]="words().loading">
        <scs-skeleton-block round [width]="60" [height]="60" />
        <div class="stack"><scs-skeleton-block [width]="140" /><scs-skeleton-block [width]="100" /></div>
        <div class="wide"><scs-skeleton-block [height]="12" /><scs-skeleton-block [width]="160" [height]="12" /></div>
      </div>
    </app-gallery-part>
    <app-gallery-part name="Skeleton — Table Row" node="290:11" testId="specimen-loading/parts/table-row">
      <div class="box rows" role="status" [attr.aria-label]="words().loading">
        @for (row of [1, 2, 3]; track row) {
          <div class="row">
            <scs-skeleton-block round [width]="24" [height]="24" />
            <scs-skeleton-block [width]="140" />
            <scs-skeleton-block class="end" [width]="60" />
          </div>
        }
      </div>
    </app-gallery-part>
    <app-gallery-part name="Page / Full-screen Loader" node="290:22" testId="specimen-loading/parts/page-loader">
      <div class="box page">
        <scs-spinner size="lg" />
        <span role="status">{{ words().dashboard }}</span>
      </div>
    </app-gallery-part>
    <app-gallery-part name="Inline Loader" node="290:31" testId="specimen-loading/parts/inline-loader">
      <div class="inline">
        <scs-spinner size="sm" />
        <span role="status">{{ words().fetching }}</span>
      </div>
    </app-gallery-part>
  `,
})
export class LoadingSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Loading 59:2 and its Arabic twins 314:54 … 314:75). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          syncing: 'جارِ المزامنة — 62%',
          loading: 'جارِ التحميل…',
          done: 'اكتملت المزامنة',
          dashboard: 'جارِ تحميل لوحة التحكم...',
          fetching: 'جارِ جلب أحدث العيادات...',
        }
      : {
          syncing: 'Syncing — 62%',
          loading: 'Loading…',
          done: 'Sync complete',
          dashboard: 'Loading your dashboard...',
          fetching: 'Fetching latest clinics...',
        },
  );

  protected sizeOf(size: string): 'sm' | 'md' | 'lg' | 'xl' {
    return ({ Small: 'sm', Medium: 'md', Large: 'lg', XLarge: 'xl' } as const)[size as 'Small'] ?? 'md';
  }
}
