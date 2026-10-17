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
  ToastService,
} from 'ui';
import { clinicsWords, featureName } from './feature-words';
import { FeatureRow, FeaturesService, PlanUse } from './features.service';

/**
 * «إيقاف الميزة…» (spec 012, User Story 4 scenarios 3 to 6; FR-019 to FR-021; frames
 * `2783:31078`/`2783:30770`). New plans cannot take the feature; plans that include it keep it,
 * and when there are any the dialog names them and their clinics first. Nothing of a plan or a
 * clinic changes.
 */
@Component({
  selector: 'app-retire-feature',
  imports: [
    TranslocoPipe,
    ScsDialogComponent,
    ScsButtonDirective,
    ScsBannerComponent,
    ScsIconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      [heading]="title()"
      [width]="440"
      [open]="feature() !== null"
      (openChange)="$event || closed.emit()"
    >
      <div data-testid="retire-dialog">
        <p data-testid="retire-text">{{ 'app.plans.retire.text' | transloco }}</p>
        @if (keepers().length > 0) {
          <div class="warning" data-testid="retire-warning">
            <scs-icon name="alert-circle" [size]="18" />
            <span>{{ warning() }}</span>
          </div>
        }
        @if (plansUnknown()) {
          <scs-banner tone="warning" data-testid="retire-plans-unknown">{{
            'app.plans.retire.plansUnknown' | transloco
          }}</scs-banner>
        }
        @if (failed()) {
          <scs-banner tone="danger" data-testid="retire-failed">{{
            'app.plans.retire.failed' | transloco
          }}</scs-banner>
        }
        <div class="buttons">
          <button
            type="button"
            scsButton="secondary"
            data-testid="retire-cancel"
            (click)="closed.emit()"
          >
            {{ 'app.plans.cancel' | transloco }}
          </button>
          <button
            type="button"
            scsButton="destructive"
            data-testid="retire-confirm"
            [busy]="working()"
            [disabled]="working() || checking()"
            (click)="retire()"
          >
            {{ 'app.plans.retire.action' | transloco }}
          </button>
        </div>
      </div>
    </scs-dialog>
  `,
})
export class RetireFeatureDialog {
  private readonly features = inject(FeaturesService);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /** The feature being retired; null while the dialog is closed. */
  readonly feature = input<FeatureRow | null>(null);
  readonly closed = output<void>();
  readonly done = output<void>();

  protected readonly working = signal(false);
  protected readonly failed = signal(false);
  /** The plans are being read: the button waits for them (the warning names them). */
  protected readonly checking = signal(false);
  protected readonly plansUnknown = signal(false);
  /** The plans that keep it; asked when the dialog opens. */
  protected readonly keepers = signal<readonly PlanUse[]>([]);
  private readonly clinics = signal(0);

  private readonly name = computed(() => {
    const feature = this.feature();
    return feature === null ? '' : featureName(feature, this.language.language());
  });

  protected readonly title = computed(() =>
    this.words.translate('app.plans.retire.title', { name: this.name() }),
  );

  protected readonly warning = computed(() =>
    this.words.translate('app.plans.retire.keep', {
      plans: this.keepers()
        .map((plan) => (this.language.language() === 'ar' ? plan.nameAr : plan.nameEn))
        .join(this.words.translate('app.plans.retire.and')),
      clinics: clinicsWords(this.words, this.clinics(), this.language.language()),
    }),
  );

  constructor() {
    // Each opening starts clean, and asks which plans keep the feature.
    effect(() => {
      const feature = this.feature();
      this.failed.set(false);
      this.keepers.set([]);
      this.plansUnknown.set(false);
      this.checking.set(false);
      if (feature !== null && feature.plans > 0) {
        this.checking.set(true);
        void this.features.whereUsed(feature.key).then(
          (used) => {
            // A late answer for a feature no longer open is dropped.
            if (this.feature()?.key !== feature.key) {
              return;
            }
            this.keepers.set(used.items);
            this.clinics.set(used.total);
            this.checking.set(false);
          },
          () => {
            if (this.feature()?.key === feature.key) {
              this.plansUnknown.set(true);
              this.checking.set(false);
            }
          },
        );
      }
    });
  }

  protected async retire(): Promise<void> {
    const feature = this.feature();
    if (feature === null) {
      return;
    }
    this.working.set(true);
    this.failed.set(false);
    try {
      await this.features.retire(feature.key);
      this.toasts.show(this.words.translate('app.plans.retire.done', { name: this.name() }));
      this.done.emit();
    } catch {
      this.failed.set(true);
      this.toasts.show(this.words.translate('app.plans.retire.failed'), '', 'danger');
    } finally {
      this.working.set(false);
    }
  }
}
