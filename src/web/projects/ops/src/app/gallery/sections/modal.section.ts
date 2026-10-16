import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsDialogComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/** The Modal page (62:57 / 309:116): its four sizes, still; working: a real window over the page. */
@Component({
  selector: 'app-gallery-modal-section',
  imports: [ScsDialogComponent, ScsButtonDirective, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .cells {
      --gallery-cell: min(660px, 100%);
    }
    p {
      margin: 0;
      color: var(--scs-color-text-secondary);
      font-size: 13px;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-lg);
    }
  `,
  template: `
    <app-gallery-component class="cells" page="Modal" set="Modal">
      <ng-template appGallerySpecimen let-values>
        <scs-dialog still [size]="sizeOf(values['Size'])" [heading]="words().title">
          <p>{{ words().body }}</p>
          <div class="actions">
            <button type="button" scsButton="secondary">{{ words().cancel }}</button>
            <button type="button" scsButton="primary">{{ words().save }}</button>
          </div>
        </scs-dialog>
      </ng-template>
      <ng-template appGalleryLive>
        <button type="button" scsButton="secondary" data-testid="live-open-modal" (click)="open.set(true)">{{ words().open }}</button>
        <scs-dialog size="md" [heading]="words().title" [(open)]="open">
          <p>{{ words().body }}</p>
          <div class="actions">
            <button type="button" scsButton="secondary" (click)="open.set(false)">{{ words().cancel }}</button>
            <button type="button" scsButton="primary" (click)="open.set(false)">{{ words().save }}</button>
          </div>
        </scs-dialog>
      </ng-template>
    </app-gallery-component>
  `,
})
export class ModalSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Modal 62:57 and its Arabic twin 309:116). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          title: 'عنوان النافذة',
          body: 'محتوى النافذة يظهر هنا. تتحرك هذه المساحة إذا زاد المحتوى عن الارتفاع المتاح.',
          cancel: 'إلغاء',
          save: 'حفظ',
          open: 'افتح النافذة',
        }
      : {
          title: 'Modal Title',
          body: 'Modal body content goes here. This area scrolls if content exceeds the available height.',
          cancel: 'Cancel',
          save: 'Save',
          open: 'Open the modal',
        },
  );
  protected readonly open = signal(false);

  protected sizeOf(size: string): 'sm' | 'md' | 'lg' | 'full' {
    return ({ Small: 'sm', Medium: 'md', Large: 'lg', 'Full-screen': 'full' } as const)[size as 'Small'] ?? 'md';
  }
}
