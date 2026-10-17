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
import { ScsBannerComponent, ScsButtonDirective, ScsDialogComponent, ToastService } from 'ui';
import { featureName } from './feature-words';
import { FeatureRow, FeaturesService } from './features.service';

/**
 * «إعادة بيع الميزة» (spec 012, User Story 4 scenario 7; FR-021a). Not drawn: built in the manner
 * of «إيقاف الميزة…» and listed for the designer. The feature is offered to every plan again.
 */
@Component({
  selector: 'app-resume-feature',
  imports: [TranslocoPipe, ScsDialogComponent, ScsButtonDirective, ScsBannerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
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
      <div data-testid="resume-dialog">
        <p data-testid="resume-text">{{ 'app.plans.resume.text' | transloco }}</p>
        @if (failed()) {
          <scs-banner tone="danger" data-testid="resume-failed">{{
            'app.plans.resume.failed' | transloco
          }}</scs-banner>
        }
        <div class="buttons">
          <button
            type="button"
            scsButton="secondary"
            data-testid="resume-cancel"
            (click)="closed.emit()"
          >
            {{ 'app.plans.cancel' | transloco }}
          </button>
          <button
            type="button"
            scsButton
            data-testid="resume-confirm"
            [busy]="working()"
            [disabled]="working()"
            (click)="resume()"
          >
            {{ 'app.plans.resume.action' | transloco }}
          </button>
        </div>
      </div>
    </scs-dialog>
  `,
})
export class ResumeFeatureDialog {
  private readonly features = inject(FeaturesService);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /** The feature being sold again; null while the dialog is closed. */
  readonly feature = input<FeatureRow | null>(null);
  readonly closed = output<void>();
  readonly done = output<void>();

  protected readonly working = signal(false);
  protected readonly failed = signal(false);

  private readonly name = computed(() => {
    const feature = this.feature();
    return feature === null ? '' : featureName(feature, this.language.language());
  });

  protected readonly title = computed(() =>
    this.words.translate('app.plans.resume.title', { name: this.name() }),
  );

  constructor() {
    effect(() => {
      this.feature();
      this.failed.set(false);
    });
  }

  protected async resume(): Promise<void> {
    const feature = this.feature();
    if (feature === null) {
      return;
    }
    this.working.set(true);
    this.failed.set(false);
    try {
      await this.features.resume(feature.key);
      this.toasts.show(this.words.translate('app.plans.resume.done', { name: this.name() }));
      this.done.emit();
    } catch {
      this.failed.set(true);
      this.toasts.show(this.words.translate('app.plans.resume.failed'), '', 'danger');
    } finally {
      this.working.set(false);
    }
  }
}
