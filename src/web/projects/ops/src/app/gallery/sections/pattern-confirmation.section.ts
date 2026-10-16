import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsConfirmComponent, ScsDialogComponent } from 'ui';
import { GalleryState } from '../gallery-state';

/**
 * Pattern — Confirmation (`120:2`; CL-SH-060): a confirmation over the dimmed page, its button naming
 * the action — never «نعم» or «موافق» — and the destructive one in red. The specimens are the still
 * dialog as drawn; the working examples are `scs-confirm`, which traps the focus and gives it back.
 */
@Component({
  selector: 'app-gallery-pattern-confirmation-section',
  imports: [ScsDialogComponent, ScsButtonDirective, ScsConfirmComponent],
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
      grid-template-columns: repeat(auto-fill, minmax(min(460px, 100%), 1fr));
      gap: var(--scs-spacing-md);
    }
    .block {
      display: grid;
      gap: var(--scs-spacing-xs);
      min-inline-size: 0;
    }
    .caption {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    /* The page dimmed behind the dialog, as the pattern draws it. */
    .stage {
      display: grid;
      place-items: center;
      min-block-size: 300px;
      padding: var(--scs-spacing-md);
      box-sizing: border-box;
      border-radius: var(--scs-radius-md);
      background: rgb(15 23 42 / 45%);
    }
    .stage scs-dialog {
      inline-size: min(400px, 100%);
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
    }
    p {
      margin: 0;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-sm);
    }
    .said {
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
  `,
  template: `
    <div class="grid">
      <div class="block" data-testid="specimen-pattern-confirmation/pattern/confirm">
        <p class="caption">{{ words().confirmCaption }}</p>
        <div class="stage">
          <scs-dialog still size="sm" [heading]="words().confirmTitle">
            <p>{{ words().confirmText }}</p>
            <div class="actions">
              <button type="button" scsButton="secondary" size="md">{{ words().cancel }}</button>
              <button type="button" scsButton="primary" size="md">{{ words().confirmAction }}</button>
            </div>
          </scs-dialog>
        </div>
      </div>
      <div class="block" data-testid="specimen-pattern-confirmation/pattern/destructive">
        <p class="caption">{{ words().deleteCaption }}</p>
        <div class="stage">
          <scs-dialog still size="sm" [heading]="words().deleteTitle">
            <p>{{ words().deleteText }}</p>
            <div class="actions">
              <button type="button" scsButton="secondary" size="md">{{ words().cancel }}</button>
              <button type="button" scsButton="destructive" size="md">{{ words().deleteAction }}</button>
            </div>
          </scs-dialog>
        </div>
      </div>
    </div>

    <div class="block" data-testid="live-pattern-confirmation">
      <p class="caption">{{ words().live }}</p>
      <div class="row">
        <button type="button" scsButton="secondary" size="md" data-testid="live-open-confirm" (click)="confirmOpen.set(true)">{{ words().confirmAction }}</button>
        <button type="button" scsButton="destructive" size="md" data-testid="live-open-delete" (click)="deleteOpen.set(true)">{{ words().deleteAction }}</button>
      </div>
      <p class="said" role="status" data-testid="live-confirm-said">{{ said() }}</p>
      <scs-confirm
        [heading]="words().confirmTitle"
        [actionLabel]="words().confirmAction"
        [cancelLabel]="words().cancel"
        [(open)]="confirmOpen"
        (confirmed)="said.set(words().confirmed)"
      >
        <p>{{ words().confirmText }}</p>
      </scs-confirm>
      <scs-confirm
        danger
        [heading]="words().deleteTitle"
        [actionLabel]="words().deleteAction"
        [cancelLabel]="words().cancel"
        [(open)]="deleteOpen"
        (confirmed)="said.set(words().deleted)"
      >
        <p>{{ words().deleteText }}</p>
      </scs-confirm>
    </div>
  `,
})
export class PatternConfirmationSection {
  private readonly state = inject(GalleryState);

  protected readonly confirmOpen = signal(false);
  protected readonly deleteOpen = signal(false);
  protected readonly said = signal('');

  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          confirmCaption: 'نافذة تأكيد',
          deleteCaption: 'تأكيد إجراء لا رجعة فيه (حذف)',
          live: 'مثال يعمل',
          confirmTitle: 'تأكيد الإجراء',
          confirmText: 'هل أنت متأكد أنك تريد تعليق حساب هذه العيادة؟ لن تتمكن من الوصول إلى النظام حتى يتم إعادة التفعيل.',
          confirmAction: 'تعليق العيادة',
          deleteTitle: 'حذف العيادة نهائياً',
          deleteText: 'سيتم حذف «عيادة النور» وكل بياناتها نهائياً. هذا الإجراء لا يمكن التراجع عنه.',
          deleteAction: 'حذف نهائياً',
          cancel: 'إلغاء',
          confirmed: 'عُلِّقت العيادة (مثال — لم يُرسل شيء).',
          deleted: 'حُذفت العيادة (مثال — لم يُرسل شيء).',
        }
      : {
          confirmCaption: 'Confirmation dialog',
          deleteCaption: 'Destructive (delete) confirmation',
          live: 'Working example',
          confirmTitle: 'Confirm Action',
          confirmText: "Are you sure you want to suspend this clinic's account? It won't be able to access the system until reactivated.",
          confirmAction: 'Suspend Clinic',
          deleteTitle: 'Permanently Delete Clinic',
          deleteText: 'This will permanently delete "Al-Noor Clinic" and all of its data. This action cannot be undone.',
          deleteAction: 'Delete Permanently',
          cancel: 'Cancel',
          confirmed: 'The clinic was suspended (an example — nothing was sent).',
          deleted: 'The clinic was deleted (an example — nothing was sent).',
        },
  );
}
