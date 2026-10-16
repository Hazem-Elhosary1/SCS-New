import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import { ScsBannerComponent, ScsButtonDirective, ScsDialogComponent, ScsTextareaComponent, ToastService } from 'ui';
import { OpsAccess } from '../access/ops-access.service';
import { countWords } from './count-words';
import { OperatorsService, RoleRow, TeamCodes, codeOf } from './operators.service';

/**
 * «حذف الدور» (spec 011, FR-019 to FR-021; frames `2788:15904`, `2788:16350` and their English
 * twins). A role nobody holds is deleted, with an optional note; a held one is not — the
 * dialog says how many hold it and its button stays disabled. If a holder appeared between the
 * list and the press, the platform refuses and the dialog turns to that state.
 */
@Component({
  selector: 'app-delete-role',
  imports: [TranslocoPipe, ScsDialogComponent, ScsButtonDirective, ScsTextareaComponent, ScsBannerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      line-height: 1.7;
    }
    p.strong {
      color: var(--scs-color-text-primary);
      font-weight: 500;
    }
    p.note {
      padding: var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    scs-banner {
      margin-block-end: var(--scs-spacing-md);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
    }
    .danger {
      border-color: var(--scs-color-feedback-danger-icon);
      background: var(--scs-color-feedback-danger-icon);
    }
  `,
  template: `
    <scs-dialog [heading]="'app.operators.deleteRole.title' | transloco" [width]="440" [open]="role() !== null" (openChange)="$event || closed.emit()">
      @if (role(); as target) {
        @if (held()) {
          <p class="strong" data-testid="delete-role-held">
            {{ 'app.operators.deleteRole.held' | transloco: { name: name() } }}
          </p>
          <p class="note" data-testid="delete-role-count">
            {{ 'app.operators.deleteRole.heldCount' | transloco: { count: holdersWords() } }}
          </p>
        } @else {
          <p data-testid="delete-role-text">{{ 'app.operators.deleteRole.free' | transloco: { name: name() } }}</p>
          <scs-textarea
            testId="delete-role-note"
            [label]="'app.operators.deleteRole.note' | transloco"
            [placeholder]="'app.operators.deleteRole.notePlaceholder' | transloco"
            [max]="500"
            [rows]="2"
            [(value)]="note"
          />
        }

        @if (failed()) {
          <scs-banner tone="danger" data-testid="delete-role-failed">{{ 'app.operators.deleteRole.failed' | transloco }}</scs-banner>
        }

        <div class="buttons">
          <button type="button" scsButton="secondary" data-testid="delete-role-cancel" (click)="closed.emit()">
            {{ 'app.operators.cancel' | transloco }}
          </button>
          <button
            type="button"
            scsButton
            class="danger"
            data-testid="delete-role-confirm"
            [busy]="working()"
            [disabled]="blocked()"
            (click)="remove(target)"
          >
            {{ (working() ? 'app.operators.deleteRole.working' : 'app.operators.deleteRole.action') | transloco }}
          </button>
        </div>
      }
    </scs-dialog>
  `,
})
export class DeleteRoleDialog {
  private readonly team = inject(OperatorsService);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly access = inject(OpsAccess);

  /** The role being deleted; null while the dialog is closed. */
  readonly role = input<RoleRow | null>(null);
  readonly closed = output<void>();
  readonly deleted = output<void>();

  protected readonly note = signal('');
  protected readonly working = signal(false);
  protected readonly failed = signal(false);

  /** How many hold it: what the list said, or what the platform said when it refused. */
  private readonly found = signal<number | null>(null);
  protected readonly holders = computed(() => this.found() ?? this.role()?.operators ?? 0);
  protected readonly held = computed(() => this.holders() !== 0);
  protected readonly blocked = computed(() => this.held() || this.working());
  protected readonly name = computed(() => {
    const role = this.role();
    return role === null ? '' : this.access.nameOfRole(role);
  });
  protected readonly holdersWords = computed(() =>
    countWords(this.words, 'app.operators.roles.operators', this.holders(), this.language.language()),
  );

  constructor() {
    // Each opening starts clean.
    effect(() => {
      this.role();
      this.note.set('');
      this.failed.set(false);
      this.found.set(null);
    });
  }

  protected async remove(role: RoleRow): Promise<void> {
    this.working.set(true);
    this.failed.set(false);
    try {
      await this.team.deleteRole(role.id, this.note());
      this.toasts.show(this.words.translate('app.operators.deleteRole.done'));
      this.deleted.emit();
    } catch (error) {
      if (codeOf(error) === TeamCodes.roleInUse) {
        const holders = (error as { problem?: { operators?: number } }).problem?.operators;
        this.found.set(typeof holders === 'number' ? holders : 1);
      } else {
        this.failed.set(true);
      }
    } finally {
      this.working.set(false);
    }
  }
}
