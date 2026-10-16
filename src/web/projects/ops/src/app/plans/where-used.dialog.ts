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
  ScsBadgeComponent,
  ScsButtonDirective,
  ScsDialogComponent,
  ScsEmptyStateComponent,
  ScsSkeletonComponent,
  ScsTableDirective,
} from 'ui';
import { countWords } from '../operators/count-words';
import { clinicsWords, featureName } from './feature-words';
import { FeatureRow, FeaturesService, WhereUsed } from './features.service';

/**
 * «أين تُستعمل» (spec 012, User Story 4 scenarios 1 and 2; FR-018; frames `2783:30449`/
 * `2783:30129`): the plans that include the feature, their state and their clinics, and a total.
 * Until plans exist (spec 013) the platform answers none, and the dialog says so plainly.
 */
@Component({
  selector: 'app-where-used',
  imports: [
    TranslocoPipe,
    ScsDialogComponent,
    ScsButtonDirective,
    ScsTableDirective,
    ScsBadgeComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      line-height: 1.7;
    }
    tr.total td {
      font-weight: 500;
    }
    td.clinics,
    th.clinics {
      text-align: end;
    }
    .buttons {
      display: flex;
      justify-content: flex-start;
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
  `,
  template: `
    <scs-dialog
      [heading]="title()"
      [width]="460"
      [open]="feature() !== null"
      (openChange)="$event || closed.emit()"
    >
      <div data-testid="where-used-dialog">
        @switch (state()) {
          @case ('loading') {
            <scs-skeleton
              data-testid="where-used-loading"
              [rows]="3"
              [label]="'app.plans.where.loading' | transloco"
            />
          }
          @case ('failed') {
            <scs-empty-state
              context="error"
              data-testid="where-used-failed"
              [title]="'app.plans.where.failed' | transloco"
            >
              <button
                type="button"
                scsButton="secondary"
                data-testid="where-used-retry"
                (click)="load()"
              >
                {{ 'app.plans.retry' | transloco }}
              </button>
            </scs-empty-state>
          }
          @default {
            @if (used(); as answer) {
              @if (answer.items.length === 0) {
                <p data-testid="where-used-none">{{ 'app.plans.where.none' | transloco }}</p>
              } @else {
                <p data-testid="where-used-text">{{ summary() }}</p>
                <table scsTable data-testid="where-used-table">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'app.plans.where.plan' | transloco }}</th>
                      <th scope="col">{{ 'app.plans.where.state' | transloco }}</th>
                      <th scope="col" class="clinics">
                        {{ 'app.plans.where.clinics' | transloco }}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (plan of answer.items; track plan.id) {
                      <tr data-testid="where-used-row">
                        <td>
                          <bdi>{{ plan.name }}</bdi>
                        </td>
                        <td>
                          <scs-badge [intent]="plan.state === 'active' ? 'success' : 'neutral'">{{
                            'app.plans.where.' + plan.state | transloco
                          }}</scs-badge>
                        </td>
                        <td class="clinics">{{ clinics(plan.clinics) }}</td>
                      </tr>
                    }
                    <tr class="total" data-testid="where-used-total">
                      <td>{{ 'app.plans.where.total' | transloco }}</td>
                      <td></td>
                      <td class="clinics">{{ clinics(answer.total) }}</td>
                    </tr>
                  </tbody>
                </table>
              }
            }
          }
        }
        <div class="buttons">
          <button
            type="button"
            scsButton="secondary"
            data-testid="where-used-close"
            (click)="closed.emit()"
          >
            {{ 'app.plans.close' | transloco }}
          </button>
        </div>
      </div>
    </scs-dialog>
  `,
})
export class WhereUsedDialog {
  private readonly features = inject(FeaturesService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /** The feature asked about; null while the dialog is closed. */
  readonly feature = input<FeatureRow | null>(null);
  readonly closed = output<void>();

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly used = signal<WhereUsed | null>(null);

  protected readonly title = computed(() => {
    const feature = this.feature();
    return feature === null
      ? ''
      : this.words.translate('app.plans.where.title', {
          name: featureName(feature, this.language.language()),
        });
  });

  protected readonly summary = computed(() => {
    const used = this.used();
    return used === null
      ? ''
      : this.words.translate('app.plans.where.text', {
          plans: countWords(
            this.words,
            'app.plans.inPlans',
            used.items.length,
            this.language.language(),
          ),
          clinics: this.clinics(used.total),
        });
  });

  constructor() {
    effect(() => {
      if (this.feature() !== null) {
        void this.load();
      }
    });
  }

  protected clinics(count: number): string {
    return clinicsWords(this.words, count, this.language.language());
  }

  protected async load(): Promise<void> {
    const feature = this.feature();
    if (feature === null) {
      return;
    }
    this.state.set('loading');
    try {
      const used = await this.features.whereUsed(feature.key);
      // A late answer for a feature no longer open is dropped.
      if (this.feature()?.key !== feature.key) {
        return;
      }
      this.used.set(used);
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }
}
