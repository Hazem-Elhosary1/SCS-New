import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsBannerComponent,
  ScsButtonDirective,
  ScsDialogComponent,
  ScsIconComponent,
  ScsTextareaComponent,
  ToastService,
} from 'ui';
import { opsNumber } from '../i18n/ops-format';
import { planName } from './plan-words';
import { PlansService } from './plans.service';

/** What the dialog needs of the plan it disables. */
export interface DisablingPlan {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  /** Its active clinics: the warning names them when there are any. */
  readonly clinics: number;
}

/**
 * «تعطيل الباقة» (spec 013, User Story 5 scenarios 1, 2 and 5; FR-021; frames
 * `1214:601`/`1214:761`). The plan's clinics keep it and renew on it; no new clinic can be given
 * it; its badge goes. When it has active clinics, the dialog says how many first. The confirm
 * button is red, as 012's retire (owner, 16 Oct 2026).
 */
@Component({
  selector: 'app-disable-plan',
  imports: [
    TranslocoPipe,
    ScsDialogComponent,
    ScsButtonDirective,
    ScsBannerComponent,
    ScsIconComponent,
    ScsTextareaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The frames of the operators' template draw a form's labels small, grey and regular (spec 011).
  host: { class: 'scs-form--template' },
  styles: `
    p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      line-height: 1.7;
    }
    .warning {
      display: flex;
      gap: var(--scs-spacing-sm);
      align-items: flex-start;
      margin-block-end: var(--scs-spacing-md);
      padding: var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
      line-height: 1.7;
    }
    scs-banner {
      margin-block-end: var(--scs-spacing-md);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
  `,
  template: `
    <scs-dialog
      [heading]="'app.plans.plan.disable.title' | transloco"
      [width]="440"
      [open]="plan() !== null"
      (openChange)="$event || closed.emit()"
    >
      <div data-testid="disable-dialog">
        <p data-testid="disable-text">{{ 'app.plans.plan.disable.text' | transloco }}</p>
        @if ((plan()?.clinics ?? 0) > 0) {
          <div class="warning" data-testid="disable-warning">
            <scs-icon name="alert-circle" [size]="18" />
            <span>{{ warning() }}</span>
          </div>
        }
        <scs-textarea
          testId="disable-reason"
          [label]="'app.plans.plan.disable.reason' | transloco"
          [placeholder]="'app.plans.plan.disable.reasonHint' | transloco"
          [max]="500"
          [rows]="2"
          [(value)]="reason"
        />
        @if (failed()) {
          <scs-banner tone="danger" data-testid="disable-failed">{{
            'app.plans.plan.disable.failed' | transloco
          }}</scs-banner>
        }
        <div class="buttons">
          <button
            type="button"
            scsButton="secondary"
            data-testid="disable-cancel"
            (click)="closed.emit()"
          >
            {{ 'app.plans.plan.disable.cancel' | transloco }}
          </button>
          <button
            type="button"
            scsButton="destructive"
            data-testid="disable-confirm"
            [busy]="working()"
            [disabled]="working()"
            (click)="disable()"
          >
            {{
              (working() ? 'app.plans.plan.disable.disabling' : 'app.plans.plan.disable.confirm')
                | transloco
            }}
          </button>
        </div>
      </div>
    </scs-dialog>
  `,
})
export class DisablePlanDialog {
  private readonly plans = inject(PlansService);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /** The plan being disabled; null while the dialog is closed. */
  readonly plan = input<DisablingPlan | null>(null);
  readonly closed = output<void>();
  readonly done = output<void>();

  protected readonly working = signal(false);
  protected readonly failed = signal(false);
  protected readonly reason = signal('');

  protected readonly warning = computed(() => {
    const plan = this.plan();
    return plan === null
      ? ''
      : this.words.translate('app.plans.plan.disable.warning', {
          name: planName(plan, this.language.language()),
          count: opsNumber(plan.clinics, this.language.language()),
        });
  });

  constructor() {
    // Each opening starts afresh: no reason, no failure from the last time.
    effect(() => {
      if (this.plan() !== null) {
        this.reason.set('');
        this.failed.set(false);
      }
    });
  }

  protected async disable(): Promise<void> {
    const plan = this.plan();
    if (plan === null || this.working()) {
      return;
    }
    this.working.set(true);
    this.failed.set(false);
    try {
      await this.plans.disable(plan.id, this.reason().trim() || null);
      this.toasts.show(this.words.translate('app.plans.plan.disable.done'));
      this.done.emit();
    } catch {
      // Nothing changed (FR-009a): the dialog stays, saying so.
      this.failed.set(true);
    } finally {
      this.working.set(false);
    }
  }
}
