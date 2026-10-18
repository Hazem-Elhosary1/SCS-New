import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsBadgeComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsSkeletonComponent,
  ToastService,
} from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { endsIn } from '../clinics/clinic-words';
import { opsDate, opsDateTime, opsNumber, opsRelative } from '../i18n/ops-format';
import { DisablePlanDialog, DisablingPlan } from './disable-plan.dialog';
import { FEATURE_GROUPS, FeatureRow, FeaturesService } from './features.service';
import { includedValue, initials, money, planName, usersWords } from './plan-words';
import { PlanDetail, PlansService } from './plans.service';

/**
 * A plan's detail (spec 013, User Story 4 and the head's actions of User Story 5; FR-023; frames
 * `2016:963`/`2019:1004`): its information, what it includes grouped as the catalogue groups it,
 * its subscriptions and its clinics, in the frame's order. «الوحدات الإضافية» is spec 021's.
 */
@Component({
  selector: 'app-plan-detail-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    DisablePlanDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      margin-block: var(--scs-spacing-sm) var(--scs-spacing-lg);
    }
    .who {
      display: flex;
      flex: 1 1 280px;
      align-items: center;
      gap: var(--scs-spacing-md);
      min-inline-size: 0;
    }
    .who > div {
      min-inline-size: 0;
    }
    .mark {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: var(--scs-avatar-lg, 56px);
      block-size: var(--scs-avatar-lg, 56px);
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-avatar-text, var(--scs-color-brand-active));
      font-size: var(--scs-avatar-lg-text, 18px);
      font-weight: var(--scs-avatar-weight, 600);
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 700;
      line-height: 1.4;
      overflow-wrap: anywhere;
    }
    .under {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
      /* A long code, derived from a long English name, breaks rather than widens the page. */
      overflow-wrap: anywhere;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-sm);
    }
    .actions button {
      min-block-size: 40px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-md);
      font-size: var(--scs-text-body);
    }
    .danger-outline {
      border-color: var(--scs-color-feedback-danger-icon);
      color: var(--scs-color-feedback-danger-text);
    }
    scs-card {
      display: block;
      margin-block-end: var(--scs-spacing-md);
    }
    dl {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      column-gap: var(--scs-spacing-lg);
      margin: 0;
    }
    dl > div {
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle);
    }
    dt {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    dd {
      margin: var(--scs-spacing-2) 0 0;
      font-size: var(--scs-text-body);
      overflow-wrap: anywhere;
    }
    .note {
      margin: 0 0 var(--scs-spacing-md);
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .included {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      column-gap: var(--scs-spacing-xl, 32px);
      align-items: start;
    }
    .group h3 {
      margin: var(--scs-spacing-md) 0 var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      font-weight: 400;
      color: var(--scs-color-text-secondary);
    }
    .group ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .group li {
      display: flex;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding-block: var(--scs-spacing-sm);
      border-block-end: 1px solid var(--scs-color-border-subtle);
      font-size: var(--scs-text-body-sm);
    }
    .tick {
      color: var(--scs-color-feedback-success-icon);
      font-weight: 600;
    }
    .closing {
      margin: var(--scs-spacing-lg) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .numbers {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: var(--scs-spacing-12);
    }
    .numbers > div {
      padding: var(--scs-spacing-md);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
    }
    .numbers strong {
      display: block;
      font-size: var(--scs-text-title-sm);
      font-weight: 500;
    }
    .numbers span {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .clinics {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .clinics li {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle);
    }
    .clinics .state {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .empty {
      margin: 0;
      color: var(--scs-color-text-secondary);
    }
    @media (max-width: 720px) {
      dl,
      .included {
        grid-template-columns: minmax(0, 1fr);
      }
      .numbers {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.plans.plan.form.crumbs' | transloco"
      [crumbs]="[
        { label: 'app.plans.plan.section' | transloco, link: '/plans' },
        { label: 'app.plans.plan.detail.crumb' | transloco },
      ]"
    />

    @switch (state()) {
      @case ('loading') {
        <scs-card
          ><scs-skeleton
            data-testid="detail-loading"
            [rows]="8"
            [label]="'app.plans.plan.form.loading' | transloco"
        /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state
            context="error"
            data-testid="detail-failed"
            [title]="'app.plans.plan.form.failedTitle' | transloco"
            [text]="'app.plans.plan.form.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="detail-retry" (click)="load()">
              {{ 'app.plans.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (plan(); as p) {
          <div class="head" data-testid="plan-detail">
            <div class="who">
              <span class="mark" aria-hidden="true">{{ letters(p) }}</span>
              <div>
                <h1 data-testid="detail-title"><bdi>{{ name(p) }}</bdi></h1>
                <p class="under" data-testid="detail-under">
                  {{ 'app.plans.plan.detail.codeAdded' | transloco: { code: p.code, date: date(p.createdAt) } }}
                </p>
              </div>
            </div>
            @if (manages()) {
              <div class="actions" data-testid="detail-actions">
                <button type="button" scsButton="secondary" data-testid="detail-edit" (click)="edit(p)">
                  {{ 'app.plans.plan.detail.edit' | transloco }}
                </button>
                @if (p.state === 'active') {
                  <button
                    type="button"
                    scsButton="secondary"
                    class="danger-outline"
                    data-testid="detail-disable"
                    (click)="disabling.set(asDisabling(p))"
                  >
                    {{ 'app.plans.plan.detail.disable' | transloco }}
                  </button>
                } @else {
                  <button
                    type="button"
                    scsButton="secondary"
                    data-testid="detail-enable"
                    [busy]="enabling()"
                    (click)="enable(p)"
                  >
                    {{ 'app.plans.plan.detail.enable' | transloco }}
                  </button>
                }
              </div>
            }
          </div>

          <scs-card data-testid="plan-info" [heading]="'app.plans.plan.detail.info' | transloco">
            <dl>
              <div>
                <dt>{{ 'app.plans.plan.detail.monthly' | transloco }}</dt>
                <dd data-testid="info-monthly"><bdi>{{ pair(p.monthlyEgp, p.monthlyUsd) }}</bdi></dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.state' | transloco }}</dt>
                <dd>
                  <scs-badge [intent]="p.state === 'active' ? 'success' : 'neutral'" data-testid="info-state">{{
                    'app.plans.plan.state.' + p.state | transloco
                  }}</scs-badge>
                </dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.annual' | transloco }}</dt>
                <dd data-testid="info-annual">
                  <!-- The EGP annual price is the billed one: 0 there means no annual price (Q9). -->
                  @if (p.annualEgp === 0) {
                    {{ 'app.plans.plan.detail.noAnnual' | transloco }}
                  } @else {
                    <bdi>{{ pair(p.annualEgp, p.annualUsd) }}</bdi>
                  }
                </dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.added' | transloco }}</dt>
                <dd data-testid="info-added">{{ dateTime(p.createdAt) }}</dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.maxUsers' | transloco }}</dt>
                <dd data-testid="info-users">{{ users(p.users.limit) }}</dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.updated' | transloco }}</dt>
                <dd data-testid="info-updated">{{ ago(p.updatedAt) }}</dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.code' | transloco }}</dt>
                <dd data-testid="info-code"><bdi dir="ltr">{{ p.code }}</bdi></dd>
              </div>
              <div>
                <dt>{{ 'app.plans.plan.detail.description' | transloco }}</dt>
                <dd data-testid="info-description"><bdi>{{ p.description }}</bdi></dd>
              </div>
              @if (p.state === 'disabled') {
                <div>
                  <dt>{{ 'app.plans.plan.detail.reason' | transloco }}</dt>
                  <dd data-testid="info-reason"><bdi>{{ p.disabledReason ?? '—' }}</bdi></dd>
                </div>
              }
            </dl>
          </scs-card>

          <scs-card data-testid="plan-features" [heading]="'app.plans.plan.detail.features' | transloco">
            <p class="note">{{ 'app.plans.plan.detail.featuresNote' | transloco }}</p>
            @if (groups().length === 0) {
              <p class="empty" data-testid="features-none">
                {{ 'app.plans.plan.detail.noFeatures' | transloco }}
              </p>
            } @else {
              <div class="included">
                @for (column of columns(); track $index) {
                  <div>
                    @for (group of column; track group.key) {
                      <div class="group" data-testid="included-group">
                        <h3>{{ 'app.plans.group.' + group.key | transloco }}</h3>
                        <ul>
                          @for (item of group.items; track item.key) {
                            <li data-testid="included-row" [attr.data-key]="item.key">
                              <bdi>{{ item.name }}</bdi>
                              <bdi [class.tick]="item.value === '✓'" data-testid="included-value">{{
                                item.value
                              }}</bdi>
                            </li>
                          }
                        </ul>
                      </div>
                    }
                  </div>
                }
              </div>
            }
            <p class="closing" data-testid="features-closing">
              {{ 'app.plans.plan.detail.excluded' | transloco: { count: number(p.excluded) } }}
              @if (manages()) {
                {{ 'app.plans.plan.detail.excludedEdit' | transloco }}
              }
            </p>
          </scs-card>

          <scs-card
            data-testid="plan-subscriptions"
            [heading]="'app.plans.plan.detail.subscriptions' | transloco"
          >
            <div class="numbers">
              <div>
                <strong data-testid="sub-clinics">{{ number(p.subscriptions.clinics) }}</strong>
                <span>{{ 'app.plans.plan.detail.subClinics' | transloco }}</span>
              </div>
              <div>
                <strong data-testid="sub-revenue"><bdi>{{ egp(p.subscriptions.monthlyRevenue) }}</bdi></strong>
                <span>{{ 'app.plans.plan.detail.revenue' | transloco }}</span>
              </div>
              <div>
                <strong data-testid="sub-new"><bdi dir="ltr">+{{ number(p.subscriptions.newThisMonth) }}</bdi></strong>
                <span>{{ 'app.plans.plan.detail.newSubs' | transloco }}</span>
              </div>
              <div>
                <strong data-testid="sub-trial">{{ number(p.subscriptions.onTrial) }}</strong>
                <span>{{ 'app.plans.plan.detail.trial' | transloco }}</span>
              </div>
            </div>
          </scs-card>

          <scs-card data-testid="plan-clinics" [heading]="'app.plans.plan.detail.clinicsTitle' | transloco">
            @if (p.clinics.length === 0) {
              <p class="empty" data-testid="clinics-none">
                {{ 'app.plans.plan.detail.noClinics' | transloco }}
              </p>
            } @else {
              <ul class="clinics">
                @for (clinic of p.clinics; track clinic.id) {
                  <li data-testid="clinic-row">
                    <bdi>{{ name(clinic) }}</bdi>
                    <span class="state"><bdi>{{ clinicState(clinic) }}</bdi></span>
                  </li>
                }
              </ul>
            }
          </scs-card>
        }
      }
    }

    <app-disable-plan
      [plan]="disabling()"
      (closed)="disabling.set(null)"
      (done)="disabling.set(null); load()"
    />
  `,
})
export class PlanDetailPage {
  private readonly plans = inject(PlansService);
  private readonly features = inject(FeaturesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly access = inject(OpsAccess);
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });

  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly plan = signal<PlanDetail | null>(null);
  private readonly catalogue = signal<readonly FeatureRow[]>([]);
  protected readonly disabling = signal<DisablingPlan | null>(null);
  protected readonly enabling = signal(false);

  protected readonly manages = computed(() => this.access.can(OpsCan.billingManagePlans));

  /** The included features by the catalogue's groups and order, each with its value. */
  protected readonly groups = computed(() => {
    this.spoken();
    const plan = this.plan();
    if (plan === null) {
      return [];
    }
    const language = this.language.language();
    const limits = new Map(plan.features.map((row) => [row.key, row.limit]));
    const known = this.catalogue().filter((feature) => limits.has(feature.key));
    return FEATURE_GROUPS.map((key) => ({
      key,
      items: known
        .filter((feature) => feature.group === key)
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((feature) => ({
          key: feature.key,
          name: language === 'ar' ? feature.nameAr : feature.nameEn,
          value: includedValue(this.words, feature, limits.get(feature.key) ?? null, language),
        })),
    })).filter((group) => group.items.length > 0);
  });

  /** Two columns as drawn: the groups shared out in order, so neither runs far longer. */
  protected readonly columns = computed(() => {
    const groups = this.groups();
    const rows = (list: typeof groups) => list.reduce((sum, group) => sum + group.items.length + 1, 0);
    let split = 0;
    while (split < groups.length && rows(groups.slice(0, split + 1)) <= rows(groups) / 2) {
      split++;
    }
    split = Math.max(1, split);
    return [groups.slice(0, split), groups.slice(split)].filter((column) => column.length > 0);
  });

  protected readonly number = (value: number): string => opsNumber(value, this.language.language());

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    if (this.plan() === null) {
      this.state.set('loading');
    }
    try {
      const [plan, catalogue] = await Promise.all([this.plans.read(this.id), this.features.list()]);
      this.plan.set(plan);
      this.catalogue.set(catalogue.items);
      this.state.set('ready');
    } catch {
      if (this.plan() === null) {
        this.state.set('failed');
      } else {
        this.toasts.show(this.words.translate('app.plans.plan.form.failedTitle'), '', 'danger');
      }
    }
  }

  protected name(item: { nameAr: string; nameEn: string }): string {
    return planName(item, this.language.language());
  }

  protected letters(plan: PlanDetail): string {
    return initials(this.name(plan), this.language.language());
  }

  /** «تجربة · تنتهي خلال 4 أيام» while in trial, as the frame draws it (spec 015); the state alone otherwise. */
  protected clinicState(clinic: { stateAr: string; stateEn: string; trialEndsAt?: string | null }): string {
    const language = this.language.language();
    const state = language === 'ar' ? clinic.stateAr : clinic.stateEn;
    if (!clinic.trialEndsAt) {
      return state;
    }
    const when = endsIn(this.words, clinic.trialEndsAt, Date.now(), language);
    return `${state} · ${this.words.translate('app.plans.plan.detail.trialEnds', { when })}`;
  }

  protected date(value: string): string {
    return opsDate(value, this.language.language());
  }

  protected dateTime(value: string): string {
    return opsDateTime(value, this.language.language());
  }

  protected ago(value: string): string {
    return opsRelative(value, Date.now(), this.language.language());
  }

  protected egp(value: number): string {
    this.spoken();
    return money(this.words, value, 'egp', this.language.language());
  }

  /** «<المبلغ> ج.م · <المبلغ> دولار» (§5-46). */
  protected pair(egp: number, usd: number): string {
    this.spoken();
    const language = this.language.language();
    return this.words.translate('app.plans.plan.money.pair', {
      egp: money(this.words, egp, 'egp', language),
      usd: money(this.words, usd, 'usd', language),
    });
  }

  protected users(limit: number | null): string {
    this.spoken();
    return usersWords(this.words, limit, this.language.language());
  }

  protected asDisabling(plan: PlanDetail): DisablingPlan {
    return {
      id: plan.id,
      nameAr: plan.nameAr,
      nameEn: plan.nameEn,
      clinics: plan.subscriptions.clinics,
    };
  }

  protected edit(plan: PlanDetail): void {
    void this.router.navigateByUrl(`/plans/${plan.id}/edit`);
  }

  /** «تفعيل الباقة» has no dialog (§5-45). */
  protected async enable(plan: PlanDetail): Promise<void> {
    this.enabling.set(true);
    try {
      await this.plans.enable(plan.id);
      this.toasts.show(this.words.translate('app.plans.plan.disable.enabled'));
      await this.load();
    } catch {
      this.toasts.show(this.words.translate('app.plans.plan.disable.enableFailed'), '', 'danger');
    } finally {
      this.enabling.set(false);
    }
  }
}
